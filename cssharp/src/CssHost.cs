using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using PlayersInfo.Core;

namespace PlayersInfo.CSSharp;

/// IPlayersHost over CounterStrikeSharp. Controllers are looked up fresh on every call.
internal sealed class CssHost : IPlayersHost
{
    public string MapName => Server.MapName;

    /// Same filter as the Metamod IsRealPlayer: an active connection, not a bot, not SourceTV, has a SteamID.
    private static bool IsReal(CCSPlayerController c)
        => c is { IsValid: true, IsBot: false, IsHLTV: false, SteamID: not 0 }
           && c.Connected is PlayerConnectedState.Connected or PlayerConnectedState.Connecting or PlayerConnectedState.Reconnecting;

    public IReadOnlyList<LivePlayer> GetPlayers()
        => [.. Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller").Where(IsReal).OrderBy(c => c.Slot).Select(c =>
        {
            var stats = c.ActionTrackingServices?.MatchStats;
            return new LivePlayer(c.Slot, c.PlayerName, c.TeamNum, c.SteamID,
                stats?.Kills ?? 0, stats?.Deaths ?? 0, stats?.HeadShotKills ?? 0, (int)c.Ping, c.PlayerPawn.IsValid);
        })];

    public (int Ct, int T) GetScores()
    {
        int ct = 0, t = 0;
        foreach (var team in Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager"))
        {
            if (team.TeamNum == 3) ct = team.Score;
            else if (team.TeamNum == 2) t = team.Score;
        }
        return (ct, t);
    }
}
