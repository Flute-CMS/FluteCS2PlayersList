namespace PlayersInfo.Core;

/// Everything the core needs from a plugin framework. One implementation per platform
/// (cssharp/, swiftly/) plus a fake in the tests. Called on the game thread only.
public interface IPlayersHost
{
    /// gpGlobals->mapname at this moment.
    string MapName { get; }

    /// Connected humans with a SteamID: no bots, no SourceTV, no disconnected slots.
    IReadOnlyList<LivePlayer> GetPlayers();

    /// Round wins from the cs_team_manager entities (TeamNum 3 = CT, 2 = T).
    (int Ct, int T) GetScores();
}

/// Team uses the engine numbering (2 = T, 3 = CT, 1 = spectator). Stats are the match totals from
/// m_pActionTrackingServices->m_matchStats (0 when the services are unavailable).
public sealed record LivePlayer(
    int Slot, string Name, int Team, ulong SteamId, int Kills, int Deaths, int Headshots, int Ping, bool Alive);
