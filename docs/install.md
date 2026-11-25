# Install & build

## Runtime requirements

| Dependency | Required | Purpose |
|---|---|---|
| CS2 dedicated server (Linux, x86_64) | yes | the server this plugin attaches to |
| Metamod:Source 2.0 (build 1459+ for `metamod`, ≤1411 for `metamod-legacy`), CounterStrikeSharp 1.0.369+ or SwiftlyS2 1.4.9+ | one of them | plugin host |
| [Pisex/SchemaEntity](https://github.com/Pisex/SchemaEntity) | yes (build time) | schema accessors (`CCSPlayerController`, `CTeam`, etc.) |

> Note: since 1.4.0 the plugin is standalone. It no longer needs `Pisex/cs2-menus` (Utils API) or `Pisex/cs2-lvl_ranks`: globals and the entity system come straight from the engine, and connect/disconnect is tracked with Metamod's built-in hooks. The `rank` field was removed together with the LvL Ranks integration.

## Quick install (prebuilt)

1. Download the archive for your framework from the [Releases](https://github.com/Flute-CMS/FluteCS2PlayersList/releases) page (see the table in the README). The steps below are for Metamod; CounterStrikeSharp and SwiftlyS2 archives extract to `addons/counterstrikesharp/plugins/PlayersInfo/` and `addons/swiftlys2/plugins/PlayersInfo/`.
2. Extract into `<csgo-root>/game/csgo/` so you end up with:
   ```
   game/csgo/addons/PlayersInfo/PlayersInfo.so
   game/csgo/addons/metamod/PlayersInfo.vdf
   ```
3. Restart the server (or `meta load addons/PlayersInfo/PlayersInfo`).
4. Verify with `meta list` — you should see `PlayersInfo (1.5.1) by Pisex`.

## Build from source

Requires:
- Ubuntu 20.04 (or equivalent with clang-10 / gcc-9)
- Python 3, AMBuild
- Metamod:Source checkout (`$MMS_PATH`)
- S2SDK checkout at the `sdk_ref` from CI (`$HL2SDK_ROOT/hl2sdk-cs2`)
- `SchemaEntity` sibling directory (next to this repo)

```bash
pip install git+https://github.com/alliedmodders/ambuild
export MMS_PATH=/path/to/metamod-source
export HL2SDK_ROOT=/path/to/sdks    # must contain hl2sdk-cs2/
git clone https://github.com/Pisex/SchemaEntity ../SchemaEntity
git -C ../SchemaEntity checkout e7965b3c83ec684d44584ee6457c5482ee5d9db1
bash prepare-schemaentity.sh ../SchemaEntity

mkdir build && cd build
python3 ../configure.py \
  --sdks=cs2 \
  --targets=x86_64 \
  --enable-optimize \
  --hl2sdk-manifests=../hl2sdk-manifests \
  --mms_path=$MMS_PATH \
  --hl2sdk-root=$HL2SDK_ROOT
ambuild
```

The artifact lands in `build/package/addons/PlayersInfo/PlayersInfo.so` and `build/package/addons/metamod/PlayersInfo.vdf`.

## CI

The repository ships with a GitHub Actions workflow at `.github/workflows/build.yml`:

- **Metamod, Linux and Windows** use Metamod 2.0.0-git1491 (KHook, API 18) and S2SDK for CS2 1.42.0.1. The legacy package uses Metamod build 1411 (SourceHook, API 17) and the archived HL2SDK for older game builds.
- **CounterStrikeSharp + SwiftlyS2:** `./build.sh` runs the core tests, then packs both C# plugins into `dist/`.
- **on push to `main`:** if the version from `PlayersInfo.cpp` has no release yet, all archives are published as `v<version>` with `.github/release-body.md` on top.

The build runs `prepare-schemaentity.sh` against the pinned SchemaEntity checkout.

## Uninstall

```bash
rm <csgo-root>/game/csgo/addons/metamod/PlayersInfo.vdf
rm -r <csgo-root>/game/csgo/addons/PlayersInfo
```

Metamod will skip the plugin on next launch.
