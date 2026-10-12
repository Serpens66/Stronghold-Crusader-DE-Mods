using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Linq;
using APIShared.Audio;
using ArabianIdleSoundTest;

internal static class Program
{
    private static string game;
    private static int Main(string[] args)
    {
        game=args[0]; AppDomain.CurrentDomain.AssemblyResolve+=Resolve;
        try { Check(); Console.WriteLine("PASS: consumer rule, public audio contracts and hook-free plugin."); return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name=new AssemblyName(args.Name).Name+".dll";
        if(name=="APIShared.dll") return Assembly.LoadFrom(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"../../../../../APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll")));
        foreach(string directory in new[]{"BepInEx/core","BepInEx/plugins/000shcdese","Stronghold Crusader Definitive Edition_Data/Managed"})
        { string path=Path.Combine(game,directory,name); if(File.Exists(path))return Assembly.LoadFrom(path); }
        return null;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Check()
    {
        var constructor=typeof(SoundContext).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single(c=>c.GetParameters().Length==7);
        var known=(SoundContext)constructor.Invoke(new object[]{180,1.6f,0f,SoundPlaybackKind.Spatial,SoundSituation.ArabianIdleSwordDraw,21,42});
        var unknown=(SoundContext)constructor.Invoke(new object[]{180,1.6f,0f,SoundPlaybackKind.Spatial,SoundSituation.Unknown,null,null});
        if(!Plugin.Matches(known)||Plugin.Matches(unknown))throw new Exception("Consumer context filter changed");
        var preConstructor=typeof(SoundPreEventArgs).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic)[0];
        var pre=(SoundPreEventArgs)preConstructor.Invoke(new object[]{known,false,(Func<int,bool>)(id=>true)});
        Plugin.Apply(pre); if(pre.Volume!=.4f || pre.VolumeFactor!=.25f || pre.SoundId!=180)throw new Exception("25 percent consumer rule changed");
        pre=(SoundPreEventArgs)preConstructor.Invoke(new object[]{unknown,false,(Func<int,bool>)(id=>true)});
        Plugin.Apply(pre); if(pre.VolumeFactor!=1f)throw new Exception("Unknown context changed");
        var handle=(SoundRegistration)Activator.CreateInstance(typeof(SoundRegistration),true);handle.SetEnabled(false);
        if(handle.Enabled)throw new Exception("Logical disable failed");
        typeof(Plugin).GetField("registration",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,handle);
        foreach(PlaybackProbe probe in Enum.GetValues(typeof(PlaybackProbe)))
        {
            Plugin.Configure(true,probe); if(!handle.Enabled)throw new Exception("Live activation failed");
            pre=(SoundPreEventArgs)preConstructor.Invoke(new object[]{known,false,(Func<int,bool>)(id=>id==179)});
            Plugin.Apply(pre);
            if(probe==PlaybackProbe.Volume25 && pre.VolumeFactor!=.25f ||
               probe==PlaybackProbe.ReplaceVanilla && pre.SoundId!=179 ||
               probe==PlaybackProbe.Suppress && !pre.SkipOriginalFunction ||
               probe==PlaybackProbe.PanLeft && pre.Pan!=-1f)throw new Exception("Live probe switch failed: "+probe);
            if(!Equals(pre.State,probe))throw new Exception("Probe state not captured for matching Post");
            Plugin.Configure(false,probe);if(handle.Enabled)throw new Exception("Live deactivation failed");
            typeof(Plugin).GetField("confirmed",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,1);
            Plugin.Configure(true,probe);
            if((int)typeof(Plugin).GetField("confirmed",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)!=0)throw new Exception("Next-probe confirmation not reset");
        }
        Plugin.Configure(true,PlaybackProbe.CustomClip);
        pre=(SoundPreEventArgs)preConstructor.Invoke(new object[]{known,false,(Func<int,bool>)(id=>true)});
        Plugin.Apply(pre);
        if(pre.Target.Kind!=SoundTargetKind.VanillaEffect)throw new Exception("Pending custom clip changed Vanilla target");
        var clipConstructor=typeof(SoundClipHandle).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single();
        var clip=(SoundClipHandle)clipConstructor.Invoke(new object[]{Plugin.Guid,"Fixture",.5f,false});
        var fakeClip=(UnityEngine.AudioClip)FormatterServices.GetUninitializedObject(typeof(UnityEngine.AudioClip));
        typeof(SoundClipHandle).GetMethod("Publish",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(clip,new object[]{fakeClip});
        typeof(Plugin).GetField("testClip",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,clip);
        pre=(SoundPreEventArgs)preConstructor.Invoke(new object[]{known,false,(Func<int,bool>)(id=>true)});
        Plugin.Apply(pre);
        if(pre.Target.Kind!=SoundTargetKind.CustomClip || !ReferenceEquals(pre.Target.Clip,clip) || pre.SoundId!=-1 || pre.Volume!=1.6f)throw new Exception("Custom probe did not select typed ready clip");
        typeof(Plugin).GetField("activeSituation",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(int)SoundSituation.RangedWeaponFire);
        var ranged=(SoundContext)constructor.Invoke(new object[]{70,1.6f,0f,SoundPlaybackKind.Spatial,SoundSituation.RangedWeaponFire,21,42});
        Plugin.Configure(true,PlaybackProbe.Volume25);
        if(Plugin.Matches(known)||!Plugin.Matches(ranged)||Plugin.Matches(unknown))throw new Exception("Live situation selector leaked to other situations");
        pre=(SoundPreEventArgs)preConstructor.Invoke(new object[]{ranged,false,(Func<int,bool>)(id=>true)});
        Plugin.Apply(pre);if(pre.Volume!=.4f)throw new Exception("Selected soldier situation did not apply the rule");
        typeof(Plugin).GetField("activeSituation",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(int)SoundSituation.ArabianIdleSwordDraw);
        var typedConstructor=typeof(SoundContext).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single(c=>c.GetParameters().Length==10);
        Func<int,string,SoundContext> typed=(type,key)=>(SoundContext)typedConstructor.Invoke(new object[]{172,1.6f,0f,SoundPlaybackKind.Spatial,SoundSituation.RangedWeaponFire,21,42,SituationEvidence.Inferred,type,key});
        var archer=typed(22,"Archer.A");var arabArcher=typed(70,"ArabArcher.A");var secondArcher=typed(22,"Archer.B");
        var typeField=typeof(Plugin).GetField("activeUnitType",BindingFlags.Static|BindingFlags.NonPublic);
        var cooldownField=typeof(Plugin).GetField("activeCooldown",BindingFlags.Static|BindingFlags.NonPublic);
        typeof(Plugin).GetField("activeSituation",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(int)SoundSituation.RangedWeaponFire);
        typeField.SetValue(null,22);cooldownField.SetValue(null,1000);Plugin.Configure(true,PlaybackProbe.Volume25);
        Func<SoundContext,SoundPreEventArgs> request=context=>(SoundPreEventArgs)preConstructor.Invoke(new object[]{context,false,(Func<int,bool>)(id=>true)});
        pre=request(arabArcher);Plugin.ApplyAt(pre,0);if(pre.VolumeFactor!=1 || pre.SkipOriginalFunction)throw new Exception("Excluded unit type was modified");
        pre=request(archer);Plugin.ApplyAt(pre,0);if(pre.Volume!=.4f || pre.SkipOriginalFunction)throw new Exception("Excluded type consumed repeat window");
        pre=request(secondArcher);Plugin.ApplyAt(pre,System.Diagnostics.Stopwatch.Frequency/2);if(!pre.SkipOriginalFunction)throw new Exception("Shared cooldown did not suppress another source");
        pre=request(archer);Plugin.ApplyAt(pre,System.Diagnostics.Stopwatch.Frequency);if(pre.SkipOriginalFunction || pre.Volume!=.4f)throw new Exception("Exact interval boundary did not admit playback");
        typeField.SetValue(null,-1);Plugin.Configure(true,PlaybackProbe.Volume25);
        pre=request(archer);Plugin.ApplyAt(pre,0);pre=request(arabArcher);Plugin.ApplyAt(pre,1);if(!pre.SkipOriginalFunction)throw new Exception("All-types cooldown must share one window");
        Plugin.ResetRepeatWindow();pre=request(arabArcher);Plugin.ApplyAt(pre,1);if(pre.SkipOriginalFunction)throw new Exception("Session reset retained cooldown window");
        Plugin.Configure(true,PlaybackProbe.Volume25);pre=request(archer);pre.SkipOriginalFunction=true;Plugin.ApplyAt(pre,0);
        pre=request(archer);Plugin.ApplyAt(pre,0);if(pre.SkipOriginalFunction)throw new Exception("Earlier contributor veto consumed repeat window");
        cooldownField.SetValue(null,0);Plugin.Configure(true,PlaybackProbe.Volume25);
        pre=request(archer);Plugin.ApplyAt(pre,0);pre=request(archer);Plugin.ApplyAt(pre,0);if(pre.SkipOriginalFunction)throw new Exception("Zero cooldown must preserve all requests");
        typeField.SetValue(null,75);if(Plugin.Matches(ranged))throw new Exception("Missing unit attribution passed specific unit filter");
        typeField.SetValue(null,-1);typeof(Plugin).GetField("activeSituation",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,(int)SoundSituation.ArabianIdleSwordDraw);
        var repeatLimiter=new SoundRepeatLimiter(1000);if(!repeatLimiter.TryAcquire(100)||repeatLimiter.TryAcquire(101)||!repeatLimiter.TryAcquire(50))throw new Exception("Regressing timestamp did not safely reopen window");
        bool intervalRejected=false;try{new SoundRepeatLimiter(60001);}catch(ArgumentOutOfRangeException){intervalRejected=true;}if(!intervalRejected)throw new Exception("Invalid interval accepted");
        var defaultSettings=new SoundTestSettings();if(defaultSettings.UnitTypeIndex!=0||defaultSettings.CooldownMilliseconds!=0)throw new Exception("Presentation defaults changed");
        defaultSettings.UnitTypeIndex=-1;defaultSettings.CooldownMilliseconds=-1;defaultSettings.CooldownText="NaN";defaultSettings.CooldownText="60001";
        if(defaultSettings.UnitTypeIndex!=0||defaultSettings.CooldownMilliseconds!=0)throw new Exception("Invalid stored/UI values accepted");
        bool invalidRejected=false;try{Plugin.Configure(true,(PlaybackProbe)99);}catch(ArgumentOutOfRangeException){invalidRejected=true;}
        if(!invalidRejected)throw new Exception("Invalid probe accepted");
        Plugin.Configure(true,PlaybackProbe.Volume25);
        if(Plugin.Version!="0.1.0" || typeof(Plugin).Assembly.GetType("ArabianIdleSoundTest.SoundRuntime")!=null)throw new Exception("Consumer still owns legacy hooks");
    }
}
