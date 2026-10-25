using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using PlayersInfo.Core;

namespace PlayersInfo.CSSharp;

/// CounterStrikeSharp adapter: the same three console commands as the Metamod build.
[MinimumApiVersion(369)]
public sealed class PlayersInfoPlugin : BasePlugin
{
    public override string ModuleName => "PlayersInfo";
    public override string ModuleVersion => typeof(PlayersInfoPlugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    public override string ModuleAuthor => "FlamesONE";
    public override string ModuleDescription => "Dumps server state and players list as JSON";

    private ServerInfo _info = null!;

    public override void Load(bool hotReload)
    {
        var prime = new SteamPrime();
        _info = new ServerInfo(new CssHost(), prime.IsPrime);

        RegisterListener<Listeners.OnClientConnected>(slot => _info.OnConnected(slot));
        RegisterListener<Listeners.OnClientDisconnect>(slot => _info.OnDisconnected(slot));

        // Server console / RCON only, like the Metamod build (its commands aren't client-executable).
        AddCommand("mm_getinfo", "Dump full server info as JSON to console", (caller, _) =>
        {
            if (caller is null) Print(_info.Dump());
        });
        AddCommand("mm_getinfo_slot", "Dump single player JSON for slot <N> (or {} if empty)", (caller, info) =>
        {
            if (caller is not null) return;
            if (info.ArgCount < 2) Print("usage: mm_getinfo_slot <slot>");
            else Print(_info.DumpSlot(info.GetArg(1)));
        });
        AddCommand("mm_getinfo_file", "Dump full server info as JSON to <path> atomically", (caller, info) =>
        {
            if (caller is not null) return;
            if (info.ArgCount < 2) Print("usage: mm_getinfo_file <path>");
            else Print(_info.DumpToFile(info.GetArg(1)) ? "mm_getinfo_file: ok" : "mm_getinfo_file: failed");
        });
    }

    /// Raw META_CONPRINT pieces plus one newline, so RCON gets the JSON as a single line.
    private static void Print(string text)
    {
        foreach (var chunk in ServerInfo.ConsoleChunks(text)) NativeAPI.PrintToServerConsole(chunk);
        NativeAPI.PrintToServerConsole("\n");
    }
}
