using APIShared.ModSettings;
using Noesis;

namespace Shared
{
    internal static class MenuToolTip
    {
        internal const int Duration = 60000;
        private static Style style;

        // Called on the Unity UI thread. Each owner gets a popup with the explicit Modsettings style.
        internal static ToolTip Create(string text)
        {
            if (style == null)
            {
                style = (Style)GUI.ParseXaml(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ToolTip}'>
                    <Setter Property='Background' Value='#FF1D1710'/>
                    <Setter Property='Foreground' Value='White'/>
                    <Setter Property='BorderBrush' Value='#FFF2D48A'/>
                    <Setter Property='Template'>
                        <Setter.Value>
                            <ControlTemplate TargetType='{x:Type ToolTip}'>
                                <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' MaxWidth='{TemplateBinding MaxWidth}' CornerRadius='2'>
                                    <TextBlock Text='{TemplateBinding Content}' Foreground='{TemplateBinding Foreground}' FontSize='{TemplateBinding FontSize}' TextWrapping='Wrap'/>
                                </Border>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>");
            }

            var tip = new ToolTip
            {
                Style = style,
                Content = text,
            };
            SharedToolTipSizing.SetEnabled(tip, true);
            return tip;
        }
    }
}
