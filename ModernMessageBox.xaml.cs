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
using Adoracion.Helpers;

namespace Adoracion
{
    public partial class ModernMessageBox : Window
    {
        public enum CustomResult { Ok, Yes, No, Cancel }
        private CustomResult _result = CustomResult.Cancel;

        public ModernMessageBox(string message, string title, MessageBoxButton buttons)
        {
            InitializeComponent();
            TxtMessage.Text = message;
            TxtTitle.Text = title;
            ConfigureButtons(buttons);
        }

        private void ConfigureButtons(MessageBoxButton buttons)
        {
            // Localize button text
            BtnOk.Content = TranslationHelper.GetString("Button_Ok", "OK");
            BtnYes.Content = TranslationHelper.GetString("Button_Yes", "Yes");
            BtnNo.Content = TranslationHelper.GetString("Button_No", "No");
            BtnCancel.Content = TranslationHelper.GetString("Button_Cancel", "Cancel");

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    BtnOk.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.OKCancel:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnOk.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNoCancel:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnOk.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        public static CustomResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, Window owner = null)
        {
            var msg = new ModernMessageBox(message, title, buttons);
            if (owner != null) msg.Owner = owner;
            else if (System.Windows.Application.Current.MainWindow != null) msg.Owner = System.Windows.Application.Current.MainWindow;
            
            msg.ShowDialog();
            return msg._result;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e) { _result = CustomResult.Ok; Close(); }
        private void BtnYes_Click(object sender, RoutedEventArgs e) { _result = CustomResult.Yes; Close(); }
        private void BtnNo_Click(object sender, RoutedEventArgs e) { _result = CustomResult.No; Close(); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { _result = CustomResult.Cancel; Close(); }
    }
}