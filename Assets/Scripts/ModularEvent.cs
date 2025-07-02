using Gamepacket;
using System.Collections.Generic;
using UnityEngine;

public interface IModularEvent
{
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

public class ServerMessageEvent : IModularEvent
{
	public string Message { get; }

	public ServerMessageEvent(string message)
	{
		Message = message;
	}
}

public class UsernamePromptEvent : IModularEvent
{
	public string PromptMessage { get; }

	public UsernamePromptEvent(string promptMessage)
	{
		PromptMessage = promptMessage;
	}
}

public class UsernameRequestEvent : IModularEvent
{
	public string Username { get; }

	public UsernameRequestEvent(string username)
	{
		Username = username;
	}
}

public class UsernameResponseEvent : IModularEvent
{
	public string Username { get; }
	public bool IsAccepted { get; }
	public string Message { get; }

	public UsernameResponseEvent(string username, bool isAccepted, string message)
	{
		Username = username;
		IsAccepted = isAccepted;
		Message = message;
	}
}

public class ReconnectionAttemptEvent : IModularEvent
{
	public string Username { get; }
	public string SessionToken { get; }

	public ReconnectionAttemptEvent(string username, string sessionToken)
	{
		Username = username;
		SessionToken = sessionToken;
	}
}

public class ReconnectionResponseEvent : IModularEvent
{
	public bool IsSuccessful { get; }
	public string Message { get; }
	public string PrivateID { get; }
	public string PublicID { get; }

	public ReconnectionResponseEvent(bool isSuccessful, string message, string privateID = "", string publicID = "")
	{
		IsSuccessful = isSuccessful;
		Message = message;
		PrivateID = privateID;
		PublicID = publicID;
	}
}

public class PlayerJoinedLobbyEvent : IModularEvent
{
	public string PublicID { get; }
	public string ColorHex { get; }
	public Vector3 Position { get; }
	public bool IsLocalPlayer;

	public PlayerJoinedLobbyEvent(string publicID, string colorHex, Vector3 position, bool isLocalPlayer)
	{
		PublicID = publicID;
		ColorHex = colorHex;
		Position = position;
		IsLocalPlayer = isLocalPlayer;
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

public class PlayerMovementEvent : IModularEvent
{
	public string ClientID { get; }
	public Vector3 Position { get; }
	public Vector3 Velocity { get; }
	public float Timestamp { get; }

	public PlayerMovementEvent(string clientID, Vector3 position, Vector3 velocity, float timestamp)
	{
		ClientID = clientID;
		Position = position;
		Velocity = velocity;
		Timestamp = timestamp;
	}
}

public class TileGenerationEvent : IModularEvent
{
	List<Tile> TileSet = new ();

	public TileGenerationEvent(List<Tile> tileSet)
	{
		TileSet = tileSet;
	}
}

public class Tile
{
	public string TileId { get; }
	public Vector3 Position { get; }
	public Vector3 Rotation { get; }
	public Vector3 Scale { get; }
	public TileType TileType { get; }
	public bool IsScalable { get; }

	public Tile(string tileId, Vector3 position, Vector3 rotation, Vector3 scale, TileType tileType, bool isScalable)
	{
		TileId = tileId;
		Position = position;
		Rotation = rotation;
		Scale = scale;
		TileType = tileType;
		IsScalable = isScalable;
	}
}

public class TileDestroyedEvent : IModularEvent
{
	public string TileId { get; }

	public TileDestroyedEvent(string tileId)
	{
		TileId = tileId;
	}
}