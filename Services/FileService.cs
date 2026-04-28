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
using System.IO;
using Adoracion.Helpers; // For MediaHelper.AllAllowedExtensions

namespace Adoracion.Services
{
    public sealed class FileService
    {
        private static readonly Lazy<FileService> _instance = new(() => new FileService());
        public static FileService Instance => _instance.Value;

        private FileService() { }

        public bool FileExists(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            return File.Exists(filePath);
        }

        public bool DirectoryExists(string? directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return false;
            return Directory.Exists(directoryPath);
        }

        public string GetFileName(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;
            return Path.GetFileName(filePath);
        }

        public string GetFileNameWithoutExtension(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;
            return Path.GetFileNameWithoutExtension(filePath);
        }

        public string GetFileExtension(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;
            return Path.GetExtension(filePath);
        }

        public void OpenInExplorer(string? filePath)
        {
            LoggingService.Instance.Log($"OpenInExplorer: Request received for path: '{filePath ?? "null"}'");

            if (string.IsNullOrEmpty(filePath))
            {
                LoggingService.Instance.Log("OpenInExplorer: Path is null or empty. Operation cancelled.");
                return;
            }

            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                };

                if (DirectoryExists(filePath))
                {
                    LoggingService.Instance.Log($"OpenInExplorer: Path identified as directory: {filePath}");
                    startInfo.Arguments = $"\"{filePath}\"";
                }
                else if (FileExists(filePath))
                {
                    LoggingService.Instance.Log($"OpenInExplorer: Path identified as file: {filePath}");
                    startInfo.Arguments = $"/select,\"{filePath}\"";
                }
                else 
                {
                    LoggingService.Instance.Log($"OpenInExplorer: Operation failed. Path does not exist on disk: {filePath}");
                    return;
                }

                LoggingService.Instance.Log($"OpenInExplorer: Executing explorer.exe with args: {startInfo.Arguments}");
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error opening explorer: {ex.Message}");
            }
        }

        public string CombinePath(params string[] paths)
        {
            if (paths == null || paths.Length == 0) return string.Empty;
            return Path.Combine(paths);
        }

        public async Task<List<string>> GetHymnFilesAsync()
        {
            return await Task.Run(() =>
            {
                string hymnsPath = CombinePath(AppDomain.CurrentDomain.BaseDirectory, "Hymns");
                if (!DirectoryExists(hymnsPath))
                {
                    CreateDirectory(hymnsPath);
                    return new List<string>();
                }

                var files = Directory.GetFiles(hymnsPath)
                    .Where(f => MediaHelper.AllAllowedExtensions.Contains(GetFileExtension(f).ToLower()));

                return files.ToList();
            });
        }

        public List<string> GetMediaFilesFromDirectory(string path, bool recursive = false)
        {
            if (!DirectoryExists(path)) return new List<string>();

            try
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = recursive,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                };

                return Directory.GetFiles(path, "*", options)
                    .Where(f => MediaHelper.AllAllowedExtensions.Contains(GetFileExtension(f).ToLower()))
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error getting media files from directory {path}: {ex.Message}");
                return new List<string>();
            }
        }

        public int GetFileCountInDirectory(string path)
        {
            if (!DirectoryExists(path)) return 0;
            try
            {
                return Directory.GetFiles(path).Length;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error getting file count in directory {path}: {ex.Message}");
                return 0;
            }
        }

        public string ReadAllText(string filePath)
        {
            if (!FileExists(filePath)) return string.Empty;
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error reading file {filePath}: {ex.Message}");
                return string.Empty;
            }
        }

        public byte[] ReadAllBytes(string filePath)
        {
            if (!FileExists(filePath)) return Array.Empty<byte>();
            try
            {
                return File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error reading bytes from file {filePath}: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        public void WriteAllText(string filePath, string content)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error writing to file {filePath}: {ex.Message}");
            }
        }

        public void CreateDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"Error creating directory {path}: {ex.Message}");
            }
        }
    }
}