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
using Microsoft.Data.Sqlite;

namespace Adoracion.Services
{
    public static class AppSettingsService
    {
        private static readonly string DbPath = FileService.Instance.CombinePath(AppDomain.CurrentDomain.BaseDirectory, "settings.db");
        private static readonly string ConnectionString = $"Data Source={DbPath}";

        public static event Action<string>? SettingChanged;

        private static readonly object CacheLock = new();
        private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);

        static AppSettingsService()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE IF NOT EXISTS AppSettings (SettingKey TEXT PRIMARY KEY, SettingValue TEXT)";
                command.ExecuteNonQuery();

                command.CommandText = "SELECT SettingKey, SettingValue FROM AppSettings";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string key = reader.GetString(0);
                        string value = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        Cache[key] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the in-memory value and notifies listeners without writing to the database.
        /// </summary>
        public static void SetCachedSetting(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!UpdateCache(key, value)) return;
            SettingChanged?.Invoke(key);
        }

        public static void SetSetting(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            bool changed = UpdateCache(key, value);
            PersistSetting(key, value);
            if (changed)
                SettingChanged?.Invoke(key);
        }

        public static string GetSetting(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            lock (CacheLock)
            {
                return Cache.TryGetValue(key, out var value) ? value : defaultValue;
            }
        }

        private static bool UpdateCache(string key, string? value)
        {
            string stored = value ?? string.Empty;
            lock (CacheLock)
            {
                if (Cache.TryGetValue(key, out var existing) && existing == stored)
                    return false;
                Cache[key] = stored;
                return true;
            }
        }

        private static void PersistSetting(string key, string? value)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO AppSettings (SettingKey, SettingValue) 
                    VALUES (@key, @value)
                    ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = @value";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value ?? (object)DBNull.Value);
                command.ExecuteNonQuery();
            }
        }
    }
}