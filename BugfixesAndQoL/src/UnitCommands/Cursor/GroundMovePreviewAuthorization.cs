using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.UnitCommands
{
    internal readonly struct GroundMoveFeedback
    {
        internal readonly int Player, Tribe, Count, Mode, X, Y, Kind, File, Image,
            Command, CommandDetail, HoveredUnit, HoveredBuilding, Wall;
        internal GroundMoveFeedback(int player, int tribe, int count, int mode,
            int x, int y, int kind, int file, int image, int command, int commandDetail,
            int hoveredUnit = 0, int hoveredBuilding = 0, int wall = 0)
        {
            Player = player; Tribe = tribe; Count = count; Mode = mode; X = x; Y = y;
            Kind = kind; File = file; Image = image; Command = command;
            CommandDetail = commandDetail; HoveredUnit = hoveredUnit;
            HoveredBuilding = hoveredBuilding; Wall = wall;
        }
        // Terminal ordinary-Move proof remains authoritative over unit hover.
        internal bool FormationMoveAllowed => Mode == 1 && Kind == 3 && File == 0x6B &&
            ((Image == 0 && Command == 1) || (Image == 0x20 && Command == 9)) &&
            CommandDetail == 0;
        internal bool GroundAllowed => Kind == 3 && File == 0x6B &&
            (Image == 0 || Image == 0x20) && (Command == 1 || Command == 9) &&
            CommandDetail == 0 && HoveredUnit == 0 && HoveredBuilding == 0 && Wall == 0;
    }

    internal sealed class GroundMovePreviewAuthorization
    {
        private readonly int player, tribe, count, x, y;
        private bool allowed;
        internal GroundMovePreviewAuthorization(int player, int tribe, int count, int x, int y)
        {
            this.player = player; this.tribe = tribe; this.count = count; this.x = x; this.y = y;
        }
        internal bool Observe(GroundMoveFeedback feedback)
        {
            if (feedback.Player != player || feedback.Tribe != tribe ||
                feedback.Count != count || feedback.Mode != 1)
                return allowed = false;
            // Dragging changes the hover point, never the gesture's command point.
            if (feedback.X == x && feedback.Y == y)
                allowed = feedback.GroundAllowed;
            return allowed;
        }
    }

    // Formation-only proof; the strict queue ground policy remains unchanged.
    internal sealed class FormationMoveAuthorization
    {
        private readonly int player, tribe, count, x, y;
        private readonly long startedAfterGeneration;
        private long observedGeneration;
        internal bool IsConfirmed { get; private set; }

        internal FormationMoveAuthorization(int player, int tribe, int count,
            int x, int y, long startedAfterGeneration)
        {
            this.player = player; this.tribe = tribe; this.count = count;
            this.x = x; this.y = y;
            this.startedAfterGeneration = startedAfterGeneration;
            observedGeneration = startedAfterGeneration;
        }

        internal bool Observe(GroundMoveFeedback feedback, long generation, bool coherentCursor)
        {
            if (feedback.Player != player || feedback.Tribe != tribe ||
                feedback.Count != count || feedback.Mode != 1)
                return IsConfirmed = false;
            // The confirmed press owns the command anchor. Subsequent hover,
            // even back on that tile, only supplies the gesture's facing.
            if (IsConfirmed)
                return true;
            if (generation <= startedAfterGeneration || generation <= observedGeneration)
                return IsConfirmed;
            observedGeneration = generation;
            // A later hover supplies facing, never a replacement command anchor.
            // Incoherent snapshots cannot establish or replace command proof.
            if (coherentCursor && feedback.X == x && feedback.Y == y)
                IsConfirmed = feedback.FormationMoveAllowed;
            return IsConfirmed;
        }
    }

    // Read only. Called once per existing native marker render pass, after cursor dispatch.
    internal sealed class NativeGroundMoveFeedbackReader
    {
        private readonly IntPtr module;
        internal NativeGroundMoveFeedbackReader(IntPtr module, ReadOnlySpan<byte> image)
        {
            if (module == IntPtr.Zero || image.Length < 0x88E3D74)
                throw new InvalidOperationException("Ground movement feedback image is unavailable.");
            ValidateContract(image);
            this.module = module;
        }
        internal static void ValidateContract(ReadOnlySpan<byte> image)
        {
            // Ordinary failed ground route: command detail -10 and reject sprite 0xAC/0x41.
            Check(image, 0x8F3DA, new byte[] { 0xC7,0x05,0x7C,0xE1,0x01,0x06,0xF6,0xFF,0xFF,0xFF });
            Check(image, 0x8F3EA, new byte[] { 0xC7,0x05,0x54,0xE1,0x01,0x06,0x41,0,0,0 });
            Check(image, 0x8F3FE, new byte[] { 0xC7,0x05,0x44,0xE1,0x01,0x06,0xAC,0,0,0 });
            // Terminal dispatch publishes accepted animated movement cursor kind 3.
            Check(image, 0x9002D, new byte[] { 0x41,0xBE,3,0,0,0,0x44,0x89,0x35,0x12,0x9E,0x41,3 });
            APIShared.Internal.NativeTroopCommandModeReader.ValidateContract(image);
        }
        private static void Check(ReadOnlySpan<byte> image, int rva, byte[] bytes)
        {
            if (rva > image.Length - bytes.Length || !image.Slice(rva, bytes.Length).SequenceEqual(bytes))
                throw new InvalidOperationException($"Ground movement feedback contract mismatch at 0x{rva:X}.");
        }
        private int Read(int rva) => Marshal.ReadInt32(IntPtr.Add(module, rva));
        internal GroundMoveFeedback Read() => new GroundMoveFeedback(
            Read(0x88E3D70), Read(0x7CC6720), Read(0x67E8420), Read(0x67E8410),
            Read(0x3A11E2C), Read(0x3A11E30), Read(0x34A9E4C), Read(0x60AD54C),
            Read(0x60AD548), Read(0x60AD55C), Read(0x60AD560),
            Read(0x3A11DF0), Read(0x3A11DE4), Read(0x3A11E34));
    }
}
