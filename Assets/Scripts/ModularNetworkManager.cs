using Gamepacket;
using System.Linq;
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
	public System.Action<string> OnUsernamePromptReceived;

	private string desiredUsername;
	private string sessionToken;

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
		_networkClient.OnUsernamePromptReceived += HandleNetworkClientUsernamePrompt; // New event subscription
		_networkClient.OnPacketReceived += HandleNetworkClientPacketReceived;
	}

	private void Start()
	{
		if (enableModuleSystem && autoInitializeModules)
		{
			InitializeModules();
		}
	}

	public void SetDesiredUsername(string username)
	{
		desiredUsername = username;
		Debug.Log($"ModularNetworkManager: Desired username set to '{username}'");
	}

	public string GetDesiredUsername()
	{
		return desiredUsername;
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

		// Handle heartbeat acknowledgments
		if (packet.HeartbeatAck != null)
		{
			ModularEventSystem.Instance.Publish(new HeartbeatAckReceivedEvent(packet.HeartbeatAck.ClientId));
		}

		// Handle username validation responses
		if (packet.UsernameResponse != null)
		{
			var response = packet.UsernameResponse;
			Debug.Log($"ModularNetworkManager: Username response - {response.Username}, Accepted: {response.IsAccepted}, Message: {response.Message}");

			ModularEventSystem.Instance.Publish(new UsernameResponseEvent(
				response.Username,
				response.IsAccepted,
				response.Message,
				response.Suggestions.ToArray()
			));
		}

		// Handle username prompts (also published here for consistency)
		if (packet.UsernamePrompt != null)
		{
			Debug.Log($"ModularNetworkManager: Username prompt packet - {packet.UsernamePrompt.Message}");
			// Already handled in HandleNetworkClientUsernamePrompt, but logging here for packet tracking
		}

		// Handle reconnection responses
		if (packet.ReconnectionResponse != null)
		{
			var response = packet.ReconnectionResponse;
			Debug.Log($"ModularNetworkManager: Reconnection response - Success: {response.IsSuccessful}, Message: {response.Message}");

			ModularEventSystem.Instance.Publish(new ReconnectionResponseEvent(
				response.IsSuccessful,
				response.Message,
				response.PrivateId,
				response.PublicId
			));
		}

		// Handle lobby join broadcasts
		if (packet.LobbyJoinBroadcast != null)
		{
			var data = packet.LobbyJoinBroadcast;
			Debug.Log($"ModularNetworkManager: Lobby join broadcast - Player: {data.PublicId}, Color: {data.Colorhex}, IsLocal: {data.IsLocalPlayer}");

			ModularEventSystem.Instance.Publish(new PlayerJoinedLobbyEvent(
				data.PublicId,
				data.Colorhex,
				data.Position.PosToVector3(),
				data.IsLocalPlayer
			));
		}

		// Handle player movement
		if (packet.ClientPosition != null)
		{
			var pos = packet.ClientPosition;
			ModularEventSystem.Instance.Publish(new PlayerMovementEvent(
				pos.ClientId,
				pos.PosToVector3(),
				pos.VelocityToVector3(),
				pos.Timestamp
			));
		}

		// Handle server status messages that indicate player disconnections
		if (packet.ServerStatus != null && packet.ServerStatus.Message.Contains("left the lobby"))
		{
			string playerWhoLeft = packet.ServerStatus.ClientId;
			if (!string.IsNullOrEmpty(playerWhoLeft))
			{
				Debug.Log($"ModularNetworkManager: Player {playerWhoLeft} left the lobby");
				ModularEventSystem.Instance.Publish(new PlayerDisconnectedEvent(playerWhoLeft, "Left lobby"));
			}
		}
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

	public void SendLobbyJoin(string colorHex) => _networkClient?.SendLobbyJoin(colorHex);

	public void SendPosition(Vector3 position, Vector3 velocity) => _networkClient?.SendPosition(position, velocity);

	public void SendUsernameSubmission(string username) => _networkClient?.SendUsernameSubmission(username);

	public void SendPacket(GamePacket packet) => _networkClient?.SendPacket(packet);

	public void ManualConnect()
	{
		Debug.Log($"ModularNetworkManager: Manual connect requested");
		_networkClient?.ManualConnect();
	}

	public void ManualDisconnect() => _networkClient?.ManualDisconnect();

	public void SetAutoReconnect(bool enabled) => _networkClient?.SetAutoReconnect(enabled);

	public float GetNextReconnectTime() => _networkClient?.GetNextReconnectTime() ?? 0f;

	public bool IsConnected() => _networkClient?.IsConnected() ?? false;

	public string GetPrivateId() => _networkClient?.GetPrivateId();

	public string GetPublicId() => _networkClient?.GetPublicId();

	public NetworkClient.ConnectionState? GetConnectionState() => _networkClient?.GetConnectionState();

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