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
using System.Windows.Media.Imaging;

namespace Adoracion.Helpers
{
    /// <summary>
    /// Decodes images at a target width and keeps a few frozen bitmaps for reuse.
    /// </summary>
    public static class BitmapLoader
    {
        private const int LowRamDecodePixelWidth = 1920;
        private static int MaxEntries => MemoryProfile.IsLowRam ? 2 : 4;
        private static readonly object Gate = new();
        private static readonly List<(string Key, BitmapImage Image)> Cache = new();

        public static BitmapImage Load(string path, int decodePixelWidth)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Image path is required.", nameof(path));
            if (decodePixelWidth <= 0)
                decodePixelWidth = LowRamDecodePixelWidth;
            if (MemoryProfile.IsLowRam && decodePixelWidth > LowRamDecodePixelWidth)
                decodePixelWidth = LowRamDecodePixelWidth;

            string key = path + "|" + decodePixelWidth;
            lock (Gate)
            {
                for (int i = 0; i < Cache.Count; i++)
                {
                    if (!string.Equals(Cache[i].Key, key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var hit = Cache[i];
                    Cache.RemoveAt(i);
                    Cache.Add(hit);
                    return hit.Image;
                }
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.IgnoreColorProfile;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.EndInit();
            bitmap.Freeze();

            lock (Gate)
            {
                Cache.Add((key, bitmap));
                while (Cache.Count > MaxEntries)
                    Cache.RemoveAt(0);
            }

            return bitmap;
        }
    }
}
