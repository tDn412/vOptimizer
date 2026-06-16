using System;
using System.Windows;
using System.Windows.Controls;

namespace vOptimizer.Views
{
    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            this.DataContext = Application.Current.MainWindow?.DataContext;
        }
    }
}
