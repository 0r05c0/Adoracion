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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Adoracion.Helpers;
using Adoracion.Models;
using Adoracion.Services;
namespace Adoracion
{
    public partial class SaveOpenPlaylistWindow  : Window
    {
        public enum WindowMode { Save, Open }
        private WindowMode _mode;
        private bool _isActuallyClosing = false;
        
        public List<MediaFile>? SelectedPlaylistItems { get; private set; }

        /// <summary>
        /// Constructor for Opening an existing playlist.
        /// </summary>
        public SaveOpenPlaylistWindow()
        {
            InitializeComponent();
            _mode = WindowMode.Open;
            SetupOpenMode();
            InitializeCommon();
        }

        /// <summary>
        /// Constructor for Saving a new playlist.
        /// </summary>
        public SaveOpenPlaylistWindow(IEnumerable<MediaFile> items)
        {
            InitializeComponent();
            _mode = WindowMode.Save;
            ItemsPreviewList.ItemsSource = items;
            SetupSaveMode();
            InitializeCommon();
        }

        private void InitializeCommon()
        {
            // Center the window relative to owner
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Subscribe to language changes for dynamic updates
            TranslationHelper.LanguageChanged += (s, e) => RefreshUIText();
        }

        private void SetupSaveMode()
        {
            InputLabel.Visibility = Visibility.Visible;
            PlaylistNameInput.Visibility = Visibility.Visible;
            PlaylistSelector.Visibility = Visibility.Collapsed;
            DeletePlaylistButton.Visibility = Visibility.Collapsed;
            
            RefreshUIText();
            Loaded += (s, e) => PlaylistNameInput.Focus();
            PlaylistNameInput.KeyDown += PlaylistNameInput_KeyDown;
            UpdateSaveButtonState();
        }

        private void SetupOpenMode()
        {
            InputLabel.Visibility = Visibility.Collapsed; // No prompt needed for dropdown selection
            PlaylistNameInput.Visibility = Visibility.Collapsed;
            PlaylistSelector.Visibility = Visibility.Visible;
            DeletePlaylistButton.Visibility = Visibility.Visible;

            RefreshUIText();
            PlaylistSelector.ItemsSource = PlaylistService.GetPlaylistNames();
            PlaylistSelector.GotFocus += (s, e) => PlaylistSelector.IsDropDownOpen = true;
            
            PlaylistSelector.Loaded += (s, e) =>
            {
                if (PlaylistSelector.Template.FindName("PART_EditableTextBox", PlaylistSelector) is System.Windows.Controls.TextBox textBox)
                {
                    textBox.TextChanged += (ts, te) =>
                    {
                        if (textBox.IsFocused)
                        {
                            PlaylistSelector.IsDropDownOpen = true;
                            var filter = textBox.Text;
                            UpdateSaveButtonState();
                            if (string.IsNullOrEmpty(filter))
                            {
                                PlaylistSelector.Items.Filter = null;
                            }
                            else
                            {
                                PlaylistSelector.Items.Filter = item =>
                                    item.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
                            }
                        }
                    };
                    textBox.KeyDown += (ts, te) => { if (te.Key == Key.Enter && SaveButton.IsEnabled) SaveButton_Click(ts, te); };
                }
            };

            UpdateSaveButtonState();
        }

        /// <summary>
        /// Updates all UI text based on current language and window mode.
        /// </summary>
        private void RefreshUIText()
        {
            bool isSave = _mode == WindowMode.Save;
            
            this.Title = TranslationHelper.GetString(isSave ? "Title_SavePlaylist" : "Title_OpenPlaylist", isSave ? "Save Playlist" : "Open Playlist");
            WindowTitleText.Text = this.Title;
            SaveButton.Content = TranslationHelper.GetString(isSave ? "Button_Save" : "Button_Open", isSave ? "Save" : "Open");
            
            // The following are handled by XAML bindings but can be manually refreshed if needed:
            // InputLabel.Text, ItemsPreviewHeader, etc.
        }

