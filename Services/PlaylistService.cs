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
using Adoracion.Helpers;
using Adoracion.Models;

namespace Adoracion.Services
{
    public static class PlaylistService
    {
        private static readonly string DbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playlists.db");
        private static readonly string ConnectionString = $"Data Source={DbPath}";

        static PlaylistService()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                var pragmaCmd = connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
                pragmaCmd.ExecuteNonQuery();

                // Check if the 'Playlists' table exists and if it has the 'Id' column
                bool playlistsTableExists = false;
                bool playlistsTableHasIdColumn = false;
                try
                {
                    using (var checkTableCmd = connection.CreateCommand())
                    {
                        checkTableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Playlists';";
                        playlistsTableExists = checkTableCmd.ExecuteScalar() != null;
                    }

                    if (playlistsTableExists)
                    {
                        using (var checkColumnCmd = connection.CreateCommand())
                        {
                            checkColumnCmd.CommandText = "PRAGMA table_info(Playlists);";
                            using (var reader = checkColumnCmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    if (reader.GetString(1).Equals("Id", StringComparison.OrdinalIgnoreCase)) // column name is at index 1
                                    {
                                        playlistsTableHasIdColumn = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Log($"Error checking database schema: {ex.Message}");
                }

                bool needsMigration = playlistsTableExists && !playlistsTableHasIdColumn;

                if (needsMigration)
                {
                    LoggingService.Instance.Log("Old database schema detected. Initiating migration.");
                    PerformDatabaseMigration(connection);
                }

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Playlists (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT UNIQUE NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS PlaylistItems (
                        PlaylistId INTEGER,
                        FileName TEXT,
                        FilePath TEXT,
                        Duration TEXT,
                        SortOrder INTEGER,
                        FOREIGN KEY(PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE
                    );";
                command.ExecuteNonQuery();
            }
        }

        private static void PerformDatabaseMigration(SqliteConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Step 1: Rename old Playlists table
                    var renamePlaylistsCmd = connection.CreateCommand();
                    renamePlaylistsCmd.Transaction = transaction;
                    renamePlaylistsCmd.CommandText = "ALTER TABLE Playlists RENAME TO OldPlaylists;";
                    renamePlaylistsCmd.ExecuteNonQuery();
                    LoggingService.Instance.Log("Renamed 'Playlists' to 'OldPlaylists'.");

                    // Step 2: Create new Playlists table
                    var createNewPlaylistsCmd = connection.CreateCommand();
                    createNewPlaylistsCmd.Transaction = transaction;
                    createNewPlaylistsCmd.CommandText = "CREATE TABLE Playlists (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT UNIQUE NOT NULL);";
                    createNewPlaylistsCmd.ExecuteNonQuery();
                    LoggingService.Instance.Log("Created new 'Playlists' table.");

                    // Step 3: Copy data from OldPlaylists to new Playlists
                    var copyPlaylistsCmd = connection.CreateCommand();
                    copyPlaylistsCmd.Transaction = transaction;
                    copyPlaylistsCmd.CommandText = "INSERT INTO Playlists (Name) SELECT Name FROM OldPlaylists;";
                    copyPlaylistsCmd.ExecuteNonQuery();
                    LoggingService.Instance.Log("Copied data to new 'Playlists' table.");

                    // Step 4: Rename old PlaylistItems table
                    var renamePlaylistItemsCmd = connection.CreateCommand();
                    renamePlaylistItemsCmd.Transaction = transaction;
                    renamePlaylistItemsCmd.CommandText = "ALTER TABLE PlaylistItems RENAME TO OldPlaylistItems;";
                    renamePlaylistItemsCmd.ExecuteNonQuery();
                    LoggingService.Instance.Log("Renamed 'PlaylistItems' to 'OldPlaylistItems'.");

                    // Step 5: Create new PlaylistItems table
                    var createNewPlaylistItemsCmd = connection.CreateCommand();
                    createNewPlaylistItemsCmd.Transaction = transaction;
                    createNewPlaylistItemsCmd.CommandText = "CREATE TABLE PlaylistItems (PlaylistId INTEGER, FileName TEXT, FilePath TEXT, Duration TEXT, SortOrder INTEGER, FOREIGN KEY(PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE);";
                    createNewPlaylistItemsCmd.ExecuteNonQuery();
                    LoggingService.Instance.Log("Created new 'PlaylistItems' table.");

                    // Step 6: Copy data from OldPlaylistItems to new PlaylistItems, mapping names to IDs
                    var copyPlaylistItemsCmd = connection.CreateCommand();
                    copyPlaylistItemsCmd.Transaction = transaction;
                    copyPlaylistItemsCmd.CommandText = @"
                        INSERT INTO PlaylistItems (PlaylistId, FileName, FilePath, Duration, SortOrder)
                        SELECT P.Id, OPI.FileName, OPI.FilePath, OPI.Duration, OPI.SortOrder
                        FROM OldPlaylistItems OPI
                        JOIN Playlists P ON OPI.PlaylistName = P.Name;";
                    copyPlaylistItemsCmd.ExecuteNonQuery();
                    LoggingService.Instance.Log("Copied data to new 'PlaylistItems' table with PlaylistId mapping.");

                    // Step 7: Drop old tables
                    var dropOldPlaylistsCmd = connection.CreateCommand();
                    dropOldPlaylistsCmd.Transaction = transaction;
                    dropOldPlaylistsCmd.CommandText = "DROP TABLE OldPlaylists;";
                    dropOldPlaylistsCmd.ExecuteNonQuery();

                    var dropOldPlaylistItemsCmd = connection.CreateCommand();
                    dropOldPlaylistItemsCmd.Transaction = transaction;
                    dropOldPlaylistItemsCmd.CommandText = "DROP TABLE OldPlaylistItems;";
                    dropOldPlaylistItemsCmd.ExecuteNonQuery();
                    LoggingService.Instance.Log("Dropped old tables.");

                    transaction.Commit();
                    LoggingService.Instance.Log("Database migration completed successfully.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    LoggingService.Instance.Log($"CRITICAL ERROR during database migration: {ex.Message}");
                    throw; // Re-throw to indicate a serious issue
                }
            }
        }

        public static void SavePlaylist(string playlistName, IEnumerable<MediaFile> items)
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    // Ensure playlist exists and get its ID
                    var getPlaylistIdCmd = connection.CreateCommand();
                    getPlaylistIdCmd.Transaction = transaction;
                    getPlaylistIdCmd.CommandText = @"
                        INSERT OR IGNORE INTO Playlists (Name) VALUES (@name);
                        SELECT Id FROM Playlists WHERE Name = @name;";
                    getPlaylistIdCmd.Parameters.AddWithValue("@name", playlistName);
                    long playlistId = (long)getPlaylistIdCmd.ExecuteScalar();

                    // Remove existing items for this ID
                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.Transaction = transaction;
                    deleteCmd.CommandText = "DELETE FROM PlaylistItems WHERE PlaylistId = @pid;";
                    deleteCmd.Parameters.AddWithValue("@pid", playlistId);
                    deleteCmd.ExecuteNonQuery();

                    int order = 0;
                    foreach (var item in items)
                    {
                        var insertCmd = connection.CreateCommand();
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText = @"
                            INSERT INTO PlaylistItems (PlaylistId, FileName, FilePath, Duration, SortOrder) 
                            VALUES (@pid, @fname, @path, @dur, @order)";
                        insertCmd.Parameters.AddWithValue("@pid", playlistId);
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
                command.CommandText = @"
                    SELECT FileName, FilePath, Duration 
                    FROM PlaylistItems 
                    JOIN Playlists ON Playlists.Id = PlaylistItems.PlaylistId 
                    WHERE Playlists.Name = @name 
                    ORDER BY SortOrder";
                command.Parameters.AddWithValue("@name", playlistName);
                using (var reader = command.ExecuteReader())
                {
                    int i = 1;
                    while (reader.Read())
                    {
                        string name = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                        string path = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        string duration = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        MediaType type = MediaHelper.DetermineMediaType(path);

                        items.Add(new MediaFile {
                            Name = name,
                            FilePath = path,
                            Duration = duration,
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
                command.CommandText = "DELETE FROM Playlists WHERE Name = @name;";
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