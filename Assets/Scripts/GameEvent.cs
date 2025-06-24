using UnityEngine;

public interface IGameEvent
{
}

public class ServerMessageEvent : IGameEvent
{
	public string Message { get; }

	public ServerMessageEvent(string message)
	{
		Message = message;
	}
}

public class PlayerMovedEvent : IGameEvent
{
	public string PlayerId { get; }
	public Vector3 Position { get; }

	public PlayerMovedEvent(string playerId, Vector3 position)
	{
		PlayerId = playerId;
		Position = position;
	}
}

public class OtherPlayerMovedEvent : IGameEvent
{
	public string PlayerId { get; }
	public Vector3 Position { get; }

	public OtherPlayerMovedEvent(string playerId, Vector3 position)
	{
		PlayerId = playerId;
		Position = position;
	}
}

public class PlayerConnectedEvent : IGameEvent
{
	public string PrivateId { get; }
	public string PublicId { get; }

	public PlayerConnectedEvent(string privateId, string publicId)
	{
		PrivateId = privateId;
		PublicId = publicId;
	}
}

public class PlayerDisconnectedEvent : IGameEvent
{
	public string ClientID { get; }
	public string Reason { get; }

	public PlayerDisconnectedEvent(string clientID, string reason = "")
	{
		Reason = reason;
		ClientID = clientID;
	}
}

public class HeartbeatSentEvent : IGameEvent
{
	public string ClientId { get; }

	public HeartbeatSentEvent(string clientId)
	{
		ClientId = clientId;
	}
}

public class HeartbeatAckReceivedEvent : IGameEvent
{
	public string ClientId { get; }

	public HeartbeatAckReceivedEvent(string clientId)
	{
		ClientId = clientId;
	}
}

public class PlayerJoinedLobbyEvent : IGameEvent
{
	public string PublicId { get; }
	public string ColorHex { get; }
	public Vector3 Position { get; }

	public PlayerJoinedLobbyEvent(string publicId, string colorHex, Vector3 position)
	{
		PublicId = publicId;
		ColorHex = colorHex;
		Position = position;
	}
}

public class ChatMessageReceivedEvent : IGameEvent
{
	public string SenderId { get; }
	public string Message { get; }

	public ChatMessageReceivedEvent(string senderId, string message)
	{
		SenderId = senderId;
		Message = message;
	}
}

public class BuildingSpawnedEvent : IGameEvent
{
	public string BuildingId { get; }
	public Vector3 Position { get; }
	public string BuildingType { get; }

	public BuildingSpawnedEvent(string buildingId, Vector3 position, string buildingType)
	{
		BuildingId = buildingId;
		Position = position;
		BuildingType = buildingType;
	}
}

public class BuildingDestroyedEvent : IGameEvent
{
	public string BuildingId { get; }

	public BuildingDestroyedEvent(string buildingId)
	{
		BuildingId = buildingId;
	}
}