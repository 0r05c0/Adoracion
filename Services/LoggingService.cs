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

namespace Adoracion.Services
{
    /// <summary>
    /// Centralized logging service that handles debug output based on user settings.
    /// </summary>
    public sealed class LoggingService
    {
        private static readonly Lazy<LoggingService> _instance = new (() => new LoggingService());
        public static LoggingService Instance => _instance.Value;

        private readonly string _logPath;

        private LoggingService()
        {
            _logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Adoracion_Logging_Debug.log");
        }

        public void Log(string message)
        {
            try
            {
                var setting = SettingsRepository.GetSetting("EnableLogging");
                bool isEnabled = setting != null && setting.Value.Equals("true", StringComparison.OrdinalIgnoreCase);

                if (!isEnabled) return;

                System.IO.File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* Ignore logging failures */ }
        }
    }
}