        private void RemovePreviewItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is MediaFile itemToRemove)
            {
                if (ItemsPreviewList.ItemsSource is IEnumerable<MediaFile> currentItems)
                {
                    var updatedList = currentItems.ToList();
                    updatedList.Remove(itemToRemove);
                    ItemsPreviewList.ItemsSource = updatedList;

                    UpdateSaveButtonState();

                    if (_mode == WindowMode.Open)
                    {
                        SelectedPlaylistItems = updatedList;

                        // Immediately persist changes to the database in Open mode
                        if (PlaylistSelector.SelectedItem is string playlistName)
                        {
                            try
                            {
                                if (updatedList.Any())
                                {
                                    PlaylistService.SavePlaylist(playlistName, updatedList);
                                }
                                else
                                {
                                    // Remove the playlist itself if it becomes empty
                                    PlaylistService.DeletePlaylist(playlistName);
                                    PlaylistSelector.ItemsSource = PlaylistService.GetPlaylistNames();
                                    SelectedPlaylistItems = null;
                                    UpdateSaveButtonState();
                                }
                            }
                            catch
                            {
                                string msg = TranslationHelper.GetString("Error_SavePlaylistFailed", "Failed to update playlist in database.");
                                string title = TranslationHelper.GetString("Error_Title", "Error");
                                ModernMessageBox.Show(msg, title, MessageBoxButton.OK, this);
                            }
                        }
                    }
                }
            }
        }

        private void ClearNameButton_Click(object sender, RoutedEventArgs e)
        {
            PlaylistNameInput.Text = string.Empty;
            PlaylistNameInput.Focus();
            UpdateSaveButtonState();
        }

        private void PlaylistNameInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSaveButtonState();

            if (_mode == WindowMode.Save)
            {
                string name = PlaylistNameInput.Text.Trim();
                bool exists = !string.IsNullOrEmpty(name) && PlaylistService.PlaylistExists(name);

                DuplicateNameErrorLabel.Visibility = exists ? Visibility.Visible : Visibility.Collapsed;

                if (exists)
                {
                    PlaylistNameInput.BorderBrush = (System.Windows.Media.Brush)FindResource("DeleteButtonRed");
                    PlaylistNameInput.BorderThickness = new Thickness(2);                    
                }
                else
                {
                    PlaylistNameInput.ClearValue(BorderBrushProperty);
                    PlaylistNameInput.ClearValue(BorderThicknessProperty);
                }
            }
        }

        private void PlaylistNameInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SaveButton.IsEnabled)
            {
                SaveButton_Click(sender, new RoutedEventArgs());
            }
        }

        private void PlaylistSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistSelector.SelectedItem is string name)
            {
                var items = PlaylistService.GetPlaylistItems(name);
                ItemsPreviewList.ItemsSource = items;
                SelectedPlaylistItems = items;
            }

            UpdateSaveButtonState();
        }

        private void UpdateSaveButtonState()
        {
            if (SaveButton == null || DeletePlaylistButton == null) return;

            if (_mode == WindowMode.Save)
            {
                string name = PlaylistNameInput.Text.Trim();
                bool hasName = !string.IsNullOrWhiteSpace(name);
                bool hasItems = ItemsPreviewList?.ItemsSource is IEnumerable<MediaFile> items && items.Any();
                bool exists = hasName && PlaylistService.PlaylistExists(name);

                SaveButton.IsEnabled = hasName && hasItems && !exists;
                DeletePlaylistButton.IsEnabled = false;
            }
            else
            {
                bool hasSelection = PlaylistSelector.SelectedItem != null;
                SaveButton.IsEnabled = hasSelection;
                DeletePlaylistButton.IsEnabled = hasSelection;
            }
        }

        private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistSelector.SelectedItem is string playlistName)
            {
                var result = ModernMessageBox.Show(
                    $"{TranslationHelper.GetString("Msg_ConfirmDeletePlaylist", "Are you sure you want to delete the playlist")} '{playlistName}'?",
                    TranslationHelper.GetString("Title_Confirm", "Confirm"),
                    MessageBoxButton.YesNo, this);

                if (result == ModernMessageBox.CustomResult.Yes || result == ModernMessageBox.CustomResult.Ok)
                {
                    try
                    {
                        PlaylistService.DeletePlaylist(playlistName);
                        PlaylistSelector.ItemsSource = PlaylistService.GetPlaylistNames();
                        SelectedPlaylistItems = null;
                        ItemsPreviewList.ItemsSource = null;
                        UpdateSaveButtonState();
                    }
                    catch
                    {
                        string msg = TranslationHelper.GetString("Error_DeletePlaylistFailed", "Failed to delete playlist.");
                        string title = TranslationHelper.GetString("Error_Title", "Error");
                        ModernMessageBox.Show(msg, title, MessageBoxButton.OK, this);
                    }
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == WindowMode.Save)
            {
                string fileName = PlaylistNameInput.Text.Trim();                
                
                try
                {
                    var items = (IEnumerable<MediaFile>)ItemsPreviewList.ItemsSource;
                    PlaylistService.SavePlaylist(fileName, items);
                }
                catch
                {
                    string msg = TranslationHelper.GetString("Error_SavePlaylistFailed", "Failed to save playlist to database.");
                    string title = TranslationHelper.GetString("Error_Title", "Error");
                    ModernMessageBox.Show(msg, title, MessageBoxButton.OK, this);
                    return;
                }
            }
            else if (_mode == WindowMode.Open)
            {
                // Respect the current state of the preview list (including manual removals)
                SelectedPlaylistItems = (ItemsPreviewList.ItemsSource as IEnumerable<MediaFile>)?.ToList();
            }

            DialogResult = true;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isActuallyClosing)
            {
                // Capture the intended DialogResult before e.Cancel = true clears it
                bool? result = this.DialogResult;

                e.Cancel = true;
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.1))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (s, ev) =>
                {
                    _isActuallyClosing = true;
                    
                    if (result.HasValue)
                        this.DialogResult = result.Value; // Re-setting DialogResult will close the window for real
                    else
                        this.Close();
                };
                this.BeginAnimation(OpacityProperty, fadeOut);
                return;
            }
            base.OnClosing(e);
        }
    }
}
