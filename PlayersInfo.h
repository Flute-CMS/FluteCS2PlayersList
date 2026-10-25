#ifndef _INCLUDE_METAMOD_SOURCE_STUB_PLUGIN_H_
#define _INCLUDE_METAMOD_SOURCE_STUB_PLUGIN_H_

#include <ISmmPlugin.h>
#include "utlvector.h"
#include "ehandle.h"
#include <iserver.h>
#include <entity2/entitysystem.h>
#include <steam/steam_gameserver.h>
#include "igameevents.h"
#include "vector.h"
#include <deque>
#include <functional>
#include <utlstring.h>
#include <KeyValues.h>
#include "CGameRules.h"
#include "CTeam.h"
#include "CCSPlayerController.h"
#include <eiface.h>

class PlayersInfo final : public ISmmPlugin
{
public:
	bool Load(PluginId id, ISmmAPI* ismm, char* error, size_t maxlen, bool late);
	bool Unload(char* error, size_t maxlen);
private:
// Metamod API 18 (builds 1459+) replaced SourceHook with KHook; API 17 (1411 and older) only has SourceHook.
#if METAMOD_PLAPI_VERSION >= 18
	KHook::Return<void> OnClientConnected(IServerGameClients*, CPlayerSlot slot, const char* pszName, uint64 xuid, const char* pszNetworkID, const char* pszAddress, bool bFakePlayer);
	KHook::Return<void> OnClientDisconnect(IServerGameClients*, CPlayerSlot slot, ENetworkDisconnectionReason reason, const char* pszName, uint64 xuid, const char* pszNetworkID);

	KHook::Virtual<IServerGameClients, void, CPlayerSlot, const char*, uint64, const char*, const char*, bool> m_hkClientConnected{ &IServerGameClients::OnClientConnected, this, nullptr, &PlayersInfo::OnClientConnected };
	KHook::Virtual<IServerGameClients, void, CPlayerSlot, ENetworkDisconnectionReason, const char*, uint64, const char*> m_hkClientDisconnect{ &IServerGameClients::ClientDisconnect, this, nullptr, &PlayersInfo::OnClientDisconnect };
#else
	void OnClientConnected(CPlayerSlot slot, const char* pszName, uint64 xuid, const char* pszNetworkID, const char* pszAddress, bool bFakePlayer);
	void OnClientDisconnect(CPlayerSlot slot, ENetworkDisconnectionReason reason, const char* pszName, uint64 xuid, const char* pszNetworkID);
#endif

	const char* GetAuthor();
	const char* GetName();
	const char* GetDescription();
	const char* GetURL();
	const char* GetLicense();
	const char* GetVersion();
	const char* GetDate();
	const char* GetLogTag();
};

PLUGIN_GLOBALVARS();

#endif //_INCLUDE_METAMOD_SOURCE_STUB_PLUGIN_H_
