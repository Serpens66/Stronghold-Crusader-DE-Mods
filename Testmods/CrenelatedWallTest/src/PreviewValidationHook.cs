using APIShared.NativeSites;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API.LowLevel;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace CrenelatedWallTest
{
    internal sealed class PreviewValidationHook
    {
        internal const int Rva = 0x7A3B0, BodySize = 1627, Displaced = 18;
        // 7A3B0 writes this field; 61210, 94120 and 6F230 read the same int.
        internal const int InvalidOffset = 0x204E6E4;
        internal const string BodyHex = "89542410535557415441554881EC800000004C63A424D8000000488BE933C94963D9390DB8A74F08448BD24C638C24D0000000448BE94963F87415C78424B00000000F000000C744244007000000EB10B81E000000898424B0000000894424404C897C247041BB010000004C899DE4E60402898DFCE6040281FF1F0300000F87CB05000081FB1F0300000F87BF0500004869CB20030000488D05B25BF8FF4C8BFB4803CF4438AC01A41EA1030F849D0500004181F91F0300000F87900500004181FC1F0300000F87830500004969CC200300004903C94C8D0D735BF8FF4638AC09A41EA1030F8464050000440FB79C24E0000000488D045B4889B424C80000004C897424784D8DB12CFF0204418B34864D8D348603F7899D10E8040289B514E80402664183FB1B754D448BC38BD6488BCDE8FA1FFEFF85C0741B448BC38BD6488BCDE8C9FFFEFF8985ECE60402448BE883F8107F3D488BB424C80000004C8B7424784C8B7C24704881C480000000415D415C5F5D5BC3664183FB1A7524448BC38BD6488BCDE84620FEFF85C00F8481040000448B9424B80000004C8D0DAF5AF8FFB801000000895C24448985E8E60402BE02000000458B36488D85008489004403F789B424C00000004D63C6428B14804A8D04804C897C2458895C2448897C244C4889442460F6C2100F8566FFFFFFB9A00F0000F7C2010000600F8555FFFFFFF7C2000400100F8549FFFFFFF6C2040F8540FFFFFF664283BC45A0BCB000000F8530FFFFFFF7C200300000743E4A0FBF844560F2A6006685C07430663BC17D2B4869C89C000000420FBF8409AAE42D0383C0FB83F80E0F87F9FEFFFF4898418B8C810CAA07004903C9FFE10FBAE2140F82E0FEFFFF0FBAE2150F82D6FEFFFF84D20F88CEFEFFFF420FBF8445006CBF006685C074490FBAE20872438BF00F1F008BD6418BCAE82619FEFF84C00F84A3FEFFFF448B9424B80000004863C64869C890040000488D058559F8FF0FBFB401FA8C7E0685F675C98BB424C0000000833D93B85E030174318B8424B0000000488D0DBB2F03068B9424B8000000448BCBC644242800448BC789442420E8803F070085C00F8528030000833DC1A44F080074768B442440488D0D842F03068B9424B8000000448BCBC7442438FFFFFFFF448BC7C7442430FFFFFFFFC7442428FFFFFFFF89442420E8163C070085C00F85A4020000390578A44F08742D488BCDE8DE07FFFF8B9424B8000000488D0D302F0306448BCB89442420448BC7E85148070085C00F856F020000833DDAB75E030175418B9424B8000000488D0D022F0306C7442438FFFFFFFF448BCBC7442430FFFFFFFF448BC7C7442428FFFFFFFFC744242007000000E8973B070085C00F855F0200008B9424B8000000488D0DC12E0306448BCBC744242002000000448BC7E8EE11070085C00F8536020000448B0D37517703488D0D982E03064183C105448BC38BD7E85A19070085C00F8503020000440FB79C24E0000000664183FB1A7556448BC3418BD6488BCDE8841DFEFF85C00F84BF010000448B8C24D00000008BD7412BD1418BC12BC78BCB413BF90F4CD0412BCC418BC42BC3413BDC0F4CC885D20F843F010000413BF90F8C34010000FFCFE92F010000664183FB1B751B4183FD100F8E7D010000488B442460F700000100000F856C010000448B8C24D00000008BD7412BD1418BC12BC78BCB413BF90F4CD0412BCC418BC42BC3413BDC0F4CC8664183FB1B75973BD10F8E9200000044394C244CBFFFFFFFFFB9010000000F4CF9037C244C4183FD10418D45F0410F4EC5448BE8413BF9751489B424C0000000413BDC750885F60F84F70000004B8D047F895C24444C8D0D1657F8FF4C897C2458458BB4812CFF0204488D85008489004403F7895C24484D63C6428B14804A8D0480897C244C4889442460F6C2100F85E3FBFFFF448B9424B8000000E970FCFFFF85C97E304439642448B901000000BBFFFFFFFF49C7C7FFFFFFFF0F4CD9035C244844396424444C0F4CF94C037C2458E950FFFFFFFFCE89B424C0000000E942FFFFFFFFC785C97411413BDC7C07FFCB49FFCFEB05FFC349FFC7413BF90F8522FFFFFF413BDC0F8519FFFFFF418BC5FFCE4183C5F089B424C000000083F810440F4EE8E91DFFFFFF83BD04E70402120F8442FBFFFFC78504E7040213000000E933FBFFFFC785E4E6040200000000E924FBFFFFC78504E7040215000000E915FBFFFFC78504E7040211000000E906FBFFFF44899DFCE60402E907FBFFFF";
        private const string Site = "crenel-build-preview";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void Validator(IntPtr manager, int owner, uint x, uint y, uint endX, uint endY, short mapper);
        internal delegate bool Reject(IntPtr manager, int owner, uint x, uint y, uint endX, uint endY, short mapper);
        private readonly ManualLogSource log;
        private readonly string ownerGuid;
        private readonly Reject reject;
        private readonly Validator callback;
        private NativeDetour<Validator> hook;
        private bool published, confirmed;
        private int enabled;
        internal PreviewValidationHook(ManualLogSource log, string ownerGuid, Reject reject)
        { this.log = log; this.ownerGuid = ownerGuid; this.reject = reject; callback = Invoke; }
        internal void SetActive(bool active) => Volatile.Write(ref enabled, active ? 1 : 0);
        internal static NativeDetourBackend CreateBackend() => new NativeDetourBackend(new NativeDetourOptions {
            AllowedSchemes = DetourScheme.Absolute, FollowCalls = false, FollowJumps = false,
            Chaining = HookChainingPolicy.Refuse, FunctionScanLimit = BodySize });
        internal void Install(CrusaderLibraryLoadContext library)
        {
            bool claimed = false;
            try
            {
                using (var stream = File.OpenRead(typeof(NativeDetourBackend).Assembly.Location))
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != StraightGraphicsHook.BackendHash)
                        throw new InvalidOperationException("NativeX64 backend changed; preview hook requires audit.");
                byte[] body = DecodeBody();
                if (library.ModuleHandle == IntPtr.Zero || library.Memory.Length < Rva + body.Length)
                    throw new InvalidOperationException("Preview validator outside image.");
                IntPtr target = IntPtr.Add(library.ModuleHandle, Rva);
                var live = new byte[body.Length]; Marshal.Copy(target, live, 0, live.Length);
                for (int i = 0; i < body.Length; i++)
                    if (library.Memory[Rva + i] != body[i] || live[i] != body[i])
                        throw new InvalidOperationException("Preview validator differs at +" + i);
                if (!NativeSiteRegistry.IsImageBound) throw new InvalidOperationException("Native-site image not bound.");
                var registration = NativeSiteRegistry.Register(ownerGuid, Site, new[] {
                    new NativeSiteSpan(Rva, Displaced, NativeSiteMode.Exclusive),
                    new NativeSiteSpan(Rva + Displaced, BodySize - Displaced, NativeSiteMode.Observe) });
                if (!registration.Succeeded) throw new InvalidOperationException(registration.ToString());
                claimed = true;
                var request = new DetourRequest<Validator> { Name = "CrenelatedWallTest Vanilla preview validation",
                    TargetAddress = unchecked((ulong)target.ToInt64()), Callback = callback };
                hook = (NativeDetour<Validator>)CreateBackend().CreateDetour(in request);
                Validate(hook, unchecked((ulong)target.ToInt64()));
                // From the first attempt to publish, rollback is forbidden even if
                // publication reports an error. Callback stays logically inactive.
                published = true;
                hook.Enable(); Validate(hook, unchecked((ulong)target.ToInt64()));
                if (!hook.IsInstalled) throw new InvalidOperationException("Preview publication incomplete.");
                SetActive(true);
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_PREVIEW_READY method=reference-rva; RVA=0x7A3B0; body=1627; Absolute/18; continuation=0x7A3C2; permanent; Vanilla invalid status only.");
            }
            catch (Exception ex)
            {
                SetActive(false);
                if (!published)
                {
                    try
                    {
                        hook?.Dispose(); hook = null;
                        if (claimed) NativeSiteRegistry.Unregister(ownerGuid, Site);
                    }
                    catch (Exception cleanup)
                    {
                        // Cleanup failure must not take the independent execution
                        // guard down. Conservatively retain any remaining site claim.
                        Shared.DebugLogHelper.LogErrorLimited(log, "crenel-preview-prepare-cleanup", "CRENEL_PREVIEW_PREPARATION_CLEANUP_ERROR", cleanup);
                    }
                }
                Shared.DebugLogHelper.LogErrorLimited(log, "crenel-preview-install", "CRENEL_PREVIEW_UNAVAILABLE; execution guard retained.", ex);
            }
        }
        internal static byte[] DecodeBody()
        {
            var bytes = new byte[BodyHex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(BodyHex.Substring(i * 2, 2), 16);
            return bytes;
        }
        internal static void Validate(NativeDetour<Validator> value, ulong target)
        {
            if (value == null || value.Scheme != DetourScheme.Absolute || value.DisplacedByteCount != Displaced ||
                value.TargetAddress != target || value.PointerSlot != IntPtr.Zero || value.ChainDepth != 1 ||
                value.TrampolineAddress == IntPtr.Zero || value.OriginalEntryPointAddress == IntPtr.Zero || value.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Preview Absolute/18 contract mismatch.");
            if (!value.IsInstalled) return;
            var bytes = new byte[Displaced]; Marshal.Copy(new IntPtr(unchecked((long)target)), bytes, 0, bytes.Length);
            if (bytes[0] != 0xFF || bytes[1] != 0x25 || BitConverter.ToInt32(bytes, 2) != 0 ||
                BitConverter.ToInt64(bytes, 6) != value.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Preview entry patch mismatch.");
            for (int i = 14; i < bytes.Length; i++) if (bytes[i] != 0x90)
                throw new InvalidOperationException("Preview padding mismatch.");
        }
        private void Invoke(IntPtr manager, int owner, uint x, uint y, uint endX, uint endY, short mapper)
        {
            hook.Original(manager, owner, x, y, endX, endY, mapper);
            if (Volatile.Read(ref enabled) == 0) return;
            try
            {
                // The original validator has just initialized this field, including
                // every early-return path. Never turn an original veto into approval.
                if (Marshal.ReadInt32(manager, InvalidOffset) != 0 || !reject(manager, owner, x, y, endX, endY, mapper)) return;
                Marshal.WriteInt32(manager, InvalidOffset, 1);
                if (!confirmed)
                {
                    confirmed = true;
                    Shared.DebugLogHelper.LogInfo(log, "CRENEL_PREVIEW_CONFIRMED owner=" + owner + "; line=" + x + "," + y + "->" + endX + "," + endY + "; Vanilla invalid status set.");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "crenel-preview-callback", "CRENEL_PREVIEW_ERROR; Vanilla validation completed; execution guard remains authoritative.", ex);
            }
        }
    }
}
