using BepInEx;
using SHCDESE.API.LowLevel;

namespace EnemyBridgePathTest
{
    // In-game acceptance of Pathfinding's enemy drawbridge closure. The feature itself lives in
    // Pathfinding (setting EnableEnemyDrawbridgeClosure); this mod only adds owned automation
    // scenarios and a read-only deck inspector. Without an owned SerpAutomation request it does
    // nothing. The inspector is rooted by the static field; there is no teardown path.
    [BepInPlugin("EnemyBridgePathTest_Serp", "Enemy Bridge Path Test", "0.2.0")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.49.0")]
    [BepInDependency("Pathfinding_Serp", "0.1.10")]
    public sealed class EnemyBridgePathTestPlugin : BaseUnityPlugin
    {
        private static DeckInspector inspector;

        private void Awake()
        {
            if (inspector != null) return;
            var log = Logger;
            inspector = new DeckInspector();
            CrusaderLibrary.Instance.LibraryLoaded += context =>
            {
                inspector.Attach(context);
                if (!inspector.Ready) Shared.DebugLogHelper.LogError(log, "Enemy bridge deck inspector inactive: " + inspector.Error);
            };
            BridgeAcceptance.TryStart(log, inspector);
            Shared.DebugLogHelper.LogInfo(log, "EnemyBridgePathTest registered: acceptance scenarios for Pathfinding's drawbridge closure.");
        }
    }
}
