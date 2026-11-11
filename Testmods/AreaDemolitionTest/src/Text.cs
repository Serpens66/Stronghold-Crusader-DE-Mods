using System;
using System.Collections.Generic;
using System.IO;

namespace AreaDemolitionTest
{
    internal static class Text
    {
        private static readonly Dictionary<string,string> values=new Dictionary<string,string>(StringComparer.Ordinal);
        private static string language;
        internal static int Revision {get;private set;}
        internal static string Get(string key)
        {
            string current="en-US";
            try
            {
                current=SHCDESE.API.GameAssetManagerAPI.Instance.CurrentLanguage??"en-US";
                string chosen=SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LanguageProvider.Value;
                if(!string.IsNullOrWhiteSpace(chosen) && !chosen.Equals("detect",StringComparison.OrdinalIgnoreCase))current=chosen;
            }
            catch { }
            if(current.Equals("german",StringComparison.OrdinalIgnoreCase))current="de-DE";
            if(current.Equals("english",StringComparison.OrdinalIgnoreCase))current="en-US";
            if(!string.Equals(language,current,StringComparison.Ordinal))
            {
                language=current;values.Clear();
                var root=Path.Combine(Path.GetDirectoryName(typeof(Text).Assembly.Location),"Locales");
                Read(Path.Combine(root,"en-US.txt"));
                if(current.IndexOfAny(new[]{'/','\\',':'})<0)Read(Path.Combine(root,current+".txt"));
                Revision++;
            }
            return values.TryGetValue(key,out string value)?value:key;
        }
        private static void Read(string path)
        {
            if(!File.Exists(path))return;
            foreach(string row in File.ReadAllLines(path))
            {int equal=row.IndexOf('=');if(equal>0 && !row.StartsWith("#"))values[row.Substring(0,equal)]=row.Substring(equal+1);}
        }
        // Translate only on Unity. Native/network callbacks retain diagnostic text without reading locale files.
        internal static string NetworkStatus(string status)
        {
            if(string.IsNullOrEmpty(status))return "";
            if(status=="Demolition completed.")return Get("StatusCompleted");
            if(status=="Unsent demolition cancelled.")return Get("StatusCancelled");
            if(status.StartsWith("Submitted batch",StringComparison.Ordinal))return Get("StatusCancelPending");
            if(status.IndexOf("native failure",StringComparison.Ordinal)>=0)return Get("StatusNativeFailure");
            if(status.IndexOf("different results",StringComparison.Ordinal)>=0 || status.StartsWith("Peers disagree",StringComparison.Ordinal))return Get("StatusMismatch");
            if(status.StartsWith("Roster changed",StringComparison.Ordinal) || status.StartsWith("A peer loaded",StringComparison.Ordinal))return Get("StatusRosterChanged");
            if(status.StartsWith("Waiting",StringComparison.Ordinal))return Get("StatusWaiting");
            return Get("StatusUnavailable");
        }
    }
}
