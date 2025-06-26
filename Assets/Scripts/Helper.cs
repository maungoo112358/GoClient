using UnityEngine;

public static class Helper
{
	public static Vector3 PosToVector3(this Gamepacket.ClientLobbyPosition lobbyPos)
	{
		return new Vector3((float)lobbyPos.Position.X, (float)lobbyPos.Position.Y, (float)lobbyPos.Position.Z);
	}

	public static Vector3 PosToVector3(this Gamepacket.ClientPosition clientPos)
	{
		return new Vector3((float)clientPos.Position.X, (float)clientPos.Position.Y, (float)clientPos.Position.Z);
	}

	public static Vector3 VelocityToVector3(this Gamepacket.ClientPosition lobbyPos)
	{
		return new Vector3((float)lobbyPos.Velocity.X, (float)lobbyPos.Velocity.Y, (float)lobbyPos.Velocity.Z);
	}
}