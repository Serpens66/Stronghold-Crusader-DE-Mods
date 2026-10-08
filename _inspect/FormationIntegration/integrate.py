from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
shared = root/'APIShared/src/UnitCommands'
main = root/'BugfixesAndQoL/src'
old = root/'Testmods/FormationTest'
changed = []
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,s):
    p.parent.mkdir(parents=True,exist_ok=True)
    s=s.replace('\r\n','\n').replace('\r','\n')
    p.write_bytes(s.replace('\n','\r\n').encode('utf-8'))
    assert read(p)==s
    changed.append(str(p.relative_to(root)))
def replace(s,a,b):
    assert a in s,a[:120]
    return s.replace(a,b)
def remove_method(s, signature):
    start=s.index(signature)
    start=s.rfind('\n',0,start)+1
    brace=s.index('{',start); depth=1; end=brace+1
    while depth:
        depth += (s[end]=='{')-(s[end]=='}');end+=1
    return s[:start]+s[end:]

for name in ['FormationModel','FormationReleaseStateModel','FormationPreviewMarkerModel','FormationOrderPacket']:
    s=read(old/'src'/f'{name}.cs').replace('namespace FormationTest','namespace APIShared.UnitCommands')
    if name=='FormationOrderPacket': s=s.replace('public sealed class','internal sealed class')
    write(shared/f'{name}.cs',s)
for name in ['FormationMenuViewModel','FormationPreviewOverlay']:
    s=read(old/'src'/f'{name}.cs').replace('namespace FormationTest','namespace BugfixesAndQoL').replace('FormationTest','BugfixesAndQoLFormation')
    s='using APIShared.UnitCommands;\n'+s
    if name=='FormationMenuViewModel':
        s=s.replace(': INotifyPropertyChanged',': INotifyPropertyChanged, IFormationPresentation')
        s=s.replace('private readonly ManualLogSource log;','private readonly ManualLogSource log;\n        internal Func<bool> Enabled = () => false;')
        s=s.replace('if (menuVisible &&','if (menuVisible && (!Enabled() ||')
        s=s.replace('FatControler.currentScene != Enums.SceneIDS.ActualMainGame))','FatControler.currentScene != Enums.SceneIDS.ActualMainGame)))',1)
        s=s.replace('private void ToggleMenu()\n        {','private void ToggleMenu()\n        {\n            if (!Enabled()) return;')
        s=s.replace('internal void RefreshHostState()', 'public void RefreshHostState()').replace('internal void CloseMenu()', 'public void CloseMenu()')
        s=s.replace('public event PropertyChangedEventHandler PropertyChanged;', '''public void RefreshPreview() => FormationPreviewOverlay.Refresh();
        public void ClearPreview() => FormationPreviewOverlay.Clear();
        public void PublishPreview(FormationPreviewPoint[] points, FormationDirectionIndicator direction) =>
            FormationPreviewOverlay.Publish(points, direction);

        public event PropertyChangedEventHandler PropertyChanged;''')
    write(main/f'{name}.cs',s)

for name in ['LargeMoveTargetMarkerRenderer','LargeMoveTargetOverflowModel','GroundMovePreviewAuthorization']:
    s=read(main/f'{name}.cs').replace('namespace BugfixesAndQoL','namespace APIShared.UnitCommands')
    if name=='LargeMoveTargetMarkerRenderer':
        s=s.replace('BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region)', '''new HookTransaction(context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true })''')
        s=replace(s,'''BugfixesHookInfrastructure.AddContextHook(
                    candidate, visibleTileHook,
                    unchecked((ulong)(libraryHandle + VisibleTileHookRva).ToInt64()),
                    RenderVisibleLargeMoveTarget, X64SmartCPUContextRegs.All,
                    VisibleTileMinimumHookLength, CallbackErrorMode.LogAndContinue,
                    OverwrittenInstructionPlacement.AfterCallback);''','''candidate.AddContextHook(visibleTileHook,
                    HookTarget.FromAddress(unchecked((ulong)(libraryHandle + VisibleTileHookRva).ToInt64())),
                    RenderVisibleLargeMoveTarget, new ContextHookOptions {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = VisibleTileMinimumHookLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.AfterCallback });''')
        # Retain a successfully committed candidate before validation: no published teardown.
        s=s.replace('CommitResult result = candidate.Commit();','CommitResult result = candidate.Commit();\n                transaction = candidate;')
        s=s.replace('transaction = null;\n                candidate.Dispose();','if (transaction == null) candidate.Dispose();\n                failed = true;')
    write(shared/f'{name}.cs',s)
    (main/f'{name}.cs').unlink()

