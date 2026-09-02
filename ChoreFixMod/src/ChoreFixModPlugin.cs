using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.GameGlobals;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ChoreFixMod
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, "Chore Fix Mod", "1.0.0")]
    public sealed unsafe class ChoreFixModPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "ChoreFixMod_Serp";
        private const string ExpectedNativeSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        private const int ScriptExtenderChoreId = 106;
        private const int MaterializePhase = 2;
        private const int RecordHeaderBytes = 8;
        private const int EmbeddedLengthBytes = sizeof(int);
        private const int PacketIdBytes = sizeof(short);
        private const int MaximumBlobBytes = 1200;
        private const int IncomingRecordLengthOffset = 0x84CC0;
        private const int TotalPackedBytesOffset = 0x84CD4;
        private const int IncomingPayloadOffset = 0xEFC;

        // The Script Extender registers handler 106 specifically as System.Action.
        // Using the identical delegate type is required when recovering its managed thunk.
        private static readonly Action ReplacementHandler = HandleChore106;
        private static Action originalHandler;
        private static IntPtr replacementHandlerPointer;
        private static ManualLogSource log;
        private static bool installed;

        private void Awake()
        {
            log = Logger;
            LogInfo("STARTUP: diagnostic Chore 106 materialization fix is loading.");

            if (!RunLengthValidationSelfTests())
            {
                LogError("SELF_TEST_FAILED: the handler will not be installed.");
                return;
            }

            if (!HasExpectedNativeDll())
            {
                LogError(
                    $"NATIVE_HASH_MISMATCH: expected={ExpectedNativeSha256}. " +
                    "The version-specific handler will not be installed.");
                return;
            }

            // Late subscribers are invoked immediately, so both native-load orderings are safe.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (installed)
                return;

            try
            {
                ulong handlerTableAddress = GameGlobalsManager.Instance.GameStateChoreHandlersVA;
                ulong choreManagerAddress = GameGlobalsManager.Instance.ChoreManagerVA;
                ulong phaseAddress = GameGlobalsManager.Instance.ChoreSendPhaseVA;
                if (libraryHandle == IntPtr.Zero || memory.IsEmpty || handlerTableAddress == 0 ||
                    choreManagerAddress == 0 || phaseAddress == 0)
                {
                    LogError(
                        $"INSTALL_FAILED: module=0x{libraryHandle.ToInt64():X}, imageBytes={memory.Length}, " +
                        $"handlerTable=0x{handlerTableAddress:X}, choreManager=0x{choreManagerAddress:X}, " +
                        $"phase=0x{phaseAddress:X}.");
                    return;
                }

                ulong* handlerTable = (ulong*)handlerTableAddress;
                IntPtr originalPointer = new IntPtr(unchecked((long)handlerTable[ScriptExtenderChoreId]));
                if (originalPointer == IntPtr.Zero)
                {
                    LogError("INSTALL_FAILED: Chore 106 did not have an existing Script Extender handler.");
                    return;
                }

                originalHandler = (Action)Marshal.GetDelegateForFunctionPointer(
                    originalPointer,
                    typeof(Action));
                replacementHandlerPointer = Marshal.GetFunctionPointerForDelegate(ReplacementHandler);
                handlerTable[ScriptExtenderChoreId] = unchecked((ulong)replacementHandlerPointer.ToInt64());

                if (handlerTable[ScriptExtenderChoreId] !=
                    unchecked((ulong)replacementHandlerPointer.ToInt64()))
                {
                    originalHandler = null;
                    replacementHandlerPointer = IntPtr.Zero;
                    LogError("INSTALL_FAILED: the Chore 106 handler table write did not persist.");
                    return;
                }

                installed = true;
                LogInfo(
                    $"INSTALLED: chore=106, original=0x{originalPointer.ToInt64():X}, " +
                    $"replacement=0x{replacementHandlerPointer.ToInt64():X}, nativeHash={ExpectedNativeSha256}.");
            }
            catch (Exception exception)
            {
                LogError($"INSTALL_FAILED: {exception}");
            }
        }

        private static void HandleChore106()
        {
            try
            {
                ulong phaseAddress = GameGlobalsManager.Instance.ChoreSendPhaseVA;
                if (phaseAddress == 0)
                    throw new InvalidOperationException("Chore phase address is unavailable.");

                int phase = *(int*)phaseAddress;
                if (phase != MaterializePhase)
                {
                    Action handler = originalHandler;
                    if (handler == null)
                        throw new InvalidOperationException("Original Chore 106 handler is unavailable.");

                    handler();
                    return;
                }

                MaterializeIncomingPayload();
            }
            catch (Exception exception)
            {
                TryClearPublishedSize();
                LogError($"HANDLER_FAILED: {exception}");
            }
        }

        private static void MaterializeIncomingPayload()
        {
            byte* choreManager = (byte*)GameGlobalsManager.Instance.ChoreManagerVA;
            if (choreManager == null)
                throw new InvalidOperationException("Chore manager is unavailable.");

            int* totalPackedBytes = (int*)(choreManager + TotalPackedBytesOffset);
            *totalPackedBytes = 0;

            int recordBytes = *(int*)(choreManager + IncomingRecordLengthOffset);
            int blobBytes = 0;
            int handlerBytes;

            // Twelve bytes are needed before the embedded length can be read safely:
            // eight native record-header bytes followed by the four-byte length field.
            if (recordBytes < RecordHeaderBytes + EmbeddedLengthBytes ||
                recordBytes > RecordHeaderBytes + EmbeddedLengthBytes + MaximumBlobBytes)
            {
                LogWarning(
                    $"PHASE2_REJECT: record={recordBytes}, handler=<invalid>, blob=<unread>, published=0.");
                return;
            }

            blobBytes = *(int*)(choreManager + IncomingPayloadOffset);
            if (!TryGetHandlerBytes(recordBytes, blobBytes, out handlerBytes))
            {
                LogWarning(
                    $"PHASE2_REJECT: record={recordBytes}, handler={recordBytes - RecordHeaderBytes}, " +
                    $"blob={blobBytes}, published=0.");
                return;
            }

            *totalPackedBytes = handlerBytes;
            LogInfo(
                $"PHASE2_ACCEPT: record={recordBytes}, handler={handlerBytes}, " +
                $"blob={blobBytes}, published={handlerBytes}.");
        }

        private static bool TryGetHandlerBytes(int recordBytes, int blobBytes, out int handlerBytes)
        {
            handlerBytes = 0;
            if (recordBytes < RecordHeaderBytes + EmbeddedLengthBytes ||
                recordBytes > RecordHeaderBytes + EmbeddedLengthBytes + MaximumBlobBytes ||
                blobBytes < PacketIdBytes || blobBytes > MaximumBlobBytes)
            {
                return false;
            }

            int availableHandlerBytes = recordBytes - RecordHeaderBytes;
            if (availableHandlerBytes != EmbeddedLengthBytes + blobBytes)
                return false;

            handlerBytes = availableHandlerBytes;
            return true;
        }

        private static bool RunLengthValidationSelfTests()
        {
            bool passed =
                ExpectValidation(21, 9, true, 13) &&
                ExpectValidation(14, 2, true, 6) &&
                ExpectValidation(1212, 1200, true, 1204) &&
                ExpectValidation(11, 2, false, 0) &&
                ExpectValidation(1213, 1200, false, 0) &&
                ExpectValidation(21, 8, false, 0) &&
                ExpectValidation(13, 1, false, 0) &&
                ExpectValidation(1212, 1201, false, 0);

            if (passed)
                LogInfo("SELF_TEST_PASSED: accepted valid minimum, reproduction and maximum lengths; rejected malformed lengths.");

            return passed;
        }

        private static bool ExpectValidation(
            int recordBytes,
            int blobBytes,
            bool expectedResult,
            int expectedHandlerBytes)
        {
            bool actualResult = TryGetHandlerBytes(recordBytes, blobBytes, out int actualHandlerBytes);
            if (actualResult == expectedResult && actualHandlerBytes == expectedHandlerBytes)
                return true;

            LogError(
                $"SELF_TEST_CASE_FAILED: record={recordBytes}, blob={blobBytes}, " +
                $"expected={expectedResult}/{expectedHandlerBytes}, actual={actualResult}/{actualHandlerBytes}.");
            return false;
        }

        private static bool HasExpectedNativeDll()
        {
            string nativeDllPath = Path.Combine(
                Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data",
                "Plugins",
                "x86_64",
                "CrusaderDE.dll");

            try
            {
                using (FileStream stream = File.OpenRead(nativeDllPath))
                using (SHA256 sha256 = SHA256.Create())
                {
                    string actualHash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
                    if (!string.Equals(actualHash, ExpectedNativeSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        LogError($"NATIVE_HASH_ACTUAL: hash={actualHash}, path={nativeDllPath}.");
                        return false;
                    }

                    LogInfo($"NATIVE_HASH_VERIFIED: hash={actualHash}.");
                    return true;
                }
            }
            catch (Exception exception)
            {
                LogError($"NATIVE_HASH_CHECK_FAILED: path={nativeDllPath}, error={exception}");
                return false;
            }
        }

        private static void TryClearPublishedSize()
        {
            try
            {
                byte* choreManager = (byte*)GameGlobalsManager.Instance.ChoreManagerVA;
                if (choreManager != null)
                    *(int*)(choreManager + TotalPackedBytesOffset) = 0;
            }
            catch
            {
                // Never allow a second exception to escape through the unmanaged callback.
            }
        }

        private static void LogInfo(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        private static void LogWarning(string message) =>
            log?.LogWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        private static void LogError(string message) =>
            log?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
