internal static class SerpLocalization
{
    internal static System.Collections.Generic.IDictionary<string, string> Overrides;
    internal static string Get(string key, params object[] replacements)
    {
        string result = Overrides != null && Overrides.TryGetValue(key, out string localized) ? localized : key;
        for (int index = 0; replacements != null && index + 1 < replacements.Length; index += 2)
            result += " " + replacements[index] + "=" + replacements[index + 1];
        return result;
    }
}
