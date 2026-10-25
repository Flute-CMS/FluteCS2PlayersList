## Which archive do I need?

| Your server runs | Download |
|---|---|
| Metamod:Source **2.0 build 1459 or newer** (KHook) | `PlayersInfo_metamod_<version>_linux.zip` / `_windows.zip` |
| Metamod:Source **2.0 build 1411 or older** (SourceHook) | `PlayersInfo_metamod-legacy_<version>_linux.zip` / `_windows.zip` |
| CounterStrikeSharp **1.0.369+** | `PlayersInfo_cssharp_<version>.zip` |
| SwiftlyS2 **1.4.9+** | `PlayersInfo_swiftly_<version>.zip` |

Not sure about Metamod? Run `meta version` in the server console. The build number is the part after `dev+` (for example `2.0.0-dev+1483`). Metamod build 1459 switched from SourceHook to KHook and raised the plugin API from 17 to 18. Each Metamod refuses the other API, so with the wrong archive the plugin won't load and Metamod prints the reason.

Every archive is standalone. You don't need Game Utils (cs2-menus), LvL Ranks or any other plugin. All of them register the same commands (`mm_getinfo`, `mm_getinfo_slot`, `mm_getinfo_file`) and print the same JSON. Install only one of them.