write(shared/'FormationPresentation.cs','''using System;
namespace APIShared.UnitCommands
{
    internal interface IFormationPresentation
    {
        void CloseMenu();
        void RefreshHostState();
        void RefreshPreview();
        void ClearPreview();
        void PublishPreview(FormationPreviewPoint[] points, FormationDirectionIndicator direction);
    }
}
''')

s=read(old/'src/FormationTestRuntime.cs').replace('namespace FormationTest','namespace APIShared.UnitCommands').replace('FormationTestRuntime','FormationRuntime').replace('FormationTest active','BugfixesAndQoL Formation active').replace('FORMATION_TEST_DISABLED','FORMATION_DISABLED')
s=s.replace('FormationMenuViewModel','IFormationPresentation').replace('FormationPreviewMarkerRenderer','LargeMoveTargetMarkerRenderer')
s=s.replace('FormationPreviewOverlay.Clear()','menuViewModel.ClearPreview()').replace('FormationPreviewOverlay.Refresh()','menuViewModel.RefreshPreview()').replace('FormationPreviewOverlay.Publish(','menuViewModel.PublishPreview(')
s=s.replace('MainViewModel.instance','(MainViewModel.viewModelLoaded ? MainViewModel.Instance : null)')
# Hooks are solely owned by the existing shared command runtime.
start=s.index('        private readonly DetourHandle<FormationSlotDelegate>')
end=s.index('        private LargeMoveTargetMarkerRenderer',start)
s=s[:start]+'''        private readonly IUnitCommandSettings settings;
        private readonly UnitCommandPathRuntime commandRuntime;
        private NativeGroundMoveFeedbackReader groundFeedbackReader;
        private bool startupConfirmed;
        internal bool Enabled => initialized && !failed && settings.EnableMod && settings.EnableMoveFormationEnhancements;

'''+s[end:]
s=s.replace('        private HookTransaction nativeTransaction;\n','')
s=replace(s,'            IFormationPresentation menuViewModel)','''            IFormationPresentation menuViewModel,
            IUnitCommandSettings settings,
            UnitCommandPathRuntime commandRuntime,
            LargeMoveTargetMarkerRenderer markerRenderer)''')
s=s.replace('            this.log = log ??', '''            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.commandRuntime = commandRuntime ?? throw new ArgumentNullException(nameof(commandRuntime));
            this.markerRenderer = markerRenderer ?? throw new ArgumentNullException(nameof(markerRenderer));
            this.log = log ??''',1)
s=s.replace('            HookTransaction pendingNative = null;\n','').replace('            LargeMoveTargetMarkerRenderer pendingMarkerRenderer = null;\n','')
start=s.index('                byte[] standardSelectorEntry =')
end=s.index('                nativeTribeManager =',start)
s=s[:start]+'''                if (!commandRuntime.FormationHooksAvailable || !markerRenderer.ReplacementAvailable)
                    throw new InvalidOperationException("Shared formation selectors or marker renderer are unavailable.");
                groundFeedbackReader = new NativeGroundMoveFeedbackReader(libraryContext.ModuleHandle, libraryContext.Memory);
'''+s[end:]
start=s.index('                pendingNative = new HookTransaction(')
end=s.index('                leftMouseStateField =',start)
s=s[:start]+s[end:]
s=s.replace('                nativeTransaction = pendingNative;\n                pendingNative = null;\n','')
start=s.index('                markerRenderer = pendingMarkerRenderer;')
end=s.index('                initialized = true;',start)
s=s[:start]+'''                commandRuntime.formationRuntime = this;
'''+s[end:]
s=s.replace('                    $"{standardContract}, {assassinContract}, {commonContract}, " +','                    "selectors=APIShared/process-owned, " +')
s=s.replace('                pendingNative?.Dispose();\n','').replace('                pendingMarkerRenderer?.RollbackUnpublished();\n','')
s=remove_method(s,'private static string ValidateNativeDetourContract<TFunction>')
# Root each installed managed hook immediately; failed initialization only disables logical state.
s=s.replace('pendingEngineRun = new Hook(engineRun, (EngineRunDelegate)EngineRunHook);','''pendingEngineRun = new Hook(engineRun, (EngineRunDelegate)EngineRunHook,
                    new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.Formation.Release" });''')
