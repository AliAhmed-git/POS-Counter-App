using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace PriceChecker
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Resolve ViewModel from DI
            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<PriceCheckerViewModel>();
            }
        }
    }
}
