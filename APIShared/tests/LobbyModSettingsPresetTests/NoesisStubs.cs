namespace Noesis
{
    public enum Visibility
    {
        Visible,
        Hidden,
        Collapsed,
    }

    public sealed class ComboBoxItem
    {
        public object Content { get; set; }
        public Visibility Visibility { get; set; }
    }
}
