using System.Collections.Generic;

// External persistence boundary only: tests exercise production key selection/defaults/mask,
// not BepInEx file IO or TOML conversion, which this fixture does not claim to reproduce.
namespace BepInEx.Configuration
{
    internal sealed class ConfigFile
    {
        internal readonly Dictionary<string,object> Values=new Dictionary<string,object>();
        public ConfigEntry<T> Bind<T>(string section,string key,T value,string description)
        {
            string identity=section+"/"+key;
            if(!Values.ContainsKey(identity))Values[identity]=value;
            return new ConfigEntry<T>(Values,identity);
        }
    }
    internal sealed class ConfigEntry<T>
    {
        private readonly Dictionary<string,object> values;
        private readonly string key;
        internal ConfigEntry(Dictionary<string,object> values,string key){this.values=values;this.key=key;}
        public T Value {get=>(T)values[key];set=>values[key]=value;}
    }
}
