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
using System.Net.Http;
using Octokit;

namespace Adoracion.Services
{

    /// <summary>
    /// Interface to abstract GitHub operations for testability.
    /// </summary>
    public interface IGitHubService
    {
        Task<Release> GetLatestReleaseAsync(string owner, string repo);
        Task DownloadAssetWithProgressAsync(string url, string destinationPath, IProgress<double> progress);
    }

    /// <summary>
    /// Production implementation using Octokit and HttpClient.
    /// </summary>
    public class OctokitGitHubService : IGitHubService
    {
        private readonly IGitHubClient _githubClient;
        private readonly HttpClient _httpClient;

        public OctokitGitHubService(string appName)
        {
            _githubClient = new GitHubClient(new ProductHeaderValue(appName));
            _httpClient = new HttpClient();
        }

        public async Task<Release> GetLatestReleaseAsync(string owner, string repo)
        {
            return await _githubClient.Repository.Release.GetLatest(owner, repo);
        }

        public async Task DownloadAssetWithProgressAsync(string url, string destinationPath, IProgress<double> progress)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalReadBytes = 0L;
            int readBytes;

            while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, readBytes));
                totalReadBytes += readBytes;

                if (totalBytes.HasValue)
                {
                    progress.Report((double)totalReadBytes / totalBytes.Value * 100);
                }
            }
        }
    }
}