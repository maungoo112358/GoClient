using Gamepacket;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class NetworkClient : MonoBehaviour
{
	public enum ConnectionState
	{
		Disconnected,
		Connecting,
		Connected,
		Reconnecting
	}

	[HideInInspector]public string serverIP = "127.0.0.1";

	[HideInInspector] public int serverPort = 9999;
	[HideInInspector] public float handshakeTimeout = 3f;
	[HideInInspector] public float heartbeatInterval = 5f;
	[HideInInspector] public float connectionTimeout = 15f; // Consider disconnected if no ack for this long

	[HideInInspector] public float[] reconnectDelays = { 5f, 10f, 15f, 20f, 25f, 30f }; // Progressive delays

	[HideInInspector] public bool enableAutoReconnect = true;

	private int _currentReconnectIndex = 0;
	private float _nextReconnectTime = -1f;

	private UdpClient _client;
	private IPEndPoint _remoteEndPoint;

	private uint _seq = 1;
	private string _myPrivateId;
	private string _myPublicId;

	private float _handshakeSentTime = -1f;
	private float _lastHeartbeatAckTime = -1f;

	private bool _connectionAttemptInProgress = false;

	private ConnectionState _connectionState = ConnectionState.Disconnected;

	private ConcurrentQueue<byte[]> _receivedPackets = new ConcurrentQueue<byte[]>();

	// Events for other scripts to subscribe to
	public System.Action<string, string> OnConnected; // privateId, publicId

	public System.Action OnDisconnected;
	public System.Action<string> OnServerMessage;

	private void Start()
	{
		InitializeClient();
	}

	private void InitializeClient()
	{
		try
		{
			_client = new UdpClient();
			_client.Client.ReceiveTimeout = 3000;

			// Parse IP address
			if (!IPAddress.TryParse(serverIP, out IPAddress ipAddress))
			{
				ipAddress = IPAddress.Loopback;
				Debug.LogWarning($"Invalid IP {serverIP}, using loopback");
			}

			_remoteEndPoint = new IPEndPoint(ipAddress, serverPort);
			_client.BeginReceive(OnReceive, null);

			Debug.Log($"UDP client initialized, targeting {_remoteEndPoint}");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to initialize client: {ex.Message}");
		}
	}

	private void Update()
	{
		HandleConnectionLogic();
		ProcessReceivedPackets();
	}

	private void HandleConnectionLogic()
	{
		switch (_connectionState)
		{
			case ConnectionState.Disconnected:
				if (enableAutoReconnect && CanAttemptReconnect() && !_connectionAttemptInProgress)
				{
					TryToConnect();
				}
				break;

			case ConnectionState.Reconnecting:
				if (CanAttemptReconnect() && !_connectionAttemptInProgress)
				{
					TryToConnect();
				}
				else if (_handshakeSentTime >= 0f && Time.time - _handshakeSentTime > handshakeTimeout)
				{
					_connectionAttemptInProgress = false; // Reset flag on timeout
					ScheduleNextReconnect();
				}
				break;

			case ConnectionState.Connecting:
				if (_handshakeSentTime >= 0f && Time.time - _handshakeSentTime > handshakeTimeout)
				{
					_connectionAttemptInProgress = false; // Reset flag on timeout
					_connectionState = ConnectionState.Reconnecting;
					ScheduleNextReconnect();
				}
				break;

			case ConnectionState.Connected:
				// Check for connection timeout
				if (_lastHeartbeatAckTime > 0 && Time.time - _lastHeartbeatAckTime > connectionTimeout)
				{
					Debug.LogWarning("Connection timed out - no heartbeat ack received");
					HandleDisconnection();
				}
				break;
		}
	}

	private bool CanAttemptReconnect()
	{
		// For initial connection
		if (_connectionState == ConnectionState.Disconnected)
		{
			return _handshakeSentTime < 0f || Time.time - _handshakeSentTime > handshakeTimeout;
		}

		// For reconnection attempts
		return _nextReconnectTime > 0f && Time.time >= _nextReconnectTime;
	}

	private void ScheduleNextReconnect()
	{
		if (reconnectDelays.Length == 0) return;

		float delay = reconnectDelays[_currentReconnectIndex];
		_nextReconnectTime = Time.time + delay;
		_handshakeSentTime = -1f; // Reset handshake timer

		// Move to next delay, loop back to start if at end
		_currentReconnectIndex = (_currentReconnectIndex + 1) % reconnectDelays.Length;

		Debug.Log($"⏰ Next reconnection attempt in {delay}s (attempt pattern: {string.Join("s, ", reconnectDelays)}s)");
	}

	private void ProcessReceivedPackets()
	{
		while (_receivedPackets.TryDequeue(out var data))
		{
			try
			{
				var response = GamePacket.Parser.ParseFrom(data);
				HandleServerPacket(response);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to parse packet: {ex.Message}");
			}
		}
	}

	private void HandleServerPacket(GamePacket packet)
	{
		OnPacketReceived?.Invoke(packet);
		if (packet.ServerStatus != null)
		{
			Debug.LogWarning($"Server message: {packet.ServerStatus.Message}");
			OnServerMessage?.Invoke(packet.ServerStatus.Message);

			if (packet.ServerStatus.Message.Contains("shutting down"))
			{
				HandleDisconnection();
			}
		}

		if (packet.HandshakeResponse != null)
		{
			_myPrivateId = packet.HandshakeResponse.PrivateId;
			_myPublicId = packet.HandshakeResponse.PublicId;
			_connectionState = ConnectionState.Connected;
			_handshakeSentTime = -1f;
			_lastHeartbeatAckTime = Time.time;

			// Reset reconnection state on successful connection
			_currentReconnectIndex = 0;
			_nextReconnectTime = -1f;

			// Start heartbeat
			CancelInvoke(nameof(SendHeartBeat));
			InvokeRepeating(nameof(SendHeartBeat), heartbeatInterval, heartbeatInterval);

			Debug.Log($"Connected! Private: {_myPrivateId}, Public: {_myPublicId}");
			OnConnected?.Invoke(_myPrivateId, _myPublicId);
		}

		if (packet.HeartbeatAck != null)
		{
			_lastHeartbeatAckTime = Time.time;
			Debug.Log($"Heartbeat acknowledged: {packet.HeartbeatAck.ClientId}");
		}

		if (packet.ChatMessage != null)
		{
			Debug.Log($"Chat from {packet.ChatMessage.ClientId}: {packet.ChatMessage.Message}");
		}

		if (packet.LobbyJoinBroadcast != null)
		{
			Debug.Log($"Player {packet.LobbyJoinBroadcast.PublicId} joined with color {packet.LobbyJoinBroadcast.Colorhex}");
		}
	}

	private void TryToConnect()
	{
		try
		{
			_connectionAttemptInProgress = true;
			_connectionState = (_connectionState == ConnectionState.Disconnected) ?
				ConnectionState.Connecting : ConnectionState.Reconnecting;

			Debug.Log($"Attempting to connect to {_remoteEndPoint}...");

			// Set handshake time BEFORE sending to ensure timeout logic works
			_handshakeSentTime = Time.time;

			var handshake = new GamePacket
			{
				Seq = ++_seq,
				HandshakeRequest = new HandshakeRequest { ClientName = "Unity Client" }
			};

			SendPacket(handshake);
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"Connection attempt failed: {ex.Message}");
			// _handshakeSentTime is already set, so timeout logic will work
		}
	}

	private void HandleDisconnection()
	{
		bool wasConnected = _connectionState == ConnectionState.Connected;

		_connectionState = enableAutoReconnect ? ConnectionState.Reconnecting : ConnectionState.Disconnected;
		_myPrivateId = null;
		_myPublicId = null;
		_lastHeartbeatAckTime = -1f;

		CancelInvoke(nameof(SendHeartBeat));

		_connectionAttemptInProgress = false;
		_handshakeSentTime = -1f;

		if (wasConnected)
		{
			// Reset reconnect pattern when coming from a successful connection
			_currentReconnectIndex = 0;
			if (enableAutoReconnect)
			{
				ScheduleNextReconnect();
			}
		}

		Debug.Log($"Disconnected from server. Auto-reconnect: {enableAutoReconnect}");
		OnDisconnected?.Invoke();
	}

	private void OnReceive(IAsyncResult ar)
	{
		try
		{
			IPEndPoint from = null;
			byte[] data = _client.EndReceive(ar, ref from);

			// Verify packet is from our server
			if (from.Equals(_remoteEndPoint))
			{
				_receivedPackets.Enqueue(data);
			}
		}
		catch (ObjectDisposedException)
		{
			// Client was disposed, normal during shutdown
			return;
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"OnReceive failed: {ex.Message}");
		}
		finally
		{
			// Continue receiving if client is still active
			try
			{
				_client?.BeginReceive(OnReceive, null);
			}
			catch (ObjectDisposedException)
			{
				// Client disposed during shutdown
			}
		}
	}

	private void SendHeartBeat()
	{
		if (string.IsNullOrWhiteSpace(_myPrivateId) || _connectionState != ConnectionState.Connected)
			return;

		var packet = new GamePacket
		{
			Seq = ++_seq,
			Heartbeat = new Heartbeat { ClientId = _myPrivateId }
		};

		SendPacket(packet);
		Debug.Log($"❤️ Heartbeat sent: {_myPrivateId}");
	}

	// Public methods for sending different packet types
	public void SendChatMessage(string message)
	{
		if (!IsConnected()) return;

		var packet = new GamePacket
		{
			Seq = ++_seq,
			ChatMessage = new ChatMessage
			{
				ClientId = _myPublicId, // Use public ID for chat
				Message = message
			}
		};

		SendPacket(packet);
	}

	public void SendLobbyJoin(string colorHex)
	{
		if (!IsConnected()) return;

		var packet = new GamePacket
		{
			Seq = ++_seq,
			LobbyJoinBroadcast = new LobbyJoinBroadcast
			{
				PublicId = _myPublicId,
				Colorhex = colorHex
			}
		};

		SendPacket(packet);
	}

	public void SendPosition(Vector3 position)
	{
		if (!IsConnected()) return;

		var packet = new GamePacket
		{
			Seq = ++_seq,
			ClientPosition = new ClientPosition
			{
				ClientId = _myPublicId,
				X = position.x,
				Y = position.y,
				Z = position.z
			}
		};

		SendPacket(packet);
	}

	public void SendPacket(GamePacket packet)
	{
		try
		{
			byte[] data = packet.ToByteArray();
			_client.Send(data, data.Length, _remoteEndPoint);
		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to send packet: {ex.Message}");
			HandleDisconnection();
		}
	}

	// Public methods for connection control
	public void ManualConnect()
	{
		if (_connectionState == ConnectionState.Disconnected)
		{
			_currentReconnectIndex = 0;
			_nextReconnectTime = -1f;
			TryToConnect();
		}
	}

	public void ManualDisconnect()
	{
		enableAutoReconnect = false;
		HandleDisconnection();
	}

	public void SetAutoReconnect(bool enabled)
	{
		enableAutoReconnect = enabled;
		if (enabled && _connectionState == ConnectionState.Disconnected)
		{
			_currentReconnectIndex = 0;
			ScheduleNextReconnect();
		}
	}

	public float GetNextReconnectTime()
	{
		return _nextReconnectTime > 0f ? Mathf.Max(0f, _nextReconnectTime - Time.time) : 0f;
	}

	// Public getters
	public bool IsConnected() => _connectionState == ConnectionState.Connected;

	public string GetPrivateId() => _myPrivateId;

	public string GetPublicId() => _myPublicId;

	public ConnectionState GetConnectionState() => _connectionState;

	public event Action<GamePacket> OnPacketReceived;

	private void OnDestroy()
	{
		enableAutoReconnect = false; // Stop any pending reconnections
		CancelInvoke();
		_client?.Close();
		_client?.Dispose();
	}

	private void OnApplicationPause(bool pauseStatus)
	{
		if (pauseStatus)
		{
			// Pause heartbeats when app is paused
			CancelInvoke(nameof(SendHeartBeat));
		}
		else if (IsConnected())
		{
			// Resume heartbeats when app is unpaused
			InvokeRepeating(nameof(SendHeartBeat), 0f, heartbeatInterval);
		}
	}
}