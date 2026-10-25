using APIShared;
using APIShared.GameModes;
using ExtraFeatures;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;

// Only engine value access, logging, event delivery and the public fear broker
// are doubles. The real consumer runtime, profile and mode evaluator run here.
namespace Shared
{
    internal static class GameplayModActivationGate
    {
        internal static readonly GameplayModeGate Gate = new GameplayModeGate(
            SerpsModProfiles.GetProfile("ExtraFeatures_Serp", "Extra Features"));
        internal static bool IsEnabled(bool configured) => Gate.IsEnabled(configured);
    }
    internal static class MissionEvents
    {
        internal static readonly Subject<MissionLifecycleNotification> Initialization = new Subject<MissionLifecycleNotification>();
        internal static readonly Subject<MissionLifecycleNotification> Started = new Subject<MissionLifecycleNotification>();
        internal static readonly Subject<MissionLifecycleNotification> Ended = new Subject<MissionLifecycleNotification>();
        internal static void Publish(MissionLifecycleNotification note)
        {
            GameplayModActivationGate.Gate.Update(note);
            if (note.Kind == MissionLifecycleKind.Start) Started.OnNext(note);
            else if (note.Kind == MissionLifecycleKind.End) Ended.OnNext(note);
            else Initialization.OnNext(note);
        }
    }
}
internal static class PopularityLifecycleTests
{
    private static GameModeSnapshot Mode(GameModeKind kind) => new GameModeSnapshot(
        false, kind==GameModeKind.CustomGame, kind==GameModeKind.CustomGame, false,
        kind==GameModeKind.MapEditor, false, true,false,false,false,false,
        0,0,0,0,0,0,0,-1,0,kind,GameModeLaunchVariant.Standard,0,-1,-1,-1,0,false);
    internal static void Run(Action<bool,string> check)
    {
        var settings = new ExtraFeaturesViewModel(false);
        settings.RationsHumanEnabled = true; settings.RationsPopModifierNoneHuman = -11;
        settings.NegativeFearHumanEnabled = true; settings.FearNegative3Human = 0;
        settings.RationsAIEnabled=true; settings.RationsPopModifierNoneAI=6;
        settings.RationsPopModifierHalfHuman=-13; settings.RationsPopModifierHalfAI=0;
        var runtime = new PopularityRuntime(null, settings);
        runtime.Register();
        long id = 0;
        foreach (var kind in new[] {GameModeKind.CustomGame,GameModeKind.MapEditor,GameModeKind.Campaign,
            GameModeKind.StandaloneMission,GameModeKind.VanillaTrail,GameModeKind.Unknown})
        {
            foreach (var start in Enum.GetValues(typeof(MissionStartKind)).Cast<MissionStartKind>())
            {
                var context = new MissionContext(++id,start,Mode(kind),null,"fixture",false);
                Action<MissionLifecycleKind,MissionInitializationPhase,MissionEndReason> publish = (k,p,r) =>
                    Shared.MissionEvents.Publish(new MissionLifecycleNotification(context,k,p,r,k==MissionLifecycleKind.Start));
                publish(MissionLifecycleKind.Initialization,MissionInitializationPhase.BeforeLoad,MissionEndReason.None);
                bool allowed = Shared.GameplayModActivationGate.Gate.IsAllowed;
                check(allowed == (kind==GameModeKind.CustomGame || kind==GameModeKind.MapEditor),"Popularity follows actual ExtraFeatures mode profile");
                check(APIShared.Economy.PopularitySelection.Resolve(137,APIShared.Economy.PopularitySelection.ForKind(1),(int)APIShared.Economy.PopularitySource.RationsPopModifierNone)==(allowed?-275:137),"Human early source selection follows mode gate");
                check(APIShared.Economy.PopularitySelection.Resolve(137,APIShared.Economy.PopularitySelection.ForKind(2),(int)APIShared.Economy.PopularitySource.RationsPopModifierNone)==(allowed?150:137),"AI simultaneously receives its own source value");
                check(APIShared.Economy.PopularitySelection.Resolve(137,APIShared.Economy.PopularitySelection.ForKind(1),(int)APIShared.Economy.PopularitySource.RationsPopModifierHalf)==(allowed?-325:137),"Group gate publishes another human member at early checkpoints");
                check(APIShared.Economy.PopularitySelection.Resolve(137,APIShared.Economy.PopularitySelection.ForKind(2),(int)APIShared.Economy.PopularitySource.RationsPopModifierHalf)==(allowed?0:137),"Group gate publishes explicit AI zero for another member");
                check(APIShared.Economy.FearPopularity.MultiplyTargeted(25,-3,APIShared.Economy.PopularitySelection.ForKind(1))==(allowed?0:-75),"Human fear obeys early mode gate");
                check(APIShared.Economy.FearPopularity.MultiplyTargeted(25,-3,APIShared.Economy.PopularitySelection.ForKind(2))==-75,"AI fear remains untouched");
                publish(MissionLifecycleKind.Initialization,MissionInitializationPhase.NativeLoaded,MissionEndReason.None);
                publish(MissionLifecycleKind.Start,MissionInitializationPhase.ManagedReady,MissionEndReason.None);

                settings.RationsPopModifierNoneHuman=4;
                check(settings.RationsPopModifierNoneHuman==-11,"Mission prevents setting mutation");
                publish(MissionLifecycleKind.End,MissionInitializationPhase.ManagedReady,MissionEndReason.Unloaded);
                check(!APIShared.Economy.PopularitySelection.NeedsPlayer && settings.PopularityBetweenMissions,"End clears targeted configuration and unlocks setup");
            }
        }
        settings.RationsHumanEnabled=false;
        settings.EnableMod=false;
        var attempt = new MissionContext(++id,MissionStartKind.NewGame,Mode(GameModeKind.CustomGame),null,"failed",false);
        Shared.MissionEvents.Publish(new MissionLifecycleNotification(attempt,MissionLifecycleKind.Initialization,MissionInitializationPhase.BeforeLoad,MissionEndReason.None,false));
        Shared.MissionEvents.Publish(new MissionLifecycleNotification(attempt,MissionLifecycleKind.End,MissionInitializationPhase.BeforeLoad,MissionEndReason.Failed,false));
        check(!APIShared.Economy.PopularitySelection.NeedsPlayer && settings.PopularityBetweenMissions,"Disabled/failed attempt leaves no state");
    }
}
