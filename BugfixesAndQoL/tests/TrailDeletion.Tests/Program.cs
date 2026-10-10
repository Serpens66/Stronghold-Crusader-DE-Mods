using System;
using System.Collections.ObjectModel;
using System.IO;
using BugfixesAndQoL;
using CrusaderDE;
using Noesis;
using Path = System.IO.Path;

public static class TrailDeletionTests
{
    private static int checks;
    private static void Assert(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
    public static string Run(string fixture)
    {
        string root = Path.Combine(fixture, "CustomTrails");
        Directory.CreateDirectory(root);
        ConfigSettings.Root = root;
        string local = Path.Combine(root, "own");
        Directory.CreateDirectory(local);
        File.WriteAllText(Path.Combine(local, "01.trail"), "mission");
        File.WriteAllText(Path.Combine(local, "own.data"), "uploaded own trail");
        string coop = Path.Combine(root, "coop");
        Directory.CreateDirectory(Path.Combine(coop, "TrailMaker"));
        File.WriteAllText(Path.Combine(coop, "cooptrail.json"), "manifest");
        File.WriteAllText(Path.Combine(coop, "TrailMaker", "01.trail"), "source");
        string workshop = Path.Combine(fixture, "Subscribed", "own");
        Directory.CreateDirectory(workshop);
        string maker = Path.Combine(fixture, "TrailMaker");
        Directory.CreateDirectory(maker);
        File.WriteAllText(Path.Combine(maker, "01.trail"), "keep editor mission");
        string resolved;
        Assert(TrailDeletionPolicy.TryResolve("own", local, false, root, out resolved), "own uploaded Trail");
        Assert(!TrailDeletionPolicy.TryResolve("own", local, true, root, out resolved), "Workshop flag overrides local-looking path");
        Assert(!TrailDeletionPolicy.TryResolve("own", workshop, false, root, out resolved), "outside folder rejected even without Workshop flag");
        Assert(!TrailDeletionPolicy.TryResolve("..", root, false, root, out resolved), "parent traversal");
        Assert(!TrailDeletionPolicy.TryResolve("CustomTrails", root, false, root, out resolved), "root cannot be deleted");
        Assert(!TrailDeletionPolicy.TryResolve("other", local, false, root, out resolved), "name/path mismatch");
        Assert(!TrailDeletionPolicy.TryResolve("01.trail", Path.Combine(local, "01.trail"), false, root, out resolved), "file and nested path rejected");
        var settings = new BugfixesAndQoLViewModel();
        var vm = new TrailDeletionViewModel(new BepInEx.Logging.ManualLogSource(), settings);
        var ownInfo = new MapFileManager.CustomTrailInfo { Name = "own", FullPath = local };
        MapFileManager.Instance.Trails.Add(ownInfo);
        var own = new FileRow { Text1 = "own" };
        var subscribed = new FileRow { Text1 = "own", trail = new MapFileManager.CustomTrailInfo { Name = "own", FullPath = workshop, workshop = true } };
        var coopRow = new FileRow { Text1 = "coop", trail = new MapFileManager.CustomTrailInfo { Name = "coop", FullPath = coop } };
        var rows = new ObservableCollection<FileRow> { own, subscribed, coopRow };
        var list = new ListView { ItemsSource = rows };
        vm.Attach(list);
        Assert(!vm.DeleteTrailEnabled, "no selection");
        list.SelectedItem = subscribed;
        Assert(!vm.DeleteTrailEnabled, "subscribed name collision");
        list.SelectedItem = new FileRow { Text1 = "unknown" };
        Assert(!vm.DeleteTrailEnabled, "unknown origin");
        list.SelectedItem = own;
        Assert(vm.DeleteTrailEnabled, "Vanilla row resolved through catalog");
        settings.ShowLoadSaveDialogControls = false;
        Assert(!vm.DeleteTrailEnabled && vm.BackupAlignment == HorizontalAlignment.Center, "controls off restores layout");
        settings.ShowLoadSaveDialogControls = true;
        settings.EnableMod = false;
        Assert(vm.DeleteTrailVisibility == Visibility.Collapsed && !vm.DeleteTrailEnabled, "mod off");
        settings.EnableMod = true;
        vm.DeleteTrailCommand.Execute();
        Assert(HUD_ConfirmationPopup.MP, "confirmation uses MP overlay");
        HUD_ConfirmationPopup.No();
        Assert(Directory.Exists(local), "cancel keeps folder");
        vm.DeleteTrailCommand.Execute();
        list.SelectedItem = subscribed;
        HUD_ConfirmationPopup.Yes();
        Assert(!Directory.Exists(local) && Directory.Exists(workshop), "selection switch deletes only confirmed local target");
        Assert(!rows.Contains(own) && rows.Contains(subscribed) && rows.Contains(coopRow), "remaining rows preserved");
        Assert(list.SelectedItem == null && !FRONT_ManageTrail.Instance.Action.IsEnabled, "selection and import cleared");
        list.SelectedItem = coopRow;
        vm.DeleteTrailCommand.Execute();
        settings.EnableMod = false;
        HUD_ConfirmationPopup.Yes();
        Assert(Directory.Exists(coop) && HUD_ConfirmationPopup.Errors == 1, "settings rechecked at confirmation");
        settings.EnableMod = true;
        vm.DeleteTrailCommand.Execute();
        Directory.Delete(coop, true);
        HUD_ConfirmationPopup.Yes();
        Assert(HUD_ConfirmationPopup.Errors == 2, "disappeared target handled");
        Directory.CreateDirectory(Path.Combine(coop, "TrailMaker"));
        File.WriteAllText(Path.Combine(coop, "TrailMaker", "01.trail"), "source");
        rows.Remove(subscribed);
        list.SelectedItem = coopRow;
        vm.DeleteTrailCommand.Execute();
        HUD_ConfirmationPopup.Yes();
        Assert(!Directory.Exists(coop) && rows.Count == 0 && !FRONT_ManageTrail.Instance.Import.IsEnabled, "complete Coop package and last row removed");
        Assert(File.ReadAllText(Path.Combine(maker, "01.trail")) == "keep editor mission", "editor missions retained");
        Assert(!Directory.Exists(Path.Combine(fixture, "TrailMakerBackup")), "no backup created");
        string locked = Path.Combine(root, "locked");
        Directory.CreateDirectory(locked);
        string lockedFile = Path.Combine(locked, "01.trail");
        File.WriteAllText(lockedFile, "locked mission");
        var lockedRow = new FileRow { Text1 = "locked", trail = new MapFileManager.CustomTrailInfo { Name = "locked", FullPath = locked } };
        rows.Add(lockedRow);
        list.SelectedItem = lockedRow;
        using (var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.DeleteTrailCommand.Execute();
            HUD_ConfirmationPopup.Yes();
            Assert(Directory.Exists(locked) && rows.Contains(lockedRow) && HUD_ConfirmationPopup.Errors == 3,
                "locked file deletion reports error and retains row");
        }
        Assert(MainViewModel.Instance.Show_HUD_ConfirmationMP && !MainViewModel.Instance.Show_HUD_Confirmation,
            "error uses MP overlay");
        MapFileManager.Instance.Trails.Add(new MapFileManager.CustomTrailInfo { Name = "own", FullPath = workshop, workshop = true });
        list.SelectedItem = own;
        Assert(!vm.DeleteTrailEnabled, "ambiguous catalog origin rejected");
        return checks + " Trail deletion checks passed.";
    }
    public static bool Resolve(string name, string source, string root) =>
        TrailDeletionPolicy.TryResolve(name, source, false, root, out _);
}
