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
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using LibVLCSharp.Shared; // Keep this for LibVLCSharp types
using Adoracion.Models; // Now ScreenInfo is definitely here
using Adoracion.Helpers;
using Adoracion.Services;

namespace Adoracion
{
    public partial class MediaScreen : Window
    {
        private DispatcherTimer? updateTimer;
        private DispatcherTimer? _overlayTimer;
        private ScreenInfo? screenInfo;
        private bool isPlayingVideo = false; // This variable seems unused, consider removing if not needed
        private string? _lastLoadedImage;
        private int _activeImageIndex = 0;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
        private bool _isActuallyClosing = false;

        public event EventHandler<RoutedEventArgs>? MediaOpened;
        public event EventHandler<RoutedEventArgs>? MediaEnded;
        public event EventHandler? PlayPauseClicked;
        public event EventHandler? CloseRequested;
        
        /// <summary>
        /// When true, suppresses the automatic call to ResetToIdleState from LibVLC events.
        /// Used when manually handling image display to prevent conflicts with wallpaper.
        /// </summary>
        public bool SuppressResetToIdleState { get; set; } = false;

        public static readonly DependencyProperty ShowOverlayControlsProperty =
            DependencyProperty.Register("ShowOverlayControls", typeof(bool), typeof(MediaScreen), new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets a value indicating whether overlay controls should be displayed.
        /// </summary>
        public bool ShowOverlayControls
        {
            get { return (bool)GetValue(ShowOverlayControlsProperty); }
            set { SetValue(ShowOverlayControlsProperty, value); }
        }

        /// <summary>
        /// Initializes a new instance of the MediaScreen window with a MediaPlayer.
        /// </summary>
        /// <param name="mediaPlayer">The LibVLC MediaPlayer instance to display video from.</param>
        public MediaScreen(LibVLCSharp.Shared.MediaPlayer mediaPlayer)
        {
            InitializeComponent();
            InitializeOverlayTimer();
            
            // Subscribe to language changes
            TranslationHelper.LanguageChanged += (s, e) => Dispatcher.Invoke(RefreshUIText);
            
            // Subscribe to settings changes for real-time overlay text update
            AppSettingsService.SettingChanged += OnSettingChanged;
            RefreshUIText();

            _mediaPlayer = mediaPlayer;
            // Defer setting MediaPlayer until VlcPlayer is loaded to ensure proper initialization
            if (VlcPlayer != null) VlcPlayer.Loaded += VlcPlayer_Loaded;
            
            // Update icon when playback state changes globally
            PlaybackService.Instance.PlaybackStateChanged += OnGlobalPlaybackStateChanged;

            LoggingService.Instance.Log("MediaScreen: Initializing with MediaPlayer.");

            // Setup event handlers with Dispatcher to marshal to UI thread
            _mediaPlayer.Opening += OnMediaPlayerOpening;
            _mediaPlayer.Playing += OnMediaPlayerPlaying;
            _mediaPlayer.Stopped += OnMediaPlayerStopped;
            _mediaPlayer.EndReached += OnMediaPlayerEndReached;
        }

        /// <summary>
        /// Event handler for when the VlcPlayer control is loaded.
        /// Attaches the MediaPlayer instance to the VideoView.
        /// </summary>
        private void VlcPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            if (VlcPlayer != null && _mediaPlayer != null)
            {
                VlcPlayer.MediaPlayer = _mediaPlayer;
                VlcPlayer.Loaded -= VlcPlayer_Loaded; // Unsubscribe to prevent multiple assignments
            }
        }

        private void OnMediaPlayerStopped(object? sender, EventArgs e)
        {
            if (SuppressResetToIdleState) return; // Suppress if we are manually handling image display
            Dispatcher.BeginInvoke(new Action(() => ResetToIdleState(force: false)));
        }

        private void OnMediaPlayerEndReached(object? sender, EventArgs e)
        {
            if (SuppressResetToIdleState) return; // Suppress if we are manually handling image display
            Dispatcher.BeginInvoke(new Action(() => 
            {
                if (IsVisualMedia())
                {
                    ResetToIdleState(force: false);
                }
                MediaEnded?.Invoke(this, new RoutedEventArgs());
            }));
        }
        
        // ... (rest of the file) ...

