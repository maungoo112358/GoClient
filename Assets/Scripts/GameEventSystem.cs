using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Enum for all event types - add new events here
public enum GameEventType
{
	// Connection Events
	ClientConnected,
	ClientDisconnected,

	// Movement Events
	LocalPlayerMoved,
	RemotePlayerMoved,

	// Chat Events
	ChatMessageReceived,
	ChatMessageSent,

	// Lobby Events
	PlayerJoinedLobby,
	PlayerLeftLobby,

	// Building Events
	BuildingSpawned,
	BuildingDestroyed,

	// Voice Events
	VoiceStarted,
	VoiceStopped,

	// System Events
	ModuleEnabled,
	ModuleDisabled,
	ServerMessage
}

// Base interface for all game events
public interface IGameEvent
{
	GameEventType EventType { get; }
	float Timestamp { get; }
}

// Base class for all game events
public abstract class GameEvent : IGameEvent
{
	public abstract GameEventType EventType { get; }
	public float Timestamp { get; private set; }

	protected GameEvent()
	{
		Timestamp = Time.time;
	}
}

// Generic Unity Event wrapper for type safety
[Serializable]
public class GameEventUnityEvent<T> : UnityEvent<T> where T : IGameEvent { }

// Main event system - handles all event routing
public class GameEventSystem : MonoBehaviour
{
	// Dictionary to store event handlers by event type
	private Dictionary<Type, UnityEventBase> _eventHandlers = new Dictionary<Type, UnityEventBase>();

	// Optional: Event history for debugging
	[Header("Debug Settings")]
	public bool logEvents = true;
	public int maxEventHistory = 100;

	private Queue<IGameEvent> _eventHistory = new Queue<IGameEvent>();

	// Singleton pattern for easy access
	private static GameEventSystem _instance;
	public static GameEventSystem Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = FindObjectOfType<GameEventSystem>();
				if (_instance == null)
				{
					GameObject go = new GameObject("GameEventSystem");
					_instance = go.AddComponent<GameEventSystem>();
					DontDestroyOnLoad(go);
				}
			}
			return _instance;
		}
	}

	private void Awake()
	{
		// Ensure singleton
		if (_instance == null)
		{
			_instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else if (_instance != this)
		{
			Destroy(gameObject);
		}
	}

	// Subscribe to events with type safety
	public void Subscribe<T>(UnityAction<T> handler) where T : IGameEvent
	{
		Type eventType = typeof(T);

		if (!_eventHandlers.ContainsKey(eventType))
		{
			_eventHandlers[eventType] = new GameEventUnityEvent<T>();
		}

		var eventHandler = _eventHandlers[eventType] as GameEventUnityEvent<T>;
		eventHandler?.AddListener(handler);

		if (logEvents)
		{
			Debug.Log($"📋 Subscribed to {eventType.Name}");
		}
	}

	// Unsubscribe from events
	public void Unsubscribe<T>(UnityAction<T> handler) where T : IGameEvent
	{
		Type eventType = typeof(T);

		if (_eventHandlers.ContainsKey(eventType))
		{
			var eventHandler = _eventHandlers[eventType] as GameEventUnityEvent<T>;
			eventHandler?.RemoveListener(handler);

			if (logEvents)
			{
				Debug.Log($"📋 Unsubscribed from {eventType.Name}");
			}
		}
	}

	// Trigger events
	public void TriggerEvent<T>(T gameEvent) where T : IGameEvent
	{
		Type eventType = typeof(T);

		// Add to history for debugging
		AddToHistory(gameEvent);

		if (_eventHandlers.ContainsKey(eventType))
		{
			var eventHandler = _eventHandlers[eventType] as GameEventUnityEvent<T>;
			eventHandler?.Invoke(gameEvent);

			if (logEvents)
			{
				Debug.Log($"🎯 Event triggered: {eventType.Name} at {gameEvent.Timestamp}");
			}
		}
		else if (logEvents)
		{
			Debug.LogWarning($"⚠️ No handlers for event: {eventType.Name}");
		}
	}

	// Helper method to check if anyone is listening to an event
	public bool HasListeners<T>() where T : IGameEvent
	{
		Type eventType = typeof(T);

		if (_eventHandlers.ContainsKey(eventType))
		{
			var eventHandler = _eventHandlers[eventType] as GameEventUnityEvent<T>;
			return eventHandler != null && eventHandler.GetPersistentEventCount() > 0;
		}

		return false;
	}

	// Get event count for debugging
	public int GetHandlerCount<T>() where T : IGameEvent
	{
		Type eventType = typeof(T);

		if (_eventHandlers.ContainsKey(eventType))
		{
			var eventHandler = _eventHandlers[eventType] as GameEventUnityEvent<T>;
			return eventHandler?.GetPersistentEventCount() ?? 0;
		}

		return 0;
	}

	// Clear all handlers (useful for scene transitions)
	public void ClearAllHandlers()
	{
		foreach (var handler in _eventHandlers.Values)
		{
			handler.RemoveAllListeners();
		}
		_eventHandlers.Clear();

		if (logEvents)
		{
			Debug.Log("🧹 All event handlers cleared");
		}
	}

	// Clear handlers for specific event type
	public void ClearHandlers<T>() where T : IGameEvent
	{
		Type eventType = typeof(T);

		if (_eventHandlers.ContainsKey(eventType))
		{
			_eventHandlers[eventType].RemoveAllListeners();
			_eventHandlers.Remove(eventType);

			if (logEvents)
			{
				Debug.Log($"🧹 Handlers cleared for {eventType.Name}");
			}
		}
	}

	// Debug methods
	private void AddToHistory(IGameEvent gameEvent)
	{
		_eventHistory.Enqueue(gameEvent);

		while (_eventHistory.Count > maxEventHistory)
		{
			_eventHistory.Dequeue();
		}
	}

	public IGameEvent[] GetEventHistory()
	{
		return _eventHistory.ToArray();
	}

	public void ClearHistory()
	{
		_eventHistory.Clear();
	}

	// Debug info
	public int GetTotalHandlerTypes()
	{
		return _eventHandlers.Count;
	}

	public string[] GetRegisteredEventTypes()
	{
		var types = new List<string>();
		foreach (var kvp in _eventHandlers)
		{
			types.Add(kvp.Key.Name);
		}
		return types.ToArray();
	}
}