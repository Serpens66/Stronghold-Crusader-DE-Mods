// The existing Extender envelope is preserved. Only MaxCounts is interpreted here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;

namespace CrusaderDETweaker.Configuration
{
    internal sealed class LobbyConfiguration
    {
        internal const string FileName = "LobbySettings.msgpack";
        private const string Prefix = "LobbyMaxCounts/";
        private readonly string[] keys;
        private readonly Func<string, Dictionary<string, int>> parse;
        private readonly Func<Dictionary<string, int>, string> encode;
        private readonly Func<string, bool> validValue;
        private static readonly MessagePackSerializerOptions Serialization =
            MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

        internal LobbyConfiguration(IEnumerable<string> names, Func<string, Dictionary<string, int>> parser,
            Func<Dictionary<string, int>, string> encoder, Func<string, bool> validator)
        {
            keys = names.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            parse = parser;
            encode = encoder;
            validValue = validator;
        }

        internal ConfigurationOption[] Options => keys.Select(key => new ConfigurationOption
        {
            Key = Prefix + key, File = FileName, Group = "Lobby overrides", Name = key,
            ValueType = "String", DefaultValue = "", RequiresRestart = false, IsSupported = true,
            Notice = "Empty uses the file value; -1 is unlimited; 0 disables; a positive integer sets the lobby cap."
        }).ToArray();

        internal static bool IsOption(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);

        internal Dictionary<string, object> Read(byte[] bytes) => ReadValues(ReadEncoded(bytes));

        internal Dictionary<string, object> ReadValues(string encoded)
        {
            var values = parse(encoded ?? "");
            return keys.ToDictionary(key => Prefix + key,
                key => (object)(values.TryGetValue(key, out int value)
                    ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""), StringComparer.Ordinal);
        }

        internal static string ReadEncoded(byte[] bytes)
        {
            var payload = Decode(bytes);
            return payload.TryGetValue("MaxCounts", out byte[] value)
                ? MessagePackSerializer.Deserialize<string>(value, Serialization) ?? "" : "";
        }

        internal ConfigurationProblem[] Validate(IDictionary<string, object> values)
        {
            var problems = new List<ConfigurationProblem>();
            var known = new HashSet<string>(keys.Select(key => Prefix + key), StringComparer.Ordinal);
            foreach (string key in values.Keys.Where(IsOption))
                if (!known.Contains(key)) problems.Add(Problem(key, "Unknown lobby option."));
            foreach (string key in known)
                if (!values.TryGetValue(key, out object raw) || !(raw is string text) || !validValue(text))
                    problems.Add(Problem(key, "Expected an empty string or a valid lobby cap."));
            return problems.ToArray();
        }

        internal byte[] Render(byte[] original, IDictionary<string, object> values)
        {
            var problems = Validate(values);
            if (problems.Length != 0) throw new InvalidDataException(string.Join("; ", problems.Select(x => x.ToString())));
            var caps = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                string text = ((string)values[Prefix + key]).Trim();
                if (text.Length != 0) caps.Add(key, int.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
            }
            return WithValue(original, "MaxCounts", encode(caps));
        }

        internal static Dictionary<string, byte[]> Decode(byte[] bytes)
        {
            if (bytes == null) return new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (bytes.Length > 8 * 1024 * 1024) throw new InvalidDataException("Lobby settings exceed 8 MiB.");
            return MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(bytes, Serialization)
                ?? throw new InvalidDataException("Invalid lobby settings envelope.");
        }

        internal static byte[] WithValue<T>(byte[] original, string name, T value)
        {
            var payload = Decode(original);
            payload[name] = MessagePackSerializer.Serialize(value, Serialization);
            byte[] encoded = MessagePackSerializer.Serialize(payload, Serialization);
            if (encoded.Length > 8 * 1024 * 1024) throw new InvalidDataException("Lobby settings exceed 8 MiB.");
            return encoded;
        }

        internal static bool ReadSyncEnabled(byte[] bytes)
        {
            var payload = Decode(bytes);
            return !payload.TryGetValue("SyncEnabled", out byte[] value)
                || MessagePackSerializer.Deserialize<bool>(value, Serialization);
        }

        private static ConfigurationProblem Problem(string key, string message) =>
            new ConfigurationProblem { Key = key, Message = message };
    }
}
