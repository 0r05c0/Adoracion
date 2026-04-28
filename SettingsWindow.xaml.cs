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
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows; // Keep this for WPF types
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input; // Keep this for MouseButtonEventArgs
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using WpfButton = System.Windows.Controls.Button;
using Adoracion.Helpers;
using Adoracion.Services;

namespace Adoracion
{
    /// <summary>
    /// Settings window for Adoracion media player.
    /// Allows users to configure media folders, language, screen selection, and appearance.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private const string SETTINGS_FILE = "UserSettings.json";
        
        private bool _isActuallyClosing = false;
        private class ThemeMetadata
        {
            public string Name { get; set; } = "";
            public string Author { get; set; } = "";
            public string Mode { get; set; } = ""; // "Light" or "Dark"
            public string FilePath { get; set; } = "";
        }
        private List<ThemeMetadata> _discoveredThemes = new List<ThemeMetadata>();

        private string _currentThemeName = "default";
        private string _currentThemeMode = "Dark";

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            (string name, string mode) = LoadThemeFromSettings();
            _currentThemeName = name;
            _currentThemeMode = mode;

            _discoveredThemes = DiscoverThemes();
            InitializeMediaFolders();
            InitializeLanguageDropdown();
            InitializeScreenDropdown();
            
            UpdateThemeSelection(_currentThemeMode);
            UpdateCustomThemesDropdown(_currentThemeMode);
            ValidateModeAvailability(_currentThemeName);

            string currentPos = AppearanceSettings.GetOverlayPosition();
            UpdatePositionVisuals(currentPos);

            OverlayTextBox.Text = AppearanceSettings.GetOverlayText();
            FontSizeSlider.Value = AppearanceSettings.GetFontSize();
            TextAlphaSlider.Value = AppearanceSettings.GetTextAlpha();
            UpdateColorVisuals(AppearanceSettings.GetTextColor());
            EnableTextShadowCheckBox.IsChecked = AppearanceSettings.GetEnableShadow();
            UpdateShadowColorVisuals(AppearanceSettings.GetShadowColor());
            ShadowBlurSlider.Value = AppearanceSettings.GetShadowBlur();
            ShadowDepthSlider.Value = AppearanceSettings.GetShadowDepth();
            ShadowOpacitySlider.Value = AppearanceSettings.GetShadowOpacity();

            LoadBackgroundImageSettings();

            TranslationHelper.LanguageChanged += TranslationHelper_LanguageChanged;
            AppSettingsService.SettingChanged += OnSettingChanged;

