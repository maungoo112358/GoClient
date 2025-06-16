using Gamepacket;
using Google.Protobuf;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class GoClient : MonoBehaviour
{
	private UdpClient _client;
	private IPEndPoint _remoteEndPoint;

	private void Start()
	{
		_client = new UdpClient();
		_remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 9999);

		var handshake = new GamePacket
		{
			HandshakeRequest = new HandshakeRequest { ClientName = "Slint" }
		};
		byte[] data = handshake.ToByteArray();
		_client.Send(data, data.Length, _remoteEndPoint);
	}

	private void SendMessage(string message, uint seq, string clientId)
	{
		var packet = new GamePacket()
		{
			Seq = seq,
			ChatMessage = new ChatMessage
			{
				ClientId = clientId,
				Message = message,
			}
		};

		byte[] data = MessageExtensions.ToByteArray(packet);
		_client.Send(data, data.Length, _remoteEndPoint);
		Debug.Log($"Send packet => SeqID: {seq}, ClientID: {clientId}, Message: {message}");
	}

}