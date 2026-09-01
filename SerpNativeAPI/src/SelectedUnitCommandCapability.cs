using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SerpNativeAPI
{
    internal readonly struct NativeSelectedUnitCommandArguments
    {
        public NativeSelectedUnitCommandArguments(IntPtr unitManager, int tribeId, int command, int argument1, int argument2, int argument3)
        {
            UnitManager = unitManager;
            TribeId = tribeId;
            Command = command;
            Argument1 = argument1;
            Argument2 = argument2;
            Argument3 = argument3;
        }

        public IntPtr UnitManager { get; }
        public int TribeId { get; }
        public int Command { get; }
        public int Argument1 { get; }
        public int Argument2 { get; }
        public int Argument3 { get; }
        public SelectedUnitCommandContext ToPublic() => new SelectedUnitCommandContext(TribeId, Command, Argument1, Argument2, Argument3);
    }

    internal interface ISelectedUnitCommandHook
    {
    }

    internal interface ISelectedUnitCommandHookFactory
    {
        ISelectedUnitCommandHook Install(long targetAddress, SelectedUnitCommandBroker broker);
    }

    internal sealed class SelectedUnitCommandBroker
    {
        private readonly object sync = new object();
        private readonly SortedDictionary<string, Registration> registrations =
            new SortedDictionary<string, Registration>(StringComparer.Ordinal);
        private readonly string binaryHash;
        private readonly ManualLogSource log;

        public SelectedUnitCommandBroker(string binaryHash, ManualLogSource log)
        {
            this.binaryHash = binaryHash;
            this.log = log;
        }

        public ISelectedUnitCommandRegistration Register(string ownerGuid, Action<SelectedUnitCommandContext> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            lock (sync)
            {
                if (registrations.TryGetValue(ownerGuid, out Registration existing))
                    return existing;
                var registration = new Registration(this, ownerGuid, callback);
                registrations.Add(ownerGuid, registration);
                return registration;
            }
        }

        public int Dispatch(NativeSelectedUnitCommandArguments arguments, Func<NativeSelectedUnitCommandArguments, int> original)
        {
            Registration[] snapshot;
            lock (sync)
            {
                var active = new List<Registration>();
                foreach (Registration registration in registrations.Values)
                    if (registration.IsEnabled)
                        active.Add(registration);
                snapshot = active.ToArray();
            }

            SelectedUnitCommandContext context = arguments.ToPublic();
            foreach (Registration registration in snapshot)
            {
                try { registration.Invoke(context); }
                catch (Exception ex)
                {
                    NativeApiLog.Error(log, $"capability={NativeCapabilityIds.SelectedUnitCommand}, build={binaryHash}, owner={registration.OwnerGuid}, callbackError={ex}");
                }
            }
            return original(arguments);
        }

        private void Remove(Registration registration)
        {
            lock (sync)
            {
                if (registrations.TryGetValue(registration.OwnerGuid, out Registration current) &&
                    ReferenceEquals(current, registration))
                {
                    registrations.Remove(registration.OwnerGuid);
                }
            }
        }

        private sealed class Registration : ISelectedUnitCommandRegistration
        {
            private readonly SelectedUnitCommandBroker owner;
            private readonly Action<SelectedUnitCommandContext> callback;
            private bool enabled = true;
            private bool disposed;

            public Registration(SelectedUnitCommandBroker owner, string ownerGuid, Action<SelectedUnitCommandContext> callback)
            {
                this.owner = owner;
                OwnerGuid = ownerGuid;
                this.callback = callback;
            }

            public string OwnerGuid { get; }
            public bool IsEnabled { get { lock (owner.sync) return enabled && !disposed; } }
            public void Enable() { lock (owner.sync) { if (!disposed) enabled = true; } }
            public void Disable() { lock (owner.sync) enabled = false; }
            public void Dispose()
            {
                lock (owner.sync)
                {
                    if (disposed)
                        return;
                    enabled = false;
                    disposed = true;
                }
                owner.Remove(this);
            }
            public void Invoke(SelectedUnitCommandContext context) => callback(context);
        }
    }

    internal sealed class NativeDetourSelectedUnitCommandHookFactory : ISelectedUnitCommandHookFactory
    {
        public ISelectedUnitCommandHook Install(long targetAddress, SelectedUnitCommandBroker broker) =>
            new Hook(targetAddress, broker);

        private sealed class Hook : ISelectedUnitCommandHook
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            private delegate int CommandDelegate(IntPtr unitManager, int tribeId, int command, int argument1, int argument2, int argument3);

            private readonly SelectedUnitCommandBroker broker;
            private readonly CommandDelegate rootedDetour;
            private readonly CommandDelegate original;
            private readonly NativeDetour detour;

            public Hook(long targetAddress, SelectedUnitCommandBroker broker)
            {
                this.broker = broker;
                rootedDetour = OnCommand;
                NativeDetour created = null;
                try
                {
                    created = new NativeDetour(
                        new IntPtr(targetAddress),
                        Marshal.GetFunctionPointerForDelegate(rootedDetour),
                        new NativeDetourConfig { ManualApply = true });
                    original = created.GenerateTrampoline<CommandDelegate>();
                    created.Apply();
                    detour = created;
                }
                catch
                {
                    created?.Dispose();
                    throw;
                }
            }

            private int OnCommand(IntPtr unitManager, int tribeId, int command, int argument1, int argument2, int argument3)
            {
                var args = new NativeSelectedUnitCommandArguments(unitManager, tribeId, command, argument1, argument2, argument3);
                return broker.Dispatch(args, InvokeOriginal);
            }

            private int InvokeOriginal(NativeSelectedUnitCommandArguments args) =>
                original(args.UnitManager, args.TribeId, args.Command, args.Argument1, args.Argument2, args.Argument3);
        }
    }

    internal sealed class SelectedUnitCommandService
    {
        private readonly object sync = new object();
        private readonly string binaryHash;
        private readonly long targetAddress;
        private readonly NativeInterval[] intervals;
        private readonly NativeOwnershipRegistry ownership;
        private readonly ISelectedUnitCommandHookFactory hookFactory;
        private readonly SelectedUnitCommandBroker broker;
        private readonly ManualLogSource log;
        private ISelectedUnitCommandHook hook;

        public SelectedUnitCommandService(
            string binaryHash,
            long targetAddress,
            int targetLength,
            NativeOwnershipRegistry ownership,
            ISelectedUnitCommandHookFactory hookFactory,
            ManualLogSource log)
        {
            this.binaryHash = binaryHash;
            this.targetAddress = targetAddress;
            intervals = new[] { new NativeInterval(targetAddress, targetAddress + targetLength) };
            this.ownership = ownership;
            this.hookFactory = hookFactory;
            this.log = log;
            broker = new SelectedUnitCommandBroker(binaryHash, log);
        }

        public ISelectedUnitCommandCapability Bind(string ownerGuid) => new OwnerCapability(this, ownerGuid);

        private bool TryRegister(
            string ownerGuid,
            Action<SelectedUnitCommandContext> callback,
            out ISelectedUnitCommandRegistration registration,
            out NativeCapabilityDiagnostic diagnostic)
        {
            registration = null;
            if (callback == null)
            {
                diagnostic = Diagnostic(NativeCapabilityState.ValidationFailed, "The selected-unit callback is null.");
                return false;
            }

            lock (sync)
            {
                if (!ownership.TryReserve(ownerGuid, NativeCapabilityIds.SelectedUnitCommand, NativeReservationMode.SharedHook, intervals, out string conflictOwner))
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.SelectedUnitCommand,
                        NativeCapabilityState.Conflict,
                        binaryHash,
                        "The selected-unit command target conflicts with an exclusive reservation.",
                        conflictOwner);
                    return false;
                }

                try
                {
                    if (hook == null)
                    {
                        hook = hookFactory.Install(targetAddress, broker);
                        NativeApiLog.Info(log, $"capability={NativeCapabilityIds.SelectedUnitCommand}, build={binaryHash}, owner={ownerGuid}, status=detour-installed.");
                    }
                    registration = broker.Register(ownerGuid, callback);
                    diagnostic = Diagnostic(NativeCapabilityState.Available, "The selected-unit Before callback is registered.");
                    return true;
                }
                catch (Exception ex)
                {
                    diagnostic = Diagnostic(NativeCapabilityState.Faulted, ex.Message);
                    NativeApiLog.Error(log, $"capability={NativeCapabilityIds.SelectedUnitCommand}, build={binaryHash}, owner={ownerGuid}, status=failed, error={ex}");
                    return false;
                }
            }
        }

        private NativeCapabilityDiagnostic Diagnostic(NativeCapabilityState state, string reason) =>
            new NativeCapabilityDiagnostic(NativeCapabilityIds.SelectedUnitCommand, state, binaryHash, reason);

        private sealed class OwnerCapability : ISelectedUnitCommandCapability
        {
            private readonly SelectedUnitCommandService service;
            private readonly string ownerGuid;
            public OwnerCapability(SelectedUnitCommandService service, string ownerGuid) { this.service = service; this.ownerGuid = ownerGuid; }
            public bool TryRegisterBefore(Action<SelectedUnitCommandContext> callback, out ISelectedUnitCommandRegistration registration, out NativeCapabilityDiagnostic diagnostic) =>
                service.TryRegister(ownerGuid, callback, out registration, out diagnostic);
        }
    }
}
