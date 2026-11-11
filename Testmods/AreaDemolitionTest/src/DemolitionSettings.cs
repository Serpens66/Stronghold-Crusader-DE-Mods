using System;
using BepInEx.Configuration;

namespace AreaDemolitionTest
{
    internal sealed class DemolitionSettings
    {
        private readonly ConfigEntry<bool>[] entries = new ConfigEntry<bool>[Enum.GetValues(typeof(TargetGroup)).Length];
        internal event Action Changed;
        internal DemolitionSettings(ConfigFile config)
        {
            for (int i=0;i<entries.Length;i++)
            {
                var group=(TargetGroup)i;
                entries[i]=config.Bind("Area filters", group.ToString(), group==TargetGroup.Buildings,
                    "Local area selection preference; Shift selection and ordinary single clicks ignore it.");
            }
        }
        internal ushort Mask { get { ushort result=0; for (int i=0;i<entries.Length;i++) if(entries[i].Value) result|=(ushort)(1<<i); return result; } }
        internal bool Get(TargetGroup group) => entries[(int)group].Value;
        internal void Set(TargetGroup group,bool value)
        { if (entries[(int)group].Value==value) return; entries[(int)group].Value=value; Changed?.Invoke(); }
    }
}
