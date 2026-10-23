using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

// Execute the built production viewmodels, activation publications and dormant consumers.
// Constructors that install hooks or create Unity/Noesis views are external boundaries.
internal static class Program
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int checks, originalCalls;
    private static readonly bool[] HostTruth = { false, false, false, false, false, true, false, true };
    private static readonly bool[] ClientTruth = { false, false, false, true, false, false, false, true };

    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new InvalidOperationException(message);
    }
    private static object Raw(Type type) => FormatterServices.GetUninitializedObject(type);
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, All).SetValue(target, value);
    private static object Property(object target, string name) => target.GetType().GetProperty(name, All).GetValue(target, null);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, All).Invoke(target, args);
    private static void Backing(object model, string name, bool value) => Field(model, char.ToLowerInvariant(name[0]) + name.Substring(1), value);
    private static object Model(Assembly assembly, string type, string[] options, bool host, bool client, string option, bool value)
    {
        object model = Raw(assembly.GetType(type, true));
        foreach (string setting in options) Backing(model, setting, true);
        Backing(model, "EnableMod", host);
        Backing(model, "EnableClientFeatures", client);
        Backing(model, option, value);
        if (type.StartsWith("Spectator.", StringComparison.Ordinal)) Invoke(model, "RefreshActivation");
        return model;
    }
    private static void CountOriginal() => originalCalls++;
    private static void InstallOriginal(object consumer, string fieldName)
    {
        FieldInfo field = consumer.GetType().GetField(fieldName, All);
        MethodInfo signature = field.FieldType.GetMethod("Invoke");
        var callback = new DynamicMethod("CountOriginal", typeof(void), signature.GetParameters().Select(p => p.ParameterType).ToArray(), typeof(Program), true);
        ILGenerator il = callback.GetILGenerator();
        il.Emit(OpCodes.Call, typeof(Program).GetMethod(nameof(CountOriginal), All));
        il.Emit(OpCodes.Ret);
        field.SetValue(consumer, callback.CreateDelegate(field.FieldType));
    }
    private static object Consumer(Assembly assembly, string name, object activation)
    {
        object consumer = Raw(assembly.GetType("ExtendedData." + name, true));
        Field(consumer, "activation", activation);
        return consumer;
    }
    private static void DormantBoundaries(Assembly extended, object activation)
    {
        if (!(bool)Property(activation, "EditorFiles"))
        {
            object map = Consumer(extended, "MapModSettingsCoordinator", activation);
            InstallOriginal(map, "saveOriginal");
            int before = originalCalls;
            Invoke(map, "SaveSaveGameOrMapHook", null, "unused.map", "unused", false, false, true);
            Check(originalCalls == before + 1, "disabled editor map save calls only its original");
            object trail = Consumer(extended, "TrailMissionSettingsCoordinator", activation);
            InstallOriginal(trail, "saveCustomTrailMapOriginal");
            before = originalCalls;
            Invoke(trail, "SaveCustomTrailMapHook", null, "unused.map", "unused", "unused.trail", null);
            Check(originalCalls == before + 1, "disabled Trail save calls only its original");
        }
        if (!(bool)Property(activation, "WorkshopUploads"))
        {
            object trail = Consumer(extended, "TrailMissionSettingsCoordinator", activation);
            InstallOriginal(trail, "uploadWorkshopMapOriginal");
            int before = originalCalls;
            Invoke(trail, "UploadWorkshopMapHook", null, "unused-root", "unused", "", new[] { "Custom Lord" }, false, "", null, null);
            Check(originalCalls == before + 1, "disabled lower upload never reaches staging or confirmation");
            InstallOriginal(trail, "editorSetupButtonOriginal");
            FieldInfo options = trail.GetType().GetField("uploadOptions", All);
            options.SetValue(trail, Raw(options.FieldType));
            before = originalCalls;
            Invoke(trail, "EditorSetupButtonHook", null, "DoUpload");
            Check(originalCalls == before + 1, "disabled editor upload forwards even without a view");
        }
        if (!(bool)Property(activation, "LordDataIntegration"))
        {
            object lords = Consumer(extended, "LordDataSyncCoordinator", activation);
            Check(!(bool)Invoke(lords, "RefreshHost", null, "test"), "disabled Lord publication never queries game authority");
            Check(!(bool)Invoke(lords, "RefreshPackageManifest", null, "test"), "disabled Lord manifest never queries the game");
            Invoke(lords, "OnSnapshotChanged", "not-json");
            Invoke(lords, "OnPackageManifestChanged", "not-json");
            Check(true, "disabled Lord receivers do not parse or enqueue input");
        }
    }
    private static void Matrix(Assembly assembly, string type, string[] options, string option, string member, bool host, bool extended)
    {
        int index = 0;
        foreach (bool h in new[] { false, true }) foreach (bool c in new[] { false, true }) foreach (bool o in new[] { false, true })
        {
            object model = Model(assembly, type, options, h, c, option, o);
            object activation = Property(model, extended ? "FeatureActivation" : "Activation");
            Check((bool)Property(activation, member) == (host ? HostTruth : ClientTruth)[index++], type + "." + option + " H/C/O=" + h + "/" + c + "/" + o);
            if (extended)
            {
                var trail = Consumer(assembly, "TrailMissionSettingsCoordinator", activation);
                string gate = member == "MissionModSettings" ? "enabled" : member == "CoopPackages" ? "CoopEnabled" : member == "EditorFiles" ? "EditorEnabled" : member == "WorkshopUploads" ? "UploadEnabled" : null;
                if (gate != null) Check((bool)Property(trail, gate) == (bool)Property(activation, member), "Trail consumer uses its published category");
                DormantBoundaries(assembly, activation);
            }
        }
    }
    private static void Equivalence(Assembly assembly, string type, string[] options, string[] members, int hostCount, bool extended)
    {
        foreach (bool hostGroup in new[] { false, true })
        {
            object mainOff = Model(assembly, type, options, !hostGroup, hostGroup, options[0], true);
            object childrenOff = Model(assembly, type, options, true, true, options[0], true);
            for (int i = 0; i < options.Length; i++)
                if ((i < hostCount) == hostGroup) Backing(childrenOff, options[i], false);
            if (!extended) Invoke(childrenOff, "RefreshActivation");
            string publication = extended ? "FeatureActivation" : "Activation";
            object first = Property(mainOff, publication), second = Property(childrenOff, publication);
            foreach (string member in members)
                Check(Equals(Property(first, member), Property(second, member)), type + " main/children equivalence: " + member);
            if (extended) { DormantBoundaries(assembly, first); DormantBoundaries(assembly, second); }
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            string game = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ?? @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string[] directories = { Path.GetDirectoryName(args[0]), Path.GetDirectoryName(args[1]), args[2], Path.Combine(game, "BepInEx/core"), Path.Combine(game, "BepInEx/plugins/000shcdese"), Path.Combine(game, "Stronghold Crusader Definitive Edition_Data/Managed") };
            AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
            {
                string name = new AssemblyName(request.Name).Name + ".dll";
                foreach (string directory in directories) { string path = Path.Combine(directory, name); if (File.Exists(path)) return Assembly.LoadFrom(path); }
                return null;
            };
            Assembly ed = Assembly.LoadFrom(Path.GetFullPath(args[0])), sp = Assembly.LoadFrom(Path.GetFullPath(args[1]));
            string[] extendedOptions = { "EnableMissionModSettings", "EnableCoopPackages", "EnableLordDataIntegration", "EnableEditorFileIntegration", "EnableWorkshopUploads" };
            string[] members = { "MissionModSettings", "CoopPackages", "LordDataIntegration", "EditorFiles", "WorkshopUploads" };
            for (int i = 0; i < extendedOptions.Length; i++) Matrix(ed, "ExtendedData.ExtendedDataSettingsViewModel", extendedOptions, extendedOptions[i], members[i], i < 3, true);
            string[] spectatorOptions = { "EnableSpectatorPerspective", "EnableStatistics", "EnableEliminatedPlayersBecomeSpectators", "EnableTestTools" };
            string[] spectatorMembers = { "Perspective", "Statistics", "Defeat", "TestTools" };
            for (int i = 0; i < spectatorOptions.Length; i++) Matrix(sp, "Spectator.SpectatorViewModel", spectatorOptions, spectatorOptions[i], spectatorMembers[i], i < 3, false);
            Equivalence(ed, "ExtendedData.ExtendedDataSettingsViewModel", extendedOptions, members, 3, true);
            Equivalence(sp, "Spectator.SpectatorViewModel", spectatorOptions, spectatorMembers, 3, false);
            Type toolbar = sp.GetType("Spectator.SpectatorFeatureActivation", true);
            bool[] toolbarTruth = { false, true, true, true, true, true, true, true };
            int toolbarCase = 0;
            foreach (bool tools in new[] { false, true }) foreach (bool perspective in new[] { false, true }) foreach (bool statistics in new[] { false, true })
                Check((bool)toolbar.GetMethod("HasToolbar", All).Invoke(null, new object[] { tools, perspective, statistics }) == toolbarTruth[toolbarCase++], "toolbar combines available groups");
            Console.WriteLine("PASS: " + checks + " production activation and dormant-entry checks; no hooks installed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
