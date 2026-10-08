from pathlib import Path
import shutil, re
root=Path.cwd()
def read(p):
    f=root/p; backup=root/'_inspect/FormationStartupFix/before'/p
    if not backup.exists():
        backup.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(f,backup)
    return f.read_text(encoding='utf-8-sig')
def write(p,s):
    (root/p).write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
def rep(s,a,b):
    assert a in s,a
    return s.replace(a,b)
p='APIShared/src/UnitCommands/NativeMovementCadenceResolver.cs';s=read(p)
s=s.replace('using SHCDESE.Interop;','using SHCDESE.Interop;\nusing SHCDESE.API.LowLevel;').replace('internal sealed unsafe class','internal sealed class')
s=s.replace('        internal readonly ulong libraryBase;','        private readonly CrusaderLibraryLoadContext libraryContext;\n        internal readonly ulong libraryBase;')
s=rep(s,'            ReadOnlySpan<byte> memory,\n            ulong libraryBase,','            CrusaderLibraryLoadContext libraryContext,')
s=rep(s,'            this.libraryBase = libraryBase;\n            moduleEnd = libraryBase + unchecked((ulong)memory.Length);','''            this.libraryContext = libraryContext ?? throw new ArgumentNullException(nameof(libraryContext));
            ReadOnlySpan<byte> memory = libraryContext.Memory;
            libraryBase = unchecked((ulong)libraryContext.ModuleHandle.ToInt64());
            moduleEnd = checked(libraryBase + (ulong)memory.Length);''')
s=rep(s,'int dispatchTableOffset = *(int*)(dispatchAddress + 4);','uint dispatchTableOffset = ReadUInt32(dispatchAddress + 4);')
s=rep(s,'ulong dispatchTable = libraryBase + unchecked((uint)dispatchTableOffset);','ulong dispatchTable = checked(libraryBase + dispatchTableOffset);')
s=s.replace('            ulong* nativeHandlers = (ulong*)dispatchTable;\n','').replace('ulong handler = nativeHandlers[unitType];','ulong handler = ReadUInt64(dispatchTable + (ulong)unitType * sizeof(ulong));')
s=s.replace('byte[] bytes = new ReadOnlySpan<byte>((byte*)handlerAddress, handlerLength).ToArray();','byte[] bytes = ReadSnapshot(handlerAddress, handlerLength).ToArray();')
s=s.replace('byte compressedCase = *((byte*)stateMap + aiState);','byte compressedCase = ReadSnapshot(stateMap + aiState, 1)[0];').replace('uint targetRva = *(uint*)(jumpTable + unchecked((ulong)compressedCase * 4));','uint targetRva = ReadUInt32(jumpTable + (ulong)compressedCase * 4);')
idx=s.index('        internal bool IsModuleRange(')
s=s[:idx]+'''        // All static analysis uses the pre-hook snapshot. Never decode live patched code.
        private ReadOnlySpan<byte> ReadSnapshot(ulong address, int length)
        {
            if (!IsModuleRange(address, length))
                throw new InvalidOperationException("Movement cadence read is outside the load snapshot.");
            return libraryContext.Memory.Slice(checked((int)(address - libraryBase)), length);
        }

        private uint ReadUInt32(ulong address)
        {
            ReadOnlySpan<byte> bytes = ReadSnapshot(address, sizeof(uint));
            return (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24);
        }

        private ulong ReadUInt64(ulong address) =>
            ReadUInt32(address) | ((ulong)ReadUInt32(address + sizeof(uint)) << 32);

'''+s[idx:]
s=s.replace('mode=read-only-no-audited-type-table.','mode=load-snapshot-read-only.')
write(p,s)
p='APIShared/src/UnitCommands/UnitCommandPathRuntime.cs';s=read(p)
s=rep(s,'''            nativeMovementCadenceResolver = new NativeMovementCadenceResolver(
                memory,
                libraryBase,
                unchecked((ulong)nativeUnitManager),
                log);''','''            try
            {
                nativeMovementCadenceResolver = new NativeMovementCadenceResolver(
                    context, unchecked((ulong)nativeUnitManager), log);
            }
            catch (Exception exception)
            {
                // Cadence qualification is optional; keep shared commands and selectors available.
                nativeMovementCadenceResolver = null;
                Shared.DebugLogHelper.LogWarning(log,
                    "CADENCE_RESOLVER_UNAVAILABLE: weighted route publication disabled; " +
                    "shared movement commands remain available: " + exception);
            }''');write(p,s)
p='APIShared/src/UnitCommands/WeightedMoatPublication.cs';s=read(p)
s=rep(s,'            if (!nativeMovementCadenceResolver.TryGetPlausibleSpeedBonuses(','''            if (nativeMovementCadenceResolver == null)
            {
                rejectionReason = "native-cadence-resolver-unavailable";
                return false;
            }
            if (!nativeMovementCadenceResolver.TryGetPlausibleSpeedBonuses(''');write(p,s)
