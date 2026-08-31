using MonoMod.RuntimeDetour;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using R3;
using System;
using System.Runtime.InteropServices;
using System.Reflection;
using InteropMarshal = System.Runtime.InteropServices.Marshal;

namespace ExtremePowers.API
{
    internal sealed unsafe class NativeExtremePowersRuntime
    {
        private const int DispatcherRva = 0xCD630;
        private const int SelectionRva = 0x105510;
        private const int HealRva = 0xE1E70;
        private const int VolleyRva = 0xDD6C0;
        private const int GoldAdvanceRva = 0x7530;
        private const int SelectedPowerRva = 0x366A0C4;
        private const int ManaRva = 0x379E7A4;
        private const int GoldRva = 0x379E7A8;
        private const int GoldCycleRva = 0x856A6D2;
        private const int PlayerStride = 0x583C;
        private static readonly byte[] DispatcherSignature = { 0x48,0x89,0x5C,0x24,0x10,0x48,0x89,0x6C,0x24,0x18,0x48,0x89,0x74,0x24,0x20,0x57,0x48,0x83,0xEC,0x40 };
        private static readonly byte[] SelectionSignature = { 0x40,0x53,0x48,0x83,0xEC,0x20,0x8B,0x05 };
        private static readonly byte[] HealSignature = { 0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x10,0x48,0x89,0x74,0x24,0x18,0x48 };
        private static readonly byte[] VolleySignature = { 0x44,0x89,0x4C,0x24,0x20,0x44,0x89,0x44,0x24,0x18,0x48,0x89,0x4C,0x24,0x08,0x53 };
        private static readonly byte[] GoldAdvanceSignature = { 0x4C,0x63,0x81,0x48,0x9C,0x00,0x00,0x33,0xC0,0x42,0x0F,0xB7,0x54,0x41,0x08,0x41 };

        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void DispatcherDelegate(IntPtr self, int playerId, int powerId, int targetTileId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void SelectionDelegate(int powerId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void HealDelegate(IntPtr manager, int targetTileId, int radius, int playerId, int amount);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void VolleyDelegate(IntPtr manager, int targetTileId, int radiusOrMode, int playerId, int strength, bool arrowMode);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GoldAdvanceDelegate(IntPtr cycleState);
        private delegate void SetGameStateDelegate(GameData self, EngineInterface.PlayState state);
        private delegate void ExtremeHoverDelegate(MainViewModel self, object parameter);
        private delegate void ExtremeHudConstructorDelegate(HUD_ExtremePowers self);

        private readonly ExtremePowersApi owner;
        private readonly ulong moduleBase;
        private readonly NativeDetour dispatcherDetour;
        private readonly NativeDetour selectionDetour;
        private readonly DispatcherDelegate rootedDispatcher;
        private readonly SelectionDelegate rootedSelection;
        private readonly DispatcherDelegate originalDispatcher;
        private readonly SelectionDelegate originalSelection;
        private readonly HealDelegate heal;
        private readonly VolleyDelegate volley;
        private readonly GoldAdvanceDelegate advanceGoldCycle;
        private readonly IDisposable mapUnloadSubscription;
        private Hook hudHook;
        private SetGameStateDelegate originalSetGameState;
        private Hook hoverHook;
        private ExtremeHoverDelegate originalHover;
        private Hook extremeHudConstructorHook;
        private ExtremeHudConstructorDelegate originalExtremeHudConstructor;
        private readonly Button[] powerButtons = new Button[8];
        private readonly string[] appliedSpriteKeys = new string[8];
        private readonly uint[] trackedMana = new uint[9];
        private readonly bool[] hasTrackedMana = new bool[9];
        private readonly long[] regenerationRemainder = new long[9];
        private int previousTick = -1;

        internal NativeExtremePowersRuntime(ExtremePowersApi owner, IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (libraryHandle == IntPtr.Zero) throw new ArgumentException("The native library handle is missing.", nameof(libraryHandle));
            RequireSignature(memory, DispatcherRva, DispatcherSignature, "dispatcher");
            RequireSignature(memory, SelectionRva, SelectionSignature, "selection");
            RequireSignature(memory, HealRva, HealSignature, "heal effect");
            RequireSignature(memory, VolleyRva, VolleySignature, "volley effect");
            RequireSignature(memory, GoldAdvanceRva, GoldAdvanceSignature, "gold cycle");
            moduleBase = unchecked((ulong)libraryHandle.ToInt64());
            heal = InteropMarshal.GetDelegateForFunctionPointer<HealDelegate>((IntPtr)(moduleBase + HealRva));
            volley = InteropMarshal.GetDelegateForFunctionPointer<VolleyDelegate>((IntPtr)(moduleBase + VolleyRva));
            advanceGoldCycle = InteropMarshal.GetDelegateForFunctionPointer<GoldAdvanceDelegate>((IntPtr)(moduleBase + GoldAdvanceRva));
            rootedDispatcher = Dispatch;
            rootedSelection = Select;

            dispatcherDetour = new NativeDetour((IntPtr)(moduleBase + DispatcherRva), InteropMarshal.GetFunctionPointerForDelegate(rootedDispatcher), new NativeDetourConfig { ManualApply = true });
            selectionDetour = new NativeDetour((IntPtr)(moduleBase + SelectionRva), InteropMarshal.GetFunctionPointerForDelegate(rootedSelection), new NativeDetourConfig { ManualApply = true });
            originalDispatcher = dispatcherDetour.GenerateTrampoline<DispatcherDelegate>();
            originalSelection = selectionDetour.GenerateTrampoline<SelectionDelegate>();
            dispatcherDetour.Apply();
            try
            {
                selectionDetour.Apply();
                MethodInfo setGameState = typeof(GameData).GetMethod("setGameState", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(EngineInterface.PlayState) }, null)
                    ?? throw new MissingMethodException(typeof(GameData).FullName, "setGameState");
                hudHook = new Hook(setGameState, (SetGameStateDelegate)SetGameState);
                originalSetGameState = hudHook.GenerateTrampoline<SetGameStateDelegate>();
                MethodInfo hover = typeof(MainViewModel).GetMethod("ButtonExtremeEnter", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object) }, null)
                    ?? throw new MissingMethodException(typeof(MainViewModel).FullName, "ButtonExtremeEnter");
                hoverHook = new Hook(hover, (ExtremeHoverDelegate)Hover);
                originalHover = hoverHook.GenerateTrampoline<ExtremeHoverDelegate>();
                ConstructorInfo hudConstructor = typeof(HUD_ExtremePowers).GetConstructor(Type.EmptyTypes)
                    ?? throw new MissingMethodException(typeof(HUD_ExtremePowers).FullName, ".ctor()");
                extremeHudConstructorHook = new Hook(hudConstructor, (ExtremeHudConstructorDelegate)ConstructExtremeHud);
                originalExtremeHudConstructor = extremeHudConstructorHook.GenerateTrampoline<ExtremeHudConstructorDelegate>();
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Where(args => args.Phase == EventHookPhase.Post).Subscribe(OnUnloadMap);
            }
            catch
            {
                extremeHudConstructorHook?.Undo(); extremeHudConstructorHook?.Dispose();
                hoverHook?.Undo(); hoverHook?.Dispose();
                hudHook?.Undo(); hudHook?.Dispose();
                selectionDetour.Undo(); selectionDetour.Dispose();
                dispatcherDetour.Undo(); dispatcherDetour.Dispose();
                throw;
            }
        }

