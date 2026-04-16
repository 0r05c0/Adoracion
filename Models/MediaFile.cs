/*
 Copyright (C) 2026 Matias Orosco

 This file is part of the Adoracion project.

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 See the LICENSE file distributed with this project for full terms.
*/

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Adoracion.Models
{
    public enum MediaType
    {
        Unknown,
        Image,
        Audio,
        Video
    }

    /// <summary>
    /// Represents a media file entry in the playlist or library.
    /// </summary>
    public class MediaFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public string? FilePath { get; set; }
        public string? Name { get; set; }
        public string? Artist { get; set; } // Added for playlist display
        public string? Duration { get; set; } // Added for playlist display

        private bool _isMissing;
        public bool IsMissing
        {
            get => _isMissing;
            set
            {
                _isMissing = value;
                OnPropertyChanged();
            }
        }

        private int _index;
        public string? FileExtension { get; set; }
        public int Index
        {
            get => _index;
            set
            {
                _index = value;
                OnPropertyChanged();
            }
        } // Added for playlist display

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                OnPropertyChanged();
            }
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }

        private MediaType _type;
        public MediaType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                }
            }
        }       
    }
}
