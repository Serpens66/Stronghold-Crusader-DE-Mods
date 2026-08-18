using UnityEngine;
using UnityEngine.Tilemaps;

public class gameTile : Tile
{
	public void setTileColour(GameMapTile tile, Vector3Int location, int light)
	{
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		GameMapTile mapTile = GameMap.instance.getMapTile(((Vector3Int)(ref location)).x, ((Vector3Int)(ref location)).y);
		if (mapTile == null)
		{
			return;
		}
		float height = mapTile.height;
		bool flag = mapTile.isRefreshBitSet(((Vector3Int)(ref location)).z);
		if (((Vector3Int)(ref location)).z == 1)
		{
			float landHeight = GameMap.instance.getLandHeight(((Vector3Int)(ref location)).x, ((Vector3Int)(ref location)).y - 1);
			if ((GameMap.instance.getLandHeight(((Vector3Int)(ref location)).x - 1, ((Vector3Int)(ref location)).y) >= height && landHeight >= height) || (Object)(object)mapTile.chevronImage == (Object)null)
			{
				((TileData)(ref tileData)).sprite = null;
				if (flag)
				{
					mapTile.clearRefreshBit(((Vector3Int)(ref location)).z);
				}
				return;
			}
			mapTile.tilemapRef.SetTransformMatrix(location, Matrix4x4.TRS(new Vector3(-0.005f, height - mapTile.chevheightdiff, 0f), Quaternion.Euler(0f, 0f, 0f), new Vector3(1.01f, 1f, 1f)));
			if (!flag)
			{
				mapTile.setRefreshBit(((Vector3Int)(ref location)).z);
				TilemapManager.instance.changes++;
				TilemapManager.instance.changedLocations.Add(location);
			}
			((TileData)(ref tileData)).sprite = mapTile.chevronImage;
			int light = mapTile.light;
			if (mapTile.chevronLight >= 0)
			{
				light = mapTile.chevronLight;
			}
			setTileColour(mapTile, location, light);
		}
		else
		{
			if (((Vector3Int)(ref location)).z != 0)
			{
				return;
			}
			if ((Object)(object)mapTile.tileImage == (Object)null && (Object)(object)mapTile.debugTileImage == (Object)null)
			{
				((TileData)(ref tileData)).sprite = null;
				if (flag)
				{
					mapTile.clearRefreshBit(((Vector3Int)(ref location)).z);
				}
			}
			else
			{
				mapTile.tilemapRef.SetTransformMatrix(location, Matrix4x4.TRS(new Vector3(0f, height, 0f), Quaternion.Euler(0f, 0f, 0f), Vector3.one));
				bool flag2 = false;
				if ((Object)(object)mapTile.debugTileImage != (Object)null)
				{
					((TileData)(ref tileData)).sprite = mapTile.debugTileImage;
				}
				else
				{
					((TileData)(ref tileData)).sprite = mapTile.tileImage;
					if (mapTile.mouseOver)
					{
						mapTile.tilemapRef.SetColor(location, TilemapManager.tileColRed);
						flag2 = true;
					}
				}
				if (!flag)
				{
					mapTile.setRefreshBit(((Vector3Int)(ref location)).z);
					TilemapManager.instance.changes++;
					TilemapManager.instance.changedLocations.Add(location);
				}
				if (!flag2)
				{
					setTileColour(mapTile, location, mapTile.light);
				}
			}
			mapTile.mirrorTileImage = ((TileData)(ref tileData)).sprite;
		}
	}
}