        private void ConstructExtremeHud(HUD_ExtremePowers self)
        {
            originalExtremeHudConstructor(self);
            for (int powerId = 0; powerId < powerButtons.Length; powerId++) powerButtons[powerId] = self.FindName("ExtremePowersButton" + powerId) as Button;
            UpdateHudReplacementState();
        }

        private void UpdateHudReplacementState()
        {
            bool ready = owner.IsSynchronizedSessionReady();
            for (int powerId = 0; powerId < powerButtons.Length; powerId++)
            {
                Button button = powerButtons[powerId];
                if (button == null) continue;
                string key = "extreme power " + (powerId + 1);
                if (ready && owner.TryGetReplacement((ExtremePowerId)powerId, out ExtremePowerReplacement replacement) && !string.IsNullOrWhiteSpace(replacement.Sprite)) key = replacement.Sprite.Trim();
                if (string.Equals(appliedSpriteKeys[powerId], key, StringComparison.Ordinal)) continue;
                ImageSource a = button.TryFindResource(key + "a") as ImageSource;
                ImageSource b = button.TryFindResource(key + "b") as ImageSource;
                ImageSource c = button.TryFindResource(key + "c") as ImageSource;
                if (a == null || b == null || c == null) { key = "extreme power " + (powerId + 1); a = button.FindResource(key + "a") as ImageSource; b = button.FindResource(key + "b") as ImageSource; c = button.FindResource(key + "c") as ImageSource; }
                PropEx.SetSprite1(button, a); PropEx.SetSprite2(button, c); PropEx.SetSprite3(button, c); PropEx.SetSprite4(button, b);
                appliedSpriteKeys[powerId] = key;
            }
        }

