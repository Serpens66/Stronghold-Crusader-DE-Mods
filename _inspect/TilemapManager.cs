using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapManager : MonoBehaviour
{
	public static TilemapManager instance = null;

	public int TMWidth = 800;

	public int TMHeight = 800;

	public Tilemap gameTileMap;

	public gameTile GTile;

	public const int NUM_DIRTY_TILE_ROWS = 801;

	[HideInInspector]
	public Tilemap[] rowTileMaps;

	public bool[] rowTileMapsVisible = new bool[801];

	[HideInInspector]
	public Stack<Tilemap> spareTilemaps = new Stack<Tilemap>();

	public static readonly Color shadow1 = new Color(0.9f, 0.9f, 0.9f);

	public static readonly Color shadow2 = new Color(0.8f, 0.8f, 0.8f);

	public static readonly Color shadow3 = new Color(0.7f, 0.7f, 0.7f);

	public static readonly Color shadow4 = new Color(0.6f, 0.6f, 0.6f);

	public static readonly Color shadow5 = new Color(0.5f, 0.5f, 0.5f);

	public static readonly Color shadow6 = new Color(0.4f, 0.4f, 0.4f);

	public static readonly Color shadow7 = new Color(0.3f, 0.3f, 0.3f);

	public static readonly Color shadowB1 = new Color(0.94f, 0.94f, 0.94f);

	public static readonly Color shadowB2 = new Color(0.88f, 0.88f, 0.88f);

	public static readonly Color shadowB3 = new Color(0.82f, 0.82f, 0.82f);

	public static readonly Color shadowB4 = new Color(0.76f, 0.76f, 0.76f);

	public static readonly Color shadowB5 = new Color(0.7f, 0.7f, 0.7f);

	public static readonly Color shadowB6 = new Color(0.64f, 0.64f, 0.64f);

	public static readonly Color shadowB7 = new Color(0.58f, 0.58f, 0.58f);

	public static readonly Color waterShadow1 = new Color(0.95f, 0.95f, 0.95f);

	public static readonly Color waterShadow2 = new Color(0.9f, 0.9f, 0.9f);

	public static readonly Color waterShadow3 = new Color(0.85f, 0.85f, 0.85f);

	public static readonly Color waterShadow4 = new Color(0.8f, 0.8f, 0.8f);

	public static readonly Color waterShadow5 = new Color(0.85f, 0.85f, 0.85f);

	public static readonly Color tileColRed = new Color(83f / 85f, 0.3019608f, 0.003921569f);

	public static readonly Color tileColGreen = new Color(0.23137255f, 0.64705884f, 1f / 15f);

	public static readonly Color tileColBlue = new Color(29f / 85f, 28f / 85f, 0.8784314f);

	public static readonly Color tileColTan = new Color(0.8980392f, 0.85490197f, 0.49803922f);

	public static readonly Color tileColGrey = new Color(0.5019608f, 0.5019608f, 0.5019608f);

	public static readonly Color tileColBrown = new Color(56f / 85f, 23f / 85f, 2f / 15f);

	public static readonly Color tileColWhite = new Color(1.1764706f, 1.1764706f, 1.1764706f);

	public static readonly Color tileColBlack = new Color(0.2509804f, 0.2509804f, 0.2509804f);

	public static readonly Color tileColBlueFaint = new Color(0.8235294f, 0.8235294f, 1f);

	[HideInInspector]
	public List<Vector3Int> changedLocations = new List<Vector3Int>();

	public int[,] dirtyTiles = new int[900, 900];

	[HideInInspector]
	public int changes;

	public int leftBounds;

	public int rightBounds;

	public int topBounds;

	public int bottomBounds;

	public void Awake()
	{
		instance = this;
		if ((Object)(object)instance == (Object)null)
		{
			instance = this;
		}
		else if ((Object)(object)instance != (Object)(object)this)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}

	public void Start()
	{
		GTile = ScriptableObject.CreateInstance<gameTile>();
	}

	public void ClearTilemap()
	{
		if (rowTileMaps != null)
		{
			int num = rowTileMaps.Length;
			gameTileMap.ClearAllTiles();
			for (int i = 1; i < num; i++)
			{
				rowTileMaps[i].ClearAllTiles();
			}
		}
	}

	public void ResetTilemap()
	{
		int num = 0;
		if (rowTileMaps != null)
		{
			num = rowTileMaps.Length;
			gameTileMap.ClearAllTiles();
			for (int i = 1; i < num; i++)
			{
				rowTileMaps[i].ClearAllTiles();
			}
		}
		if (num < GameMap.tilemapSize + 1)
		{
			Tilemap[] array = (Tilemap[])(object)new Tilemap[GameMap.tilemapSize + 1];
			if (num > 0)
			{
				for (int j = 0; j < num; j++)
				{
					array[j] = rowTileMaps[j];
				}
			}
			else
			{
				num = 1;
				array[0] = gameTileMap;
			}
			for (int k = num; k < GameMap.tilemapSize + 1; k++)
			{
				if (spareTilemaps.Count == 0)
				{
					array[k] = Object.Instantiate<Tilemap>(gameTileMap);
					((Component)array[k]).transform.parent = ((Component)gameTileMap).transform.parent;
				}
				else
				{
					array[k] = spareTilemaps.Pop();
					((Component)array[k]).transform.parent = ((Component)gameTileMap).transform.parent;
					((Component)array[k]).gameObject.SetActive(true);
				}
			}
			rowTileMaps = array;
		}
		else if (num > GameMap.tilemapSize + 1)
		{
			Tilemap[] array2 = (Tilemap[])(object)new Tilemap[GameMap.tilemapSize + 1];
			for (int l = 0; l < GameMap.tilemapSize + 1; l++)
			{
				array2[l] = rowTileMaps[l];
			}
			for (int m = GameMap.tilemapSize + 1; m < num; m++)
			{
				((Component)rowTileMaps[m]).gameObject.SetActive(false);
				((Component)rowTileMaps[m]).gameObject.transform.parent = null;
				spareTilemaps.Push(rowTileMaps[m]);
			}
			rowTileMaps = array2;
		}
		clearAllDirty();
		changedLocations.Clear();
		for (int n = 0; n < 801 && n < rowTileMaps.Length; n++)
		{
			rowTileMapsVisible[n] = ((Component)rowTileMaps[n]).gameObject.activeSelf;
		}
	}

	public void GenerateTileMaps()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		ResetTilemap();
		for (int i = 0; i < TMWidth; i++)
		{
			for (int j = 0; j < TMHeight; j++)
			{
				GameMapTile mapTile = GameMap.instance.getMapTile(i, j);
				if (mapTile != null)
				{
					mapTile.tilemapRef = rowTileMaps[mapTile.row];
					mapTile.tilemapRef.SetTile(new Vector3Int(i, j, 1), (TileBase)(object)GTile);
					TilemapRenderer component = ((Component)mapTile.tilemapRef).GetComponent<TilemapRenderer>();
					((Renderer)component).sortingOrder = -20000 + mapTile.row * 49;
					((Renderer)component).sharedMaterial = spriteLoader.instance.plainMaterials[0];
					component.chunkSize = new Vector3Int(400, 400, 2);
					mapTile.tilemapRef.SetTile(new Vector3Int(i, j, 0), (TileBase)(object)GTile);
				}
			}
		}
		((Component)gameTileMap).gameObject.SetActive(true);
	}

	public int NumDirtyTiles()
	{
		return 0;
	}

	public void clearAllDirty()
	{
		for (int i = 0; i < 900; i++)
		{
			for (int j = 0; j < 900; j++)
			{
				dirtyTiles[i, j] = 0;
			}
		}
	}

	public void startTileRefresh()
	{
		changedLocations.Clear();
	}

	public void endTileRefresh(bool noGameTick = false)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		changedLocations.Clear();
		if (dirtyTiles == null)
		{
			return;
		}
		Vector3Int val = default(Vector3Int);
		_ = GameMap.instance.cyclicRowUpdater;
		if (noGameTick)
		{
			GameMap.instance.cyclicRowUpdater += 5;
		}
		else
		{
			GameMap.instance.cyclicRowUpdater += 2;
		}
		_ = GameMap.instance.cyclicRowUpdater;
		if (GameMap.instance.cyclicRowUpdater >= GameMap.tilemapSize)
		{
			GameMap.instance.cyclicRowUpdater = 0;
		}
		for (int i = 0; i <= GameMap.tilemapSize; i++)
		{
			if (!rowTileMapsVisible[i])
			{
				continue;
			}
			for (int j = leftBounds; j <= rightBounds; j++)
			{
				if (dirtyTiles[i, j] == 0)
				{
					continue;
				}
				int x = dirtyTiles[i, j] % 1000;
				int y = dirtyTiles[i, j] / 1000;
				((Vector3Int)(ref val)).x = x;
				((Vector3Int)(ref val)).y = y;
				GameMapTile mapTile = GameMap.instance.getMapTile(x, y);
				if (mapTile != null)
				{
					int num = 1;
					if (!mapTile.chevronChanged)
					{
						num = 0;
					}
					else
					{
						mapTile.chevronChanged = false;
					}
					for (int num2 = num; num2 >= 0; num2--)
					{
						((Vector3Int)(ref val)).z = num2;
						rowTileMaps[i].RefreshTile(val);
					}
					dirtyTiles[i, j] = 0;
				}
			}
		}
		foreach (Vector3Int changedLocation in changedLocations)
		{
			Vector3Int current = changedLocation;
			int row = GameMap.instance.getRow(((Vector3Int)(ref current)).x, ((Vector3Int)(ref current)).y);
			rowTileMaps[row].RefreshTile(current);
		}
	}

	public void triggerTMTileRefresh(Vector2Int location, int row, int column, bool heightDiff = false)
	{
		if (row < 0 || row >= 900)
		{
			Debug.Log((object)("OOB " + row));
		}
		int num = ((Vector2Int)(ref location)).x + ((Vector2Int)(ref location)).y * 1000;
		dirtyTiles[row, column] = num;
		if (heightDiff)
		{
			GameMapTile mapTile = GameMap.instance.getMapTile(((Vector2Int)(ref location)).x + 1, ((Vector2Int)(ref location)).y);
			if (mapTile != null)
			{
				mapTile.chevronChanged = true;
				num = ((Vector2Int)(ref location)).x + 1 + ((Vector2Int)(ref location)).y * 1000;
				dirtyTiles[mapTile.row, mapTile.column] = num;
			}
			mapTile = GameMap.instance.getMapTile(((Vector2Int)(ref location)).x, ((Vector2Int)(ref location)).y + 1);
			if (mapTile != null)
			{
				mapTile.chevronChanged = true;
				num = ((Vector2Int)(ref location)).x + (((Vector2Int)(ref location)).y + 1) * 1000;
				dirtyTiles[mapTile.row, mapTile.column] = num;
			}
		}
	}

	public void triggerTMFullRefresh()
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		changedLocations.Clear();
		changes = 0;
		for (int i = 1; i < GameMap.tilemapSize + 1 && i < rowTileMaps.Length; i++)
		{
			rowTileMaps[i].RefreshAllTiles();
		}
		foreach (Vector3Int changedLocation in changedLocations)
		{
			Vector3Int current = changedLocation;
			int row = GameMap.instance.getRow(((Vector3Int)(ref current)).x, ((Vector3Int)(ref current)).y);
			rowTileMaps[row].RefreshTile(current);
		}
		clearAllDirty();
	}

	public bool IsTileRendered(int x, int y)
	{
		if (x < leftBounds)
		{
			return false;
		}
		if (x > rightBounds)
		{
			return false;
		}
		if (y < topBounds)
		{
			return false;
		}
		if (y > bottomBounds)
		{
			return false;
		}
		return true;
	}

	public void filterRows(int topRow, int bottowRow, int leftCol, int rightCol, List<bool> extraRows)
	{
		leftBounds = leftCol;
		rightBounds = rightCol;
		topBounds = topRow;
		bottomBounds = bottowRow;
		int i;
		for (i = 1; i < topRow && i < GameMap.tilemapSize + 1 && i < rowTileMaps.Length; i++)
		{
			if (rowTileMapsVisible[i])
			{
				((Component)rowTileMaps[i]).gameObject.SetActive(false);
				rowTileMapsVisible[i] = false;
			}
		}
		for (; i <= bottowRow && i < GameMap.tilemapSize + 1 && i < rowTileMaps.Length; i++)
		{
			if (!rowTileMapsVisible[i])
			{
				((Component)rowTileMaps[i]).gameObject.SetActive(true);
				rowTileMapsVisible[i] = true;
			}
		}
		if (extraRows != null)
		{
			for (int j = 0; j < extraRows.Count; j++)
			{
				if (i >= GameMap.tilemapSize + 1)
				{
					break;
				}
				if (i >= rowTileMaps.Length)
				{
					break;
				}
				if (!extraRows[j])
				{
					if (rowTileMapsVisible[i])
					{
						((Component)rowTileMaps[i]).gameObject.SetActive(false);
						rowTileMapsVisible[i] = false;
					}
				}
				else if (!rowTileMapsVisible[i])
				{
					((Component)rowTileMaps[i]).gameObject.SetActive(true);
					rowTileMapsVisible[i] = true;
				}
				i++;
			}
		}
		for (; i < GameMap.tilemapSize + 1 && i < rowTileMaps.Length; i++)
		{
			if (rowTileMapsVisible[i])
			{
				((Component)rowTileMaps[i]).gameObject.SetActive(false);
				rowTileMapsVisible[i] = false;
			}
		}
	}

	public void optimiseTilemaps()
	{
		for (int i = 1; i < GameMap.tilemapSize + 1 && i < rowTileMaps.Length; i++)
		{
			rowTileMaps[i].CompressBounds();
		}
	}

	public void translateTileToScreenCoords(int xPos, int yPos, int size, ref float ssXPos, ref float ssYPos)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		Vector3Int val = default(Vector3Int);
		((Vector3Int)(ref val))._002Ector(xPos, yPos, 0);
		Vector3 cellCenterWorld = gameTileMap.GetCellCenterWorld(val);
		ssXPos = cellCenterWorld.x;
		ssYPos = cellCenterWorld.y;
		if (size > 1)
		{
			ssYPos += (float)(size - 1) * 0.25f;
		}
	}
}
