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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Adoracion.Helpers;
using Adoracion.Models;

namespace Adoracion.Services
{
    public static class PlaylistService
    {
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playlists.db");
        private static readonly string ConnectionString = $"Data Source={DbPath}";

        static PlaylistService()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var pragmaCmd = connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
                pragmaCmd.ExecuteNonQuery();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Playlists (
                        Name TEXT PRIMARY KEY
                    );
                    CREATE TABLE IF NOT EXISTS PlaylistItems (
                        PlaylistName TEXT,
                        FileName TEXT,
                        FilePath TEXT,
                        Duration TEXT,
                        SortOrder INTEGER,
                        FOREIGN KEY(PlaylistName) REFERENCES Playlists(Name) ON DELETE CASCADE
                    );";
                command.ExecuteNonQuery();
            }
        }

        public static void SavePlaylist(string playlistName, IEnumerable<MediaFile> items)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    // Remove existing and insert playlist name
                    var setupCmd = connection.CreateCommand();
                    setupCmd.Transaction = transaction;
                    setupCmd.CommandText = "DELETE FROM PlaylistItems WHERE PlaylistName = @name; INSERT OR IGNORE INTO Playlists (Name) VALUES (@name);";
                    setupCmd.Parameters.AddWithValue("@name", playlistName);
                    setupCmd.ExecuteNonQuery();

                    int order = 0;
                    foreach (var item in items)
                    {
                        var insertCmd = connection.CreateCommand();
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText = "INSERT INTO PlaylistItems (PlaylistName, FileName, FilePath, Duration, SortOrder) VALUES (@pname, @fname, @path, @dur, @order)";
                        insertCmd.Parameters.AddWithValue("@pname", playlistName);
                        insertCmd.Parameters.AddWithValue("@fname", item.Name ?? "");
                        insertCmd.Parameters.AddWithValue("@path", item.FilePath ?? "");
                        insertCmd.Parameters.AddWithValue("@dur", item.Duration ?? "");
                        insertCmd.Parameters.AddWithValue("@order", order++);
                        insertCmd.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
        }

        public static List<string> GetPlaylistNames()
        {
            var list = new List<string>();
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Name FROM Playlists ORDER BY Name";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) list.Add(reader.GetString(0));
                }
            }
            return list;
        }

        public static List<MediaFile> GetPlaylistItems(string playlistName)
        {
            var items = new List<MediaFile>();
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT FileName, FilePath, Duration FROM PlaylistItems WHERE PlaylistName = @name ORDER BY SortOrder";
                command.Parameters.AddWithValue("@name", playlistName);
                using (var reader = command.ExecuteReader())
                {
                    int i = 1;
                    while (reader.Read())
                    {
                        string path = reader.GetString(1);
                        MediaType type = MediaHelper.DetermineMediaType(path);

                        items.Add(new MediaFile {
                            Name = reader.GetString(0),
                            FilePath = path,
                            Duration = reader.GetString(2),
                            Index = i++,
                            IsFavorite = FavoritesService.IsFavorite(path),
                            Type = type
                        });
                    }
                }
            }
            return items;
        }

        public static void DeletePlaylist(string name)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM PlaylistItems WHERE PlaylistName = @name; DELETE FROM Playlists WHERE Name = @name;";
                command.Parameters.AddWithValue("@name", name);
                command.ExecuteNonQuery();
            }
        }

        public static bool PlaylistExists(string name)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(1) FROM Playlists WHERE Name = @name";
                command.Parameters.AddWithValue("@name", name);
                var result = command.ExecuteScalar();
                return result != null && Convert.ToInt32(result) > 0;
            }
        }
    }
}