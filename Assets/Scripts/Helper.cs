using Gamepacket;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Helper
{
	public static Vector3 PosToVector3(this ClientLobbyPosition lobby) => new(lobby.Position.X, lobby.Position.Y, lobby.Position.Z);

	public static Vector3 PosToVector3(this ClientPosition client) => new(client.Position.X, client.Position.Y, client.Position.Z);

	public static Vector3 VelocityToVector3(this ClientPosition client) => new(client.Velocity.X, client.Velocity.Y, client.Velocity.Z);

	public static List<Tile> GetTileSet(this TileSet tileSet)
	{
		Debug.Log($"Processing {tileSet.Tiles.Count} tiles");
		var tiles = new List<Tile>(tileSet.Tiles.Count);
		tiles.AddRange(tileSet.Tiles.Select(tile => {
			Debug.Log($"Converting tile {tile.TileId} at {tile.GetTilePosition()}");
			var (position, rotation, scale, type) = tile.GetTileData();
			return new Tile(tile.TileId, position, rotation, scale, type, tile.IsScalable);
		}));
		Debug.Log($"Converted {tiles.Count} tiles");
		return tiles;
	}

	public static Vector3 GetTilePosition(this Gamepacket.Tile tile) => new(tile.Position.X, tile.Position.Y, tile.Position.Z);

	public static Vector3 GetTileRotation(this Gamepacket.Tile tile) => new(tile.Rotation.X, tile.Rotation.Y, tile.Rotation.Z);

	public static Vector3 GetTileScale(this Gamepacket.Tile tile) => new(tile.Scale.X, tile.Scale.Y, tile.Scale.Z);

	public static TileType GetTileType(this Gamepacket.Tile tile) => (int)tile.Type switch
	{
		0 => TileType.ROAD_LANE,
		1 => TileType.CROSS_SECTION,
		2 => TileType.GRASS,
		_ => TileType.None
	};

	public static (Vector3 position, Vector3 rotation, Vector3 scale, TileType type) GetTileData(this Gamepacket.Tile tile) =>
	   (new(tile.Position.X, tile.Position.Y, tile.Position.Z), new(tile.Rotation.X, tile.Rotation.Y, tile.Rotation.Z), new(tile.Scale.X, tile.Scale.Y, tile.Scale.Z), (int)tile.Type switch
	   {
		   0 => TileType.ROAD_LANE,
		   1 => TileType.CROSS_SECTION,
		   2 => TileType.GRASS,
		   _ => TileType.None
	   });
}