s=s.replace('pendingCameraUpdate = new Hook(cameraUpdate, (CameraUpdateDelegate)CameraUpdateHook);','''pendingCameraUpdate = new Hook(cameraUpdate, (CameraUpdateDelegate)CameraUpdateHook,
                    new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.Formation.Zoom" });''')
s=s.replace('                packetHook =', '''                engineRunHook = pendingEngineRun;
                pendingEngineRun = null;
                engineRunHook.Apply();
                cameraUpdateHook = pendingCameraUpdate;
                pendingCameraUpdate = null;
                cameraUpdateHook.Apply();

                packetHook =''',1)
s=s.replace('                engineRunHook = pendingEngineRun;\n                pendingEngineRun = null;\n                cameraUpdateHook = pendingCameraUpdate;\n                pendingCameraUpdate = null;\n','')
s=s.replace('            catch\n            {\n                pendingUnitMove', '            catch\n            {\n                failed = true;\n                pendingUnitMove',1)
s=s.replace('if (!initialized || failed || args == null)','if (!Enabled || args == null)')
s=s.replace('if (!initialized || failed)\n                return engineRunOriginal(mpFrameSkip);','''if (!startupConfirmed && initialized && HasValidMap())
            {
                startupConfirmed = true;
                LogDebugNoThrow("FORMATION_RUNTIME_AFTER_STARTUP_CLEANUP: persistent EngineInterface.run callback reached.");
            }
            if (!Enabled)
            {
                ResetTransientState();
                return engineRunOriginal(mpFrameSkip);
            }''')
s=s.replace('if (!failed && state != null)','if (Enabled && state != null)')
s=s.replace('state != null && markerRenderer != null &&','Enabled && state != null && markerRenderer != null &&')
s=s.replace('            if (!ValidatePacket(packet, out string rejection))','''            if (!Enabled)
                return TryIssuePacketVanillaFallback(packet, source, "feature-disabled");
            if (!ValidatePacket(packet, out string rejection))''')
# Existing native selector dispatcher calls these logical providers; no second installation.
start=s.index('        private void ChooseStandardFormationSlot(')
end=s.index('        private long CommonGroupMoveHook(',start)
s=s[:start]+'''        internal bool TryChooseStandardFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            ActiveFormationCommand command;
            lock (stateSync) command = activeCommand;
            if (!Enabled || !Matches(command, manager, x, y) ||
                !TryTakeDestination(command, out NativeDestination destination)) return false;
            WriteFormationOutput(destination);
            RecordSelectorAssignment(command, false);
            return true;
        }

        internal bool TryChooseAssassinFormationSlot(IntPtr manager, int spacing, int x, int y, out int tileId)
        {
            tileId = 0;
            ActiveFormationCommand command;
            lock (stateSync) command = activeCommand;
            if (!Enabled || !Matches(command, manager, x, y) ||
                !TryTakeDestination(command, out NativeDestination destination)) return false;
            WriteFormationOutput(destination);
            RecordSelectorAssignment(command, true);
            tileId = destination.TileId;
            return true;
        }

'''+s[end:]
s=s.replace('private long CommonGroupMoveHook(', 'internal long CommonGroupMoveHook(')
s=s.replace('            int newOrder)\n        {\n            ActiveFormationCommand previous;', '            int newOrder, Func<long> original)\n        {\n            ActiveFormationCommand previous;')
s=s.replace('return commonGroupMoveHandle.Original(\n                    manager, tribeId, x, y, patrol, newOrder);','return original();')
s=s.replace('                if (!markerRenderer.SetPreviewMarkerTiles(markerTiles))','''                markerRenderer.SetPreviewMarkerTiles(markerTiles, state.PreviewAuthorization);
                if (!markerRenderer.ReplacementAvailable)''')
