#include "PlayersInfo.h"

#include <nlohmann/json.hpp>

#include "metamod_oslink.h"
#include "schemasystem/schemasystem.h"

#include <cerrno>
#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <limits>
#include <string>
#include <unordered_map>
#include <vector>

using json = nlohmann::json;

PlayersInfo g_PlayersInfo;
PLUGIN_EXPOSE(PlayersInfo, g_PlayersInfo);

IVEngineServer2 *engine = nullptr;
CGameEntitySystem *g_pGameEntitySystem = nullptr;
CEntitySystem *g_pEntitySystem = nullptr;
CGlobalVars *gpGlobals = nullptr;

IUtilsApi *g_pUtils = nullptr;
IPlayersApi *g_pPlayers = nullptr;
ILRApi *g_pLRCore = nullptr;

CGameEntitySystem *GameEntitySystem() { return g_pGameEntitySystem; }

namespace {

constexpr int kMaxPlayers = 64;
constexpr int kTeamCT = 3;
constexpr int kTeamT = 2;
constexpr AppId_t kAppPrime = 624820;
constexpr size_t kConsoleChunk = 1024;

time_t g_ConnectionTime[kMaxPlayers] = {};
std::unordered_map<uint64, bool> g_PrimeCache;
CSteamGameServerAPIContext g_SteamApi;
bool g_SteamApiReady = false;

bool IsValidSlot(int slot) noexcept {
  return slot >= 0 && slot < kMaxPlayers;
}

int GetSlotLimit() noexcept {
  if (!gpGlobals || gpGlobals->maxClients <= 0)
    return kMaxPlayers;
  return gpGlobals->maxClients < kMaxPlayers ? gpGlobals->maxClients
                                             : kMaxPlayers;
}

bool IsValidServerSlot(int slot) noexcept {
  return slot >= 0 && slot < GetSlotLimit();
}

bool IsActivePlayerState(PlayerConnectedState state) noexcept {
  switch (state) {
    case PlayerConnectedState::PlayerConnected:
    case PlayerConnectedState::PlayerConnecting:
    case PlayerConnectedState::PlayerReconnecting:
      return true;
    default:
      return false;
  }
}

bool TryParseSlotArgument(const char *value, int &slot) noexcept {
  if (!value || !*value)
    return false;

  for (const unsigned char *p = reinterpret_cast<const unsigned char *>(value);
       *p; ++p) {
    if (!std::isdigit(*p))
      return false;
  }

  errno = 0;
  char *end = nullptr;
  const long parsed = std::strtol(value, &end, 10);
  if (errno == ERANGE || end == value || !end || *end != '\0' ||
      parsed > std::numeric_limits<int>::max())
    return false;

  slot = static_cast<int>(parsed);
  return true;
}

bool IsRealPlayer(int slot) {
  if (!IsValidServerSlot(slot))
    return false;
  CCSPlayerController *ctrl = CCSPlayerController::FromSlot(slot);
  if (!ctrl)
    return false;
  // CS2 keeps controllers alive after disconnect; only active connection states
  // should contribute to the live player list.
  if (!IsActivePlayerState(ctrl->m_iConnected()))
    return false;
  if (ctrl->IsBot())
    return false;
  return ctrl->m_steamID() != 0;
}

bool EnsureSteamApi() {
  if (g_SteamApiReady)
    return true;
  g_SteamApiReady = g_SteamApi.Init();
  return g_SteamApiReady;
}

bool LookupPrime(uint64 steamid64) {
  if (steamid64 == 0)
    return false;
  auto it = g_PrimeCache.find(steamid64);
  return it != g_PrimeCache.end() ? it->second : false;
}

void CachePrime(uint64 steamid64) {
  if (steamid64 == 0 || g_PrimeCache.count(steamid64))
    return;
  if (!EnsureSteamApi())
    return;
  ISteamGameServer *pServer = g_SteamApi.SteamGameServer();
  if (!pServer || !pServer->BLoggedOn())
    return;
  CSteamID id(steamid64);
  const bool prime = pServer->UserHasLicenseForApp(id, kAppPrime) ==
                     k_EUserHasLicenseResultHasLicense;
  g_PrimeCache.emplace(steamid64, prime);
}

std::string SanitizeUtf8(const char *s) {
  if (!s || !*s)
    return std::string();
  std::string out;
  out.reserve(std::strlen(s));
  const unsigned char *p = reinterpret_cast<const unsigned char *>(s);
  while (*p) {
    unsigned char c = *p;
    int len;
    if (c < 0x80) len = 1;
    else if ((c & 0xE0) == 0xC0) len = 2;
    else if ((c & 0xF0) == 0xE0) len = 3;
    else if ((c & 0xF8) == 0xF0) len = 4;
    else { ++p; continue; }

    bool valid = true;
    for (int i = 1; i < len; ++i) {
      if ((p[i] & 0xC0) != 0x80) { valid = false; break; }
    }
    if (valid) {
      out.append(reinterpret_cast<const char *>(p), len);
      p += len;
    } else {
      ++p;
    }
  }
  return out;
}

void ConPrintChunked(const std::string &str) {
  const size_t n = str.size();
  for (size_t i = 0; i < n; i += kConsoleChunk)
    META_CONPRINT(str.substr(i, kConsoleChunk).c_str());
}

bool WriteFileAtomic(const char *path, const std::string &data) {
  if (!path || !*path)
    return false;
  std::string tmp = std::string(path) + ".tmp";
  FILE *f = std::fopen(tmp.c_str(), "wb");
  if (!f)
    return false;
  const size_t written = std::fwrite(data.data(), 1, data.size(), f);
  std::fclose(f);
  if (written != data.size()) {
    std::remove(tmp.c_str());
    return false;
  }
  if (std::rename(tmp.c_str(), path) != 0) {
    std::remove(tmp.c_str());
    return false;
  }
  return true;
}

struct TeamScores {
  int ct = 0;
  int t = 0;
};

TeamScores GetTeamScores() {
  TeamScores scores;
  std::vector<CEntityInstance *> teams =
      UTIL_FindEntityByClassnameAll("cs_team_manager");
  for (CEntityInstance *inst : teams) {
    auto *pTeam = static_cast<CTeam *>(inst);
    if (!pTeam)
      continue;

    const int num = pTeam->m_iTeamNum();
    if (num == kTeamCT)
      scores.ct = pTeam->m_iScore();
    else if (num == kTeamT)
      scores.t = pTeam->m_iScore();
  }
  return scores;
}

std::string GetMapNameSafe() {
  if (!gpGlobals)
    return std::string();
  char buf[64] = {};
  g_SMAPI->Format(buf, sizeof(buf), "%s", gpGlobals->mapname);
  return std::string(buf);
}

json BuildPlayerJson(int slot, CCSPlayerController *ctrl, time_t now) {
  json j;

  const char *name = ctrl->GetPlayerName();
  const uint64 steamid64 = ctrl->m_steamID();

  std::string sanitized = SanitizeUtf8(name);
  j["userid"] = slot;
  j["name"] = sanitized.empty() ? std::string("Unknown") : sanitized;
  j["team"] = ctrl->GetTeam();
  j["steamid"] = std::to_string(steamid64);

  int kills = 0, deaths = 0, headshots = 0;
  if (auto *ats = ctrl->m_pActionTrackingServices()) {
    kills = ats->m_matchStats().m_iKills();
    deaths = ats->m_matchStats().m_iDeaths();
    headshots = ats->m_matchStats().m_iHeadShotKills();
  }
  j["kills"] = kills;
  j["death"] = deaths;
  j["headshots"] = headshots;

  j["ping"] = ctrl->m_iPing();
  j["playtime"] = static_cast<int64_t>(now - g_ConnectionTime[slot]);
  CachePrime(steamid64);
  j["prime"] = LookupPrime(steamid64);
  j["alive"] = (ctrl->m_hPlayerPawn() != nullptr);

  if (g_pLRCore && g_pLRCore->GetClientStatus(slot))
    j["rank"] = g_pLRCore->GetClientInfo(slot, ST_RANK);

  return j;
}

json GetServerInfo() {
  const time_t now = std::time(nullptr);

  json jdata;
  jdata["time"] = now;
  jdata["current_map"] = GetMapNameSafe();

  const TeamScores scores = GetTeamScores();
  jdata["score_ct"] = scores.ct;
  jdata["score_t"] = scores.t;

  json players = json::array();
  for (int i = 0; i < GetSlotLimit(); ++i) {
    if (!IsRealPlayer(i)) {
      g_ConnectionTime[i] = 0;
      continue;
    }

    if (g_ConnectionTime[i] == 0)
      g_ConnectionTime[i] = now;

    CCSPlayerController *ctrl = CCSPlayerController::FromSlot(i);
    if (!ctrl)
      continue;

    players.push_back(BuildPlayerJson(i, ctrl, now));
  }

  jdata["player_count"] = players.size();
  jdata["players"] = std::move(players);
  return jdata;
}

}  // namespace

