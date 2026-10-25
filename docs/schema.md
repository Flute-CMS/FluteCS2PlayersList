# JSON schema

## Server info

Returned by `mm_getinfo` and written by `mm_getinfo_file`.

```jsonc
{
  "time": 1776502984,              // unix timestamp (int, seconds)
  "current_map": "de_dust2",       // map name, "" if not yet known
  "score_ct": 2,                   // CT team score (int)
  "score_t": 0,                    // T team score (int)
  "player_count": 1,               // length of the players array (int)
  "players": [ /* Player[] */ ]    // array of Player objects
}
```

### Field types

| Field | Type | Description |
|---|---|---|
| `time` | `int64` | Unix timestamp at the moment the command runs |
| `current_map` | `string` | Current map name (`de_dust2`, `cs_office`, …). Empty string if `gpGlobals` is not yet initialized |
| `score_ct` | `int32` | CT score. Read from a `cs_team_manager` entity with `m_iTeamNum == 3` |
| `score_t` | `int32` | T score. `cs_team_manager` with `m_iTeamNum == 2` |
| `player_count` | `int` | Number of elements in `players` |
| `players` | `array<Player>` | Real, connected players (bots excluded) |

---

## Player

A single entry in `players[]`. Also returned directly by `mm_getinfo_slot N`.

```jsonc
{
  "userid": 2,                      // slot number, 0..maxClients-1
  "name": "Flames 👾",               // UTF-8 nickname (invalid bytes stripped)
  "team": 3,                         // 0=unassigned, 1=spectator, 2=T, 3=CT
  "steamid": "76561198295345385",    // SteamID64 as a string
  "kills": 2,                        // match kills
  "death": 0,                        // match deaths
  "headshots": 1,                    // match headshot kills
  "ping": 1,                         // ping in ms
  "playtime": 6,                     // seconds since connect
  "prime": true,                     // has Prime Status (CS2)
  "alive": true                      // has a live pawn
}
```

### Field types

| Field | Type | Source | Notes |
|---|---|---|---|
| `userid` | `int` | slot number | 0-based, matches the slot in `status` output. **Not a SteamID, not an entity index** |
| `name` | `string` | `CCSPlayerController::GetPlayerName()` | Passed through a UTF-8 sanitizer; invalid bytes are stripped |
| `team` | `int` | `CCSPlayerController::GetTeam()` | CS values: 0 unassigned, 1 spec, 2 T, 3 CT |
| `steamid` | `string` | `CBasePlayerController::m_steamID` | Sent as a string so JS `Number` precision is not lost |
| `kills` | `int` | `m_pActionTrackingServices->m_matchStats->m_iKills` | 0 if services are unavailable |
| `death` | `int` | `m_iDeaths` | 0 if unavailable. Spelled `death` (not `deaths`) for backwards compatibility with v1.0 clients |
| `headshots` | `int` | `m_iHeadShotKills` | |
| `ping` | `int` | `m_iPing` | May be 0 for a few seconds after connect |
| `playtime` | `int64` | `time - connection_time` | Seconds since the client connected (or since the plugin first saw the player, if it was loaded mid-match) |
| `prime` | `bool` | `ISteamGameServer::UserHasLicenseForApp(steamid, 624820)` | Cached per SteamID. Returns `false` until the Steam gameserver has logged in |
| `alive` | `bool` | `m_hPlayerPawn != null` | |

---

## Not included

- **Bots** (`CCSPlayerController::IsBot() == true`) are filtered out
- **Disconnecting / disconnected / reserved controllers** — only active
  controller states (`PlayerConnected`, `PlayerConnecting`,
  `PlayerReconnecting`) are included
- **Slots beyond `maxClients`** — on a 32-slot server, slots 32..63 are skipped
- **HLTV** clients — caught by the `IsBot()` filter

---

## Known caveats

### `current_map: ""` right after hot-reload

If the plugin is reloaded via `meta load/unload` while a map is running, `gpGlobals->mapname` may be empty until the next map change. `map <name>` / `changelevel` populates it.

### Prime detection requires Steam login

`prime` is cached lazily per SteamID on the first `mm_getinfo` call after the player authorizes. Until `SteamGameServer()->BLoggedOn()` returns true (typically a few seconds after server start), `prime` stays `false`.