p='BugfixesAndQoL/src/FormationFeature.cs';s=read(p)
s=rep(s,'            config.Save();','            RegisterPresentationBindings();\n            config.Save();')
start=s.index('                try\n                {\n                    GameXAMLManagerAPI.Instance.RegisterBinding(');end=s.index('\n            }\n            catch (Exception ex)',start)
s=s[:start]+s[end:]
idx=s.index('        private static string ReadConfig(')
s=s[:idx]+'''        private static void RegisterPresentationBindings()
        {
            foreach (string host in new[] {
                "BugfixesAndQoLFormationButtonHost", "BugfixesAndQoLFormationMenuHost",
                "BugfixesAndQoLFormationRolloverHost" })
            {
                try { GameXAMLManagerAPI.Instance.RegisterBinding(host, menu); }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogWarning(log,
                        "FORMATION_BINDING_FAILED: host=" + host + "; " + exception);
                }
            }
        }

'''+s[idx:]
needle='                Shared.DebugLogHelper.LogError(log, "Formation initialization failed; Vanilla remains active: " + ex);\n            }'
s=rep(s,needle,needle+'''
            finally
            {
                Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => menu.RefreshAvailability());
            }''');write(p,s)
p='BugfixesAndQoL/src/FormationMenuViewModel.cs';s=read(p)
s=s.replace('public bool MenuVisible => menuVisible;','public bool MenuVisible => menuVisible && FeatureAvailable;').replace('public bool RolloverVisible => rolloverVisible;','public bool RolloverVisible => rolloverVisible && FeatureAvailable;')
start=s.index('        public void RefreshHostState()');end=s.index('            MainViewModel current',start)
s=s[:start]+'''        internal void RefreshAvailability()
        {
            bool available = FeatureAvailable;
            if (!available)
            {
                SetMenuVisible(false);
                HideRollover();
            }
            if (lastAvailability != available)
            {
                lastAvailability = available;
                OnChanged(nameof(FeatureAvailable));
                OnChanged(nameof(MenuVisible));
                OnChanged(nameof(RolloverVisible));
            }
        }

        public void RefreshHostState()
        {
            RefreshAvailability();
'''+s[end:]
s=rep(s,'public void CloseMenu() => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => SetMenuVisible(false));','''public void CloseMenu() => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
        {
            SetMenuVisible(false);
            RefreshAvailability();
        });''')
for method in ['SelectFormation','SelectDensity','SelectPlacement','SelectRoleMarkers','ShowRollover']:
    needle=f'        private void {method}(object parameter)\n        {{'
    s=rep(s,needle,needle+'\n            if (!FeatureAvailable) return;')
write(p,s)
p='APIShared/src/UnitCommands/FormationRuntime.cs';s=read(p)
needle='            // Fail-open must reach Vanilla even if a presentation or logger also fails.'
s=s.replace('            try { menuViewModel.CloseMenu(); } catch { }','            try { menuViewModel.CloseMenu(); } catch { }\n            try { menuViewModel.RefreshHostState(); } catch { }')
idx=s.index('            try\n            {\n                Shared.DebugLogHelper.LogError(',s.index('        private void DisableAfterNativeFailure('))
s=s[:idx]+'''            try { menuViewModel.CloseMenu(); } catch { }
            try { menuViewModel.RefreshHostState(); } catch { }
'''+s[idx:];write(p,s)
p='BugfixesAndQoL/src/ExtendedShiftCommandQueueRuntime.cs';s=read(p)
idx=s.index('        public void Install(');body=s.index('        {',idx)+len('        {')
s=s[:body]+'''
            UnitCommandPathRuntime sharedCommands = UnitCommandPathAPI.Runtime;
            if (sharedCommands == null)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "SHIFT_QUEUE_UNAVAILABLE: shared command runtime is unavailable; Vanilla remains active.");
                return;
            }
'''+s[body:]
# Remove later duplicate after native setup; the prerequisite is captured before any installation.
idx=s.index('            UnitCommandPathRuntime sharedCommands = UnitCommandPathAPI.Runtime;',body+200)
s=s[:idx]+s[idx:].replace('            UnitCommandPathRuntime sharedCommands = UnitCommandPathAPI.Runtime;\n','',1);write(p,s)
p='BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml';s=read(p)
for prop in ['FeatureAvailable','MenuVisible','RolloverVisible','IsVanilla','IsBlock','IsLine','IsColumn','IsWedge','IsCircle']:
    s=s.replace(f'{{Binding {prop}, Converter={{StaticResource booleanToVisibilityConverter}}}}',f'{{Binding {prop}, Converter={{StaticResource booleanToVisibilityConverter}}, FallbackValue=Collapsed}}')
write(p,s)
