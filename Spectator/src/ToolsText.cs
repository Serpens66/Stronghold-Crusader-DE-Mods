namespace Spectator
{
    public sealed class ToolsText
    {
        public string this[string key] => SerpLocalization.Get("Tools." + key);
    }

}
