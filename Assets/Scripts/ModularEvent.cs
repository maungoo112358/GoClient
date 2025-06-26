using UnityEngine;

public interface IModularEvent
{
}

public class ServerMessageEvent : IModularEvent
{
	public string Message { get; }

	public ServerMessageEvent(string message)
	{
		Message = message;
	}
}


public class PlayerMovementEvent : IModularEvent
{
	public string ClientID { get; }
	public Vector3 Position { get; }
	public Vector3 Velocity { get; }
	public float Timestamp { get; }

	public PlayerMovementEvent(string clientID, Vector3 position, Vector3 velocity,float timestamp)
	{
		ClientID = clientID;
		Position = position;
		Velocity = velocity;
		Timestamp = timestamp;
	}
}

public class PlayerConnectedEvent : IModularEvent
{
	public string PrivateID { get; }
	public string PublicID { get; }

	public PlayerConnectedEvent(string privavteID, string publicID)
	{
		PrivateID = privavteID;
		PublicID = publicID;
	}
}

public class PlayerDisconnectedEvent : IModularEvent
{
	public string ClientID { get; }
	public string Reason { get; }

	public PlayerDisconnectedEvent(string clientID, string reason = "")
	{
		Reason = reason;
		ClientID = clientID;
	}
}

public class HeartbeatSentEvent : IModularEvent
{
	public string ClientID { get; }

	public HeartbeatSentEvent(string clientID)
	{
		ClientID = clientID;
	}
}

public class HeartbeatAckReceivedEvent : IModularEvent
{
	public string ClientID { get; }

	public HeartbeatAckReceivedEvent(string clientID)
	{
		ClientID = clientID;
	}
}

public class PlayerJoinedLobbyEvent : IModularEvent
{
	public string PublicID { get; }
	public string ColorHex { get; }
	public Vector3 Position { get; }
	public bool IsLocalPlayer;

	public PlayerJoinedLobbyEvent(string publicID, string colorHex, Vector3 position,bool isLocalPlayer)
	{
		PublicID = publicID;
		ColorHex = colorHex;
		Position = position;
		IsLocalPlayer = isLocalPlayer;
	}
}

public class ChatMessageReceivedEvent : IModularEvent
{
	public string SenderId { get; }
	public string Message { get; }

	public ChatMessageReceivedEvent(string senderId, string message)
	{
		SenderId = senderId;
		Message = message;
	}
}

public class BuildingSpawnedEvent : IModularEvent
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

public class BuildingDestroyedEvent : IModularEvent
{
	public string BuildingId { get; }

	public BuildingDestroyedEvent(string buildingId)
	{
		BuildingId = buildingId;
	}
}