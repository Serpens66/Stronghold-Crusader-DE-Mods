using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using APIShared.Automation;
using Mono.Cecil;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private const string LoadCacheAssetName="TrappedUnitRescueTest.LoadCacheAsset";
        private const string LoadCacheSentinelType="RescueLoadCacheProbe.Sentinel";

        private IEnumerable<AutomationStep> AssemblyLoadCacheSteps()
        {yield return new AutomationStep("unity-mono-loadfile-identity",TimeSpan.FromSeconds(30),AssemblyLoadCache);}

        private IEnumerable<AutomationWait> AssemblyLoadCache(AutomationRun run)
        {
            yield return AutomationWait.Until("front end without a mission for managed load-cache check",()=>RescueScenarioSeries.MenuReady(run));
            Require(run.CurrentMission==null && Type.GetType("Mono.Runtime",false)!=null &&
                typeof(object).Assembly.GetName().Name=="mscorlib",
                "Load-cache acceptance requires the actual game Unity-Mono process before mission start; desktop CLR is insufficient");
            string package=Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            string path=Path.GetFullPath(Path.Combine(package,"Assets","LoadCache",LoadCacheAssetName+".dll"));
            Require(File.Exists(path),"Owned load-cache asset was not built/packaged: "+path);
            foreach(var loaded in AppDomain.CurrentDomain.GetAssemblies())
                Require(loaded.GetName().Name!=LoadCacheAssetName,
                    "First-load asset is already in this AppDomain; use a fresh game process, not a repeated case");

            Guid diskMvid;
            using(var module=ModuleDefinition.ReadModule(path))
            {
                Require(module.Assembly.Name.Name==LoadCacheAssetName &&
                    module.AssemblyReferences.All(reference=>reference.Name=="mscorlib") &&
                    module.Types.All(type=>!type.CustomAttributes.Any(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin")),
                    "Load-cache asset is not the pure owned BCL-only non-plugin fixture");
                var moduleType=module.Types.Single(type=>type.Name=="<Module>");
                var sentinel=module.Types.Single(type=>type.FullName==LoadCacheSentinelType);
                Require(!moduleType.Methods.Any(method=>method.IsConstructor) && !sentinel.Methods.Any(method=>method.IsConstructor),
                    "Load-cache asset has a module/type initializer");
                var field=sentinel.Fields.Single(value=>value.Name=="Value");
                Require(sentinel.IsPublic && sentinel.IsAbstract && sentinel.IsSealed && field.IsPublic && field.IsStatic &&
                    !field.IsInitOnly && field.FieldType.FullName=="System.Int32","Load-cache sentinel metadata changed");
                diskMvid=module.Mvid;
            }
            string assetHash=LoadCacheSha256(path);
            log("loadcache-first-load asset="+path+" diskMvid="+diskMvid+" sha256="+assetHash+
                " beforeMission=true runtime=UnityMono previouslyLoaded=false");
            // Actual LoadFile is deliberately exercised twice on the same canonical path.
            // No LoadFrom, Reflection.Emit, resolve handler, plugin instantiation or game write.
            Assembly first=Assembly.LoadFile(path);
            Type firstType=first.GetType(LoadCacheSentinelType,true);
            FieldInfo firstField=firstType.GetField("Value",BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
            int initialFirst=(int)firstField.GetValue(null);
            Require(initialFirst==0,"Fresh fixture static sentinel was already modified");
            Assembly second=Assembly.LoadFile(path);
            Type secondType=second.GetType(LoadCacheSentinelType,true);
            FieldInfo secondField=secondType.GetField("Value",BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
            int initialSecond=(int)secondField.GetValue(null);
            bool firstToSecond=false,secondToFirst=false;
            try
            {
                firstField.SetValue(null,271828);
                firstToSecond=(int)secondField.GetValue(null)==271828;
                secondField.SetValue(null,314159);
                secondToFirst=(int)firstField.GetValue(null)==314159;
            }
            finally
            {
                // Restore both views even if a runtime unexpectedly creates separate load contexts.
                firstField.SetValue(null,initialFirst);
                secondField.SetValue(null,initialSecond);
            }
            bool restored=(int)firstField.GetValue(null)==initialFirst && (int)secondField.GetValue(null)==initialSecond;
            bool sameAssembly=ReferenceEquals(first,second),sameType=ReferenceEquals(firstType,secondType);
            bool sameMvid=first.ManifestModule.ModuleVersionId==diskMvid && second.ManifestModule.ModuleVersionId==diskMvid;
            log("loadcache-asset-result assemblyReferenceEquals="+sameAssembly+" typeReferenceEquals="+sameType+
                " firstMvid="+first.ManifestModule.ModuleVersionId+" secondMvid="+second.ManifestModule.ModuleVersionId+
                " diskMvidMatch="+sameMvid+" sentinelFirstToSecond="+firstToSecond+" sentinelSecondToFirst="+secondToFirst+
                " sentinelRestored="+restored+" firstPath="+first.Location+" secondPath="+second.Location);
            Require(sameAssembly && sameType && sameMvid && initialSecond==0 && firstToSecond && secondToFirst && restored &&
                LoadCachePathEquals(first.Location,path) && LoadCachePathEquals(second.Location,path) && LoadCacheSha256(path)==assetHash,
                "Actual Unity-Mono same-path LoadFile did not preserve assembly/type/MVID and shared reset static state");

            // Compare the APIShared assembly already used by this real running mod.
            // typeof/GetType do not call any APIShared initialization or write its static fields.
            Type actualApiType=typeof(APIShared.Patching.SafeIlHook);
            Assembly api=actualApiType.Assembly;
            string apiPath=Path.GetFullPath(api.Location),apiHash=LoadCacheSha256(apiPath);
            Guid apiDiskMvid;
            using(var module=ModuleDefinition.ReadModule(apiPath))apiDiskMvid=module.Mvid;
            Require(api.GetName().Name=="APIShared" && api.ManifestModule.ModuleVersionId==apiDiskMvid,
                "Currently loaded APIShared does not match the selected on-disk module");
            Assembly reloadedApi=Assembly.LoadFile(apiPath);
            Type reloadedApiType=reloadedApi.GetType(actualApiType.FullName,true);
            bool sameApi=ReferenceEquals(api,reloadedApi),sameApiType=ReferenceEquals(actualApiType,reloadedApiType);
            bool sameApiMvid=reloadedApi.ManifestModule.ModuleVersionId==apiDiskMvid;
            log("loadcache-apishared-result assemblyReferenceEquals="+sameApi+" typeReferenceEquals="+sameApiType+
                " mvidMatch="+sameApiMvid+" loadedMvid="+api.ManifestModule.ModuleVersionId+
                " reloadedMvid="+reloadedApi.ManifestModule.ModuleVersionId+" path="+apiPath+" sha256="+apiHash+
                " apiStaticWrites=0");
            Require(sameApi && sameApiType && sameApiMvid && LoadCachePathEquals(reloadedApi.Location,apiPath) &&
                LoadCacheSha256(apiPath)==apiHash,"Actual Unity-Mono LoadFile did not reuse the already loaded APIShared assembly/type");
            Require(run.CurrentMission==null && RescueScenarioSeries.MenuReady(run),"Managed load-cache check changed the pre-mission front end");
            log("loadcache-complete runtime=UnityMono assetFirstLoad=true samePathAssembly=true type=true mvid=true sharedSentinel=true"+
                " sentinelRestored=true actualApiSharedSameAssembly=true actualApiSharedSameType=true apiStaticWrites=0 missionStarted=false");
        }
        private static string LoadCacheSha256(string path)
        {
            using(var input=File.OpenRead(path))using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-",string.Empty);
        }
        private static bool LoadCachePathEquals(string left,string right)=>string.Equals(Path.GetFullPath(left),Path.GetFullPath(right),
            Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    }
}
