using UnityEngine;
using UnityEngine.Tilemaps;

public class GameMapTile
{
	public int row;

	public int column;

	private int refreshInfo;

	public int light;

	public int chevronLight;

	public int floatColour;

	public float height;

	public float buildingHeight;

	public float testHeight;

	public float chevheightdiff;

	public int gameMapX;

	public int gameMapY;

	public int org = -1;

	public int lastChevronFile = -1;

	public int lastChevronImage = -1;

	public bool chevronChanged = true;

	public Sprite chevronImage;

	public Sprite tileImage;

	public Sprite debugTileImage;

	public Tilemap tilemapRef;

	public Sprite constructionOrigImage;

	public Sprite mirrorTileImage;

	public bool mouseOver;

	public void setRefreshBit(int layer)
	{
		refreshInfo |= 1 << layer;
	}

	public void clearRefreshBit(int layer)
	{
		refreshInfo &= ~(1 << layer);
	}

	public void wipeRefreshBitmask()
	{
		refreshInfo = 0;
	}

	public bool isRefreshBitSet(int layer)
	{
		return (refreshInfo & (1 << layer)) != 0;
	}
}
