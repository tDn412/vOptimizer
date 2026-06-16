using System;
using System.Windows;
using System.Windows.Controls;

namespace vOptimizer.Views
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            this.DataContext = Application.Current.MainWindow?.DataContext;
        }
    }
}
