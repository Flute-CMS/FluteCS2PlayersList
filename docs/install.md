# Install & build

## Runtime requirements

| Dependency | Required | Purpose |
|---|---|---|
| CS2 dedicated server (Linux, x86_64) | yes | the server this plugin attaches to |
| Metamod:Source 2.x for CS2 | yes | plugin host |
| [Pisex/SchemaEntity](https://github.com/Pisex/SchemaEntity) | yes (build time) | schema accessors (`CCSPlayerController`, `CTeam`, etc.) |

> Note: since 1.4.0 the plugin is standalone. It no longer needs `Pisex/cs2-menus` (Utils API) or `Pisex/cs2-lvl_ranks`: globals and the entity system come straight from the engine, and connect/disconnect is tracked with Metamod's built-in hooks. The `rank` field was removed together with the LvL Ranks integration.

## Quick install (prebuilt)

1. Download `PlayersInfo.zip` from the [Releases](https://github.com/FlamesONE/cs2-PlayersListCommand/releases) page, or from a successful CI run's artifacts.
2. Extract into `<csgo-root>/game/csgo/` so you end up with:
   ```
   game/csgo/addons/PlayersInfo/PlayersInfo.so
   game/csgo/addons/metamod/PlayersInfo.vdf
   ```
3. Restart the server (or `meta load addons/PlayersInfo/PlayersInfo`).
4. Verify with `meta list` — you should see `PlayersInfo (1.4.0) by Pisex`.

## Build from source

Requires:
- Ubuntu 20.04 (or equivalent with clang-10 / gcc-9)
- Python 3, AMBuild
- Metamod:Source checkout (`$MMS_PATH`)
- HL2SDK-CS2 checkout (`$HL2SDK_ROOT/hl2sdk-cs2`)
- `SchemaEntity` sibling directory (next to this repo)

```bash
pip install git+https://github.com/alliedmodders/ambuild
export MMS_PATH=/path/to/metamod-source
export HL2SDK_ROOT=/path/to/sdks    # must contain hl2sdk-cs2/
git clone https://github.com/Pisex/SchemaEntity ../SchemaEntity

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

- **on push/PR to `main`:** builds the plugin in `ghcr.io/pisex/ubuntu20` and uploads `cs2-PlayersListCommand.zip` as a workflow artifact.
- **on release:** builds and attaches the same ZIP to the release.
- **manual:** `workflow_dispatch` is enabled, you can rerun from the Actions tab.

The workflow patches `SchemaEntity` at build time to match current `hl2sdk-cs2`:
- `NetworkStateChanged_t` → `NetworkStateChangedData`
- `FL_{PAWN,CONTROLLER}_FAKECLIENT` → `FL_FAKECLIENT`

If upstream `Pisex/SchemaEntity` updates, the patches can be dropped.

## Uninstall

```bash
rm <csgo-root>/game/csgo/addons/metamod/PlayersInfo.vdf
rm -r <csgo-root>/game/csgo/addons/PlayersInfo
```

Metamod will skip the plugin on next launch.
