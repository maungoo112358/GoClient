public enum ConnectionState
{
	Disconnected,
	Connecting,
	HandshakeComplete,
	WaitingForUsernamePrompt,
	UsernameValidating,
	Connected,
	Reconnecting,
}

public enum PacketType
{
	HeartbeatAck,
	UsernameResponse,
	UsernamePrompt,
	ReconnectionResponse,
	LobbyJoinBroadcast,
	ClientPosition,
	ServerStatus,
	HandshakeResponse,
	TileSet,
}

public enum TileType
{
	None = 0,
	ROAD_LANE = 1,
	CROSS_SECTION = 2,
	GRASS = 3
}