internal static class SerpLocalization
{
    internal static string Get(string key, params object[] replacements)
    {
        string result = key;
        for (int index = 0; replacements != null && index + 1 < replacements.Length; index += 2)
            result += " " + replacements[index] + "=" + replacements[index + 1];
        return result;
    }
}
