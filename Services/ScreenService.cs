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
using Adoracion.Models;
using Adoracion.Helpers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;

namespace Adoracion.Services
{
    /// <summary>
    /// Provides information about available display screens.
    /// Encapsulates System.Windows.Forms.Screen to prevent namespace conflicts in UI code.
    /// </summary>
    public sealed class ScreenService
    {
        private static readonly Lazy<ScreenService> _instance = new Lazy<ScreenService>(() => new ScreenService());
        public static ScreenService Instance => _instance.Value;

        private ScreenService() { }

        /// <summary>
        /// Retrieves a list of all available display screens.
        /// </summary>
        /// <returns>A read-only list of ScreenInfo objects.</returns>
        public IReadOnlyList<ScreenInfo> GetAllScreens()
        {
            return Screen.AllScreens.Select(s => new ScreenInfo
            {
                DeviceName = s.DeviceName,
                Bounds = s.Bounds,
                WorkingArea = s.WorkingArea, // Include WorkingArea for taskbar-aware positioning
                Primary = s.Primary,
                DisplayName = GetScreenDisplayName(s)
            }).ToList();
        }

        /// <summary>
        /// Retrieves the primary display screen.
        /// </summary>
        /// <returns>The ScreenInfo object for the primary screen, or null if not found.</returns>
        public ScreenInfo? GetPrimaryScreen()
        {
            return GetAllScreens().FirstOrDefault(s => s.Primary);
        }

        public bool IsMultipleScreens()
        {
            return Screen.AllScreens.Length > 1;
        }

        /// <summary>
        /// Retrieves the screen intended for the UI (not the one selected for media).
        /// Falls back to primary screen if only one monitor is available.
        /// </summary>
        public ScreenInfo GetUIScreen()
        {
            string mediaScreenName = AppSettingsService.GetSetting("SelectedScreen", "");
            var screens = GetAllScreens();

            if (screens.Count <= 1)
                return GetPrimaryScreen() ?? screens.FirstOrDefault() ?? new ScreenInfo();

            return screens.FirstOrDefault(s => s.DeviceName != mediaScreenName)
                   ?? GetPrimaryScreen()
                   ?? screens[0];
        }

        /// <summary>
        /// Moves and animates a window to the UI screen.
        /// Consolidates logic for Main and SettingsWindow.
        /// </summary>
        /// <param name="window">The window to move.</param>
        /// <param name="fillScreen">If true, the window will fill the working area (custom maximize). If false, it will center itself.</param>
        /// <param name="onLayoutUpdated">Callback triggered when the layout is updated (e.g., to sync local state).</param>
        public void MoveWindowToUIScreen(Window window, bool fillScreen = true, Action<bool>? onLayoutUpdated = null)
        {
            var targetScreen = GetUIScreen();
            if (targetScreen == null) return;

            // Avoid unnecessary jumps if already on target screen and multiple screens are available
            var helper = new WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero && IsMultipleScreens())
            {
                var currentScreen = System.Windows.Forms.Screen.FromHandle(helper.Handle);
                if (currentScreen.DeviceName == targetScreen.DeviceName) return;
            }
            else if (!IsMultipleScreens())
            {
                // Fallback for single monitor: Ensure normal state and notify layout change
                window.WindowState = WindowState.Normal;
                onLayoutUpdated?.Invoke(false);
                return;
            }

            var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.15));
            fadeOut.Completed += (s, e) =>
            {
                var dpi = VisualTreeHelper.GetDpi(window);
                window.WindowState = WindowState.Normal;

                if (fillScreen)
                {
                    window.Left = targetScreen.WorkingArea.Left / dpi.DpiScaleX;
                    window.Top = targetScreen.WorkingArea.Top / dpi.DpiScaleY;
                    window.Width = targetScreen.WorkingArea.Width / dpi.DpiScaleX;
                    window.Height = targetScreen.WorkingArea.Height / dpi.DpiScaleY;
                }
                else
                {
                    double windowWidth = window.ActualWidth > 0 ? window.ActualWidth : (double.IsNaN(window.Width) ? 1200 : window.Width);
                    double windowHeight = window.ActualHeight > 0 ? window.ActualHeight : (double.IsNaN(window.Height) ? 800 : window.Height);

                    window.Left = (targetScreen.WorkingArea.Left / dpi.DpiScaleX) + ((targetScreen.WorkingArea.Width / dpi.DpiScaleX) - windowWidth) / 2;
                    window.Top = (targetScreen.WorkingArea.Top / dpi.DpiScaleY) + ((targetScreen.WorkingArea.Height / dpi.DpiScaleY) - windowHeight) / 2;
                }

                onLayoutUpdated?.Invoke(fillScreen);

                var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.25));
                window.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };
            window.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        /// <summary>
        /// Toggles a custom maximize state that fills the working area (taskbar-aware) 
        /// of the monitor the window is currently on.
        /// </summary>
        /// <param name="window">The window to maximize or restore.</param>
        /// <param name="isCurrentlyMaximized">The current custom maximization state.</param>
        /// <param name="restoreBounds">The bounds to restore the window to when un-maximizing.</param>
        /// <returns>The new maximization state.</returns>
        public bool ToggleCustomMaximize(Window window, bool isCurrentlyMaximized, Rect restoreBounds)
        {
            if (isCurrentlyMaximized)
            {
                window.WindowState = WindowState.Normal;
                window.Left = restoreBounds.Left;
                window.Top = restoreBounds.Top;
                window.Width = restoreBounds.Width;
                window.Height = restoreBounds.Height;
                return false;
            }
            else
            {
                var helper = new WindowInteropHelper(window);
                var screen = Screen.FromHandle(helper.Handle);
                var dpi = VisualTreeHelper.GetDpi(window);

                // Ensure state is normal so we can set Left/Top/Width/Height
                window.WindowState = WindowState.Normal;
                
                window.Left = screen.WorkingArea.Left / dpi.DpiScaleX;
                window.Top = screen.WorkingArea.Top / dpi.DpiScaleY;
                window.Width = screen.WorkingArea.Width / dpi.DpiScaleX;
                window.Height = screen.WorkingArea.Height / dpi.DpiScaleY;
                
                return true;
            }
        }

        /// <summary>
        /// Generates a user-friendly display name for a screen.
        /// </summary>
        private string GetScreenDisplayName(Screen screen)
        {
            // Example: "\\.\DISPLAY1 (Primary)" or "\\.\DISPLAY2"
            string name = screen.DeviceName.Replace("\\\\.\\", ""); // Remove common prefix
            return $"{name} ({(screen.Primary ? TranslationHelper.GetString("Label_Primary", "Primary") : TranslationHelper.GetString("Label_Secondary","Secondary"))})";
        }
    }
}