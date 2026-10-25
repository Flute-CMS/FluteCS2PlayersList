using PlayersInfo.Core;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Plugins;

namespace PlayersInfo.Swiftly;

/// SwiftlyS2 adapter: the same three console commands as the Metamod build. They are registered raw,
/// so they keep their Metamod names instead of getting the sw_ prefix.
[PluginMetadata(
    Id = "PlayersInfo",
    Name = "PlayersInfo",
    Version = Generated.Version,
    Author = "FlamesONE",
    Description = "Dumps server state and players list as JSON",
    Website = "https://github.com/Flute-CMS/FluteCS2PlayersList",
    MinimumAPIVersion = Generated.SwiftlyApi)]
public sealed class PlayersInfoPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private ServerInfo _info = null!;
    private readonly List<Guid> _commands = [];
    private EventDelegates.OnClientConnected? _onConnected;
    private EventDelegates.OnClientDisconnected? _onDisconnected;

    public override void Load(bool hotReload)
    {
        var prime = new SteamPrime();
        _info = new ServerInfo(new SwiftlyHost(Core), prime.IsPrime);

        _onConnected = ev => _info.OnConnected(ev.PlayerId);
        _onDisconnected = ev => _info.OnDisconnected(ev.PlayerId);
        Core.Event.OnClientConnected += _onConnected;
        Core.Event.OnClientDisconnected += _onDisconnected;

        Register("mm_getinfo", "Dump full server info as JSON to console", _ => _info.Dump());
        Register("mm_getinfo_slot", "Dump single player JSON for slot <N> (or {} if empty)",
            args => args.Length < 1 ? "usage: mm_getinfo_slot <slot>" : _info.DumpSlot(args[0]));
        Register("mm_getinfo_file", "Dump full server info as JSON to <path> atomically",
            args => args.Length < 1 ? "usage: mm_getinfo_file <path>"
                : _info.DumpToFile(args[0]) ? "mm_getinfo_file: ok" : "mm_getinfo_file: failed");
    }

    public override void Unload()
    {
        foreach (var id in _commands) Core.Command.UnregisterCommand(id);
        if (_onConnected is not null) Core.Event.OnClientConnected -= _onConnected;
        if (_onDisconnected is not null) Core.Event.OnClientDisconnected -= _onDisconnected;
    }

    /// Server console / RCON only, like the Metamod build (its commands aren't client-executable).
    /// Output goes out as raw console pieces plus one newline, so RCON gets the JSON as a single line.
    private void Register(string name, string help, Func<string[], string> handler)
        => _commands.Add(Core.Command.RegisterCommand(name, ctx =>
        {
            if (ctx.IsSentByPlayer) return;
            foreach (var chunk in ServerInfo.ConsoleChunks(handler(ctx.Args))) Core.ConsoleOutput.WriteToServerConsole(chunk);
            Core.ConsoleOutput.WriteToServerConsole("\n");
        }, registerRaw: true, permission: "", helpText: help));
}
