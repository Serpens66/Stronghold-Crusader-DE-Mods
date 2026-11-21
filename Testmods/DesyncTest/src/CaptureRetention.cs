using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DesyncTest
{
    internal static class CaptureRetention
    {
        private const long ClosedBudget=512L*1024*1024;
        private static readonly Regex SessionName=new Regex(
            @"\A(?<stamp>\d{8}-\d{6}-\d{3})-p(?<pid>\d+)-s(?<session>\d+)\z",RegexOptions.CultureInvariant);
        private static readonly HashSet<string> Names=new HashSet<string>(StringComparer.Ordinal)
        {
            "events.bin","snapshots.bin","checkpoints.bin","checkpoints.previous.bin","markers.bin","evidence.bin","manifest.json","manifest.json.tmp","summary.txt"
        };

        internal static void Prune(string root,DateTime now,Action<string> report)
        {
            if(!Directory.Exists(root)||IsLink(root))return;
            var packages=new List<Package>();
            foreach(string path in Directory.EnumerateDirectories(root))
            {
                try
                {
                    Package candidate=Inspect(path,now);
                    if(candidate!=null)packages.Add(candidate);
                }
                catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is FormatException)
                { report("DESYNC_RETENTION_SKIPPED "+Path.GetFileName(path)+": "+ex.Message); }
            }
            long bytes=0;
            int incidents=0;
            foreach(var item in packages.OrderByDescending(p=>p.Incident).ThenByDescending(p=>p.Stamp))
            {
                if(item.Incident&&++incidents<=6&&bytes+item.Size<=ClosedBudget){bytes+=item.Size;continue;}
                TryRemove(item.Path,now,false,item.Incident,item.Size,report);
            }
        }

        internal static void DiscardClosedControl(string path,DateTime now,Action<string> report) =>
            TryRemove(path,now,true,false,null,report);

        private static void TryRemove(string path,DateTime now,bool justClosed,bool incident,long? size,Action<string> report)
        {
            try
            {
                if(IsLink(Path.GetDirectoryName(path)))return;
                // Unknown entries and links are never traversed. Recheck immediately before deletion.
                var current=Inspect(path,now,justClosed);
                if(current==null||current.Incident!=incident||(size.HasValue&&current.Size!=size.Value))return;
                foreach(string file in current.Files.Where(f=>Path.GetFileName(f)!="manifest.json"))File.Delete(file);
                File.Delete(Path.Combine(path,"manifest.json"));
                Directory.Delete(path,false);
                report("DESYNC_RETENTION_REMOVED "+Path.GetFileName(path));
            }
            catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is FormatException)
            { report("DESYNC_RETENTION_SKIPPED "+Path.GetFileName(path)+": "+ex.Message); }
        }

        private static bool IsLink(string path) => (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;

        private static Package Inspect(string path,DateTime now,bool justClosed=false)
        {
            var match=SessionName.Match(Path.GetFileName(path));
            if(!match.Success||IsLink(path)||!DateTime.TryParseExact(match.Groups["stamp"].Value,"yyyyMMdd-HHmmss-fff",
                CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime stamp))return null;
            string[] files=Directory.GetFileSystemEntries(path);
            if(files.Any(f=>!Names.Contains(Path.GetFileName(f))||
                (File.GetAttributes(f)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0))return null;
            string manifest=Path.Combine(path,"manifest.json");
            var info=new FileInfo(manifest);
            if(!info.Exists||info.Length>30000)return null;
            var data=Shared.DependencyFreeJson.Parse(File.ReadAllText(manifest)) as Dictionary<string,object>;
            if(data==null||!data.TryGetValue("plugin",out var plugin)||!Equals(plugin,"DesyncTest_Serp")||
                !data.TryGetValue("retentionSchema",out var schema)||Convert.ToString(schema,CultureInfo.InvariantCulture)!="1"||
                !data.TryGetValue("session",out var session)||Convert.ToString(session,CultureInfo.InvariantCulture)!=match.Groups["session"].Value||
                !data.TryGetValue("incidents",out var incidents)||!int.TryParse(Convert.ToString(incidents,CultureInfo.InvariantCulture),out int count)||count<0||
                !data.TryGetValue("ended",out var ended)||!(ended is bool closed))return null;
            // The grace interval also lets the writer close all streams after publishing its manifest.
            if(justClosed?!closed:now-info.LastWriteTimeUtc<TimeSpan.FromMinutes(10))return null;
            if(!closed)
            {
                if(now-info.LastWriteTimeUtc<TimeSpan.FromDays(1)||!int.TryParse(match.Groups["pid"].Value,out int pid))return null;
                try { using(Process live=Process.GetProcessById(pid)) { if(!live.HasExited)return null; } }
                catch(ArgumentException) { } // The recording process no longer exists.
            }
            return new Package{Path=path,Stamp=stamp,Size=files.Sum(f=>new FileInfo(f).Length),Files=files,Incident=count>0};
        }

        private sealed class Package
        {
            internal string Path;
            internal DateTime Stamp;
            internal long Size;
            internal string[] Files;
            internal bool Incident;
        }
    }
}
