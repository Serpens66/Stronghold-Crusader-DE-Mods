using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.Interop;
using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;

namespace BugfixesAndQoL
{
    // The Extender already detours getAIVData. Use the same managed chain so its asset
    // and AIC overrides still run, and only replace the game-owned vanilla array.
    internal sealed class Snake2StockpileAivFix
    {
        private const int Snake2Variant = 1;
        private const int StockpileOffsetIndex = 8;
        private const short DiagonalStockpile = 5955;
        private const short AdjacentStockpile = 5855;
        private const string OriginalHash = "8B7AFD53BBDDD634B0844F8443600E0AF43CB8A816C438C64AEA04B7B2CEF112";
        private delegate short[] LoadAiv(int lord, int variant, bool community, bool historical);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly FieldInfo vanillaSnakeBank;
        private readonly LoadAiv original;
        private readonly Hook hook;
        private int reportedChangedPlan;

        internal Snake2StockpileAivFix(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            vanillaSnakeBank = typeof(AIVLoader).GetField("snake", BindingFlags.NonPublic | BindingFlags.Static);
            if (vanillaSnakeBank?.FieldType != typeof(short[][]))
                throw new InvalidOperationException("The vanilla Snake AIV bank is unavailable.");

            Hook candidate = new Hook(((LoadAiv)AIVLoader.getAIVData).Method, (LoadAiv)Load,
                new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.Snake2StockpileAiv" });
            try
            {
                original = candidate.GenerateTrampoline<LoadAiv>();
                candidate.Apply();
                hook = candidate;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
        }

        private short[] Load(int lord, int variant, bool community, bool historical)
        {
            short[] data = original(lord, variant, community, historical);
            if (community || historical || !IsEnabled(lord, variant))
                return data;

            return Apply(data, GetBuiltIn());
        }

        // CastlePlanner has already looked for an asset override before consulting this
        // path. Avoid invoking the Extender loader here: it also applies AIC overrides.
        internal short[] GetCorrection(int lord, int variant)
        {
            if (!IsEnabled(lord, variant)) return null;
            short[] builtIn = GetBuiltIn();
            short[] result = Apply(builtIn, builtIn);
            return ReferenceEquals(result, builtIn) ? null : result;
        }

        private bool IsEnabled(int lord, int variant) =>
            lord == (int)Enums.AILords.SK_SNAKE - 1 && variant == Snake2Variant &&
            settings.EnableMod && settings.EnableSnake2StockpileAivFix;

        private short[] GetBuiltIn()
        {
            var bank = (short[][])vanillaSnakeBank.GetValue(null);
            return bank != null && bank.Length > Snake2Variant ? bank[Snake2Variant] : null;
        }

        private short[] Apply(short[] data, short[] builtIn)
        {
            short[] result = Select(data, builtIn, enabled: true, out bool changedVanilla);
            if (changedVanilla && Interlocked.Exchange(ref reportedChangedPlan, 1) == 0)
                Shared.DebugLogHelper.LogWarningLimited(log, "snake2-aiv-layout-changed",
                    "Snake2 AIV stockpile fix skipped because the vanilla layout differs from the audited plan.");
            return result;
        }

        internal static short[] Select(short[] loaded, short[] builtIn, bool enabled, out bool changedVanilla)
        {
            changedVanilla = false;
            if (!enabled || loaded == null || !ReferenceEquals(loaded, builtIn))
                return loaded;

            if (!MatchesAuditedPlan(loaded))
            {
                changedVanilla = true;
                return loaded;
            }

            var copy = (short[])loaded.Clone();
            copy[StockpileOffsetIndex] = AdjacentStockpile;
            return copy;
        }

        private static bool MatchesAuditedPlan(short[] words)
        {
            if (words.Length != 887 || words[StockpileOffsetIndex - 1] != (short)eMappers.MAPPER_STORES ||
                words[StockpileOffsetIndex] != DiagonalStockpile)
                return false;

            var bytes = new byte[words.Length * sizeof(short)];
            Buffer.BlockCopy(words, 0, bytes, 0, bytes.Length);
            using (SHA256 sha = SHA256.Create())
                return string.Equals(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty),
                    OriginalHash, StringComparison.Ordinal);
        }
    }
}
