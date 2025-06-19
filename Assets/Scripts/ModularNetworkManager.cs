using Gamepacket;
using UnityEngine;

/// <summary>
/// Modular wrapper around NetworkClient
/// </summary>
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

	// Core NetworkClient instance - keeps all existing functionality
	private NetworkClient _networkClient;

	// Module management
	private ModuleRegistry _moduleRegistry;
	private bool _modulesInitialized = false;

	// Events - same as NetworkClient but also publish to event system
	public System.Action<string, string> OnConnected;
	public System.Action OnDisconnected;
	public System.Action<string> OnServerMessage;

	private void Awake()
	{
		InitializeModuleSystem();
		InitializeNetworkClient();
	}

	private void InitializeModuleSystem()
	{
		if (!enableModuleSystem) return;

		_moduleRegistry = new ModuleRegistry();
		//Debug.Log("Module system initialized");
	}

	private void InitializeNetworkClient()
	{
		// Create NetworkClient as a child component
		var networkClientObj = new GameObject("NetworkClient");
		networkClientObj.transform.SetParent(transform);

		_networkClient = networkClientObj.AddComponent<NetworkClient>();

		// Copy all settings to NetworkClient
		_networkClient.serverIP = serverIP;
		_networkClient.serverPort = serverPort;
		_networkClient.handshakeTimeout = handshakeTimeout;
		_networkClient.heartbeatInterval = heartbeatInterval;
		_networkClient.connectionTimeout = connectionTimeout;
		_networkClient.reconnectDelays = reconnectDelays;
		_networkClient.enableAutoReconnect = enableAutoReconnect;

		// Subscribe to NetworkClient events and bridge to event system
		_networkClient.OnConnected += HandleNetworkClientConnected;
		_networkClient.OnDisconnected += HandleNetworkClientDisconnected;
		_networkClient.OnServerMessage += HandleNetworkClientServerMessage;
		_networkClient.OnPacketReceived += HandleNetworkClientPacketReceived;

		//Debug.Log("NetworkClient initialized and bridged to event system");
	}

	private void Start()
	{
		if (enableModuleSystem && autoInitializeModules)
		{
			InitializeModules();
		}
	}

	#region NetworkClient Event Bridging

	private void HandleNetworkClientConnected(string privateId, string publicId)
	{
		//Debug.Log($"Connection established - bridging to event system: {publicId}");

		// Maintain original callback behavior
		OnConnected?.Invoke(privateId, publicId);

		// Publish to event system for modules
		if (enableModuleSystem)
		{
			GameEventSystem.Instance.Publish(new PlayerConnectedEvent(privateId, publicId));
		}

		// Enable modules after connection
		if (enableModuleSystem && _modulesInitialized)
		{
			_moduleRegistry.EnableAllModules();
		}
	}

	private void HandleNetworkClientDisconnected()
	{
		//Debug.Log("Disconnected - bridging to event system");

		// Maintain original callback behavior
		OnDisconnected?.Invoke();

		// Publish to event system for modules
		if (enableModuleSystem)
		{
			GameEventSystem.Instance.Publish(new PlayerDisconnectedEvent("Connection lost"));

			// Disable modules on disconnection
			if (_modulesInitialized)
			{
				_moduleRegistry.DisableAllModules();
			}
		}
	}

	private void HandleNetworkClientServerMessage(string message)
	{
		//Debug.Log($"Server message - bridging to event system: {message}");

		// Maintain original callback behavior
		OnServerMessage?.Invoke(message);

		// Publish to event system for modules
		if (enableModuleSystem)
		{
			GameEventSystem.Instance.Publish(new ServerMessageEvent(message));
		}
	}

	private void HandleNetworkClientPacketReceived(GamePacket packet)
	{
		if (!enableModuleSystem) return;

		// Bridge specific packet types to events
		if (packet.HeartbeatAck != null)
		{
			GameEventSystem.Instance.Publish(new HeartbeatAckReceivedEvent(packet.HeartbeatAck.ClientId));
		}

		if (packet.ChatMessage != null)
		{
			GameEventSystem.Instance.Publish(new ChatMessageReceivedEvent(packet.ChatMessage.ClientId, packet.ChatMessage.Message));
		}

		if (packet.LobbyJoinBroadcast != null)
		{
			GameEventSystem.Instance.Publish(new PlayerJoinedLobbyEvent(packet.LobbyJoinBroadcast.PublicId, packet.LobbyJoinBroadcast.ColorHex));
		}

		if (packet.ClientPosition != null)
		{
			GameEventSystem.Instance.Publish(new OtherPlayerMovedEvent(packet.ClientPosition.ClientId,
				new Vector3(packet.ClientPosition.X, packet.ClientPosition.Y, packet.ClientPosition.Z)));
		}
	}

	#endregion

	#region Module Management

	/// <summary>
	/// Initialize all registered modules
	/// </summary>
	public void InitializeModules()
	{
		if (!enableModuleSystem || _modulesInitialized)
		{
			Debug.LogWarning("Module system disabled or already initialized");
			return;
		}

		_moduleRegistry.InitializeAllModules();
		_modulesInitialized = true;

		// Enable modules if already connected
		if (IsConnected())
		{
			_moduleRegistry.EnableAllModules();
		}
	}

	/// <summary>
	/// Register a new module
	/// </summary>
	public bool RegisterModule(INetworkModule module)
	{
		if (!enableModuleSystem)
		{
			Debug.LogWarning("Module system is disabled");
			return false;
		}

		return _moduleRegistry.RegisterModule(module);
	}

	/// <summary>
	/// Enable a specific module
	/// </summary>
	public bool EnableModule(string moduleId)
	{
		if (!enableModuleSystem) return false;
		return _moduleRegistry.EnableModule(moduleId);
	}

	/// <summary>
	/// Disable a specific module
	/// </summary>
	public bool DisableModule(string moduleId)
	{
		if (!enableModuleSystem) return false;
		return _moduleRegistry.DisableModule(moduleId);
	}

	/// <summary>
	/// Get a specific module
	/// </summary>
	public T GetModule<T>(string moduleId) where T : class, INetworkModule
	{
		if (!enableModuleSystem) return null;
		return _moduleRegistry.GetModule(moduleId) as T;
	}

	/// <summary>
	/// Check if module system is enabled
	/// </summary>
	public bool IsModuleSystemEnabled()
	{
		return enableModuleSystem;
	}

	#endregion

	#region NetworkClient Passthrough Methods

	// All public methods from NetworkClient are available here
	public void SendChatMessage(string message) => _networkClient?.SendChatMessage(message);
	public void SendLobbyJoin(string colorHex) => _networkClient?.SendLobbyJoin(colorHex);
	public void SendPosition(Vector3 position) => _networkClient?.SendPosition(position);
	public void SendPacket(GamePacket packet) => _networkClient?.SendPacket(packet);
	public void ManualConnect() => _networkClient?.ManualConnect();
	public void ManualDisconnect() => _networkClient?.ManualDisconnect();
	public void SetAutoReconnect(bool enabled) => _networkClient?.SetAutoReconnect(enabled);
	public float GetNextReconnectTime() => _networkClient?.GetNextReconnectTime() ?? 0f;
	public bool IsConnected() => _networkClient?.IsConnected() ?? false;
	public string GetPrivateId() => _networkClient?.GetPrivateId();
	public string GetPublicId() => _networkClient?.GetPublicId();
	public NetworkClient.ConnectionState GetConnectionState() => _networkClient?.GetConnectionState() ?? NetworkClient.ConnectionState.Disconnected;

	#endregion

	private void OnDestroy()
	{
		if (enableModuleSystem && _moduleRegistry != null)
		{
			_moduleRegistry.DestroyAllModules();
		}
	}

	private void OnValidate()
	{
		// Sync settings to NetworkClient when changed in inspector
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