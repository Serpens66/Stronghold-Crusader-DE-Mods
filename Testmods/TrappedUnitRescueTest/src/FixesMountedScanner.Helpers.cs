using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API.LowLevel;

namespace TrappedUnitRescueTest
{
    internal static class MountedScannerHelpers
    {
        // Only relevant loaded helper bodies, never a DLL/version hash. Populated from the audited actual assemblies.
        internal static readonly Dictionary<string,string> Expected=new Dictionary<string,string>{
            {"Scanner.ScanString","552CA978B34C15176754CF873AC76C2E179010CD45407A13AEC22AC94403B9D4"},{"Scanner.ScanCompiled","F64AFFF50554B726C5A6866A2BD9333AF627DAC3988787C60C1FE8BDD98A5424"},
            {"Context.CreateScanner","D3F46A9B5B91EA87346C42338A91227680782EB755B0085B4BC8FF73A5F6BE4B"},{"Context.get_Region","B296F6E04F3753E4F76B3CAE7934B1EFCB1EAC0705220899899217C1FA9FE9D0"},{"Context.get_ModuleHandle","730D43C7DDA80F6ED4559996BF1B581E6D1A76D7A25C2FEAB50CB028D01C096D"},{"Context.get_Memory","51DB8365893A3EF66B8D331B4FCBFEB7C50A7B6C34BC778F47549C30251427A6"},
            {"Scanner.Create","14D09852E9E0E18794C7DE85C9E4439B151A911ACAEC77A97514681515D190D8"},{"Scanner.ctor","FF76C6C42214EBD92488AE9FC22217F3473CC2DD2DB71530B16CA858CC343E93"},{"Scanner.At","48732B62E12C54A5EE47C4021D9D079FB6161E7122F6B166B97CB3593A0D066A"},{"Scanner.get_CurrentAddress","9D73FCA0D824B87E06D8B58D2A5BE5A9B16EB56E78CC4E24CD0E286CE4C7BC62"},{"Scanner.get_Found","52F4F6C920DC76ACC75C662F64D9D2954B8FED0DCEA441E492A701FA08DB93BE"}};
        private const string Context="SHCDESE.API.LowLevel.CrusaderLibraryLoadContext",Scanner="RedBird.X64.Memory.Scanners.DataScanner";
        internal static Dictionary<string,MethodDefinition> Select(ModuleDefinition module,string family)
        {
            string owner=family=="Context"?Context:Scanner;
            var type=module.GetType(owner)??throw new InvalidOperationException("Required helper owner missing.");
            var selected=new Dictionary<string,MethodDefinition>();
            Action<string,string,bool,string,string[]> add=(key,name,isStatic,result,args)=>selected.Add(family+"."+key,
                type.Methods.Single(m=>m.DeclaringType.FullName==owner && m.Name==name && m.IsStatic==isStatic &&
                    m.HasThis==!isStatic && !m.ExplicitThis && m.CallingConvention==MethodCallingConvention.Default &&
                    m.GenericParameters.Count==0 && m.ReturnType.FullName==result && m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(args)));
            if(family=="Context")
            {
                add("CreateScanner","CreateScanner",false,Scanner,new[]{"Microsoft.Extensions.Logging.ILogger"});
                add("get_Region","get_Region",false,"RedBird.Core.Memory.ScanRegion",new string[0]);
                add("get_ModuleHandle","get_ModuleHandle",false,"System.IntPtr",new string[0]);
                add("get_Memory","get_Memory",false,"System.ReadOnlySpan`1<System.Byte>",new string[0]);
            }
            else
            {
                add("ctor",".ctor",false,"System.Void",new[]{"RedBird.Core.Memory.ScanRegion","Microsoft.Extensions.Logging.ILogger","RedBird.Core.Memory.Scanners.AobCacheOptions"});
                add("Create","Create",true,Scanner,new[]{"RedBird.Core.Memory.ScanRegion","Microsoft.Extensions.Logging.ILogger"});
                add("get_CurrentAddress","get_CurrentAddress",false,"System.UInt64",new string[0]);
                add("get_Found","get_Found",false,"System.Boolean",new string[0]);
                add("At","At",false,Scanner,new[]{"System.UInt64"});
                add("ScanString","Scan",false,Scanner,new[]{"System.String","System.Int32"});
                add("ScanCompiled","Scan",false,Scanner,new[]{"RedBird.Core.Memory.Scanners.CompiledPattern","System.Int32"});
            }
            return selected;
        }
        internal static Dictionary<string,string> Fingerprints()
        {
            var result=new Dictionary<string,string>();
            foreach(string family in new[]{"Context","Scanner"})
                using(var module=ModuleDefinition.ReadModule((family=="Context"?typeof(CrusaderLibraryLoadContext):typeof(DataScanner)).Assembly.Location))
                    foreach(var pair in Select(module,family))result.Add(pair.Key,MountedScannerContract.Fingerprint(pair.Value));
            return result;
        }
        internal static bool ValidateSelected(ModuleDefinition module,string family,out string reason)
        {
            try
            {
                foreach(var pair in Select(module,family))
                    if(!Expected.TryGetValue(pair.Key,out string hash) || MountedScannerContract.Fingerprint(pair.Value)!=hash)
                    {reason="scanner-helper-contract:"+pair.Key;return false;}
                reason=string.Empty;return true;
            }
            catch{reason="scanner-helper-metadata-unavailable";return false;}
        }
        internal static bool Validate(out string reason)
        {
            try
            {
                foreach(string family in new[]{"Context","Scanner"})
                {
                    var assembly=(family=="Context"?typeof(CrusaderLibraryLoadContext):typeof(DataScanner)).Assembly;
                    using(var module=ModuleDefinition.ReadModule(assembly.Location))
                    {
                        if(module.Mvid!=assembly.ManifestModule.ModuleVersionId)throw new InvalidOperationException("Loaded helper module differs.");
                        if(!ValidateSelected(module,family,out reason))return false;
                        foreach(var method in Select(module,family).Values)MountedScannerContract.ValidateLoadedMethod(assembly,method);
                    }
                }
                reason=string.Empty;return true;
            }
            catch{reason="scanner-helper-loaded-contract-unavailable";return false;}
        }
    }
}
