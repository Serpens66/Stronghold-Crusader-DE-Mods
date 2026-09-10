namespace APIShared
{
    /// <summary>Immutable arguments of one native AIV build-step invocation.</summary>
    public sealed class AivBuildStepContext
    {
        /// <summary>Creates an AIV build-step context.</summary>
        public AivBuildStepContext(ulong aivStateAddress, int playerId, int frameIndex, int restrictedMode, byte freeOrForced)
        {
            AivStateAddress = aivStateAddress;
            PlayerId = playerId;
            FrameIndex = frameIndex;
            RestrictedMode = restrictedMode;
            FreeOrForced = freeOrForced;
        }

        /// <summary>Native AIV runtime-state address.</summary>
        public ulong AivStateAddress { get; }
        /// <summary>Player identifier supplied by Vanilla.</summary>
        public int PlayerId { get; }
        /// <summary>Prepared AIV frame index.</summary>
        public int FrameIndex { get; }
        /// <summary>Vanilla restricted-placement mode.</summary>
        public int RestrictedMode { get; }
        /// <summary>Vanilla free-or-forced placement selector.</summary>
        public byte FreeOrForced { get; }
    }

    /// <summary>Immutable completion state for one AIV build-step invocation.</summary>
    public sealed class AivBuildStepCompletion
    {
        /// <summary>Creates an AIV build-step completion.</summary>
        public AivBuildStepCompletion(AivBuildStepContext context, bool vanillaCompleted, int vanillaResult)
        {
            Context = context;
            VanillaCompleted = vanillaCompleted;
            VanillaResult = vanillaResult;
        }

        /// <summary>The corresponding immutable invocation context.</summary>
        public AivBuildStepContext Context { get; }
        /// <summary>Whether the single Vanilla call returned normally.</summary>
        public bool VanillaCompleted { get; }
        /// <summary>The unchanged Vanilla result, or zero when Vanilla did not complete.</summary>
        public int VanillaResult { get; }
    }

    /// <summary>Owner-created per-call state completed after Vanilla returns or aborts.</summary>
    public interface IAivBuildStepInvocation
    {
        /// <summary>Completes this invocation exactly once.</summary>
        void Complete(AivBuildStepCompletion completion);
    }

    /// <summary>Observer called before the single Vanilla build-step invocation.</summary>
    public interface IAivBuildStepObserver
    {
        /// <summary>Returns per-call state to complete, or null when this call is not observed.</summary>
        IAivBuildStepInvocation TryBegin(AivBuildStepContext context);
    }

    /// <summary>Owner-bound process-wide AIV build-step observer service.</summary>
    public interface IAivBuildStepCapability
    {
        /// <summary>Registers one process-lifetime observer under an owner-local stable ID.</summary>
        bool TryRegisterObserver(string registrationId, IAivBuildStepObserver observer, out NativeCapabilityDiagnostic diagnostic);
    }
}
