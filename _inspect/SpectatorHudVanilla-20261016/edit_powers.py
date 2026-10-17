from pathlib import Path
root=Path('Testmods/SpectatorEditorBuildTest')
p=root/'src/TestToolsPowers.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('internal long Session;','internal long Session, Generation;')
s=s.replace('private static int toolMouseState, powerMouseState;\n        private static bool consumePowerGesture;\n        private static IDisposable powerEscapeSubscription;', '''private static int toolMouseState;
        private static NativePowerContext powerContext;
        [ThreadStatic] private static bool selectingTestPower;''')
a=s.index('        private static void RegisterPowerInput()');b=s.index('        private static ToolPower DescribePower',a)
s=s[:a]+'''        private static void RegisterPowerInput() => RegisterVanillaPowerActions();

'''+s[b:]
s=s.replace('power.Enabled = nativePower != null &&', 'power.Enabled = nativePower != null && powerContext?.Ready == true &&')
a=s.index('        private static void SelectPower(');b=s.index('        private static unsafe uint ReadMana(',a)
s=s[:a]+'''        private static void SelectPower(int id)
        {
            amountInput?.EndEditing();
            lock (engineLock)
            {
                CancelPowerSelection();
                MainControls.instance?.StopAllPlacement();
                if ((uint)id > (uint)ExtremePower.RockVolley || !IsSelectableAi(humanSelectedOwner)) return;
                ToolPower power = DescribePower(id);
                if (!power.Enabled) { toolsModel.Status = SerpLocalization.Get("Tools.PowerUnavailable"); return; }
                pendingPower = new PowerSelection { Session = CurrentSessionId, Generation = powerGeneration,
                    Actor = humanSelectedOwner, Power = power };
                selectingTestPower = true;
                try
                {
                    if (power.Replacement != null && power.TargetKind == 0)
                        NativePowerQuery(3);
                    else
                    {
                        int nativeId = power.Id == (int)ExtremePower.GrantGold && power.TargetKind == 1
                            ? (int)ExtremePower.ArrowVolley : power.Id;
                        ToolPolicy.WithoutCost(() => ReadMana(humanSelectedOwner), value => WriteMana(humanSelectedOwner, value),
                            (uint)Math.Max(power.Cost, (nativeId + 1) * 636), () => {
                                powerContext.Select(nativeId, humanSelectedOwner); return true;
                            });
                        if (pendingPower != null && powerContext.Mode == 5)
                            powerContext.SetSelectedPower(power.Id); // Same Gold-map-target mapping as ExtremePowers.
                        else if (pendingPower != null) pendingPower = null; // Vanilla refused selection (e.g. player paused).
                    }
                }
                catch { CancelPowerSelection(); throw; }
                finally { selectingTestPower = false; }
            }
        }

        private static void CancelPowerSelection()
        {
            if (engineLock == null) { pendingPower = null; powerGeneration++; return; }
            lock (engineLock)
            {
                PowerSelection previous = pendingPower;
                pendingPower = null; powerGeneration++;
                if (previous != null && IsCurrentSession(previous.Session) && powerContext?.Ready == true &&
                    (powerContext.Mode == 4 || powerContext.Mode == 5) && powerContext.SelectedPower == previous.Power.Id)
                    powerContext.Cancel();
            }
        }

        // Called inside Vanilla under its engine monitor. Never read Noesis or change a bound ViewModel here.
        private static int NativePowerQuery(int site)
        {
            PowerSelection selected = pendingPower;
            if (powerContext?.Ready != true || selected == null || (site == 0 || site == 3) && !selectingTestPower) return 0;
            try
            {
                if (!ToolPolicy.PowerContextMatches(selected.Session, CurrentSessionId, selected.Actor, humanSelectedOwner,
                    selected.Generation, powerGeneration, IsCurrentSession(selected.Session) &&
                    GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled())) return site >= 2 ? 1 : 0;
                if (site < 2) return selected.Actor;
                // Atomically consume the native confirmation, retaining the captured AI/session for the queue.
                if (!ReferenceEquals(System.Threading.Interlocked.CompareExchange(ref pendingPower, null, selected), selected)) return 1;
                testActions.Enqueue(new TestAction { Kind = TestActionKind.Power, Actor = selected.Actor,
                    Session = selected.Session, PowerGeneration = selected.Generation, Power = selected.Power,
                    Tile = site == 3 ? -1 : powerContext.TargetTile,
                    Operation = System.Threading.Interlocked.Increment(ref nextTestAction) });
                return 1;
            }
            catch (Exception error)
            {
                pendingPower = null;
                log.LogError("TEST_POWER_CONTEXT_CALLBACK: " + error);
                return site >= 2 ? 1 : 0;
            }
        }

        // Observes clicks only for gates. Power input is never consumed or remapped by the mod.
        private static void ObserveToolMouse(ref int state, ref bool rightDown, ref bool rightUp) => toolMouseState = state;

        private static void CompleteToolMouse(int x, int y)
        {
            if (pendingPower == null && toolMouseState == 1 && MainControls.instance?.CurrentAction == 0 &&
                !EditorDirector.instance.overUI()) SelectTestGate(x, y);
            toolMouseState = 0;
        }

'''+s[b:]
s=s.replace('if (!current.Enabled || !ValidPowerTarget(current, action.Actor, action.Tile, out string reason))','if (!current.Enabled || (current.TargetKind == 1 && !GameTileManagerAPI.Instance.IsValidTileId(action.Tile)))')
s=s.replace('            toolsModel.RefreshPowerFlag(','''            if (pendingPower != null && (!IsCurrentSession(pendingPower.Session) || pendingPower.Actor != humanSelectedOwner ||
                !GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled())) CancelPowerSelection();
            else if (pendingPower != null && powerContext?.Ready == true && powerContext.Mode != 4 && powerContext.Mode != 5)
                pendingPower = null; // Native confirmation/cancellation finished; no synthetic key handling.
            toolsModel.RefreshPowerFlag(''')
