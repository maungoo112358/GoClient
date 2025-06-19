using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized event system for decoupled communication between modules
/// </summary>
public class GameEventSystem : MonoBehaviour
{
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
					var go = new GameObject("GameEventSystem");
					_instance = go.AddComponent<GameEventSystem>();
					DontDestroyOnLoad(go);
				}
			}
			return _instance;
		}
	}

	// Dictionary to store event subscribers
	private Dictionary<Type, List<Delegate>> _eventSubscribers = new Dictionary<Type, List<Delegate>>();

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
		}
	}

	/// <summary>
	/// Subscribe to an event
	/// </summary>
	public void Subscribe<T>(Action<T> callback) where T : IGameEvent
	{
		Type eventType = typeof(T);

		if (!_eventSubscribers.ContainsKey(eventType))
		{
			_eventSubscribers[eventType] = new List<Delegate>();
		}

		_eventSubscribers[eventType].Add(callback);
		Debug.Log($"Subscribed to event: {eventType.Name}");
	}

	/// <summary>
	/// Unsubscribe from an event
	/// </summary>
	public void Unsubscribe<T>(Action<T> callback) where T : IGameEvent
	{
		Type eventType = typeof(T);

		if (_eventSubscribers.ContainsKey(eventType))
		{
			_eventSubscribers[eventType].Remove(callback);
			Debug.Log($"Unsubscribed from event: {eventType.Name}");
		}
	}

	/// <summary>
	/// Publish an event to all subscribers
	/// </summary>
	public void Publish<T>(T gameEvent) where T : IGameEvent
	{
		Type eventType = typeof(T);

		if (_eventSubscribers.ContainsKey(eventType))
		{
			var subscribers = _eventSubscribers[eventType];
			Debug.Log($"Publishing event: {eventType.Name} to {subscribers.Count} subscribers");

			for (int i = subscribers.Count - 1; i >= 0; i--)
			{
				try
				{
					var callback = subscribers[i] as Action<T>;
					callback?.Invoke(gameEvent);
				}
				catch (Exception ex)
				{
					Debug.LogError($"Error invoking event callback for {eventType.Name}: {ex.Message}");
				}
			}
		}
		else
		{
			Debug.Log($"No subscribers for event: {eventType.Name}");
		}
	}

	/// <summary>
	/// Get subscriber count for debugging
	/// </summary>
	public int GetSubscriberCount<T>() where T : IGameEvent
	{
		Type eventType = typeof(T);
		return _eventSubscribers.ContainsKey(eventType) ? _eventSubscribers[eventType].Count : 0;
	}

	/// <summary>
	/// Clear all subscribers (useful for scene changes)
	/// </summary>
	public void ClearAllSubscribers()
	{
		_eventSubscribers.Clear();
		Debug.Log("Cleared all event subscribers");
	}

	private void OnDestroy()
	{
		ClearAllSubscribers();
	}
}