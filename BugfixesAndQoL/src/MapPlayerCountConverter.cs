// Feature: Present the maximum playable map slots in the editor map list.
using CrusaderDE;
using Noesis;
using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    public sealed class MapPlayerCountConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            FileHeader header = (value as FileRow)?.fileHeader;
            return VanillaMapEditorPolicy.FormatPlayerCount(header?.maxPlayers ?? 0);
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
