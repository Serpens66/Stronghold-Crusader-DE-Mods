using System;
using System.ComponentModel;
using Noesis;
using SHCDESE.NoesisUtil;

namespace Spectator
{
    public sealed class StatisticsControlsViewModel : INotifyPropertyChanged
    {
        private readonly Func<bool> active;
        internal StatisticsControlsViewModel(Func<bool> active, Action refresh)
        { this.active = active; RefreshStatisticsCommand = new RelayCommand(refresh); }
        public event PropertyChangedEventHandler PropertyChanged;
        public RelayCommand RefreshStatisticsCommand { get; }
        public Visibility StatisticsPreviewControlsVisibility => active() ? Visibility.Visible : Visibility.Collapsed;
        public string RefreshStatisticsButtonText => SerpLocalization.Get("Tools.RefreshStatistics");
        public string RefreshStatisticsButtonHelpText => SerpLocalization.Get("Tools.RefreshStatisticsHelp");
        internal void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatisticsPreviewControlsVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RefreshStatisticsButtonText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RefreshStatisticsButtonHelpText)));
        }
    }
}
