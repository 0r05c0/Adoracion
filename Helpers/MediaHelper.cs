/*
 Copyright (C) 2026 Matias Orosco

 This file is part of the Adoracion project.

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 See the LICENSE file distributed with this project for full terms.
*/
using System.IO;
using System.Linq;
using Adoracion.Models;

namespace Adoracion.Helpers
{
    /// <summary>
    /// Provides helper methods related to media files.
    /// </summary>
    public static class MediaHelper
    {
        public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        public static readonly string[] AudioExtensions = { ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".wma", ".aac" };
        public static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mkv", ".wmv", ".webm", ".flv", ".mov" };

        public static readonly string[] AllAllowedExtensions = ImageExtensions.Concat(AudioExtensions).Concat(VideoExtensions).ToArray();
        public static readonly string[] MediaWithDurationExtensions = AudioExtensions.Concat(VideoExtensions).ToArray();

        /// <summary>
        /// Determines the media type (Image, Audio, Video, Unknown) based on the file extension.
        /// </summary>
        public static MediaType DetermineMediaType(string? path)
        {
            if (string.IsNullOrEmpty(path)) return MediaType.Unknown;
            string ext = Path.GetExtension(path).ToLower();

            if (ImageExtensions.Contains(ext)) return MediaType.Image;
            if (AudioExtensions.Contains(ext)) return MediaType.Audio;
            if (VideoExtensions.Contains(ext)) return MediaType.Video;

            return MediaType.Unknown;
        }
    }
}