p.write_text(s,encoding='utf-8')
p=root/'src/VanillaPowerToolbar.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('RestoreVanillaPowerToolbar(true); LogOnce("TEST_POWER_TOOLBAR_UNAVAILABLE: Vanilla toolbar contract changed."); return;', 'RestoreVanillaPowerToolbar(true); nextToolbarSearch = DateTime.UtcNow.AddMilliseconds(250); return;');s=s.replace('            if (pendingPower != null) powerToolbarLabel.Text += "\\n" + SerpLocalization.Get("Tools.PowerChooseTarget");\n','');p.write_text(s,encoding='utf-8')
p=root/'src/TestToolsNativeActions.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('                log.LogInfo("TEST_NATIVE_ACTIONS_READY', '''                try
                {
                    if (powerContext == null) powerContext = new NativePowerContext(context, NativePowerQuery, image);
                    log.LogInfo("TEST_POWER_CONTEXT_READY spans=105593/14,8CAF8/14,8CE20/18,105627/14; Vanilla targeting; explicit actor; permanent hooks");
                }
                catch (Exception error) { log.LogError("TEST_POWER_CONTEXT_DISABLED: " + error); }
                log.LogInfo("TEST_NATIVE_ACTIONS_READY''');p.write_text(s,encoding='utf-8')
p=root/'SpectatorEditorBuildTest.csproj';s=p.read_text(encoding='utf-8-sig');i=s.index('    <Reference Include="APIShared">');s=s[:i]+'''    <Reference Include="RedBird.Core"><HintPath>$(ExtenderDir)\\RedBird.Core.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="RedBird.X64"><HintPath>$(ExtenderDir)\\RedBird.X64.dll</HintPath><Private>false</Private></Reference>
'''+s[i:];p.write_text(s,encoding='utf-8')
for p in (root/'Locales').glob('*.txt'):
 s=p.read_text(encoding='utf-8-sig');s='\n'.join(line for line in s.splitlines() if not line.startswith(('Tools.PowerChooseTarget=','Tools.PowerCancelled=')))+'\n'
 s+='Tools.GateAutomatic='+('Automatische Torsteuerung aktiviert.' if p.name=='de-DE.txt' else 'Automatic gate control enabled.')+'\n'
 s+='Tools.GateFixed='+('Tor auf manuelle Steuerung gesetzt.' if p.name=='de-DE.txt' else 'Gate set to manual control.')+'\n'
 lines=s.splitlines()
 for i,line in enumerate(lines):
  if line.startswith('Tools.PowerHelp='): lines[i]='Tools.PowerHelp='+('Bedienung über die Vanilla-Kräfteleiste mit der eingestellten Spielsteuerung. Ausführung für die gewählte KI ohne Kraftkosten; normale Zielbeschränkungen gelten.' if p.name=='de-DE.txt' else 'Use the Vanilla powers toolbar and configured game controls. Executes for the selected AI without power costs; normal target restrictions apply.')
 p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
