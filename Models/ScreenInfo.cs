/*
 Copyright (C) 2026 Matias Orosco

 This file is part of the Adoracion project.

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 See the LICENSE file distributed with this project for full terms.
*/

using System.Drawing;

namespace Adoracion.Models
{
    /// <summary>
    /// Represents information about a display screen.
    /// </summary>
    public class ScreenInfo
    {
        public string? DeviceName { get; set; }
        public Rectangle Bounds { get; set; }
        public Rectangle WorkingArea { get; set; }
        public string? DisplayName { get; set; }
        public bool Primary { get; set; }
    }
}