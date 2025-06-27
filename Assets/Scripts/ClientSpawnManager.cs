using System.Collections.Generic;
using UnityEngine;

public class ClientSpawnManager : MonoBehaviour
{
	public GameObject clientPrefab;
	public Transform plane;

	private PlayerJoinedLobbyEvent playerLobbyEvt;
	private string localClientID;

	private Dictionary<string, GameObject> spawnedClients = new();

	private ModularNetworkManager _networkManager;

	private void Start()
	{
		clientPrefab.SetActive(false); 

		_networkManager =GetComponent<ModularNetworkManager>();

		_networkManager.OnDisconnected += OnNetworkDisconnected;

		ModularEventSystem.Instance.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
		ModularEventSystem.Instance.Subscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinLobby);

		localClientID = GetLocalClientID();
	}

	private void OnDestroy()
	{
		if (ModularEventSystem.Instance != null)
		{
			_networkManager.OnDisconnected -= OnNetworkDisconnected;
			ModularEventSystem.Instance.Unsubscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
			ModularEventSystem.Instance.Unsubscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinLobby);
		}
	}

	private void OnPlayerJoinLobby(PlayerJoinedLobbyEvent evt)
	{
		if (evt == null) return;
		ClientSpawn(evt);
	}

	private void OnPlayerDisconnected(PlayerDisconnectedEvent evt)
	{
		if (evt == null || string.IsNullOrEmpty(evt.ClientID)) return;

		if (spawnedClients.TryGetValue(evt.ClientID, out GameObject client))
		{
			Destroy(client);
			spawnedClients.Remove(evt.ClientID);
			Debug.Log($"Removed disconnected player: {evt.ClientID}");
		}
	}

	private void ClientSpawn(PlayerJoinedLobbyEvent evt)
	{
		if (spawnedClients.ContainsKey(evt.PublicID)) return;

		Vector3 pos = evt.Position;
		var go = Instantiate(clientPrefab, pos, Quaternion.identity);
		var client = go.GetComponent<Client>();
		client.label.text = evt.PublicID;

		Color bodyColor;
		ColorUtility.TryParseHtmlString(evt.ColorHex, out bodyColor);
		client.prefabBody.GetComponent<Renderer>().materials[0].color = bodyColor;

		bool isLocalClient = evt.IsLocalPlayer;
		if (isLocalClient)
		{
			localClientID = evt.PublicID;
			client.label.color = Color.green;
			SetupLocalClient(go);
		}

		go.SetActive(true);
		spawnedClients[evt.PublicID] = go;
		Debug.Log($"Spawned {(isLocalClient ? "LOCAL" : "REMOTE")} client: {evt.PublicID}");
	}

	private void SetupLocalClient(GameObject localClient)
	{
		var movementController = localClient.AddComponent<LocalMovementController>();

		movementController.movementSpeed = 5f;
		movementController.networkSendRate = 20f;
		movementController.followLocalPlayer = true;
		movementController.cameraTransform = Camera.main.transform;
		movementController.cameraOffset = new Vector3(0, 20, -15);
		movementController.cameraFollowSpeed = 5f;
		movementController.allowInputSwitching = true;
		movementController.networkManager = GetComponent<ModularNetworkManager>();
		NetworkMovementManager networkMovementManager = GetComponent<NetworkMovementManager>();
		if (networkMovementManager != null)
		{
			networkMovementManager.SetLocalClientId(localClientID);
		}

		Debug.Log($"✅ Local client setup complete for: {localClientID}");
	}

	private string GetLocalClientID()
	{
		return localClientID ?? "";
	}

	public GameObject GetClientGameObject(string clientId)
	{
		return spawnedClients.TryGetValue(clientId, out GameObject client) ? client : null;
	}

	private void OnNetworkDisconnected()
	{
		foreach (var client in spawnedClients.Values)
		{
			if (client != null)
				Destroy(client);
		}
		spawnedClients.Clear();
		Debug.Log("Cleared all clients on network disconnect");
	}
}