s=s.replace('            ResolveDirectionAndWidth(state, out int direction, out int width);\n            state.DirectionSector', '''            state.Authorization = new GroundMovePreviewAuthorization(
                GamePlayerManagerAPI.Instance.GetLocalPlayerId(), tribeId, selection.Length,
                target.NativeX, target.NativeY);
            state.PreviewAuthorization = () => AuthorizePreview(state);
            ResolveDirectionAndWidth(state, out int direction, out int width);
            state.DirectionSector''',1)
insert=s.index('        private bool ValidateActiveDrag(')
s=s[:insert]+'''        private bool AuthorizePreview(ActiveDrag state)
        {
            lock (stateSync)
                return Enabled && ReferenceEquals(drag, state) && ValidateActiveDrag(state) &&
                    state.Authorization.Observe(groundFeedbackReader.Read());
        }

        internal void ResetTransientState()
        {
            lock (stateSync)
            {
                if (drag == null && pendingCommand == null && activeCommand == null) return;
                drag = null;
                pendingCommand = null;
                activeCommand = commonGroupCommand = null;
                ClearUnitAssignmentFrames(null);
                unitFallbackAttempt = null;
            }
            ClearPreview();
            menuViewModel.CloseMenu();
        }

'''+s[insert:]
s=s.replace('        private sealed class ActiveDrag\n        {', '''        private sealed class ActiveDrag
        {
            internal GroundMovePreviewAuthorization Authorization;
            internal Func<bool> PreviewAuthorization;''')
write(shared/'FormationRuntime.cs',s)

# Remove old density selector/planner while keeping native safety and traversal adapters.
p=shared/'NativeFormationSlots.cs';s=read(p)
start=s.index('        internal MoveFormationPreviewPlanner');end=s.index('        internal sealed class OriginalFormationSlotException',start)
s=s[:start]+'''        internal FormationRuntime formationRuntime;
        internal bool FormationHooksAvailable => formationSlotDetour != null && formationSlotDetour.Committed &&
            assassinGroundFormationSlotDetour != null && assassinGroundFormationSlotDetour.Committed &&
            commonGroupMoveDetour != null && commonGroupMoveDetour.Committed;

'''+s[end:]
start=s.index('            // Vanilla uses fixed spacing');end=s.index('            if (nativeTribeManager ==',start)
s=s[:start]+'''            if (formationRuntime != null && formationRuntime.TryChooseStandardFormationSlot(manager, spacing, x, y)) return;
            int effectiveSpacing = spacing;
'''+s[end:]
start=s.index('            MoveCommandScope command = activeMoveCommand;',s.index('internal int ChooseAssassinGroundFormationSlot('))
end=s.index('        internal void InvokeOriginalFormationSlot(',start)
s=s[:start]+'''            if (formationRuntime != null && formationRuntime.TryChooseAssassinFormationSlot(manager, spacing, x, y, out int tileId))
                return tileId;
            return originalAssassinGroundFormationSlot(manager, spacing, x, y);
        }

'''+s[end:]
write(p,s)
p=shared/'MoatPlacement.cs';s=read(p)
s=replace(s,'                return originalCommonGroupMove(manager, tribe, x, y, patrol, newOrder);','''                return formationRuntime != null
                    ? formationRuntime.CommonGroupMoveHook(manager, tribe, x, y, patrol, newOrder,
                        () => originalCommonGroupMove(manager, tribe, x, y, patrol, newOrder))
                    : originalCommonGroupMove(manager, tribe, x, y, patrol, newOrder);''')
