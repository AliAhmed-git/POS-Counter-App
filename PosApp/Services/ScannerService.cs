using System;
using System.IO.Ports;
using System.Text;
using PosApp.Desktop.Services;

namespace PosApp.Desktop.Services
{
    public interface IScannerService : IDisposable
    {
        event Action<string> BarcodeScanned;
        void Start();
        void Stop();
        bool IsRunning { get; }
    }

    public class ScannerService : IScannerService
    {
        private readonly ISettingsService _settingsService;
        private SerialPort? _serialPort;
        private StringBuilder _buffer = new StringBuilder();

        public event Action<string>? BarcodeScanned;

        public bool IsRunning => _serialPort != null && _serialPort.IsOpen;

        public ScannerService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void Start()
        {
            if (_settingsService.Settings.ScannerMode != "Serial")
            {
                Stop();
                return;
            }

            if (IsRunning) return;

            try
            {
                _serialPort = new SerialPort(_settingsService.Settings.ScannerComPort)
                {
                    BaudRate = _settingsService.Settings.ScannerBaudRate,
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();
                System.Diagnostics.Debug.WriteLine($"Scanner started on {_settingsService.Settings.ScannerComPort}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start scanner: {ex.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            try
            {
                if (_serialPort != null)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
                System.Diagnostics.Debug.WriteLine("Scanner stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping scanner: {ex.Message}");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                string data = _serialPort.ReadExisting();
                foreach (char c in data)
                {
                    if (c == '\r' || c == '\n')
                    {
                        if (_buffer.Length > 0)
                        {
                            string barcode = _buffer.ToString();
                            _buffer.Clear();
                            BarcodeScanned?.Invoke(barcode);
                        }
                    }
                    else
                    {
                        _buffer.Append(c);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading scanner data: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
