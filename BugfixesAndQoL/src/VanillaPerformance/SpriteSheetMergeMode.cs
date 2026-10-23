using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL
{
    // Local V7 sprite-sheet merge choice. Automatic decides on this machine's graphics memory
    // when the sprites are loaded; the stored value stays machine independent.
    [MessagePackFormatter(typeof(SpriteSheetMergeModeFormatter))]
    public enum SpriteSheetMergeMode
    {
        Automatic = 0,
        On = 1,
        Off = 2
    }

    public sealed class SpriteSheetMergeModeFormatter : IMessagePackFormatter<SpriteSheetMergeMode>
    {
        public void Serialize(ref MessagePackWriter writer, SpriteSheetMergeMode value, MessagePackSerializerOptions options)
        {
            if (!SpriteSheetMergePolicy.IsDefinedMode(value))
                throw new MessagePackSerializationException($"Unknown sprite-sheet merge mode [{(int)value}].");
            writer.Write((int)value);
        }

        public SpriteSheetMergeMode Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.NextMessagePackType != MessagePackType.Integer)
                throw new MessagePackSerializationException("Sprite-sheet merge mode must be an integer.");
            int rawValue = reader.ReadInt32();
            var value = (SpriteSheetMergeMode)rawValue;
            if (!SpriteSheetMergePolicy.IsDefinedMode(value))
                throw new MessagePackSerializationException($"Unknown sprite-sheet merge mode [{rawValue}].");
            return value;
        }
    }

    public static class SpriteSheetMergePolicy
    {
        // User decision 2026-10-22: Automatic merges only with at least 8 GB graphics memory.
        public const int AutomaticMinimumGraphicsMemoryMb = 8192;
        // Technical floor for an explicit "On": Vanilla needs about 2.4 GB, the merge adds about 1.6 GB.
        public const int MinimumGraphicsMemoryMb = 6144;

        public static bool IsDefinedMode(SpriteSheetMergeMode mode) =>
            mode == SpriteSheetMergeMode.Automatic || mode == SpriteSheetMergeMode.On || mode == SpriteSheetMergeMode.Off;

        // Returns null when the merge should run, otherwise the reason it is skipped.
        public static string Refusal(SpriteSheetMergeMode mode, int graphicsMemoryMb)
        {
            if (mode == SpriteSheetMergeMode.Off) return "disabled in the settings";
            int required = mode == SpriteSheetMergeMode.Automatic ? AutomaticMinimumGraphicsMemoryMb : MinimumGraphicsMemoryMb;
            return graphicsMemoryMb < required
                ? "graphics memory " + graphicsMemoryMb + " MB < " + required + " MB (" + mode + ")"
                : null;
        }
    }
}
