using UnityEngine;

public static class Helper
{
	public static Vector3 FormatPosToVector3(this Gamepacket.ClientLobbyPosition lobbyPos)
	{
		var pos = new Vector3();
		pos.x = (float)lobbyPos.X;
		pos.y = (float)lobbyPos.Y;
		pos.z = (float)lobbyPos.Z;
		return pos;
	}
}