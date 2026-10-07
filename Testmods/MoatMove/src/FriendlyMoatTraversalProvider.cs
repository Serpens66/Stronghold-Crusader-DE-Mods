using System;
using BepInEx.Logging;
using APIShared.UnitCommands;
using SHCDESE.API.LowLevel;
namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatTraversalProvider : UnitCommandTraversalProvider
    {
        private readonly UnitCommandPathRuntime runtime;
        private readonly MoatMoveOptions settings;
        private readonly ManualLogSource log;
        private volatile bool enabled;
        internal override bool Enabled => enabled;
        internal override bool RequiredOnly => settings.GetFriendlyMoatMovementMode() == FriendlyMoatMovementMode.RequiredOnly;
        internal bool RequiredOnlyMode => RequiredOnly;
        internal override IMoatSearchKernel CreateKernel(int width, int height, MoatSearchEdge edge) => new MoatSearchKernel(width, height, edge);
        internal FriendlyMoatTraversalProvider(ManualLogSource log, MoatMoveOptions options)
        {
            this.log = log; settings = options;
            runtime = UnitCommandPathAPI.Runtime ?? throw new InvalidOperationException("Main command runtime unavailable.");
            UnitCommandPathAPI.RegisterTraversal(this);
        }
        internal void Install(CrusaderLibraryLoadContext context)
        {
            try
            {
                if (settings.NativeFast) FastNativeKernel.Initialize(context.Memory.Slice(FastNativeKernel.SourceRva, FastNativeKernel.SourceLength).ToArray(), unchecked((ulong)context.ModuleHandle.ToInt64()));
                if (settings.NativeFast) FastNativeKernel.Get(800, 800);
                enabled = true;
                InstallFastCommandRuntime(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()));
            }
            catch { enabled = false; throw; }
        }
    }
}
