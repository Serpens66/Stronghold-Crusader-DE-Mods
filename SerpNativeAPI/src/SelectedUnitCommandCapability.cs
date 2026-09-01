using BepInEx.Logging;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace SerpNativeAPI
{
    public readonly struct SelectedUnitCommandContext
    {
        public SelectedUnitCommandContext(
            int tribeId,
            TribeAICommand command,
            int targetValue1,
            int targetValue2,
            int argument6)
        {
            TribeId = tribeId;
            Command = command;
            TargetValue1 = targetValue1;
            TargetValue2 = targetValue2;
            Argument6 = argument6;
        }

        public int TribeId { get; }
        public TribeAICommand Command { get; }
        public int TargetValue1 { get; }
        public int TargetValue2 { get; }
        public int Argument6 { get; }
    }

    public interface ISelectedUnitCommandRegistration : IDisposable
    {
        bool IsEnabled { get; }
        void Enable();
        void Disable();
    }

    public interface ISelectedUnitCommandCapability
    {
        bool TryRegisterBefore(
            Action<SelectedUnitCommandContext> callback,
            out ISelectedUnitCommandRegistration registration,
            out NativeCapabilityDiagnostic diagnostic);
    }

    internal static class SelectedUnitCommandCapabilityResolver
    {
        public static void Resolve(
            string binaryHash,
            ISelectedUnitCommandEventSource eventSource,
            ManualLogSource log,
            out SelectedUnitCommandService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            try
            {
                if (eventSource == null)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The Script Extender selected-unit event source is unavailable.");
                service = new SelectedUnitCommandService(binaryHash, eventSource, log);
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.SelectedUnitCommand,
                    NativeCapabilityState.Available,
                    binaryHash,
                    "Provided through the Script Extender OnTribeIssueOrderWithTarget Pre event; SerpNativeAPI installs no native detour.");
            }
            catch (NativeResolutionException ex)
            {
                diagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.SelectedUnitCommand, ex.State, binaryHash, ex.Message);
            }
            catch (Exception ex)
            {
                diagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.SelectedUnitCommand, NativeCapabilityState.Faulted, binaryHash, ex.Message);
            }
        }
    }

    internal readonly struct SelectedUnitCommandEventData
    {
        public SelectedUnitCommandEventData(EventHookPhase phase, SelectedUnitCommandContext context)
        {
            Phase = phase;
            Context = context;
        }

        public EventHookPhase Phase { get; }
        public SelectedUnitCommandContext Context { get; }
    }

    internal interface ISelectedUnitCommandEventSource
    {
        IDisposable Subscribe(Action<SelectedUnitCommandEventData> callback);
    }

    internal sealed class ScriptExtenderSelectedUnitCommandEventSource : ISelectedUnitCommandEventSource
    {
        public IDisposable Subscribe(Action<SelectedUnitCommandEventData> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(eventArgs =>
            {
                // Copy the mutable Script Extender event data. API consumers never receive the
                // original EventArgs and therefore cannot alter Vanilla control flow or results.
                var context = new SelectedUnitCommandContext(
                    eventArgs.TribeId,
                    eventArgs.AICommand,
                    eventArgs.TargetValue1,
                    eventArgs.TargetValue2,
                    eventArgs.a6);
                callback(new SelectedUnitCommandEventData(eventArgs.Phase, context));
            });
        }
    }

    internal sealed class SelectedUnitCommandBroker
    {
        private readonly object sync = new object();
        private readonly SortedDictionary<string, Registration> registrations =
            new SortedDictionary<string, Registration>(StringComparer.Ordinal);
        private readonly string binaryHash;
        private readonly BepInEx.Logging.ManualLogSource log;

        public SelectedUnitCommandBroker(string binaryHash, BepInEx.Logging.ManualLogSource log)
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

        public void Dispatch(SelectedUnitCommandContext context)
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

            foreach (Registration registration in snapshot)
            {
                try { registration.Invoke(context); }
                catch (Exception ex)
                {
                    NativeApiLog.Error(log, $"capability={NativeCapabilityIds.SelectedUnitCommand}, build={binaryHash}, owner={registration.OwnerGuid}, callbackError={ex}");
                }
            }
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

    internal sealed class SelectedUnitCommandService
    {
        private readonly object sync = new object();
        private readonly string binaryHash;
        private readonly ISelectedUnitCommandEventSource eventSource;
        private readonly SelectedUnitCommandBroker broker;
        private readonly BepInEx.Logging.ManualLogSource log;
        private IDisposable rootedSubscription;

        public SelectedUnitCommandService(
            string binaryHash,
            ISelectedUnitCommandEventSource eventSource,
            BepInEx.Logging.ManualLogSource log)
        {
            this.binaryHash = binaryHash ?? string.Empty;
            this.eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            this.log = log;
            broker = new SelectedUnitCommandBroker(this.binaryHash, log);
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
                ISelectedUnitCommandRegistration candidate = null;
                try
                {
                    candidate = broker.Register(ownerGuid, callback);
                    if (rootedSubscription == null)
                    {
                        IDisposable subscription = eventSource.Subscribe(OnEvent);
                        if (subscription == null)
                            throw new InvalidOperationException("The Script Extender event source returned no subscription handle.");
                        rootedSubscription = subscription;
                        NativeApiLog.Info(log, $"capability={NativeCapabilityIds.SelectedUnitCommand}, build={binaryHash}, owner={ownerGuid}, status=script-extender-event-subscribed.");
                    }
                    registration = candidate;
                    diagnostic = Diagnostic(NativeCapabilityState.Available, "The selected-unit Before callback is registered through the Script Extender event.");
                    return true;
                }
                catch (Exception ex)
                {
                    // A failed first subscription must not leave an unusable owner registration.
                    if (rootedSubscription == null)
                        candidate?.Dispose();
                    diagnostic = Diagnostic(NativeCapabilityState.Faulted, ex.Message);
                    NativeApiLog.Error(log, $"capability={NativeCapabilityIds.SelectedUnitCommand}, build={binaryHash}, owner={ownerGuid}, status=failed, error={ex}");
                    return false;
                }
            }
        }

        private void OnEvent(SelectedUnitCommandEventData eventData)
        {
            if (eventData.Phase != EventHookPhase.Pre)
                return;
            broker.Dispatch(eventData.Context);
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
