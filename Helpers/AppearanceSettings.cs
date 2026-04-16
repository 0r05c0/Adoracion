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
using Adoracion.Services;

namespace Adoracion.Helpers
{
    public static class AppearanceSettings
    {
        // Keys used in the SQLite database
        public const string OverlayPositionKey = "OverlayPosition";
        public const string OverlayTextKey = "OverlayText";
        public const string FontSizeKey = "OverlayFontSize";
        public const string TextAlphaKey = "OverlayTextAlpha";
        public const string TextColorKey = "OverlayTextColor";
        public const string EnableShadowKey = "EnableTextShadow";
        public const string ShadowColorKey = "OverlayShadowColor";
        public const string ShadowBlurKey = "OverlayShadowBlur";
        public const string ShadowDepthKey = "OverlayShadowDepth";
        public const string ShadowOpacityKey = "OverlayShadowOpacity";
        public const string EnableBackgroundImageKey = "EnableBackgroundImage";
        public const string BackgroundImagePathKey = "BackgroundImagePath";
        public const string EnableTextBorderKey = "EnableTextBorder";

        // Default Values
        public static string DefaultOverlayText => TranslationHelper.GetString("Label_Psalm_Breath", "Let everything that has breath praise the Lord. Praise the Lord!");
        public static string DefaultBackgroundImagePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "default_background.jpg");

        // Centralized Getters
        public static string GetOverlayPosition() => AppSettingsService.GetSetting(OverlayPositionKey, "TopCenter");
        public static string GetOverlayText() => AppSettingsService.GetSetting(OverlayTextKey, DefaultOverlayText);
        public static double GetFontSize() => double.Parse(AppSettingsService.GetSetting(FontSizeKey, "60"));
        public static double GetTextAlpha() => double.Parse(AppSettingsService.GetSetting(TextAlphaKey, "100"));
        public static string GetTextColor() => AppSettingsService.GetSetting(TextColorKey, "#FFFFFF");
        public static bool GetEnableShadow() => bool.Parse(AppSettingsService.GetSetting(EnableShadowKey, "True"));
        public static string GetShadowColor() => AppSettingsService.GetSetting(ShadowColorKey, "#000000");
        public static double GetShadowBlur() => double.Parse(AppSettingsService.GetSetting(ShadowBlurKey, "20"));
        public static double GetShadowDepth() => double.Parse(AppSettingsService.GetSetting(ShadowDepthKey, "0"));
        public static double GetShadowOpacity() => double.Parse(AppSettingsService.GetSetting(ShadowOpacityKey, "80"));
        public static bool GetEnableBackgroundImage() => bool.Parse(AppSettingsService.GetSetting(EnableBackgroundImageKey, "True"));
        public static string GetBackgroundImagePath() => AppSettingsService.GetSetting(BackgroundImagePathKey, DefaultBackgroundImagePath);
        
        public static bool IsAppearanceKey(string key)
        {
            return key == OverlayTextKey || key == OverlayPositionKey || key == FontSizeKey || 
                   key == EnableBackgroundImageKey || key == BackgroundImagePathKey || key == TextColorKey || 
                   key == TextAlphaKey || key == EnableShadowKey || key == ShadowColorKey || 
                   key == ShadowBlurKey || key == ShadowDepthKey || key == ShadowOpacityKey || key == EnableTextBorderKey;
        }
    }
}