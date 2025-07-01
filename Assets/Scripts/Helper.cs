using UnityEngine;

public static class Helper
{
	public static Vector3 PosToVector3(this Gamepacket.ClientLobbyPosition lobby)
	{
		return new Vector3(lobby.Position.X, lobby.Position.Y, lobby.Position.Z);
	}

	public static Vector3 PosToVector3(this Gamepacket.ClientPosition client)
	{
		return new Vector3(client.Position.X, client.Position.Y, client.Position.Z);
	}

	public static Vector3 VelocityToVector3(this Gamepacket.ClientPosition client)
	{
		return new Vector3(client.Velocity.X, client.Velocity.Y, client.Velocity.Z);
	}
}