using System;
using BepInEx.Configuration;

namespace BugfixesAndQoL.AreaDemolition
{
    internal sealed class DemolitionSettings
    {
        private readonly ConfigEntry<bool>[] entries = new ConfigEntry<bool>[Enum.GetValues(typeof(TargetGroup)).Length];
        internal event Action Changed;
        internal DemolitionSettings(ConfigFile config, ConfigFile legacy=null)
        {
            // Appended categories get independent default-off keys; existing preferences keep their names.
            for (int i=0;i<entries.Length;i++)
            {
                var group=(TargetGroup)i;
                // A stored destination value overrides Bind's default, including imported defaults.
                bool fallback=legacy==null ? group==TargetGroup.Buildings :
                    legacy.Bind("Area filters",group.ToString(),group==TargetGroup.Buildings,"").Value;
                entries[i]=config.Bind("Area demolition filters", group.ToString(), fallback,
                    "Local area selection preference; Shift selection and ordinary single clicks ignore it.");
            }
        }
        internal ushort Mask { get { ushort result=0; for (int i=0;i<entries.Length;i++) if(entries[i].Value) result|=(ushort)(1<<i); return result; } }
        internal bool Get(TargetGroup group) => entries[(int)group].Value;
        internal void Set(TargetGroup group,bool value)
        { if (entries[(int)group].Value==value) return; entries[(int)group].Value=value; Changed?.Invoke(); }
    }
}
