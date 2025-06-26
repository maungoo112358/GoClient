using System.Collections.Generic;
using UnityEngine;

public class NetworkMovementManager : MonoBehaviour
{
	private ClientSpawner clientSpawner; 
	private LocalMovementController cachedLocalMovementController;

	[Header("Interpolation Settings")]
	public float interpolationRate = 15f;

	//Predict where they should be
	public float extrapolationLimit = 0.5f;

	private Dictionary<string, ClientMovementData> clientMovements = new Dictionary<string, ClientMovementData>();
	private string cachedLocalClientID = "";

	private struct MovementSnapshot
	{
		public Vector3 position;
		public Vector3 velocity;
		public float timestamp;
	}

	private class ClientMovementData
	{
		public Queue<MovementSnapshot> snapshots = new Queue<MovementSnapshot>();
		public Vector3 currentPosition;
		public Vector3 targetPosition;
		public Vector3 velocity;
		public float lastUpdateTime;
		public GameObject clientObject;

		public const int maxSnapshots = 10;
	}

	private void Start()
	{
		clientSpawner = GetComponent<ClientSpawner>();
		ModularEventSystem.Instance.Subscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
		ModularEventSystem.Instance.Subscribe<PlayerJoinedLobbyEvent>(OnLocalPlayerJoined);
		ModularEventSystem.Instance.Subscribe<PlayerMovementEvent>(OnPlayerMovementUpdate);
		ModularEventSystem.Instance.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);

	}

	private void OnDestroy()
	{
		if (ModularEventSystem.Instance != null)
		{
			ModularEventSystem.Instance.Unsubscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
			ModularEventSystem.Instance.Unsubscribe<PlayerJoinedLobbyEvent>(OnLocalPlayerJoined);
			ModularEventSystem.Instance.Unsubscribe<PlayerMovementEvent>(OnPlayerMovementUpdate);
			ModularEventSystem.Instance.Unsubscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
		}
	}

	private void Update()
	{
		InterpolateClientMovements();
	}

	private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent evt)
	{
		if (evt.IsLocalPlayer)
		{
			var localClientObject = clientSpawner?.GetClientGameObject(evt.PublicID);
			cachedLocalMovementController = localClientObject?.GetComponent<LocalMovementController>();
		}
	}

	private void OnLocalPlayerJoined(PlayerJoinedLobbyEvent evt)
	{
		if (evt.IsLocalPlayer)
		{
			cachedLocalClientID = evt.PublicID;
		}
	}

	private void OnPlayerMovementUpdate(PlayerMovementEvent evt)
	{
		if (evt == null || string.IsNullOrEmpty(evt.ClientID)) return;

		if (evt.ClientID == cachedLocalClientID)
		{
			HandleLocalClientUpdate(evt);
		}
		else
		{
			HandleRemoteClientUpdate(evt);
		}
	}

	private void HandleLocalClientUpdate(PlayerMovementEvent evt)
	{
		if (cachedLocalMovementController != null)
		{
			cachedLocalMovementController.OnServerPositionReceived(evt.Position, evt.Timestamp);
		}
	}

	private void HandleRemoteClientUpdate(PlayerMovementEvent evt)
	{
		if (!clientMovements.TryGetValue(evt.ClientID, out ClientMovementData clientData))
		{
			clientData = new ClientMovementData();
			clientMovements[evt.ClientID] = clientData;

			clientData.clientObject = FindClientObject(evt.ClientID);

			if (clientData.clientObject != null)
			{
				clientData.currentPosition = clientData.clientObject.transform.position;
			}
		}

		MovementSnapshot snapshot = new MovementSnapshot
		{
			position = evt.Position,
			velocity = evt.Velocity,
			timestamp = evt.Timestamp
		};

		clientData.snapshots.Enqueue(snapshot);
		clientData.lastUpdateTime = Time.time;

		while (clientData.snapshots.Count > ClientMovementData.maxSnapshots)
		{
			clientData.snapshots.Dequeue();
		}

		// Update target position for interpolation
		if (clientData.snapshots.Count >= 2)
		{
			var snapshots = clientData.snapshots.ToArray();
			var latest = snapshots[snapshots.Length - 1];
			var previous = snapshots[snapshots.Length - 2];

			// Calculate interpolation target based on network delay
			float timeDiff = latest.timestamp - previous.timestamp;
			float networkDelay = Time.time - latest.timestamp;

			// Extrapolate position slightly ahead to compensate for network delay
			if (networkDelay > 0 && networkDelay < extrapolationLimit)
			{
				clientData.targetPosition = latest.position + (latest.velocity * networkDelay);
			}
			else
			{
				clientData.targetPosition = latest.position;
			}
		}
		else if (clientData.snapshots.Count == 1)
		{
			clientData.targetPosition = snapshot.position;
		}
	}

	private void InterpolateClientMovements()
	{
		foreach (var kvp in clientMovements)
		{
			ClientMovementData clientData = kvp.Value;

			if (clientData.clientObject == null) continue;

			Vector3 oldPosition = clientData.currentPosition;
			clientData.currentPosition = Vector3.Lerp(clientData.currentPosition, clientData.targetPosition, interpolationRate * Time.deltaTime);

			// Calculate velocity for smooth movement
			clientData.velocity = (clientData.currentPosition - oldPosition) / Time.deltaTime;

			clientData.clientObject.transform.position = clientData.currentPosition;

			// Clean up old snapshots
			CleanupOldSnapshots(clientData);
		}
	}

	private void CleanupOldSnapshots(ClientMovementData clientData)
	{
		float currentTime = Time.time;

		while (clientData.snapshots.Count > 0)
		{
			var snapshot = clientData.snapshots.Peek();
			if (currentTime - snapshot.timestamp > 2f)
			{
				clientData.snapshots.Dequeue();
			}
			else
			{
				break;
			}
		}
	}

	private void OnPlayerDisconnected(PlayerDisconnectedEvent evt)
	{
		if (evt == null || string.IsNullOrEmpty(evt.ClientID)) return;

		if (clientMovements.ContainsKey(evt.ClientID))
		{
			clientMovements.Remove(evt.ClientID);
			Debug.Log($"Removed movement data for disconnected player: {evt.ClientID}");
		}
	}

	private GameObject FindClientObject(string clientId)
	{
		if (clientSpawner == null) return null;

		var clientObject = clientSpawner.GetClientGameObject(clientId);
		if (clientObject == null)
		{
			Debug.LogWarning($"Could not find GameObject for client: {clientId}");
		}
		return clientObject;
	}

	public void SetLocalClientId(string clientId)
	{
		cachedLocalClientID = clientId;
	}
}