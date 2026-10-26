using System;
using System.IO;

namespace ExtendedData
{
    // Carries a stable failure category so export and package errors can name the actual
    // cause instead of reporting every unreadable asset as a multiplayer incompatibility.
    internal sealed class MissionAssetException : Exception
    {
        internal const string MapUnreadable = "map-unreadable";
        internal const string MapMissing = "map-missing";
        internal const string MapNotMultiplayer = "map-not-multiplayer";
        internal const string MapTooFewPlayers = "map-too-few-players";
        internal const string LordUnresolved = "lord-unresolved";
        internal const string AivMissing = "aiv-missing";
        internal const string EmbeddedTrailUnreadable = "embedded-trail-unreadable";
        internal const string EmbeddedSlotInvalid = "embedded-slot-invalid";

        internal MissionAssetException(string category, string assetPath, string message)
            : base(message)
        {
            Category = category;
            AssetPath = assetPath;
        }

        internal string Category { get; }
        internal string AssetPath { get; }
    }

    // A transient preparation conflict (for example a Lord catalogue reload); selecting the mission again resolves it.
    internal sealed class MissionRetryException : IOException
    {
        internal MissionRetryException(string message) : base(message) { }
    }

    // Identifies the exported mission that failed; the inner exception holds the cause.
    internal sealed class CoopMissionExportException : Exception
    {
        internal CoopMissionExportException(int ordinal, string sourcePath, Exception inner)
            : base("Coop mission " + ordinal + " [" + sourcePath + "] could not be exported" +
                (inner is MissionAssetException asset ? " (" + asset.Category + ")" : string.Empty) +
                ": " + inner?.Message, inner)
        {
            Ordinal = ordinal;
            SourcePath = sourcePath;
            Reason = inner?.Message ?? string.Empty;
        }

        internal int Ordinal { get; }
        internal string SourcePath { get; }
        internal string Reason { get; }
    }
}
