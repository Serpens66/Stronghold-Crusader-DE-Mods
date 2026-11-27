using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class TrappedUnitRescuePresetTests
{
    private const string Property=nameof(BugfixesAndQoL.BugfixesAndQoLViewModel.EnableTrappedUnitRescue);
    internal static void Run()
    {
        bool host=GameNetworkAPI.LocalHost,network=GameNetworkAPI.Networked,mp=GameNetworkAPI.MultiplayerGame,throws=GameNetworkAPI.ThrowOnRoleQuery;
        try
        {
            GameNetworkAPI.ThrowOnRoleQuery=false;GameNetworkAPI.LocalHost=true;GameNetworkAPI.Networked=false;GameNetworkAPI.MultiplayerGame=false;
            Require(typeof(BugfixesAndQoL.BugfixesAndQoLViewModel).GetProperty(Property).GetCustomAttribute<SyncHostOnlyAttribute>()!=null,"host synchronization");
            string plugin=Path.Combine(Program.PresetFixtureInstallRoot,"TrappedUnitRescue.dll"),guid="TrappedUnitRescue";
            string file=new PersonalSettingsPaths(plugin,"Tests."+guid).SettingsFile;
            var model=Prepare(plugin,guid);Require(model.EnableTrappedUnitRescue,"production default true");
            model.EnableTrappedUnitRescue=false;Require(!Prepare(plugin,guid).EnableTrappedUnitRescue,"explicit false survives restart");
            model.System_LoadModDefaults();Require(model.EnableTrappedUnitRescue && Prepare(plugin,guid).EnableTrappedUnitRescue,"shared reset restores actual default true");
            string legacyGuid=guid+"Legacy",legacyFile=new PersonalSettingsPaths(plugin,"Tests."+legacyGuid).SettingsFile;
            Directory.CreateDirectory(Path.GetDirectoryName(legacyFile));
            File.WriteAllBytes(legacyFile,MessagePackSerializer.Serialize(new Dictionary<string,byte[]>{
                ["__SerpPresetSchemaVersion"]=MessagePackSerializer.Serialize(3),
                ["__SerpCurrentSettings"]=MessagePackSerializer.Serialize(new Dictionary<string,byte[]>()),
                ["__SerpPresetDirty"]=MessagePackSerializer.Serialize(false),
                ["__SerpLegacyPresetImportCompleted"]=MessagePackSerializer.Serialize(true)}));
            Require(Prepare(plugin,legacyGuid).EnableTrappedUnitRescue,"old missing property uses production true");
            byte[] before=File.ReadAllBytes(file);
            GameNetworkAPI.LocalHost=false;GameNetworkAPI.Networked=true;GameNetworkAPI.MultiplayerGame=true;
            model.System_RefreshSettingsAccess();
            model.EnableTrappedUnitRescue=false;Require(model.EnableTrappedUnitRescue,"client cannot edit host property");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(model,()=>model.EnableTrappedUnitRescue=false);
            Require(!model.EnableTrappedUnitRescue && before.SequenceEqual(File.ReadAllBytes(file)),"host false is transient on client, never persisted locally");
            GameNetworkAPI.LocalHost=true;GameNetworkAPI.Networked=false;GameNetworkAPI.MultiplayerGame=false;
            model=Prepare(plugin,guid);var trail=model.System_CreateCurrentWorkingSnapshot();trail[Property]=MessagePackSerializer.Serialize(false);
            model.System_EnterMissionPreset(trail,"Rescue Trail",false);
            Require(!model.EnableTrappedUnitRescue && !model.CanEditHostSettings,"Trail supplies independent false");
            model.EnableTrappedUnitRescue=true;Require(!model.EnableTrappedUnitRescue,"read-only Trail cannot be edited");
            model.System_ExitMissionPreset();Require(model.EnableTrappedUnitRescue && before.SequenceEqual(File.ReadAllBytes(file)),"Trail exit restores personal true without changing storage");
            Console.WriteLine("PASS: trapped-unit rescue actual production default/reset, old/missing and false persistence, host/client and Trail contracts (10 checks).");
        }
        finally{GameNetworkAPI.LocalHost=host;GameNetworkAPI.Networked=network;GameNetworkAPI.MultiplayerGame=mp;GameNetworkAPI.ThrowOnRoleQuery=throws;}
    }
    private static BugfixesAndQoL.BugfixesAndQoLViewModel Prepare(string plugin,string guid)
    {var model=new BugfixesAndQoL.BugfixesAndQoLViewModel();model.PreparePresets(null,plugin,guid);model.ActivatePresets();return model;}
    private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException("Rescue preset: "+reason);}
}