using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Reflection;

namespace ExtraFeatures
{
    internal sealed class UnitLimitBridge
    {
        private const string PluginGuid = "UnitLimit_Serp";
        private const string IntegrationTypeName = "UnitLimit.UnitLimitIntegration";

        private delegate bool TryGetCapacityDelegate(int playerId, int unitType, out int count, out int limit);
        private delegate bool TryReserveOneDelegate(
            int playerId,
            int unitType,
            out long reservationId,
            out int count,
            out int limit);
        private delegate bool ArmReservationDelegate(long reservationId);
        private delegate void ReleaseReservationDelegate(long reservationId);
        private delegate void RegisterStateChangedDelegate(Action callback);

        private readonly ManualLogSource log;
        private TryGetCapacityDelegate tryGetCapacity;
        private TryReserveOneDelegate tryReserveOne;
        private ArmReservationDelegate armReservation;
        private ReleaseReservationDelegate releaseReservation;
        private RegisterStateChangedDelegate registerStateChanged;
        private bool available;
        private bool failureLogged;

        internal UnitLimitBridge(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out var pluginInfo))
                return;

            try
            {
                Type integrationType = pluginInfo.Instance.GetType().Assembly.GetType(IntegrationTypeName, true);
                tryGetCapacity = CreateDelegate<TryGetCapacityDelegate>(integrationType, "TryGetCapacity");
                tryReserveOne = CreateDelegate<TryReserveOneDelegate>(integrationType, "TryReserveOne");
                armReservation = CreateDelegate<ArmReservationDelegate>(integrationType, "ArmReservation");
                releaseReservation = CreateDelegate<ReleaseReservationDelegate>(integrationType, "ReleaseReservation");
                registerStateChanged = CreateDelegate<RegisterStateChangedDelegate>(integrationType, "RegisterStateChanged");
                available = true;
                Shared.DebugLogHelper.LogDebug(log, "Extra Features connected to UnitLimit capacity integration.");
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        internal void RegisterStateChanged(Action callback)
        {
            if (!available || callback == null)
                return;

            try
            {
                registerStateChanged(callback);
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        internal bool TryGetCapacity(int playerId, eChimps unitType, out int count, out int limit)
        {
            count = 0;
            limit = -1;
            if (!available)
                return false;

            try
            {
                return tryGetCapacity(playerId, (int)unitType, out count, out limit);
            }
            catch (Exception ex)
            {
                Disable(ex);
                count = 0;
                limit = -1;
                return false;
            }
        }

        internal bool TryReserveOne(
            int playerId,
            eChimps unitType,
            out long reservationId,
            out int count,
            out int limit)
        {
            reservationId = 0;
            count = 0;
            limit = -1;
            if (!available)
                return true;

            try
            {
                return tryReserveOne(playerId, (int)unitType, out reservationId, out count, out limit);
            }
            catch (Exception ex)
            {
                long issuedReservationId = reservationId;
                Disable(ex);
                ReleaseReservation(issuedReservationId);
                reservationId = 0;
                count = 0;
                limit = -1;
                return true;
            }
        }

        internal bool ArmReservation(long reservationId)
        {
            if (reservationId == 0)
                return true;
            if (!available)
                return false;

            try
            {
                return armReservation(reservationId);
            }
            catch (Exception ex)
            {
                Disable(ex);
                return false;
            }
        }

        internal void ReleaseReservation(long reservationId)
        {
            if (reservationId == 0 || releaseReservation == null)
                return;

            try
            {
                releaseReservation(reservationId);
            }
            catch (Exception ex)
            {
                Disable(ex);
            }
        }

        private void Disable(Exception exception)
        {
            available = false;
            if (failureLogged)
                return;

            failureLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Extra Features could not use the optional UnitLimit integration; " +
                $"standalone transformation behavior remains active: {exception.GetBaseException().Message}");
        }

        private static T CreateDelegate<T>(Type type, string methodName) where T : Delegate
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);
            return (T)method.CreateDelegate(typeof(T));
        }
    }
}
