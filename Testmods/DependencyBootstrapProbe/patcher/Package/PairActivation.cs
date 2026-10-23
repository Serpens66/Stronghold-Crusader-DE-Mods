using System;
using System.Collections.Generic;
using System.Linq;

namespace DependencyBootstrapProbe
{
    internal static class PairActivation
    {
        private static PairContract prepared;
        internal static bool Bound { get; private set; }
        internal static bool AllowsCorrection(bool requested, bool bound) => requested && bound;
        private static string LoadedPath => typeof(PairActivation).Assembly.Location;
        internal static bool Prepare()
        {
            Bound = false;
            try
            {
                if (AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "DependencyBootstrapProbe.Patcher") != 1)
                    throw new InvalidOperationException("Multiple loaded bootstrap instances");
                prepared = PairContract.Read(BepInEx.Paths.GameRootPath, LoadedPath, ProbeIdentity.Hash(LoadedPath));
                PairDiagnostics.Mark("PAIR_PREPARED", "manifest=" + prepared.ManifestHash);
                return true;
            }
            catch (Exception ex) { prepared = null; PairDiagnostics.Reject(ex); return false; }
        }
        internal static void Bind(IEnumerable<ShadowPlugin> winners)
        {
            Bound = false;
            try
            {
                if (prepared == null) throw new InvalidOperationException("No prepared package");
                if (AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "DependencyBootstrapProbe.Patcher") != 1)
                    throw new InvalidOperationException("Multiple loaded bootstrap instances");
                prepared.Bind(winners, LoadedPath, ProbeIdentity.Hash(LoadedPath));
                Bound = true;
                PairDiagnostics.Mark("PAIR_BOUND", "guid=" + PairContract.Guid + " version=" + prepared.Version + " runtime=" + prepared.RuntimeHash);
            }
            catch (Exception ex) { PairDiagnostics.Reject(ex); }
        }
    }
}
