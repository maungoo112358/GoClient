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
	CROSS_INTERSECTION_2_WAYS = 2,
	CROSS_INTERSECTION_3_WAYS = 3,
	CROSS_INTERSECTION_4_WAYS = 4,
	GRASS = 5
}