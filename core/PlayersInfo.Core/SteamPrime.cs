using System.Runtime.InteropServices;

namespace PlayersInfo.Core;

/// Prime Status via ISteamGameServer::UserHasLicenseForApp, the same call the Metamod build makes.
/// Goes through the flat Steamworks API of the steam_api library the server process already has loaded,
/// so it needs nothing from the plugin framework.
public sealed unsafe class SteamPrime
{
    private const uint PrimeAppId = 624820;
    private const int HasLicense = 0;

    private readonly Dictionary<ulong, bool> _cache = [];
    private readonly delegate* unmanaged<nint> _server;
    private readonly delegate* unmanaged<nint, byte> _loggedOn;
    private readonly delegate* unmanaged<nint, ulong, uint, int> _hasLicense;

    public SteamPrime()
    {
        if (!NativeLibrary.TryLoad(OperatingSystem.IsWindows() ? "steam_api64" : "libsteam_api.so", out var lib)) return;
        if (NativeLibrary.TryGetExport(lib, "SteamAPI_SteamGameServer_v015", out var server)
            && NativeLibrary.TryGetExport(lib, "SteamAPI_ISteamGameServer_BLoggedOn", out var loggedOn)
            && NativeLibrary.TryGetExport(lib, "SteamAPI_ISteamGameServer_UserHasLicenseForApp", out var hasLicense))
        {
            _server = (delegate* unmanaged<nint>)server;
            _loggedOn = (delegate* unmanaged<nint, byte>)loggedOn;
            _hasLicense = (delegate* unmanaged<nint, ulong, uint, int>)hasLicense;
        }
    }

    /// false until the game server has logged on to Steam; only definite answers are cached.
    public bool IsPrime(ulong steamId)
    {
        if (steamId == 0) return false;
        if (_cache.TryGetValue(steamId, out var prime)) return prime;
        if (_server == null) return false;

        var server = _server();
        if (server == 0 || _loggedOn(server) == 0) return false;
        prime = _hasLicense(server, steamId, PrimeAppId) == HasLicense;
        _cache[steamId] = prime;
        return prime;
    }
}
