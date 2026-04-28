/*
 * This file is part of the Adoracion project (https://github.com/0r05c0/Adoracion).
 * Copyright (C) 2026 Matias Orosco 
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * See the LICENSE file distributed with this project for full terms.
 */
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using Adoracion.Models; // Added for MediaFile and ScreenInfo
using Adoracion.Helpers;
using Adoracion.Services;

namespace Adoracion
{
    public partial class Main : Window
    {
        private ObservableCollection<MediaFile> mediaFiles;
        private MediaFile? selectedFile;
        private DispatcherTimer updateTimer;
        private bool isPlaying = false;
        private bool _isActuallyClosing = false;
        private SplashScreenWindow? _splash;
        private bool isDraggingSlider = false;
        private MediaScreen? mediaScreen;
        private int currentPlaylistIndex = -1;
        private bool isTransitioning = false;
        private string _currentPlaceholder = "";
        private System.Windows.Point _startPoint;
        private System.Windows.Controls.ListViewItem? _dragItemAbove;
        private System.Windows.Controls.ListViewItem? _dragItemBelow;
        private MediaFile? _draggedData;
        private int _lastVolume = 100;
        private List<System.Windows.Shapes.Rectangle> visualizerBars = new List<System.Windows.Shapes.Rectangle>();
        private DispatcherTimer visualizerTimer;
        private CancellationTokenSource? _playCts;
        private CancellationTokenSource? _libraryRefreshCts;
        
        private double _normalLeft;
        private double _normalTop;
        private double _normalWidth;
        private double _normalHeight;

        private bool ShouldCrossfade => CrossfadeToggle?.IsChecked == true && isPlaying && !isTransitioning;

        public static readonly DependencyProperty IsMaximizedViewProperty =
            DependencyProperty.Register("IsMaximizedView", typeof(bool), typeof(Main), new PropertyMetadata(false));

        public bool IsMaximizedView
        {
            get => (bool)GetValue(IsMaximizedViewProperty);
            set => SetValue(IsMaximizedViewProperty, value);
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string? psz1, string? psz2);
        private Random rand = new Random();
        private List<int> _shuffleQueue = new List<int>();
        private int _shuffleQueuePointer = -1;

        public ObservableCollection<string> HymnFiles { get; set; } = new ObservableCollection<string>();
        // This list will now store full paths
        private List<string> allHymnFiles = new List<string>();

        public Main()
        {
            _splash = new SplashScreenWindow();
            _splash.Show();
            
            // Force a render pass so the Splash and its ProgressBar appear and start animating immediately
            _splash.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => { }));
            _splash.UpdateStatus(TranslationHelper.GetString("Splash_InitUI", "Initializing UI..."));

