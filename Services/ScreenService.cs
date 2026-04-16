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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Adoracion.Models;
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

        /// <summary>
        /// Generates a user-friendly display name for a screen.
        /// </summary>
        private string GetScreenDisplayName(Screen screen)
        {
            // Example: "\\.\DISPLAY1 (Primary)" or "\\.\DISPLAY2"
            string name = screen.DeviceName.Replace("\\\\.\\", ""); // Remove common prefix
            return $"{name} ({(screen.Primary ? "Primary" : "Secondary")})";
        }
    }
}