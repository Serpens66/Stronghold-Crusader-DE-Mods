using SHCDESE.Interop;

namespace ImprovedHunters
{
    /// <summary>
    /// Read-only snapshot used to correlate Hunter movement, animation and AI
    /// transitions at already validated native callback sites.
    /// </summary>
    internal static unsafe class HunterMovementSnapshot
    {
        private const int LocomotionControlOffset = 0x4;
        private const int HunterPathStateOffset = 0xF2;
        private const int HunterPathFieldF4Offset = 0xF4;
        private const int HunterPathProgressOffset = 0xF6;
        private const int HunterPathLengthOffset = 0xF8;
        private const int HunterAiStateOffset = 0x2BC;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int HunterPathAdvanceControlOffset = 0x3F0;

        public static string TryFormat(GameUnit* hunter)
        {
            if (hunter == null)
                return "snapshot=unavailable";

            try
            {
                byte* hunterBytes = (byte*)hunter;
                return
                    $"animationFrame={hunter->r_AnimationFrame}, " +
                    $"locomotionControl=0x{*(uint*)(hunterBytes + LocomotionControlOffset):X}, " +
                    $"spriteAnimationFrame={hunter->r_CurrentSpriteAnimationFrame}, " +
                    $"animationTimer={hunter->r_AnimationTimer}, " +
                    $"speed={hunter->r_CurrentSpeed}, speed2={hunter->r_CurrentSpeed2}, " +
                    $"aiState={*(ushort*)(hunterBytes + HunterAiStateOffset)}, " +
                    $"transformInto={hunter->r_TransformIntoUnitOfType}, direction={(int)hunter->r_Direction}, " +
                    $"target={*(ushort*)(hunterBytes + HunterTargetUnitIdOffset)}/" +
                    $"{*(uint*)(hunterBytes + HunterTargetGlobalIdOffset)}, " +
                    $"path={*(ushort*)(hunterBytes + HunterPathStateOffset)}/" +
                    $"{*(ushort*)(hunterBytes + HunterPathFieldF4Offset)}/" +
                    $"{*(ushort*)(hunterBytes + HunterPathProgressOffset)}/" +
                    $"{*(uint*)(hunterBytes + HunterPathLengthOffset)}, " +
                    $"advanceControl=0x{*(ushort*)(hunterBytes + HunterPathAdvanceControlOffset):X}, " +
                    $"tile={hunter->r_CurrentTilePositionX},{hunter->r_CurrentTilePositionY}, " +
                    $"world={hunter->r_CurrentWorldPositionX},{hunter->r_CurrentWorldPositionY}";
            }
            catch
            {
                // Diagnostics must never suppress Vanilla or an independent fix.
                return "snapshot=failed";
            }
        }
    }
}
