using APIShared;
using RedBird.X64.Assembly;
using SHCDESE.Interop;

namespace GatehouseLivingCaptureTest
{
    internal static unsafe class CaptureDecision
    {
        // Never make a Vanilla-ineligible unit eligible. A failed lookup keeps Vanilla's result.
        internal static void Apply(X64SmartCPUContext* context, GameUnit* unit, bool active)
        {
            if (active && (context->R11 & 0xFF) != 0 && unit != null && !UnitAccess.IsReallyAlive(unit))
                context->R11 &= ~0xFFUL;
        }
    }
}
