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
		HandshakeComplete,
		WaitingForUsernamePrompt,
		UsernameValidating,
		Connected,
		Reconnecting,
		ReconnectingWithSession
	}

	[Header("Connection Settings")]
	[HideInInspector] public string serverIP = "127.0.0.1";
	[HideInInspector] public int serverPort = 9999;
	[HideInInspector] public float handshakeTimeout = 3f;
	[HideInInspector] public float heartbeatInterval = 5f;
	[HideInInspector] public float connectionTimeout = 15f;

	[Header("Reconnection Settings")]
	[HideInInspector] public float[] reconnectDelays = { 5f, 10f, 15f, 20f, 25f, 30f };
	[HideInInspector] public bool enableAutoReconnect = true;
	[HideInInspector] public bool enableSessionReconnect = true;
	[HideInInspector] public float sessionTimeout = 30f;

	// Network state
	private int _currentReconnectIndex = 0;
	private float _nextReconnectTime = -1f;
	private UdpClient _client;
	private IPEndPoint _remoteEndPoint;
	private uint _seq = 1;

	// Connection state
	private string _myPrivateId;
	private string _myPublicId;
	private float _handshakeSentTime = -1f;
	private float _usernameSentTime = -1f;
	private float _reconnectionSentTime = -1f;
	private float _lastHeartbeatAckTime = -1f;
	private bool _connectionAttemptInProgress = false;
	private ConnectionState _connectionState = ConnectionState.Disconnected;

	// Session management
	private string _storedUsername;
	private string _storedSessionToken;
	private bool _hasValidSession = false;
	private bool _isAttemptingSessionReconnect = false;

	// Packet handling
	private ConcurrentQueue<byte[]> _receivedPackets = new ConcurrentQueue<byte[]>();

	// Events
	public System.Action<string, string> OnConnected;
	public System.Action OnDisconnected;
	public System.Action<string> OnServerMessage;
	public System.Action<string> OnUsernamePromptReceived; // New event for username prompt
	public event Action<GamePacket> OnPacketReceived;

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

			if (!IPAddress.TryParse(serverIP, out IPAddress ipAddress))
			{
				ipAddress = IPAddress.Loopback;
				Debug.LogWarning($"Invalid IP {serverIP}, using loopback");
			}

			_remoteEndPoint = new IPEndPoint(ipAddress, serverPort);
			_client.BeginReceive(OnReceive, null);

			Debug.Log($"NetworkClient: UDP client initialized, targeting {_remoteEndPoint}");
		}
		catch (Exception ex)
		{
			Debug.LogError($"NetworkClient: Failed to initialize client: {ex.Message}");
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
			case ConnectionState.ReconnectingWithSession:
				if (CanAttemptReconnect() && !_connectionAttemptInProgress)
				{
					TryToConnect();
				}
				else if (ShouldTimeoutCurrentAttempt())
				{
					_connectionAttemptInProgress = false;
					ScheduleNextReconnect();
				}
				break;

			case ConnectionState.Connecting:
				if (_handshakeSentTime >= 0f && Time.time - _handshakeSentTime > handshakeTimeout)
				{
					_connectionAttemptInProgress = false;
					_connectionState = enableAutoReconnect ? ConnectionState.Reconnecting : ConnectionState.Disconnected;
					ScheduleNextReconnect();
				}
				break;

			case ConnectionState.WaitingForUsernamePrompt:
				// Wait for server to send username prompt (no timeout needed)
				break;

			case ConnectionState.UsernameValidating:
				if (_usernameSentTime >= 0f && Time.time - _usernameSentTime > handshakeTimeout)
				{
					Debug.LogWarning("NetworkClient: Username validation timed out");
					_connectionState = enableAutoReconnect ? ConnectionState.Reconnecting : ConnectionState.Disconnected;
					_connectionAttemptInProgress = false;
					ScheduleNextReconnect();
				}
				break;

			case ConnectionState.Connected:
				if (_lastHeartbeatAckTime > 0 && Time.time - _lastHeartbeatAckTime > connectionTimeout)
				{
					Debug.LogWarning("NetworkClient: Connection timed out - no heartbeat ack received");
					HandleDisconnection();
				}
				break;
		}
	}

	private bool ShouldTimeoutCurrentAttempt()
	{
		if (_connectionState == ConnectionState.ReconnectingWithSession)
		{
			return _reconnectionSentTime >= 0f && Time.time - _reconnectionSentTime > sessionTimeout;
		}
		return _handshakeSentTime >= 0f && Time.time - _handshakeSentTime > handshakeTimeout;
	}

	private bool CanAttemptReconnect()
	{
		if (_connectionState == ConnectionState.Disconnected)
		{
			return _handshakeSentTime < 0f || Time.time - _handshakeSentTime > handshakeTimeout;
		}
		return _nextReconnectTime > 0f && Time.time >= _nextReconnectTime;
	}

	private void ScheduleNextReconnect()
	{
		if (reconnectDelays.Length == 0) return;

		float delay = reconnectDelays[_currentReconnectIndex];
		_nextReconnectTime = Time.time + delay;
		_handshakeSentTime = -1f;
		_usernameSentTime = -1f;
		_reconnectionSentTime = -1f;

		_currentReconnectIndex = (_currentReconnectIndex + 1) % reconnectDelays.Length;

		Debug.Log($"NetworkClient: ⏰ Next reconnection attempt in {delay}s");
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
				Debug.LogWarning($"NetworkClient: Failed to parse packet: {ex.Message}");
			}
		}
	}

	private void HandleServerPacket(GamePacket packet)
	{
		OnPacketReceived?.Invoke(packet);

		if (packet.ServerStatus != null)
		{
			Debug.LogWarning($"NetworkClient: Server message: {packet.ServerStatus.Message}");
			OnServerMessage?.Invoke(packet.ServerStatus.Message);

			if (packet.ServerStatus.Message.Contains("shutting down"))
			{
				HandleDisconnection();
			}
		}

		// Handle handshake responses
		if (packet.HandshakeResponse != null)
		{
			HandleHandshakeResponse(packet.HandshakeResponse);
		}

		// Handle username prompts from server
		if (packet.UsernamePrompt != null)
		{
			HandleUsernamePrompt(packet.UsernamePrompt);
		}

		// Handle reconnection responses
		if (packet.ReconnectionResponse != null)
		{
			HandleReconnectionResponse(packet.ReconnectionResponse);
		}

		// Handle heartbeat acknowledgments
		if (packet.HeartbeatAck != null)
		{
			_lastHeartbeatAckTime = Time.time;
			Debug.Log($"NetworkClient: ❤️ Heartbeat acknowledged: {packet.HeartbeatAck.ClientId}");
		}

		if (packet.LobbyJoinBroadcast != null)
		{
			Debug.Log($"NetworkClient: Player {packet.LobbyJoinBroadcast.PublicId} joined lobby");
		}
	}

	private void HandleHandshakeResponse(HandshakeResponse response)
	{
		if (response.PrivateId == "pending" && response.PublicId == "pending")
		{
			// Handshake acknowledgment - wait for server to request username
			_connectionState = ConnectionState.WaitingForUsernamePrompt;
			_handshakeSentTime = -1f;

			Debug.Log("NetworkClient: Handshake acknowledged, waiting for username prompt from server...");
		}
		else
		{
			// Final handshake with real IDs (after successful username validation)
			CompleteConnection(response.PrivateId, response.PublicId);
		}
	}

	private void HandleUsernamePrompt(UsernamePrompt prompt)
	{
		Debug.Log($"NetworkClient: Username prompt received: {prompt.Message}");
		_connectionState = ConnectionState.HandshakeComplete;

		// Notify UI that server is requesting username
		OnUsernamePromptReceived?.Invoke(prompt.Message);
	}

	private void HandleReconnectionResponse(ReconnectionResponse response)
	{
		_reconnectionSentTime = -1f;
		_isAttemptingSessionReconnect = false;

		if (response.IsSuccessful)
		{
			Debug.Log("NetworkClient: 🔄 Session reconnection successful!");
			CompleteConnection(response.PrivateId, response.PublicId);
		}
		else
		{
			Debug.Log($"NetworkClient: Session reconnection failed: {response.Message}");
			// Clear invalid session and fall back to normal connection
			ClearStoredSession();
			_connectionState = ConnectionState.Reconnecting;
			ScheduleNextReconnect();
		}
	}

	private void CompleteConnection(string privateId, string publicId)
	{
		_myPrivateId = privateId;
		_myPublicId = publicId;
		_connectionState = ConnectionState.Connected;
		_handshakeSentTime = -1f;
		_usernameSentTime = -1f;
		_reconnectionSentTime = -1f;
		_lastHeartbeatAckTime = Time.time;

		// Store session for future reconnections
		if (enableSessionReconnect)
		{
			StoreSession(publicId, privateId);
		}

		// Reset reconnection state
		_currentReconnectIndex = 0;
		_nextReconnectTime = -1f;
		_connectionAttemptInProgress = false;
		_isAttemptingSessionReconnect = false;

		// Start heartbeat
		CancelInvoke(nameof(SendHeartBeat));
		InvokeRepeating(nameof(SendHeartBeat), heartbeatInterval, heartbeatInterval);

		Debug.Log($"NetworkClient: ✅ Connected! Private: {privateId}, Public: {publicId}");
		OnConnected?.Invoke(privateId, publicId);
	}

	private void TryToConnect()
	{
		try
		{
			_connectionAttemptInProgress = true;

			// Determine connection type
			bool shouldTrySessionReconnect = enableSessionReconnect && _hasValidSession &&
				(_connectionState == ConnectionState.Disconnected || _connectionState == ConnectionState.ReconnectingWithSession);

			if (shouldTrySessionReconnect && !_isAttemptingSessionReconnect)
			{
				// Try session reconnection first
				_connectionState = ConnectionState.ReconnectingWithSession;
				_isAttemptingSessionReconnect = true;
				_reconnectionSentTime = Time.time;

				Debug.Log($"NetworkClient: 🔄 Attempting session reconnection for {_storedUsername}");
				SendReconnectionRequest(_storedUsername, _storedSessionToken);
			}
			else
			{
				// Normal connection flow
				_connectionState = (_connectionState == ConnectionState.Disconnected) ?
					ConnectionState.Connecting : ConnectionState.Reconnecting;
				_handshakeSentTime = Time.time;

				Debug.Log($"NetworkClient: 🔌 Attempting normal connection to {_remoteEndPoint}");
				var handshake = new GamePacket
				{
					Seq = ++_seq,
					HandshakeRequest = new HandshakeRequest { ClientName = "Unity Client" }
				};
				SendPacket(handshake);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"NetworkClient: Connection attempt failed: {ex.Message}");
			_connectionAttemptInProgress = false;
		}
	}

	private void HandleDisconnection()
	{
		bool wasConnected = _connectionState == ConnectionState.Connected;

		_connectionState = enableAutoReconnect ?
			(enableSessionReconnect && _hasValidSession ? ConnectionState.ReconnectingWithSession : ConnectionState.Reconnecting) :
			ConnectionState.Disconnected;

		_myPrivateId = null;
		_myPublicId = null;
		_lastHeartbeatAckTime = -1f;

		CancelInvoke(nameof(SendHeartBeat));

		_connectionAttemptInProgress = false;
		_handshakeSentTime = -1f;
		_usernameSentTime = -1f;
		_reconnectionSentTime = -1f;
		_isAttemptingSessionReconnect = false;

		if (wasConnected)
		{
			_currentReconnectIndex = 0;
			if (enableAutoReconnect)
			{
				ScheduleNextReconnect();
			}
		}

		Debug.Log($"NetworkClient: 🔌 Disconnected. Auto-reconnect: {enableAutoReconnect}, Session available: {_hasValidSession}");
		OnDisconnected?.Invoke();
	}

	#region Session Management

	private void StoreSession(string username, string privateId)
	{
		_storedUsername = username;
		_storedSessionToken = GenerateSessionToken(privateId);
		_hasValidSession = true;

		Debug.Log($"NetworkClient: 💾 Session stored for {username}");
	}

	private void ClearStoredSession()
	{
		_storedUsername = null;
		_storedSessionToken = null;
		_hasValidSession = false;
		_isAttemptingSessionReconnect = false;

		Debug.Log("NetworkClient: 🗑️ Session cleared");
	}

	private string GenerateSessionToken(string privateId)
	{
		if (string.IsNullOrEmpty(privateId))
		{
			return System.Guid.NewGuid().ToString();
		}

		long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		string tokenData = $"{privateId}_{timestamp}";
		int hash = tokenData.GetHashCode();
		return $"session_{hash:X8}";
	}

	public bool HasValidSession() => _hasValidSession;
	public string GetStoredUsername() => _storedUsername;

	#endregion

	#region Network Communication

	private void OnReceive(IAsyncResult ar)
	{
		try
		{
			IPEndPoint from = null;
			byte[] data = _client.EndReceive(ar, ref from);

			if (from.Equals(_remoteEndPoint))
			{
				_receivedPackets.Enqueue(data);
			}
		}
		catch (ObjectDisposedException)
		{
			return;
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"NetworkClient: OnReceive failed: {ex.Message}");
		}
		finally
		{
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
		Debug.Log($"NetworkClient: ❤️ Heartbeat sent: {_myPrivateId}");
	}

	public void SendUsernameSubmission(string username)
	{
		if (_connectionState != ConnectionState.HandshakeComplete)
		{
			Debug.LogWarning("NetworkClient: Cannot send username - not ready for submission");
			return;
		}

		_connectionState = ConnectionState.UsernameValidating;
		_usernameSentTime = Time.time;

		var packet = new GamePacket
		{
			Seq = ++_seq,
			UsernameSubmission = new UsernameSubmission { Username = username }
		};

		SendPacket(packet);
		Debug.Log($"NetworkClient: 📝 Username submission sent: {username}");
	}

	public void SendReconnectionRequest(string username, string sessionToken)
	{
		var packet = new GamePacket
		{
			Seq = ++_seq,
			ReconnectionRequest = new ReconnectionRequest
			{
				Username = username,
				SessionToken = sessionToken
			}
		};

		SendPacket(packet);
		Debug.Log($"NetworkClient: 🔄 Reconnection request sent for: {username}");
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

	public void SendPosition(Vector3 position, Vector3 velocity)
	{
		if (!IsConnected()) return;

		var packet = new GamePacket
		{
			Seq = ++_seq,
			ClientPosition = new ClientPosition
			{
				ClientId = _myPublicId,
				Position = new Position { X = position.x, Y = position.y, Z = position.z },
				Velocity = new Velocity { X = velocity.x, Y = velocity.y, Z = velocity.z },
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
			Debug.LogError($"NetworkClient: Failed to send packet: {ex.Message}");
			HandleDisconnection();
		}
	}

	#endregion

	#region Public API

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
		ClearStoredSession();
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

	public void ClearSession()
	{
		ClearStoredSession();
	}

	public bool IsConnected() => _connectionState == ConnectionState.Connected;
	public string GetPrivateId() => _myPrivateId;
	public string GetPublicId() => _myPublicId;
	public ConnectionState GetConnectionState() => _connectionState;
	public float GetNextReconnectTime() => _nextReconnectTime > 0f ? Mathf.Max(0f, _nextReconnectTime - Time.time) : 0f;

	#endregion

	private void OnDestroy()
	{
		enableAutoReconnect = false;
		CancelInvoke();
		_client?.Close();
		_client?.Dispose();
	}

	private void OnApplicationPause(bool pauseStatus)
	{
		if (pauseStatus)
		{
			CancelInvoke(nameof(SendHeartBeat));
		}
		else if (IsConnected())
		{
			InvokeRepeating(nameof(SendHeartBeat), 0f, heartbeatInterval);
		}
	}
}