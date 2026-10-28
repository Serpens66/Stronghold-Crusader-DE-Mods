using BepInEx;
using SHCDESE.API.LowLevel;
namespace LordOverviewTest {
 [BepInPlugin("LordOverviewTest_Serp","Lord Overview Test","0.1.0")]
 [BepInDependency("000shcdese","2.14.0")]
 [BepInDependency("APIShared_Serp","0.18.0")]
 public sealed class Plugin:BaseUnityPlugin {
  private void Awake(){var log=Logger;CrusaderLibrary.Instance.LibraryLoaded+=context=>OverviewRuntime.Start(log);}
 }
}
