using UnityEngine;

/// <summary>
/// Manages lobby interactions - automatically joins lobby on connection
/// and handles lobby-related events
/// </summary>
public class LobbyManager : MonoBehaviour
{
	[Header("Lobby Settings")]
	public bool autoJoinOnConnect = true;

	public string[] availableColors = {
		"#FF5733", "#33FF57", "#3357FF", "#FF33F1",
		"#F1FF33", "#33FFF1", "#FF8C33", "#8C33FF",
		"#33FF8C", "#FF338C", "#8CFF33", "#338CFF"
	};

	[Header("References")]
	public ModularNetworkManager networkManager;

	private bool _hasJoinedLobby = false;
	private string _myColor;

	private void Start()
	{
		// Find network manager if not assigned
		if (networkManager == null)
		{
			networkManager = FindObjectOfType<ModularNetworkManager>();
		}

		if (networkManager == null)
		{
			Debug.LogError("LobbyManager: No ModularNetworkManager found!");
			return;
		}

		// Subscribe to connection events
		GameEventSystem.Instance.Subscribe<PlayerConnectedEvent>(OnPlayerConnected);
		GameEventSystem.Instance.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
		GameEventSystem.Instance.Subscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
	}

	private void OnPlayerConnected(PlayerConnectedEvent evt)
	{
		Debug.Log($"LobbyManager: Connected to server as {evt.PublicId}");

		if (autoJoinOnConnect && !_hasJoinedLobby)
		{
			// Wait a moment for connection to stabilize, then join lobby
			Invoke(nameof(JoinLobby), 1f);
		}
	}

	private void OnPlayerDisconnected(PlayerDisconnectedEvent evt)
	{
		Debug.Log("LobbyManager: Disconnected from server");
		_hasJoinedLobby = false;
		_myColor = null;
	}

	private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent evt)
	{
		// Check if this is our own join event
		string myPublicId = networkManager?.GetPublicId();

		if (evt.PublicId == myPublicId)
		{
			// This is us joining
			Debug.Log($"LobbyManager: Successfully joined lobby with color {evt.ColorHex}");
			_hasJoinedLobby = true;
			_myColor = evt.ColorHex;

			// Log to GUI Logger
			if (GUILogger.Instance != null)
			{
				GUILogger.Instance.LogLobby($"You joined the lobby with color {evt.ColorHex} and pos {evt.Position}");
			}
		}
		else
		{
			// Someone else joined
			Debug.Log($"LobbyManager: Player {evt.PublicId} joined lobby with color {evt.ColorHex}");

			// Log to GUI Logger
			if (GUILogger.Instance != null)
			{
				GUILogger.Instance.LogLobby($"Player {evt.PublicId} joined with color {evt.ColorHex}");
			}
		}
	}

	[ContextMenu("Join Lobby")]
	public void JoinLobby()
	{
		if (networkManager == null || !networkManager.IsConnected())
		{
			Debug.LogWarning("LobbyManager: Cannot join lobby - not connected to server");
			return;
		}

		if (_hasJoinedLobby)
		{
			Debug.LogWarning("LobbyManager: Already joined lobby");
			return;
		}

		// Pick a random color
		string colorHex = availableColors[Random.Range(0, availableColors.Length)];

		Debug.Log($"LobbyManager: Joining lobby with color {colorHex}");
		networkManager.SendLobbyJoin(colorHex);
	}

	[ContextMenu("Leave Lobby")]
	public void LeaveLobby()
	{
		// For now, leaving lobby means disconnecting
		// You could extend this to send a specific "leave lobby" message
		if (networkManager != null && _hasJoinedLobby)
		{
			Debug.Log("LobbyManager: Leaving lobby");
			_hasJoinedLobby = false;
			_myColor = null;

			if (GUILogger.Instance != null)
			{
				GUILogger.Instance.LogLobby("You left the lobby");
			}
		}
	}

	public bool HasJoinedLobby() => _hasJoinedLobby;

	public string GetMyColor() => _myColor;

	private void OnDestroy()
	{
		if (GameEventSystem.Instance != null)
		{
			GameEventSystem.Instance.Unsubscribe<PlayerConnectedEvent>(OnPlayerConnected);
			GameEventSystem.Instance.Unsubscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
			GameEventSystem.Instance.Unsubscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
		}
	}
}