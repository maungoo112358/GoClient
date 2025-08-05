using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
public class TileGenerationManager : MonoBehaviour
{
	[SerializeField]
	private Transform TileParent;

	private Dictionary<string, GameObject> spawnedTiles = new();

	private void Start()
	{
		ModularEventSystem.Instance.Subscribe<TileGenerationEvent>(OnTileGeneration);
	}

	private async void OnTileGeneration(TileGenerationEvent evt)
	{
		ClearAllTiles();

		for (int i = 0; i < evt.TileSet.Count; i++)
		{
			var tile = evt.TileSet[i];
			string address = tile.TileType.GetAddressFromTileType();

			try
			{
				var prefab = await Addressables.LoadAssetAsync<GameObject>(address).Task;
				var go = Instantiate(prefab, tile.Position, Quaternion.Euler(tile.Rotation), TileParent);
				go.transform.localScale = tile.Scale;
				go.name = $"{address}_{tile.TileId}";

				spawnedTiles[tile.TileId] = go;
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Failed to load addressable '{address}': {ex.Message}");
			}
		}
	}


	private void ClearAllTiles()
	{
		foreach (var tile in spawnedTiles.Values)
		{
			if (tile != null) Destroy(tile.gameObject);
		}
		spawnedTiles.Clear();
	}

	private void OnDestroy()
	{
		ModularEventSystem.Instance.Unsubscribe<TileGenerationEvent>(OnTileGeneration);
	}
}