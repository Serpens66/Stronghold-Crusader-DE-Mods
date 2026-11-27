// Isolated native boundary. The production callback operates on a fake manager and register frame.
using System;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;

namespace Microsoft.Extensions.Logging
{
    internal sealed class LoggerFactory
    {
        internal object CreateLogger(string name) => new object();
    }
}

namespace SHCDESE.BepInEx.Bootstrap
{
    internal sealed class Plugin
    {
        internal static Plugin Instance { get; } = new Plugin();
        internal Microsoft.Extensions.Logging.LoggerFactory LoggerFactory { get; } = new Microsoft.Extensions.Logging.LoggerFactory();
    }
}

namespace SHCDESE.GameGlobals
{
    internal sealed class GameGlobalsManager
    {
        internal static GameGlobalsManager Instance { get; } = new GameGlobalsManager();
        internal ulong MessageManagerVA;
    }
}

namespace RedBird.Core.Memory
{
    internal readonly unsafe struct NativePointer<T> where T : unmanaged
    {
        internal NativePointer(T* pointer) => Pointer = pointer;
        internal T* Pointer { get; }
    }

    internal sealed class ScanRegion
    {
        private readonly byte[] image;
        internal ScanRegion(byte[] image) => this.image = image;
        internal ReadOnlySpan<byte> Span => image;
    }
}

namespace RedBird.Abstractions.Hooks
{
    internal enum HookState { Prepared, Failed }
}

namespace RedBird.X64.Assembly
{
    internal struct X64SmartCPUContext { internal ulong RBX, RDI; }
    internal enum X64SmartCPUContextRegs { All }
    internal enum OverwrittenInstructionPlacement { AfterCallback }
}

namespace RedBird.X64.Hooks
{
    internal sealed class X64InlineHook : IDisposable
    {
        private readonly ulong address;
        internal X64InlineHook(ulong address, int length, object logger, string name)
        {
            this.address = address;
            DisplacedByteCount = length;
        }

        internal HookState State => HookState.Prepared;
        internal bool IsInstalled { get; private set; }
        internal IntPtr StubAddress { get; private set; }
        internal int DisplacedByteCount { get; }

        internal void Generate(Action<object, object, ulong> generate)
        {
            generate(null, null, address + (uint)DisplacedByteCount);
            StubAddress = new IntPtr(1);
        }

        internal void Enable() => IsInstalled = true;
        public void Dispose() => IsInstalled = false;
    }
}

namespace RedBird.X64.Hooks.Context
{
    internal enum CallbackErrorMode { LogAndContinue }

    internal sealed class ContextHookOptions
    {
        internal X64SmartCPUContextRegs Registers;
        internal int HookSize;
        internal OverwrittenInstructionPlacement Placement;
        internal CallbackErrorMode ErrorMode;
    }

    internal static unsafe class ContextStub
    {
        private static Action<NativePointer<X64SmartCPUContext>> apply;
        internal static Action<NativePointer<X64SmartCPUContext>> Guard(Action<NativePointer<X64SmartCPUContext>> callback,
            ContextHookOptions options, ulong address, object logger, string name) => callback;
        internal static Action<object, object, ulong> CreateGenerator(X64InlineHook hook,
            Action<NativePointer<X64SmartCPUContext>> callback, ContextHookOptions options) =>
            (_, _, _) => apply = callback;

        internal static ulong RunAgeGate(ulong manager, uint now)
        {
            var state = new X64SmartCPUContext { RBX = manager, RDI = now };
            apply(new NativePointer<X64SmartCPUContext>(&state));
            return state.RDI;
        }
    }
}

namespace Shared
{
    internal static class NativePatchGuard
    {
        internal static void RequireUnpatched(ulong address, int length, string name, string feature) { }
        internal static void RequireUnpatched(X64InlineHook hook, string feature) { }
        internal static void Watch(X64InlineHook hook, string feature) { }
    }
}

namespace BugfixesAndQoL
{
    internal enum NotificationSkipSurface { Video, Minimap }

    internal static class NotificationSkipBehavior
    {
        internal static void Configure(NotificationSkipFeature feature) { }
    }
}
