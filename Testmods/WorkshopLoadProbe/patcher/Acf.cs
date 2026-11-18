using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WorkshopLoadProbe
{
    // T3: logs every field Steam keeps for this app's Workshop items, without interpreting
    // them. Which fields mean "subscribed", "installed" and "update pending" is the question.
    internal static class Acf
    {
        internal static void Dump(string path)
        {
            try
            {
                if (!File.Exists(path)) { ProbeLog.Warning("ACF", "missing " + path); return; }
                var root = Parse(File.ReadAllText(path));
                ProbeLog.Info("ACF", path + " lastWriteUtc=" + File.GetLastWriteTimeUtc(path).ToString("o"));
                if (!(root.Values.FirstOrDefault() is Dictionary<string, object> app)) { ProbeLog.Warning("ACF", "no root block"); return; }
                ProbeLog.Info("ACF_APP", Scalars(app));
                foreach (var block in app.Where(entry => entry.Value is Dictionary<string, object>))
                    foreach (var item in (Dictionary<string, object>)block.Value)
                        ProbeLog.Info("ACF_ITEM", block.Key + " " + item.Key + " " +
                            (item.Value is Dictionary<string, object> fields ? Scalars(fields) : "value=" + item.Value));
            }
            catch (Exception ex) { ProbeLog.Warning("ACF", "unreadable " + ex.Message); }
        }

        private static string Scalars(Dictionary<string, object> block) =>
            string.Join(" ", block.Where(entry => entry.Value is string).Select(entry => entry.Key + "=" + entry.Value).ToArray());

        // Minimal Valve KeyValues reader: quoted keys/values and braces.
        internal static Dictionary<string, object> Parse(string text)
        {
            int index = 0;
            return Block(text, ref index, false);
        }

        private static Dictionary<string, object> Block(string text, ref int index, bool nested)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                Skip(text, ref index);
                if (index >= text.Length) { if (nested) throw new InvalidDataException("unterminated block"); return result; }
                if (text[index] == '}') { if (!nested) throw new InvalidDataException("unexpected }"); index++; return result; }
                string key = Quoted(text, ref index);
                Skip(text, ref index);
                if (index < text.Length && text[index] == '{') { index++; result[key] = Block(text, ref index, true); }
                else result[key] = Quoted(text, ref index);
            }
        }

        private static void Skip(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        }

        private static string Quoted(string text, ref int index)
        {
            if (index >= text.Length || text[index] != '"') throw new InvalidDataException("expected quote at " + index);
            var value = new StringBuilder();
            for (index++; index < text.Length; index++)
            {
                char c = text[index];
                if (c == '\\' && index + 1 < text.Length) { value.Append(text[++index]); continue; }
                if (c == '"') { index++; return value.ToString(); }
                value.Append(c);
            }
            throw new InvalidDataException("unterminated string");
        }
    }
}
