using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PriceChecker
{
    public interface IAppStatusService : INotifyPropertyChanged
    {
        string ApiStatus { get; set; }
    }

    public class AppStatusService : IAppStatusService
    {
        private string _apiStatus = "API: INITIALIZING";

        public string ApiStatus
        {
            get => _apiStatus;
            set
            {
                if (_apiStatus != value)
                {
                    _apiStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