        private void OnMediaPlayerOpening(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => HandleMediaStarted()));
        }

        private void OnMediaPlayerPlaying(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => HandleMediaStarted()));
        }

        private void OnGlobalPlaybackStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => UpdatePlayPauseIcon(PlaybackService.Instance.IsPlaying));
        }

        /// <summary>
        /// Initializes a new instance of the MediaScreen window in idle mode.
        /// Displays a welcome message until media is loaded.
        /// </summary>
        public MediaScreen()
        {
            InitializeComponent();
            InitializeIdleMode();
            InitializeOverlayTimer();

            // Subscribe to language changes
            TranslationHelper.LanguageChanged += (s, e) => Dispatcher.Invoke(RefreshUIText);
            
            // Subscribe to settings changes for real-time overlay text update
            AppSettingsService.SettingChanged += OnSettingChanged;
            RefreshUIText();
            
            // Setup event handlers for when video is loaded later
            if (MediaElement != null)
            {
                MediaElement.MediaOpened += (s, e) => 
                { 
                    FadeLabelOut();
                    MediaElement.Play();
                    ShowControls();
                    MediaOpened?.Invoke(this, e); 
                    HideBothImages(); // Ensure any previous images are cleared
                };
                MediaElement.MediaEnded += (s, e) => 
                {
                    ShowControls();
                    MediaEnded?.Invoke(this, e);
                    LoadAndApplyBackgroundImage(); // Ensure default wallpaper is loaded
                };
            }
        }

        /// <summary>
        /// Initializes a new instance of the MediaScreen window with a specific video file and screen.
        /// </summary>
        /// <param name="videoPath">The path to the video file to play.</param>
        /// <param name="screenInfo">The screen information for positioning the window.</param>
        public MediaScreen(string videoPath, ScreenInfo screenInfo)
        {
            InitializeComponent();
            this.screenInfo = screenInfo;
            InitializeOverlayTimer();

            // Subscribe to language changes
            TranslationHelper.LanguageChanged += (s, e) => Dispatcher.Invoke(RefreshUIText);
            
            // Subscribe to settings changes for real-time overlay text update
            AppSettingsService.SettingChanged += OnSettingChanged;
            RefreshUIText();

            // Configure window for fullscreen on secondary monitor
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            WindowStartupLocation = WindowStartupLocation.Manual;
            
            // Position and size window on secondary monitor
            Left = screenInfo.Bounds.Left;
            Top = screenInfo.Bounds.Top;
            Width = screenInfo.Bounds.Width;
            Height = screenInfo.Bounds.Height;

            // Setup update timer for progress
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromMilliseconds(100);
            updateTimer.Tick += UpdateTimer_Tick;

            // Load the video
            if (MediaElement != null)
            {
                MediaElement.Source = new System.Uri(videoPath);
            }
            
            isPlayingVideo = true;
        }

        private void OnSettingChanged(string key)
        {
            if (key == "SelectedScreen")
            {
                Dispatcher.Invoke(() =>
                {
                    var screens = Screen.AllScreens;
                    string savedScreen = AppSettingsService.GetSetting("SelectedScreen", "");

                    if (screenInfo != null && screenInfo.DeviceName == savedScreen) {
                        if (this.IsLoaded) RestoreContent();
                        return;
                    }

                    var targetScreen = ScreenService.Instance.GetAllScreens().FirstOrDefault(s => s.DeviceName == savedScreen) ?? ScreenService.Instance.GetAllScreens().FirstOrDefault(s => !s.Primary);

                    if (targetScreen != null)
                    {
                        // 1. Start a quick fade out to hide the "jump"
                        var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.15));
                        fadeOut.Completed += (s, e) => 
                        {
                            screenInfo = new ScreenInfo
                            {
                                DeviceName = targetScreen.DeviceName,
                                Bounds = targetScreen.Bounds,
                                DisplayName = targetScreen.DisplayName
                            };

                            // 2. Move while invisible and clear any existing animation state
                            this.BeginAnimation(OpacityProperty, null);
                            this.Opacity = 0;

                            // Always set to Normal before monitor jumps to avoid Windows "snapping"
                            this.WindowState = WindowState.Normal;
                            this.Left = targetScreen.Bounds.Left + 10;
                            this.Top = targetScreen.Bounds.Top + 10;

                            // 3. Defer maximization and restoration until the window has settled on the new monitor
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                this.WindowState = WindowState.Maximized;
                                RefreshUIText();
                                RestoreContent();
                                this.UpdateLayout(); // Force layout pass while still invisible

                                // 4. Fade back in smoothly
                                var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.35))
                                {
                                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                                };
                                this.BeginAnimation(OpacityProperty, fadeIn);
                            }), DispatcherPriority.Background);
                        };
                        this.BeginAnimation(OpacityProperty, fadeOut);
                    }
                });
            }
            else if (AppearanceSettings.IsAppearanceKey(key))
            {
                Dispatcher.Invoke(RefreshUIText);
            }
        }

        private void RestoreContent()
        {
            if (LabelOverlay != null) LabelOverlay.Visibility = Visibility.Visible;
            LoadAndApplyBackgroundImage(force: true); // This will show/hide BackgroundImage based on settings
        }

        private void InitializeOverlayTimer()
        {
            _overlayTimer = new DispatcherTimer();
            _overlayTimer.Interval = TimeSpan.FromSeconds(2);
            _overlayTimer.Tick += (s, e) => HideControls();
        }

        /// <summary>
        /// Initializes the window in idle mode by detecting available secondary screens.
        /// </summary>
        private void InitializeIdleMode()
        {
            var screens = ScreenService.Instance.GetAllScreens();
            string savedScreen = AppSettingsService.GetSetting("SelectedScreen", "");
            
            var targetScreen = screens.FirstOrDefault(s => s.DeviceName == savedScreen); // Find the saved screen
            if (targetScreen == null)
            {
                targetScreen = screens.FirstOrDefault(s => !s.Primary);
                if (targetScreen != null)
                {
                    AppSettingsService.SetSetting("SelectedScreen", targetScreen.DeviceName);
                }
            }

            if (targetScreen != null)
            {
                // Position window on target screen and make it fullscreen
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                WindowStartupLocation = WindowStartupLocation.Manual;
                
                // Store screen info for use in Window_Loaded
                this.screenInfo = new ScreenInfo
                {
                    DeviceName = targetScreen.DeviceName,
                    Bounds = targetScreen.Bounds,
                    DisplayName = targetScreen.DeviceName
                };
            }
            else
            {
                // Fallback to normal window on primary screen
                WindowState = WindowState.Normal;
                this.Opacity = 0.6;
                
                // Configure window for fallback mode
                ConfigureFallbackMode();
                
                // Ensure controls are visible
                if (ControlsOverlay != null)
                {
                    ControlsOverlay.Visibility = Visibility.Visible;
                }
                
            }
        }

        /// <summary>
        /// Ensures controls are visible and properly configured for fallback mode.
        /// </summary>
        public void ConfigureFallbackMode()
        {
            // Ensure window is visible and properly sized for primary screen
            if (WindowState == WindowState.Normal)
            {
                Width = 800;
                Height = 600;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            
            // Make sure controls are clearly visible
            this.Topmost = true;
            
            // Ensure label is visible in fallback mode
            ShowLabel();
            
            // Ensure overlay controls are visible
            ShowOverlayControls = true;
            
            // Force controls overlay to be visible
            if (ControlsOverlay != null)
            {
                ShowControls();
            }
            
        }

        /// <summary>
        /// Handles the window loaded event.
        /// Applies final positioning and sizing to the window.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // If screenInfo is not set (idle mode), get secondary screen
            if (screenInfo == null)
            {
                var screens = ScreenService.Instance.GetAllScreens();
                string savedScreen = AppSettingsService.GetSetting("SelectedScreen", "");
                
                var targetScreen = screens.FirstOrDefault(s => s.DeviceName == savedScreen); // Find the saved screen
                if (targetScreen == null)
                {
                    targetScreen = screens.FirstOrDefault(s => !s.Primary);
                }
                
                if (targetScreen != null)
                {
                    screenInfo = new ScreenInfo
                    {
                        DeviceName = targetScreen.DeviceName,
                        Bounds = targetScreen.Bounds,
                        DisplayName = targetScreen.DisplayName
                    };
                }
            }

            // Apply positioning if we have screen info
            if (screenInfo != null)
            {
                WindowStyle = WindowStyle.None;
                WindowStartupLocation = WindowStartupLocation.Manual;
                
                // Set window state to Normal first to allow size/position changes
                WindowState = WindowState.Normal;
                
                // Set window position and size to match secondary screen exactly
                Left = screenInfo.Bounds.Left;
                Top = screenInfo.Bounds.Top;
                Width = screenInfo.Bounds.Width;
                Height = screenInfo.Bounds.Height;
                
                // Now maximize to fill the screen completely without gaps
                WindowState = WindowState.Maximized;
            }            
            else if (ShowOverlayControls)
            {
                // Ensure window decorations are visible when in fallback mode
                WindowStyle = WindowStyle.SingleBorderWindow;
                ConfigureFallbackMode();
                
                // Ensure controls are visible
                if (ControlsOverlay != null)
                {
                    ShowControls();
                }
            }

             // Ensure the overlay position updates dynamically when the window size changes
            this.SizeChanged += (s, ev) => ApplyOverlayPosition();


            // Finalize text positioning once window dimensions and visual tree are set
            ApplyOverlayPosition();
            LoadAndApplyBackgroundImage(force: true);
        }

        /// <summary>
        /// Refreshes UI text based on current language.
        /// </summary>
        private void RefreshUIText()
        {
            this.Title = TranslationHelper.GetString("Title_MediaScreen", "Multi window player");
            
            string customText = AppearanceSettings.GetOverlayText();
            double fontSize = AppearanceSettings.GetFontSize();
            string colorHex = AppearanceSettings.GetTextColor();            
            double alphaPct = AppearanceSettings.GetTextAlpha();
            // Apply Border/Outline using DropShadowEffect
            bool enableShadow = AppearanceSettings.GetEnableShadow();

            if (MainText != null)
            {
                MainText.Text = customText;
                MainText.FontSize = fontSize;

                // Apply color and alpha
                try
                {
                    System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                    color.A = (byte)((alphaPct / 100.0) * 255);
                    MainText.Foreground = new SolidColorBrush(color);

                    if (enableShadow)
                    {
                        string shadowColorHex = AppearanceSettings.GetShadowColor();
                        System.Windows.Media.Color shadowColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(shadowColorHex);
                        shadowColor.A = color.A; // Shared Alpha
                        double blur = AppearanceSettings.GetShadowBlur();
                        double depth = AppearanceSettings.GetShadowDepth();
                        double shadowOpacity = AppearanceSettings.GetShadowOpacity() / 100.0;
                        
                        MainText.Effect = new DropShadowEffect
                        {
                            Color = shadowColor,
                            BlurRadius = blur,
                            ShadowDepth = depth,
                            Opacity = shadowOpacity
                        };
                    }
                    else { MainText.Effect = null; }
                }
                catch { MainText.Foreground = System.Windows.Media.Brushes.Black; }

                ApplyOverlayPosition();
                LoadAndApplyBackgroundImage(force: false); // Ensure background image is updated if text changes
            }
        }

        /// <summary>
        /// Applies the alignment stored in SQLite to the MainText element.
        /// </summary>
        private void ApplyOverlayPosition()
        {
            if (MainText == null || LabelOverlay == null) return;
            
            string pos = AppearanceSettings.GetOverlayPosition();
            
            System.Windows.HorizontalAlignment halign = System.Windows.HorizontalAlignment.Center;
            System.Windows.VerticalAlignment valign = System.Windows.VerticalAlignment.Center;
            System.Windows.TextAlignment talign = System.Windows.TextAlignment.Center;

            // Pre-calculate resolution using screenInfo bounds to prevent jumping.
            // ActualWidth/Height often lag behind during monitor changes.
            double refWidth = (screenInfo != null) ? screenInfo.Bounds.Width : (this.ActualWidth > 0 ? this.ActualWidth : 1920);
            double refHeight = (screenInfo != null) ? screenInfo.Bounds.Height : (this.ActualHeight > 0 ? this.ActualHeight : 1080);

            double mx = refWidth * 0.01;
            double my = refHeight * 0.01;

            System.Windows.Thickness margin = new System.Windows.Thickness(0);

            // The definition of OverlayPosition is based on a 3x3 grid with 1% margin from edges for corner positions, and centered for middle positions. 
            switch (pos)
            {
                case "TopLeft": 
                    halign = System.Windows.HorizontalAlignment.Left; 
                    valign = System.Windows.VerticalAlignment.Top; 
                    talign = System.Windows.TextAlignment.Left; 
                    margin = new System.Windows.Thickness(mx, my, 0, 0); break;
                case "TopCenter": 
                    halign = System.Windows.HorizontalAlignment.Center; 
                    valign = System.Windows.VerticalAlignment.Top; 
                    talign = System.Windows.TextAlignment.Center; 
                    margin = new System.Windows.Thickness(0, my, 0, 0); break;
                case "TopRight": 
                    halign = System.Windows.HorizontalAlignment.Right; 
                    valign = System.Windows.VerticalAlignment.Top; 
                    talign = System.Windows.TextAlignment.Right; 
                    margin = new System.Windows.Thickness(0, my, mx, 0); break;
                case "CenterLeft": 
                    halign = System.Windows.HorizontalAlignment.Left; 
                    valign = System.Windows.VerticalAlignment.Center; 
                    talign = System.Windows.TextAlignment.Left; 
                    margin = new System.Windows.Thickness(mx, 0, 0, 0); break;
                case "Center": 
                    halign = System.Windows.HorizontalAlignment.Center; 
                    valign = System.Windows.VerticalAlignment.Center; 
                    talign = System.Windows.TextAlignment.Center; 
                    margin = new System.Windows.Thickness(0); break;
                case "CenterRight": 
                    halign = System.Windows.HorizontalAlignment.Right; 
                    valign = System.Windows.VerticalAlignment.Center; 
                    talign = System.Windows.TextAlignment.Right; 
                    margin = new System.Windows.Thickness(0, 0, mx, 0); break;
                case "BottomLeft": 
                    halign = System.Windows.HorizontalAlignment.Left; 
                    valign = System.Windows.VerticalAlignment.Bottom; 
                    talign = System.Windows.TextAlignment.Left; 
                    margin = new System.Windows.Thickness(mx, 0, 0, my); break;
                case "BottomCenter": 
                    halign = System.Windows.HorizontalAlignment.Center; 
                    valign = System.Windows.VerticalAlignment.Bottom; 
                    talign = System.Windows.TextAlignment.Center; 
                    margin = new System.Windows.Thickness(0, 0, 0, my); break;
                case "BottomRight": 
                    halign = System.Windows.HorizontalAlignment.Right; 
                    valign = System.Windows.VerticalAlignment.Bottom; 
                    talign = System.Windows.TextAlignment.Right; 
                    margin = new System.Windows.Thickness(0, 0, mx, my); break;
            }

            // Apply TextAlignment directly to the TextBlock
            MainText.TextAlignment = talign;
            
            // Clear local properties on MainText that might interfere with container-level alignment
            MainText.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            MainText.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            MainText.Margin = new System.Windows.Thickness(0);

            // Find the top-most container within the LabelOverlay (usually a Viewbox or Border)
            FrameworkElement topContainer = MainText;
            DependencyObject current = VisualTreeHelper.GetParent(MainText);
            while (current != null && current != LabelOverlay)
            {
                if (current is FrameworkElement fe)
                {
                    topContainer = fe;
                    fe.HorizontalAlignment = halign;
                    fe.VerticalAlignment = valign;
                    
                    if (fe is System.Windows.Controls.Viewbox vb)
                    {
                        vb.Stretch = System.Windows.Media.Stretch.Uniform; // Keeps text contained
                        vb.StretchDirection = System.Windows.Controls.StretchDirection.DownOnly; // Prevents Viewbox from overriding specific font size upwards
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }

            // Apply the margin and alignment to the topContainer to ensure the whole block moves
            topContainer.HorizontalAlignment = halign;
            topContainer.VerticalAlignment = valign;
            topContainer.Margin = margin;
        }

        /// <summary>
        /// Animates the label overlay to fade out when video playback begins.
        /// </summary>
        private void FadeLabelOut()
        {
            if (LabelOverlay == null)
            {
                return;
            }
            
            var fadeOutAnimation = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromSeconds(1.0),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                LabelOverlay.Visibility = Visibility.Hidden;
            };

            LabelOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }

        /// <summary>
        /// Displays the label overlay with a fade-in animation.
        /// </summary>
        public void ShowLabel()
        {
            LabelOverlay.Visibility = Visibility.Visible;
            
            var fadeInAnimation = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromSeconds(1.0),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            LabelOverlay.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
        }

        /// <summary>
        /// Immediately resets the screen to its idle state (Label and Background) 
        /// while hiding the video player to prevent black screen flickers.
        /// </summary>
        public void ResetToIdleState(bool force = true)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ResetToIdleState(force)));
                return;
            }

            // If not forced (i.e. triggered by LibVLC events), ignore if we are showing a playlist image.
            // This prevents 'Stop' events from clearing images during playlist transitions.
            if (!force && _lastLoadedImage != null && _lastLoadedImage != AppearanceSettings.GetBackgroundImagePath())
                return;

            LoggingService.Instance.Log("MediaScreen.ResetToIdleState: Entry.");            
            VlcPlayer?.BeginAnimation(UIElement.OpacityProperty, null); // Clear existing animations
            ShowLabel();
            
            ShowControls();
            
            // Restore the default wallpaper from settings
            LoggingService.Instance.Log("MediaScreen.ResetToIdleState: Restoring default background.");
            LoadAndApplyBackgroundImage(force: true);
        }

        /// <summary>
        /// Displays a playlist image using the native WPF Image control instead of LibVLC.
        /// This significantly reduces RAM consumption for high-resolution images.
        /// </summary>
        public void ShowPlaylistImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !FileService.Instance.FileExists(filePath)) return; // Use FileService

            Dispatcher.Invoke(() =>
            {
                FadeLabelOut(); 
                TransitionToImage(filePath);
            });
        }

        private void TransitionToImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !FileService.Instance.FileExists(filePath)) // Use FileService
            {
                _lastLoadedImage = null;
                HideBothImages();
                return;
            }

            if (_lastLoadedImage == filePath) return;
            _lastLoadedImage = filePath;

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                System.Windows.Controls.Image active = _activeImageIndex == 0 ? ImageA : ImageB;
                System.Windows.Controls.Image next = _activeImageIndex == 0 ? ImageB : ImageA;

                // Capture current opacities before clearing animations to prevent "snapping" to 0
                double currentActiveOpacity = active.Opacity;
                double currentContainerOpacity = ImageContainer.Opacity;
                double currentBackgroundOpacity = ImageBackground.Opacity;

                active.BeginAnimation(UIElement.OpacityProperty, null);
                ImageContainer.BeginAnimation(UIElement.OpacityProperty, null);
                ImageBackground.BeginAnimation(UIElement.OpacityProperty, null);
                next.BeginAnimation(UIElement.OpacityProperty, null);

                active.Opacity = currentActiveOpacity;
                ImageContainer.Opacity = currentContainerOpacity;
                ImageBackground.Opacity = currentBackgroundOpacity;

                // Detect if we are transitioning from a black/empty state
                bool isVlcActive = VlcPlayer != null && VlcPlayer.Visibility == Visibility.Visible && VlcPlayer.Opacity > 0;
                bool isComingFromHidden = currentContainerOpacity < 0.1;

                next.Source = bitmap;
                next.Visibility = Visibility.Visible;

                var transitionTime = TimeSpan.FromSeconds(0.8);
                var fadeIn = new DoubleAnimation(1.0, transitionTime)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };

                if (isComingFromHidden)
                {
                    if (isVlcActive)
                    {
                        // Fade the container in over the video
                        ImageContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    }
                    else
                    {
                        // No video playing (e.g. startup), still fade in for smoothness
                        ImageContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    }

                    // Set background to opaque; the container fade handles the visual entry
                    ImageBackground.Opacity = 1.0;

                    // Set image to opaque immediately. The ImageContainer fade handles the visual transition.
                    next.Opacity = 1.0;
                    active.Opacity = 0;
                    active.Visibility = Visibility.Collapsed;
                    active.Source = null;
                }
                else
                {
                    // Standard Crossfade
                    next.Opacity = 0;
                    var fadeOut = new DoubleAnimation(0.0, transitionTime) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };

                    fadeOut.Completed += (s, e) =>
                    {
                        active.Source = null;
                        active.Visibility = Visibility.Collapsed;
                    };

                    next.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    active.BeginAnimation(UIElement.OpacityProperty, fadeOut);

                    // Ensure container and blue background are fully visible during crossfade
                    ImageContainer.Opacity = 1.0;
                    ImageBackground.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                }

                _activeImageIndex = _activeImageIndex == 0 ? 1 : 0;
            }
            catch (Exception ex) { LoggingService.Instance.Log($"TransitionToImage Error: {ex.Message}"); }
        }

        /// <summary>
        /// Loads the background image from settings and applies it.
        /// </summary>
        /// <param name="force">When true, bypasses checks for active video playback.</param>
        private void LoadAndApplyBackgroundImage(bool force = false)
        {
            // Prevent the idle background image from overlaying the video surface 
            // if a video is currently active (playing, paused, or loading).
            if (!force && _mediaPlayer != null && IsVisualMedia() && 
                (_mediaPlayer.State == VLCState.Playing || _mediaPlayer.State == VLCState.Paused || 
                 _mediaPlayer.State == VLCState.Opening || _mediaPlayer.State == VLCState.Buffering))
            {
                return;
            }

            bool enableBgImage = AppearanceSettings.GetEnableBackgroundImage();
            string imagePath = AppearanceSettings.GetBackgroundImagePath();

            if (enableBgImage && !string.IsNullOrEmpty(imagePath) && FileService.Instance.FileExists(imagePath)) // Use FileService
            {
                try
                {
                    TransitionToImage(imagePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading background image: {ex.Message}");
                    HideBothImages();
                }
            }
            else
            {
                HideBothImages();
            }
        }

        /// <summary>
        /// Smoothly fades out both image controls. Used when transitioning to video 
        /// or when the background is disabled in settings.
        /// </summary>
        private void FadeOutBothImages()
        {
            _lastLoadedImage = null;

            // We MUST animate the ImageContainer to 0. Even if no images are present, 
            // the Black background of the container covers the video. 
            // Fading it to 0 reveals the video surface underneath.
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromSeconds(0.5))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                ImageA.Source = null;
                ImageB.Source = null;
                ImageA.Visibility = Visibility.Collapsed;
                ImageB.Visibility = Visibility.Collapsed;
            };

            ImageA.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            ImageB.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            ImageBackground.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            ImageContainer.BeginAnimation(UIElement.OpacityProperty, fadeOut); // Fade out ImageContainer
        }

        private void ShowBackgroundImage()
        {
            System.Windows.Controls.Image active = _activeImageIndex == 0 ? ImageA : ImageB;
            active.Visibility = Visibility.Visible;
            active.Opacity = 1.0;
        }

        private void HideBothImages()
        {
            _lastLoadedImage = null;
            ImageA.BeginAnimation(UIElement.OpacityProperty, null);
            ImageB.BeginAnimation(UIElement.OpacityProperty, null);
            ImageBackground.BeginAnimation(UIElement.OpacityProperty, null);
            ImageContainer.BeginAnimation(UIElement.OpacityProperty, null); // Clear animation
            ImageA.Source = null;
            ImageB.Source = null;
            ImageA.Visibility = Visibility.Collapsed;
            ImageB.Visibility = Visibility.Collapsed;
            ImageA.Opacity = 0;
            ImageB.Opacity = 0;
            // Ensure the background layer remains black and visible
            ImageBackground.Opacity = 1.0;
            ImageContainer.Opacity = 1.0;
        }

        private void FadeInPlayer()
        {
            if (VlcPlayer == null) return;
            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.8))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            VlcPlayer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void FadeOutPlayer()
        {
            // We no longer hide the root. We cover it with ImageContainer instead.
        }

        private void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ShowControls();
        }

        private void ShowControls()
        {
            if (ControlsOverlay == null) return;

            // Do not show controls if there is more than one monitor available
            if (System.Windows.Forms.Screen.AllScreens.Length > 1)
            {
                ControlsOverlay.Visibility = Visibility.Hidden;
                return;
            }

            // Reset the idle timer to prevent controls from hiding while mouse is moving
            _overlayTimer?.Stop();
            _overlayTimer?.Start();

            // Only start the fade-in if the controls aren't already fully visible
            if (ControlsOverlay.Visibility != Visibility.Visible || ControlsOverlay.Opacity < 1.0)
            {
                ControlsOverlay.Visibility = Visibility.Visible;
                
                var fadeInAnimation = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(0.4), // Smooth fade-in duration
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                
                ControlsOverlay.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            }
        }

        private void HideControls()
        {
            if (ControlsOverlay == null) return;

            var fadeOutAnimation = new DoubleAnimation
            {
                To = 0.05,
                Duration = TimeSpan.FromSeconds(0.6),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            ControlsOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }

        /// <summary>
        /// Starts the update timer for progress monitoring.
        /// </summary>
        public void StartUpdateTimer()
        {
            updateTimer.Start();
        }

        /// <summary>
        /// Stops the update timer.
        /// </summary>
        public void StopUpdateTimer()
        {
            updateTimer.Stop();
        }

        /// <summary>
        /// Updates the playback progress at regular intervals.
        /// </summary>
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // This will be called from the main window to update progress
        }

        /// <summary>
        /// Specifically handles the KeyDown event on the VLC VideoView.
        /// </summary>
        private void VlcPlayer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {           
            switch (e.Key)
            {
                case Key.Escape:
                    // Request stop via Main window to handle crossfade logic and centralized state management
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
                case Key.Space:
                    PlayPauseClicked?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
                case Key.Left:
                    if (PlaybackService.Instance.Player != null)
                        PlaybackService.Instance.SetTime(Math.Max(0, PlaybackService.Instance.GetTime() - 5000));
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (PlaybackService.Instance.Player != null)
                        PlaybackService.Instance.SetTime(PlaybackService.Instance.GetTime() + 5000);
                    e.Handled = true;
                    break;
                case Key.Up:
                    PlaybackService.Instance.SetVolume(Math.Min(100, (PlaybackService.Instance.Player?.Volume ?? 0) + 5));
                    e.Handled = true;
                    break;
                case Key.Down:
                    PlaybackService.Instance.SetVolume(Math.Max(0, (PlaybackService.Instance.Player?.Volume ?? 0) - 5));
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Handles keyboard input for media control and window management.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The keyboard event arguments.</param>
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    // Request stop via Main window to handle crossfade logic and centralized state management
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
                case Key.Space:
                    PlayPauseClicked?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
                case Key.Left:
                    if (PlaybackService.Instance.Player != null)
                        PlaybackService.Instance.SetTime(Math.Max(0, PlaybackService.Instance.GetTime() - 5000));
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (PlaybackService.Instance.Player != null)
                        PlaybackService.Instance.SetTime(PlaybackService.Instance.GetTime() + 5000);
                    e.Handled = true;
                    break;
                case Key.Up:
                    PlaybackService.Instance.SetVolume(Math.Min(100, (PlaybackService.Instance.Player?.Volume ?? 0) + 5));
                    e.Handled = true;
                    break;
                case Key.Down:
                    PlaybackService.Instance.SetVolume(Math.Max(0, (PlaybackService.Instance.Player?.Volume ?? 0) - 5));
                    e.Handled = true;
                    break;
            }
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

        /// <summary>
        /// Handles the window closing event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from global events to prevent memory leaks
            PlaybackService.Instance.PlaybackStateChanged -= OnGlobalPlaybackStateChanged;
            AppSettingsService.SettingChanged -= OnSettingChanged;

            if (_mediaPlayer == null) return;

            _mediaPlayer.Opening -= OnMediaPlayerOpening;
            _mediaPlayer.Playing -= OnMediaPlayerPlaying;
            _mediaPlayer.Stopped -= OnMediaPlayerStopped;
            _mediaPlayer.EndReached -= OnMediaPlayerEndReached;

            base.OnClosed(e);
        }

        /// <summary>
        /// Handles the close button click event.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles the play/pause overlay button click event.
        /// </summary>
        private void PlayPauseOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            PlayPauseClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles the minimize button click event.
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }       
        
        /// <summary>
        /// Updates the play/pause button icon based on playback state.
        /// </summary>
        /// <param name="isPlaying">A value indicating whether media is currently playing.</param>
        public void UpdatePlayPauseIcon(bool isPlaying)
        {
            if (PlayPausePath != null)
            {
                PlayPausePath.Data = (Geometry)FindResource(isPlaying ? "PauseIcon" : "PlayArrowIcon");
            }
        }

        /// <summary>
        /// Determines the visual state of the screen based on whether the media is a video/image or audio.
        /// </summary>
        private void HandleMediaStarted()
        {
            if (IsVisualMedia())
            {
                // Reset opacity to 0 so we can perform the "expected" visual fade-in
                if (VlcPlayer != null)
                {
                    VlcPlayer.BeginAnimation(UIElement.OpacityProperty, null);
                    VlcPlayer.Opacity = 0;
                }

                FadeInPlayer();

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.AspectRatio = string.Empty; 
                }
                
                FadeLabelOut();
                FadeOutBothImages(); 
            }
            else
            {
                LoadAndApplyBackgroundImage(); // Force reload the wallpaper for audio files
                ShowLabel();
            }
            ShowControls();
        }

        private bool IsVisualMedia()
        {
            // Use a local reference to prevent the Media object from being nullified 
            // mid-check by the PlaybackService.
            var currentMedia = _mediaPlayer?.Media;
            if (currentMedia == null) return false;

            try
            { 
                var uri = new Uri(currentMedia.Mrl);
                string ext = System.IO.Path.GetExtension(uri.LocalPath).ToLower();
                return !MediaHelper.AudioExtensions.Contains(ext);
            }
            catch { return false; }
        }
    }
}
