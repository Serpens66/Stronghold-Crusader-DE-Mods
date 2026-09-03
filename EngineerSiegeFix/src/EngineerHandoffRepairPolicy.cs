namespace EngineerSiegeFix
{
    internal static class EngineerHandoffRepairPolicy
    {
        public const ushort CatapultType = 0x27;
        public const ushort TrebuchetType = 0x28;
        public const ushort ArabBallistaType = 0x4D;
        public const ushort IdleMainState = 0;
        public const ushort RecoveryMainState = 0x6D;
        public const uint VanillaRecoveryPackedState = 0x0005006D;
        public const ushort VanillaRecoveryVisual = 0x0200;
        public const int MaximumCoordinateDistance = 32;
        public const uint RecoveryTimeoutTicks = 512;

        public static int RequiredCrew(ushort deviceType)
        {
            if (deviceType == CatapultType || deviceType == ArabBallistaType)
                return 2;
            if (deviceType == TrebuchetType)
                return 3;
            return 0;
        }

        public static bool IsRepairableIdleEngineer(
            ushort deviceType,
            int expectedUnitId,
            uint expectedGlobalId,
            byte expectedOwner,
            int actualUnitId,
            uint actualGlobalId,
            short actualAliveState,
            ushort actualType,
            byte actualOwner,
            uint actualPackedState,
            ushort deviceWorldX,
            ushort deviceWorldY,
            ushort engineerWorldX,
            ushort engineerWorldY)
        {
            if (RequiredCrew(deviceType) == 0 || expectedUnitId <= 0 || expectedGlobalId == 0 ||
                actualUnitId != expectedUnitId || actualGlobalId != expectedGlobalId ||
                actualAliveState != EngineerHandoffDiagnosticPolicy.LiveUnitState ||
                actualType != EngineerHandoffDiagnosticPolicy.EngineerType ||
                actualOwner != expectedOwner || unchecked((ushort)actualPackedState) != IdleMainState)
            {
                return false;
            }

            return AbsoluteDifference(deviceWorldX, engineerWorldX) <= MaximumCoordinateDistance &&
                AbsoluteDifference(deviceWorldY, engineerWorldY) <= MaximumCoordinateDistance;
        }

        public static bool IsAllowedRecoveryState(uint packedState)
        {
            ushort mainState = unchecked((ushort)packedState);
            return mainState == RecoveryMainState ||
                mainState == EngineerHandoffDiagnosticPolicy.BoundCrewMainState;
        }

        private static int AbsoluteDifference(ushort left, ushort right) =>
            left >= right ? left - right : right - left;
    }
}
