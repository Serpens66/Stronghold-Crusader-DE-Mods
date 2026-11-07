using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Shared
{
    /// <summary>User-owned file with one-time, durable adoption of an installation-local source.</summary>
    /// <remarks>Migration preserves opaque bytes. Callers retain format validation and mark rejected
    /// data with PreserveOnNextWrite before replacing it. No Unity callbacks or global path switches.</remarks>
    internal sealed class PersonalDataFile
    {
        internal static Func<string> TestDocumentsDirectory;
        private readonly string legacyPath, receiptPath, identity, mutexName;
        private readonly Action<string> report;
        private readonly long maximumReadBytes;
        private readonly object gate = new object();
        private bool initialized, writable, preserve;
        private byte[] sessionBytes;
        private Exception failure;

        internal PersonalDataFile(string targetPath, string legacyPath, Action<string> report = null, string identity = null,
            long maximumReadBytes = long.MaxValue)
        {
            if (maximumReadBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumReadBytes));
            Path = PersonalDataIO.Normalize(targetPath);
            this.legacyPath = string.IsNullOrEmpty(legacyPath) ? null : PersonalDataIO.Normalize(legacyPath);
            if (string.Equals(Path, this.legacyPath, PersonalDataIO.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new ArgumentException("The personal data source and target must differ.", nameof(legacyPath));
            this.report = report;
            this.identity = identity ?? string.Empty;
            this.maximumReadBytes = maximumReadBytes;
            receiptPath = Path + ".migration.json";
            mutexName = (PersonalDataIO.IsWindows ? @"Local\" : "") + "SHCDE.PersonalData." +
                PersonalDataIO.Hash(Encoding.UTF8.GetBytes(PersonalDataIO.IsWindows ? Path.ToUpperInvariant() : Path));
        }

        internal string Path { get; }

        internal static string GetPath(string modDirectory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(modDirectory) || modDirectory != System.IO.Path.GetFileName(modDirectory) ||
                modDirectory == "." || modDirectory == ".." || modDirectory.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("A single safe mod directory is required.", nameof(modDirectory));
            if (string.IsNullOrWhiteSpace(fileName) || fileName != System.IO.Path.GetFileName(fileName) ||
                fileName == "." || fileName == ".." || fileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("A single safe data filename is required.", nameof(fileName));
            string folder = TestDocumentsDirectory != null ? TestDocumentsDirectory() : GetDocuments();
            return PersonalDataIO.Normalize(System.IO.Path.Combine(folder, "SHCDE", modDirectory, fileName));
        }

        internal static string GetDocuments()
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string candidate in new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                string.IsNullOrWhiteSpace(profile) ? null : System.IO.Path.Combine(profile, "Documents"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) })
                if (!string.IsNullOrWhiteSpace(candidate) && System.IO.Path.IsPathRooted(candidate)) return PersonalDataIO.Normalize(candidate);
            throw new IOException("Cannot resolve an absolute per-user directory for SHCDE personal data.");
        }

        internal byte[] ReadAllBytes()
        {
            lock (gate)
            using (Acquire())
            {
                Initialize();
                if (!writable)
                {
                    if (sessionBytes != null) return (byte[])sessionBytes.Clone();
                    throw new IOException("Personal data could not be opened safely: " + Path, failure);
                }
                try { return PersonalDataIO.ReadOptional(Path, maximumReadBytes); }
                catch (Exception error)
                {
                    writable = false;
                    sessionBytes = null;
                    failure = error;
                    throw;
                }
            }
        }

        internal string ReadAllText()
        {
            byte[] bytes = ReadAllBytes();
            if (bytes == null) return null;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true)) return reader.ReadToEnd();
        }

        internal string[] ReadAllLines()
        {
            string text = ReadAllText();
            if (text == null) return null;
            var lines = new List<string>();
            using (var reader = new StringReader(text)) { string line; while ((line = reader.ReadLine()) != null) lines.Add(line); }
            return lines.ToArray();
        }

        internal void PreserveOnNextWrite() { lock (gate) preserve = true; }
        internal void WriteAllText(string text) => WriteAllBytes(new UTF8Encoding(false).GetBytes(text ?? throw new ArgumentNullException(nameof(text))));
        internal void WriteAllBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.LongLength > maximumReadBytes) throw new IOException("Personal data file is too large: " + Path);
            lock (gate)
            using (Acquire())
            {
                Initialize();
                RequireWritable();
                if (preserve)
                {
                    byte[] original = PersonalDataIO.ReadOptional(Path, maximumReadBytes);
                    if (original != null)
                        PersonalDataIO.Publish(Path + ".invalid-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".bak", original, false);
                    preserve = false;
                }
                PersonalDataIO.Publish(Path, bytes, true);
            }
        }

        /// <summary>Explicit user reset only. Oversized input is preserved without allocating it.</summary>
        /// <remarks>Ordinary writes still fail closed. This recovery never bypasses receipt,
        /// identity, access or publication errors. An oversized legacy source remains untouched.</remarks>
        internal void ResetAllBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.LongLength > maximumReadBytes) throw new PersonalDataSizeException(Path);
            lock (gate)
            using (Acquire())
            {
                Initialize();
                if (writable) { WriteAllBytes(bytes); return; }
                var tooLarge = failure as PersonalDataSizeException;
                if (tooLarge == null || (tooLarge.FilePath != Path && tooLarge.FilePath != legacyPath)) { RequireWritable(); return; }

                // A target size error can precede receipt validation during normal initialization.
                // Validate it independently before granting this narrowly scoped explicit reset.
                byte[] previousReceipt = PersonalDataIO.ReadOptional(receiptPath, 256 * 1024);
                Receipt receipt;
                try
                {
                    receipt = previousReceipt == null
                        ? new Receipt { Source = legacyPath ?? string.Empty, Identity = identity }
                        : ParseReceipt(previousReceipt);
                }
                catch (Exception error) { throw new IOException("Personal data reset requires a valid migration receipt: " + Path, error); }
                bool oversizedTarget = false;
                byte[] currentTarget = null;
                try { currentTarget = PersonalDataIO.ReadOptional(Path, maximumReadBytes); }
                catch (PersonalDataSizeException error)
                {
                    if (error.FilePath != Path) throw;
                    oversizedTarget = true;
                }
                if (currentTarget != null)
                {
                    initialized = false;
                    failure = null;
                    sessionBytes = null;
                    Initialize();
                    RequireWritable();
                    WriteAllBytes(bytes);
                    return;
                }

                string backup = null;
                bool backupMoved = false, publicationStarted = false;
                try
                {
                    if (oversizedTarget)
                    {
                        backup = Path + ".invalid-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".bak";
                        PersonalDataIO.Move(Path, backup, false);
                        backupMoved = true;
                    }
                    else
                    {
                        // An oversized source is not imported or deleted by a reset. Re-read it
                        // only to distinguish a size failure from a newly introduced access error.
                        if (legacyPath != null)
                        {
                            try { PersonalDataIO.ReadOptional(legacyPath, maximumReadBytes); }
                            catch (PersonalDataSizeException error) { if (error.FilePath != legacyPath) throw; }
                        }
                    }
                    publicationStarted = true;
                    PersonalDataIO.Publish(Path, bytes, true);
                    SaveReceipt(receipt);
                    failure = null;
                    writable = true;
                    sessionBytes = null;
                    preserve = false;
                    Report("explicit reset completed; oversized original retained");
                }
                catch
                {
                    if (backupMoved)
                    {
                        // Keep the backup if rollback itself is unavailable; never discard it.
                        PersonalDataIO.Move(backup, Path, true);
                    }
                    else if (publicationStarted)
                    {
                        // Failed publication can already have created a reset target. Retain no
                        // unverified reset/default file that a later initialization could adopt.
                        PersonalDataIO.Delete(Path);
                    }
                    throw;
                }
            }
        }

        internal void Delete()
        {
            lock (gate)
            using (Acquire()) { Initialize(); RequireWritable(); PersonalDataIO.Delete(Path); }
        }

        private void RequireWritable()
        {
            if (!writable) throw new IOException("Personal data writes are blocked until migration can complete: " + Path, failure);
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                // A target read error is NEVER absence and must never enable the old source.
                byte[] target = PersonalDataIO.ReadOptional(Path, maximumReadBytes);
                byte[] receiptBytes = PersonalDataIO.ReadOptional(receiptPath, 256 * 1024);
                if (receiptBytes != null)
                {
                    Receipt previous = ParseReceipt(receiptBytes);
                    writable = true;
                    Cleanup(previous);
                    return;
                }
                sessionBytes = target;

                byte[] source = null;
                if (legacyPath != null)
                {
                    try { source = PersonalDataIO.ReadOptional(legacyPath, maximumReadBytes); }
                    catch (Exception error)
                    {
                        if (target == null) throw;
                        Report("existing-target; legacy-unreadable; source retained; " + error.GetType().Name);
                    }
                }
                var receipt = new Receipt { Source = legacyPath ?? string.Empty, Identity = identity };
                if (target == null && source != null)
                {
                    sessionBytes = source;
                    PersonalDataIO.Publish(Path, source, false);
                    receipt.Hash = PersonalDataIO.Hash(source);
                    receipt.CleanupPending = true;
                }
                else if (source != null && PersonalDataIO.Equal(target, source))
                {
                    receipt.Hash = PersonalDataIO.Hash(source);
                    receipt.CleanupPending = true;
                }
                else if (source != null) Report("existing-target; different legacy source retained");

                SaveReceipt(receipt);
                writable = true;
                Report(source != null && receipt.CleanupPending ? "migration verified" : "personal location adopted");
                Cleanup(receipt);
            }
            catch (Exception error)
            {
                failure = error;
                writable = false;
                Report("migration/read failed; writes disabled; " + error.GetType().Name);
            }
        }

        private Receipt ParseReceipt(byte[] bytes)
        {
            var root = DependencyFreeJson.Parse(Encoding.UTF8.GetString(bytes)) as Dictionary<string, object>;
            object schema, valueIdentity, source, hash, pending;
            if (root == null || !root.TryGetValue("schema", out schema) || !((schema is int && (int)schema == 1) || (schema is long && (long)schema == 1)) ||
                !root.TryGetValue("identity", out valueIdentity) || !(valueIdentity is string) || (string)valueIdentity != identity ||
                !root.TryGetValue("source", out source) || !(source is string) ||
                !root.TryGetValue("hash", out hash) || !(hash is string) ||
                !root.TryGetValue("cleanupPending", out pending) || !(pending is bool))
                throw new InvalidDataException("Personal data migration receipt is invalid or belongs to another installation: " + receiptPath);
            string digest = (string)hash;
            bool cleanup = (bool)pending;
            if ((digest.Length != 0 && (digest.Length != 64 || !IsHex(digest))) ||
                (cleanup && (digest.Length == 0 || ((string)source).Length == 0)))
                throw new InvalidDataException("Personal data migration receipt has an invalid source digest: " + receiptPath);
            if (((string)source).Length != 0) PersonalDataIO.Normalize((string)source);
            return new Receipt { Source = (string)source, Identity = (string)valueIdentity, Hash = digest, CleanupPending = cleanup };
        }

        private static bool IsHex(string value)
        {
            foreach (char item in value) if (!(item >= '0' && item <= '9' || item >= 'A' && item <= 'F')) return false;
            return true;
        }

        private void SaveReceipt(Receipt receipt)
        {
            string text = DependencyFreeJson.Serialize(new Dictionary<string, object> {
                ["schema"] = 1L, ["identity"] = receipt.Identity, ["source"] = receipt.Source,
                ["hash"] = receipt.Hash, ["cleanupPending"] = receipt.CleanupPending });
            PersonalDataIO.Publish(receiptPath, new UTF8Encoding(false).GetBytes(text), true);
        }

        private void Cleanup(Receipt receipt)
        {
            if (!receipt.CleanupPending) return;
            try
            {
                bool removed = PersonalDataIO.DeleteUnchanged(receipt.Source, receipt.Hash, maximumReadBytes);
                receipt.CleanupPending = false;
                SaveReceipt(receipt);
                Report(removed ? "legacy cleanup completed" : "legacy source changed; retained");
            }
            catch (Exception error) { Report("legacy cleanup pending; personal data remains authoritative; " + error.GetType().Name); }
        }

        private IDisposable Acquire()
        {
            var mutex = new Mutex(false, mutexName);
            try
            {
                bool acquired;
                try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) throw new IOException("Personal data is in use by another process: " + Path);
                return new MutexLease(mutex);
            }
            catch { mutex.Dispose(); throw; }
        }

        private void Report(string status)
        {
            try { report?.Invoke(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) +
                " Personal data: " + status + "; file=" + Path); } catch { }
        }
        private sealed class Receipt { internal string Source, Identity, Hash = string.Empty; internal bool CleanupPending; }
        private sealed class MutexLease : IDisposable
        {
            private readonly Mutex mutex;
            internal MutexLease(Mutex mutex) { this.mutex = mutex; }
            public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
        }
    }
}
