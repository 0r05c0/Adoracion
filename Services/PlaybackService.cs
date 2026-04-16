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
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace Adoracion.Services
{
    public sealed class PlaybackService : IDisposable
    {
        private static readonly Lazy<PlaybackService> _instance = new (() => new PlaybackService());
        public static PlaybackService Instance => _instance.Value;

        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private readonly SemaphoreSlim _playSemaphore = new(1, 1);

        public MediaPlayer? Player => _mediaPlayer;
        public LibVLC? LibVLC => _libVLC;
        public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;

        public event EventHandler? PlaybackStateChanged;

        private PlaybackService()
        {
            try
            {
                Core.Initialize();
                LoggingService.Instance.Log("LibVLC Core initialized successfully.");
                _libVLC = new LibVLC(
                    "--no-osd",
                    "--vout=direct3d11",
                    "--avcodec-hw=any",
                    "--avcodec-fast",
                    "--no-video-title-show",
                    "--video-title-timeout=0",
                    "--quiet"
                );
                _mediaPlayer = new MediaPlayer(_libVLC);
                AttachEvents();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Log($"CRITICAL: LibVLC initialization failed: {ex.Message}");
            }
        }

        private void AttachEvents()
        {
            if (_mediaPlayer == null) return;
            _mediaPlayer.Playing += OnStateChanged;
            _mediaPlayer.Paused += OnStateChanged;
            _mediaPlayer.Stopped += OnStateChanged;
            _mediaPlayer.EndReached += OnStateChanged;
            _mediaPlayer.EncounteredError += OnStateChanged;
        }

        private void DetachEvents()
        {
            if (_mediaPlayer == null) return;
            _mediaPlayer.Playing -= OnStateChanged;
            _mediaPlayer.Paused -= OnStateChanged;
            _mediaPlayer.Stopped -= OnStateChanged;
            _mediaPlayer.EndReached -= OnStateChanged;
            _mediaPlayer.EncounteredError -= OnStateChanged;
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            LoggingService.Instance.Log($"LibVLC State Change Detected: {_mediaPlayer?.State}");
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task PlayAsync(string filePath, CancellationToken token = default)
        {
            if (_mediaPlayer == null || _libVLC == null) return;

            // Use a semaphore to ensure only one play operation happens at a time.
            // Allow cancellation if the task is still waiting in the queue.
            await _playSemaphore.WaitAsync(token);
            try
            {
                if (token.IsCancellationRequested) return;

                LoggingService.Instance.Log($"PlaybackService.Play: Current media MRL before new play: {_mediaPlayer.Media?.Mrl}");

                // 1. Capture the old media reference. Assigning new media automatically
                // handles the stop transition internally in the native engine.
                var oldMedia = _mediaPlayer.Media;

                // 2. Create the new Media object (I/O operation)
                var media = await Task.Run(() => new Media(_libVLC, filePath, FromType.FromPath));
                
                // 3. Swap and Play on a background thread to prevent UI frosting
                await Task.Run(() => 
                {
                    _mediaPlayer.Media = media;
                    _mediaPlayer.Play();
                });

                // 4. Cleanup. Dispose the C# wrappers; native LibVLC handles its own pointers.
                media.Dispose();
                if (oldMedia != null)
                {
                    // Offload disposal to prevent native cleanup from stalling the next track
                    _ = Task.Run(() => oldMedia.Dispose());
                }
                
                LoggingService.Instance.Log($"PlaybackService: Successfully started {filePath}");
            }
            catch (OperationCanceledException) { }
            finally
            {
                _playSemaphore.Release();
            }
        }

        public void TogglePlayPause()
        {
            if (_mediaPlayer == null) return;

            if (_mediaPlayer.State == VLCState.Playing)
            {
                _mediaPlayer.Pause();
            }
            else if (_mediaPlayer.State == VLCState.Paused)
            {
                _mediaPlayer.Play();
            }
        }

        public void Stop()
        {
            _mediaPlayer?.Stop();
        }

        public void SetVolume(int volume)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = volume;
            }
        }

        public long GetTime() => _mediaPlayer?.Time ?? 0;
        public void SetTime(long time)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Time = time;
            }
        }

        public long GetDuration() => _mediaPlayer?.Media?.Duration ?? 0;

        public void Dispose()
        {
            DetachEvents();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _mediaPlayer = null;
            _libVLC = null;
        }
    }
}