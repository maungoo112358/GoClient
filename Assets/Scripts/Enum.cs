
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
	HandshakeResponse
}