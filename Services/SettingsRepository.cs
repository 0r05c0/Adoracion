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
using System.Text.Json;

namespace Adoracion.Services
{
    /// <summary>
    /// Repository for managing application settings using JSON file storage.
    /// Provides a simple and lightweight persistence mechanism for user preferences.
    /// </summary>
    public static class SettingsRepository
    {
        private static readonly string SettingsPath = "UserSettings.json";
        private static Dictionary<string, string> _settings = new Dictionary<string, string>();

        static SettingsRepository()
        {
            LoadSettings();
        }

        /// <summary>
        /// Loads settings from the JSON file.
        /// </summary>
        private static void LoadSettings()
        {
            try
            {
                if (FileService.Instance.FileExists(SettingsPath)) // Use FileService
                {
                    string json = FileService.Instance.ReadAllText(SettingsPath); // Use FileService
                    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (settings != null)
                    {
                        _settings = settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves settings to the JSON file.
        /// </summary>
        private static void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                FileService.Instance.WriteAllText(SettingsPath, json); // Use FileService
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves or updates a setting.
        /// </summary>
        public static void SaveSetting(string key, string value)
        {
            try
            {
                _settings[key] = value;
                SaveSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving setting: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a setting.
        /// </summary>
        public static Setting GetSetting(string key)
        {
            try
            {
                if (_settings.TryGetValue(key, out var value))
                {
                    return new Setting
                    {
                        Key = key,
                        Value = value,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving setting: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a setting.
        /// </summary>
        public static void DeleteSetting(string key)
        {
            try
            {
                if (_settings.ContainsKey(key))
                {
                    _settings.Remove(key);
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting setting: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Represents an application setting.
    /// </summary>
    public class Setting
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