CON_COMMAND_F(mm_getinfo, "Dump full server info as JSON to console",
              FCVAR_GAMEDLL) {
  const std::string dump = GetServerInfo().dump();
  ConPrintChunked(dump);
  META_CONPRINT("\n");
}

CON_COMMAND_F(mm_getinfo_file,
              "Dump full server info as JSON to <path> atomically",
              FCVAR_GAMEDLL) {
  if (args.ArgC() < 2) {
    META_CONPRINT("usage: mm_getinfo_file <path>\n");
    return;
  }
  const std::string dump = GetServerInfo().dump();
  if (WriteFileAtomic(args[1], dump))
    META_CONPRINT("mm_getinfo_file: ok\n");
  else
    META_CONPRINT("mm_getinfo_file: failed\n");
}

CON_COMMAND_F(mm_getinfo_slot,
              "Dump single player JSON for slot <N> (or {} if empty)",
              FCVAR_GAMEDLL) {
  if (args.ArgC() < 2) {
    META_CONPRINT("usage: mm_getinfo_slot <slot>\n");
    return;
  }
  int slot = -1;
  json j;
  if (TryParseSlotArgument(args[1], slot) && IsRealPlayer(slot)) {
    if (g_ConnectionTime[slot] == 0)
      g_ConnectionTime[slot] = std::time(nullptr);
    if (CCSPlayerController *ctrl = CCSPlayerController::FromSlot(slot))
      j = BuildPlayerJson(slot, ctrl, std::time(nullptr));
  }
  const std::string dump = j.is_null() ? std::string("{}") : j.dump();
  ConPrintChunked(dump);
  META_CONPRINT("\n");
}

