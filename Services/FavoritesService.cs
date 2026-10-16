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
    public static class FavoritesService
    {
        private static readonly string DbPath = FileService.Instance.CombinePath(AppDomain.CurrentDomain.BaseDirectory, "favorites.db");
        private static readonly string ConnectionString = $"Data Source={DbPath}";
        private static readonly object CacheLock = new();
        private static HashSet<string>? _favorites;

        static FavoritesService()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE IF NOT EXISTS Favorites (FilePath TEXT PRIMARY KEY)";
                command.ExecuteNonQuery();
            }
        }

        public static void AddFavorite(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            lock (CacheLock)
            {
                if (!Favorites().Add(filePath)) return;
            }

            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "INSERT OR IGNORE INTO Favorites (FilePath) VALUES (@path)";
                command.Parameters.AddWithValue("@path", filePath);
                command.ExecuteNonQuery();
            }
        }

        public static void RemoveFavorite(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            lock (CacheLock)
            {
                if (!Favorites().Remove(filePath)) return;
            }

            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM Favorites WHERE FilePath = @path";
                command.Parameters.AddWithValue("@path", filePath);
                command.ExecuteNonQuery();
            }
        }

        public static bool IsFavorite(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            lock (CacheLock)
                return Favorites().Contains(filePath);
        }

        public static List<string> GetFavorites()
        {
            lock (CacheLock)
                return Favorites().ToList();
        }

        private static HashSet<string> Favorites()
        {
            if (_favorites != null) return _favorites;

            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT FilePath FROM Favorites";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        loaded.Add(reader.GetString(0));
                    }
                }
            }

            _favorites = loaded;
            return _favorites;
        }
    }
}