            InitializeComponent();
            mediaFiles = new ObservableCollection<MediaFile>();
            AppSettingsService.SettingChanged += OnSettingChanged;
            TranslationHelper.LanguageChanged += TranslationHelper_LanguageChanged;
            this.Closing += Main_Closing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource? source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                DriveService.Instance.Initialize(source);
                DriveService.Instance.DriveChanged += OnDriveChanged;
            }
            DriveService.Instance.RefreshDrives();
        }

        private void OnDriveChanged(bool isArrival)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateLibraryTabs();
                RefreshLibraryList();

                // Auto-select the USB tab when a device is connected
                if (isArrival && DriveService.Instance.SelectedDrive != null)
                {
                    LibraryTabs.SelectedIndex = LibraryTabs.Items.Count - 1;
                }
            }));
        }

        private void InitializeMonitors()
        {
            MonitorComboBox.SelectionChanged -= MonitorComboBox_SelectionChanged;
            MonitorComboBox.Items.Clear();            
            
            string savedScreen = AppSettingsService.GetSetting("SelectedScreen", "");

            if (!ScreenService.Instance.IsMultipleScreens())
            {
                MonitorComboBox.IsEnabled = false;
                MonitorComboBox.ToolTip = TranslationHelper.GetString("Tooltip_NoSecondaryMonitor", "A secondary monitor is not available");
            }
            else
            {
                MonitorComboBox.IsEnabled = true;
                MonitorComboBox.ToolTip = null;
            }

            var screens = ScreenService.Instance.GetAllScreens();
            foreach (var screen in screens)
            {
                var item = new ComboBoxItem
                {
                    Content = screen.DisplayName, //$"{screen.DeviceName} ({(screen.Primary ? TranslationHelper.GetString("Label_Primary", "Primary") : TranslationHelper.GetString("Label_Secondary","Secondary"))})",
                    Tag = screen.DeviceName,
                };
                MonitorComboBox.Items.Add(item);

                if (screen.DeviceName == savedScreen)
                {
                    MonitorComboBox.SelectedItem = item;
                }
            }

            if (MonitorComboBox.SelectedItem == null && MonitorComboBox.Items.Count > 0)
            {
                MonitorComboBox.SelectedIndex = 0;
            }

            MonitorComboBox.SelectionChanged += MonitorComboBox_SelectionChanged;
        }

        private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MonitorComboBox.SelectedItem is ComboBoxItem item)
            {
                string deviceName = item.Tag as string;
                AppSettingsService.SetSetting("SelectedScreen", deviceName);
            }
        }

        private void ShowMediaScreen()
        {
            if (mediaScreen == null)
            {
                mediaScreen = new MediaScreen(PlaybackService.Instance.Player!);
                mediaScreen.PlayPauseClicked += (s, e) => 
                    Dispatcher.Invoke(() => PlayPauseButton_Click(this, new RoutedEventArgs()));
                mediaScreen.CloseRequested += (s, e) => 
                    Dispatcher.Invoke(() => StopButton_Click(this, new RoutedEventArgs()));
                mediaScreen.Closed += (s, e) => mediaScreen = null;
                mediaScreen.Show();
            }
        }    
        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (!PlaybackService.Instance.IsPlaying || isDraggingSlider)
                return;

            // If current media is an image, maintain the static slider state
            if (selectedFile != null && IsImageFile(selectedFile.FilePath))
            {
                ProgressSlider.IsEnabled = false;
                ProgressSlider.Value = 97;
                TimeLeftTextBlock.Text = TranslationHelper.GetString("Time_Format", "--:--");
                return;
            }

            ProgressSlider.IsEnabled = true;
            var length = PlaybackService.Instance.GetDuration();
            var position = PlaybackService.Instance.GetTime();
            ProgressSlider.Maximum = length / 1000.0;
            ProgressSlider.Value = position / 1000.0;

            TimeSpan timeLeft = TimeSpan.FromMilliseconds(length - position);
            TimeLeftTextBlock.Text = $"{TranslationHelper.GetString("Label_TimeRemainingPrefix", "-")}{FormatTime(timeLeft)}";

            // Crossfade detection: Trigger 3 seconds before the end
            if (ShouldCrossfade && length > 0)
            {
                long remainingMs = length - position;
                if (remainingMs <= 3000 && remainingMs > 500)
                {
                    HandleCrossfadeTransition(() => MoveToNext(wrapAround: false));
                }
            }
        }

        private async void HandleCrossfadeTransition(Action transitionAction)
        {
            if (isTransitioning) return;
            isTransitioning = true;

            // Use the existing PlayMediaFile logic to coordinate the fade.
            // We fade out manually here, then trigger the next file which handles the fade-in.
            int currentVol = (int)VolumeSlider.Value;
            _ = Task.Run(async () => {
                for (int i = currentVol; i >= 0; i -= 10) {
                    PlaybackService.Instance.SetVolume(i);
                    await Task.Delay(50);
                }
                Dispatcher.Invoke(() => transitionAction.Invoke());
            });
        }

        private async void FadeOutAndStop()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            // Start visual transition immediately so it runs alongside audio fade
            if (mediaScreen != null) mediaScreen.ResetToIdleState();

            int originalVolume = (int)VolumeSlider.Value;
            // Fade out until 1 as suggested to keep the audio channel "warm" 
            // and avoid the "silent start" bug on next play
            for (int i = originalVolume; i >= 1; i -= 10)
            {
                PlaybackService.Instance.SetVolume(i);
                await Task.Delay(60);
            }
            StopMedia(forceInstant: true);            
        }

        private void InitializeVisualizer()
        {
            if (VisualizerContainer == null) return;
            VisualizerContainer.Children.Clear();
            visualizerBars.Clear();

            for (int i = 0; i < 15; i++)
            {
                var bar = new System.Windows.Shapes.Rectangle
                {
                    Width = 3,
                    Height = 5,
                    Margin = new Thickness(1),
                    RadiusX = 1.5,
                    RadiusY = 1.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                bar.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Primary");
                visualizerBars.Add(bar);
                VisualizerContainer.Children.Add(bar);
            }
        }

        private void OnSettingChanged(string key)
        {
            if (key == "SelectedScreen")
            {
                Dispatcher.Invoke(() => MoveToOppositeScreen());
            }
        }

        private void MoveToOppositeScreen()
        {
            ScreenService.Instance.MoveWindowToUIScreen(this, fillScreen: true, (isMax) => IsMaximizedView = isMax);
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
                IsMaximizedView = ScreenService.Instance.ToggleCustomMaximize(
                this, 
                IsMaximizedView, 
                new Rect(_normalLeft, _normalTop, _normalWidth, _normalHeight));
        }

        private void VisualizerTimer_Tick(object? sender, EventArgs e)
        {
            if (!isPlaying) return;
            foreach (var bar in visualizerBars)
            {
                bar.Height = rand.Next(5, 35);
            }
        }

        private void ResetVisualizer()
        {
            foreach (var bar in visualizerBars)
                bar.Height = 5;
        }
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
            
            // Refresh tabs in case folders were changed
            UpdateLibraryTabs();
            RefreshLibraryList();
        }

        private void LibraryRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is string filePath)
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    FavoritesService.RemoveFavorite(filePath);

                    // Sync the favorite status in the current playlist if the item is present
                    var playlistItem = mediaFiles.FirstOrDefault(m => m.FilePath == filePath);
                    if (playlistItem != null)
                    {
                        playlistItem.IsFavorite = false;
                    }

                    // Refresh the library view to reflect the removal
                    RefreshLibraryList();
                }
            }
        }

        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            // Opens settings which defaults to the Media Folders section
            SettingsButton_Click(sender, e);
        }

        private object CreateUSBHeader()
        {
            var viewbox = new Viewbox { Width = 18, Height = 18 };
            var path = new System.Windows.Shapes.Path 
            { 
                Data = (Geometry)FindResource("USBIcon"),
                Stretch = Stretch.Uniform
            };
            path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "TextSecondary");
            viewbox.Child = path;
            return viewbox;
        }

        private void UpdateLibraryTabs()
        {
            if (LibraryTabs == null) return;
            
            LibraryTabs.Items.Clear();

            // Fixed Tabs
            var localTab = new TabItem 
            { 
                Header = TranslationHelper.GetString("Tab_Local", "Local"),
                ContextMenu = (ContextMenu)this.FindResource("LocalTabContextMenu")
            };
            localTab.PreviewMouseLeftButtonDown += (s, e) => {
                if (LibraryTabs.SelectedItem == localTab)
                {
                    localTab.ContextMenu.PlacementTarget = localTab;
                    localTab.ContextMenu.DataContext = localTab.DataContext; // Explicitly set DataContext
                    localTab.ContextMenu.IsOpen = true;
                    e.Handled = true;
                }
            };

            LibraryTabs.Items.Add(localTab);
            LibraryTabs.Items.Add(new TabItem { Header = TranslationHelper.GetString("Tab_Favorites", "Favorites") });

            // Custom Tabs from Settings
            string foldersJson = AppSettingsService.GetSetting("MediaFolders", "[]");
            int customFolderCount = 0;
            try
            {
                var folderPaths = JsonSerializer.Deserialize<List<string>>(foldersJson);
                if (folderPaths != null)
                {
                    customFolderCount = folderPaths.Count;
                    foreach (var path in folderPaths.Take(3))
                    {
                        LibraryTabs.Items.Add(new TabItem
                        { 
                            Header = FileService.Instance.GetFileName(path), 
                            Tag = path 
                        });
                    }
                }
            }
            catch { }

            // Add Flash Drive Tab if available
            if (DriveService.Instance.SelectedDrive != null)
            {
                var usbTab = new TabItem
                {
                    Header = CreateUSBHeader(),
                    Tag = DriveService.Instance.SelectedDrive.RootDirectory.FullName,
                    ToolTip = $"{DriveService.Instance.SelectedDrive.VolumeLabel} ({DriveService.Instance.SelectedDrive.Name})"
                };

                if (DriveService.Instance.RemovableDrives.Count > 1)
                {
                    var menu = new ContextMenu { Style = (Style)System.Windows.Application.Current.FindResource("ModernContextMenuStyle") };
                    foreach (var drive in DriveService.Instance.RemovableDrives)
                    {
                        var mi = new MenuItem { Header = $"{drive.VolumeLabel} ({drive.Name})", IsCheckable = true, IsChecked = drive.Name == DriveService.Instance.SelectedDrive.Name,
                                                Style = (Style)System.Windows.Application.Current.FindResource("ModernMenuItemStyle")
                        };
                        mi.Click += (s, e) => {
                            DriveService.Instance.SelectedDrive = drive;
                            UpdateLibraryTabs();
                            foreach (TabItem item in LibraryTabs.Items)
                                if (item.Tag?.ToString() == DriveService.Instance.SelectedDrive.RootDirectory.FullName) { LibraryTabs.SelectedItem = item; break; }
                            RefreshLibraryList();
                        };
                        menu.Items.Add(mi);
                    }
                    usbTab.ContextMenu = menu;
                    usbTab.PreviewMouseLeftButtonDown += (s, e) => {
                    if (LibraryTabs.SelectedItem == usbTab && DriveService.Instance.RemovableDrives.Count > 1)
                        {
                            usbTab.ContextMenu.IsOpen = true;
                            e.Handled = true;
                        }
                    };
                }
                LibraryTabs.Items.Add(usbTab);
            }

            // Show add button only if we have room for more custom tabs (max 3)
            if (AddTabButton != null)
            {
                AddTabButton.Visibility = customFolderCount < 3 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            // Do not automatically move to next if the current media is an image.
            // Images should stay on screen indefinitely until the user manually triggers a skip or play.
            if (selectedFile != null && IsImageFile(selectedFile.FilePath)) {
                LoggingService.Instance.Log("EndReached suppressed: Current media is an image.");
                return;
            }

            if (isTransitioning || mediaFiles.Count == 0)
                return;

            LoggingService.Instance.Log("EndReached detected. Advancing playlist.");
            Dispatcher.BeginInvoke(new Action(() => MoveToNext(wrapAround: false)));
        }

        private void MoveToNext(bool wrapAround)
        {
            if (mediaFiles.Count == 0) return;

            if (ShuffleToggle?.IsChecked == true && mediaFiles.Count > 0)
            {
                currentPlaylistIndex = GetNextShuffleIndex();
            }
            else
            {
                if (currentPlaylistIndex < mediaFiles.Count - 1)
                {
                    currentPlaylistIndex++;
                }
                else if (wrapAround)
                {
                    currentPlaylistIndex = 0;
                }
                else
                {
                    currentPlaylistIndex = -1;
                }
            }

            if (currentPlaylistIndex >= 0 && currentPlaylistIndex < mediaFiles.Count)
            {
                var nextFile = mediaFiles[currentPlaylistIndex];
                PlaylistView.SelectedItem = nextFile;
                LoggingService.Instance.Log($"Advancing to next index: {currentPlaylistIndex}");
                PlayMediaFile(nextFile);
            }
            else
            {
                StopMedia();
            }
        }

        private void MoveToPrevious()
        {
            if (mediaFiles.Count == 0) return;

            currentPlaylistIndex--;
            if (currentPlaylistIndex < 0)
            {
                currentPlaylistIndex = mediaFiles.Count - 1;
            }

            var prevFile = mediaFiles[currentPlaylistIndex];
            PlaylistView.SelectedItem = prevFile;
            PlayMediaFile(prevFile);
        }

        private void Main_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isActuallyClosing)
            {
                e.Cancel = true;
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.3))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (s, ev) =>
                {
                    _isActuallyClosing = true;
                    this.Close();
                };
                this.BeginAnimation(OpacityProperty, fadeOut);
                return;
            }
            updateTimer?.Stop();
            PlaybackService.Instance.Dispose();
             AppSettingsService.SettingChanged -= OnSettingChanged;
              mediaFiles.Clear(); // Explicitly clear the playlist collection
            mediaScreen?.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private void DragMove_Handler(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private async Task LoadHymnFilesAsync()
        {
            try
            {
                LibraryLoadingState.Visibility = Visibility.Visible;

                var fileList = await FileService.Instance.GetHymnFilesAsync();
                
                // Sort by file name after getting the full paths
                var list = fileList.Select(f => FileService.Instance.GetFileName(f)).ToList();
                list.Sort((s1, s2) => StrCmpLogicalW(s1, s2));
                // Re-map to full paths for allHymnFiles
                fileList = fileList.OrderBy(f => FileService.Instance.GetFileName(f), new NaturalStringComparer()).ToList();                Dispatcher.Invoke(() =>
                {
                    HymnFiles.Clear();
                    allHymnFiles.Clear();
                    foreach (var filePath in fileList)
                    {
                        allHymnFiles.Add(filePath);
                        HymnFiles.Add(filePath);
                    }
                
                // Set DataContext of the Local tab to the absolute Hymns path (exe location /Hymns)
                if (LibraryTabs.Items.Count > 0 && LibraryTabs.Items[0] is TabItem local)
                {
                    local.DataContext = FileService.Instance.CombinePath(AppDomain.CurrentDomain.BaseDirectory, "Hymns");
                }
                LibraryLoadingState.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error loading hymn files: {ex.Message}");
                Dispatcher.Invoke(() => LibraryLoadingState.Visibility = Visibility.Collapsed);
            }
        }
        
        private void ListBoxItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.ContextMenu != null)
            {
                item.ContextMenu.DataContext = item.DataContext;
                LoggingService.Instance.Log($"ListBoxItem_PreviewMouseRightButtonDown: Setting ContextMenu.DataContext to '{item.DataContext ?? "null"}' for item '{item.DataContext as string ?? "N/A"}'");
            }
        }

        /// <summary>
        /// Implements Fisher-Yates shuffle to guarantee all songs play once before repeating.
        /// </summary>
        private int GetNextShuffleIndex()
        {
            if (mediaFiles.Count == 0) return -1;

            // If the queue is empty or the playlist size changed, regenerate the "deck"
            if (_shuffleQueue.Count != mediaFiles.Count)
            {
                _shuffleQueue = Enumerable.Range(0, mediaFiles.Count).ToList();
                _shuffleQueuePointer = -1;

                // Fisher-Yates Shuffle
                for (int i = _shuffleQueue.Count - 1; i > 0; i--)
                {
                    int j = rand.Next(i + 1);
                    int temp = _shuffleQueue[i];
                    _shuffleQueue[i] = _shuffleQueue[j];
                    _shuffleQueue[j] = temp;
                }
            }

            _shuffleQueuePointer++;

            // If we reached the end of the deck, reshuffle for the next cycle
            if (_shuffleQueuePointer >= _shuffleQueue.Count)
            {
                _shuffleQueuePointer = -1;
                return GetNextShuffleIndex();
            }

            return _shuffleQueue[_shuffleQueuePointer];
        }
                
        private void FolderFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ClearFilterButton != null)
            {
                ClearFilterButton.Visibility = (string.IsNullOrEmpty(FolderFilterBox.Text) || FolderFilterBox.Text == _currentPlaceholder) 
                    ? Visibility.Collapsed : Visibility.Visible;
            }
            RefreshLibraryList();
        }

        /// <summary>
        /// Handles the Enter key in the search box to quickly play the first filtered result.
        /// </summary>
        private async void FolderFilterBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Do nothing if input is empty or just the placeholder
                if (string.IsNullOrWhiteSpace(FolderFilterBox.Text) || FolderFilterBox.Text == _currentPlaceholder)
                {
                    e.Handled = true;
                    return;
                }

                string? firstResult = HymnFiles.FirstOrDefault();
                if (!string.IsNullOrEmpty(firstResult))
                {
                    await GetOrAddLibraryFileAsync(firstResult);
                }
                
                // Mark as handled to prevent the "ding" sound
                e.Handled = true;
            }
        }

        private void LibraryTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is System.Windows.Controls.TabControl)
            {
                if (FolderFilterBox != null)
                {
                    // Clear search, restore placeholder, and reset color when switching tabs
                    FolderFilterBox.Text = _currentPlaceholder;
                    FolderFilterBox.SetResourceReference(ForegroundProperty, "TextSecondary");
                }
                RefreshLibraryList();
                RefreshUIText(); // Update the empty state label text when tab changes
            }
        }

        private async void RefreshLibraryList()
        {
            if (FolderFilterBox == null || HymnFiles == null || LibraryTabs == null || LibraryLoadingState == null) return;

            // Cancel any existing refresh task
            _libraryRefreshCts?.Cancel();
            _libraryRefreshCts = new CancellationTokenSource();
            var token = _libraryRefreshCts.Token;

            LibraryLoadingState.Visibility = Visibility.Visible;

            string placeholder = TranslationHelper.GetString("Placeholder_SearchLibrary", "Search library...");
            string filter = (FolderFilterBox.Text == placeholder) ? string.Empty : FolderFilterBox.Text.Trim().ToLower();

            int selectedIndex = LibraryTabs.SelectedIndex;
            TabItem? selectedTab = LibraryTabs.SelectedItem as TabItem;

            // CRITICAL: Capture necessary data on the UI thread before starting the background task.
            // Accessing selectedTab.Tag or allHymnFiles inside Task.Run causes cross-thread exceptions.
            string? directoryPath = selectedTab?.Tag as string;
            List<string> localFilesSnapshot = allHymnFiles.ToList();
            List<string> favoritesSnapshot = FavoritesService.GetFavorites().ToList();

            try
            {
                var filteredList = await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return new List<string>();

                    IEnumerable<string> source = Enumerable.Empty<string>();
                    if (selectedIndex == 0) // Local
                    {
                        source = localFilesSnapshot; 
                    }
                    else if (selectedIndex == 1) // Favorites
                    {
                        source = favoritesSnapshot;
                    }
                    else if (selectedIndex > 1 && !string.IsNullOrEmpty(directoryPath))
                    {
                        source = FileService.Instance.GetMediaFilesFromDirectory(directoryPath, recursive: true);
                    }

                    var filtered = source.Where(f => string.IsNullOrEmpty(filter) || FileService.Instance.GetFileName(f).ToLower().Contains(filter)).ToList();
                    filtered.Sort((s1, s2) => StrCmpLogicalW(FileService.Instance.GetFileName(s1), FileService.Instance.GetFileName(s2)));
                    return filtered;
                }, token);

                if (token.IsCancellationRequested) return;

                HymnFiles.Clear();
                foreach (var filePath in filteredList)
                    HymnFiles.Add(filePath);
            }
            catch (OperationCanceledException) { /* Task was superseded */ }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"RefreshLibraryList failed: {ex.Message}");
                HymnFiles.Clear();
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    LibraryLoadingState.Visibility = Visibility.Collapsed;
            }
        }

        private async void FolderExplorer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FolderExplorer.SelectedItem is string filePath) // filePath is already the full path
            {
                if (string.IsNullOrEmpty(filePath) || !FileService.Instance.FileExists(filePath)) return;

                if (!mediaFiles.Any(m => m.FilePath == filePath))
                {
                                MediaFile file = new MediaFile
                                {
                                    FilePath = filePath,
                                    Name = FileService.Instance.GetFileName(filePath), // Get file name from full path
                                    Duration = "", // Initialize as empty
                                    Index = mediaFiles.Count + 1,
                                    Type = MediaHelper.DetermineMediaType(filePath), // Set media type here
                                    IsFavorite = FavoritesService.IsFavorite(filePath) // Set initial favorite status

                                };
                                        
                                // If the file is an audio or video, get its duration. Use _mediaExtensionsWithDuration.
                                if (MediaHelper.MediaWithDurationExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                                { // This block is duplicated in GetOrAddLibraryFileAsync, consider refactoring
                                    var media = new Media(PlaybackService.Instance.LibVLC!, filePath, FromType.FromPath);
                                    var result = await media.Parse(MediaParseOptions.ParseLocal);
                                    using (media) 
                                    {
                                        if (result == MediaParsedStatus.Done && media.Duration > 0)
                                        {
                                            file.Duration = FormatTime(TimeSpan.FromMilliseconds(media.Duration));
                                        }
                                    }
                                }
                                mediaFiles.Add(file);
                                // Add to playlist and select it, but do not start playback automatically.
                                PlaylistView.SelectedItem = file;   
                }         
            }
        }

        private async void LibraryAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is string filePath) // filePath is now the full path
            {
                await GetOrAddLibraryFileAsync(filePath);
            }
        }

        private async void LibraryPlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is string filePath) // filePath is now the full path
            {
                await PlayLibraryFileAsync(filePath);
            }
        }

        private void OpenHymnsFolder_Click(object sender, RoutedEventArgs e)
        {
            // Hardcoded fallback for the Local tab to ensure it works immediately
            string hymnsPath = FileService.Instance.CombinePath(AppDomain.CurrentDomain.BaseDirectory, "Hymns");

            // Edge case: If the user deleted the folder while the app was open, recreate it now
            if (!FileService.Instance.DirectoryExists(hymnsPath))
            {
                LoggingService.Instance.Log($"OpenHymnsFolder_Click: Folder missing, recreating: {hymnsPath}");
                FileService.Instance.CreateDirectory(hymnsPath);
            }

            LoggingService.Instance.Log($"OpenHymnsFolder_Click: Opening hardcoded path: {hymnsPath}");
            FileService.Instance.OpenInExplorer(hymnsPath);
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                string? filePath = menuItem.DataContext as string;
                LoggingService.Instance.Log($"OpenInExplorer_Click: MenuItem.DataContext is '{filePath ?? "null"}'");

                // FilePath should now be correctly set by the ContextMenu's DataContext
                FileService.Instance.OpenInExplorer(filePath);
            }
        }

        /// <summary>
        /// Shared logic to resolve a library filename to a path, add it to playlist if missing, 
        /// and trigger immediate playback (with crossfade support).
        /// </summary>
        private async Task PlayLibraryFileAsync(string filePath) // Parameter is now full path
        {
            var fileToPlay = await GetOrAddLibraryFileAsync(filePath);
            if (fileToPlay == null) return;

            // Images should bypass crossfade logic as they have no audio and 
            // need specific timing to render the static frame correctly.
            // FIX: We check if the target is an image only to decide if we need a fade-IN.
            // We always allow a fade-OUT if current media is playing.
            bool isImage = IsImageFile(fileToPlay.FilePath);

            if (ShouldCrossfade)
            {
                HandleCrossfadeTransition(() => PlayMediaDirectly(fileToPlay));
            }
            else
            {
                PlayMediaDirectly(fileToPlay);
            }
        }


        /// <summary>
        /// Resolves a library filename and adds it to the playlist collection if it's not already there.
        /// Returns the MediaFile object for further actions (like playback).
        /// </summary>
        private async Task<MediaFile?> GetOrAddLibraryFileAsync(string filePath) // Parameter is now full path
        {
            LoggingService.Instance.Log($"GetOrAddLibraryFileAsync: Entry for {filePath}");
            // filePath is already the full path, no need to construct it.
            // The `fileName` variable is no longer needed here.


            if (string.IsNullOrEmpty(filePath)) return null;
            
            if (!FileService.Instance.FileExists(filePath))
            {
                // Localize the message and title for the ModernMessageBox
                string message = TranslationHelper.GetString("Error_FileNotFound", "The file could not be found at the specified path:") + Environment.NewLine + filePath;
                string title = TranslationHelper.GetString("Error_Title", "Error");
                ModernMessageBox.Show(message, title, MessageBoxButton.OK, this);
                return null;
            }

            var fileToPlay = mediaFiles.FirstOrDefault(m => m.FilePath == filePath);
            if (fileToPlay == null)
            {
                fileToPlay = new MediaFile
                {
                    FilePath = filePath,
                    Name = FileService.Instance.GetFileName(filePath), // Get file name from full path
                    Duration = TranslationHelper.GetString("Time_Format", "--:--"),
                    Index = mediaFiles.Count + 1, // Set media type here
                    Type = MediaHelper.DetermineMediaType(filePath),
                    IsFavorite = FavoritesService.IsFavorite(filePath)
                };

                if (MediaHelper.MediaWithDurationExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        var media = new Media(PlaybackService.Instance.LibVLC!, filePath, FromType.FromPath);
                        LoggingService.Instance.Log($"Parsing media for duration: {FileService.Instance.GetFileName(filePath)}");
                        var result = await media.Parse(MediaParseOptions.ParseLocal);

                        using (media)
                        {
                            if (result == MediaParsedStatus.Done && media.Duration > 0)
                            {
                                fileToPlay.Duration = FormatTime(TimeSpan.FromMilliseconds(media.Duration));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.Log($"Error parsing media duration for {FileService.Instance.GetFileName(filePath)}: {ex.Message}");
                    }
                }
                mediaFiles.Add(fileToPlay);
            }
            LoggingService.Instance.Log($"GetOrAddLibraryFileAsync: Exit for {FileService.Instance.GetFileName(filePath)}, returning {fileToPlay?.Name}");
            return fileToPlay;
        }        

        private void PlayMediaDirectly(MediaFile file)
        {
            currentPlaylistIndex = mediaFiles.IndexOf(file);
            PlaylistView.SelectedItem = file;
            PlayMediaFile(file);
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FolderFilterBox.Text = string.Empty;
            // When clearing, we usually want the user to type immediately
            FolderFilterBox.Focus(); 
            FolderFilterBox.SetResourceReference(ForegroundProperty, "TextLight");
        }

        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.DataContext is MediaFile file) // DataContext is MediaFile, not string
            {                
                file.IsFavorite = !file.IsFavorite; // Toggle the property first

                if (file.IsFavorite)
                    FavoritesService.AddFavorite(file.FilePath!);
                else
                    FavoritesService.RemoveFavorite(file.FilePath!);
                // Refresh UI if we are currently looking at the Favorites tab
                if ((LibraryTabs.SelectedItem as TabItem)?.Header.ToString() == TranslationHelper.GetString("Tab_Favorites", "Favorites"))
                    RefreshLibraryList();
            }
        }

        /// <summary>
        /// Clears all items from the current playlist.
        /// </summary>
        private void ClearPlaylist_Click(object sender, RoutedEventArgs e)
        {
            this.Focus();

            StopMedia();

            mediaFiles.Clear();
            currentPlaylistIndex = -1;
            selectedFile = null;
            TrackTitleTextBlock.Text = string.Empty;
        }

        private void RemovePlaylistItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.DataContext is MediaFile fileToRemove)
            {
                // If the file being removed is the currently selected/playing file
                if (selectedFile == fileToRemove)
                {
                    StopMedia();

                    selectedFile = null; // Clear selected file
                    TrackTitleTextBlock.Text = string.Empty;
                }

                mediaFiles.Remove(fileToRemove);

                // Re-index the remaining items in the playlist
                for (int i = 0; i < mediaFiles.Count; i++)
                {
                    mediaFiles[i].Index = i + 1;
                }

                // Adjust currentPlaylistIndex if the removed item was before it
                if (currentPlaylistIndex > mediaFiles.IndexOf(fileToRemove))
                {
                    currentPlaylistIndex--;
                }
            }
        }
        private void FolderFilterBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (FolderFilterBox.Text == _currentPlaceholder)
            {
                FolderFilterBox.Text = string.Empty;
            }
            FolderFilterBox.SetResourceReference(ForegroundProperty, "TextLight");
        }

        private void FolderFilterBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FolderFilterBox.Text))
            {
                FolderFilterBox.Text = _currentPlaceholder;
                FolderFilterBox.SetResourceReference(ForegroundProperty, "TextSecondary");
            }
        }

        private async void PlaylistView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistView.SelectedItem is MediaFile file)
            {
                if (string.IsNullOrEmpty(file.FilePath)) return;

                if (!FileService.Instance.FileExists(file.FilePath))
                {
                    file.IsMissing = true;
                    string message = TranslationHelper.GetString("Error_FileNotFound", "The file could not be found at the specified path:") + Environment.NewLine + file.FilePath;
                    string title = TranslationHelper.GetString("Error_Title", "Error");
                    ModernMessageBox.Show(message, title, MessageBoxButton.OK, this);
                    return;
                }
                file.IsMissing = false;

                bool isImage = IsImageFile(file.FilePath);

                if (ShouldCrossfade)
                {
                    HandleCrossfadeTransition(() =>
                    {
                        currentPlaylistIndex = mediaFiles.IndexOf(file);
                        PlaylistView.SelectedItem = file;
                        PlayMediaFile(file);
                    });
                }
                else
                {
                    currentPlaylistIndex = mediaFiles.IndexOf(file);
                    PlaylistView.SelectedItem = file;
                    PlayMediaFile(file);
                }
            }
        }

        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Focus(); // Restore focus to window
            
            // Ignore play/pause commands during an active track transition
            if (isTransitioning) return;

            var state = PlaybackService.Instance.Player?.State;
            if (state == VLCState.Playing)
            {
                if (CrossfadeToggle?.IsChecked == true)
                {
                    isTransitioning = true;
                    int originalVolume = (int)VolumeSlider.Value;
                    for (int i = originalVolume; i >= 0; i -= 10)
                    {
                        PlaybackService.Instance.SetVolume(i);
                        await Task.Delay(50);
                    }
                    PlaybackService.Instance.TogglePlayPause();
                    PlaybackService.Instance.SetVolume(0); // Ensure volume is 0 after fade-out to pause
                    isTransitioning = false;
                }
                else
                {
                    PlaybackService.Instance.TogglePlayPause();
                }
            }
            else if (state == VLCState.Paused)
            {
                if (CrossfadeToggle?.IsChecked == true)
                {
                    isTransitioning = true;
                    int targetVolume = (int)VolumeSlider.Value;
                    PlaybackService.Instance.SetVolume(0);
                    PlaybackService.Instance.TogglePlayPause();
                    await Task.Delay(100); // Small buffer for LibVLC to resume audio
                    for (int i = 0; i <= targetVolume; i += 10)
                    {
                        PlaybackService.Instance.SetVolume(i);
                        await Task.Delay(40);
                    }
                    PlaybackService.Instance.SetVolume(targetVolume);
                    isTransitioning = false;
                }
                else
                {
                    PlaybackService.Instance.TogglePlayPause();
                }
            }
            else
            {
                isTransitioning = false; // Reset flag on manual user action
                var file = PlaylistView.SelectedItem as MediaFile;
                if (file == null && mediaFiles.Count > 0)
                {
                    file = mediaFiles[0];
                    PlaylistView.SelectedItem = file; // This line is duplicated below, consider removing.
                }

                if (file != null)
                {
                    if (FileService.Instance.FileExists(file.FilePath))
                    {
                        file.IsMissing = false;
                        currentPlaylistIndex = mediaFiles.IndexOf(file); // This line is duplicated above, consider removing.
                        PlayMediaFile(file);
                    }
                    else
                    {
                        file.IsMissing = true;
                        string message = TranslationHelper.GetString("Error_FileNotFound", "The file could not be found at the specified path:") + Environment.NewLine + file.FilePath;
                        string title = TranslationHelper.GetString("Error_Title", "Error");
                        ModernMessageBox.Show(message, title, MessageBoxButton.OK, this);
                    }
                }
            }
        }

        private void SkipNextButton_Click(object sender, RoutedEventArgs e)
        {
            this.Focus(); // Restore focus to window so Space bar shortcut works reliably

            if (ShouldCrossfade)
            {
                HandleCrossfadeTransition(() => MoveToNext(wrapAround: true));
            }
            else
            {
                isTransitioning = false;
                MoveToNext(wrapAround: true);
            }
        }

        private void SkipPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            this.Focus(); // Restore focus to window

            if (ShouldCrossfade)
            {
                HandleCrossfadeTransition(() => MoveToPrevious());
            }
            else
            {
                isTransitioning = false;
                MoveToPrevious();
            }
        }

        private async void PlayMediaFile(MediaFile file)
        {
            _playCts?.Cancel(); // Cancel any pending setup (like the image pause timer) from the previous file
            var cts = new CancellationTokenSource();
            _playCts = cts;

            LoggingService.Instance.Log($"PlayMediaFile triggered for: {file.Name}");
            if (!FileService.Instance.FileExists(file.FilePath))
            {
                file.IsMissing = true;
                LoggingService.Instance.Log($"ERROR: File does not exist at path: {file.FilePath}");
                string message = TranslationHelper.GetString("Error_FileNotFound", "The file could not be found at the specified path:") + Environment.NewLine + file.FilePath;
                string title = TranslationHelper.GetString("Error_Title", "Error");
                ModernMessageBox.Show(message, title, MessageBoxButton.OK, this);
                return;
            }
            file.IsMissing = false;

            // Update playing status for playlist visualizer
            try
            {
                // 1. Update UI state immediately (Lightweight)
                foreach (var m in mediaFiles) m.IsPlaying = false;
                file.IsPlaying = true;
                selectedFile = file;
                TrackTitleTextBlock.Text = file.Name;

                // 2. DEBOUNCE: Wait 200ms before touching LibVLC. 
                // If the user clicks Next again, this task is cancelled here.
                await Task.Delay(200, cts.Token);

                // 3. Prepare slider for media type
                bool isImage = IsImageFile(file.FilePath);
                ProgressSlider.IsEnabled = !isImage;
                if (isImage)
                {
                    ProgressSlider.Minimum = 0;
                    ProgressSlider.Maximum = 200;
                    ProgressSlider.Value = 97;
                    TimeLeftTextBlock.Text = TranslationHelper.GetString("Time_Format", "--:--");
                }

                ShowMediaScreen();
                
                if (isImage)
                {   
                    // Ensure volume is muted immediately so we don't hear trailing video audio
                    // while the image is fading in on the media screen.
                    PlaybackService.Instance.SetVolume(0);
                    
                    // Delay the LibVLC stop to allow the native image to fade in over the video
                    _ = Task.Delay(1000, cts.Token).ContinueWith(t => 
                    {
                        if (t.IsCompletedSuccessfully && !cts.IsCancellationRequested)
                        {
                            PlaybackService.Instance.Stop();
                            // Small additional safety buffer to ensure LibVLC's internal 
                            // 'Stopped' event message has cleared the queue.
                            _ = Task.Delay(500).ContinueWith(_ => Dispatcher.Invoke(() => {
                                if (mediaScreen != null) mediaScreen.SuppressResetToIdleState = false;
                            }));
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());

                    // 2. Display the image natively on the media screen.
                    mediaScreen?.ShowPlaylistImage(file.FilePath);
                    
                    isPlaying = false;
                    UpdatePlayPauseIcon(false);
                    visualizerTimer.Stop();
                    ResetVisualizer();
                    updateTimer.Stop();
                    isTransitioning = false;
                }
                else
                {
                    LoggingService.Instance.Log($"PlayMediaFile: Awaiting PlayAsync for video/audio: {file.FilePath}");
                    await PlaybackService.Instance.PlayAsync(file.FilePath, cts.Token);

                    // 6. Handle volume. We offload this to a task that monitors for cancellation.
                    int targetVolume = (int)VolumeSlider.Value;
                    bool useCrossfade = CrossfadeToggle?.IsChecked == true;

                    _ = Task.Run(async () => 
                    {
                        // Small delay to let the engine initialize before we touch volume
                        await Task.Delay(300, cts.Token);
                        if (cts.IsCancellationRequested) return;

                        if (useCrossfade)
                        {
                            PlaybackService.Instance.SetVolume(0);
                            for (int i = 0; i <= targetVolume; i += 10)
                            {
                                if (cts.IsCancellationRequested) break;
                                PlaybackService.Instance.SetVolume(i);
                                await Task.Delay(40);
                            }
                        }
                        
                        if (!cts.IsCancellationRequested)
                        {
                            PlaybackService.Instance.SetVolume(targetVolume);
                            isTransitioning = false;
                        }
                    }, cts.Token);
                }
            }
            catch (OperationCanceledException) 
            { 
                LoggingService.Instance.Log($"PlayMediaFile: Task cancelled for {file.Name}"); 
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"PlayMediaFile Error: {ex.Message}");
            }            
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.Focus(); // Restore focus to window

            StopMedia();
        }

        private async void StopMedia(bool forceInstant = false)
        {
            if (!forceInstant && ShouldCrossfade)
            {
                FadeOutAndStop();
                return;
            }

            LoggingService.Instance.Log("StopMedia: Entry.");
            _playCts?.Cancel(); // Stop any pending image setup

            // Reset UI and state immediately
            selectedFile = null;
            TrackTitleTextBlock.Text = string.Empty;
            currentPlaylistIndex = -1;
            foreach (var m in mediaFiles) m.IsPlaying = false;

            // 1. Trigger the visual transition to idle (wallpaper/label) first.
            // This allows the overlay to fade in OVER the video before we stop the engine.
            if (mediaScreen != null)
            {
                mediaScreen.SuppressResetToIdleState = true;
                mediaScreen.ResetToIdleState();
            }

            if (PlaybackService.Instance.Player != null)
            {
                // Ensure volume is 0 after stopping, especially after a fade-out.
                // The volume will be restored to the slider value when playback starts again.
                PlaybackService.Instance.SetVolume(0);

                // 2. Wait for the MediaScreen transition (fade-in of wallpaper/label) to cover 
                // the video content before we actually destroy the VLC video surface.

                await Task.Delay(800);

                PlaybackService.Instance.Stop();
                updateTimer?.Stop();
                ProgressSlider.Value = 0;
                ProgressSlider.IsEnabled = true;
                TimeLeftTextBlock.Text = TranslationHelper.GetString("Time_Format", "--:--");
            }
            
            isTransitioning = false; // Reset flag at the end
            LoggingService.Instance.Log("StopMedia: Resetting media screen to idle state.");

            if (mediaScreen != null)
            {
                mediaScreen.SuppressResetToIdleState = false;

                // If in fallback mode (single monitor), close the media window automatically on stop
                if (!ScreenService.Instance.IsMultipleScreens())
                {
                    mediaScreen.Close();
                }
            }
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (VolumeSlider.Value > 0)
             {
                _lastVolume = (int)VolumeSlider.Value;
                VolumeSlider.Value = 0;
            }
            else
            {
                VolumeSlider.Value = _lastVolume > 0 ? _lastVolume : 50;
             }
        }
        
        private void VolumeSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            int volume = (int)VolumeSlider.Value;
              PlaybackService.Instance.SetVolume(volume);
              UpdateVolumeIcon(volume);
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingSlider = true;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingSlider = false;
            if (PlaybackService.Instance.Player != null)
            {
                long newTimeMs = (long)(ProgressSlider.Value * 1000);
                PlaybackService.Instance.SetTime(newTimeMs);

                // Update the time display immediately if paused (since the timer isn't running)
                long duration = PlaybackService.Instance.GetDuration();
                TimeSpan timeLeft = TimeSpan.FromMilliseconds(duration - newTimeMs);
                TimeLeftTextBlock.Text = $"{TranslationHelper.GetString("Label_TimeRemainingPrefix", "-")}{FormatTime(timeLeft)}";
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
                        // Capture initial normal state
            _normalLeft = this.Left;
            _normalTop = this.Top;
            _normalWidth = this.Width;
            _normalHeight = this.Height;

            // Allow the splash screen to animate by yielding the UI thread between setup steps
            await Task.Yield();

            _splash?.UpdateStatus(TranslationHelper.GetString("Splash_InitUI", "Initializing Monitors..."));
            InitializeMonitors();
            await Task.Yield();

            if (ScreenService.Instance.IsMultipleScreens())
            {
                ShowMediaScreen();
                await Task.Yield();
            }

            _splash?.UpdateStatus(TranslationHelper.GetString("Splash_Visualizer", "Preparing Visualizer..."));
            InitializeVisualizer();
            visualizerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            visualizerTimer.Tick += VisualizerTimer_Tick;
            await Task.Yield();

            // Setup Timers and Playback Subscriptions
            updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            updateTimer.Tick += UpdateTimer_Tick;

            PlaybackService.Instance.PlaybackStateChanged += (s, ev) =>
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    isPlaying = PlaybackService.Instance.IsPlaying;
                    UpdatePlayPauseIcon(isPlaying);
                    if (isPlaying) { updateTimer.Start(); visualizerTimer.Start(); }
                    else { updateTimer.Stop(); visualizerTimer.Stop(); ResetVisualizer(); }
                }));

            if (PlaybackService.Instance.Player != null)
                PlaybackService.Instance.Player.EndReached += MediaPlayer_EndReached;

            _splash?.UpdateStatus(TranslationHelper.GetString("Splash_Hymns", "Loading Hymn Files..."));
            await LoadHymnFilesAsync();

            PlaylistView.ItemsSource = mediaFiles;
            FolderExplorer.ItemsSource = HymnFiles;
            PlaylistView.MouseDoubleClick += PlaylistView_MouseDoubleClick;

            UpdateLibraryTabs();
            RefreshLibraryList();
            // Ensure volume slider is synced with media player volume
            if (PlaybackService.Instance.Player != null)
                VolumeSlider.Value = PlaybackService.Instance.Player.Volume;
            
            // Load Crossfade setting (defaulting to true)
            CrossfadeToggle.IsChecked = AppSettingsService.GetSetting("Crossfade", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

            // If the window is set to start maximized in XAML, handle it here.
            // Otherwise, it starts normal and MoveToOppositeScreen will be called
            // if a secondary screen is selected.
            if (this.WindowState == WindowState.Maximized) {
                MaximizeButton_Click(sender, e); // Trigger custom maximize logic
            }
            // Update UI text based on current language
            RefreshUIText();

            // Close splash screen once everything is settled
            _splash?.Close();
            _splash = null;
            this.Activate(); // Bring main window to front
        }

        private void CrossfadeToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded && CrossfadeToggle != null)
            {
                AppSettingsService.SetSetting("Crossfade", (CrossfadeToggle.IsChecked == true).ToString().ToLower());
            }
        }

        private void SavePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (mediaFiles.Count == 0) return;

            SaveOpenPlaylistWindow saveWindow = new SaveOpenPlaylistWindow(mediaFiles);
            saveWindow.Owner = this; // Set owner for proper centering and modality
            saveWindow.ShowDialog();
        }

        private void OpenPlaylist_Click(object sender, RoutedEventArgs e)
        {
            SaveOpenPlaylistWindow openWindow = new SaveOpenPlaylistWindow();
            openWindow.Owner = this;
            if (openWindow.ShowDialog() == true && openWindow.SelectedPlaylistItems != null)
            {
                mediaFiles.Clear();
                foreach (var item in openWindow.SelectedPlaylistItems)
                {
                    item.IsMissing = !FileService.Instance.FileExists(item.FilePath);
                    mediaFiles.Add(item);
                }
                currentPlaylistIndex = -1;
            }
        }

        /// <summary>
        /// Global key handler for the Main window to support standard shortcuts like Space for Play/Pause.
        /// </summary>
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            // Esc: Clear and unfocus search box
            if (e.Key == Key.Escape && FolderFilterBox.IsFocused)
            {
                FolderFilterBox.Text = string.Empty;
                this.Focus();
                e.Handled = true;
                return;
            }

            // Ctrl + F: Focus Search Box
            if (e.Key == Key.F && isCtrl)
            {
                FolderFilterBox.Focus();
                FolderFilterBox.SelectAll();
                e.Handled = true;
                return;
            }

            // Ctrl + S: Open Save Playlist Window
            if (e.Key == Key.S && isCtrl)
            {
                SavePlaylist_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Ctrl + O: Open Open Playlist Window
            if (e.Key == Key.O && isCtrl)
            {
                OpenPlaylist_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Check both keyboard focus and the original input source to allow typing in search boxes
            var focused = Keyboard.FocusedElement as DependencyObject;
            var original = e.OriginalSource as DependencyObject;

            bool isTyping = (focused is System.Windows.Controls.TextBox || focused is PasswordBox || FindAncestor<System.Windows.Controls.TextBox>(focused) != null) ||
                            (original is System.Windows.Controls.TextBox || original is PasswordBox || FindAncestor<System.Windows.Controls.TextBox>(original) != null);

            if (isTyping) return;

            if (e.Key == Key.Space)
            {
                e.Handled = true;
                // Execute the Play/Pause logic
                PlayPauseButton_Click(PlayPauseButton, new RoutedEventArgs());
                return;
            }

            // Playback and Volume Arrow Key Controls
            switch (e.Key)
            {
                case Key.Up:
                    VolumeSlider.Value = Math.Min(VolumeSlider.Maximum, VolumeSlider.Value + 5);
                    e.Handled = true;
                    break;
                case Key.Down:
                    VolumeSlider.Value = Math.Max(VolumeSlider.Minimum, VolumeSlider.Value - 5);
                    e.Handled = true;
                    break;
                case Key.Right:
                    PlaybackService.Instance.SetTime(PlaybackService.Instance.GetTime() + 5000); // Forward 5s
                    e.Handled = true;
                    break;
                case Key.Left:
                    PlaybackService.Instance.SetTime(Math.Max(0, PlaybackService.Instance.GetTime() - 5000)); // Back 5s
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Handles language changed event.
        /// </summary>
        private void TranslationHelper_LanguageChanged(object? sender, LanguageChangedEventArgs e)
        {
            RefreshUIText();
        }

        /// <summary>
        /// Refreshes UI text based on current language.
        /// </summary>
        private void RefreshUIText()
        {            
            // Update Labels and other UI elements
            // Update Fixed TabControl headers
            if (LibraryTabs != null && LibraryTabs.Items.Count >= 2)
            {
                ((TabItem)LibraryTabs.Items[0]).Header = TranslationHelper.GetString("Tab_Local", "Local");
                ((TabItem)LibraryTabs.Items[1]).Header = TranslationHelper.GetString("Tab_Favorites", "Favorites");
            }

            // Update placeholder text if the box is currently "empty" (showing previous placeholder)
            string newPlaceholder = TranslationHelper.GetString("Placeholder_SearchLibrary", "Search library...");            
            
            if (!FolderFilterBox.IsFocused)
            {
                if (string.IsNullOrEmpty(FolderFilterBox.Text) || FolderFilterBox.Text == _currentPlaceholder)
                {
                    FolderFilterBox.Text = newPlaceholder;
                    FolderFilterBox.SetResourceReference(ForegroundProperty, "TextSecondary");
                }
            }
            _currentPlaceholder = newPlaceholder; // Update the stored placeholder
                
            if (LibraryEmptyLabel != null)
            {
                LibraryEmptyLabel.Text = (LibraryTabs?.SelectedIndex == 1)
                    ? TranslationHelper.GetString("Label_FavoritesEmpty", "Your favorites list is empty.")
                    : TranslationHelper.GetString("Label_LibraryEmpty", "No media files found in this folder.");
            }
        }

        private bool IsImageFile(string? filePath)
        {
            return MediaHelper.DetermineMediaType(filePath) == Adoracion.Models.MediaType.Image;
        }

        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }

        private void PlaylistView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void PlaylistView_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("MediaFile") || _draggedData == null) 
            {
                e.Effects = System.Windows.DragDropEffects.None;
                return;
            }

            e.Effects = System.Windows.DragDropEffects.Move;
            
            // Find the item container under the mouse
            System.Windows.Controls.ListViewItem? item = FindAncestor<System.Windows.Controls.ListViewItem>((DependencyObject)e.OriginalSource);

            if (item != null)
            {
                // Get indices for math
                MediaFile targetFile = (MediaFile)PlaylistView.ItemContainerGenerator.ItemFromContainer(item);
                int oldIdx = mediaFiles.IndexOf(_draggedData);
                int targetIdx = mediaFiles.IndexOf(targetFile);

                System.Windows.Point pos = e.GetPosition(item);
                
                // COMPENSATION: If this item is currently shifted by our padding, 
                // adjust the mouse Y coordinate so the detection stays stable.
                double adjustedY = pos.Y;
                if (item.Padding.Top > 0) adjustedY -= item.Padding.Top;
                
                // Thresholds: Use 30/70 to ensure deliberate movement.
                // Calculate based on the original item height (ActualHeight minus the padding we added)
                double originalHeight = item.ActualHeight - item.Padding.Top - item.Padding.Bottom;
                bool isMouseInTopZone = adjustedY < originalHeight * 0.3;
                bool isMouseInBottomZone = adjustedY > originalHeight * 0.7;

                if (!isMouseInTopZone && !isMouseInBottomZone)
                {
                    ClearDragVisuals();
                    return;
                }

                // Calculate what the new index WOULD be if dropped here
                int potentialNewIndex = targetIdx;
                if (isMouseInBottomZone) potentialNewIndex++; 
                if (oldIdx < potentialNewIndex) potentialNewIndex--;

                // Issue: If the move is redundant (same position), don't show visual feedback
                if (potentialNewIndex == oldIdx)
                {
                    ClearDragVisuals();
                    return;
                }

                // Clear visuals if the drop boundary has changed
                ClearDragVisuals();

                // Identify the two items forming the gap
                // potentialNewIndex is the index where the item will land.
                // The gap is ABOVE the item currently at that index.
                int indexBelow = potentialNewIndex;
                int indexAbove = potentialNewIndex - 1;

                // Adjust for the fact that the moving item isn't in the calculation of containers
                if (oldIdx < potentialNewIndex) indexBelow++;
                if (oldIdx < potentialNewIndex) indexAbove++;

                _dragItemBelow = PlaylistView.ItemContainerGenerator.ContainerFromIndex(indexBelow) as System.Windows.Controls.ListViewItem;
                _dragItemAbove = PlaylistView.ItemContainerGenerator.ContainerFromIndex(indexAbove) as System.Windows.Controls.ListViewItem;

                // Create the "Both Move" effect
                if (_dragItemBelow != null)
                {
                    _dragItemBelow.BorderThickness = new Thickness(0, 2, 0, 0); // Visual line at the gap
                    _dragItemBelow.Padding = new Thickness(_dragItemBelow.Padding.Left, 15, _dragItemBelow.Padding.Right, 0); // Shift down
                }
                if (_dragItemAbove != null)
                {
                    // We shift the item above UP by using bottom padding
                    _dragItemAbove.Padding = new Thickness(_dragItemAbove.Padding.Left, 0, _dragItemAbove.Padding.Right, 15); // Shift up
                }
            }
            e.Handled = true;
        }

        private void PlaylistView_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            // Cleanup visual indicators when mouse leaves the list or items
            ClearDragVisuals();
        }

        private void ClearDragVisuals()
        {
            if (_dragItemAbove != null)
            {
                // Use ClearValue to return to the original Style padding
                _dragItemAbove.ClearValue(System.Windows.Controls.Control.PaddingProperty); 
                _dragItemAbove.ClearValue(System.Windows.Controls.Control.BorderThicknessProperty);
                _dragItemAbove = null;
            }
            if (_dragItemBelow != null)
            {
                _dragItemBelow.ClearValue(System.Windows.Controls.Control.BorderThicknessProperty);
                _dragItemBelow.ClearValue(System.Windows.Controls.Control.PaddingProperty);
                _dragItemBelow = null;
            }
            
            // Reset opacity of the dragged item if it was ghosted
            if (_draggedData != null)
            {
                var container = PlaylistView.ItemContainerGenerator.ContainerFromItem(_draggedData) as System.Windows.Controls.ListViewItem;
                if (container != null) container.Opacity = 1.0;
            }
        }
        private void PlaylistView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    System.Windows.Controls.ListViewItem? listViewItem = FindAncestor<System.Windows.Controls.ListViewItem>((DependencyObject)e.OriginalSource);
                    if (listViewItem == null) return;

                    MediaFile file = (MediaFile)PlaylistView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                    if (file == null) return;

                    _draggedData = file;
                    listViewItem.Opacity = 0.5; // "Ghost" the source item

                    System.Windows.DataObject dragData = new System.Windows.DataObject("MediaFile", file);
                    System.Windows.DragDrop.DoDragDrop(listViewItem, dragData, System.Windows.DragDropEffects.Move);
                    
                    // Cleanup after drop
                    ClearDragVisuals();
                }
            }
        }

        private void PlaylistView_Drop(object sender, System.Windows.DragEventArgs e)
        {
            ClearDragVisuals();
            if (e.Data.GetDataPresent("MediaFile"))
            {
                MediaFile droppedFile = e.Data.GetData("MediaFile") as MediaFile;
                int oldIndex = mediaFiles.IndexOf(droppedFile);

                System.Windows.Controls.ListViewItem? listViewItem = FindAncestor<System.Windows.Controls.ListViewItem>((DependencyObject)e.OriginalSource);

                int newIndex = -1;
                if (listViewItem != null)
                {
                    MediaFile targetFile = (MediaFile)PlaylistView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                    newIndex = mediaFiles.IndexOf(targetFile);
                    
                    // Refine index based on whether we dropped on the top or bottom half
                    System.Windows.Point pos = e.GetPosition(listViewItem);
                    double adjustedY = pos.Y;
                    if (listViewItem.Padding.Top > 0) adjustedY -= listViewItem.Padding.Top;

                    double originalHeight = listViewItem.ActualHeight - listViewItem.Padding.Top - listViewItem.Padding.Bottom;

                    // Match the 70/30 visual logic exactly
                    if (adjustedY > originalHeight * 0.7)
                    {
                        newIndex++; // Drop after the item
                    }
                    else if (adjustedY > originalHeight * 0.3)
                    {
                        // Mouse is in the middle "dead zone", prevent move by matching old index
                        newIndex = oldIndex;
                    }
                    
                    // Adjust index if moving forward in list
                    if (oldIndex < newIndex) newIndex--;
                }
                else
                {
                    newIndex = mediaFiles.Count - 1;
                }

                if (oldIndex != -1 && newIndex != -1 && oldIndex != newIndex)
                {
                    mediaFiles.Move(oldIndex, newIndex);

                    // Re-index all items to update the UI # column
                    for (int i = 0; i < mediaFiles.Count; i++)
                    {
                        mediaFiles[i].Index = i + 1;
                    }

                    // Update current index if playing
                    if (selectedFile != null) currentPlaylistIndex = mediaFiles.IndexOf(selectedFile);
                }
            }
        }

        /// <summary>
        /// Dynamically adjusts the Title column width to stretch and fill available space,
        /// pushing the Actions column to the right edge of the playlist.
        /// </summary>
        private void PlaylistView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (PlaylistView.View is GridView gridView && gridView.Columns.Count >= 5) // Index, Name, Type, Duration, Actions
            {
                // Fixed widths: Index(0), Type(2), Duration(3), Actions(4)
                // We subtract fixed columns and a small margin (25px) to account for borders and the scrollbar.
                double fixedWidths = gridView.Columns[0].Width + gridView.Columns[2].Width + gridView.Columns[3].Width + gridView.Columns[4].Width;
                double newWidth = PlaylistView.ActualWidth - fixedWidths - 25; // 25px for scrollbar/margin

                if (newWidth > 0)
                    gridView.Columns[1].Width = newWidth; // Name column is now at index 1
            }
        }

        private void UpdateVolumeIcon(int volume)
        {
            if (VolumeIconPath != null)
            {
                VolumeIconPath.Data = (Geometry)FindResource(volume == 0 ? "VolumeMuteIcon" : "VolumeUpIcon");
            }
        }

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            if (PlayPauseIconPath != null)
            {
                PlayPauseIconPath.Data = (Geometry)FindResource(isPlaying ? "PauseIcon" : "PlayArrowIcon");
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T ancestor) return ancestor;
                current = VisualTreeHelper.GetParent(current);
            } while (current != null);
            return null;
        }

        /// <summary>
        /// Handles the window closed event to ensure MediaScreen is properly closed before the main window.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            mediaScreen?.Close();
            System.Windows.Application.Current.Shutdown();
        }
    }

    public class FileNameWithoutExtensionConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string fileName && !string.IsNullOrEmpty(fileName))
            { // Use FileService for consistency
                return FileService.Instance.GetFileNameWithoutExtension(fileName);
            }
            return value;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a full file path to just the file name (including extension).
    /// </summary>
    public class FileNameConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string filePath && !string.IsNullOrEmpty(filePath))
            { // Use FileService for consistency
                return FileService.Instance.GetFileName(filePath);
            }
            return value;
        }
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new System.NotImplementedException();
    }
}
