
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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Octokit;

namespace Adoracion.Services
{
    public class UpdateCheckerService
    {
        private readonly IGitHubService _gitHubService;
        private readonly string _owner;
        private readonly string _repo;

        public UpdateCheckerService(string owner, string repo, string appName, IGitHubService? gitHubService = null)
        {
            _owner = owner;
            _repo = repo;
            _gitHubService = gitHubService ?? new OctokitGitHubService(appName);
        }

        /// <summary>
        /// Checks if a newer version is available on GitHub Releases.
        /// <paramref name="currentVersion"/> should be the version of the currently running application, e.g., Assembly.GetExecutingAssembly().GetName().Version;
        /// </summary>
        public async Task<bool> IsUpdateAvailableAsync(Version? currentVersion = null)
        {
            try
            {
                var latestRelease = await _gitHubService.GetLatestReleaseAsync(_owner, _repo);
                var latestVersion = new Version(latestRelease.TagName.TrimStart('v', 'V'));
                var localVersion = currentVersion ?? Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);

                return latestVersion > localVersion;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to check for updates: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Downloads the latest release and prepares the update script.
        /// </summary>
        public async Task DownloadAndInstallUpdateAsync(IProgress<double> progress)
        {
            var latestRelease = await _gitHubService.GetLatestReleaseAsync(_owner, _repo);
            
            // Find the asset (assuming it's a ZIP file)
            var asset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip")) 
                        ?? throw new Exception("No ZIP asset found in the latest release.");

            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            string zipPath = Path.Combine(tempFolder, asset.Name);
            string extractPath = Path.Combine(tempFolder, "extracted");

            // Download the asset
            await _gitHubService.DownloadAssetWithProgressAsync(asset.BrowserDownloadUrl, zipPath, progress);

            // Extract
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractPath));

            // Perform the replacement
            ApplyUpdate(extractPath);
        }

        private void ApplyUpdate(string newFilesPath)
        {
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string currentProcessPath = Process.GetCurrentProcess().MainModule.FileName;
            string batchScriptPath = Path.Combine(Path.GetTempPath(), "update_script.bat");

            // This batch script waits for the app to close, copies files, deletes itself, and restarts the app.
            // %1 = PID of current app, %2 = Source Path, %3 = Destination Path, %4 = App Executable Path
            string batchContent = $@"
                                    @echo off
                                    :wait
                                    tasklist /FI ""PID eq {Environment.ProcessId}"" 2>NUL | find /I /N ""{Environment.ProcessId}"" >NUL
                                    if ""%ERRORLEVEL%""==""0"" (
                                        timeout /t 1 /nobreak >nul
                                        goto wait
                                    )
                                    xcopy /y /s /e ""{newFilesPath}\*"" ""{currentDirectory}""
                                    start """" ""{currentProcessPath}""
                                    del ""%~f0""
                                    ";

            File.WriteAllText(batchScriptPath, batchContent);

            // Execute the batch script
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchScriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(startInfo);

            // Close the application immediately
            Environment.Exit(0);
        }
    }
}
