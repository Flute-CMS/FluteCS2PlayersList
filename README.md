# PlayersInfo

CS2 plugin (Metamod:Source, CounterStrikeSharp or SwiftlyS2) that dumps the server state and connected players as a single JSON line. Built for web dashboards, admin panels, and anything that needs to pull live player info over RCON or a shared file.

[![Build](https://github.com/Flute-CMS/FluteCS2PlayersList/actions/workflows/build.yml/badge.svg)](https://github.com/Flute-CMS/FluteCS2PlayersList/actions/workflows/build.yml)

---

## Features

- **Single-line JSON** output via three console commands
- **Full server dump** (`mm_getinfo`), **single player** (`mm_getinfo_slot <N>`), **atomic file write** (`mm_getinfo_file <path>`)
- **Prime status** detection via `UserHasLicenseForApp` with proper Steam API init
- **UTF-8 safe** — emoji, Cyrillic, any nickname passes through, invalid bytes stripped
- **RCON multi-packet** aware — 60-player JSON (~12 KB) delivered in full without truncation
- **Standalone** — no Utils, no LvL Ranks, no MySQL, no HTTP client, no other plugins

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

Pick one archive from [Releases](https://github.com/Flute-CMS/FluteCS2PlayersList/releases) and extract it into `game/csgo/`:

| Your server runs | Archive | Check it loaded |
|---|---|---|
| Metamod:Source 2.0 build **1459+** (KHook) | `PlayersInfo_metamod_<ver>_linux.zip` | `meta list` |
| Metamod:Source 2.0 build **≤1411** (SourceHook) | `PlayersInfo_metamod-legacy_<ver>_linux.zip` | `meta list` |
| CounterStrikeSharp 1.0.369+ | `PlayersInfo_cssharp_<ver>.zip` | `css_plugins list` |
| SwiftlyS2 1.4.9+ | `PlayersInfo_swiftly_<ver>.zip` | `sw plugins list` |

`meta version` shows the Metamod build after `dev+`. Build 1459 moved Metamod from SourceHook to KHook (plugin API 17 → 18), and each Metamod refuses the other API. Windows archives are named the same with `_windows`.

All four register the same commands and print the same JSON, so install only one of them.

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
