namespace Shared
{
    public static class ToolTipPresentation
    {
        // TODO: Monitor Script Extender updates for the global SE_ToolTip style. Once it
        // provides automatic scaling, reference that style from all modsettings XAML files
        // and remove these local fixed presentation values (work item #97).

        // Noesis FontSize and MaxWidth are floats. Returning the exact CLR type is
        // required because x:Static values are not converted like XAML literals.
        public static float FontSize => 50.0f;

        public static float MaximumWidth => 1000.0f;
    }
}
