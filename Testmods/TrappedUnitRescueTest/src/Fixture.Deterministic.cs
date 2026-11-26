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
        private static readonly FieldInfo PlayerRowsField=typeof(FRONT_Multiplayer).GetField("playerRows",BindingFlags.Instance|BindingFlags.NonPublic);
        private const string PassiveLordName="DoNothing";
        // These are versioned test assets, not a general claim about a mutable user lord.
        private const string PassiveAicFileHash="E20D0BDA6B1AC9D97F9A1E2BDE3F26EAC6385C5D49F84BE92915E32152463348";
        private const string PassiveAivFileHash="733FCBA924438DC3142EAC8A0326376DE9DD422A0FE0EC2A2F8B7068574A5606";
        private bool deterministicPassive;
        private string deterministicAicHash;
        private ulong deterministicAicChecksum,deterministicAivChecksum;
        private int deterministicCustomLobbyType;
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
            deterministicPassive=UsesPassiveWorkerOpponent();
            if(deterministicPassive)ai=ReplaceFixtureRatWithPassiveLord(view,room,human,ai);
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
            ConfigurePerformanceLobby(setup);
            Require(view.AIVs!=null && view.AIVs.Length>=deterministicAi,"Lobby AIV slot unavailable");
            var selection=view.AIVs[deterministicAi-1];Require(selection!=null,"Lobby AIV selection missing");
            if(deterministicPassive)
            {
                deterministicCustomLobbyType=ai.GetLordType();
                Require(deterministicCustomLobbyType==checked((int)AILords.SK_X1+deterministicAi-2) && ai.GetLordSubType()==0,
                    "DoNothing custom lobby slot/subtype differs from the actual Vanilla mapping");
                RequirePassiveSelection(selection);
                selection.rotation=0;selection.community=false;selection.historical=false;
                deterministicAivHash=FixtureAivHash(selection.aivs[0].data);
                deterministicAicHash=FixtureBytesHash(selection.lordConfig.encode());
                deterministicAicChecksum=selection.lordConfig.checksum;deterministicAivChecksum=selection.aivs[0].checksum;
                room.setTeam(human,1);room.setTeam(ai,2);
                Require(room.getTeam(human)==1 && room.getTeam(ai)==2,"DoNothing fixture teams were not retained");
            }
            else
            {
                // Select exactly the first original Rat plan, independently of localization,
                // custom-lord catalog order or the user's last chosen AIV collection.
                short[] data=AIVLoader.getAIVData(RatLobbyType,0,false,false);
                Require(data!=null && data.Length>0,"Original Rat Default1 AIV is unavailable");
                var catalog=CustomisationFileManager.Instance.getLordAIVList(RatLobbyType);
                var original=catalog?.Where(a=>a!=null && a.builtIn && a.lordType==RatLobbyType &&
                    a.data!=null && a.data.SequenceEqual(data)).OrderBy(a=>a.AIVName,StringComparer.Ordinal).FirstOrDefault();
                Require(original!=null,"Original Rat Default1 metadata is missing from the Vanilla catalogue");
                selection.Init(RatLobbyType,string.Empty);
                selection.builtIn=false;selection.rotation=0;
                selection.aivs.Add(new CustomisationFileManager.CustomAIV{lordType=original.lordType,AIVName=original.AIVName,
                    builtIn=original.builtIn,checksum=original.checksum,path=original.path,data=(short[])data.Clone()});
                deterministicAivHash=FixtureAivHash(data);
            }
            RefreshHost.Invoke(view,new object[]{false});
            Require(setup.start_keep_location_order.SequenceEqual(deterministicPlaces) && setup.peacetime==peaceTimeMinutes && setup.advopt_pre_build==0,"Lobby did not retain deterministic setup");
            Require(ReferenceEquals(view.currentLobby,room) && room.members.Count(m=>!m.dummyToBeKicked)==2 &&
                room.members.Contains(human) && room.members.Contains(ai),"Deterministic fixture roster changed");
            if(deterministicPassive)VerifyPassiveSelection(selection);
            else Require(selection.builtInLord && selection.lordConfig==null && selection.aivs.Count==1 && FixtureAivHash(selection.aivs[0].data)==deterministicAivHash,"Deterministic Rat AIC/AIV changed");
            deterministicLobby=room;deterministicLobbyConfigured=true;
            if(deterministicPassive)log("fixture-lobby human="+deterministicHuman+" ai="+deterministicAi+" lord="+PassiveLordName+
                " lobbyType="+ai.GetLordType()+" peace="+peaceTimeMinutes+" prebuild=0 rotation=auto AICsha256="+
                deterministicAicHash+" AIVsha256="+deterministicAivHash+" teamHuman=1 teamAi=2");
            else log("fixture-lobby human="+deterministicHuman+" ai="+deterministicAi+" ratLobbyType="+RatLobbyType+" peace="+peaceTimeMinutes+" prebuild=0 AIC=vanilla-Rat AIV=Default1 rotation=auto sha256="+deterministicAivHash);
            foreach(int place in places)log("LOBBY_KEEP place="+place+" at="+locations[place,0]+","+locations[place,1]+" player="+(deterministicPlaces[place]+1));
        }
        private bool UsesPassiveWorkerOpponent()
        {
            return scenario=="rescue-auto-wheat10-off" ||
                scenario=="rescue-auto-wheat10-on" ||
                scenario=="rescue-auto-hops8-off" ||
                scenario=="rescue-auto-hops8-on" ||
                scenario=="rescue-auto-cattle5-off" ||
                scenario=="rescue-auto-cattle5-on" ||
                scenario=="rescue-auto-hunter5-on" || scenario=="rescue-auto-hunter5-off" ||
                scenario=="rescue-auto-woodcutter1-on" || scenario=="rescue-auto-woodcutter1-off" ||
                scenario=="rescue-auto-miner1-on" || scenario=="rescue-auto-miner1-off" ||
                scenario=="rescue-auto-pitch3-on" || scenario=="rescue-auto-pitch3-off" ||
                scenario=="rescue-auto-fletcher-on" || scenario=="rescue-auto-fletcher-off";
        }
        private Platform_Multiplayer.MPLobbyMember ReplaceFixtureRatWithPassiveLord(FRONT_Multiplayer view,
            Platform_Multiplayer.MPLobby room,Platform_Multiplayer.MPLobbyMember human,Platform_Multiplayer.MPLobbyMember rat)
        {
            VerifyPassiveAssetFiles();
            Require(PlayerRowsField!=null && PlayerRowsField.FieldType==typeof(FRONT_Multiplayer.PlayerRow[]),
                "Vanilla Kick player-row reflection contract is unavailable");
            var rows=(FRONT_Multiplayer.PlayerRow[])PlayerRowsField.GetValue(view);
            int ratId=room.getThisPlayerFromSteamID(rat.GetSteamID());
            Require(rows!=null && rows.Length==8,"Vanilla lobby player rows are unavailable");
            var matches=Enumerable.Range(0,rows.Length).Where(i=>rows[i]!=null && rows[i].playerID==ratId).ToArray();
            Require(matches.Length==1,"The actual Rat Kick row is not unique");
            // Kick_N is a UI-row ordinal, not a player ID. Use the ordinary command's mapping/refresh path.
            view.ButtonClicked("Kick_"+(matches[0]+1));
            Require(ReferenceEquals(view.currentLobby,room) && room.members.Count==1 && room.members[0]==human,
                "The temporary SDK Rat was not removed synchronously");
            view.SkirmishAIAddClick("98");
            var list=view.FindName("CustomLordList") as Noesis.ListView;
            Require(list?.ItemsSource!=null,"Vanilla custom-lord picker is unavailable");
            var candidates=list.ItemsSource.Cast<object>().OfType<FileRow>()
                .Where(row=>string.Equals(row.lord?.lordName,PassiveLordName,StringComparison.Ordinal)).ToArray();
            Require(candidates.Length==1,"Expected exactly one Vanilla DoNothing picker row");
            var lord=candidates[0].lord;
            string folder=PassiveAssetFolder();
            var firstConfig=lord.configs?.FirstOrDefault();var firstPlan=lord.aivs?.FirstOrDefault();
            log("fixture-passive-catalog lord="+FixtureCatalogValue(lord.lordName)+" workshop="+lord.workshop+
                " expectedFolder="+FixtureCatalogValue(folder)+" lordPath="+FixtureCatalogValue(lord.customPath)+
                " configs="+(lord.configs?.Count??-1)+" aivs="+(lord.aivs?.Count??-1)+
                " aicName="+FixtureCatalogValue(firstConfig?.name)+" aicPath="+FixtureCatalogValue(firstConfig?.path)+
                " aicWorkshop="+firstConfig?.workshop+" aivName="+FixtureCatalogValue(firstPlan?.AIVName)+
                " aivPath="+FixtureCatalogValue(firstPlan?.path)+" aivWorkshop="+firstPlan?.workshop+" aivBuiltIn="+firstPlan?.builtIn);
            Require(!lord.workshop && SameFixturePath(lord.customPath,folder) && lord.configs!=null && lord.configs.Count==1 &&
                lord.aivs!=null && lord.aivs.Count==1 && lord.configs[0]!=null && lord.aivs[0]!=null &&
                !lord.configs[0].workshop && !lord.aivs[0].workshop && !lord.aivs[0].builtIn &&
                string.Equals(lord.configs[0].name,PassiveLordName,StringComparison.OrdinalIgnoreCase) &&
                string.Equals(lord.aivs[0].AIVName,PassiveLordName,StringComparison.OrdinalIgnoreCase) &&
                SameFixturePath(lord.configs[0].path,folder) && SameFixturePath(lord.aivs[0].path,folder),
                "DoNothing is not the exact single-AIC/single-AIV local test package");
            list.SelectedItem=candidates[0];view.ButtonClicked("AddCustomLord");
            Require(ReferenceEquals(view.currentLobby,room) && room.members.Count==2 && room.members.Contains(human),
                "Custom-lord addition did not synchronously preserve the original human/room");
            var ai=room.members.Single(member=>!ReferenceEquals(member,human));
            Require(ai.SkirmishMember && !ai.SkirmishHumanMember && !ai.dummyToBeKicked &&
                ai.customLordName==PassiveLordName,"Vanilla did not add the requested passive custom lord");
            return ai;
        }
        // Vanilla lowercases file paths before deriving AIC/AIV names; lordName itself retains the folder spelling.
        // One bounded snapshot per launch identifies the exact failed catalogue field without weakening its guard.
        private static string FixtureCatalogValue(string value)
        {
            if(value==null)return "<null>";
            value=value.Replace('\r',' ').Replace('\n',' ');
            return value.Length<=240?value:value.Substring(0,240)+"...";
        }
        private static string PassiveAssetFolder()=>System.IO.Path.GetFullPath(
            System.IO.Path.Combine(ConfigSettings.GetUserCustomLordsPath(),PassiveLordName));
        private static bool SameFixturePath(string actual,string expected)=>!string.IsNullOrWhiteSpace(actual) &&
            string.Equals(System.IO.Path.GetFullPath(actual).TrimEnd('\\','/'),
                System.IO.Path.GetFullPath(expected).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase);
        private static void VerifyPassiveAssetFiles()
        {
            string folder=PassiveAssetFolder();
            Require(System.IO.File.Exists(System.IO.Path.Combine(folder,PassiveLordName+".lordjson")) &&
                System.IO.File.Exists(System.IO.Path.Combine(folder,PassiveLordName+".aivjson")),"DoNothing test assets are not installed");
            Require(FixtureBytesHash(System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,PassiveLordName+".lordjson")))==PassiveAicFileHash &&
                FixtureBytesHash(System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,PassiveLordName+".aivjson")))==PassiveAivFileHash,
                "Installed DoNothing test assets differ from the audited fixture version");
        }
        private static void RequirePassiveSelection(FRONT_Multiplayer.MPAIVInfo selection)
        {
            Require(selection!=null && selection.lordName==PassiveLordName && !selection.builtIn && !selection.builtInLord &&
                selection.lordConfig!=null && string.Equals(selection.lordConfig.name,PassiveLordName,StringComparison.OrdinalIgnoreCase) && !selection.lordConfig.workshop &&
                selection.aivs!=null && selection.aivs.Count==1 && selection.aivs[0]!=null &&
                string.Equals(selection.aivs[0].AIVName,PassiveLordName,StringComparison.OrdinalIgnoreCase) && !selection.aivs[0].builtIn && !selection.aivs[0].workshop &&
                selection.aivs[0].data!=null && selection.aivs[0].data.Length>0,"The named DoNothing AIC/AIV was not selected");
        }
        private void VerifyPassiveSelection(FRONT_Multiplayer.MPAIVInfo selection)
        {
            RequirePassiveSelection(selection);
            Require(selection.lordType==deterministicCustomLobbyType && selection.rotation==0 && !selection.community && !selection.historical &&
                selection.lordConfig.checksum==deterministicAicChecksum && selection.aivs[0].checksum==deterministicAivChecksum &&
                FixtureBytesHash(selection.lordConfig.encode())==deterministicAicHash && FixtureAivHash(selection.aivs[0].data)==deterministicAivHash,
                "Pinned DoNothing AIC/AIV identity changed");
        }
        private static string FixtureBytesHash(byte[] data)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-",string.Empty);
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
                if(deterministicPassive)
                {
                    // DLL_SetExtendedLordConfig uses player+29; the checked custom enum starts at SK_X1=30.
                    var expectedLord=(AILords)checked((int)AILords.SK_X1+deterministicAi-1);
                    Require((int)GamePlayerManagerAPI.Instance.GetAILord(deterministicAi)==(int)expectedLord,
                        "Native AI identity differs from the requested custom player slot");
                    var restart=MainViewModel.Instance.HUDIngameMenu?.restartSkirmishMapInfo;
                    Require(restart?.aivs!=null && restart.aivs.Length>=deterministicAi &&
                        restart.lordTypes.Count>=deterministicAi && restart.teams.Count>=deterministicAi &&
                        restart.teams[deterministicHuman-1]==1 && restart.teams[deterministicAi-1]==2,
                        "Loaded custom-lord roster/teams are unavailable");
                    Require(restart.lordTypes[deterministicAi-1]==checked(deterministicCustomLobbyType*8+1),
                        "Loaded custom-lord member identity changed");
                    VerifyPassiveSelection(restart.aivs[deterministicAi-1]);
                    VerifyPassiveAssetFiles();
                }
                else Require((int)GamePlayerManagerAPI.Instance.GetAILord(deterministicAi)==(int)AILords.SK_RAT,"Native AI identity differs from requested Rat");
                Require(peaceTimeMinutes==0 || PeaceTimeState.TryGetActive(out bool activePeace) && activePeace,"Native peace time is inactive");
                Require(GamePlayerManagerAPI.Instance.GetPlayerKeepId(deterministicHuman)>0 && GamePlayerManagerAPI.Instance.GetPlayerKeepId(deterministicAi)>0,"Fixture Keeps are missing");
                if(deterministicPassive)log("fixture-world-verified session="+run.CurrentMission.SessionId+" owner="+owner+" ai="+deterministicAi+
                    " nativeLord="+PassiveLordName+" nativeEnum="+(int)GamePlayerManagerAPI.Instance.GetAILord(deterministicAi)+
                    " peace="+peaceTimeMinutes+" prebuild=0 pinnedAivSha256="+deterministicAivHash+" pinnedAicSha256="+deterministicAicHash);
                else log("fixture-world-verified session="+run.CurrentMission.SessionId+" owner="+owner+" ai="+deterministicAi+" nativeLord=Rat peace="+peaceTimeMinutes+" prebuild=0 pinnedAivSha256="+deterministicAivHash);
                return true;
            });
        }
    }
}
