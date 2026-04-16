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
using System.Globalization;
using System.Windows.Data;
using Adoracion.Helpers;
using System.Windows.Media;
using System.Windows;

namespace Adoracion.Converters
{
    /// <summary>
    /// WPF IValueConverter for retrieving translated strings using TranslationHelper.
    /// </summary>
    public class TranslationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string key)
            {
                return TranslationHelper.GetString(key, key); // Fallback to key if translation not found
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean value to a SolidColorBrush.
    /// If true, returns a yellow brush (or a brush specified by parameter).
    /// If false, returns a default gray brush.
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFavorite)
            {
                if (isFavorite)
                {
                    // If favorite, highlight with Yellow
                    return new SolidColorBrush(Colors.Yellow);
                }
                else
                {
                    // If not favorite, use the resource specified in the parameter (e.g., 'TextMuted')
                    string resourceKey = parameter as string ?? "TextMuted";
                    return System.Windows.Application.Current.TryFindResource(resourceKey) as SolidColorBrush ?? new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray); // Default if value is not a boolean
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Takes a boolean and a parameter string containing two translation keys separated by '|'.
    /// Returns the translation for the first key if false, and the second key if true.
    /// </summary>
    public class BoolToTranslationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool condition && parameter is string keys)
            {
                string[] parts = keys.Split('|');
                if (parts.Length == 2)
                {
                    string selectedKey = condition ? parts[1] : parts[0];
                    return TranslationHelper.GetString(selectedKey, selectedKey);
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}