            RefreshUIText();
        }

        private void TranslationHelper_LanguageChanged(object? sender, LanguageChangedEventArgs e)
        {
            RefreshUIText();
        }

        private void InitializeMediaFolders()
        {
            var folders = new ObservableCollection<MediaFolder>();
            string foldersJson = AppSettingsService.GetSetting("MediaFolders", "[]");
            try
            {
                var paths = JsonSerializer.Deserialize<List<string>>(foldersJson);
                if (paths != null)
                {
                    foreach (var path in paths)
                    { // Use FileService
                        int count = FileService.Instance.DirectoryExists(path) ? FileService.Instance.GetFileCountInDirectory(path) : 0;
                        folders.Add(new MediaFolder { Path = path, Count = count });
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error loading media folders from settings: {ex.Message}");
            }

            FoldersListControl.ItemsSource = folders;
            UpdateFolderLimitUI();
        }

        private void InitializeLanguageDropdown()
        {
            if (LanguageDropdown == null) return;

            LanguageDropdown.SelectionChanged -= LanguageDropdown_SelectionChanged;
            LanguageDropdown.Items.Clear();

            // Use the dynamically loaded languages from LocalizationService
            var availableLanguages = LocalizationService.Instance.AvailableLanguages;
            string currentLang = TranslationHelper.GetCurrentLanguage();

            foreach (var lang in availableLanguages)
            {
                string displayName = LocalizationService.Instance.LanguageDisplayNames.TryGetValue(lang, out var name)
                                     ? name
                                     : lang.ToUpper(); // Fallback to uppercase code if display name is missing

                var item = new ComboBoxItem
                {
                    Content = displayName,
                    Tag = lang
                };
                LanguageDropdown.Items.Add(item);

                if (lang.Equals(currentLang, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageDropdown.SelectedItem = item;
                }
            }
            LanguageDropdown.SelectionChanged += LanguageDropdown_SelectionChanged;
        }

        private List<ThemeMetadata> DiscoverThemes()
        {
            var list = new List<ThemeMetadata>();
            string themesRoot = FileService.Instance.CombinePath(AppDomain.CurrentDomain.BaseDirectory, "Themes", "Color_Themes");
            if (!FileService.Instance.DirectoryExists(themesRoot)) return list; // Use FileService

            var themeFolders = FileService.Instance.GetDirectories(themesRoot);
            foreach (var folder in themeFolders)
            {
                var xamlFiles = FileService.Instance.GetMediaFilesFromDirectory(folder); // Use FileService
                foreach (var file in xamlFiles.Where(f => FileService.Instance.GetFileExtension(f).Equals(".xaml", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        // Use string searching for metadata to avoid heavy ResourceDictionary instantiation
                        string content = FileService.Instance.ReadAllText(file);
                        if (content.Contains("x:Key=\"ThemeName\"") && content.Contains("x:Key=\"ThemeMode\""))
                        {
                            // We only load the dictionary if it appears to be a valid theme file // This is fine, it's a WPF specific action
                            var dict = new ResourceDictionary { Source = new Uri(file, UriKind.Absolute) };
                            list.Add(new ThemeMetadata
                            {
                                Name = dict["ThemeName"] as string ?? FileService.Instance.GetFileNameWithoutExtension(file),
                                Author = dict["ThemeAuthor"] as string ?? "Unknown",
                                Mode = dict["ThemeMode"] as string ?? "Dark",
                                FilePath = file
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.Log($"Error discovering theme file {file}: {ex.Message}");
                    }
                }
            }
            return list;
        }

        private void UpdateCustomThemesDropdown(string mode)
        {
            if (CustomThemeDropdown == null) return;
            CustomThemeDropdown.SelectionChanged -= CustomThemeDropdown_SelectionChanged;

            // Identify the target file path (or "default") for the current theme name in the new mode
            string targetTag = "default";
            if (_currentThemeName != "default")
            {
                var matchingTheme = _discoveredThemes.FirstOrDefault(t => t.Name == _currentThemeName && t.Mode == mode);
                if (matchingTheme != null)
                {
                    targetTag = matchingTheme.FilePath;
                }
            }

            CustomThemeDropdown.Items.Clear();

            // Add Standard option
            CustomThemeDropdown.Items.Add(new ComboBoxItem { 
                Content = TranslationHelper.GetString("Theme_Standard", "default"), 
                Tag = "default" 
            });

            var filtered = _discoveredThemes.Where(t => t.Mode.Equals(mode, StringComparison.OrdinalIgnoreCase));
            foreach (var theme in filtered)
            {
                CustomThemeDropdown.Items.Add(new ComboBoxItem {
                    Content = $"{theme.Name} (by {theme.Author})",
                    Tag = theme.FilePath
                });
            }
            
            foreach (ComboBoxItem item in CustomThemeDropdown.Items)
            {
                if (item.Tag.ToString() == targetTag)
                {
                    CustomThemeDropdown.SelectedItem = item;
                    break;
                }
            }

            if (CustomThemeDropdown.SelectedItem == null) 
                CustomThemeDropdown.SelectedIndex = 0;

            CustomThemeDropdown.SelectionChanged += CustomThemeDropdown_SelectionChanged;
        }

        private void ValidateModeAvailability(string themeName)
        {
            if (themeName == "default")
            {
                LightModeBox.IsEnabled = true;
                DarkModeBox.IsEnabled = true;
                LightModeBox.Opacity = 1.0;
                DarkModeBox.Opacity = 1.0;
                return;
            }

            bool hasLight = _discoveredThemes.Any(t => t.Name == themeName && t.Mode == "Light");
            bool hasDark = _discoveredThemes.Any(t => t.Name == themeName && t.Mode == "Dark");

            LightModeBox.IsEnabled = hasLight;
            LightModeBox.Opacity = hasLight ? 1.0 : 0.3;
            DarkModeBox.IsEnabled = hasDark;
            DarkModeBox.Opacity = hasDark ? 1.0 : 0.3;
        }

        /// <summary>
        /// Handles language dropdown selection change.
        /// Changes the application language and saves the preference.
        /// </summary>
        private void LanguageDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageDropdown.SelectedItem is ComboBoxItem selectedItem)
            {
                string languageCode = (string)selectedItem.Tag;
                TranslationHelper.SetLanguage(languageCode);
                
                // Refresh UI text after language change
                RefreshUIText();
            }
        }

        /// <summary>
        /// Refreshes all UI text based on current language.
        /// </summary>
        private void RefreshUIText()
        {
            this.Title = TranslationHelper.GetString("Title_Settings", "Settings");
            
            // Update navigation button text
            UpdateNavButtonText(NavMediaFolders, "Nav_MediaFolders", "Media Folders");
            UpdateNavButtonText(NavLanguage, "Nav_Language", "Language");
            UpdateNavButtonText(NavScreenSelection, "Nav_ScreenSelection", "Screen Selection");
            UpdateNavButtonText(NavAppearance, "Nav_Appearance", "Appearance");
            UpdateNavButtonText(NavTextWallpaper, "Nav_TextWallpaper", "Text & Wallpaper");
            UpdateNavButtonText(NavUpdates, "Nav_Updates", "Updates");
            UpdateNavButtonText(NavAbout, "Nav_About", "About");
            
            // Update sidebar logo/title
            if (FindName("LabelAdoracion") is TextBlock lblAdoracion)
                lblAdoracion.Text = TranslationHelper.GetString("Label_AppName", "Adoracion");
            if (FindName("LabelSettingsTitle") is TextBlock lblSettingsTitle)
                lblSettingsTitle.Text = TranslationHelper.GetString("Label_Settings_Title", "SETTINGS");

            // Update ALL section headers and descriptions regardless of visibility
            UpdateSectionHeader(MediaFoldersSection, "Section_MediaFolders_Title", "Media Folders", 
                               "Section_MediaFolders_Description", "Manage where Adoracion looks for your content.");
            
            UpdateSectionHeader(LanguageSection, "Section_Language_Title", "Language",
                               "Section_Language_Description", "Choose your preferred language.");

            // Update Add Folder button
            if (FindName("AddFolderButtonContent") is StackPanel addFolderStackPanel && addFolderStackPanel.Children.Count > 1)
            {
                if (addFolderStackPanel.Children[1] is TextBlock addFolderTextBlock)
                {
                    addFolderTextBlock.Text = TranslationHelper.GetString("Button_AddFolder", "Add Folder");
                }
            }
            
            UpdateSectionHeader(ScreenSelectionSection, "Section_ScreenSelection_Title", "Screen Selection",
                               "Section_ScreenSelection_Description", "Choose which display to use for media playback.");
            
            UpdateSectionHeader(AppearanceSection, "Section_Appearance_Title", "Theme Appearance",
                               "Section_Appearance_Description", "Customize how Adoracion looks.");

            UpdateSectionHeader(TextWallpaperSection, "Section_TextWallpaper_Title", "Text & Wallpaper",
                               "Section_TextWallpaper_Description", "Customize the overlay text and background image.");

            UpdateSectionHeader(UpdatesSection, "Section_Updates_Title", "Check for Updates",
                               "Section_Updates_Description", "Keep Adoracion up to date with the latest features.");

            if (FindName("CheckUpdatesButtonText") is TextBlock lblCheckUpdates)
                lblCheckUpdates.Text = TranslationHelper.GetString("Button_CheckUpdates", "Check for Updates");

            // Update Appearance section labels
            if (FindName("LabelLightMode") is TextBlock lblLightMode)
                lblLightMode.Text = TranslationHelper.GetString("Label_LightMode", "Light Mode");
            if (FindName("LabelDarkMode") is TextBlock lblDarkMode)
                lblDarkMode.Text = TranslationHelper.GetString("Label_DarkMode", "Dark Mode");
            
            // review it
            if (FindName("LabelHarlequinMode") is TextBlock lblHarlequinMode)
                lblHarlequinMode.Text = TranslationHelper.GetString("Label_HarlequinMode", "Harlequin Mode");
            
            if (FindName("LabelCustomTheme") is TextBlock lblCustomTheme)
                lblCustomTheme.Text = TranslationHelper.GetString("Label_CustomTheme", "Custom Accent Theme");           
            if (FindName("LabelOverlayTextHeader") is TextBlock lblOverlayHeader)
                lblOverlayHeader.Text = TranslationHelper.GetString("Label_OverlayText", "Overlay Welcome Text");
            if (FindName("LabelOverlayTextDesc") is TextBlock lblOverlayDesc)
                lblOverlayDesc.Text = TranslationHelper.GetString("Settings_Appearance_OverlayTextDesc", "Enter the text to display on the media screen when idle.");
            if (FindName("LabelFontSizeHeader") is TextBlock lblFontSize)
                lblFontSize.Text = TranslationHelper.GetString("Label_FontSize", "Font Size");
            if (FindName("SaveOverlayButton") is WpfButton btnSave)
                btnSave.Content = TranslationHelper.GetString("Button_Save", "Save");

            if (FindName("LabelOverlayPositionHeader") is TextBlock lblPosHeader)
                lblPosHeader.Text = TranslationHelper.GetString("Label_OverlayPosition", "Overlay Position");
            if (FindName("LabelTextColorHeader") is TextBlock lblTextColor)
                lblTextColor.Text = TranslationHelper.GetString("Label_TextColor", "Text Color");
            if (FindName("EnableTextShadowCheckBox") is System.Windows.Controls.CheckBox chkEnableShadow)
                chkEnableShadow.Content = TranslationHelper.GetString("Checkbox_EnableTextShadow", "Enable Text Shadow");
            if (FindName("LabelShadowColorHeader") is TextBlock lblShadowColor)
                lblShadowColor.Text = TranslationHelper.GetString("Label_ShadowColor", "Shadow Color");
            if (FindName("LabelShadowBlurHeader") is TextBlock lblBlur)
                lblBlur.Text = TranslationHelper.GetString("Label_ShadowBlur", "Shadow Blur");
            if (FindName("LabelShadowDepthHeader") is TextBlock lblDepth)
                lblDepth.Text = TranslationHelper.GetString("Label_ShadowDepth", "Shadow Depth");
            if (FindName("LabelShadowOpacityHeader") is TextBlock lblShadowOpacity)
                lblShadowOpacity.Text = TranslationHelper.GetString("Label_ShadowOpacity", "Shadow Opacity");

            if (FindName("LabelTextAlphaHeader") is TextBlock lblTextAlpha)
                lblTextAlpha.Text = TranslationHelper.GetString("Label_TextAlpha", "Text Opacity");

            if (FindName("LabelBackgroundImageHeader") is TextBlock lblBgImageHeader)
                lblBgImageHeader.Text = TranslationHelper.GetString("Label_BackgroundImage", "Background Image");
            if (FindName("LabelBackgroundImageDesc") is TextBlock lblBgImageDesc)
                lblBgImageDesc.Text = TranslationHelper.GetString("Settings_Appearance_BackgroundImageDesc", "Select an image to display as background when idle.");
            if (FindName("EnableBackgroundImageCheckBox") is System.Windows.Controls.CheckBox chkEnableBgImage)
                chkEnableBgImage.Content = TranslationHelper.GetString("Checkbox_EnableBackgroundImage", "Enable Background Image");
            if (FindName("BrowseImageButton") is WpfButton btnBrowseImage)
                btnBrowseImage.Content = TranslationHelper.GetString("Button_BrowseImage", "Browse Image");

            // Refresh Folder Limit Tooltip/State
            UpdateFolderLimitUI();

            UpdateSectionHeader(AboutSection, "Section_About_Title", "About Adoracion", "", "");

            // Update About section labels and links
            if (FindName("LabelAppName") is TextBlock lblAppName)
                lblAppName.Text = TranslationHelper.GetString("Label_AppName", "Adoracion Media Player");
            if (FindName("LabelVersionInfo") is TextBlock lblVersionInfo)
                lblVersionInfo.Text = TranslationHelper.GetString("Label_VersionInfo", "Version 1.0.0 (Beta)") +
                    $" {Assembly.GetEntryAssembly().GetName()?.Version}";
			if (FindName("LabelAppDescription") is TextBlock lblAppDescription)
                lblAppDescription.Text = TranslationHelper.GetString("Label_AppDescription", "Professional media playback engine designed for multi-screen presentations and performances. Built with love by the community.");
            if (FindName("LabelResources") is TextBlock lblResources)
                lblResources.Text = TranslationHelper.GetString("Label_Resources", "Resources");

            if (FindName("LinkOfficialWebsite") is Run linkOfficialWebsite)
                linkOfficialWebsite.Text = TranslationHelper.GetString("Link_OfficialWebsite", "Official Website");
            if (FindName("LinkReleaseNotes") is Run linkReleaseNotes)
                linkReleaseNotes.Text = TranslationHelper.GetString("Link_ReleaseNotes", "Release Notes");
            if (FindName("LinkSupport") is Run linkSupport)
                linkSupport.Text = TranslationHelper.GetString("Link_Support", "Support");
            if (FindName("LinkLicense") is Run linkLicense)
                linkLicense.Text = TranslationHelper.GetString("Link_License", "License");
        }

        /// <summary>
        /// Updates navigation button text.
        /// </summary>
        private void UpdateNavButtonText(WpfButton btn, string key, string defaultText)
        {
            var stackPanel = btn.Content as StackPanel;
            if (stackPanel != null && stackPanel.Children.Count > 1)
            {
                var textBlock = stackPanel.Children[1] as TextBlock;
                if (textBlock != null)
                {
                    textBlock.Text = TranslationHelper.GetString(key, defaultText);
                }
            }
        }

        /// <summary>
        /// Updates section header and description text.
        /// </summary>
        private void UpdateSectionHeader(StackPanel section, string titleKey, string titleDefault, 
                                        string descKey, string descDefault)
        {
            // Find and update the title (first TextBlock that's large)
            var children = section.Children;
            if (children.Count > 0 && children[0] is TextBlock titleBlock)
            {
                titleBlock.Text = TranslationHelper.GetString(titleKey, titleDefault);
            }
            // Find and update the description (second TextBlock)
            if (children.Count > 1 && children[1] is TextBlock descBlock)
            {
                descBlock.Text = TranslationHelper.GetString(descKey, descDefault);
            }
        }

        /// <summary>
        /// Initializes the screen dropdown with available displays.
        /// </summary>
        private void InitializeScreenDropdown()
        {
            ScreenDropdown.SelectionChanged -= ScreenDropdown_SelectionChanged;
            ScreenDropdown.Items.Clear();

            var screens = ScreenService.Instance.GetAllScreens();
            string savedScreen = AppSettingsService.GetSetting("SelectedScreen", "");

            foreach (var screen in screens)
            {
                var item = new ComboBoxItem
                {
                    // Localize "Primary" and "Secondary"
                    Content = screen.DisplayName,
                    Tag = screen.DeviceName // Use DeviceName as the unique identifier
                };
                ScreenDropdown.Items.Add(item);

                if (screen.DeviceName == savedScreen)
                {
                    ScreenDropdown.SelectedItem = item;
                }
            }

            if (ScreenDropdown.SelectedItem == null && ScreenDropdown.Items.Count > 0)
            {
                ScreenDropdown.SelectedIndex = 0;
            }

            ScreenDropdown.SelectionChanged += ScreenDropdown_SelectionChanged;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
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
            base.OnClosing(e);
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
            // This method is primarily for the Main window to move itself.
            // For the Settings window, we just ensure it's centered on the UI screen.
            // The logic here is simplified as SettingsWindow doesn't "maximize" to a working area.

            ScreenService.Instance.MoveWindowToUIScreen(this, fillScreen: false);
        }

        private void ScreenDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScreenDropdown.SelectedItem is ComboBoxItem item)
            {
                string deviceName = item.Tag as string;
                AppSettingsService.SetSetting("SelectedScreen", deviceName);
            }
        }

        /// <summary>
        /// Handles navigation button clicks.
        /// Shows the corresponding settings section.
        /// </summary>
        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            WpfButton btn = sender as WpfButton;
            if (btn == null) return;

            // Hide all sections
            MediaFoldersSection.Visibility = Visibility.Collapsed;
            LanguageSection.Visibility = Visibility.Collapsed;
            ScreenSelectionSection.Visibility = Visibility.Collapsed;
            AppearanceSection.Visibility = Visibility.Collapsed;
            TextWallpaperSection.Visibility = Visibility.Collapsed;
            UpdatesSection.Visibility = Visibility.Collapsed;
            AboutSection.Visibility = Visibility.Collapsed;

            // Clear selection tags
            NavMediaFolders.Tag = null;
            NavLanguage.Tag = null;
            NavScreenSelection.Tag = null;
            NavAppearance.Tag = null;
            NavTextWallpaper.Tag = null;
            NavUpdates.Tag = null;
            NavAbout.Tag = null;

            // Show clicked section and highlight nav button
            if (btn == NavMediaFolders)
            {
                MediaFoldersSection.Visibility = Visibility.Visible;
                NavMediaFolders.Tag = "Selected";
            }
            else if (btn == NavLanguage)
            {
                LanguageSection.Visibility = Visibility.Visible;
                NavLanguage.Tag = "Selected";
            }
            else if (btn == NavScreenSelection)
            {
                ScreenSelectionSection.Visibility = Visibility.Visible;
                NavScreenSelection.Tag = "Selected";
            }
            else if (btn == NavAppearance)
            {
                AppearanceSection.Visibility = Visibility.Visible;
                NavAppearance.Tag = "Selected";
            }
            else if (btn == NavTextWallpaper)
            {
                TextWallpaperSection.Visibility = Visibility.Visible;
                NavTextWallpaper.Tag = "Selected";
            }
            else if (btn == NavUpdates)
            {
                UpdatesSection.Visibility = Visibility.Visible;
                NavUpdates.Tag = "Selected";
            }
            else if (btn == NavAbout)
            {
                AboutSection.Visibility = Visibility.Visible;
                NavAbout.Tag = "Selected";
            }
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            await CheckForUpdates();
            CheckUpdatesButton.IsEnabled = true;
        }

        public async Task CheckForUpdates()
        {
            UpdateStatusTextBlock.Text = TranslationHelper.GetString("Status_CheckingUpdates", "Checking for updates...");
            var updater = new UpdateCheckerService("0r05c0", "Adoracion", "Adoracion");

            if (await updater.IsUpdateAvailableAsync())
            {
                UpdateStatusTextBlock.Text = TranslationHelper.GetString("Status_UpdateFound", "New version found.");
                
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdatePercentageTextBlock.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = 0;

                var progress = new Progress<double>(p =>
                {
                    UpdateProgressBar.Value = p;
                    UpdatePercentageTextBlock.Text = $"{TranslationHelper.GetString("Status_Downloading", "Downloading")} {(int)p}%";
                    if (p >= 100)
                    {
                        UpdateStatusTextBlock.Text = TranslationHelper.GetString("Status_InstallingUpdate", "Installing update and restarting...");
                    }
                });

                await updater.DownloadAndInstallUpdateAsync(progress);
            }
            else
            {
                UpdateStatusTextBlock.Text = TranslationHelper.GetString("Status_NoUpdate", "No new version available.");
            }
        }

        private void SaveMediaFolders()
        {
            var folders = FoldersListControl.ItemsSource as ObservableCollection<MediaFolder>;
            if (folders != null)
            {
                var paths = folders.Select(f => f.Path).ToList();
                string json = JsonSerializer.Serialize(paths);
                AppSettingsService.SetSetting("MediaFolders", json);
            }
        }

        /// <summary>
        /// Handles the add folder button click.
        /// Opens a folder dialog for user to select a new media folder.
        /// </summary>
        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Add new folder to list
                var folders = FoldersListControl.ItemsSource as ObservableCollection<MediaFolder>;
                if (folders != null)
                {
                    folders.Add(new MediaFolder { Path = dialog.SelectedPath, Count = 0 });
                    SaveMediaFolders();
                    UpdateFolderLimitUI();
                }
            }
        }

        /// <summary>
        /// Handles the delete folder button click.
        /// </summary>
        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            WpfButton btn = sender as WpfButton;
            if (btn?.DataContext is MediaFolder folder)
            {
                var folders = FoldersListControl.ItemsSource as ObservableCollection<MediaFolder>;
                if (folders != null)
                {
                    folders.Remove(folder);
                    SaveMediaFolders();
                    UpdateFolderLimitUI();
                }
            }
        }

        /// <summary>
        /// Disables the add folder button and sets a tooltip if the limit is reached.
        /// </summary>
        private void UpdateFolderLimitUI()
        {
            if (AddFolderButton == null || FoldersListControl == null) return;

            var folders = FoldersListControl.ItemsSource as ObservableCollection<MediaFolder>;
            bool isUnderLimit = folders == null || folders.Count < 3;

            AddFolderButton.IsEnabled = isUnderLimit;
            AddFolderButton.ToolTip = isUnderLimit ? null : TranslationHelper.GetString("Tooltip_MaxFoldersReached", "You can add a maximum of 3 media folders.");
        }

        /// <summary>
        /// Handles clicking a position segment in the monitor shape.
        /// </summary>
        private void PosButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string pos)
            {
                AppSettingsService.SetSetting(AppearanceSettings.OverlayPositionKey, pos);
                UpdatePositionVisuals(pos);
            }
        }

        /// <summary>
        /// Updates the visuals of the monitor shape grid to show selection.
        /// </summary>
        private void UpdatePositionVisuals(string selectedPos)
        {
            if (PositionGrid == null) return;
            foreach (var child in PositionGrid.Children)
            {
                if (child is WpfButton btn)
                {
                    bool isSelected = btn.Tag?.ToString() == selectedPos;
                    if (isSelected)
                    {
                        btn.SetResourceReference(BackgroundProperty, "Primary");
                    }
                    else
                    {
                        btn.Background = System.Windows.Media.Brushes.Transparent;
                    }
                    btn.Opacity = isSelected ? 1.0 : 0.6;
                }
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string colorHex)
            {
                AppSettingsService.SetSetting(AppearanceSettings.TextColorKey, colorHex);
                UpdateColorVisuals(colorHex);
            }
        }

        private void EnableTextShadowCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettingsService.SetSetting(AppearanceSettings.EnableShadowKey, "True");
        }

        private void EnableTextShadowCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettingsService.SetSetting(AppearanceSettings.EnableShadowKey, "False");
        }

        private void ShadowColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string colorHex)
            {
                AppSettingsService.SetSetting(AppearanceSettings.ShadowColorKey, colorHex);
                UpdateShadowColorVisuals(colorHex);
            }
        }

        private void ShadowBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded)
            {
                AppSettingsService.SetSetting(AppearanceSettings.ShadowBlurKey, e.NewValue.ToString());
            }
        }

        private void ShadowDepthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded)
            {
                AppSettingsService.SetSetting(AppearanceSettings.ShadowDepthKey, e.NewValue.ToString());
            }
        }

        private void ShadowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded)
            {
                AppSettingsService.SetSetting(AppearanceSettings.ShadowOpacityKey, e.NewValue.ToString());
            }
        }

        private void UpdateShadowColorVisuals(string selectedColor)
        {
            if (ShadowColorPaletteGrid == null) return;
            foreach (var child in ShadowColorPaletteGrid.Children)
            {
                if (child is WpfButton btn)
                {
                    bool isSelected = btn.Tag?.ToString() == selectedColor;
                    btn.BorderThickness = new Thickness(isSelected ? 3 : 1);
                    btn.BorderBrush = isSelected ? (System.Windows.Media.Brush)FindResource("Primary") : (System.Windows.Media.Brush)FindResource("ItemBorder");
                }
            }
        }

        private void UpdateColorVisuals(string selectedColor)
        {
            if (ColorPaletteGrid == null) return;
            foreach (var child in ColorPaletteGrid.Children)
            {
                if (child is WpfButton btn)
                {
                    bool isSelected = btn.Tag?.ToString() == selectedColor;
                    // Highlight the selected color with a primary border
                    btn.BorderThickness = new Thickness(isSelected ? 3 : 1);
                    btn.BorderBrush = isSelected ? (System.Windows.Media.Brush)FindResource("Primary") : (System.Windows.Media.Brush)FindResource("ItemBorder");
                }
            }
        }

        private void TextAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded)
            {
                AppSettingsService.SetSetting(AppearanceSettings.TextAlphaKey, e.NewValue.ToString());
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded)
            {
                AppSettingsService.SetSetting(AppearanceSettings.FontSizeKey, e.NewValue.ToString());
            }
        }

        /// <summary>
        /// Saves the custom overlay text to SQLite and triggers a refresh.
        /// </summary>
        private void SaveOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            AppSettingsService.SetSetting(AppearanceSettings.OverlayTextKey, OverlayTextBox.Text);
        }

        /// <summary>
        /// Loads the background image settings from SQLite and updates the UI.
        /// </summary>
        private void LoadBackgroundImageSettings()
        {
            bool enableBgImage = AppearanceSettings.GetEnableBackgroundImage();
            string imagePath = AppearanceSettings.GetBackgroundImagePath();

            EnableBackgroundImageCheckBox.IsChecked = enableBgImage;
            BackgroundImagePathTextBox.Text = imagePath;
            UpdateBackgroundImagePreview(imagePath);
        }

        /// <summary>
        /// Handles the Checked event of the EnableBackgroundImageCheckBox.
        /// </summary>
        private void EnableBackgroundImageCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettingsService.SetSetting(AppearanceSettings.EnableBackgroundImageKey, "True");
        }

        /// <summary>
        /// Handles the Unchecked event of the EnableBackgroundImageCheckBox.
        /// </summary>
        private void EnableBackgroundImageCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettingsService.SetSetting(AppearanceSettings.EnableBackgroundImageKey, "False");
        }

        /// <summary>
        /// Handles the BrowseImageButton click event to select a background image.
        /// </summary>
        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string selectedImagePath = openFileDialog.FileName;
                AppSettingsService.SetSetting(AppearanceSettings.BackgroundImagePathKey, selectedImagePath);
                BackgroundImagePathTextBox.Text = selectedImagePath;
                UpdateBackgroundImagePreview(selectedImagePath);
            }
        }

        /// <summary>
        /// Updates the preview image in the settings window.
        /// </summary>
        private void UpdateBackgroundImagePreview(string imagePath)
        {
            if (FileService.Instance.FileExists(imagePath)) // Use FileService
            {
                BackgroundImagePreview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath));
            }
            else
            {
                BackgroundImagePreview.Source = null; // Clear preview if path is invalid
                LoggingService.Instance.Log($"Background image path is invalid: {imagePath}");
            }
        }

        /// <summary>
        /// Handles light mode selection.
        /// </summary>
        private void LightMode_Click(object sender, MouseButtonEventArgs e)
        {
            ChangeMode("Light");
        }

        private void DarkMode_Click(object sender, MouseButtonEventArgs e)
        {
            ChangeMode("Dark");
        }

        private void ChangeMode(string newMode)
        {
            if (ApplyThemeByNameAndMode(_currentThemeName, newMode))
            {
                _currentThemeMode = newMode;
                UpdateThemeSelection(_currentThemeMode);
                UpdateCustomThemesDropdown(_currentThemeMode);
                SaveThemeToSettings(_currentThemeName, _currentThemeMode);
            }
        }

        private void CustomThemeDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomThemeDropdown.SelectedItem is ComboBoxItem item)
            {
                string tagValue = item.Tag.ToString();
                string intendedThemeName = "default";
                
                if (tagValue != "default")
                {
                    var meta = _discoveredThemes.FirstOrDefault(t => t.FilePath == tagValue);
                    if (meta != null) intendedThemeName = meta.Name;
                }

                if (ApplyThemeByNameAndMode(intendedThemeName, _currentThemeMode))
                {
                    _currentThemeName = intendedThemeName;
                    ValidateModeAvailability(_currentThemeName);
                    SaveThemeToSettings(_currentThemeName, _currentThemeMode);
                }
                else if (e.RemovedItems.Count > 0)
                {
                    // Revert selection if application failed
                    CustomThemeDropdown.SelectionChanged -= CustomThemeDropdown_SelectionChanged;
                    CustomThemeDropdown.SelectedItem = e.RemovedItems[0];
                    CustomThemeDropdown.SelectionChanged += CustomThemeDropdown_SelectionChanged;
                }
            }
        }

        private bool ApplyThemeByNameAndMode(string name, string mode)
        {
            try
            {
                Uri themeUri;
                
                if (name == "default")
                {
                    themeUri = new Uri($"pack://application:,,,/Themes/{mode}Theme.xaml", UriKind.Absolute);
                }
                else
                {
                    var theme = _discoveredThemes.FirstOrDefault(t => t.Name == name && t.Mode == mode);
                    if (theme == null) // Fallback if requested mode doesn't exist for this theme
                    {
                        theme = _discoveredThemes.FirstOrDefault(t => t.Name == name);
                    }
                    
                    if (theme != null)
                        themeUri = new Uri(theme.FilePath, UriKind.Absolute);
                    else
                        themeUri = new Uri($"pack://application:,,,/Themes/{mode}Theme.xaml", UriKind.Absolute);
                }

                var resourceDict = new ResourceDictionary { Source = themeUri };
                var appResources = System.Windows.Application.Current.Resources.MergedDictionaries;

                // 1. Identify and remove only the "Main" theme and "Custom" themes
                // We avoid removing unrelated dictionaries (like Icons or Converters)
                for (int i = appResources.Count - 1; i >= 0; i--)
                {
                    string? source = appResources[i].Source?.OriginalString;
                    if (string.IsNullOrEmpty(source)) continue;

                    if (source.Contains("DarkTheme.xaml") || source.Contains("LightTheme.xaml") || source.Contains("Color_Themes"))
                    {
                        appResources.RemoveAt(i);
                    }
                }

                // 2. If applying a custom theme, ensure the base DarkTheme is present first for fallback
                if (name != "default")
                {
                    string baseUriStr = "pack://application:,,,/Themes/DarkTheme.xaml";
                    if (!appResources.Any(d => d.Source?.ToString().Equals(baseUriStr, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        appResources.Add(new ResourceDictionary { Source = new Uri(baseUriStr, UriKind.Absolute) });
                    }
                }

                // Add new theme dictionary
                appResources.Add(resourceDict);              
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error applying {name} theme in ({mode}) mode: {ex.Message}");
                
                string message = TranslationHelper.GetString("Error_ApplyingTheme", "Error applying theme:") 
                + $" {name} ({mode}) - "
                + TranslationHelper.GetString("Error_Title", "Error")
                + $" {ex.Message}";
                string title = TranslationHelper.GetString("Error_Title", "Error");
                ModernMessageBox.Show(message, title, MessageBoxButton.OK, this);
                return false;
            }
        }

        /// <summary>
        /// Updates the theme selection visuals.
        /// </summary>
        private void UpdateThemeSelection(string theme)
        {
            try
            {
                if (theme == "Light")
                {
                    LightModeBox.Tag = "Current";
                    DarkModeBox.Tag = "Other";
                }
                else
                {
                    DarkModeBox.Tag = "Current";
                    LightModeBox.Tag = "Other";
                }
            }
            catch (Exception ex)
            {
                // Log if this happens, though it's often expected during early initialization
                LoggingService.Instance.Log($"Visual update for theme '{theme}' failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the theme from settings file.
        /// </summary>
        private (string Name, string Mode) LoadThemeFromSettings()
        {
            try
            {
                if (FileService.Instance.FileExists(SETTINGS_FILE)) // Use FileService
                {
                    string json = FileService.Instance.ReadAllText(SETTINGS_FILE); // Use FileService
                    string name = "default";
                    string mode = "Dark";

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("ThemeName", out JsonElement nameEl))
                            name = nameEl.GetString() ?? "default";
                        
                        if (doc.RootElement.TryGetProperty("ThemeMode", out JsonElement modeEl))
                            mode = modeEl.GetString() ?? "Dark";
                        else if (doc.RootElement.TryGetProperty("Theme", out JsonElement legacyEl))
                        {
                            // Migration logic: if old "Theme" exists, use it as mode
                            mode = legacyEl.GetString() ?? "Dark";
                        }
                    }
                    return (name, mode);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error reading {SETTINGS_FILE} for theme loading: {ex.Message}");
            }
            return ("default", "Dark");
        }

        /// <summary>
        /// Saves the theme to settings file.
        /// </summary>
        private void SaveThemeToSettings(string themeName, string themeMode)
        {
            try
            {
                var settings = new System.Collections.Generic.Dictionary<string, object>();

                // Load existing settings
                if (FileService.Instance.FileExists(SETTINGS_FILE)) // Use FileService
                {
                    string json = FileService.Instance.ReadAllText(SETTINGS_FILE);
                    var doc = JsonDocument.Parse(json);
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        if (property.Name == "Theme" || property.Name == "ThemeName" || property.Name == "ThemeMode")
                            continue; // Skip theme, we'll update it
                        settings[property.Name] = property.Value.GetString() ?? string.Empty;
                    }
                }

                settings["ThemeName"] = themeName;
                settings["ThemeMode"] = themeMode;

                // Save updated settings
                string updatedJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }); // This is fine
                FileService.Instance.WriteAllText(SETTINGS_FILE, updatedJson); // Use FileService
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error saving theme to {SETTINGS_FILE}: {ex.Message}");
                
                string message = TranslationHelper.GetString("Error_SavingSettings", $"Error saving settings: {ex.Message}");
                string title = TranslationHelper.GetString("Error_Title", "Error");
                ModernMessageBox.Show(message, title, MessageBoxButton.OK, this);
            }
        }

        /// <summary>
        /// Handles hyperlink navigation requests by opening the specified URI in the default web browser.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing navigation information.</param>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }

        /// <summary>
        /// Saves the language preference to settings file.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// Represents a media folder entry.
    /// </summary>
    public class MediaFolder
    {
        public string Path { get; set; }
        public int Count { get; set; }
    }
}
