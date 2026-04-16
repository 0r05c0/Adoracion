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
using System.Configuration;
using System.IO;
using System.Text.Json;
using System.Windows;
using Adoracion.Helpers;
using Adoracion.Services;

namespace Adoracion
{
    /// <summary>
    /// Application class for Adoracion media player.
    /// Manages application lifecycle and initializes localization and theme.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private const string SETTINGS_FILE = "UserSettings.json";

        /// <summary>
        /// Handles application startup event.
        /// Initializes localization and core services, and loads saved theme.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize localization service
            string translationsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "Languages.json");
            TranslationHelper.Initialize(translationsPath);

            // Load and apply saved theme
            LoadAndApplyTheme();
        }

        /// <summary>
        /// Loads the saved theme from settings and applies it to the application.
        /// </summary>
        private void LoadAndApplyTheme()
        {
            try
            {
                string themeName = "default";
                string themeMode = "Dark";

                if (File.Exists(SETTINGS_FILE))
                {
                    string json = File.ReadAllText(SETTINGS_FILE);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    { 
                        if (doc.RootElement.TryGetProperty("ThemeName", out JsonElement nameEl))
                            themeName = nameEl.GetString() ?? "default";

                        if (doc.RootElement.TryGetProperty("ThemeMode", out JsonElement modeEl))
                            themeMode = modeEl.GetString() ?? "Dark";
                    }
                }

                ApplyTheme(themeName, themeMode);
            }
            catch (Exception ex)
            {
                // If error loading theme, continue with default
                System.Diagnostics.Debug.WriteLine($"Error loading theme: {ex.Message}");
            }
        }

        private void ApplyTheme(string themeName, string themeMode)
        {
            Uri themeUri;
            bool isCustom = themeName != "default";

            if (!isCustom)
            {
                themeUri = new Uri($"pack://application:,,,/Themes/{themeMode}Theme.xaml", UriKind.Absolute);
            }
            else
            {
                string? path = ResolveThemePath(themeName, themeMode);
                if (path != null)
                {
                    themeUri = new Uri(path, UriKind.Absolute);
                }
                else
                {
                    // Fallback to default mode if custom theme not found
                    themeUri = new Uri($"pack://application:,,,/Themes/{themeMode}Theme.xaml", UriKind.Absolute);
                    isCustom = false;
                }
            }

            // Safely remove only existing theme dictionaries to avoid clearing global styles/converters
            for (int i = this.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = this.Resources.MergedDictionaries[i];
                if (dict.Source != null)
                {
                    string source = dict.Source.ToString().ToLower();
                    if (source.Contains("theme.xaml") || source.Contains("color_themes"))
                    {
                        this.Resources.MergedDictionaries.RemoveAt(i);
                    }
                }
            }

            if (isCustom)
            {
                // Ensure base DarkTheme is present for fallback on custom themes
                this.Resources.MergedDictionaries.Add(new ResourceDictionary 
                { 
                    Source = new Uri("pack://application:,,,/Themes/DarkTheme.xaml", UriKind.Absolute) 
                });
            }

            this.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
        }

        private string? ResolveThemePath(string themeName, string themeMode)
        {
            string themesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "Color_Themes");
            if (!Directory.Exists(themesRoot)) return null;

            var themeFolders = Directory.GetDirectories(themesRoot);
            foreach (var folder in themeFolders)
            {
                var xamlFiles = Directory.GetFiles(folder, "*.xaml");
                foreach (var file in xamlFiles)
                {
                    // Lightweight check: Avoid creating a ResourceDictionary object which parses the whole XAML tree.
                    // This prevents OutOfMemory exceptions during theme discovery.
                    string content = File.ReadAllText(file);
                    if (content.Contains($"x:Key=\"ThemeName\">{themeName}<") && 
                        content.Contains($"x:Key=\"ThemeMode\">{themeMode}<"))
                    {
                        return file;
                    }
                }
            }
            return null;
        }
    }

}
