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
		var tiles = new List<Tile>(tileSet.Tiles.Count);
		tiles.AddRange(tileSet.Tiles.Select(tile =>
		{
			var (position, rotation, scale, type) = tile.GetTileData();
			return new Tile(tile.TileId, position, rotation, scale, type, tile.IsScalable);
		}));
		return tiles;
	}

	public static (Vector3 position, Vector3 rotation, Vector3 scale, TileType type) GetTileData(this Gamepacket.Tile tile) =>
	   (new(tile.Position.X, tile.Position.Y, tile.Position.Z), new(tile.Rotation.X, tile.Rotation.Y, tile.Rotation.Z), new(tile.Scale.X, tile.Scale.Y, tile.Scale.Z), (int)tile.Type switch
	   {
		   1 => TileType.ROAD_LANE,
		   2 => TileType.CROSS_INTERSECTION,
		   3 => TileType.GRASS,
		   _ => TileType.None
	   });

	public static string GetAddressFromTileType(this TileType tileType) =>
		tileType switch
		{
			TileType.ROAD_LANE => "road_lane",
			TileType.CROSS_INTERSECTION => "cross_intersection",
			TileType.GRASS => "grass",
			_ => "grass"
		};
}