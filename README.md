# PlayersInfo

Metamod:Source plugin for CS2 that dumps the server state and connected players as a single JSON line. Built for web dashboards, admin panels, and anything that needs to pull live player info over RCON or a shared file.

[![Build](https://github.com/FlamesONE/cs2-PlayersListCommand/actions/workflows/build.yml/badge.svg)](https://github.com/FlamesONE/cs2-PlayersListCommand/actions/workflows/build.yml)

---

## Features

- **Single-line JSON** output via three console commands
- **Full server dump** (`mm_getinfo`), **single player** (`mm_getinfo_slot <N>`), **atomic file write** (`mm_getinfo_file <path>`)
- **Prime status** detection via `UserHasLicenseForApp` with proper Steam API init
- **UTF-8 safe** — emoji, Cyrillic, any nickname passes through, invalid bytes stripped
- **RCON multi-packet** aware — 60-player JSON (~12 KB) delivered in full without truncation
- **No MySQL, no HTTP client, no external services** — just C++ and the CS2 SDK

---

## Quick start

```
# from server console or RCON
mm_getinfo                          # full JSON to console/RCON reply
mm_getinfo_slot 2                   # one player's JSON
mm_getinfo_file /var/www/stats.json # atomic file write
```

Example response (`mm_getinfo`):

```json
{
  "time": 1776504678,
  "current_map": "de_dust2",
  "score_ct": 0,
  "score_t": 0,
  "player_count": 1,
  "players": [{
    "userid": 2,
    "name": "Flames 👾",
    "team": 3,
    "steamid": "76561198295345385",
    "kills": 0,
    "death": 0,
    "headshots": 0,
    "ping": 1,
    "playtime": 31,
    "prime": true,
    "alive": true
  }]
}
```

---

## Install

1. Grab `PlayersInfo.zip` from [Releases](https://github.com/FlamesONE/cs2-PlayersListCommand/releases) or a successful CI run.
2. Extract into `game/csgo/` so you get:
   ```
   game/csgo/addons/PlayersInfo/PlayersInfo.so
   game/csgo/addons/metamod/PlayersInfo.vdf
   ```
3. Restart the server, or hot-load: `meta load addons/PlayersInfo/PlayersInfo`
4. Verify: `meta list` → `PlayersInfo (1.3.3) by Pisex`

**Requirements:**
- Metamod:Source 2.x for CS2
- [Pisex/cs2-menus](https://github.com/Pisex/cs2-menus) (Utils API)
- [Pisex/cs2-lvl_ranks](https://github.com/Pisex/cs2-lvl_ranks) *(optional — enables `rank` field)*

Full build-from-source instructions in [docs/install.md](docs/install.md).

---

## Web integration

Consume the JSON from PHP, Node, Python, or Go over RCON — or serve the file as static via nginx. Full recipes with working code in [docs/web-integration.md](docs/web-integration.md).

Quick RCON example (Node):
```js
import RCON from 'rcon-srcds';
const server = new RCON({ host: '1.2.3.4', port: 27015 });
await server.authenticate('rcon_pw');
const data = JSON.parse((await server.execute('mm_getinfo')).trim());
```

---

## Documentation

| File | Covers |
|---|---|
| [docs/commands.md](docs/commands.md) | each command in detail, edge cases |
| [docs/schema.md](docs/schema.md) | JSON structure, field types and sources |
| [docs/web-integration.md](docs/web-integration.md) | PHP / Node / Python / Go / nginx |
| [docs/install.md](docs/install.md) | build, CI, dependencies, uninstall |

---

## License

GPL. Based on the original plugin by Pisex, maintained in this fork by FlamesONE with crash fixes, schema changes, and the `mm_getinfo_slot` / `mm_getinfo_file` commands.
