using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API;
using UnityEngine;

namespace ProjectileTrailsTest
{
    [BepInPlugin(Guid, "Projectile trails acceptance", "0.5.0")]
    [BepInDependency(MainModGuid, "1.0.237")]
    [BepInDependency("APIShared_Serp", "0.62.0")]
    [BepInDependency("000shcdese", "2.14.4")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        internal const string Guid = "ProjectileTrailsTest_Serp";
        private const string MainModGuid = "BugfixesAndQoL_Serp";
        private const int FullLength = 10;
        private const int FullMaximum = 60000;
        private const int DefaultLength = 5;
        private const int DefaultMaximum = 10000;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo VanillaFlies = typeof(GameMap).GetField("flies", InstanceMembers);
        private static ManualLogSource log;
        private static Type runtime;
        private static object settings;
        private static MemberInfo segmentCount, extremeMode, standardReplacement, renderedTick, meshState;
        private static MethodInfo getAccepted, opacity;
        private static FieldInfo echoTick, echoPosition, echoSortOrder;

        private void Awake()
        {
            log = Logger;
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ =>
            {
                if (!api.Ui.TryPost(() => StartAutomation(api)))
                    WriteInfo("APIShared could not dispatch projectile trail acceptance.");
            });
        }

        private static void BindMainMod()
        {
            Require(Chainloader.PluginInfos.TryGetValue(MainModGuid, out var plug), "BugfixesAndQoL is not loaded.");
            var owner = plug.Instance;
            runtime = owner.GetType().Assembly.GetType("BugfixesAndQoL.ProjectileTrails.ProjectileTrailRuntime", throwOnError: true);
            settings = owner.GetType().GetProperty("Settings", InstanceMembers)?.GetValue(owner);
            Require(settings != null, "BugfixesAndQoL settings are not ready.");
            segmentCount = BindRead("LastSegmentCount");
            extremeMode = BindRead("SessionExtreme");
            standardReplacement = BindRead("replaceStandard");
            renderedTick = BindRead("lastTick");
            meshState = BindRead("meshes");
            Require(VanillaFlies?.FieldType == typeof(Dictionary<int, Fly>), "The Vanilla flight dictionary layout changed.");
        }

        private static MemberInfo BindRead(string name) =>
            (MemberInfo)runtime.GetProperty(name, StaticMembers) ?? runtime.GetField(name, StaticMembers) ??
                throw new MissingMemberException(runtime.FullName, name);

        private static T Read<T>(MemberInfo member) =>
            (T)(member is PropertyInfo property ? property.GetValue(null) : ((FieldInfo)member).GetValue(null));

        private static bool SessionExtreme => Read<bool>(extremeMode);
        private static bool ReplaceStandard => Read<bool>(standardReplacement);
        private static int LastSegmentCount => Read<int>(segmentCount);
        private static uint MapTick => unchecked((uint)GameTimeManagerAPI.Instance.GetElapsedMapTicks());

        private static void TraceModEchoes(uint tick, int count)
        {
            var renderer = Read<object>(meshState);
            if (getAccepted == null)
            {
                getAccepted = renderer.GetType().GetMethod("GetAccepted", InstanceMembers);
                Require(getAccepted != null, "The production renderer does not expose the accepted echo snapshot.");
                var sample = getAccepted.ReturnType;
                echoTick = sample.GetField("Tick", InstanceMembers);
                echoPosition = sample.GetField("Position", InstanceMembers);
                echoSortOrder = sample.GetField("SortKey", InstanceMembers);
                opacity = sample.DeclaringType.GetMethod("Opacity", StaticMembers);
                Require(opacity != null && echoTick != null && echoPosition != null && echoSortOrder != null,
                    "The production echo snapshot layout changed.");
            }
            uint published = Read<uint>(renderedTick);
            var oneIndex = new object[1];
            var alphaArgs = new object[] { 0u, FullLength };
            for (int i = 0; i < count; i++)
            {
                oneIndex[0] = i;
                var echo = getAccepted.Invoke(renderer, oneIndex);
                var point = (System.Numerics.Vector3)echoPosition.GetValue(echo);
                alphaArgs[0] = unchecked(published - (uint)echoTick.GetValue(echo));
                LogEcho("replacement", tick, new Vector3(point.X, point.Y, point.Z),
                    unchecked((int)(long)echoSortOrder.GetValue(echo)), (float)opacity.Invoke(null, alphaArgs));
            }
        }

        private static void SetOption<T>(string name, T value)
        {
            var property = settings.GetType().GetProperty(name, InstanceMembers);
            Require(property?.PropertyType == typeof(T) && property.CanWrite, "Missing BugfixesAndQoL option " + name);
            property.SetValue(settings, value);
            Require(Equals(property.GetValue(settings), value), "BugfixesAndQoL rejected test option " + name);
        }

        private static void Configure(bool replace, bool enabled, int length, int maximum)
        {
            SetOption("EnableMod", true);
            SetOption("EnableClientFeatures", true);
            SetOption("ReplaceStandardProjectileAfterimages", replace);
            SetOption("EnableProjectileTrails", enabled);
            SetOption("ProjectileTrailLength", length);
            SetOption("ProjectileTrailMaximum", maximum);
        }

        private static void WriteInfo(string message) =>
            log.LogInfo(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + message);
    }
}