        private void Hover(MainViewModel self, object parameter)
        {
            if (owner.IsSynchronizedSessionReady() && int.TryParse(parameter as string, out int powerId) && (uint)powerId <= 7 &&
                owner.TryGetReplacement((ExtremePowerId)powerId, out ExtremePowerReplacement replacement))
            {
                self.HUDmain.SetRolloverOtherString(replacement.Name + (string.IsNullOrWhiteSpace(replacement.Tooltip) ? string.Empty : "\n" + replacement.Tooltip));
                return;
            }
            originalHover(self, parameter);
        }

        private void SetGameState(GameData self, EngineInterface.PlayState state)
        {
            originalSetGameState(self, state);
            UpdateHudReplacementState();
            if (state == null || state.extremeEnabled <= 0 || !owner.IsSynchronizedSessionReady()) return;
            int[] costs = owner.Snapshot().Costs;
            string[] enabled = new string[8];
            for (int index = 0; index < enabled.Length; index++) enabled[index] = state.extremeCount >= costs[index] ? "True" : "False";
            MainViewModel.Instance.ExtremePower1_Enabled = enabled[0]; MainViewModel.Instance.ExtremePower2_Enabled = enabled[1];
            MainViewModel.Instance.ExtremePower3_Enabled = enabled[2]; MainViewModel.Instance.ExtremePower4_Enabled = enabled[3];
            MainViewModel.Instance.ExtremePower5_Enabled = enabled[4]; MainViewModel.Instance.ExtremePower6_Enabled = enabled[5];
            MainViewModel.Instance.ExtremePower7_Enabled = enabled[6]; MainViewModel.Instance.ExtremePower8_Enabled = enabled[7];
        }

