using Gamepacket;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ModularNetworkManager : MonoBehaviour
{
	[Header("Module Management")]
	public bool enableModuleSystem = true;

	public bool autoInitializeModules = true;

	[Header("Connection Settings - Same as NetworkClient")]
	public string serverIP = "127.0.0.1";

	public int serverPort = 9999;
	public float handshakeTimeout = 3f;
	public float heartbeatInterval = 5f;
	public float connectionTimeout = 15f;

	[Header("Reconnection Settings - Same as NetworkClient")]
	public float[] reconnectDelays = { 5f, 10f, 15f, 20f, 25f, 30f };

	public bool enableAutoReconnect = true;

	private NetworkClient _networkClient;
	private ModuleRegistry _moduleRegistry;
	private bool _modulesInitialized = false;

	public Action<string, string> OnConnected;
	public Action OnDisconnected;
	public Action<string> OnServerMessage;
	public Action<string> OnUsernamePromptReceived;

	private string desiredUsername;
	private string sessionToken;

	private Dictionary<PacketType, Action<GamePacket>> _packetHandlers;

	private void Awake()
	{
		InitializePacketHandlers();
		InitializeModuleSystem();
		InitializeNetworkClient();
	}

	private void Start()
	{
		if (enableModuleSystem && autoInitializeModules)
		{
			InitializeModules();
		}
	}

	private void InitializePacketHandlers()
	{
		_packetHandlers = new Dictionary<PacketType, Action<GamePacket>>
		{
			{ PacketType.ReconnectionResponse, HandleReconnectionResponse },
			{ PacketType.ServerStatus, HandleServerStatus },
			{ PacketType.HeartbeatAck, HandleHeartbeatAck },
			{ PacketType.UsernamePrompt, HandleUsernamePrompt },
			{ PacketType.UsernameResponse, HandleUsernameResponse },
			{ PacketType.LobbyJoinBroadcast, HandleLobbyJoinBroadcast },
			{ PacketType.ClientPosition, HandleClientPosition }
		};
	}

	private void InitializeModuleSystem()
	{
		if (!enableModuleSystem) return;

		_moduleRegistry = new ModuleRegistry();
	}

	private void InitializeNetworkClient()
	{
		var networkClientObj = new GameObject("NetworkClient");
		networkClientObj.transform.SetParent(transform);

		_networkClient = networkClientObj.AddComponent<NetworkClient>();

		_networkClient.serverIP = serverIP;
		_networkClient.serverPort = serverPort;
		_networkClient.handshakeTimeout = handshakeTimeout;
		_networkClient.heartbeatInterval = heartbeatInterval;
		_networkClient.connectionTimeout = connectionTimeout;
		_networkClient.reconnectDelays = reconnectDelays;
		_networkClient.enableAutoReconnect = enableAutoReconnect;

		_networkClient.OnConnected += HandleNetworkClientConnected;
		_networkClient.OnDisconnected += HandleNetworkClientDisconnected;
		_networkClient.OnServerMessage += HandleNetworkClientServerMessage;
		_networkClient.OnUsernamePromptReceived += HandleNetworkClientUsernamePrompt;
		_networkClient.OnPacketReceived += HandleNetworkClientPacketReceived;
	}

	public void AttemptReconnection(string username, string sessionToken)
	{
		this.sessionToken = sessionToken;
		_networkClient?.SendReconnectionRequest(username, sessionToken);
	}

	#region NetworkClient Event Bridging

	private void HandleNetworkClientConnected(string privateId, string publicId)
	{
		Debug.Log($"ModularNetworkManager: Connection established - Private: {privateId}, Public: {publicId}");

		OnConnected?.Invoke(privateId, publicId);

		if (enableModuleSystem)
		{
			ModularEventSystem.Instance.Publish(new PlayerConnectedEvent(privateId, publicId));
		}

		if (enableModuleSystem && _modulesInitialized)
		{
			_moduleRegistry.EnableAllModules();
		}
	}

	private void HandleNetworkClientDisconnected()
	{
		Debug.Log("ModularNetworkManager: Disconnected from server");

		OnDisconnected?.Invoke();

		if (enableModuleSystem)
		{
			ModularEventSystem.Instance.Publish(new PlayerDisconnectedEvent("", "Connection lost"));

			if (_modulesInitialized)
			{
				_moduleRegistry.DisableAllModules();
			}
		}
	}

	private void HandleReconnectionResponse(GamePacket packet)
	{
		var response = packet.ReconnectionResponse;
		Debug.Log($"ModularNetworkManager: Reconnection response - Success: {response.IsSuccessful}, Message: {response.Message}");
		ModularEventSystem.Instance.Publish(new ReconnectionResponseEvent(response.IsSuccessful, response.Message, response.PrivateId, response.PublicId));
	}

	private void HandleNetworkClientServerMessage(string message)
	{
		Debug.Log($"ModularNetworkManager: Server message - {message}");

		OnServerMessage?.Invoke(message);

		if (enableModuleSystem)
		{
			ModularEventSystem.Instance.Publish(new ServerMessageEvent(message));
		}
	}

	private void HandleNetworkClientUsernamePrompt(string promptMessage)
	{
		Debug.Log($"ModularNetworkManager: Username prompt received - {promptMessage}");

		OnUsernamePromptReceived?.Invoke(promptMessage);

		if (enableModuleSystem)
		{
			ModularEventSystem.Instance.Publish(new UsernamePromptEvent(promptMessage));
		}
	}

	private void HandleNetworkClientPacketReceived(GamePacket packet)
	{
		if (!enableModuleSystem) return;

		var packetType = GetPacketType(packet);
		if (packetType.HasValue && _packetHandlers.TryGetValue(packetType.Value, out var handler))
		{
			handler(packet);
		}
	}

	private void HandleHeartbeatAck(GamePacket packet)
	{
		ModularEventSystem.Instance.Publish(new HeartbeatAckReceivedEvent(packet.HeartbeatAck.ClientId));
	}

	private void HandleUsernameResponse(GamePacket packet)
	{
		var response = packet.UsernameResponse;
		Debug.Log($"ModularNetworkManager: Username response - {response.Username}, Accepted: {response.IsAccepted}, Message: {response.Message}");
		ModularEventSystem.Instance.Publish(new UsernameResponseEvent(response.Username, response.IsAccepted, response.Message));
	}

	private void HandleUsernamePrompt(GamePacket packet)
	{
		Debug.Log($"ModularNetworkManager: Username prompt packet - {packet.UsernamePrompt.Message}");
		// Already handled in HandleNetworkClientUsernamePrompt, but logging here for packet tracking
	}

	private void HandleLobbyJoinBroadcast(GamePacket packet)
	{
		var data = packet.LobbyJoinBroadcast;
		Debug.Log($"ModularNetworkManager: Lobby join broadcast - Player: {data.PublicId}, Color: {data.Colorhex}, IsLocal: {data.IsLocalPlayer}");
		ModularEventSystem.Instance.Publish(new PlayerJoinedLobbyEvent(data.PublicId, data.Colorhex, data.Position.PosToVector3(), data.IsLocalPlayer));
	}

	private void HandleClientPosition(GamePacket packet)
	{
		var pos = packet.ClientPosition;
		ModularEventSystem.Instance.Publish(new PlayerMovementEvent(pos.ClientId, pos.PosToVector3(), pos.VelocityToVector3(), pos.Timestamp));
	}

	private void HandleServerStatus(GamePacket packet)
	{
		if (packet.ServerStatus.Message.Contains("left the lobby"))
		{
			string playerWhoLeft = packet.ServerStatus.ClientId;
			if (!string.IsNullOrEmpty(playerWhoLeft))
			{
				Debug.Log($"ModularNetworkManager: Player {playerWhoLeft} left the lobby");
				ModularEventSystem.Instance.Publish(new PlayerDisconnectedEvent(playerWhoLeft, "Left lobby"));
			}
		}
	}

	private PacketType? GetPacketType(GamePacket packet)
	{
		if (packet.HeartbeatAck != null) return PacketType.HeartbeatAck;
		if (packet.UsernameResponse != null) return PacketType.UsernameResponse;
		if (packet.UsernamePrompt != null) return PacketType.UsernamePrompt;
		if (packet.ReconnectionResponse != null) return PacketType.ReconnectionResponse;
		if (packet.LobbyJoinBroadcast != null) return PacketType.LobbyJoinBroadcast;
		if (packet.ClientPosition != null) return PacketType.ClientPosition;
		if (packet.ServerStatus != null) return PacketType.ServerStatus;

		return null;
	}

	#endregion NetworkClient Event Bridging

	#region Module Management

	public void InitializeModules()
	{
		if (!enableModuleSystem || _modulesInitialized)
		{
			Debug.LogWarning("Module system disabled or already initialized");
			return;
		}

		_moduleRegistry.InitializeAllModules();
		_modulesInitialized = true;

		if (IsConnected())
		{
			_moduleRegistry.EnableAllModules();
		}
	}

	public bool RegisterModule(INetworkModule module)
	{
		if (!enableModuleSystem)
		{
			Debug.LogWarning("Module system is disabled");
			return false;
		}

		return _moduleRegistry.RegisterModule(module);
	}

	public bool EnableModule(string moduleId)
	{
		if (!enableModuleSystem) return false;
		return _moduleRegistry.EnableModule(moduleId);
	}

	public bool DisableModule(string moduleId)
	{
		if (!enableModuleSystem) return false;
		return _moduleRegistry.DisableModule(moduleId);
	}

	public T GetModule<T>(string moduleId) where T : class, INetworkModule
	{
		if (!enableModuleSystem) return null;
		return _moduleRegistry.GetModule(moduleId) as T;
	}

	public bool IsModuleSystemEnabled() => enableModuleSystem;

	public void SetDesiredUsername(string username) => desiredUsername = username;

	public string GetDesiredUsername() => desiredUsername;

	#endregion Module Management

	#region NetworkClient Passthrough Methods

	public void SendLobbyJoin(string colorHex) => _networkClient?.SendLobbyJoin(colorHex);

	public void SendPosition(Vector3 position, Vector3 velocity) => _networkClient?.SendPosition(position, velocity);

	public void SendUsernameSubmission(string username) => _networkClient?.SendUsernameSubmission(username);

	public void SendPacket(GamePacket packet) => _networkClient?.SendPacket(packet);

	public void ManualConnect() => _networkClient?.ManualConnect();

	public void ManualDisconnect() => _networkClient?.ManualDisconnect();

	public void SetAutoReconnect(bool enabled) => _networkClient?.SetAutoReconnect(enabled);

	public float GetNextReconnectTime() => _networkClient?.GetNextReconnectTime() ?? 0f;

	public bool IsConnected() => _networkClient?.IsConnected() ?? false;

	public string GetPrivateId() => _networkClient?.GetPrivateId();

	public string GetPublicId() => _networkClient?.GetPublicId();

	public ConnectionState? GetConnectionState() => _networkClient?.GetConnectionState();

	#endregion NetworkClient Passthrough Methods

	private void OnDestroy()
	{
		if (enableModuleSystem && _moduleRegistry != null)
		{
			_moduleRegistry.DestroyAllModules();
		}
	}

	private void OnValidate()
	{
		if (_networkClient != null)
		{
			_networkClient.serverIP = serverIP;
			_networkClient.serverPort = serverPort;
			_networkClient.handshakeTimeout = handshakeTimeout;
			_networkClient.heartbeatInterval = heartbeatInterval;
			_networkClient.connectionTimeout = connectionTimeout;
			_networkClient.reconnectDelays = reconnectDelays;
			_networkClient.enableAutoReconnect = enableAutoReconnect;
		}
	}
}