using UnityEngine;

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
		if (networkManager == null)
		{
			networkManager = GetComponent<ModularNetworkManager>();
		}

		if (networkManager == null)
		{
			Debug.LogError("LobbyManager: No ModularNetworkManager found!");
			return;
		}

		ModularEventSystem.Instance.Subscribe<PlayerConnectedEvent>(OnPlayerConnected);
		ModularEventSystem.Instance.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
		ModularEventSystem.Instance.Subscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
	}

	private void OnPlayerConnected(PlayerConnectedEvent evt)
	{
		Debug.Log($"LobbyManager: Connected to server as {evt.PublicID}");

		if (autoJoinOnConnect && !_hasJoinedLobby)
		{
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
		string myPublicId = networkManager?.GetPublicId();

		if (evt.PublicID == myPublicId)
		{
			Debug.Log($"LobbyManager: Successfully joined lobby with color {evt.ColorHex}");
			_hasJoinedLobby = true;
			_myColor = evt.ColorHex;
		}
		else
		{
			Debug.Log($"LobbyManager: Player {evt.PublicID} joined lobby with color {evt.ColorHex}");
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

		string colorHex = availableColors[Random.Range(0, availableColors.Length)];

		Debug.Log($"LobbyManager: Joining lobby with color {colorHex}");
		networkManager.SendLobbyJoin(colorHex);
	}

	[ContextMenu("Leave Lobby")]
	public void LeaveLobby()
	{
		if (networkManager != null && _hasJoinedLobby)
		{
			Debug.Log("LobbyManager: Leaving lobby");
			_hasJoinedLobby = false;
			_myColor = null;
		}
	}

	public bool HasJoinedLobby() => _hasJoinedLobby;

	public string GetMyColor() => _myColor;

	private void OnDestroy()
	{
		if (ModularEventSystem.Instance != null)
		{
			ModularEventSystem.Instance.Unsubscribe<PlayerConnectedEvent>(OnPlayerConnected);
			ModularEventSystem.Instance.Unsubscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
			ModularEventSystem.Instance.Unsubscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
		}
	}
}