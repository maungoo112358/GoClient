using Gamepacket;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class GoClient : MonoBehaviour
{
	private UdpClient _client;
	private IPEndPoint _remoteEndPoint;

	private uint _seq = 1;
	private string _myPrivateId;

	private float _nextRetryTime = 0f;
	private float _retryDelay = 5f;

	private bool _isConnected = false;

	private ConcurrentQueue<byte[]> _receivedPackets = new ConcurrentQueue<byte[]>();

	private void Start()
	{
		_client = new UdpClient();
		_client.Client.ReceiveTimeout = 3000;
		_remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 9999);
		_client.BeginReceive(OnReceive, null);
	}

	private void Update()
	{
		if (!_isConnected && Time.time >= _nextRetryTime)
		{
			TryToConnect();
			_nextRetryTime = Time.time + _retryDelay;
			_retryDelay = Mathf.Min(_retryDelay * 2f, 30f);
		}

		while (_receivedPackets.TryDequeue(out var data))
		{
			try
			{
				var response = GamePacket.Parser.ParseFrom(data);

				if (response.HandshakeResponse != null)
				{
					_myPrivateId = response.HandshakeResponse.PrivateId;
					_isConnected = true;
					_retryDelay = 5f;
					SendHeartBeat();
					InvokeRepeating(nameof(SendHeartBeat), 5f, 5f);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("Failed to parse packet: " + ex.Message);
			}
		}
	}

	private void TryToConnect()
	{
		try
		{
			var handshake = new GamePacket
			{
				HandshakeRequest = new HandshakeRequest { ClientName = "Slint" }
			};
			byte[] data = handshake.ToByteArray();
			_client.Send(data, data.Length, _remoteEndPoint);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Server not reachable: " + ex.Message);
		}
	}

	private void OnReceive(IAsyncResult ar)
	{
		try
		{
			IPEndPoint from = null;
			byte[] data = _client.EndReceive(ar, ref from);
			_receivedPackets.Enqueue(data);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("OnReceive failed: " + ex.Message);
		}
		finally
		{
			_client.BeginReceive(OnReceive, null);
		}
	}

	private void SendHeartBeat()
	{
		if (string.IsNullOrWhiteSpace(_myPrivateId)) return;

		var packet = new GamePacket
		{
			Seq = ++_seq,
			Heartbeat = new Heartbeat
			{
				ClientId = _myPrivateId
			}
		};

		byte[] data = packet.ToByteArray();
		_client.Send(data, data.Length, _remoteEndPoint);
		Debug.Log($"SendHeartBeat::<<❤️>>:: {_myPrivateId}");
	}
}
