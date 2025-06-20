using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ClientSpawner : MonoBehaviour
{
	public GameObject clientPrefab;
	public Transform plane;

	PlayerJoinedLobbyEvent playerLobbyEvt;

	private Dictionary<string, GameObject> spawnedClients = new();

	private void Start()
	{
		clientPrefab.SetActive(false);

		GameEventSystem.Instance.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
		GameEventSystem.Instance.Subscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinLobby);
	}

	

	private void OnDestroy()
	{
		if (GameEventSystem.Instance != null)
		{
			GameEventSystem.Instance.Unsubscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
			GameEventSystem.Instance.Unsubscribe<PlayerJoinedLobbyEvent>(OnPlayerJoinLobby);
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
		if (spawnedClients.ContainsKey(evt.PublicId)) return;

		Vector3 pos = evt.Position;
		var go = Instantiate(clientPrefab, pos, Quaternion.identity);
		var client = go.GetComponent<Client>();
		client.label.text = evt.PublicId;
		Color bodyColor;
		ColorUtility.TryParseHtmlString(evt.ColorHex, out bodyColor);
		Color headColor;
		ColorUtility.TryParseHtmlString(evt.ColorHex_Head, out headColor);
		client.prefabBody.GetComponent<Renderer>().materials[0].color = bodyColor;
		client.prefabHead.GetComponent<Renderer>().materials[0].color = headColor;
		go.SetActive(true);

		spawnedClients[evt.PublicId] = go;
	}

}