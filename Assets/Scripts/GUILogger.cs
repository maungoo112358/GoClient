using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game logger with tabs for different message types
/// Replaces Debug.Log for player-visible messages
/// </summary>
public class GUILogger : MonoBehaviour
{
	[Header("UI References")]
	public GameObject loggerPanel;

	public Button toggleButton;
	public Transform tabContainer;
	public Transform contentContainer;
	public ScrollRect scrollRect;
	public TMP_Text logTextPrefab;

	[Header("Logger Settings")]
	public int maxMessages = 100;

	public bool startVisible = true;
	public KeyCode toggleKey = KeyCode.F1;

	[Header("Tab Settings")]
	public Color activeTabColor = Color.white;

	public Color inactiveTabColor = Color.gray;

	// Logger categories
	public enum LogCategory
	{
		Connection,
		Lobby,
		Chat,
		System,
		All
	}

	// Tab system
	private Dictionary<LogCategory, Button> _tabs = new Dictionary<LogCategory, Button>();

	private Dictionary<LogCategory, List<LogMessage>> _messages = new Dictionary<LogCategory, List<LogMessage>>();
	private LogCategory _currentCategory = LogCategory.All;
	private bool _isVisible = true;

	// Message data
	private struct LogMessage
	{
		public string text;
		public LogCategory category;
		public DateTime timestamp;
		public Color color;
	}

	// Singleton for easy access
	private static GUILogger _instance;

	public static GUILogger Instance
	{
		get
		{
			if (_instance == null)
				_instance = FindObjectOfType<GUILogger>();
			return _instance;
		}
	}

