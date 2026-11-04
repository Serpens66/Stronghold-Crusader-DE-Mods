using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

internal static partial class Program
{
    private static void PeaceTimeCivilianPolicyTests()
    {
        // These independent product truth tables target accidental client/SP-switch gates,
        // missing mission/peace readiness and legacy ExtraFeatures bypasses.
        for (int bits = 0; bits < 64; bits++)
        {
            bool host = (bits & 1) != 0, feature = (bits & 2) != 0, mission = (bits & 4) != 0,
                compatible = (bits & 8) != 0, available = (bits & 16) != 0, peace = (bits & 32) != 0;
            Check(BugfixesAndQoL.PeaceTimeCivilianProximityPolicy.ShouldLimit(
                host, feature, mission, compatible, available, peace) == (bits == 63),
                "peace cap requires all six authoritative prerequisites: " + bits);
        }
        bool[] coexist = { true, true, false, true };
        int index = 0;
        foreach (bool installed in new[] { false, true })
            foreach (bool registered in new[] { false, true })
                Check(BugfixesAndQoL.PeaceTimeCivilianProximityPolicy.CanCoexist(installed, registered) == coexist[index++],
                    "legacy EF direct subscriber is never combined with the shared cap");
    }

    private static void PeaceTimeCivilianCompiledTests(string workspace)
    {
        const string property = "EnablePeaceTimeCivilianProximityReduction";
        Type featureType = mod.GetType("BugfixesAndQoL.PeaceTimeCivilianProximityFeature", true);
        foreach (bool host in new[] { false, true }) foreach (bool option in new[] { false, true })
            foreach (bool spExtension in new[] { false, true }) foreach (bool client in new[] { false, true })
            {
                object vm = Model(host, client, property, option);
                Set(vm, "EnablePeaceTimeGameplayFixes", spExtension);
                Check(Bool(Feature("PeaceTimeCivilianProximityFeature", vm), "DesiredEnabled") == (host && option),
                    "peace civilian cap ignores client activation and the SP extension switch");
            }
        Check(featureType.GetMethods(All).All(m => m.Name != "Dispose" && m.Name != "OnDestroy" && m.Name != "Update"),
            "process-owned peace contributor has no component teardown or polling entry");
        object lockedModel = FormatterServices.GetUninitializedObject(modelType);
        Set(lockedModel, property, true);
        modelType.GetField("peaceTimeCivilianMissionLocked", All).SetValue(lockedModel, true);
        modelType.GetProperty(property).SetValue(lockedModel, false, null);
        Check((bool)modelType.GetProperty(property).GetValue(lockedModel, null),
            "local setter cannot change the peace option during an active mission");
        Check(!(bool)modelType.GetProperty("CanConfigurePeaceTimeCivilianProximity").GetValue(lockedModel, null),
            "active mission disables the bound peace editor");
        var xaml = new XmlDocument();
        xaml.Load(Path.Combine(workspace, "BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml"));
        XmlNode editor = xaml.SelectSingleNode("//*[local-name()='CheckBox' and contains(@IsChecked,'" + property + ",')]");
        Check(editor?.Attributes?["IsEnabled"]?.Value == "{Binding CanConfigurePeaceTimeCivilianProximity}",
            "UI uses the same mission-specific editing authority as the setter");
    }
}