write(p,s)
p=shared/'UnitCommandPathRuntime.cs';s=read(p)
start=s.index('                MoveFormationCommandContext.ObserveMoveOrder(');end=s.index('                ClearUnitMoveFrames();',start)
s=s[:start]+s[end:]
s=s.replace('                    MovementOptionsSnapshot.Capture(settings),\n                    hasFormationSpacing,\n                    formationSpacing);','                    MovementOptionsSnapshot.Capture(settings));')
start=s.index('                if (hasFormationSpacing)');end=s.index('                if (!activeMoveCommand.Options.RequiredOnly',start)
s=s[:start]+s[end:]
s=s.replace('                CompleteManagedFormationPlan(command);\n','').replace('            CompleteManagedFormationPlan(null);','            formationRuntime?.ResetTransientState();')
s=s.replace('            MoveFormationCommandSnapshotStore.Clear();\n','')
start=s.index('            MoveFormationUnitIdentity[] formationIdentities =');end=s.index('            foreach (int unitId',start)
s=s[:start]+s[end:]
start=s.index('                if (formationIdentities != null)');end=s.index('                if (CanDigMoat(unit))',start)
s=s[:start]+s[end:]
start=s.index('            if (formationIdentities != null');end=s.index('\n        }',start)
s=s[:start]+s[end:]
s=s.replace('                MovementOptionsSnapshot options,\n                bool hasFormationSpacing = false,\n                int formationSpacing = MoveFormationSpacingPolicy.Default)','                MovementOptionsSnapshot options)')
s=s.replace('                HasFormationSpacing = hasFormationSpacing;\n                FormationSpacing = MoveFormationSpacingPolicy.Normalize(formationSpacing);\n','')
s=s.replace('            public bool HasFormationSpacing { get; }\n            public int FormationSpacing { get; }\n','')
write(p,s)
p=shared/'PermanentCommandHooks.cs';s=read(p)
s=s.replace('        [ThreadStatic] private static Stack<Action> moveFormationParents;\n','')
s=s.replace('                if (moveFormationParents == null) moveFormationParents = new Stack<Action>();\n                Action parent = activeMoveCommand != null ? MoveFormationCommandContext.CaptureForNestedCommand() : null;\n                moveFormationParents.Push(parent);\n','')
s=re.sub(r'^\s*moveFormationParents\.(?:Pop|Push).*\n','\n',s,flags=re.M)
s=s.replace('                    if (moveFormationParents?.Count > 0) moveFormationParents.Pop()?.Invoke();\n','')
write(p,s)
p=shared/'UnitCommandPathRuntime.cs';s=read(p).replace(' moveFormationParents?.Clear();','');write(p,s)

p=shared/'QueueNativeContract.cs';s=read(p)
s=s.replace('        public const int MoveFormationSpacingMask = 0x0C;\n','')
start=s.index('        public static bool ShouldPackFormationSpacing(');end=s.index('        public const int GameTribePointerAdjustment',start)
s=s[:start]+s[end:]
s=s.replace('int vanillaPayload = payloadByte & ~MoveFormationSpacingMask;', 'int vanillaPayload = payloadByte;')
start=s.index('        public static bool TryEncodeFormationSpacing(');end=s.index('        public static bool TryDecodeQueuedMoveType(',start)
s=s[:start]+s[end:];write(p,s)

p=main/'ExtendedShiftCommandQueueRuntime.cs';s=read(p)
s=s.replace('        private readonly MoveFormationDragRuntime moveFormationDrag;\n','')
start=s.index('            Func<int, int, bool> targetAvailable =');end=s.index('\n        }',start)
s=s[:start]+s[end:]
start=s.index('            try\n            {\n                moveFormationDrag.Install(context);');end=s.index('            sharedCommands.queueTargetEvent',start)
s=s[:start]+s[end:]
s=re.sub(r'            if \(!settings.EnableMod \|\| !settings.EnableMoveFormationEnhancements\)\n                moveFormationDrag.ResetTransientState\(\);\n','',s)
s=re.sub(r'^\s*moveFormationDrag.ResetTransientState\(\);\n','\n',s,flags=re.M)
s=re.sub(r'^\s*moveFormationDrag.DisableForProcess\([^;]+;\n','\n',s,flags=re.M)
start=s.index('                if (ShouldMarkOutgoingFormationOrder())');end=s.index('                if (ShouldMarkOutgoingMultiplayerOrder())',start)
s=s[:start]+s[end:]
start=s.index('            bool executeFormationScope =');end=s.index('\n        }',start)
s=s[:start]+'''            trampolineEntered = true;
            moveChoreHandlerHook.Original();'''+s[end:]
