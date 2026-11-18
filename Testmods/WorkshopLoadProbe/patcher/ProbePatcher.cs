using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace WorkshopLoadProbe
{
    // Preloader entry for unattended game tests of APIShared's Workshop installation: it only hands
    // quitafter to the observer plugin. The installation itself is APIShared's own preloader.
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
            }
            catch (Exception ex) { ProbeLog.Error("ERROR", "Finish " + ex); }
        }
    }
}