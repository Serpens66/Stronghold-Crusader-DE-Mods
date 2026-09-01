using MonoMod.RuntimeDetour;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using R3;
using System;
using System.Runtime.InteropServices;
using System.Reflection;
using InteropMarshal = System.Runtime.InteropServices.Marshal;
using static ExtremePowers.API.NativeExtremePowersSignatures;

namespace ExtremePowers.API
{
    internal sealed unsafe class NativeExtremePowersRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void DispatcherDelegate(IntPtr self, int playerId, int powerId, int targetTileId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void SelectionDelegate(int powerId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void HealDelegate(IntPtr manager, int targetTileId, int radius, int playerId, int amount);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void VolleyDelegate(IntPtr manager, int targetTileId, int radiusOrMode, int playerId, int strength, bool arrowMode);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GoldAdvanceDelegate(IntPtr cycleState);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void AudioDelegate(IntPtr audioManager, int soundId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ResourceUpdateDelegate(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int SpawnGroupDelegate(IntPtr manager, int mode, int adjustedTileId, int elevation, int playerId, int unitType, int count);
        private delegate void SetGameStateDelegate(GameData self, EngineInterface.PlayState state);
        private delegate void ExtremeHoverDelegate(MainViewModel self, object parameter);
        private delegate void ExtremeHudConstructorDelegate(HUD_ExtremePowers self);

        private readonly ExtremePowersApi owner;
        private readonly ulong moduleBase;
        private readonly NativeDetour dispatcherDetour;
        private readonly NativeDetour selectionDetour;
        private readonly NativeDetour resourceUpdateDetour;
        private readonly DispatcherDelegate rootedDispatcher;
        private readonly SelectionDelegate rootedSelection;
        private readonly DispatcherDelegate originalDispatcher;
        private readonly SelectionDelegate originalSelection;
        private readonly ResourceUpdateDelegate rootedResourceUpdate;
        private readonly ResourceUpdateDelegate originalResourceUpdate;
        private readonly HealDelegate heal;
        private readonly VolleyDelegate volley;
        private readonly GoldAdvanceDelegate advanceGoldCycle;
        private readonly AudioDelegate playAudio;
        private readonly SpawnGroupDelegate spawnGroup;
        private readonly IDisposable mapUnloadSubscription;
        private Hook hudHook;
        private SetGameStateDelegate originalSetGameState;
        private Hook hoverHook;
        private ExtremeHoverDelegate originalHover;
        private Hook extremeHudConstructorHook;
        private ExtremeHudConstructorDelegate originalExtremeHudConstructor;
        private readonly Button[] powerButtons = new Button[8];
        private readonly string[] appliedSpriteKeys = new string[8];
        private readonly RegenerationAccumulator[] regenerationAccumulators = CreateRegenerationAccumulators();
        private readonly uint[] resourceManaBefore = new uint[9];
        private bool unexpectedResourceDeltaLogged;
        private ulong nativeOperationSequence;
        private ushort mapEpoch = 1;

        internal NativeExtremePowersRuntime(ExtremePowersApi owner, IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (libraryHandle == IntPtr.Zero) throw new ArgumentException("The native library handle is missing.", nameof(libraryHandle));
            if (!NativeExtremePowersSignatures.MatchesMappedImage(memory, out string signatureError)) throw new InvalidOperationException(signatureError);
            moduleBase = unchecked((ulong)libraryHandle.ToInt64());
            heal = InteropMarshal.GetDelegateForFunctionPointer<HealDelegate>((IntPtr)(moduleBase + HealRva));
            volley = InteropMarshal.GetDelegateForFunctionPointer<VolleyDelegate>((IntPtr)(moduleBase + VolleyRva));
            advanceGoldCycle = InteropMarshal.GetDelegateForFunctionPointer<GoldAdvanceDelegate>((IntPtr)(moduleBase + GoldAdvanceRva));
            playAudio = InteropMarshal.GetDelegateForFunctionPointer<AudioDelegate>((IntPtr)(moduleBase + AudioRva));
            spawnGroup = InteropMarshal.GetDelegateForFunctionPointer<SpawnGroupDelegate>((IntPtr)(moduleBase + SpawnGroupRva));
            rootedDispatcher = Dispatch;
            rootedSelection = Select;
            rootedResourceUpdate = UpdateResources;

            dispatcherDetour = new NativeDetour((IntPtr)(moduleBase + DispatcherRva), InteropMarshal.GetFunctionPointerForDelegate(rootedDispatcher), new NativeDetourConfig { ManualApply = true });
            selectionDetour = new NativeDetour((IntPtr)(moduleBase + SelectionRva), InteropMarshal.GetFunctionPointerForDelegate(rootedSelection), new NativeDetourConfig { ManualApply = true });
            resourceUpdateDetour = new NativeDetour((IntPtr)(moduleBase + ResourceUpdateRva), InteropMarshal.GetFunctionPointerForDelegate(rootedResourceUpdate), new NativeDetourConfig { ManualApply = true });
            originalDispatcher = dispatcherDetour.GenerateTrampoline<DispatcherDelegate>();
            originalSelection = selectionDetour.GenerateTrampoline<SelectionDelegate>();
            originalResourceUpdate = resourceUpdateDetour.GenerateTrampoline<ResourceUpdateDelegate>();
            dispatcherDetour.Apply();
            try
            {
                selectionDetour.Apply();
                resourceUpdateDetour.Apply();
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
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Where(args => args.Phase == EventHookPhase.Post).Subscribe(OnUnloadMap);
            }
            catch
            {
                extremeHudConstructorHook?.Undo(); extremeHudConstructorHook?.Dispose();
                hoverHook?.Undo(); hoverHook?.Dispose();
                hudHook?.Undo(); hudHook?.Dispose();
                resourceUpdateDetour.Undo(); resourceUpdateDetour.Dispose();
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

        internal void RefreshHudReplacementState()
        {
            Array.Clear(appliedSpriteKeys, 0, appliedSpriteKeys.Length);
            UpdateHudReplacementState();
        }

        private void UpdateHudReplacementState()
        {
            bool ready = owner.GetSessionReadiness().Ready;
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
            if (owner.GetSessionReadiness().Ready && int.TryParse(parameter as string, out int powerId) && (uint)powerId <= 7 &&
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
            if (state == null || state.extremeEnabled <= 0 || !owner.GetSessionReadiness().Ready) return;
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
            ExtremePowersReadiness readiness = owner.GetSessionReadiness();
            if (!readiness.Ready) { owner.LogState("selection-fallback-" + powerId, "Selection Vanilla fallback power=" + powerId + ": " + readiness.Reason); originalSelection(powerId); return; }
            if ((uint)powerId > 7) { originalSelection(powerId); return; }
            ExtremePowersTuning tuning = owner.Snapshot();
            int selectedForVanilla = powerId;
            owner.TryGetReplacement((ExtremePowerId)powerId, out ExtremePowerReplacement replacement);
            if (powerId == (int)ExtremePowerId.Gold && replacement != null && replacement.TargetKind == ExtremePowerTargetKind.MapPoint)
                selectedForVanilla = (int)ExtremePowerId.ArrowVolley;

            int player = *(int*)(moduleBase + LocalPlayerRva);
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(player)) { originalSelection(powerId); return; }
            uint* mana = PlayerMana(player);
            uint before = *mana;
            int desiredCost = tuning.Costs[powerId];
            if (before > int.MaxValue) { owner.Log("Selection Vanilla fallback power=" + powerId + " player=" + player + ": mana exceeds the native signed range."); originalSelection(powerId); return; }
            if (before < desiredCost) return;
            if (ShouldQueueReplacementImmediately(replacement))
            {
                if (!owner.QueueReplacement((ExtremePowerId)powerId, player, ExtremePowerTarget.None, out string rejection))
                    owner.Log("Immediate replacement selection rejected power=" + powerId + " player=" + player + ": " + rejection);
                return;
            }
            int vanillaSelectionCost = (selectedForVanilla + 1) * 636;
            if (!ExtremePowerSafety.TryCompensateMana(before, desiredCost, vanillaSelectionCost, out uint compensated)) { owner.Log("Selection Vanilla fallback power=" + powerId + " player=" + player + ": mana compensation overflow."); originalSelection(powerId); return; }
            *mana = compensated;
            try { originalSelection(selectedForVanilla); }
            finally { *mana = before; }
            // Vanilla Arrow Volley supplies map targeting; restore the original power in the field consumed by target completion.
            if (selectedForVanilla != powerId) *(int*)(moduleBase + PendingTargetPowerRva) = powerId;
        }

        private void Dispatch(IntPtr self, int playerId, int powerId, int targetTileId)
        {
            ExtremePowersReadiness readiness = owner.GetSessionReadiness();
            if (!readiness.Ready) { owner.LogState("dispatcher-fallback-" + playerId + "-" + powerId, "Dispatcher Vanilla fallback power=" + powerId + " player=" + playerId + ": " + readiness.Reason); originalDispatcher(self, playerId, powerId, targetTileId); return; }
            if ((uint)powerId > 7) { originalDispatcher(self, playerId, powerId, targetTileId); return; }
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId)) return;
            ExtremePowersTuning tuning = owner.Snapshot();
            uint* mana = PlayerMana(playerId);
            int cost = tuning.Costs[powerId];
            if (*mana > int.MaxValue) { owner.Log("Dispatcher Vanilla fallback power=" + powerId + " player=" + playerId + ": mana exceeds the native signed range."); originalDispatcher(self, playerId, powerId, targetTileId); return; }
            if (*mana < cost) { owner.Log("Rejected power=" + powerId + " player=" + playerId + " mana=" + *mana + " cost=" + cost + "."); return; }

            ExtremePowerId power = (ExtremePowerId)powerId;
            if (owner.TryGetReplacement(power, out ExtremePowerReplacement replacement))
            {
                if (replacement.TargetKind == ExtremePowerTargetKind.Unit) { owner.Log("Rejected unit-target replacement because unit targeting is unavailable."); return; }
                ExtremePowerTarget target = replacement.TargetKind == ExtremePowerTargetKind.None ? ExtremePowerTarget.None : ExtremePowerTarget.MapPoint(targetTileId);
                if (!IsRuntimeTargetValid(target)) { owner.Log("Rejected replacement power=" + power + " player=" + playerId + ": invalid target."); return; }
                int tick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
                ulong operation = NextNativeOperationId(playerId);
                uint before = *mana;
                if (owner.TryExecuteReplacement(new ExtremePowerExecutionContext(power, playerId, target, operation, tick), out string rejection))
                {
                    *mana = before - (uint)cost;
                    owner.Log("Executed replacement power=" + power + " player=" + playerId + " target=" + target.Kind + " mana=" + before + " cost=" + cost + " operation=" + operation + ".");
                }
                else owner.Log("Rejected replacement power=" + power + " player=" + playerId + ": " + rejection);
                return;
            }

            if (TryExecuteTunedEffect(tuning, power, playerId, targetTileId))
            {
                *mana -= (uint)cost;
                owner.Log("Executed tuned power=" + power + " player=" + playerId + " mana=" + (*mana + (uint)cost) + " cost=" + cost + ".");
                return;
            }

            int vanillaCost = (powerId + 1) * 636;
            if (!ExtremePowerSafety.TryCompensateMana(*mana, cost, vanillaCost, out uint compensatedMana)) { owner.Log("Dispatcher Vanilla fallback power=" + powerId + " player=" + playerId + ": mana compensation overflow."); originalDispatcher(self, playerId, powerId, targetTileId); return; }
            *mana = compensatedMana;
            originalDispatcher(self, playerId, powerId, targetTileId);
        }

        private void UpdateResources(IntPtr self)
        {
            for (int player = 1; player <= 8; player++) resourceManaBefore[player] = *PlayerMana(player);
            originalResourceUpdate(self);
            if (!owner.GetSessionReadiness().Ready) return;
            int percent = owner.Snapshot().RegenerationPercent;
            if (percent == 100) return;
            for (int player = 1; player <= 8; player++)
            {
                uint* mana = PlayerMana(player);
                uint before = resourceManaBefore[player];
                uint after = *mana;
                if (after == before) continue;
                if (!regenerationAccumulators[player].TryScaleConfirmedIncrement(before, after, percent, ExtremePowerSafety.VanillaManaCap, out uint adjusted))
                {
                    if (!unexpectedResourceDeltaLogged) { unexpectedResourceDeltaLogged = true; owner.Log("Resource update produced a non-regeneration mana delta; leaving it unchanged (player=" + player + " before=" + before + " after=" + after + ")."); }
                    continue;
                }
                *mana = adjusted;
            }
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            for (int player = 1; player < regenerationAccumulators.Length; player++) regenerationAccumulators[player].Reset();
            Array.Clear(resourceManaBefore, 0, resourceManaBefore.Length);
            unexpectedResourceDeltaLogged = false;
            nativeOperationSequence = 0;
            unchecked { mapEpoch++; if (mapEpoch == 0) mapEpoch = 1; }
            // HUD_ExtremePowers survives normal map transitions; retain its live button references.
            Array.Clear(appliedSpriteKeys, 0, appliedSpriteKeys.Length);
        }

        private bool TryExecuteTunedEffect(ExtremePowersTuning tuning, ExtremePowerId power, int playerId, int tile)
        {
            switch (power)
            {
                case ExtremePowerId.ArrowVolley:
                    if (Same(tuning.ArrowVolley, owner.Vanilla.ArrowVolley)) return false;
                    volley((IntPtr)(moduleBase + EffectManagerRva), tile, tuning.ArrowVolley.Radius, playerId, tuning.ArrowVolley.Damage, tuning.ArrowVolley.ProjectileKind == ExtremePowerProjectileKind.Arrow); return true;
                case ExtremePowerId.Heal:
                    if (tuning.Heal.Amount == owner.Vanilla.Heal.Amount && tuning.Heal.Radius == owner.Vanilla.Heal.Radius) return false;
                    heal((IntPtr)(moduleBase + EffectManagerRva), tile, tuning.Heal.Radius, playerId, tuning.Heal.Amount); PlayLocalCompletionAudio(playerId, ExtremePowerId.Heal); return true;
                case ExtremePowerId.Spearmen: return SpawnIfChanged(tuning.Spearmen, owner.Vanilla.Spearmen, playerId, tile, power);
                case ExtremePowerId.Engineers: return SpawnIfChanged(tuning.Engineers, owner.Vanilla.Engineers, playerId, tile, power);
                case ExtremePowerId.Macemen: return SpawnIfChanged(tuning.Macemen, owner.Vanilla.Macemen, playerId, tile, power);
                case ExtremePowerId.Knights: return SpawnIfChanged(tuning.Knights, owner.Vanilla.Knights, playerId, tile, power);
                case ExtremePowerId.Gold:
                    if (tuning.Gold.Minimum == 1000 && tuning.Gold.Maximum == 2499) return false;
                    int cycle = *(short*)(moduleBase + GoldCycleRva);
                    long range = (long)tuning.Gold.Maximum - tuning.Gold.Minimum + 1;
                    uint* gold = (uint*)(moduleBase + GoldRva + (ulong)(playerId * PlayerStride));
                    *gold = ExtremePowerSafety.SaturatingAdd(*gold, (uint)((long)tuning.Gold.Minimum + PositiveModulo(cycle, range)));
                    advanceGoldCycle((IntPtr)(moduleBase + GoldCycleRva - 2)); return true;
                case ExtremePowerId.RockVolley:
                    if (Same(tuning.RockVolley, owner.Vanilla.RockVolley)) return false;
                    volley((IntPtr)(moduleBase + EffectManagerRva), tile, tuning.RockVolley.Radius, playerId, tuning.RockVolley.Damage, tuning.RockVolley.ProjectileKind == ExtremePowerProjectileKind.Arrow); PlayLocalCompletionAudio(playerId, ExtremePowerId.RockVolley); return true;
                default: return false;
            }
        }

        private bool SpawnIfChanged(SpawnConfiguration value, SpawnConfiguration vanilla, int playerId, int tile, ExtremePowerId power)
        {
            if (value.UnitType == vanilla.UnitType && value.Count == vanilla.Count) return false;
            ExtremePowerSpawnResult result = SpawnUnitGroup(playerId, tile, value.UnitType, value.Count);
            owner.Log("Spawn power player=" + playerId + " unitType=" + value.UnitType + " requested=" + value.Count + " spawned=" + result.SpawnedUnitCount + " groupId=" + result.GroupUnitId + ".");
            PlayLocalCompletionAudio(playerId, power);
            return true;
        }

        internal ExtremePowerSpawnResult SpawnUnitGroup(int ownerPlayerId, int tile, int unitType, int count)
        {
            if (!ExtremePowerSafety.IsValidSpawnOwnerPlayerId(ownerPlayerId) ||
                (ownerPlayerId != 0 && !GamePlayerManagerAPI.Instance.IsPlayerIdValid(ownerPlayerId)))
                throw new ArgumentOutOfRangeException(nameof(ownerPlayerId));
            if (!GameTileManagerAPI.Instance.IsValidTileId(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (!ExtremePowerSafety.IsSpawnableUnitType(unitType) || !Enum.IsDefined(typeof(eChimps), (ushort)unitType)) throw new ArgumentOutOfRangeException(nameof(unitType));
            if (count == 0) return new ExtremePowerSpawnResult(ownerPlayerId, 0, 0, 0);

            short elevation = *(short*)(moduleBase + TileHeightRva + (ulong)(tile * sizeof(short)));
            int rowOffset = *(int*)(moduleBase + TileRowOffsetRva + (ulong)(elevation * 3 * sizeof(int)));
            int adjustedTile = tile - rowOffset;
            int groupId = spawnGroup((IntPtr)(moduleBase + UnitManagerRva), 1, adjustedTile, elevation, ownerPlayerId, unitType, count);
            if (groupId > 0) *(ushort*)(moduleBase + UnitManagerRva + (ulong)(groupId * UnitStride + ExtremeSpawnStateOffset)) = 2;
            ulong groupAddress = moduleBase + UnitManagerRva + (ulong)(groupId * UnitStride);
            int actualCount = groupId > 0 ? ReadGroupMemberCount(new ReadOnlySpan<byte>((void*)groupAddress, GroupMemberCountOffset + sizeof(ushort))) : 0;
            return new ExtremePowerSpawnResult(ownerPlayerId, groupId, count, actualCount);
        }

        internal static int ReadGroupMemberCount(ReadOnlySpan<byte> groupRecord)
        {
            if (groupRecord.Length < GroupMemberCountOffset + sizeof(ushort)) throw new ArgumentException("The native group record is truncated.", nameof(groupRecord));
            return groupRecord[GroupMemberCountOffset] | (groupRecord[GroupMemberCountOffset + 1] << 8);
        }

        internal static int GetTunedEffectAudioId(ExtremePowerId power)
        {
            if (power == ExtremePowerId.Heal) return 0xCF;
            if (power == ExtremePowerId.Spearmen || power == ExtremePowerId.Engineers || power == ExtremePowerId.Macemen || power == ExtremePowerId.Knights) return 0x104;
            return power == ExtremePowerId.RockVolley ? 0x105 : 0;
        }

        internal static bool ShouldQueueReplacementImmediately(ExtremePowerReplacement replacement) => replacement != null && replacement.TargetKind == ExtremePowerTargetKind.None;
        internal static bool ShouldPlayTunedEffectAudio(ExtremePowerId power, int effectPlayerId, int localPlayerId) => effectPlayerId == localPlayerId && GetTunedEffectAudioId(power) != 0;

        private void PlayLocalCompletionAudio(int playerId, ExtremePowerId power)
        {
            int soundId = GetTunedEffectAudioId(power);
            if (ShouldPlayTunedEffectAudio(power, playerId, *(int*)(moduleBase + LocalPlayerRva))) playAudio((IntPtr)(moduleBase + AudioManagerRva), soundId);
        }

        private uint* PlayerMana(int playerId) => (uint*)(moduleBase + ManaRva + (ulong)(playerId * PlayerStride));
        private static bool IsRuntimeTargetValid(ExtremePowerTarget target)
        {
            if (!ExtremePowerTargetValidator.IsValid(target)) return false;
            if (target.Kind == ExtremePowerTargetKind.MapPoint) return GameTileManagerAPI.Instance.IsValidTileId(target.TileIndex);
            if (target.Kind == ExtremePowerTargetKind.Unit) return GameUnitManagerAPI.Instance.IsValid(target.UnitId);
            return target.Kind == ExtremePowerTargetKind.None;
        }
        private ulong NextNativeOperationId(int playerId)
        {
            nativeOperationSequence = (nativeOperationSequence + 1) & 0xFFFFFFFFFFUL;
            if (nativeOperationSequence == 0) nativeOperationSequence = 1;
            return ((ulong)mapEpoch << 48) | ((ulong)(byte)playerId << 40) | nativeOperationSequence;
        }
        private static RegenerationAccumulator[] CreateRegenerationAccumulators()
        {
            var values = new RegenerationAccumulator[9];
            for (int index = 0; index < values.Length; index++) values[index] = new RegenerationAccumulator();
            return values;
        }
        private static bool Same(VolleyConfiguration a, VolleyConfiguration b) => a.Damage == b.Damage && a.Radius == b.Radius && a.ProjectileKind == b.ProjectileKind;
        private static long PositiveModulo(long value, long divisor) { long result = value % divisor; return result < 0 ? result + divisor : result; }
    }
}