	private void Awake()
	{
		if (_instance == null)
		{
			_instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else if (_instance != this)
		{
			Destroy(gameObject);
			return;
		}

		InitializeLogger();
	}

	private void Start()
	{
		// Subscribe to game events
		GameEventSystem.Instance.Subscribe<PlayerConnectedEvent>(OnPlayerConnected);
		GameEventSystem.Instance.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
		//GameEventSystem.Instance.Subscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
		GameEventSystem.Instance.Subscribe<ServerMessageEvent>(OnServerMessage);
		GameEventSystem.Instance.Subscribe<ChatMessageReceivedEvent>(OnChatMessage);

		SetVisible(startVisible);
	}

	private void Update()
	{
		if (Input.GetKeyDown(toggleKey))
		{
			ToggleVisibility();
		}
	}

	private void InitializeLogger()
	{
		// Initialize message lists
		foreach (LogCategory category in Enum.GetValues(typeof(LogCategory)))
		{
			_messages[category] = new List<LogMessage>();
		}

		// Setup toggle button
		if (toggleButton != null)
		{
			toggleButton.onClick.AddListener(ToggleVisibility);
		}

		// Create tabs
		CreateTabs();

		// Set initial tab
		SwitchToTab(LogCategory.All);
	}

	private void CreateTabs()
	{
		if (tabContainer == null) return;

		foreach (LogCategory category in Enum.GetValues(typeof(LogCategory)))
		{
			CreateTab(category);
		}
	}

	private void CreateTab(LogCategory category)
	{
		// Create tab button
		GameObject tabObj = new GameObject($"Tab_{category}");
		tabObj.transform.SetParent(tabContainer);

		Button tabButton = tabObj.AddComponent<Button>();
		Image tabImage = tabObj.AddComponent<Image>();
		tabImage.color = inactiveTabColor;

		// Add text to tab
		GameObject textObj = new GameObject("Text");
		textObj.transform.SetParent(tabObj.transform);
		Text tabText = textObj.AddComponent<Text>();
		tabText.text = category.ToString();
		tabText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tabText.fontSize = 14;
		tabText.color = Color.black;
		tabText.alignment = TextAnchor.MiddleCenter;

		// Setup RectTransforms
		RectTransform tabRect = tabObj.GetComponent<RectTransform>();
		tabRect.sizeDelta = new Vector2(50, 30);
		tabRect.anchoredPosition = Vector2.zero;

		RectTransform textRect = textObj.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.sizeDelta = Vector2.zero;
		textRect.anchoredPosition = Vector2.zero;

		// Setup button click
		tabButton.onClick.AddListener(() => SwitchToTab(category));

		_tabs[category] = tabButton;
	}

	private void SwitchToTab(LogCategory category)
	{
		_currentCategory = category;

		// Update tab colors
		foreach (var kvp in _tabs)
		{
			if (kvp.Value != null)
			{
				kvp.Value.GetComponent<Image>().color =
					kvp.Key == category ? activeTabColor : inactiveTabColor;
			}
		}

		// Refresh display
		RefreshDisplay();
	}

	private void RefreshDisplay()
	{
		if (contentContainer == null) return;

		// Clear existing content
		foreach (Transform child in contentContainer)
		{
			Destroy(child.gameObject);
		}

		// Get messages to display
		List<LogMessage> messagesToShow = _currentCategory == LogCategory.All ?
			GetAllMessages() : _messages[_currentCategory];

		// Create UI elements for messages
		foreach (var message in messagesToShow)
		{
			CreateMessageUI(message);
		}

		// Scroll to bottom
		if (scrollRect != null)
		{
			Canvas.ForceUpdateCanvases();
			scrollRect.verticalNormalizedPosition = 0f;
		}
	}

	private List<LogMessage> GetAllMessages()
	{
		List<LogMessage> allMessages = new List<LogMessage>();

		foreach (var category in _messages.Keys)
		{
			if (category != LogCategory.All)
			{
				allMessages.AddRange(_messages[category]);
			}
		}

		// Sort by timestamp
		allMessages.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
		return allMessages;
	}

	private void CreateMessageUI(LogMessage message)
	{
		if (logTextPrefab == null || contentContainer == null) return;

		TMP_Text messageText = Instantiate(logTextPrefab, contentContainer);
		messageText.text = $"[{message.timestamp:HH:mm:ss}] [{message.category}] {message.text}";
		messageText.color = message.color;
	}

	// Public logging methods
	public void LogConnection(string message, Color? color = null)
	{
		AddMessage(message, LogCategory.Connection, color ?? Color.green);
	}

	public void LogLobby(string message, Color? color = null)
	{
		AddMessage(message, LogCategory.Lobby, color ?? Color.cyan);
	}

	public void LogChat(string message, Color? color = null)
	{
		AddMessage(message, LogCategory.Chat, color ?? Color.white);
	}

	public void LogSystem(string message, Color? color = null)
	{
		AddMessage(message, LogCategory.System, color ?? Color.yellow);
	}

	private void AddMessage(string text, LogCategory category, Color color)
	{
		var message = new LogMessage
		{
			text = text,
			category = category,
			timestamp = DateTime.Now,
			color = color
		};

		// Add to category list
		_messages[category].Add(message);

		// Limit message count
		if (_messages[category].Count > maxMessages)
		{
			_messages[category].RemoveAt(0);
		}

		// Refresh display if this category is active
		if (_currentCategory == category || _currentCategory == LogCategory.All)
		{
			RefreshDisplay();
		}
	}

	public void ToggleVisibility()
	{
		SetVisible(!_isVisible);
	}

	public void SetVisible(bool visible)
	{
		_isVisible = visible;
		if (loggerPanel != null)
		{
			loggerPanel.SetActive(visible);
		}
	}

	// Event handlers
	private void OnPlayerConnected(PlayerConnectedEvent evt)
	{
		LogConnection($"Connected to server! Player ID: {evt.PublicId}");
	}

	private void OnPlayerDisconnected(PlayerDisconnectedEvent evt)
	{
		LogConnection($"Disconnected from server. Reason: {evt.Reason}");
	}

	//private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent evt)
	//{
	//	LogLobby($"Player {evt.PublicId} joined the lobby (Color: {evt.ColorHex})");
	//}

	private void OnServerMessage(ServerMessageEvent evt)
	{
		LogSystem($"Server: {evt.Message}");
	}

	private void OnChatMessage(ChatMessageReceivedEvent evt)
	{
		LogChat($"{evt.SenderId}: {evt.Message}");
	}

	private void OnDestroy()
	{
		if (GameEventSystem.Instance != null)
		{
			GameEventSystem.Instance.Unsubscribe<PlayerConnectedEvent>(OnPlayerConnected);
			GameEventSystem.Instance.Unsubscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
			//GameEventSystem.Instance.Unsubscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
			GameEventSystem.Instance.Unsubscribe<ServerMessageEvent>(OnServerMessage);
			GameEventSystem.Instance.Unsubscribe<ChatMessageReceivedEvent>(OnChatMessage);
		}
	}
}