// TEMP_GATE_ROUTE_ACCEPTANCE: isolated observer for the production handoff tests.
namespace APIShared.UnitCommands
{
    internal static class UnitCommandPathRuntime
    {
        internal static void ReportTemporaryAssassinStage(string stage, string result, string detail)
        {
            if (APIShared.TemporaryGateRouteAcceptanceBridge.Current is APIShared.ITemporaryAssassinGateObserver observer)
                try { observer.ObserveAssassinStage(null, 0, stage, result, detail); }
                catch (System.Exception error) { APIShared.TemporaryGateRouteAcceptanceBridge.ReportFailure(stage, error); }
        }
    }
}
