/*
 Copyright (C) 2026 Matias Orosco

 This file is part of the Adoracion project.

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 See the LICENSE file distributed with this project for full terms.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.ComponentModel;
using System.Windows.Threading;

namespace Adoracion.Services
{
    /// <summary>
    /// Service for managing application localization and language switching.
    /// Supports multiple languages and persists user language preference.
    /// </summary>
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService _instance;
        private static readonly object _lockObject = new object();

        private Dictionary<string, Dictionary<string, string>> _translations;
        private string _currentLanguage = "en";
        private string _translationsPath = "resources/Languages.json";
        private List<string> _availableLanguages = new List<string>();
        private Dictionary<string, string> _languageDisplayNames = new Dictionary<string, string>();

        public event EventHandler<LanguageChangedEventArgs> LanguageChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Indexer to allow XAML binding: {Binding [KeyName], Source={x:Static services:LocalizationService.Instance}}
        /// </summary>
        public string this[string key]
        {
            get => GetString(key, key);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private LocalizationService()
        {
            _translations = new Dictionary<string, Dictionary<string, string>>();
        }

        /// <summary>
        /// Gets the singleton instance of LocalizationService.
        /// </summary>
        public static LocalizationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new LocalizationService();
                        }
                    }
                }
                return _instance;
            }
        }

        public string CurrentLanguage => _currentLanguage;
        public IReadOnlyList<string> AvailableLanguages => _availableLanguages.AsReadOnly();
        public IReadOnlyDictionary<string, string> LanguageDisplayNames => _languageDisplayNames;

        /// <summary>
        /// Initializes the localization service by loading translations and user preference.
        /// </summary>
        /// <param name="translationsFilePath">The absolute path to the Languages.json file.</param>
        public void Initialize(string translationsFilePath)
        {
            LoggingService.Instance.Log($"Initialize called. Target Path: {translationsFilePath}");
            _translationsPath = translationsFilePath;
            
            LoadTranslations();
            LoadUserPreferredLanguage();
        }

        /// <summary>
        /// Loads translations from the JSON resource file.
        /// </summary>
        private void LoadTranslations()
        {
            try
            {
                string absolutePath = Path.GetFullPath(_translationsPath);
                LoggingService.Instance.Log($"Attempting to load translations from: {absolutePath}");

                if (!File.Exists(_translationsPath))
                {
                    LoggingService.Instance.Log("FILE NOT FOUND. Falling back to hardcoded defaults.");
                    CreateDefaultTranslations(saveToFile: true);
                    return;
                }

                // Read as bytes to automatically handle BOM (Byte Order Mark) issues
                byte[] jsonBytes = File.ReadAllBytes(_translationsPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var rawTranslations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonBytes, options);

                if (rawTranslations != null)
                {
                    LoggingService.Instance.Log($"JSON parsed successfully. Found {rawTranslations.Count} language keys.");
                    _translations = rawTranslations;
                    _availableLanguages.Clear();
                    _languageDisplayNames.Clear();

                    foreach (var langCode in _translations.Keys)
                    {
                        _availableLanguages.Add(langCode);
                        if (_translations[langCode].TryGetValue("__displayName", out var displayName))
                        {
                            _languageDisplayNames[langCode] = displayName;
                        }
                    }
                    LoggingService.Instance.Log($"Loaded languages: {string.Join(", ", _availableLanguages)}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"CRITICAL ERROR during LoadTranslations: {ex.Message}");
                CreateDefaultTranslations(saveToFile: false);
            }
        }

        /// <summary>
        /// Creates default translations if the file doesn't exist.
        /// </summary>
        private void CreateDefaultTranslations(bool saveToFile)
        {
            _translations = new Dictionary<string, Dictionary<string, string>>
            {
                { "en", new Dictionary<string, string>
                    {
                        { "__displayName", "English" },
                        { "Title_Main", "Multi-Screen Media Player" },
                        { "Label_Now_Playing", "Now Playing:" },
                        { "Label_None_Selected", "None selected" },
                        { "Label_Media_Folder", "Media folder" },
                        { "Label_Playlist", "Playlist" },
                        { "Label_Filter_Search", "Filter/search" },
                        { "Label_Duration", "Duration" },
                        { "Button_Clear_Playlist", "Clear All" },
                        { "Label_Actions", "Actions" },
                        { "Label_Media_Name", "media name" },
                        { "Label_Progress", "Progress:" },
                        { "Label_Volume", "Volume:" },
                        { "Label_Secondary_Display", "Secondary Display (if available):" },
                        { "Label_Language", "Language:" },
                        { "Button_Play", "? Play" },
                        { "Button_Close", "X" },
                        { "Title_Select_Video", "Select a Video File" },
                        { "Filter_Video_Files", "Video Files (*.mp4;*.avi;*.mkv;*.wmv;*.flv)|*.mp4;*.avi;*.mkv;*.wmv;*.flv|All Files (*.*)|*.*" },
                        { "Label_Add_From_Hymns", "Add from Hymns folder:" },
                        { "Label_Current_Playlist", "Current Playlist:" },
                        { "Time_Format", "--:--" },
                        { "Label_AppName", "Adoracion" },
                        { "Label_Psalm_Breath", "Let everything that has breath praise the Lord. Praise the Lord!" },
                        { "Title_MediaScreen", "Multi window player" },
                        { "Title_Settings", "Settings" },
                        { "Label_Settings_Title", "SETTINGS" },
                        { "Nav_MediaFolders", "Media Folders" },
                        { "Nav_Language", "Language" },
                        { "Nav_ScreenSelection", "Screen Selection" },
                        { "Nav_Appearance", "Appearance" },
                        { "Nav_TextWallpaper", "Text & Wallpaper" },
                        { "Nav_About", "About" },
                        { "Section_MediaFolders_Title", "Media Folders" },
                        { "Section_MediaFolders_Description", "Manage where Adoracion looks for your content." },
                        { "Section_TextWallpaper_Title", "Text & Wallpaper" },
                        { "Section_TextWallpaper_Description", "Customize the overlay text and background image." },
                        { "Label_OverlayPosition", "Overlay Position" },
                        { "Label_TextColor", "Text Color" },
                        { "Checkbox_EnableTextShadow", "Enable Text Shadow" },
                        { "Label_ShadowColor", "Shadow Color" },
                        { "Label_ShadowBlur", "Shadow Blur" },
                        { "Label_ShadowDepth", "Shadow Depth" },
                        { "Label_ShadowOpacity", "Shadow Opacity" },
                        { "Label_TextAlpha", "Text Opacity" },
                        { "Button_AddFolder", "Add Folder" },
                        { "Section_Language_Title", "Language" },
                        { "Section_Language_Description", "Choose your preferred language." },
                        { "Section_ScreenSelection_Title", "Screen Selection" },
                        { "Section_ScreenSelection_Description", "Choose which display to use for media playback." },
                        { "Section_Appearance_Title", "Theme Appearance" },
                        { "Section_Appearance_Description", "Customize how Adoracion looks." }, 
                        { "Label_LightMode", "Light Mode" },
                        { "Label_DarkMode", "Dark Mode" },
                        { "Tab_Local", "Local" },
                        { "Tab_Favorites", "Favorites" },
                        { "Placeholder_SearchLibrary", "Search library..." },
                        { "Label_OverlayText", "Overlay Welcome Text" },
                        { "Settings_Appearance_OverlayTextDesc", "Enter the text to display on the media screen when idle." },
                        { "Button_Save", "Save" },
                        { "Label_BackgroundImage", "Background Image" },
                        { "Settings_Appearance_BackgroundImageDesc", "Select an image to display as background when idle." },
                        { "Checkbox_EnableBackgroundImage", "Enable Background Image" },
                        { "Button_BrowseImage", "Browse Image" },
                        { "Label_PlaylistEmpty", "Playlist is Empty" },
                        { "Label_LibraryEmpty", "No media files found in this folder." },
                        { "Label_FavoritesEmpty", "Your favorites list is empty." },
                        { "Splash_InitUI", "Initializing UI..." },
                        { "Splash_Visualizer", "Preparing Visualizer..." },
                        { "Splash_Hymns", "Loading Hymn Files..." }
                    }
                }
            };

            if (saveToFile)
            {
                SaveTranslations();
            }
        }

        /// <summary>
        /// Saves translations to the JSON resource file.
        /// </summary>
        private void SaveTranslations()
        {
            try
            {
                string directory = Path.GetDirectoryName(_translationsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_translations, options);
                File.WriteAllText(_translationsPath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving translations: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a translated string for the given key in the current language.
        /// </summary>
        /// <param name="key">The translation key.</param>
        /// <param name="defaultValue">The default value if translation is not found.</param>
        /// <returns>The translated string or default value.</returns>
        public string GetString(string key, string defaultValue = "")
        {
            // 1. Try current language
            if (_translations.TryGetValue(_currentLanguage, out var currentDict) && 
                currentDict.TryGetValue(key, out var translation))
            {
                return translation;
            }

            // 2. Fallback to English if current is not English
            if (_currentLanguage != "en" && _translations.TryGetValue("en", out var enDict) && 
                enDict.TryGetValue(key, out var enTranslation))
            {
                return enTranslation;
            }

            return defaultValue;
        }

        /// <summary>
        /// Changes the current language and persists the choice.
        /// </summary>
        /// <param name="languageCode">The language code (e.g., "en", "es").</param>
        public void SetLanguage(string languageCode)
        {
            if (!_availableLanguages.Contains(languageCode))
                return;

            _currentLanguage = languageCode;
            SaveUserPreferredLanguage(languageCode);

            // Notify WPF that all bindings to this object (including the indexer) need to refresh
            OnPropertyChanged(null); 

            LanguageChanged?.Invoke(this, new LanguageChangedEventArgs { LanguageCode = languageCode });
        }

        /// <summary>
        /// Loads the user's preferred language from the database.
        /// </summary>
        private void LoadUserPreferredLanguage()
        {
            try
            {
                var settings = SettingsRepository.GetSetting("Language");
                if (settings != null && !string.IsNullOrEmpty(settings.Value))
                {
                    if (_availableLanguages.Contains(settings.Value))
                    {
                        _currentLanguage = settings.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading language preference: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the user's preferred language to the database.
        /// </summary>
        private void SaveUserPreferredLanguage(string languageCode)
        {
            try
            {
                SettingsRepository.SaveSetting("Language", languageCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving language preference: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Event arguments for language change events.
    /// </summary>
    public class LanguageChangedEventArgs : EventArgs
    {
        public string LanguageCode { get; set; }
    }
}