        private void Select(int powerId)
        {
            if (!owner.IsSynchronizedSessionReady()) { originalSelection(powerId); return; }
            if ((uint)powerId > 7) { originalSelection(powerId); return; }
            ExtremePowersTuning tuning = owner.Snapshot();
            int selectedForVanilla = powerId;
            if (powerId == (int)ExtremePowerId.Gold && owner.TryGetReplacement(ExtremePowerId.Gold, out ExtremePowerReplacement replacement) && replacement.TargetKind == ExtremePowerTargetKind.MapPoint)
                selectedForVanilla = (int)ExtremePowerId.ArrowVolley;

            int player = *(int*)(moduleBase + 0x88E3D70);
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(player)) { originalSelection(powerId); return; }
            uint* mana = PlayerMana(player);
            uint before = *mana;
            int desiredCost = tuning.Costs[powerId];
            int vanillaSelectionCost = (selectedForVanilla + 1) * 636;
            if (before < desiredCost) return;
            *mana = checked((uint)((long)before + vanillaSelectionCost - desiredCost));
            try { originalSelection(selectedForVanilla); }
            finally { *mana = before; }
            if (selectedForVanilla != powerId) *(int*)(moduleBase + SelectedPowerRva) = powerId;
        }

        private void Dispatch(IntPtr self, int playerId, int powerId, int targetTileId)
        {
            if (!owner.IsSynchronizedSessionReady()) { originalDispatcher(self, playerId, powerId, targetTileId); return; }
            if ((uint)powerId > 7) { originalDispatcher(self, playerId, powerId, targetTileId); return; }
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId)) return;
            ExtremePowersTuning tuning = owner.Snapshot();
            uint* mana = PlayerMana(playerId);
            int cost = tuning.Costs[powerId];
            if (*mana < cost) { Track(playerId, *mana); return; }

            ExtremePowerId power = (ExtremePowerId)powerId;
            if (owner.TryGetReplacement(power, out ExtremePowerReplacement replacement))
            {
                ExtremePowerTarget target = replacement.TargetKind == ExtremePowerTargetKind.None ? ExtremePowerTarget.None : ExtremePowerTarget.MapPoint(targetTileId);
                if (!IsRuntimeTargetValid(target)) { Track(playerId, *mana); return; }
                int tick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
                ulong operation = ((ulong)(uint)tick << 32) | ((ulong)(byte)playerId << 8) | (byte)powerId;
                if (owner.TryExecuteReplacement(new ExtremePowerExecutionContext(power, playerId, target, operation, tick), out _)) *mana -= (uint)cost;
                Track(playerId, *mana);
                return;
            }

            if (TryExecuteTunedEffect(tuning, power, playerId, targetTileId))
            {
                *mana -= (uint)cost;
                Track(playerId, *mana);
                return;
            }

            int vanillaCost = (powerId + 1) * 636;
            *mana = checked((uint)((long)*mana + vanillaCost - cost));
            originalDispatcher(self, playerId, powerId, targetTileId);
            Track(playerId, *mana);
        }

        private void OnTick(int tick)
        {
            if (!owner.IsSynchronizedSessionReady()) { Array.Clear(hasTrackedMana, 0, hasTrackedMana.Length); return; }
            if (tick < previousTick)
            {
                Array.Clear(hasTrackedMana, 0, hasTrackedMana.Length);
                Array.Clear(regenerationRemainder, 0, regenerationRemainder.Length);
            }
            previousTick = tick;
            int percent = owner.Snapshot().RegenerationPercent;
            for (int player = 1; player <= 8; player++)
            {
                uint* mana = PlayerMana(player);
                uint current = *mana;
                if (!hasTrackedMana[player] || current < trackedMana[player]) { Track(player, current); continue; }
                uint vanillaIncrease = current - trackedMana[player];
                if (vanillaIncrease == 0) continue;
                long scaled = checked((long)vanillaIncrease * percent + regenerationRemainder[player]);
                long desiredIncrease = scaled / 100;
                regenerationRemainder[player] = scaled % 100;
                long adjusted = checked((long)trackedMana[player] + desiredIncrease);
                *mana = adjusted > uint.MaxValue ? uint.MaxValue : (uint)adjusted;
                Track(player, *mana);
            }
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            previousTick = -1;
            Array.Clear(hasTrackedMana, 0, hasTrackedMana.Length);
            Array.Clear(trackedMana, 0, trackedMana.Length);
            Array.Clear(regenerationRemainder, 0, regenerationRemainder.Length);
            Array.Clear(powerButtons, 0, powerButtons.Length);
            Array.Clear(appliedSpriteKeys, 0, appliedSpriteKeys.Length);
        }

        private bool TryExecuteTunedEffect(ExtremePowersTuning tuning, ExtremePowerId power, int playerId, int tile)
        {
            switch (power)
            {
                case ExtremePowerId.ArrowVolley:
                    if (Same(tuning.ArrowVolley, owner.Vanilla.ArrowVolley)) return false;
                    volley((IntPtr)(moduleBase + 0x60AD660), tile, tuning.ArrowVolley.Radius, playerId, tuning.ArrowVolley.Damage, tuning.ArrowVolley.ProjectileMode != 0); return true;
                case ExtremePowerId.Heal:
                    if (tuning.Heal.Amount == owner.Vanilla.Heal.Amount && tuning.Heal.Radius == owner.Vanilla.Heal.Radius) return false;
                    heal((IntPtr)(moduleBase + 0x60AD660), tile, tuning.Heal.Radius, playerId, tuning.Heal.Amount); return true;
                case ExtremePowerId.Spearmen: return SpawnIfChanged(tuning.Spearmen, owner.Vanilla.Spearmen, playerId, tile);
                case ExtremePowerId.Engineers: return SpawnIfChanged(tuning.Engineers, owner.Vanilla.Engineers, playerId, tile);
                case ExtremePowerId.Macemen: return SpawnIfChanged(tuning.Macemen, owner.Vanilla.Macemen, playerId, tile);
                case ExtremePowerId.Knights: return SpawnIfChanged(tuning.Knights, owner.Vanilla.Knights, playerId, tile);
                case ExtremePowerId.Gold:
                    if (tuning.Gold.Minimum == 1000 && tuning.Gold.Maximum == 2499) return false;
                    int cycle = *(short*)(moduleBase + GoldCycleRva);
                    int range = checked(tuning.Gold.Maximum - tuning.Gold.Minimum + 1);
                    *(uint*)(moduleBase + GoldRva + (ulong)(playerId * PlayerStride)) += (uint)(tuning.Gold.Minimum + PositiveModulo(cycle, range));
                    advanceGoldCycle((IntPtr)(moduleBase + GoldCycleRva - 2)); return true;
                case ExtremePowerId.RockVolley:
                    if (Same(tuning.RockVolley, owner.Vanilla.RockVolley)) return false;
                    volley((IntPtr)(moduleBase + 0x60AD660), tile, tuning.RockVolley.Radius, playerId, tuning.RockVolley.Damage, tuning.RockVolley.ProjectileMode != 0); return true;
                default: return false;
            }
        }

        private bool SpawnIfChanged(SpawnConfiguration value, SpawnConfiguration vanilla, int playerId, int tile)
        {
            if (value.UnitType == vanilla.UnitType && value.Count == vanilla.Count) return false;
            if (!GameTileManagerAPI.Instance.IsValidTileId(tile)) return true;
            UnmanagedVector2<ushort> point = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            for (int index = 0; index < value.Count; index++)
                if (GameUnitManagerAPI.Instance.CreateUnitLocal(playerId, playerId, point.X, point.Y, 0, (eChimps)value.UnitType) <= 0) break;
            return true;
        }

        private uint* PlayerMana(int playerId) => (uint*)(moduleBase + ManaRva + (ulong)(playerId * PlayerStride));
        private static bool IsRuntimeTargetValid(ExtremePowerTarget target)
        {
            if (!ExtremePowerTargetValidator.IsValid(target)) return false;
            if (target.Kind == ExtremePowerTargetKind.MapPoint) return GameTileManagerAPI.Instance.IsValidTileId(target.TileIndex);
            if (target.Kind == ExtremePowerTargetKind.Unit) return GameUnitManagerAPI.Instance.IsValid(target.UnitId);
            return target.Kind == ExtremePowerTargetKind.None;
        }
        private void Track(int playerId, uint mana) { if ((uint)playerId >= (uint)hasTrackedMana.Length) return; trackedMana[playerId] = mana; hasTrackedMana[playerId] = true; }
        private static bool Same(VolleyConfiguration a, VolleyConfiguration b) => a.Damage == b.Damage && a.Radius == b.Radius && a.ProjectileMode == b.ProjectileMode;
        private static int PositiveModulo(int value, int divisor) { int result = value % divisor; return result < 0 ? result + divisor : result; }
        private static void RequireSignature(ReadOnlySpan<byte> memory, int rva, byte[] expected, string name)
        {
            if (rva < 0 || rva + expected.Length > memory.Length) throw new InvalidOperationException(name + " RVA is outside the mapped library image.");
            for (int i = 0; i < expected.Length; i++) if (memory[rva + i] != expected[i]) throw new InvalidOperationException(name + " signature mismatch at RVA 0x" + rva.ToString("X") + ".");
        }
    }
}
