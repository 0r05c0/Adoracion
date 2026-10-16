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

namespace Adoracion.Helpers
{
    /// <summary>
    /// Detects machines with about 4 GB of RAM so playback and image caches can stay small.
    /// The check is under 5 GB so a 4 GB PC is included and an 8 GB PC is not.
    /// </summary>
    public static class MemoryProfile
    {
        private const long LowRamThresholdBytes = 5L * 1024 * 1024 * 1024;

        public static bool IsLowRam { get; } = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes < LowRamThresholdBytes;
    }
}
