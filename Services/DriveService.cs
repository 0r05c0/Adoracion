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
using System.IO;
using System.Linq;

namespace Adoracion.Services
{
    public sealed class DriveService
    {
        private static readonly Lazy<DriveService> _instance = new(() => new DriveService());
        public static DriveService Instance => _instance.Value;

        private List<DriveInfo> _removableDrives = new();
        public IReadOnlyList<DriveInfo> RemovableDrives => _removableDrives;
        public DriveInfo? SelectedDrive { get; set; }

        private DriveService() 
        {
            RefreshDrives();
        }

        public void RefreshDrives()
        {
            try
            {
                // Get the root of the system drive (e.g., "C:\") to ensure we don't include it
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";

                _removableDrives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && 
                               (d.DriveType == DriveType.Removable || 
                                (d.DriveType == DriveType.Fixed && !string.Equals(d.Name, systemDrive, StringComparison.OrdinalIgnoreCase))))
                    .ToList();

                // Update selection if the previously selected drive was removed
                if (SelectedDrive != null && !_removableDrives.Any(d => d.Name == SelectedDrive.Name)) 
                    SelectedDrive = null;

                // Default to the first detected drive if none is selected
                if (SelectedDrive == null && _removableDrives.Any()) 
                    SelectedDrive = _removableDrives.First();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"RefreshDrives failed: {ex.Message}");
            }
        }
    }
}