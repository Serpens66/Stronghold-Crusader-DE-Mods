using Noesis;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BuildingCosts
{
    public sealed class BuildingCostTooltipViewModel : INotifyPropertyChanged, INoesisElementBindingAware
    {
        private bool hasAdditionalCosts;
        private FrameworkElement hudRoot;
        private double tooltipMaxWidth = 960;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<BuildingCostTooltipEntry> TooltipItems { get; } = new ObservableCollection<BuildingCostTooltipEntry>();

        public Visibility ExtendedVisibility => hasAdditionalCosts ? Visibility.Visible : Visibility.Collapsed;
        public Visibility VanillaVisibility => hasAdditionalCosts ? Visibility.Collapsed : Visibility.Visible;

        public double TooltipMaxWidth
        {
            get => tooltipMaxWidth;
            private set
            {
                if (tooltipMaxWidth == value)
                    return;
                tooltipMaxWidth = value;
                OnPropertyChanged(nameof(TooltipMaxWidth));
            }
        }

        public void SetTooltip(IEnumerable<BuildingCostTooltipEntry> items, bool hasExtraCosts)
        {
            TooltipItems.Clear();
            foreach (BuildingCostTooltipEntry item in items)
                TooltipItems.Add(item);
            hasAdditionalCosts = hasExtraCosts;
            OnPropertyChanged(nameof(ExtendedVisibility));
            OnPropertyChanged(nameof(VanillaVisibility));
        }

        public void Clear()
        {
            TooltipItems.Clear();
            hasAdditionalCosts = false;
            OnPropertyChanged(nameof(ExtendedVisibility));
            OnPropertyChanged(nameof(VanillaVisibility));
        }

        void INoesisElementBindingAware.OnNoesisElementBound(FrameworkElement element)
        {
            FrameworkElement root = element;
            while (root.Parent is FrameworkElement parent)
                root = parent;

            if (!ReferenceEquals(root, hudRoot))
            {
                DetachHudRoot();
                hudRoot = root;
                hudRoot.SizeChanged += OnHudSizeChanged;
                hudRoot.Unloaded += OnHudUnloaded;
            }
            UpdateTooltipWidth();
        }

        private void OnHudSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTooltipWidth();
        }

        private void OnHudUnloaded(object sender, RoutedEventArgs e)
        {
            DetachHudRoot();
        }

        private void DetachHudRoot()
        {
            if (hudRoot == null)
                return;
            hudRoot.SizeChanged -= OnHudSizeChanged;
            hudRoot.Unloaded -= OnHudUnloaded;
            hudRoot = null;
        }

        private void UpdateTooltipWidth()
        {
            if (hudRoot != null && hudRoot.ActualWidth > 320)
                TooltipMaxWidth = Math.Max(320, hudRoot.ActualWidth - 320);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
