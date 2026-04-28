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
        private static readonly string DbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.db");
        private static readonly string ConnectionString = $"Data Source={DbPath}";

        public static event Action<string>? SettingChanged;

        static AppSettingsService()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE IF NOT EXISTS AppSettings (SettingKey TEXT PRIMARY KEY, SettingValue TEXT)";
                command.ExecuteNonQuery();
            }
        }

        public static void SetSetting(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
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
            SettingChanged?.Invoke(key);
        }

        public static string GetSetting(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT SettingValue FROM AppSettings WHERE SettingKey = @key";
                command.Parameters.AddWithValue("@key", key);
                var result = command.ExecuteScalar();
                return result != null && result != DBNull.Value ? result.ToString() : defaultValue;
            }
        }
    }
}