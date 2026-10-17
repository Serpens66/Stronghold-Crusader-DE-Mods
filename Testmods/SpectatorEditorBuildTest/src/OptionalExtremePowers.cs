using Noesis;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace SpectatorEditorBuildTest
{
    public sealed class ToolPower
    {
        public string Name { get; internal set; }
        public string Help { get; internal set; }
        public ImageSource Icon { get; internal set; }
        public bool Enabled { get; internal set; }
        internal int Id, Cost, TargetKind;
        internal string Sprite;
        internal object Api, Replacement;
        internal bool SameContract(ToolPower other) => other != null && Id == other.Id && Cost == other.Cost &&
            TargetKind == other.TargetKind && ReferenceEquals(Api, other.Api) && ReferenceEquals(Replacement, other.Replacement);
    }

    // Late binding keeps both optional mods optional. Never initializes or changes their API/settings.
    internal static class OptionalExtremePowers
    {
        private static Type bootstrap;
        internal static object Api()
        {
            if (bootstrap == null)
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    if (assembly.GetName().Name == "ExtremePowers.API")
                        bootstrap = assembly.GetType("ExtremePowers.API.ExtremePowersBootstrap", false);
            if (bootstrap == null) return null;
            try { return bootstrap.GetProperty("Instance").GetValue(null, null); }
            catch (TargetInvocationException) { throw new InvalidOperationException("Loaded ExtremePowers API is not initialized."); }
        }

        internal static void Describe(ToolPower power)
        {
            object provider = Api();
            if (provider == null) return;
            Type type = provider.GetType();
            if (!(bool)type.GetProperty("NativeBackendAvailable").GetValue(provider, null))
                throw new InvalidOperationException("ExtremePowers native backend unavailable.");
            MethodInfo readinessMethod = type.GetMethod("GetSessionReadiness", BindingFlags.Instance | BindingFlags.NonPublic);
            if (readinessMethod == null) throw new InvalidOperationException("Unknown ExtremePowers readiness contract.");
            object readiness = readinessMethod.Invoke(provider, null);
            if (!(bool)readiness.GetType().GetProperty("Ready").GetValue(readiness, null)) return;
            power.Api = provider;
            object tuning = type.GetProperty("Current").GetValue(provider, null);
            power.Cost = ((int[])tuning.GetType().GetProperty("Costs").GetValue(tuning, null))[power.Id];
            if (power.Cost < 0 || power.Cost > 1000000) throw new InvalidOperationException("Unknown ExtremePowers cost contract.");
            MethodInfo get = type.GetMethod("TryGetReplacement");
            object id = Enum.ToObject(get.GetParameters()[0].ParameterType, power.Id);
            object[] args = { id, null };
            if (!(bool)get.Invoke(provider, args)) return;
            object replacement = args[1];
            power.Replacement = replacement;
            Type replacementType = replacement.GetType();
            power.Name = (string)replacementType.GetProperty("Name").GetValue(replacement, null);
            power.Help = (string)replacementType.GetProperty("Tooltip").GetValue(replacement, null);
            power.Sprite = (string)replacementType.GetProperty("Sprite").GetValue(replacement, null);
            power.TargetKind = Convert.ToInt32(replacementType.GetProperty("TargetKind").GetValue(replacement, null));
        }

        internal static bool ExecuteReplacement(ToolPower power, int player, int tile, ulong operation, int tick, out string reason)
        {
            MethodInfo method = power.Api.GetType().GetMethod("TryExecuteReplacement");
            Type contextType = method.GetParameters()[0].ParameterType;
            ConstructorInfo ctor = contextType.GetConstructors()[0];
            ParameterInfo[] parameters = ctor.GetParameters();
            Type targetType = parameters[2].ParameterType;
            object target = power.TargetKind == 0 ? targetType.GetProperty("None").GetValue(null, null) :
                targetType.GetMethod("MapPoint").Invoke(null, new object[] { tile });
            object context = ctor.Invoke(new[] { Enum.ToObject(parameters[0].ParameterType, power.Id), (object)player, target, operation, tick });
            object[] args = { context, null };
            bool accepted = (bool)method.Invoke(power.Api, args);
            reason = args[1] as string;
            return accepted;
        }

        internal static bool OwnsDispatcher(IntPtr address)
        {
            object provider = Api();
            if (provider == null) return false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object runtime = provider.GetType().GetField("nativeRuntime", flags)?.GetValue(provider);
            if (runtime == null) return false;
            object detour = runtime.GetType().GetField("dispatcherDetour", flags)?.GetValue(runtime);
            Delegate callback = runtime.GetType().GetField("rootedDispatcher", flags)?.GetValue(runtime) as Delegate;
            if (detour == null || callback == null || !(bool)detour.GetType().GetProperty("IsApplied").GetValue(detour, null)) return false;
            object data = detour.GetType().GetProperty("Data").GetValue(detour, null);
            IntPtr method = (IntPtr)data.GetType().GetField("Method").GetValue(data);
            IntPtr target = (IntPtr)data.GetType().GetField("Target").GetValue(data);
            if (method != address || target != Marshal.GetFunctionPointerForDelegate(callback)) return false;
            IntPtr current = address;
            for (int hop = 0; hop < 4; hop++)
            {
                if (current == target) return true;
                byte first = Marshal.ReadByte(current);
                if (first == 0xE9) current = new IntPtr(current.ToInt64() + 5 + Marshal.ReadInt32(current, 1));
                else if (first == 0xFF && Marshal.ReadByte(current, 1) == 0x25)
                    current = Marshal.ReadIntPtr(new IntPtr(current.ToInt64() + 6 + Marshal.ReadInt32(current, 2)));
                else if (first == 0x48 && Marshal.ReadByte(current, 1) == 0xB8 && Marshal.ReadByte(current, 10) == 0xFF && Marshal.ReadByte(current, 11) == 0xE0)
                    current = Marshal.ReadIntPtr(current, 2);
                else return false;
            }
            return current == target;
        }
    }
}
