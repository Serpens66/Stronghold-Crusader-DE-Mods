using System;
using System.Collections.Generic;
using System.Globalization;
using APIShared;
using APIShared.Presentation;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace LordMessageReplacementTest
{
    [BepInPlugin(PluginGuid, "Lord Message Replacement Test", PluginVersion)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.47.0")]
    public sealed class LordMessageReplacementTestPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "LordMessageReplacementTest_Serp";
        public const string PluginVersion = "0.1.0";
        private static ReplacementRuntime runtime;

        private void Awake()
        {
            bool enabled = Config.Bind("General", "Enabled", true,
                "Replace generic allied messenger messages. Read at startup; restart the game after changing it.").Value;
            runtime = new ReplacementRuntime(Logger, enabled);
            ReplacementRuntime current = runtime;
            ApiShared.ForMod(PluginGuid).WhenReady(client => {
                if (!client.Ui.TryPost(current.Initialize)) current.Error("LORD_MESSAGE_TEST_DISPATCH_UNAVAILABLE");
            });
        }
    }

    // The static plugin field and the public process-owned broker retain this runtime
    // after SHCDE destroys early plugin components. No Update or teardown is involved.
    internal sealed class ReplacementRuntime
    {
        private readonly ManualLogSource log;
        private readonly bool enabled;
        private readonly Dictionary<long, Rule> rules = new Dictionary<long, Rule>();
        private readonly HashSet<Rule> confirmed = new HashSet<Rule>();
        private LordMessageRegistration registration;
        private bool initialized;

        internal ReplacementRuntime(ManualLogSource log, bool enabled) { this.log = log; this.enabled = enabled; }
        private static long Key(AILords lord, AILordMessageType type) => ((long)(uint)lord << 32) | (uint)type;

        internal void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                if (!VanillaLordMessages.Available) throw new InvalidOperationException(VanillaLordMessages.Diagnostic);
                foreach (AILords lord in new[] { AILords.SK_RAT, AILords.SK_SNAKE, AILords.SK_PIG, AILords.SK_WOLF })
                {
                    if (!VanillaLordMessages.TryGet(lord, AILordMessageType.Taunt1, out var donor, out string reason))
                        throw new InvalidOperationException(lord + ": " + reason);
                    foreach (AILordMessageType type in Enum.GetValues(typeof(AILordMessageType)))
                    {
                        if (!ReplacementPolicy.IsAlliedCategory(type)) continue;
                        if (!VanillaLordMessages.TryGet(lord, type, out var original, out reason))
                            throw new InvalidOperationException(lord + "/" + type + ": " + reason);
                        if (ReplacementPolicy.ShouldReplace(lord, type, original.AudioPath, original.VideoPath,
                            original.AudioPath, original.VideoPath)) rules.Add(Key(lord, type), new Rule(original, donor));
                    }
                }
                if (!LordMessageEvents.TryRegister(LordMessageReplacementTestPlugin.PluginGuid, "allied-placeholders",
                    BeforeMessage, AfterMessage, out registration, out string registrationReason))
                    throw new InvalidOperationException(registrationReason);
                registration.SetEnabled(enabled);
                Info("LORD_MESSAGE_TEST_READY enabled=" + enabled + ",rules=" + rules.Count +
                    ",donor=Taunt1; only actual messages, no preview or synthetic requests.");
            }
            catch (Exception error) { Error("LORD_MESSAGE_TEST_UNAVAILABLE " + error); }
        }

        private void BeforeMessage(LordMessagePreEventArgs args)
        {
            var context = args.Context;
            if (!context.VanillaLord.HasValue || !rules.TryGetValue(Key(context.VanillaLord.Value, context.MessageType), out var rule)) return;
            if (!ReplacementPolicy.ShouldReplace(rule.Original.Lord, rule.Original.MessageType,
                rule.Original.AudioPath, rule.Original.VideoPath, context.OriginalAudioPath, context.OriginalVideoPath)) return;
            // Failure of any part rolls back this complete callback through the public broker.
            if (!args.TryReplaceAudio(rule.Audio) || !args.TryReplaceVideo(rule.Video) || !args.TryReplaceText(rule.Text))
                throw new InvalidOperationException("The prepared Vanilla message reference is unavailable.");
            args.State = rule;
        }

        private void AfterMessage(LordMessagePostEventArgs args)
        {
            if (!(args.State is Rule rule) || !confirmed.Add(rule)) return;
            bool selected = ReferenceEquals(args.Audio, rule.Audio) && ReferenceEquals(args.Video, rule.Video) && ReferenceEquals(args.Text, rule.Text);
            // At most one support confirmation per prepared Lord/category pair per process.
            // This is submission evidence, never a claim that hardware played the clip.
            Info("LORD_MESSAGE_TEST_SUBMITTED lord=" + rule.Original.Lord + ",source=" + rule.Original.MessageType +
                ",donor=Taunt1,selected=" + selected + ",invocation=" + args.Context.InvocationId +
                ",header=" + args.Context.HeaderIndex + "; audible/video acceptance remains separate.");
        }

        private sealed class Rule
        {
            internal readonly VanillaLordMessage Original;
            internal readonly LordMessageAudio Audio;
            internal readonly LordMessageVideo Video;
            internal readonly LordMessageText Text;
            internal Rule(VanillaLordMessage original, VanillaLordMessage donor)
            {
                Original = original;
                Audio = LordMessageAudio.FromVanilla(donor);
                Video = LordMessageVideo.FromVanilla(donor);
                Text = LordMessageText.FromVanilla(donor);
            }
        }

        private void Info(string message) => log.LogInfo(Stamp(message));
        internal void Error(string message) => log.LogError(Stamp(message));
        private static string Stamp(string message) => "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + message;
    }
}
