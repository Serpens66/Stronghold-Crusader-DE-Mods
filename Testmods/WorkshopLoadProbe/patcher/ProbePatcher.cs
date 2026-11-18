using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace WorkshopLoadProbe
{
    // Preloader entry. Finish runs before the chainloader has opened or loaded anything from
    // BepInEx/plugins, which is the moment the safe installation needs.
    public static class ProbePatcher
    {
        public static IEnumerable<string> TargetDLLs => new string[0];

        public static void Patch(AssemblyDefinition assembly) { }

        public static void Finish()
        {
            try
            {
                ProbeLog.Initialize();
                var config = ProbeConfig.Read(BepInEx.Paths.ConfigPath);
                if (config.Error != null) { ProbeLog.Info("INACTIVE", config.Error); return; }
                ProbeLog.Info("CONFIG", config.ToString());
                // Read by the observer plugin for unattended test runs.
                if (config.QuitAfter > 0) AppDomain.CurrentDomain.SetData("WorkshopLoadProbe.QuitAfter", config.QuitAfter);
                if (config.Install) InstallRunner.Run();
            }
            catch (Exception ex) { ProbeLog.Error("ERROR", "Finish " + ex); }
        }
    }
}
