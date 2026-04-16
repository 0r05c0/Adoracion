/*
 Copyright (C) 2026 Matias Orosco

 This file is part of the Adoracion project.

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 See the LICENSE file distributed with this project for full terms.
*/
using System.Windows;

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
        }
    }
}