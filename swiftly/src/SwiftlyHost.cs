using PlayersInfo.Core;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace PlayersInfo.Swiftly;

/// IPlayersHost over SwiftlyS2. Players are looked up fresh on every call.
internal sealed class SwiftlyHost(ISwiftlyCore core) : IPlayersHost
{
    public string MapName => core.Engine.GlobalVars.MapName.Value ?? "";

    /// Same filter as the Metamod IsRealPlayer: an active connection, not a bot, not SourceTV, has a SteamID.
    private static bool IsReal(IPlayer p)
        => !p.IsFakeClient && p.Controller is { IsValid: true, IsHLTV: false, SteamID: not 0 } c
           && c.Connected is PlayerConnectedState.Connected or PlayerConnectedState.Connecting or PlayerConnectedState.Reconnecting;

    public IReadOnlyList<LivePlayer> GetPlayers()
        => [.. core.PlayerManager.GetAllPlayers().Where(IsReal).OrderBy(p => p.Slot).Select(p =>
        {
            var c = p.Controller;
            var stats = c.ActionTrackingServices is { IsValid: true } ats ? ats.MatchStats : null;
            return new LivePlayer(p.Slot, c.PlayerName, c.TeamNum, c.SteamID,
                stats?.Kills ?? 0, stats?.Deaths ?? 0, stats?.HeadShotKills ?? 0, (int)c.Ping, c.PlayerPawn.IsValid);
        })];

    public (int Ct, int T) GetScores()
    {
        int ct = 0, t = 0;
        foreach (var team in core.EntitySystem.GetAllEntitiesByDesignerName<CCSTeam>("cs_team_manager"))
        {
            if (team.TeamNum == 3) ct = team.Score;
            else if (team.TeamNum == 2) t = team.Score;
        }
        return (ct, t);
    }
}
