using Noesis;
using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    // The extender updates ToolTip.FontSize on opening. Bind to that value rather
    // than replacing its resolution-dependent preset with a fixed font size.
    public sealed class ArrangementTooltipFontConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            System.Convert.ToSingle(value, CultureInfo.InvariantCulture) * 2f;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
