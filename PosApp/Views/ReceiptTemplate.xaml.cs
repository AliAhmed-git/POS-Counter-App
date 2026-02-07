using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using QRCoder;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace PosApp.Desktop.Views
{
    public partial class ReceiptTemplate : UserControl
    {
        public ReceiptTemplate()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty AddressProperty = DependencyProperty.Register("Address", typeof(string), typeof(ReceiptTemplate), new PropertyMetadata(string.Empty));
        public string Address { get => (string)GetValue(AddressProperty); set => SetValue(AddressProperty, value); }

        public static readonly DependencyProperty PhoneProperty = DependencyProperty.Register("Phone", typeof(string), typeof(ReceiptTemplate), new PropertyMetadata(string.Empty));
        public string Phone { get => (string)GetValue(PhoneProperty); set => SetValue(PhoneProperty, value); }
        
        public static readonly DependencyProperty TimeProperty = DependencyProperty.Register("Time", typeof(string), typeof(ReceiptTemplate), new PropertyMetadata(string.Empty));
        public string Time { get => (string)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

        public static readonly DependencyProperty ShopNameProperty = DependencyProperty.Register("ShopName", typeof(string), typeof(ReceiptTemplate), new PropertyMetadata(string.Empty));
        public string ShopName { get => (string)GetValue(ShopNameProperty); set => SetValue(ShopNameProperty, value); }

        public static readonly DependencyProperty FbrNtnProperty = DependencyProperty.Register("FbrNtn", typeof(string), typeof(ReceiptTemplate), new PropertyMetadata(string.Empty));
        public string FbrNtn { get => (string)GetValue(FbrNtnProperty); set => SetValue(FbrNtnProperty, value); }

        public static readonly DependencyProperty FbrStrProperty = DependencyProperty.Register("FbrStr", typeof(string), typeof(ReceiptTemplate), new PropertyMetadata(string.Empty));
        public string FbrStr { get => (string)GetValue(FbrStrProperty); set => SetValue(FbrStrProperty, value); }

        public static readonly DependencyProperty FbrPosIdProperty = DependencyProperty.Register("FbrPosId", typeof(string), typeof(ReceiptTemplate), new PropertyMetadata(string.Empty));
        public string FbrPosId { get => (string)GetValue(FbrPosIdProperty); set => SetValue(FbrPosIdProperty, value); }

        public static readonly DependencyProperty FbrInvoiceNoProperty = DependencyProperty.Register("FbrInvoiceNo", typeof(string), typeof(ReceiptTemplate), new PropertyMetadata(string.Empty));
        public string FbrInvoiceNo { get => (string)GetValue(FbrInvoiceNoProperty); set => SetValue(FbrInvoiceNoProperty, value); }

        public static readonly DependencyProperty IsRefundProperty = DependencyProperty.Register("IsRefund", typeof(bool), typeof(ReceiptTemplate), new PropertyMetadata(false));
        public bool IsRefund { get => (bool)GetValue(IsRefundProperty); set => SetValue(IsRefundProperty, value); }

        private BitmapSource? _qrCodeImage;
        public BitmapSource? QrCodeImage
        {
            get
            {
                if (_qrCodeImage == null)
                    GenerateQrCode();
                return _qrCodeImage;
            }
        }

        private void GenerateQrCode()
        {
            try
            {
                string qrData = $"FBR-POS-ID:{FbrPosId}|INV:{FbrInvoiceNo}|NTN:{FbrNtn}|STR:{FbrStr}|GST:{GetTaxAmount()}";
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q))
                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
                    using (MemoryStream ms = new MemoryStream(qrCodeAsPngByteArr))
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        _qrCodeImage = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating QR code: {ex.Message}");
            }
        }

        private string GetTaxAmount()
        {
            if (DataContext is Models.SalesHead sale)
            {
                return sale.TotalTax.ToString("F2");
            }
            return "0.00";
        }
    }

    public class ItemIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var item = value as FrameworkElement;
            if (item == null) return "";

            var itemsControl = ItemsControl.ItemsControlFromItemContainer(item) as ItemsControl;
            if (itemsControl == null) return "";

            int index = itemsControl.ItemContainerGenerator.IndexFromContainer(item);
            return (index + 1).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DiscountVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d > 0) return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
