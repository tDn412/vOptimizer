using System;
using System.Windows;
using System.Windows.Controls;

namespace vOptimizer.Views
{
    public partial class BenchmarkPage : Page
    {
        public BenchmarkPage()
        {
            InitializeComponent();
            this.DataContext = Application.Current.MainWindow?.DataContext;
        }
    }
}
