using System;
using System.IO;
namespace BugfixesAndQoL.GateRouting
{
    internal static class MigrationSources
    {
        internal static string Read(string path)
        {
            string name=Path.GetFileName(path);
            string main=Path.Combine("src","GateRouting",name);
            string api=Path.Combine("..","APIShared","src","Pathfinding","GateRoutes","Native",name);
            if(name=="EnemyGatePathfindingTestPlugin.cs")return File.ReadAllText("src/GateRouting/GateRoutingFeature.cs");
            if(name=="AssassinGateDescentRuntime.cs" || name=="AssassinGateDescentRule.cs")return File.ReadAllText(Path.Combine("src","Assassins",name));
            // A DEVDIAG partial (*.DevDiag.cs) belongs to the same class; read both parts.
            if(File.Exists(main))return File.ReadAllText(main)+(File.Exists(Path.ChangeExtension(main,".DevDiag.cs"))?File.ReadAllText(Path.ChangeExtension(main,".DevDiag.cs")):"");
            // Development-only diagnosis moved to *.DevDiag.cs (SERP_DEVDIAG builds).
            string devDiag=Path.ChangeExtension(main,".DevDiag.cs");
            if(File.Exists(devDiag))return File.ReadAllText(devDiag);
            if(File.Exists(api))return File.ReadAllText(api);
            string normalized=path.Replace('\\','/');
            int apiSegment=normalized.IndexOf("APIShared/",StringComparison.Ordinal);
            if(apiSegment>=0)return File.ReadAllText(Path.Combine("..",normalized.Substring(apiSegment)));
            int mainSegment=normalized.IndexOf("BugfixesAndQoL/",StringComparison.Ordinal);
            if(mainSegment>=0)return File.ReadAllText(normalized.Substring(mainSegment+"BugfixesAndQoL/".Length));
            return File.ReadAllText(path);
        }
    }
}
