using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UsernameManager : MonoBehaviour
{
	[Header("UI References")]
	public GameObject usernamePanel;
	public TMP_InputField usernameInput;
	public Button submitButton;
	public Button connectButton;
	public TMP_Text statusText;

	[Header("Settings")]
	public string defaultUsername = "Player";
	public bool autoHideOnSuccess = true;
	public bool rememberUsername = true;

	[Header("Debug")]
	public bool enableDebugLogs = true;

	private ModularNetworkManager _networkManager;
	private bool _isWaitingForPrompt = false;
	private bool _isValidatingUsername = false;

	private void Start()
	{
		InitializeComponents();
		SetupEventListeners();
		LoadSavedUsername();
		UpdateUI();
	}

	private void InitializeComponents()
	{
		_networkManager = FindObjectOfType<ModularNetworkManager>();
		if (_networkManager == null)
		{
			Debug.LogError("UsernameManager: ModularNetworkManager not found!");
			return;
		}

		// Setup UI components
		if (usernameInput != null)
		{
			usernameInput.onEndEdit.AddListener(OnUsernameInputEndEdit);
		}

		if (submitButton != null)
		{
			submitButton.onClick.AddListener(OnSubmitButtonClicked);
		}

		if (connectButton != null)
		{
			connectButton.onClick.AddListener(OnConnectButtonClicked);
		}

		// Set default values
		if (usernameInput != null && string.IsNullOrEmpty(usernameInput.text))
		{
			usernameInput.text = defaultUsername;
		}
	}

	private void SetupEventListeners()
	{
		if (_networkManager == null) return;

		// Listen for network events
		_networkManager.OnConnected += OnNetworkConnected;
		_networkManager.OnDisconnected += OnNetworkDisconnected;
		_networkManager.OnUsernamePromptReceived += OnUsernamePromptReceived; // New event listener

		// Listen for modular events if module system is enabled
		if (_networkManager.IsModuleSystemEnabled())
		{
			ModularEventSystem.Instance.Subscribe<UsernamePromptEvent>(OnUsernamePromptEvent);
			ModularEventSystem.Instance.Subscribe<UsernameResponseEvent>(OnUsernameResponseEvent);
			ModularEventSystem.Instance.Subscribe<PlayerConnectedEvent>(OnPlayerConnectedEvent);
			ModularEventSystem.Instance.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnectedEvent);
		}
	}

	#region Network Event Handlers

	private void OnNetworkConnected(string privateId, string publicId)
	{
		LogDebug($"Network connected - Private: {privateId}, Public: {publicId}");

		if (autoHideOnSuccess)
		{
			HideUsernamePanel();
		}

		UpdateStatusText("Connected successfully!", Color.green);

		// Save username for future use
		if (rememberUsername && usernameInput != null)
		{
			SaveUsername(usernameInput.text);
		}

		_isValidatingUsername = false;
		_isWaitingForPrompt = false;
	}

	private void OnNetworkDisconnected()
	{
		LogDebug("Network disconnected");
		UpdateStatusText("Disconnected from server", Color.yellow);
		ShowUsernamePanel();
		_isValidatingUsername = false;
		_isWaitingForPrompt = false;
		UpdateUI();
	}

	private void OnUsernamePromptReceived(string promptMessage)
	{
		LogDebug($"Username prompt received: {promptMessage}");

		_isWaitingForPrompt = false;

		// Show username panel and enable input
		ShowUsernamePanel();
		UpdateStatusText(promptMessage, Color.blue); // Display server's prompt as status
		UpdateUI();

		// Focus the input field
		if (usernameInput != null)
		{
			usernameInput.Select();
			usernameInput.ActivateInputField();
		}
	}

	#endregion

	#region Modular Event Handlers

	private void OnUsernamePromptEvent(UsernamePromptEvent eventData)
	{
		LogDebug($"Username prompt event: {eventData.PromptMessage}");
		OnUsernamePromptReceived(eventData.PromptMessage);
	}

	private void OnUsernameResponseEvent(UsernameResponseEvent eventData)
	{
		LogDebug($"Username response - Username: {eventData.Username}, Accepted: {eventData.IsAccepted}, Message: {eventData.Message}");

		_isValidatingUsername = false;

		if (eventData.IsAccepted)
		{
			UpdateStatusText($"Username '{eventData.Username}' accepted!", Color.green);
			// Connection completion will be handled by OnNetworkConnected
		}
		else
		{
			UpdateStatusText($"Username rejected: {eventData.Message}", Color.red);

			// Re-enable UI for another attempt
			UpdateUI();

			// Focus the input field for correction
			if (usernameInput != null)
			{
				usernameInput.Select();
				usernameInput.ActivateInputField();
			}
		}
	}

	private void OnPlayerConnectedEvent(PlayerConnectedEvent eventData)
	{
		LogDebug($"Player connected event - Private: {eventData.PrivateID}, Public: {eventData.PublicID}");
		// Additional handling if needed
	}

	private void OnPlayerDisconnectedEvent(PlayerDisconnectedEvent eventData)
	{
		LogDebug($"Player disconnected event - Client: {eventData.ClientID}, Reason: {eventData.Reason}");
		// Additional handling if needed
	}

	#endregion

	#region UI Event Handlers

	private void OnConnectButtonClicked()
	{
		LogDebug("Connect button clicked");

		if (_networkManager == null)
		{
			UpdateStatusText("Network manager not found!", Color.red);
			return;
		}

		if (_networkManager.IsConnected())
		{
			UpdateStatusText("Already connected!", Color.yellow);
			return;
		}

		// Start connection process - no username required yet
		_isWaitingForPrompt = true;
		UpdateStatusText("Connecting to server...", Color.blue);
		UpdateUI();

		_networkManager.ManualConnect();
	}

	private void OnSubmitButtonClicked()
	{
		LogDebug("Submit button clicked");
		SubmitUsername();
	}

	private void OnUsernameInputEndEdit(string value)
	{
		if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
		{
			SubmitUsername();
		}
	}

	#endregion

	#region Core Functionality

	private void SubmitUsername()
	{
		if (_networkManager == null)
		{
			UpdateStatusText("Network manager not found!", Color.red);
			return;
		}

		if (_isValidatingUsername)
		{
			UpdateStatusText("Username validation in progress...", Color.yellow);
			return;
		}

		string username = usernameInput?.text?.Trim();
		if (string.IsNullOrEmpty(username))
		{
			UpdateStatusText("Please enter a username", Color.red);
			return;
		}

		// Check connection state
		var connectionState = _networkManager.GetConnectionState();
		if (connectionState != ConnectionState.HandshakeComplete)
		{
			UpdateStatusText("Not ready for username submission. Please connect first.", Color.yellow);
			return;
		}

		LogDebug($"Submitting username: {username}");

		_isValidatingUsername = true;
		UpdateStatusText($"Validating username '{username}'...", Color.blue);
		UpdateUI();

		// Send username to server for validation
		_networkManager.SendUsernameSubmission(username);
	}

	private void UpdateUI()
	{
		bool isConnected = _networkManager?.IsConnected() ?? false;
		var connectionState = _networkManager?.GetConnectionState();

		// Enable/disable connect button
		if (connectButton != null)
		{
			connectButton.interactable = !isConnected && !_isWaitingForPrompt;
			connectButton.GetComponentInChildren<TMP_Text>().text = isConnected ? "Connected" :
				_isWaitingForPrompt ? "Connecting..." : "Connect";
		}

		if (submitButton != null)
		{
			bool canSubmitUsername = connectionState == ConnectionState.HandshakeComplete && !isConnected;

			submitButton.interactable = canSubmitUsername;
			submitButton.GetComponentInChildren<TMP_Text>().text = _isValidatingUsername ? "Validating..." : "Submit";
		}

		// Enable/disable username input - allow changes during validation for quick retry
		if (usernameInput != null)
		{
			usernameInput.interactable = !isConnected && !_isWaitingForPrompt;
		}
	}

	private void ShowUsernamePanel()
	{
		if (usernamePanel != null)
		{
			usernamePanel.SetActive(true);
		}
	}

	private void HideUsernamePanel()
	{
		if (usernamePanel != null)
		{
			usernamePanel.SetActive(false);
		}
	}

	private void UpdateStatusText(string message, Color color)
	{
		if (statusText != null)
		{
			statusText.text = message;
			statusText.color = color;
		}

		LogDebug($"Status: {message}");
	}

	#endregion

	#region Username Persistence

	private void SaveUsername(string username)
	{
		if (!rememberUsername) return;

		PlayerPrefs.SetString("SavedUsername", username);
		PlayerPrefs.Save();
		LogDebug($"Username saved: {username}");
	}

	private void LoadSavedUsername()
	{
		if (!rememberUsername) return;

		string savedUsername = PlayerPrefs.GetString("SavedUsername", defaultUsername);
		if (usernameInput != null && !string.IsNullOrEmpty(savedUsername))
		{
			usernameInput.text = savedUsername;
			LogDebug($"Username loaded: {savedUsername}");
		}
	}

	#endregion

	#region Public API

	public void SetUsername(string username)
	{
		if (usernameInput != null)
		{
			usernameInput.text = username;
		}
	}

	public string GetCurrentUsername()
	{
		return usernameInput?.text?.Trim() ?? "";
	}

	public bool IsUsernameValidating()
	{
		return _isValidatingUsername;
	}

	public void ShowPanel()
	{
		ShowUsernamePanel();
	}

	public void HidePanel()
	{
		HideUsernamePanel();
	}

	#endregion

	#region Utility

	private void LogDebug(string message)
	{
		if (enableDebugLogs)
		{
			Debug.Log($"UsernameManager: {message}");
		}
	}

	#endregion

	#region Cleanup

	private void OnDestroy()
	{
		// Unsubscribe from events
		if (_networkManager != null)
		{
			_networkManager.OnConnected -= OnNetworkConnected;
			_networkManager.OnDisconnected -= OnNetworkDisconnected;
			_networkManager.OnUsernamePromptReceived -= OnUsernamePromptReceived;
		}

		if (_networkManager?.IsModuleSystemEnabled() == true)
		{
			ModularEventSystem.Instance.Unsubscribe<UsernamePromptEvent>(OnUsernamePromptEvent);
			ModularEventSystem.Instance.Unsubscribe<UsernameResponseEvent>(OnUsernameResponseEvent);
			ModularEventSystem.Instance.Unsubscribe<PlayerConnectedEvent>(OnPlayerConnectedEvent);
			ModularEventSystem.Instance.Unsubscribe<PlayerDisconnectedEvent>(OnPlayerDisconnectedEvent);
		}

		// Clean up UI events
		if (usernameInput != null)
		{
			usernameInput.onEndEdit.RemoveAllListeners();
		}
		if (submitButton != null)
		{
			submitButton.onClick.RemoveAllListeners();
		}
		if (connectButton != null)
		{
			connectButton.onClick.RemoveAllListeners();
		}
	}

	#endregion
}