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
using System.Runtime.InteropServices;

namespace Adoracion.Helpers
{
    /// <summary>
    /// Implements natural alphanumeric sorting (e.g., "2" comes before "10")
    /// by wrapping the Windows shlwapi.dll StrCmpLogicalW function.
    /// </summary>
    public class NaturalStringComparer : IComparer<string>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string? psz1, string? psz2);

        public int Compare(string? x, string? y) => StrCmpLogicalW(x, y);
    }
}