static void OnStartupServer() {
  if (!g_pUtils)
    return;
  g_pGameEntitySystem = g_pUtils->GetCGameEntitySystem();
  g_pEntitySystem = g_pUtils->GetCEntitySystem();
  gpGlobals = g_pUtils->GetCGlobalVars();

  g_PrimeCache.clear();
  for (int i = 0; i < kMaxPlayers; ++i)
    g_ConnectionTime[i] = 0;
}

static void OnPlayerConnect(const char *, IGameEvent *pEvent, bool) {
  if (!pEvent)
    return;
  const int slot = pEvent->GetInt("userid");
  if (!IsValidSlot(slot))
    return;
  g_ConnectionTime[slot] = std::time(nullptr);
}

static void OnPlayerDisconnect(const char *, IGameEvent *pEvent, bool) {
  if (!pEvent)
    return;
  const int slot = pEvent->GetInt("userid");
  if (!IsValidSlot(slot))
    return;
  g_ConnectionTime[slot] = 0;
}

bool PlayersInfo::Load(PluginId id, ISmmAPI *ismm, char *error, size_t maxlen,
                       bool late) {
  PLUGIN_SAVEVARS();

  GET_V_IFACE_CURRENT(GetEngineFactory, g_pCVar, ICvar, CVAR_INTERFACE_VERSION);
  GET_V_IFACE_ANY(GetEngineFactory, g_pSchemaSystem, ISchemaSystem,
                  SCHEMASYSTEM_INTERFACE_VERSION);
  GET_V_IFACE_CURRENT(GetEngineFactory, engine, IVEngineServer2,
                      SOURCE2ENGINETOSERVER_INTERFACE_VERSION);

  g_SMAPI->AddListener(this, this);
  ConVar_Register(FCVAR_RELEASE | FCVAR_SERVER_CAN_EXECUTE | FCVAR_GAMEDLL);

  return true;
}

bool PlayersInfo::Unload(char *error, size_t maxlen) {
  if (g_pUtils)
    g_pUtils->ClearAllHooks(g_PLID);
  ConVar_Unregister();
  g_PrimeCache.clear();
  g_SteamApiReady = false;
  for (int i = 0; i < kMaxPlayers; ++i)
    g_ConnectionTime[i] = 0;
  return true;
}

void PlayersInfo::AllPluginsLoaded() {
  int ret = 0;

  g_pUtils = static_cast<IUtilsApi *>(
      g_SMAPI->MetaFactory(Utils_INTERFACE, &ret, nullptr));
  if (ret == META_IFACE_FAILED) {
    ConColorMsg(Color(255, 0, 0, 255), "[%s] Missing Utils system plugin\n",
                GetLogTag());
    engine->ServerCommand(("meta unload " + std::to_string(g_PLID)).c_str());
    return;
  }

  g_pLRCore = static_cast<ILRApi *>(
      g_SMAPI->MetaFactory(LR_INTERFACE, &ret, nullptr));
  if (ret == META_IFACE_FAILED)
    g_pLRCore = nullptr;

  g_pUtils->StartupServer(g_PLID, OnStartupServer);
  g_pUtils->HookEvent(g_PLID, "player_connect", OnPlayerConnect);
  g_pUtils->HookEvent(g_PLID, "player_disconnect", OnPlayerDisconnect);

  if (!gpGlobals)
    OnStartupServer();
}

const char *PlayersInfo::GetLicense() { return "GPL"; }
const char *PlayersInfo::GetVersion() { return "1.3.3"; }
const char *PlayersInfo::GetDate() { return __DATE__; }
const char *PlayersInfo::GetLogTag() { return "PlayersInfo"; }
const char *PlayersInfo::GetAuthor() { return "Pisex"; }
const char *PlayersInfo::GetDescription() { return "PlayersInfo"; }
const char *PlayersInfo::GetName() { return "PlayersInfo"; }
const char *PlayersInfo::GetURL() { return "https://discord.gg/g798xERK5Y"; }
