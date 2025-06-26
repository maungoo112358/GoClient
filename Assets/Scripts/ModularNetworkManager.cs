using Gamepacket;
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
		_networkClient.OnPacketReceived += HandleNetworkClientPacketReceived;
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

		OnConnected?.Invoke(privateId, publicId);

		if (enableModuleSystem)
		{
			ModularEventSystem.Instance.Publish(new PlayerConnectedEvent(privateId, publicId));
			Debug.Log("HandleNetworkClientConnected");
		}

		if (enableModuleSystem && _modulesInitialized)
		{
			_moduleRegistry.EnableAllModules();
		}
	}

	private void HandleNetworkClientDisconnected()
	{
		//Debug.Log("Disconnected - bridging to event system");

		OnDisconnected?.Invoke();

		if (enableModuleSystem)
		{
			ModularEventSystem.Instance.Publish(new PlayerDisconnectedEvent("Connection lost"));

			if (_modulesInitialized)
			{
				_moduleRegistry.DisableAllModules();
			}
		}
	}

	private void HandleNetworkClientServerMessage(string message)
	{
		//Debug.Log($"Server message - bridging to event system: {message}");

		OnServerMessage?.Invoke(message);

		if (enableModuleSystem)
		{
			ModularEventSystem.Instance.Publish(new ServerMessageEvent(message));
		}
	}

	private void HandleNetworkClientPacketReceived(GamePacket packet)
	{
		if (!enableModuleSystem) return;

		if (packet.HeartbeatAck != null)
		{
			ModularEventSystem.Instance.Publish(new HeartbeatAckReceivedEvent(packet.HeartbeatAck.ClientId));
		}
		if (packet.ChatMessage != null)
		{
			ModularEventSystem.Instance.Publish(new ChatMessageReceivedEvent(packet.ChatMessage.ClientId, packet.ChatMessage.Message));
		}
		if (packet.LobbyJoinBroadcast != null)
		{
			var data = packet.LobbyJoinBroadcast;
			ModularEventSystem.Instance.Publish(new PlayerJoinedLobbyEvent(data.PublicId, data.Colorhex, data.Position.PosToVector3(),data.IsLocalPlayer));
		}
		if (packet.ClientPosition != null)
		{
			var pos = packet.ClientPosition;
			ModularEventSystem.Instance.Publish(new PlayerMovementEvent(pos.ClientId, pos.PosToVector3(), pos.VelocityToVector3(), pos.Timestamp));
		}
		if (packet.ServerStatus != null && packet.ServerStatus.Message.Contains("left the lobby"))
		{
			string playerWhoLeft = packet.ServerStatus.ClientId;
			if (!string.IsNullOrEmpty(playerWhoLeft))
			{
				ModularEventSystem.Instance.Publish(new PlayerDisconnectedEvent(playerWhoLeft));
			}
		}
	}

	private string ExtractPlayerIdFromMessage(string message)
	{
		var match = System.Text.RegularExpressions.Regex.Match(message, @"Player (.+?) left the lobby");
		return match.Success ? match.Groups[1].Value : string.Empty;
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

	public bool IsModuleSystemEnabled()
	{
		return enableModuleSystem;
	}

	#endregion Module Management

	#region NetworkClient Passthrough Methods

	public void SendChatMessage(string message) => _networkClient?.SendChatMessage(message);

	public void SendLobbyJoin(string colorHex) => _networkClient?.SendLobbyJoin(colorHex);

	public void SendPosition(Vector3 position, Vector3 velocity) => _networkClient?.SendPosition(position, velocity);

	public void SendPacket(GamePacket packet) => _networkClient?.SendPacket(packet);

	public void ManualConnect() => _networkClient?.ManualConnect();

	public void ManualDisconnect() => _networkClient?.ManualDisconnect();

	public void SetAutoReconnect(bool enabled) => _networkClient?.SetAutoReconnect(enabled);

	public float GetNextReconnectTime() => _networkClient?.GetNextReconnectTime() ?? 0f;

	public bool IsConnected() => _networkClient?.IsConnected() ?? false;

	public string GetPrivateId() => _networkClient?.GetPrivateId();

	public string GetPublicId() => _networkClient?.GetPublicId();

	public NetworkClient.ConnectionState GetConnectionState() => _networkClient?.GetConnectionState() ?? NetworkClient.ConnectionState.Disconnected;

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