start=s.index('        private bool ShouldMarkOutgoingFormationOrder()');end=s.index('        private void LogMultiplayerMarkerFailure',start)
s=s[:start]+s[end:]
# Remove dangling old-density conditions and event bookkeeping.
s=s.replace('            if (chore.StartsWith("Chore 17", StringComparison.Ordinal))\n','')
s=re.sub(r'\s*MoveFormationCommandContext.ObserveMoveOrder\(\s*args,\s*settings.EnableMod && settings.EnableMoveFormationEnhancements\);','',s)
s=s.replace('                MoveFormationCommandSnapshotStore.Clear();\n','').replace('                MoveFormationCommandContext.CompleteMoveOrder();\n','').replace('            MoveFormationCommandSnapshotStore.Clear();\n','').replace('            MoveFormationCommandContext.CompleteMoveOrder();\n','')
write(p,s)

# Project source ownership.
p=root/'APIShared/APIShared.csproj';s=read(p)
for name in ['MoveFormationCommandContext','MoveFormationPreviewPlanner','MoveFormationSpacingPolicy']:
    s=s.replace(f'    <Compile Include="src\\UnitCommands\\{name}.cs" />\n','')
new=['FormationModel','FormationReleaseStateModel','FormationPreviewMarkerModel','FormationOrderPacket','FormationPresentation','FormationRuntime','LargeMoveTargetMarkerRenderer','LargeMoveTargetOverflowModel','GroundMovePreviewAuthorization']
s=s.replace('    <Compile Include="src\\UnitCommands\\NativeFormationSlots.cs" />', '\n'.join(f'    <Compile Include="src\\UnitCommands\\{n}.cs" />' for n in new)+'\n    <Compile Include="src\\UnitCommands\\NativeFormationSlots.cs" />')
s=s.replace('    <Reference Include="System.Core" />','    <Reference Include="System.Core" />\n    <Reference Include="System.Numerics" />\n    <Reference Include="UnityEngine.InputLegacyModule"><HintPath>$(GameDir)\\Stronghold Crusader Definitive Edition_Data\\Managed\\UnityEngine.InputLegacyModule.dll</HintPath><Private>false</Private></Reference>')
write(p,s)
p=root/'BugfixesAndQoL/BugfixesAndQoL.csproj';s=read(p)
for name in ['MoveFormationDragGesture','MoveFormationDragRuntime','LargeMoveTargetMarkerRenderer','LargeMoveTargetOverflowModel','GroundMovePreviewAuthorization']:
    s=s.replace(f'    <Compile Include="src\\{name}.cs" />\n','')
s=s.replace('    <Compile Include="src\\LargeMoveTargetMarkerRuntime.cs" />','\n'.join(f'    <Compile Include="src\\{n}.cs" />' for n in ['FormationFeature','FormationSelectionMigration','FormationMenuViewModel','FormationPreviewOverlay'])+'\n    <Compile Include="src\\LargeMoveTargetMarkerRuntime.cs" />')
write(p,s)
for name in ['MoveFormationCommandContext','MoveFormationPreviewPlanner','MoveFormationSpacingPolicy']: (shared/f'{name}.cs').unlink()
for name in ['MoveFormationDragGesture','MoveFormationDragRuntime']: (main/f'{name}.cs').unlink()
p=root/'Testmods/MoatMove/src/MoatMoveOptions.cs';s=read(p).replace('        public int MoveFormationSpacing => MoveFormationSpacingPolicy.Default;\n','');write(p,s)
write(root/'_inspect/FormationIntegration/changed.txt','\n'.join(changed)+'\n')
