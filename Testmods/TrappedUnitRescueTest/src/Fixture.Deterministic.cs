using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using APIShared.Automation;
using APIShared.Simulation;
using BepInEx.Bootstrap;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private const BindingFlags FixtureMembers=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly int RatLobbyType=checked((int)AILords.SK_RAT-1);
        private static readonly FieldInfo SetupField=typeof(FRONT_Multiplayer).GetField("MPsetupData",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo SelectedMapField=typeof(FRONT_Multiplayer).GetField("selectedMPHeader",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly MethodInfo RefreshHost=typeof(FRONT_Multiplayer).GetMethod("UpdateHostInfo",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(bool)},null);
        private bool deterministicLobbyConfigured;
        private Platform_Multiplayer.MPLobby deterministicLobby;
        private int deterministicHuman,deterministicAi;
        private int[] deterministicPlaces;
        private string deterministicAivHash;
        private readonly Dictionary<string,object> deterministicSettings=new Dictionary<string,object>(StringComparer.Ordinal);

        // Frontend only. The external transaction restores personal settings after the run.
        private void PrepareDeterministicModSettings()
        {
            Require(FatControler.currentScene==Enums.SceneIDS.FrontEnd,"Fixture settings require the frontend");
            foreach(var entry in Chainloader.PluginInfos.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                var settings=FindFixtureSettings(entry.Value.Instance);
                if(settings==null)continue;
                var reset=settings.GetType().GetProperty("ResetToDefaultCommand",FixtureMembers)?.GetValue(settings,null);
                var execute=reset?.GetType().GetMethod("Execute",new[]{typeof(object)});
                Require(execute!=null,"No reset command for "+entry.Key);
                execute.Invoke(reset,new object[]{null});
                deterministicSettings.Add(entry.Key,settings);
                log("fixture-settings-defaults mod="+entry.Key);
            }
            SetFixtureSetting("RandomEvents_Serp","EnableMod",false);
            SetFixtureSetting("CastlePlanner_Serp","EnableMod",true);
            SetFixtureSetting("CastlePlanner_Serp","SpawnCastle",false);
        }
        private static object FindFixtureSettings(object plugin)
        {
            if(plugin==null)return null;
            if(plugin.GetType().GetProperty("ResetToDefaultCommand",FixtureMembers)!=null)return plugin;
            foreach(var first in FixtureSettingMembers(plugin))
            {
                if(first==null)continue;
                if(first.GetType().GetProperty("ResetToDefaultCommand",FixtureMembers)!=null)return first;
                foreach(var second in FixtureSettingMembers(first))
                    if(second!=null && second.GetType().GetProperty("ResetToDefaultCommand",FixtureMembers)!=null)return second;
            }
            return null;
        }
        private static IEnumerable<object> FixtureSettingMembers(object target)
        {
            foreach(string name in new[]{"Settings","processRoot"})
            {
                var p=target.GetType().GetProperty(name,FixtureMembers);
                if(p!=null && p.GetIndexParameters().Length==0 && p.GetGetMethod(true)!=null)
                    yield return p.GetValue(p.GetGetMethod(true).IsStatic?null:target,null);
                var f=target.GetType().GetField(name,FixtureMembers);
                if(f!=null)yield return f.GetValue(f.IsStatic?null:target);
            }
        }
        private void SetFixtureSetting(string guid,string name,bool value)
        {
            if(!Chainloader.PluginInfos.ContainsKey(guid))return;
            Require(deterministicSettings.TryGetValue(guid,out var settings),"Loaded mod has no resettable settings: "+guid);
            var property=settings.GetType().GetProperty(name,FixtureMembers);
            Require(property!=null && property.PropertyType==typeof(bool) && property.CanWrite,"Missing fixture setting "+guid+"."+name);
            property.SetValue(settings,value,null);
            Require(Equals(property.GetValue(settings,null),value),"Fixture setting was rejected: "+guid+"."+name);
            log("fixture-setting mod="+guid+" property="+name+" value="+value);
        }

        // Called once by the SDK after setup and before its final live-room/map
        // validation. Visible stale frontend lobby objects are never a trigger.
        private void ConfigureDeterministicLobby()
        {
            Require(!deterministicLobbyConfigured,"Fixture lobby preparation was invoked twice");
            Require(MainViewModel.viewModelLoaded && MainViewModel.Instance.Show_MPGameCreation,"Fixture lobby is not ready for launch preparation");
            var view=MainViewModel.Instance.FRONTMultiplayer;
            var room=view?.currentLobby;
            Require(room!=null && room.members.Count>=2,"Fixture opponent roster is not ready");
            Require(FRONT_Multiplayer.skirmishGame && !FRONT_Multiplayer.coopGame && room.isHost,"Expected local Skirmish host");
            var members=room.members.Where(m=>!m.dummyToBeKicked).ToArray();
            Require(members.Length==2 && members.All(m=>m.SkirmishMember) && members.Count(m=>m.SkirmishHumanMember)==1,"Expected one human and one AI");
            var human=members.Single(m=>m.SkirmishHumanMember);
            var ai=members.Single(m=>!m.SkirmishHumanMember);
            Require(ai.GetLordType()==RatLobbyType && string.IsNullOrEmpty(ai.customLordName),"SDK did not create the requested built-in Rat");
            Require(SetupField!=null && SetupField.FieldType==typeof(EngineInterface.MultiplayerSetupData) &&
                SelectedMapField!=null && SelectedMapField.FieldType==typeof(FileHeader) && RefreshHost!=null &&
                RefreshHost.ReturnType==typeof(void),"Lobby reflection contract unavailable");
            var header=SelectedMapField.GetValue(view) as FileHeader;
            Require(header!=null,"Selected fixture map is unavailable");
            Require(string.Equals(System.IO.Path.GetFileName(header.filePath),System.IO.Path.GetFileName(map),StringComparison.OrdinalIgnoreCase),"Selected fixture map changed");
            var setup=(EngineInterface.MultiplayerSetupData)SetupField.GetValue(view);
            Require(setup!=null && setup.start_keep_location_order!=null,"Lobby setup missing");
            deterministicHuman=room.getThisPlayerFromSteamID(human.GetSteamID());
            deterministicAi=room.getThisPlayerFromSteamID(ai.GetSteamID());
            Require(deterministicHuman>=1 && deterministicHuman<=8 && deterministicAi>=1 && deterministicAi<=8 && deterministicHuman!=deterministicAi,"Invalid fixture player mapping");
            var locations=GameData.Instance.Keep_Locations;
            var places=Enumerable.Range(0,Math.Min(setup.start_keep_location_order.Length,locations.GetLength(0)))
                .Where(i=>locations[i,0]>=0 && locations[i,1]>=0).Take(2).ToArray();
            Require(places.Length==2,"Fixture map requires two valid Keep locations");
            deterministicPlaces=Enumerable.Repeat(-10,setup.start_keep_location_order.Length).ToArray();
            deterministicPlaces[places[0]]=deterministicHuman-1;deterministicPlaces[places[1]]=deterministicAi-1;
            Array.Copy(deterministicPlaces,setup.start_keep_location_order,deterministicPlaces.Length);
            setup.peacetime=peaceTimeMinutes;
            setup.advopt_pre_build=0;
            // Select exactly the first original Rat plan, independently of localization,
            // custom-lord catalog order or the user's last chosen AIV collection.
            short[] data=AIVLoader.getAIVData(RatLobbyType,0,false,false);
            Require(data!=null && data.Length>0,"Original Rat Default1 AIV is unavailable");
            var catalog=CustomisationFileManager.Instance.getLordAIVList(RatLobbyType);
            var original=catalog?.Where(a=>a!=null && a.builtIn && a.lordType==RatLobbyType &&
                a.data!=null && a.data.SequenceEqual(data)).OrderBy(a=>a.AIVName,StringComparer.Ordinal).FirstOrDefault();
            Require(original!=null,"Original Rat Default1 metadata is missing from the Vanilla catalogue");
            Require(view.AIVs!=null && view.AIVs.Length>=deterministicAi,"Lobby AIV slot unavailable");
            var selection=view.AIVs[deterministicAi-1];Require(selection!=null,"Lobby AIV selection missing");
            selection.Init(RatLobbyType,string.Empty);
            selection.builtIn=false;selection.rotation=0;
            selection.aivs.Add(new CustomisationFileManager.CustomAIV{lordType=original.lordType,AIVName=original.AIVName,
                builtIn=original.builtIn,checksum=original.checksum,path=original.path,data=(short[])data.Clone()});
            deterministicAivHash=FixtureAivHash(data);
            RefreshHost.Invoke(view,new object[]{false});
            Require(setup.start_keep_location_order.SequenceEqual(deterministicPlaces) && setup.peacetime==peaceTimeMinutes && setup.advopt_pre_build==0,"Lobby did not retain deterministic setup");
            Require(selection.builtInLord && selection.lordConfig==null && selection.aivs.Count==1 && FixtureAivHash(selection.aivs[0].data)==deterministicAivHash,"Deterministic Rat AIC/AIV changed");
            deterministicLobby=room;deterministicLobbyConfigured=true;
            log("fixture-lobby human="+deterministicHuman+" ai="+deterministicAi+" ratLobbyType="+RatLobbyType+" peace="+peaceTimeMinutes+" prebuild=0 AIC=vanilla-Rat AIV=Default1 rotation=auto sha256="+deterministicAivHash);
            foreach(int place in places)log("LOBBY_KEEP place="+place+" at="+locations[place,0]+","+locations[place,1]+" player="+(deterministicPlaces[place]+1));
        }
        private static string FixtureAivHash(short[] data)
        {
            var bytes=new byte[checked(data.Length*sizeof(short))];Buffer.BlockCopy(data,0,bytes,0,bytes.Length);
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty);
        }
        private void VerifyDeterministicWorld(AutomationRun run)
        {
            Require(deterministicLobbyConfigured && deterministicLobby!=null && owner==deterministicHuman,"Deterministic fixture lobby was not applied");
            foreach(var pair in new[]{Tuple.Create("RandomEvents_Serp","EnableMod",false),Tuple.Create("CastlePlanner_Serp","EnableMod",true),Tuple.Create("CastlePlanner_Serp","SpawnCastle",false)})
                if(Chainloader.PluginInfos.TryGetValue(pair.Item1,out var plugin))
                {
                    var settings=FindFixtureSettings(plugin.Instance);
                    Require(settings!=null && Equals(settings.GetType().GetProperty(pair.Item2,FixtureMembers)?.GetValue(settings,null),pair.Item3),
                        "Mission preset changed fixture setting "+pair.Item1+"."+pair.Item2);
                }
            run.InSimulation(run.CurrentMission,()=>{
                var loaded=EngineInterface.getMultiplayerStartingData();
                Require(loaded!=null && loaded.peacetime==peaceTimeMinutes && loaded.advopt_pre_build==0 &&
                    loaded.start_keep_location_order.SequenceEqual(deterministicPlaces),"Native world received different fixture settings");
                Require((int)GamePlayerManagerAPI.Instance.GetAILord(deterministicAi)==(int)AILords.SK_RAT,"Native AI identity differs from requested Rat");
                Require(peaceTimeMinutes==0 || PeaceTimeState.TryGetActive(out bool activePeace) && activePeace,"Native peace time is inactive");
                Require(GamePlayerManagerAPI.Instance.GetPlayerKeepId(deterministicHuman)>0 && GamePlayerManagerAPI.Instance.GetPlayerKeepId(deterministicAi)>0,"Fixture Keeps are missing");
                log("fixture-world-verified session="+run.CurrentMission.SessionId+" owner="+owner+" ai="+deterministicAi+" nativeLord=Rat peace="+peaceTimeMinutes+" prebuild=0 pinnedAivSha256="+deterministicAivHash);
                return true;
            });
        }
    }
}
