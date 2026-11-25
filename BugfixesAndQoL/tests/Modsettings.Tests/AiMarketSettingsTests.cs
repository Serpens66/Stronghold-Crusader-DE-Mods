using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;

internal static partial class Program
{
    // Scoped production checks allow this feature to be validated independently
    // of unrelated option contracts. The ordinary full suite remains unchanged.
    private static void AiMarketSettingsTests(string workspace)
    {
        const string name = "EnableAiMarketOverbuyFix";
        PropertyInfo property = modelType.GetProperty(name);
        Check(property.GetCustomAttributesData().Any(a => a.AttributeType.Name == "SyncHostOnlyAttribute"),
            "market option is host synchronized");
        var reset = Instructions(modelType.GetMethod("ResetToDefault", All));
        Check(reset.Select((instruction, index) => new { instruction, index }).Any(item =>
            Equals(item.instruction.Item2, property.GetSetMethod()) && item.index > 0 &&
            reset[item.index - 1].Item1 == OpCodes.Ldc_I4_1), "market reset default is enabled");
        object presentation = Activator.CreateInstance(mod.GetType("BugfixesAndQoL.ModSettingsPresentation", true),
            All, null, new object[] { (Action)(() => { }) }, null);
        object option = ((IEnumerable)Get(presentation, "Options")).Cast<object>()
            .Single(o => (string)Get(o, "PropertyName") == name);
        Check(Get(option, "Category").ToString() == "Fixes" && (string)Get(option, "Symbol") == "★",
            "market fix is classified and recommended");
        var document = new XmlDocument();
        document.Load(Path.Combine(workspace, "BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml"));
        var editor = document.SelectNodes("//*").Cast<XmlNode>()
            .Single(n => n.Attributes?["IsChecked"]?.Value == "{Binding EnableAiMarketOverbuyFix, Mode=TwoWay}");
        XmlNode row = Ancestors(editor).First(a => SearchAttribute(a, "Key") != null);
        Check(SearchAttribute(row, "Key") == "bugfixes.enable-ai-market-overbuy-fix" &&
            row.ParentNode.Attributes?["Visibility"]?.Value == "{Binding SettingsPresentation.HostAi.FixesVisibility}" &&
            Ancestors(editor).Any(a => a.Attributes?["IsEnabled"]?.Value == "{Binding CanEditHostSettings}"),
            "market editor belongs to AI fixes with host authority and a unique search identity");
        Check(editor.Attributes?["ToolTipService.ShowDuration"]?.Value == "60000" &&
            editor.SelectSingleNode(".//*[local-name()='ToolTip' and @Content='{Binding EnableAiMarketOverbuyFixHelpText}']") != null,
            "market editor has its localized long tooltip");
        Type feature = mod.GetType("BugfixesAndQoL.AI.Market.MarketDemandFeature", true);
        Type owner = mod.GetType("BugfixesAndQoL.AI.Market.MarketPendingDemandFix", true);
        foreach (bool host in new[] { false, true }) foreach (bool client in new[] { false, true }) foreach (bool enabled in new[] { false, true })
        {
            object vm = Model(host, client, name, enabled);
            feature.GetMethod("ApplySettings", All).Invoke(null, new[] { vm });
            Check((int)owner.GetField("requested", All).GetValue(null) == (host && enabled ? 1 : 0),
                "production market activation uses host and option, independently of client activation");
        }
        feature.GetMethod("ApplySettings", All).Invoke(null, new object[] { null });
    }
}
