using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace PlayersInfo.Core;

/// mm_getinfo / mm_getinfo_slot / mm_getinfo_file, byte-compatible field set with the Metamod build
/// (keys in the same alphabetical order nlohmann::json writes them).
public sealed class ServerInfo(IPlayersHost host, Func<ulong, bool> isPrime, Func<long>? clock = null)
{
    public const int MaxPlayers = 64;
    private const int ConsoleChunk = 1024;

    // '[' is escaped so console color tags in nicknames ("[red]") can't be rewritten by SwiftlyS2's console.
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = BuildEncoder() };

    private readonly long[] _connectedAt = new long[MaxPlayers];
    private readonly Func<long> _now = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    private static JavaScriptEncoder BuildEncoder()
    {
        var settings = new TextEncoderSettings(UnicodeRanges.All);
        settings.ForbidCharacter('[');
        return JavaScriptEncoder.Create(settings);
    }

    private static bool IsValidSlot(int slot) => slot is >= 0 and < MaxPlayers;

    public void OnConnected(int slot)
    {
        if (IsValidSlot(slot)) _connectedAt[slot] = _now();
    }

    public void OnDisconnected(int slot)
    {
        if (IsValidSlot(slot)) _connectedAt[slot] = 0;
    }

    public string Dump()
    {
        var now = _now();
        var players = host.GetPlayers().Where(p => IsValidSlot(p.Slot)).ToList();
        var live = new bool[MaxPlayers];
        foreach (var p in players) live[p.Slot] = true;
        for (var i = 0; i < MaxPlayers; i++)
            if (!live[i]) _connectedAt[i] = 0;

        var (ct, t) = host.GetScores();
        return Write(w =>
        {
            w.WriteStartObject();
            w.WriteString("current_map", host.MapName);
            w.WriteNumber("player_count", players.Count);
            w.WriteStartArray("players");
            foreach (var p in players) WritePlayer(w, p, now);
            w.WriteEndArray();
            w.WriteNumber("score_ct", ct);
            w.WriteNumber("score_t", t);
            w.WriteNumber("time", now);
            w.WriteEndObject();
        });
    }

    /// One player as JSON, or "{}" when the argument is not a slot or the slot holds no live player.
    public string DumpSlot(string? arg)
    {
        if (!TryParseSlot(arg, out var slot)) return "{}";
        var player = host.GetPlayers().FirstOrDefault(p => p.Slot == slot);
        return player is null ? "{}" : Write(w => WritePlayer(w, player, _now()));
    }

    /// Atomic: readers never see a half-written file.
    public bool DumpToFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var tmp = path + ".tmp";
        try
        {
            File.WriteAllText(tmp, Dump(), new UTF8Encoding(false));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            try { File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return false;
        }
    }

    /// Console lines are capped by the engine; RCON clients reassemble the pieces, so no newlines in between.
    public static IEnumerable<string> ConsoleChunks(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var len = Math.Min(ConsoleChunk, text.Length - i);
            // Never split a surrogate pair: half an emoji is not valid UTF-8 on the native side.
            if (len < text.Length - i && char.IsHighSurrogate(text[i + len - 1])) len--;
            yield return text.Substring(i, len);
            i += len;
        }
    }

    internal static bool TryParseSlot(string? value, out int slot)
    {
        slot = -1;
        return !string.IsNullOrEmpty(value) && value.All(char.IsAsciiDigit)
               && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out slot);
    }

    private void WritePlayer(Utf8JsonWriter w, LivePlayer p, long now)
    {
        if (_connectedAt[p.Slot] == 0) _connectedAt[p.Slot] = now;

        w.WriteStartObject();
        w.WriteBoolean("alive", p.Alive);
        w.WriteNumber("death", p.Deaths);
        w.WriteNumber("headshots", p.Headshots);
        w.WriteNumber("kills", p.Kills);
        w.WriteString("name", SanitizeName(p.Name));
        w.WriteNumber("ping", p.Ping);
        w.WriteNumber("playtime", now - _connectedAt[p.Slot]);
        w.WriteBoolean("prime", isPrime(p.SteamId));
        w.WriteString("steamid", p.SteamId.ToString(CultureInfo.InvariantCulture));
        w.WriteNumber("team", p.Team);
        w.WriteNumber("userid", p.Slot);
        w.WriteEndObject();
    }

    /// Drops lone surrogates (they have no UTF-8 form); an empty result becomes "Unknown" like in Metamod.
    internal static string SanitizeName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1])) sb.Append(c).Append(name[++i]);
            else if (!char.IsSurrogate(c)) sb.Append(c);
        }
        return sb.Length == 0 ? "Unknown" : sb.ToString();
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions)) body(w);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
