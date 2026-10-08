from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[2]
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,s):
    p.parent.mkdir(parents=True,exist_ok=True)
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
    p.write_bytes(data)
    assert p.read_bytes()==data and not re.search(rb'(?<!\r)\n',data)

p=ROOT/'APIShared/src/Core/Contracts.cs'
s=read(p)
s=s.replace('public static IApiShared Current => ApiSharedRuntime.ProcessInstance;', '''public static IApiShared Current => ApiSharedRuntime.ProcessInstance;

        /// <summary>Creates a client bound to a stable, non-empty BepInEx plugin GUID. Acquiring a client installs no hooks and reserves no capability.</summary>
        public static ModApiClient ForMod(string ownerGuid) => new ModApiClient(ownerGuid, Current);''')
s=s.replace('Registrations run synchronously.', 'Registrations run synchronously.')
s=s.replace('registrations run synchronously.','registrations run synchronously on the registering thread. Early callbacks run on the initialization publisher thread. Callback exceptions are logged and isolated in both cases; no thread dispatch is performed.')
write(p,s)
methods=re.findall(r'bool (TryGet\w+)\(\s*string ownerGuid,\s*out (\w+) capability,\s*out NativeCapabilityDiagnostic diagnostic\);',s,re.S)
assert len(methods)==9,methods
body='''using System;

namespace APIShared
{
    /// <summary>Owner-bound access to shared capabilities. Keep this client in your process-owned runtime; it does not own or dispose shared hooks.</summary>
    public sealed class ModApiClient
    {
        private readonly IApiShared api;

        internal ModApiClient(string ownerGuid, IApiShared api)
        {
            if (string.IsNullOrWhiteSpace(ownerGuid))
                throw new ArgumentException("A non-empty BepInEx plugin GUID is required.", nameof(ownerGuid));
            OwnerGuid = ownerGuid;
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        /// <summary>The exact GUID used for acquisitions and owner-local registrations; never a display name.</summary>
        public string OwnerGuid { get; }
        /// <summary>Global publication state, independent of individual capability availability.</summary>
        public NativeApiState State => api.State;

        /// <summary>Runs after global initialization reaches a terminal state. Late calls run synchronously on the caller's thread; early calls use the initialization publisher thread. Exceptions are isolated; no dispatch occurs.</summary>
        public void WhenReady(Action<ModApiClient> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            ApiSharedRuntime.ProcessInstance.WhenReady(_ => callback(this));
        }
'''
for name,cap in methods:
    body+=f'''
        /// <summary>Acquires {cap} for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool {name}(out {cap} capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.{name}(OwnerGuid, out capability, out diagnostic);
'''
write(p.parent/'ModApiClient.cs',body+'    }\n}\n')
p=ROOT/'APIShared/src/Core/ApiSharedRuntime.cs'; s=read(p)
s=s.replace('            callback(this);\n        }', '''            InvokeReadyCallback(callback);
        }

        private void InvokeReadyCallback(Action<IApiShared> callback)
        {
            try { callback(this); }
            catch (Exception ex) { NativeApiLog.Error(log, $"APIShared readiness callback failed: build={binaryHash}, error={ex}"); }
        }''',1)
s=s.replace('''                try { callback(this); }
                catch (Exception ex) { NativeApiLog.Error(log, $"APIShared readiness callback failed: build={binaryHash}, error={ex}"); }''','''                InvokeReadyCallback(callback);''')
write(p,s)

# Optional general policy: keep default semantics for every existing Serps caller.
p=ROOT/'APIShared/src/GameModes/GameplayModModePolicy.cs'; s=read(p)
s=s.replace('GameplayModAllowedContext allowedContexts)', 'GameplayModAllowedContext allowedContexts,\n            bool allowRealMultiplayer = true)')
s=s.replace('            AllowedContexts = allowedContexts;','            AllowedContexts = allowedContexts;\n            AllowRealMultiplayer = allowRealMultiplayer;')
s=s.replace('        public GameplayModAllowedContext AllowedContexts { get; }','''        public GameplayModAllowedContext AllowedContexts { get; }
        /// <summary>Whether this optional profile permits real multiplayer. Local skirmish is not real multiplayer.</summary>
        public bool AllowRealMultiplayer { get; }''')
s=s.replace('            bool allowed = (profile.AllowedContexts & context) == context;', '''            if (snapshot.IsRealMultiplayer && !profile.AllowRealMultiplayer)
            {
                reason = "profile-does-not-allow-real-multiplayer";
                return false;
            }
            bool allowed = (profile.AllowedContexts & context) == context;''')
s=s.replace('Single typed source of truth for mode permissions of regular gameplay mods.', 'Optional evaluator for caller-defined gameplay profiles. Construct a profile for any mod GUID; capture and lifecycle do not apply this policy automatically.')
s=s.replace('GameplayModAllowedContext in the centralized mission policy contract.', 'Recognized contexts that an optional gameplay profile can permit; combine values with bitwise OR.')
s=s.replace('GameplayModActivationProfile in the centralized mission policy contract.', 'Caller-defined optional permissions. Unknown or conflicting contexts fail closed when evaluated.')
s=s.replace('ModGuid in the centralized mission policy contract.', 'The consumer BepInEx GUID; no Serps GUID whitelist is applied.')
s=s.replace('DisplayName in the centralized mission policy contract.', 'Human-readable name, falling back to the owner GUID.')
s=s.replace('AllowedContexts in the centralized mission policy contract.', 'Contexts explicitly permitted by this caller.')
s=s.replace('IsAllowed in the centralized mission policy contract.', 'Evaluates the supplied profile without changing simulation state and returns a diagnostic reason.')
s=s.replace('ResolveContext in the centralized mission policy contract.', 'Maps an existing mode snapshot to a recognized permission context; unknown contexts return None.')
write(p,s)

# Populate partial-class IntelliSense, retained member bodies need no extra game access.
for suffix,summary in [('Persistence','Personal preset persistence, defaults, working snapshots and stable storage schema.'),('Sources','Preset source selection, save/load UI and settings search bindings.')]:
    p=ROOT/f'APIShared/src/ModSettings/PresetLobbyModSettingsViewModel.{suffix}.cs'
    write(p,read(p).replace('    public abstract partial class','    /// <summary>'+summary+'</summary>\n    public abstract partial class',1))
print('Owner-bound API, optional multiplayer policy and isolated readiness callbacks added.')
