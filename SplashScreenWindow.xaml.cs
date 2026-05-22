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
using System.Windows.Threading;

namespace Adoracion
{
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string message)
        {
            StatusText.Text = message;

            // Force the UI thread to process the message queue. 
            // This allows the ProgressBar animation to advance and the StatusText to refresh 
            // even if the main thread is busy with synchronous initialization logic.
            Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
    }
}