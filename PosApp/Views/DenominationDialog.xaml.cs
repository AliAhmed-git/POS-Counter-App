using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PosApp.Desktop.Views
{
    public partial class DenominationDialog : Window
    {
        public ObservableCollection<DenominationItem> Items { get; }
        public decimal TotalCash => Items.Sum(i => i.Total);
        public string Notes => NotesBox.Text;

        public DenominationDialog()
        {
            InitializeComponent();
            Items = new ObservableCollection<DenominationItem>
            {
                new DenominationItem { Value = 5000 },
                new DenominationItem { Value = 1000 },
                new DenominationItem { Value = 500 },
                new DenominationItem { Value = 100 },
                new DenominationItem { Value = 50 },
                new DenominationItem { Value = 20 },
                new DenominationItem { Value = 10 },
                new DenominationItem { Value = 5 },
                new DenominationItem { Value = 2 },
                new DenominationItem { Value = 1 }
            };

            foreach (var item in Items)
            {
                // Logic preserved for calculation, but UI update removed for honesty
            }

            DenomList.ItemsSource = Items;
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb) tb.SelectAll();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    public class DenominationItem : INotifyPropertyChanged
    {
        private int _count;
        public decimal Value { get; set; }
        public int Count
        {
            get => _count;
            set
            {
                if (_count != value)
                {
                    _count = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Total));
                }
            }
        }
        public decimal Total => Value * Count;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
