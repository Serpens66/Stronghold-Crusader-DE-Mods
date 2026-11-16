using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace GameMemReader;

/// <summary>Client for the in-game Testmods/GameStateProbe endpoint (one JSON object per line).</summary>
internal sealed class ProbeClient : IDisposable
{
    private readonly TcpClient _client;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    public string Context { get; }

    public ProbeClient(int port, string context)
    {
        Context = context;
        _client = new TcpClient();
        _client.Connect("127.0.0.1", port);
        var stream = _client.GetStream();
        _reader = new StreamReader(stream, new UTF8Encoding(false));
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
    }

    public JsonObject Send(JsonObject request)
    {
        request["context"] ??= Context;
        _writer.WriteLine(request.ToJsonString());
        var line = _reader.ReadLine() ?? throw new IOException("probe closed the connection");
        return JsonNode.Parse(line)!.AsObject();
    }

    /// <summary>Sends a request and yields every response line until the server stops (used by watch).</summary>
    public IEnumerable<JsonObject> SendMany(JsonObject request, int expected)
    {
        _writer.WriteLine(request.ToJsonString());
        for (int i = 0; i < expected; i++)
        {
            var line = _reader.ReadLine();
            if (line == null) yield break;
            yield return JsonNode.Parse(line)!.AsObject();
        }
    }

    /// <summary>Copies memory inside the game on its own thread (tick-consistent in context "tick").</summary>
    public byte[] Read(ulong address, int length)
    {
        var resp = Send(new JsonObject { ["cmd"] = "mem", ["va"] = "0x" + address.ToString("X16"), ["len"] = length });
        if (resp["ok"]?.GetValue<bool>() != true)
            throw new InvalidOperationException("probe mem failed: " + resp["error"]);
        return Convert.FromBase64String(resp["result"]!["base64"]!.GetValue<string>());
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
