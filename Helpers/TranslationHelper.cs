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
using System.Windows;
using Adoracion.Services;

namespace Adoracion.Helpers
{
    /// <summary>
    /// Helper class for accessing translations in XAML and code-behind.
    /// Provides a static way to get localized strings throughout the application.
    /// </summary>
    public class TranslationHelper
    {
        private static LocalizationService _localizationService;

        /// <summary>
        /// Initializes the localization system.
        /// </summary>
        /// <param name="translationsFilePath">Optional absolute path to the Languages.json file.</param>
        public static void Initialize(string? translationsFilePath = null)
        {
            lock (typeof(TranslationHelper))
            {
                _localizationService = LocalizationService.Instance;
                
                if (string.IsNullOrEmpty(translationsFilePath))
                {
                    translationsFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "Languages.json");
                }
                
                _localizationService.Initialize(translationsFilePath);
            }
        }

        /// <summary>
        /// Gets a translated string from the localization service.
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
            if (_localizationService == null)
                Initialize();

            return _localizationService.GetString(key, defaultValue);
        }

        /// <summary>
        /// Changes the current language.
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            if (_localizationService == null)
                Initialize();

            _localizationService.SetLanguage(languageCode);
        }

        /// <summary>
        /// Gets the currently active language.
        /// </summary>
        public static string GetCurrentLanguage()
        {
            if (_localizationService == null)
                Initialize();

            return _localizationService.CurrentLanguage;
        }

        /// <summary>
        /// Subscribes to language change events.
        /// </summary>
        public static event EventHandler<LanguageChangedEventArgs> LanguageChanged
        {
            add
            {
                if (_localizationService == null)
                    Initialize();
                _localizationService.LanguageChanged += value;
            }
            remove
            {
                if (_localizationService != null)
                    _localizationService.LanguageChanged -= value;
            }
        }
    }
}
