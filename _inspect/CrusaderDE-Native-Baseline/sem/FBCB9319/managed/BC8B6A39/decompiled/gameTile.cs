using UnityEngine;
using UnityEngine.Tilemaps;

public class gameTile : Tile
{
	private void setTileColour(GameMapTile tile, Vector3Int location, int light)
	{
		if (tile.floatColour == 0)
		{
			switch (light)
			{
			default:
				tile.tilemapRef.SetColor(location, Color.white);
				break;
			case 1:
				tile.tilemapRef.SetColor(location, TilemapManager.shadow1);
				break;
			case 2:
				tile.tilemapRef.SetColor(location, TilemapManager.shadow2);
				break;
			case 3:
				tile.tilemapRef.SetColor(location, TilemapManager.shadow3);
				break;
			case 4:
				tile.tilemapRef.SetColor(location, TilemapManager.shadow4);
				break;
			case 5:
				tile.tilemapRef.SetColor(location, TilemapManager.shadow5);
				break;
			case 6:
				tile.tilemapRef.SetColor(location, TilemapManager.shadow6);
				break;
			case 7:
				tile.tilemapRef.SetColor(location, TilemapManager.shadow7);
				break;
			case 11:
				tile.tilemapRef.SetColor(location, TilemapManager.shadowB1);
				break;
			case 12:
				tile.tilemapRef.SetColor(location, TilemapManager.shadowB2);
				break;
			case 13:
				tile.tilemapRef.SetColor(location, TilemapManager.shadowB3);
				break;
			case 14:
				tile.tilemapRef.SetColor(location, TilemapManager.shadowB4);
				break;
			case 15:
				tile.tilemapRef.SetColor(location, TilemapManager.shadowB5);
				break;
			case 16:
				tile.tilemapRef.SetColor(location, TilemapManager.shadowB6);
				break;
			case 17:
				tile.tilemapRef.SetColor(location, TilemapManager.shadowB7);
				break;
			case 50:
				tile.tilemapRef.SetColor(location, Color.white);
				break;
			case 51:
				tile.tilemapRef.SetColor(location, TilemapManager.waterShadow1);
				break;
			case 52:
				tile.tilemapRef.SetColor(location, TilemapManager.waterShadow2);
				break;
			case 53:
				tile.tilemapRef.SetColor(location, TilemapManager.waterShadow3);
				break;
			case 54:
				tile.tilemapRef.SetColor(location, TilemapManager.waterShadow4);
				break;
			case 55:
				tile.tilemapRef.SetColor(location, TilemapManager.waterShadow5);
				break;
			}
		}
		else
		{
			switch (tile.floatColour)
			{
			case 55:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColRed);
				break;
			case 56:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColGreen);
				break;
			case 57:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColBlue);
				break;
			case 58:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColTan);
				break;
			case 59:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColGrey);
				break;
			case 60:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColBrown);
				break;
			case 61:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColBlack);
				break;
			case 62:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColGrey);
				break;
			case 63:
				tile.tilemapRef.SetColor(location, TilemapManager.tileColBlueFaint);
				break;
			}
		}
	}

	public override void GetTileData(Vector3Int location, ITilemap iTilemap, ref TileData tileData)
	{
		GameMapTile mapTile = GameMap.instance.getMapTile(location.x, location.y);
		if (mapTile == null)
		{
			return;
		}
		float height = mapTile.height;
		bool flag = mapTile.isRefreshBitSet(location.z);
		if (location.z == 1)
		{
			float landHeight = GameMap.instance.getLandHeight(location.x, location.y - 1);
			if ((GameMap.instance.getLandHeight(location.x - 1, location.y) >= height && landHeight >= height) || mapTile.chevronImage == null)
			{
				tileData.sprite = null;
				if (flag)
				{
					mapTile.clearRefreshBit(location.z);
				}
				return;
			}
			mapTile.tilemapRef.SetTransformMatrix(location, Matrix4x4.TRS(new Vector3(-0.005f, height - mapTile.chevheightdiff, 0f), Quaternion.Euler(0f, 0f, 0f), new Vector3(1.01f, 1f, 1f)));
			if (!flag)
			{
				mapTile.setRefreshBit(location.z);
				TilemapManager.instance.changes++;
				TilemapManager.instance.changedLocations.Add(location);
			}
			tileData.sprite = mapTile.chevronImage;
			int light = mapTile.light;
			if (mapTile.chevronLight >= 0)
			{
				light = mapTile.chevronLight;
			}
			setTileColour(mapTile, location, light);
		}
		else
		{
			if (location.z != 0)
			{
				return;
			}
			if (mapTile.tileImage == null && mapTile.debugTileImage == null)
			{
				tileData.sprite = null;
				if (flag)
				{
					mapTile.clearRefreshBit(location.z);
				}
			}
			else
			{
				mapTile.tilemapRef.SetTransformMatrix(location, Matrix4x4.TRS(new Vector3(0f, height, 0f), Quaternion.Euler(0f, 0f, 0f), Vector3.one));
				bool flag2 = false;
				if (mapTile.debugTileImage != null)
				{
					tileData.sprite = mapTile.debugTileImage;
				}
				else
				{
					tileData.sprite = mapTile.tileImage;
					if (mapTile.mouseOver)
					{
						mapTile.tilemapRef.SetColor(location, TilemapManager.tileColRed);
						flag2 = true;
					}
				}
				if (!flag)
				{
					mapTile.setRefreshBit(location.z);
					TilemapManager.instance.changes++;
					TilemapManager.instance.changedLocations.Add(location);
				}
				if (!flag2)
				{
					setTileColour(mapTile, location, mapTile.light);
				}
			}
			mapTile.mirrorTileImage = tileData.sprite;
		}
	}
}
