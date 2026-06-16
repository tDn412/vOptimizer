using System;
using System.Windows;
using System.Windows.Controls;

namespace vOptimizer.Views
{
    public partial class HardwarePage : Page
    {
        public HardwarePage()
        {
            InitializeComponent();
            this.DataContext = Application.Current.MainWindow?.DataContext;
        }
    }
}
