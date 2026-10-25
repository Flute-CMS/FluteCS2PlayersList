using System.Text.Json;
using PlayersInfo.Core;

namespace PlayersInfo.Core.Tests;

public class ServerInfoTests
{
    private sealed class FakeHost : IPlayersHost
    {
        public string MapName { get; set; } = "de_dust2";
        public List<LivePlayer> Players { get; } = [];
        public (int Ct, int T) Scores { get; set; } = (3, 5);
        public IReadOnlyList<LivePlayer> GetPlayers() => Players;
        public (int Ct, int T) GetScores() => Scores;
    }

    private static LivePlayer Player(int slot, string name = "Flames", ulong steamId = 76561198295345385) =>
        new(slot, name, 3, steamId, 2, 1, 1, 15, true);

    private long _now = 1_000;
    private readonly FakeHost _host = new();
    private ServerInfo Info() => new(_host, id => id == 76561198295345385, () => _now);

    [Fact]
    public void Dump_has_the_metamod_field_set_and_key_order()
    {
        _host.Players.Add(Player(2));
        var json = Info().Dump();

        Assert.Equal(
            """{"current_map":"de_dust2","player_count":1,"players":[{"alive":true,"death":1,"headshots":1,"kills":2,"name":"Flames","ping":15,"playtime":0,"prime":true,"steamid":"76561198295345385","team":3,"userid":2}],"score_ct":3,"score_t":5,"time":1000}""",
            json);
    }

    [Fact]
    public void Playtime_counts_from_connect_and_resets_when_the_slot_empties()
    {
        var info = Info();
        info.OnConnected(4);
        _host.Players.Add(Player(4));
        _now += 90;
        Assert.Equal(90, PlayerField(info.Dump(), "playtime").GetInt64());

        _host.Players.Clear();
        info.Dump();
        _host.Players.Add(Player(4, steamId: 1));
        _now += 10;
        Assert.Equal(0, PlayerField(info.Dump(), "playtime").GetInt64());
    }

    [Fact]
    public void Player_seen_without_connect_event_starts_counting_at_first_dump()
    {
        var info = Info();
        _host.Players.Add(Player(1));
        info.Dump();
        _now += 30;
        Assert.Equal(30, PlayerField(info.Dump(), "playtime").GetInt64());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1a")]
    [InlineData(" 1")]
    [InlineData("99999999999")]
    [InlineData("7")]
    public void DumpSlot_returns_empty_object_for_bad_or_empty_slots(string? arg)
    {
        _host.Players.Add(Player(2));
        Assert.Equal("{}", Info().DumpSlot(arg));
    }

    [Fact]
    public void DumpSlot_returns_the_player()
    {
        _host.Players.Add(Player(2));
        using var doc = JsonDocument.Parse(Info().DumpSlot("2"));
        Assert.Equal(2, doc.RootElement.GetProperty("userid").GetInt32());
    }

    [Fact]
    public void Names_stay_valid_json_and_color_tags_are_escaped()
    {
        _host.Players.Add(Player(0, "[red]Флеймс 👾\ud800\"x"));
        var json = Info().Dump();

        Assert.DoesNotContain("[red]", json, StringComparison.Ordinal);
        Assert.Equal("[red]Флеймс 👾\"x", PlayerField(json, "name").GetString());
    }

    [Fact]
    public void Empty_name_becomes_Unknown()
    {
        _host.Players.Add(Player(0, ""));
        Assert.Equal("Unknown", PlayerField(Info().Dump(), "name").GetString());
    }

    [Fact]
    public void Console_chunks_rejoin_to_the_original_and_never_split_a_surrogate_pair()
    {
        var text = new string('a', 1023) + "👾" + new string('b', 3000);
        var chunks = ServerInfo.ConsoleChunks(text).ToList();

        Assert.Equal(text, string.Concat(chunks));
        Assert.All(chunks, c => Assert.True(c.Length <= 1024));
        Assert.All(chunks, c => Assert.False(char.IsHighSurrogate(c[^1])));
    }

    [Fact]
    public void DumpToFile_writes_atomically_and_reports_failure()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "stats.json");
            Assert.True(Info().DumpToFile(path));
            Assert.Equal("de_dust2", JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("current_map").GetString());
            Assert.False(File.Exists(path + ".tmp"));

            Assert.False(Info().DumpToFile(Path.Combine(dir.FullName, "missing", "stats.json")));
            Assert.False(Info().DumpToFile(""));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static JsonElement PlayerField(string json, string field) =>
        JsonDocument.Parse(json).RootElement.GetProperty("players")[0].GetProperty(field).Clone();
}
