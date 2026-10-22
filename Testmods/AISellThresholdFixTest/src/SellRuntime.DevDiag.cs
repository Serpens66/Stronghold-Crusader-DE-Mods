using System;
using System.Runtime.InteropServices;
using R3;
using SHCDESE.EventAPI;

namespace AISellThresholdFixTest
{
    internal sealed unsafe partial class SellRuntime
    {
        private IDisposable subtractObserver;
        private int reports, lastTick;
        private long lastAttempts, lastBlocked;
        partial void DiagStart()
        {
            try
            {
                subtractObserver = PlayerR3EventHooks.OnPlayerSubtractResource.Observable.Subscribe(e =>
                {
                    try
                    {
                        if (e.Phase != EventHookPhase.Post || e.DontSubtract || Marshal.ReadInt32(state, 56) == 0 ||
                            e.PlayerId != Marshal.ReadInt32(state, 32) || (int)e.Good != Marshal.ReadInt32(state, 36) ||
                            e.Amount != Marshal.ReadInt32(state, 48)) return;
                        Marshal.WriteInt32(state, 56, 0);
                        int player = e.PlayerId, good = (int)e.Good, requested = Marshal.ReadInt32(state, 40);
                        int before = Marshal.ReadInt32(state, 44), limited = Marshal.ReadInt32(state, 48);
                        int stock = *(int*)(image + SellAssembly.GoodsBase + (uint)(player * SellAssembly.PlayerStride + good * 4));
                        int gold = *(int*)(image + SellAssembly.GoldBase + (uint)(player * SellAssembly.PlayerStride));
                        long goldDelta = (long)gold - Marshal.ReadInt32(state, 52);
                        bool balanced = before - (long)stock == limited;
                        Shared.DevDiag.Count("ai.sell.booked");
                        if (!balanced || reports++ < 12)
                            Shared.DevDiag.Log(() => $"ai.sell result player={player}, good={e.Good}, requested={requested}, limited={limited}, stockBefore={before}, stockAfter={stock}, goldDelta={goldDelta}, removalBalanced={balanced}; observed after resource subtraction, before native sale presentation.");
                    }
                    catch (Exception ex) { Shared.DevDiag.Log(() => "ai.sell observation failed: " + ex); }
                });
            }
            catch (Exception ex) { Shared.DevDiag.Log(() => "ai.sell observation unavailable; native fixes remain active: " + ex); }
        }
        partial void DiagTick(int tick)
        {
            if (tick == lastTick) return;
            lastTick = tick;
            long attempts = Marshal.ReadInt64(state, 16), blocked = Marshal.ReadInt64(state, 24);
            if (attempts == lastAttempts && blocked == lastBlocked) return;
            Shared.DevDiag.Count("ai.sell.attempts", attempts - lastAttempts);
            Shared.DevDiag.Count("ai.sell.emptyRejected", blocked - lastBlocked);
            lastAttempts = attempts; lastBlocked = blocked;
        }
    }
}
