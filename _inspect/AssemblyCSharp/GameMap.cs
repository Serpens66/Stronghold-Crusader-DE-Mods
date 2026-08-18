using System;
using System.Collections.Generic;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class GameMap : MonoBehaviour
{
	public const int RAW_MAP_SIZE = 800;

	public static GameMap instance = null;

	public static int tilemapSize = 800;

	public const int SUB_SPECIALOFSET = 1000000;

	public const int SPRITEPOOL_ID_ORG = 1073741824;

	public const int SPRITEPOOL_ID_WALL = 536870912;

	public const int SPRITEPOOL_ID_BUILDING_PIECE = 268435456;

	public const int SPRITEPOOL_ID_CHIMP = 134217728;

	public const int SPRITEPOOL_ID_FLY = 67108864;

	private GameMapTile[,] gameMap;

	private int[,] pixelHeightMap = new int[800, 800];

	private readonly Dictionary<int, Chimp> chimps = new Dictionary<int, Chimp>();

	private readonly Dictionary<int, Fly> flies = new Dictionary<int, Fly>();

	private readonly Dictionary<int, Org> orgs = new Dictionary<int, Org>();

	private readonly Dictionary<int, BuildingAnim> buildingAnims = new Dictionary<int, BuildingAnim>();

	private readonly Dictionary<int, WallFillin> wallFillins = new Dictionary<int, WallFillin>();

	private readonly Dictionary<int, Pixie> pixies = new Dictionary<int, Pixie>();

	private GameObject mouseCursorGO;

	private GameObject mouseCursorGO2;

	private int mouseXOffset;

	private int mouseYOffset;

	private int debugLayerRendering = -1;

	private byte[] debugLayerValues = new byte[640000];

	private int[] debugMapping;

	private const int RADAR_MIN_SIZE = 124;

	private int RADAR_TEXTURE_SIZE = 124;

	private const int RADARPREVIEW_TEXTURE_SIZE = 200;

	private Texture2D radarTexture;

	private Texture2D radarPreviewTexture;

	[HideInInspector]
	public int cyclicRowUpdater;

	private bool UIVisible = true;

	private const int INPUTCOLUMN_TYPE = 0;

	private const int INPUTCOLUMN_X = 1;

	private const int INPUTCOLUMN_Y = 2;

	private const int INPUTCOLUMN_FILE_OR_TYPE = 3;

	private const int INPUTCOLUMN_IMAGE_OR_STATE = 4;

	private const int INPUTCOLUMN_HI = 5;

	private const int INPUTCOLUMN_LUM = 6;

	private const int INPUTCOLUMN_COLOUR = 7;

	private const int INPUTCOLUMN_TILEX = 8;

	private const int INPUTCOLUMN_TILEY = 9;

	private const int INPUTCOLUMN_OBJID = 10;

	private const int INPUTCOLUMN_CHEVRON_FILE1_OR_TRANS = 11;

	private const int INPUTCOLUMN_CHEVRON_IMAGE1_OR_LAYER_DELAY = 12;

	private const int INPUTCOLUMN_CHEVRON_FILE2 = 13;

	private const int INPUTCOLUMN_CHEVRON_IMAGE2 = 14;

	private const int INPUTCOLUMN_ORIG_FILE = 15;

	private const int INPUTCOLUMN_ORIG_IMAGE = 16;

	private const int INPUTCOLUMN_SPRITE_OFFSET_1 = 17;

	private const int INPUTCOLUMN_SPRITE_OFFSET_2 = 18;

	private const int INPUTCOLUMN_SPRITE_OFFSET_3 = 19;

	private const int INPUTCOLUMN_GAME_OBJ_TYPE = 20;

	public const int NUM_ARRAY_ELEMENTS = 21;

	private byte[] lastRadarMap;

	private Action postUpdateFunction;

	private readonly int[][] siegeTowerRotations = new int[64][]
	{
		new int[1] { 1 },
		new int[4] { 2, 3, 4, 5 },
		new int[8] { 2, 3, 4, 5, 6, 7, 8, 9 },
		new int[12]
		{
			2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
			12, 13
		},
		new int[16]
		{
			2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
			12, 13, 14, 15, 16, 17
		},
		new int[12]
		{
			32, 31, 30, 29, 28, 27, 26, 25, 24, 23,
			22, 21
		},
		new int[8] { 32, 31, 30, 29, 28, 27, 26, 25 },
		new int[4] { 32, 31, 30, 29 },
		new int[4] { 4, 3, 2, 1 },
		new int[1] { 5 },
		new int[4] { 6, 7, 8, 9 },
		new int[8] { 6, 7, 8, 9, 10, 11, 12, 13 },
		new int[12]
		{
			6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
			16, 17
		},
		new int[16]
		{
			6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
			16, 17, 18, 19, 20, 21
		},
		new int[12]
		{
			4, 3, 2, 1, 32, 31, 30, 29, 28, 27,
			26, 25
		},
		new int[8] { 4, 3, 2, 1, 32, 31, 30, 29 },
		new int[8] { 8, 7, 6, 5, 4, 3, 2, 1 },
		new int[4] { 8, 7, 6, 5 },
		new int[1] { 9 },
		new int[4] { 10, 11, 12, 13 },
		new int[8] { 10, 11, 12, 13, 14, 15, 16, 17 },
		new int[12]
		{
			10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
			20, 21
		},
		new int[16]
		{
			10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
			20, 21, 22, 23, 24, 25
		},
		new int[12]
		{
			8, 7, 6, 5, 4, 3, 2, 1, 32, 31,
			30, 29
		},
		new int[12]
		{
			12, 11, 10, 9, 8, 7, 6, 5, 4, 3,
			2, 1
		},
		new int[8] { 12, 11, 10, 9, 8, 7, 6, 5 },
		new int[4] { 12, 11, 10, 9 },
		new int[1] { 13 },
		new int[4] { 14, 15, 16, 17 },
		new int[8] { 14, 15, 16, 17, 18, 19, 20, 21 },
		new int[12]
		{
			14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
			24, 25
		},
		new int[16]
		{
			14, 15, 16, 17, 18, 19, 20, 21, 22, 23,
			24, 25, 26, 27, 28, 29
		},
		new int[16]
		{
			18, 19, 20, 21, 22, 23, 24, 25, 26, 27,
			28, 29, 30, 31, 32, 1
		},
		new int[12]
		{
			16, 15, 14, 13, 12, 11, 10, 9, 8, 7,
			6, 5
		},
		new int[8] { 16, 15, 14, 13, 12, 11, 10, 9 },
		new int[4] { 16, 15, 14, 13 },
		new int[1] { 17 },
		new int[4] { 18, 19, 20, 21 },
		new int[8] { 18, 19, 20, 21, 22, 23, 24, 25 },
		new int[12]
		{
			18, 19, 20, 21, 22, 23, 24, 25, 26, 27,
			28, 29
		},
		new int[12]
		{
			22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
			32, 1
		},
		new int[16]
		{
			22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
			32, 1, 2, 3, 4, 5
		},
		new int[12]
		{
			20, 19, 18, 17, 16, 15, 14, 13, 12, 11,
			10, 9
		},
		new int[8] { 20, 19, 18, 17, 16, 15, 14, 13 },
		new int[4] { 20, 19, 18, 17 },
		new int[1] { 21 },
		new int[4] { 22, 23, 24, 25 },
		new int[8] { 22, 23, 24, 25, 26, 27, 28, 29 },
		new int[8] { 26, 27, 28, 29, 30, 31, 32, 1 },
		new int[12]
		{
			26, 27, 28, 29, 30, 31, 32, 1, 2, 3,
			4, 5
		},
		new int[16]
		{
			26, 27, 28, 29, 30, 31, 32, 1, 2, 3,
			4, 5, 6, 7, 8, 9
		},
		new int[12]
		{
			24, 23, 22, 21, 20, 19, 18, 17, 16, 15,
			14, 13
		},
		new int[8] { 24, 23, 22, 21, 20, 19, 18, 17 },
		new int[4] { 24, 23, 22, 21 },
		new int[1] { 25 },
		new int[4] { 26, 27, 28, 29 },
		new int[4] { 30, 31, 32, 1 },
		new int[8] { 30, 31, 32, 1, 2, 3, 4, 5 },
		new int[12]
		{
			30, 31, 32, 1, 2, 3, 4, 5, 6, 7,
			8, 9
		},
		new int[16]
		{
			30, 31, 32, 1, 2, 3, 4, 5, 6, 7,
			8, 9, 10, 11, 12, 13
		},
		new int[12]
		{
			28, 27, 26, 25, 24, 23, 22, 21, 20, 19,
			18, 17
		},
		new int[8] { 28, 27, 26, 25, 24, 23, 22, 21 },
		new int[4] { 28, 27, 26, 25 },
		new int[1] { 29 }
	};

	private Vector3 cachecCentreMapVector = Vector3.zero;

	private Vector3Int cachecCentreTileMapVector = Vector3Int.zero;

	private int screenCentreTileScreenSpaceX = -1;

	private int screenCentreTileScreenSpaceY = -1;

	private int screenCentreTileX = -1;

	private int screenCentreTileY = -1;

	private int screenTilesWide = 1;

	private int screenTilesHigh = 1;

	private int radarMapWidth = 124;

	private int radarMapHeight = 124;

	private int radarZoom = 1;

	private int screenZoom = 1;

	private Enums.Dircs currentRotation = Enums.Dircs.North;

	private Enums.Dircs pendingRotation = Enums.Dircs.North;

	public bool pendingRotationtriggered;

	private bool healthbarsTopMost;

	[HideInInspector]
	public float lastMouseLandscapeHeight = 8f;

	[HideInInspector]
	public bool overTopHalf = true;

	[HideInInspector]
	public int debugGrabbedChimps;

	private float[,] body_actual_dimensions = new float[90, 3]
	{
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 30f, 62f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 75f, 55f, 6f },
		{ 75f, 55f, 24f },
		{ 75f, 55f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 32f, 0f },
		{ 34f, 36f, 0f },
		{ 18f, 12f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 75f, 55f, 0f },
		{ 75f, 55f, 0f },
		{ 26f, 12f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 80f, 135f, 18f },
		{ 75f, 55f, 24f },
		{ 75f, 55f, 8f },
		{ 75f, 55f, 0f },
		{ 26f, 12f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 30f, 62f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 75f, 52f, 24f },
		{ 30f, 62f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 30f, 62f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f },
		{ 26f, 42f, 0f }
	};

	public int cachedRenderBoundsLeft;

	public int cachedRenderBoundsRight;

	public int cachedRenderBoundsTop;

	public int cachedRenderBoundsBottom;

	public bool ignoreNextCachedBounds;

	public int DebugLayerRendering => debugLayerRendering;

	public int ScreenCentreTileScreenSpaceX => screenCentreTileScreenSpaceX;

	public int ScreenCentreTileScreenSpaceY => screenCentreTileScreenSpaceY;

	public int ScreenCentreTileX => screenCentreTileX;

	public int ScreenCentreTileY => screenCentreTileY;

	public int ScreenTilesWide => screenTilesWide;

	public int ScreenTilesHigh => screenTilesHigh;

	public int RadarMapWidth => radarMapWidth;

	public int RadarMapHeight => radarMapHeight;

	public int RadarZoom => radarZoom;

	public int ScreenZoom => screenZoom;

	private void Awake()
	{
		instance = this;
		if (instance == null)
		{
			instance = this;
		}
		else if (instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		MemoryBuffers.instance.GenerateRadarBuffers(800);
		createMap();
		CreateRadarTextureAndSprite();
		MainViewModel.Instance.Hide_Loading_Screen = true;
		Platform_Multiplayer.Instance.HandleCommandline();
		Noesis.GUI.SetCursorCallback(null);
		if (!ConfigSettings.Settings_SkipIntro)
		{
			MainViewModel.Instance.Intro_Sequence.InitVideos();
		}
		if (Platform_Multiplayer.Instance.PendingMPLobby)
		{
			spriteLoader.instance.loadSprites();
			MainViewModel.Instance.Intro_Sequence.ForceStopVideo();
			FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
			if (Platform_Multiplayer.Instance.PendingMPLobby)
			{
				Platform_Multiplayer.Instance.PendingMPLobby_DelayedMPEnter = true;
			}
		}
		else
		{
			FatControler.instance.NewScene(Enums.SceneIDS.Intro);
		}
	}

	public void CreateRadarTextureAndSprite()
	{
		if (radarTexture == null)
		{
			radarTexture = new Texture2D(RADAR_TEXTURE_SIZE, RADAR_TEXTURE_SIZE, UnityEngine.TextureFormat.BGRA32, mipChain: false);
			radarTexture.filterMode = FilterMode.Point;
		}
		if (radarPreviewTexture == null)
		{
			radarPreviewTexture = new Texture2D(200, 200, UnityEngine.TextureFormat.BGRA32, mipChain: false);
			radarPreviewTexture.filterMode = FilterMode.Point;
		}
		wipeRadar();
	}

	private int getMaxRadarSize()
	{
		if (tilemapSize > 800)
		{
			return 800;
		}
		return tilemapSize - 4;
	}

	public void resetRadarZoom()
	{
		if (ConfigSettings.Settings_RadarDefaultZoomedOut)
		{
			int maxRadarSize = getMaxRadarSize();
			if (RADAR_TEXTURE_SIZE != maxRadarSize)
			{
				SetRadarTexturePixelSize(maxRadarSize);
			}
		}
		else if (RADAR_TEXTURE_SIZE != 124)
		{
			SetRadarTexturePixelSize(124);
		}
	}

	public void capRadarZoom(int size)
	{
		if (RADAR_TEXTURE_SIZE > size - 4)
		{
			SetRadarTexturePixelSize(size - 4);
		}
	}

	public void changeRadarMapSize(int diff)
	{
		int maxRadarSize = getMaxRadarSize();
		int num = RADAR_TEXTURE_SIZE + diff;
		if (num < 124)
		{
			num = 124;
		}
		else if (num > maxRadarSize)
		{
			num = maxRadarSize;
		}
		if (num != RADAR_TEXTURE_SIZE)
		{
			SetRadarTexturePixelSize(num);
		}
	}

	public void wipeRadar()
	{
		if (radarTexture != null)
		{
			byte[] data = new byte[RADAR_TEXTURE_SIZE * RADAR_TEXTURE_SIZE * 4];
			radarTexture.SetPixelData(data, 0);
			radarTexture.Apply();
		}
	}

	public void SetUIVisibleState(bool state)
	{
		UIVisible = state;
	}

	public void SetRadarTexturePixelSize(int size)
	{
		if (radarTexture != null)
		{
			RADAR_TEXTURE_SIZE = (radarMapWidth = (radarMapHeight = size));
			EngineInterface.GameAction(Enums.GameActionCommand.ResizeRadarMapBuffer, RADAR_TEXTURE_SIZE, RADAR_TEXTURE_SIZE);
			radarTexture = new Texture2D(RADAR_TEXTURE_SIZE, RADAR_TEXTURE_SIZE, UnityEngine.TextureFormat.BGRA32, mipChain: false);
			radarTexture.filterMode = FilterMode.Point;
			float sHRadarScalar = (float)size / 124f;
			FatControler.instance.SHRadarScalar = sHRadarScalar;
		}
	}

	public Texture2D getRadarTexture()
	{
		return radarTexture;
	}

	public void DEBUG_ShowRadarPreview(byte[] radarMapPreview)
	{
		radarPreviewTexture.SetPixelData(radarMapPreview, 0);
		radarPreviewTexture.Apply();
	}

	public void createMap()
	{
		MemoryBuffers.instance.resetBuffers();
		wipeRadar();
		gameMap = new GameMapTile[tilemapSize, tilemapSize];
		for (int i = 0; i < 800; i++)
		{
			for (int j = 0; j < 800; j++)
			{
				pixelHeightMap[i, j] = 0;
			}
		}
		setupMapData();
		clearSprites();
	}

	private void createMouseCursors()
	{
		if (ObjectPool.poolsCreated && mouseCursorGO == null)
		{
			mouseCursorGO = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
			if (mouseCursorGO != null)
			{
				mouseCursorGO.transform.parent = null;
				SpriteRenderer component = mouseCursorGO.GetComponent<SpriteRenderer>();
				component.sortingOrder = 32000;
				component.sprite = null;
				mouseCursorGO.SetActive(value: false);
			}
			mouseCursorGO2 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
			if (mouseCursorGO2 != null)
			{
				mouseCursorGO2.transform.parent = null;
				SpriteRenderer component2 = mouseCursorGO.GetComponent<SpriteRenderer>();
				component2.sortingOrder = 32001;
				component2.sprite = null;
				mouseCursorGO2.SetActive(value: false);
			}
		}
	}

	public void clearSprites()
	{
		if (chimps.Count > 0)
		{
			foreach (KeyValuePair<int, Chimp> chimp in chimps)
			{
				deleteChimp(chimp.Value, removeFromDictionary: false);
			}
			chimps.Clear();
		}
		if (flies.Count > 0)
		{
			foreach (KeyValuePair<int, Fly> fly in flies)
			{
				deleteFly(fly.Value, removeFromDictionary: false);
			}
			flies.Clear();
		}
		if (orgs.Count > 0)
		{
			foreach (KeyValuePair<int, Org> org in orgs)
			{
				deleteOrg(org.Value, removeFromDictionary: false);
			}
			orgs.Clear();
		}
		if (buildingAnims.Count > 0)
		{
			foreach (KeyValuePair<int, BuildingAnim> buildingAnim in buildingAnims)
			{
				deleteBuildingAnim(buildingAnim.Value, removeFromDictionary: false);
			}
			buildingAnims.Clear();
		}
		if (wallFillins.Count > 0)
		{
			foreach (KeyValuePair<int, WallFillin> wallFillin in wallFillins)
			{
				deleteWallFillin(wallFillin.Value, removeFromDictionary: false);
			}
			wallFillins.Clear();
		}
		if (pixies.Count <= 0)
		{
			return;
		}
		foreach (KeyValuePair<int, Pixie> pixy in pixies)
		{
			deletePixie(pixy.Value, removeFromDictionary: false);
		}
		pixies.Clear();
	}

	private void setupMapData()
	{
	}

	public void newMapLoaded(int mapSize)
	{
		currentRotation = Enums.Dircs.North;
		pendingRotationtriggered = false;
		tilemapSize = mapSize;
		createMap();
		setupMapLayers();
		GameData.scenario.reset();
		Director.instance.ResetFrameRate();
		MainViewModel.Instance.resetDiffData();
		resetRadarZoom();
		MainViewModel.Instance.HUDRoot.resetPulse();
	}

	public void changeMapSize(int newSize)
	{
		tilemapSize = newSize;
		createMap();
		setupMapLayers();
		capRadarZoom(newSize);
	}

	public void setupMapLayers()
	{
		createRowsAndTiles();
		TilemapManager.instance.GenerateTileMaps();
		setGameCoordMapping();
	}

	private void setGameCoordMapping()
	{
		for (int i = 0; i < 800; i++)
		{
			for (int j = 0; j < 800; j++)
			{
				mapGameTileToTilemapCoord(j, i, out var tileMapX, out var tileMapY);
				GameMapTile mapTile = getMapTile(tileMapX, tileMapY);
				if (mapTile != null)
				{
					mapTile.gameMapX = j;
					mapTile.gameMapY = i;
					TilemapManager.instance.triggerTMTileRefresh(new Vector2Int(j, i), mapTile.row, mapTile.column, heightDiff: true);
				}
			}
		}
	}

	private void wipeTiles()
	{
		for (int i = 0; i < 800; i++)
		{
			for (int j = 0; j < 800; j++)
			{
				GameMapTile mapTile = getMapTile(j, i);
				if (mapTile != null)
				{
					mapTile.chevronImage = null;
					mapTile.tileImage = null;
					mapTile.wipeRefreshBitmask();
				}
			}
		}
	}

	public void createRowsAndTiles()
	{
		int num = 0;
		int num2 = tilemapSize / 2;
		int num3 = tilemapSize / 2;
		int num4 = 0;
		int num5 = 0;
		for (int i = 0; i < num2; i++)
		{
			int num6 = num;
			int num7 = tilemapSize / 2 - 1;
			for (int j = 0; j < num3; j++)
			{
				GameMapTile gameMapTile = new GameMapTile();
				gameMap[num6 + num4, num7 + num5] = gameMapTile;
				gameMapTile.row = tilemapSize - i * 2;
				gameMapTile.column = j;
				num6++;
				num7--;
			}
			num5++;
			num6 = num;
			num7 = tilemapSize / 2 - 1;
			for (int k = 0; k <= num3; k++)
			{
				GameMapTile gameMapTile2 = new GameMapTile();
				gameMap[num6 + num4, num7 + num5] = gameMapTile2;
				gameMapTile2.row = tilemapSize - (i * 2 + 1);
				gameMapTile2.column = k;
				num6++;
				num7--;
			}
			num4++;
		}
	}

	private void renderDebugMap(bool force = false)
	{
		setupDebugRenderLayerMap();
		byte[] layerData = EngineInterface.GetLayerData(debugLayerRendering);
		for (int i = 0; i < 640000; i++)
		{
			byte b = layerData[i];
			if ((debugLayerValues[i] != b || force) && b < 250)
			{
				debugLayerValues[i] = b;
				if (debugMapping[i] >= 0)
				{
					int x = debugMapping[i] % 800;
					int y = debugMapping[i] / 800;
					GameMapTile mapTile = getMapTile(x, y);
					if (mapTile != null)
					{
						SpriteMapping.setDebugTileGraphic(mapTile, b);
						TilemapManager.instance.triggerTMTileRefresh(new Vector2Int(x, y), mapTile.row, mapTile.column);
					}
				}
				else if (b == 254)
				{
					int x2 = debugMapping[i] % 800;
					int y2 = debugMapping[i] / 800;
					GameMapTile mapTile2 = getMapTile(x2, y2);
					if (mapTile2 != null)
					{
						SpriteMapping.setDebugTileGraphic(mapTile2, -1);
					}
				}
			}
			else if (b == 254)
			{
				int x3 = debugMapping[i] % 800;
				int y3 = debugMapping[i] / 800;
				GameMapTile mapTile3 = getMapTile(x3, y3);
				if (mapTile3 != null)
				{
					SpriteMapping.setDebugTileGraphic(mapTile3, -1);
				}
			}
		}
	}

	public void setupDebugRenderLayerMap(bool force = false)
	{
		if (!(debugMapping == null || force))
		{
			return;
		}
		if (debugMapping == null)
		{
			debugMapping = new int[640000];
		}
		for (int i = 0; i < tilemapSize; i++)
		{
			for (int j = 0; j < tilemapSize; j++)
			{
				if (gameMap[j, i] != null)
				{
					debugMapping[gameMap[j, i].gameMapX + gameMap[j, i].gameMapY * 800] = j + i * 800;
				}
			}
		}
	}

	public void setDebugRendering(int mode, bool force = false)
	{
		if (mode == debugLayerRendering && !force)
		{
			return;
		}
		setupDebugRenderLayerMap();
		for (int i = 0; i < 640000; i++)
		{
			debugLayerValues[i] = 254;
		}
		debugLayerRendering = mode;
		for (int j = 0; j < 640000; j++)
		{
			int x = debugMapping[j] % 800;
			int y = debugMapping[j] / 800;
			GameMapTile mapTile = getMapTile(x, y);
			if (mapTile != null)
			{
				SpriteMapping.setDebugTileGraphic(mapTile, -1);
			}
		}
		if (mode >= 0)
		{
			renderDebugMap(force: true);
		}
		TilemapManager.instance.triggerTMFullRefresh();
	}

	public void processTestMap(short[] sourceMap, int numElements, EngineInterface.PlayState state, byte[] radarMap)
	{
		if (numElements < 0)
		{
			int i;
			for (i = 0; sourceMap[i * 21] >= 0; i++)
			{
			}
			numElements = i;
		}
		GameData.Instance.SetCameraFromGameState(state);
		if (state.rotateHappened > 0 && pendingRotation >= Enums.Dircs.Centre)
		{
			finishPendingRotate();
			GameData.Instance.SetCameraFromGameState(state);
		}
		experimentalRowManager();
		if (debugLayerRendering >= 0)
		{
			renderDebugMap();
		}
		lastRadarMap = radarMap;
		GameData.scenario.reset();
		if (state.flattenedHappened != 0)
		{
			foreach (KeyValuePair<int, Chimp> chimp in chimps)
			{
				chimp.Value.interp = false;
			}
		}
		bool flag = false;
		for (int j = 0; j < numElements; j++)
		{
			mapGameTileToTilemapCoord(sourceMap[j * 21 + 1], sourceMap[j * 21 + 2], out var tileMapX, out var tileMapY);
			int num = sourceMap[j * 21];
			GameMapTile mapTile = getMapTile(tileMapX, tileMapY);
			if (num == 0 && mapTile != null)
			{
				int num2 = sourceMap[j * 21 + 15];
				int image = sourceMap[j * 21 + 16];
				float num3 = (mapTile.testHeight = (float)sourceMap[j * 21 + 5] / 32f);
				if (num2 == 0)
				{
					mapTile.constructionOrigImage = null;
				}
				else
				{
					SpriteMapping.setGenericBuildingTileGraphic(mapTile, num2, image, 0);
					mapTile.constructionOrigImage = mapTile.tileImage;
					if (sourceMap[j * 21 + 17] != 10000)
					{
						float testHeight = (float)sourceMap[j * 21 + 17] / 32f;
						mapTile.testHeight = testHeight;
					}
				}
				int num4 = sourceMap[j * 21 + 3];
				int num5 = sourceMap[j * 21 + 4];
				mapTile.buildingHeight = (float)sourceMap[j * 21 + 18] / 32f;
				bool heightDiff = false;
				if (mapTile.height != num3)
				{
					mapTile.height = num3;
					heightDiff = true;
					mapTile.chevronChanged = true;
				}
				int light = sourceMap[j * 21 + 6];
				int num6 = sourceMap[j * 21 + 7];
				int num7 = sourceMap[j * 21 + 11];
				SpriteMapping.setGenericBuildingTileGraphic(mapTile, num4, num5, light);
				if (mapTile.floatColour != num6)
				{
					mapTile.chevronChanged = true;
				}
				mapTile.floatColour = num6;
				mapTile.chevheightdiff = 0f;
				mapTile.chevronLight = -1;
				if (num7 > 0)
				{
					int num8 = sourceMap[j * 21 + 12];
					int num9 = num7;
					int num10 = num8;
					int num11 = sourceMap[j * 21 + 13];
					if (num11 > 0)
					{
						int num12 = sourceMap[j * 21 + 14];
						if ((num7 != num11 || num8 != num12) && num7 == 10 && num8 >= 64 && num8 < 72 && num4 == 12 && num5 >= 291 && num5 <= 298)
						{
							num9 = num11;
							num10 = num12;
							mapTile.chevheightdiff = 2.484375f;
							SpriteMapping.setGenericBuildingTileGraphic(mapTile, num7, num8 + 9, light);
						}
					}
					if (mapTile.lastChevronFile != num9 || mapTile.lastChevronImage != num10)
					{
						mapTile.chevronImage = SpriteMapping.getChevronImage(mapTile, num9, num10);
						mapTile.lastChevronFile = num9;
						mapTile.lastChevronImage = num10;
						mapTile.chevronChanged = true;
					}
				}
				else if (mapTile.lastChevronFile != -1)
				{
					mapTile.lastChevronFile = -1;
					mapTile.chevronImage = null;
					mapTile.chevronChanged = true;
				}
				if (mapTile.row >= 0 && mapTile.row < 800 && mapTile.column >= 0 && mapTile.column < 800)
				{
					switch (num4)
					{
					case 6:
					case 7:
					case 8:
					case 10:
					case 12:
					case 14:
					case 49:
					case 52:
					case 56:
					case 149:
						if (mapTile.tileImage != null)
						{
							pixelHeightMap[mapTile.row, mapTile.column] = (int)(num3 * 32f + (mapTile.tileImage.rect.height - 32f) / 2f);
						}
						else
						{
							pixelHeightMap[mapTile.row, mapTile.column] = 0;
						}
						break;
					default:
						pixelHeightMap[mapTile.row, mapTile.column] = (int)(num3 * 32f);
						break;
					}
				}
				TilemapManager.instance.triggerTMTileRefresh(new Vector2Int(tileMapX, tileMapY), mapTile.row, mapTile.column, heightDiff);
				continue;
			}
			switch (num)
			{
			case 1:
			{
				int num31 = sourceMap[j * 21 + 3];
				int objectID3 = sourceMap[j * 21 + 10];
				if (num31 == 0)
				{
					deleteOrg(objectID3);
					break;
				}
				int num32 = -sourceMap[j * 21 + 5];
				int image6 = sourceMap[j * 21 + 4];
				int colour2 = sourceMap[j * 21 + 7];
				int transparency3 = sourceMap[j * 21 + 11];
				addUpdateOrg(objectID3, tileMapX, tileMapY, (float)num32 / 32f, num31, image6, colour2, transparency3);
				break;
			}
			case 2:
			{
				int num47 = sourceMap[j * 21 + 3];
				int objectID6 = sourceMap[j * 21 + 10];
				if (num47 == 0)
				{
					deleteChimp(objectID6);
					break;
				}
				int num48 = -sourceMap[j * 21 + 5];
				int tile_x3 = sourceMap[j * 21 + 8];
				int tile_y3 = sourceMap[j * 21 + 9];
				int num49 = sourceMap[j * 21 + 7];
				bool inJourney = (num49 & 0x4000) != 0;
				num49 &= 0x3FFF;
				int image8 = sourceMap[j * 21 + 4];
				int transparency5 = sourceMap[j * 21 + 11];
				int layerDelay3 = sourceMap[j * 21 + 12];
				int num50 = sourceMap[j * 21 + 20];
				int chopFeet = num50 >> 8;
				num50 &= 0xFF;
				int num51 = sourceMap[j * 21 + 13];
				int colour4 = num51 >> 11;
				num51 &= 0x7FF;
				int image9 = sourceMap[j * 21 + 14];
				int image10 = sourceMap[j * 21 + 16];
				int yOffset = -sourceMap[j * 21 + 17];
				int yOffset2 = -sourceMap[j * 21 + 18];
				int yOffset3 = -sourceMap[j * 21 + 19];
				int siegeFlag_x = 0;
				int siegeFlag_y = 0;
				int num52 = 0;
				int siegeFlag_colour = 0;
				int siegeFlag_file = 0;
				int num53 = 0;
				int hpsX = 0;
				int hpsY = 0;
				int num54 = 0;
				int oilY = 0;
				int num55 = 0;
				int targetX = 0;
				int targetY = 0;
				int nexttilex = 0;
				int nexttiley = 0;
				int tileMapX2 = 0;
				int tileMapY2 = 0;
				bool interp = false;
				int nextnumframes = 0;
				int num56 = 0;
				int nextlayerdelay = 0;
				int tele_x_adjust = 0;
				int assassin_rope_x = 0;
				int assassin_rope_y = 0;
				int assassin_rope_fade = 0;
				int recharge = 0;
				if (j + 1 < numElements)
				{
					if (sourceMap[(j + 1) * 21] == 53)
					{
						j++;
						tele_x_adjust = sourceMap[j * 21 + 17];
						assassin_rope_x = sourceMap[j * 21 + 18];
						assassin_rope_y = sourceMap[j * 21 + 19];
						assassin_rope_fade = sourceMap[j * 21 + 16];
					}
					if (j + 1 < numElements && sourceMap[(j + 1) * 21] == 51)
					{
						j++;
						num52 = sourceMap[j * 21 + 4];
						if (num52 > 0)
						{
							siegeFlag_x = sourceMap[j * 21 + 1];
							siegeFlag_y = -sourceMap[j * 21 + 2];
							siegeFlag_colour = sourceMap[j * 21 + 7];
							siegeFlag_file = 189;
						}
						num53 = sourceMap[j * 21 + 10];
						if (num53 > 0)
						{
							hpsX = sourceMap[j * 21 + 8];
							hpsY = -sourceMap[j * 21 + 9];
							recharge = sourceMap[j * 21 + 16];
						}
						num54 = sourceMap[j * 21 + 11];
						if (num54 > 0)
						{
							oilY = -sourceMap[j * 21 + 12];
						}
						num55 = sourceMap[j * 21 + 17];
						if (num55 > 0)
						{
							targetX = sourceMap[j * 21 + 18];
							targetY = -sourceMap[j * 21 + 19];
						}
					}
					if (j + 1 < numElements && sourceMap[(j + 1) * 21] == 52)
					{
						j++;
						interp = true;
						nexttilex = sourceMap[j * 21 + 8];
						nexttiley = sourceMap[j * 21 + 9];
						mapGameTileToTilemapCoord(sourceMap[j * 21 + 1], sourceMap[j * 21 + 2], out tileMapX2, out tileMapY2);
						num56 = -sourceMap[j * 21 + 5];
						nextnumframes = sourceMap[j * 21 + 7];
						nextlayerdelay = sourceMap[j * 21 + 17];
					}
				}
				addUpdateChimp(objectID6, tileMapX, tileMapY, tile_x3, tile_y3, (float)num48 / 32f, num47, image8, num49, transparency5, layerDelay3, num51, image9, image10, yOffset, yOffset2, yOffset3, siegeFlag_x, siegeFlag_y, num52, siegeFlag_colour, siegeFlag_file, num53, hpsX, hpsY, num54, oilY, num55, targetX, targetY, interp, tileMapX2, tileMapY2, nexttilex, nexttiley, (float)num56 / 32f, nextnumframes, nextlayerdelay, num50, chopFeet, tele_x_adjust, colour4, assassin_rope_x, assassin_rope_y, assassin_rope_fade, recharge, inJourney);
				break;
			}
			case 3:
			{
				int num44 = sourceMap[j * 21 + 3];
				int objectID5 = sourceMap[j * 21 + 10];
				if (num44 == 0)
				{
					deleteFly(objectID5);
					break;
				}
				int num45 = -sourceMap[j * 21 + 5];
				int tile_x2 = sourceMap[j * 21 + 8];
				int tile_y2 = sourceMap[j * 21 + 9];
				int colour3 = sourceMap[j * 21 + 7];
				int image7 = sourceMap[j * 21 + 4];
				int transparency4 = sourceMap[j * 21 + 11];
				int layerDelay2 = sourceMap[j * 21 + 12];
				int gameObjectType = sourceMap[j * 21 + 20];
				int infoType = sourceMap[j * 21 + 17];
				int infoData = sourceMap[j * 21 + 18] + sourceMap[j * 21 + 19] * 10000;
				addUpdateFly(objectID5, tileMapX, tileMapY, tile_x2, tile_y2, (float)num45 / 32f, num44, image7, colour3, transparency4, layerDelay2, gameObjectType, infoType, infoData);
				break;
			}
			case 4:
			{
				int num65 = sourceMap[j * 21 + 3];
				int num66 = sourceMap[j * 21 + 10];
				num66 += sourceMap[j * 21 + 19] * 10000;
				if (num65 == 0)
				{
					deleteBuildingAnim(num66);
					break;
				}
				int animLayer = sourceMap[j * 21 + 5];
				int tile_x4 = sourceMap[j * 21 + 8];
				int tile_y4 = -sourceMap[j * 21 + 9];
				int colour5 = sourceMap[j * 21 + 7];
				int num67 = sourceMap[j * 21 + 4];
				int transparency6 = sourceMap[j * 21 + 11];
				int num68 = sourceMap[j * 21 + 12];
				int halfPixelX = sourceMap[j * 21 + 17];
				int halfPixelY = sourceMap[j * 21 + 18];
				bool hasSubSpecial = false;
				if (num65 == 54)
				{
					switch (num67)
					{
					case 9:
						num68 -= 49;
						addUpdateBuildingAnim(num66 + 1000000, tileMapX, tileMapY, tile_x4, tile_y4, animLayer, num65, num67, colour5, transparency6, num68);
						num68 += 49;
						num67 = 5;
						hasSubSpecial = true;
						break;
					case 10:
						num67 = 7;
						num68 -= 49;
						addUpdateBuildingAnim(num66 + 1000000, tileMapX, tileMapY, tile_x4, tile_y4, animLayer, num65, num67, colour5, transparency6, num68);
						num68 += 49;
						num67 = 10;
						hasSubSpecial = true;
						break;
					}
				}
				addUpdateBuildingAnim(num66, tileMapX, tileMapY, tile_x4, tile_y4, animLayer, num65, num67, colour5, transparency6, num68, hasSubSpecial, halfPixelX, halfPixelY);
				break;
			}
			case 5:
			{
				mouseXOffset = sourceMap[j * 21 + 1];
				mouseYOffset = sourceMap[j * 21 + 2];
				int file = sourceMap[j * 21 + 3];
				int image4 = sourceMap[j * 21 + 4];
				int transparency2 = sourceMap[j * 21 + 11];
				mouseCursorGO.SetActive(value: true);
				SpriteMapping.SetBodySprite(mouseCursorGO.GetComponent<SpriteRenderer>(), file, image4, 0, altFrame: false, 0, transparency2);
				int num29 = sourceMap[j * 21 + 13];
				if (num29 > 0)
				{
					int image5 = sourceMap[j * 21 + 14];
					mouseCursorGO2.SetActive(value: true);
					SpriteRenderer component = mouseCursorGO2.GetComponent<SpriteRenderer>();
					component.sortingOrder = 32000;
					SpriteMapping.SetBodySprite(component, num29, image5, 0, altFrame: false, 0, 0);
				}
				else
				{
					mouseCursorGO2.SetActive(value: false);
				}
				flag = true;
				break;
			}
			case 6:
			{
				int num40 = sourceMap[j * 21 + 4];
				int objectID4 = -(tileMapX + tileMapY * 800);
				if (num40 < 0)
				{
					deleteOrg(objectID4);
				}
				else
				{
					addUpdateWhiteCap(objectID4, tileMapX, tileMapY, num40);
				}
				break;
			}
			case 7:
			{
				short num17 = sourceMap[j * 21 + 3];
				int objectID = tileMapX + tileMapY * 800;
				if (num17 == 0)
				{
					deleteWallFillin(objectID);
					break;
				}
				int num18 = -sourceMap[j * 21 + 5];
				int image2 = sourceMap[j * 21 + 4];
				int xoffset = sourceMap[j * 21 + 8];
				addUpdateWallFillin(objectID, tileMapX, tileMapY, (float)num18 / 32f, image2, xoffset);
				break;
			}
			case 8:
			case 9:
			{
				int num19 = sourceMap[j * 21 + 3];
				int objectID2 = sourceMap[j * 21 + 10];
				if (num19 == 0)
				{
					deletePixie(objectID2);
					break;
				}
				int num20 = -sourceMap[j * 21 + 5];
				int image3 = sourceMap[j * 21 + 4];
				int tile_x = sourceMap[j * 21 + 8];
				int tile_y = -sourceMap[j * 21 + 9];
				int transparency = sourceMap[j * 21 + 11];
				int colour = sourceMap[j * 21 + 7];
				int layerDelay = sourceMap[j * 21 + 12];
				addUpdatePixie(objectID2, tileMapX, tileMapY, tile_x, tile_y, num20, num19, image3, num == 9, transparency, layerDelay, colour);
				break;
			}
			case 12:
			{
				int num21 = sourceMap[j * 21 + 5];
				int num22 = sourceMap[j * 21 + 8];
				int num23 = sourceMap[j * 21 + 9];
				int num24 = sourceMap[j * 21 + 17];
				int num25 = sourceMap[j * 21 + 18] * 1000 + sourceMap[j * 21 + 15];
				int num26 = sourceMap[j * 21 + 19] * 1000 + sourceMap[j * 21 + 16];
				short num27 = sourceMap[j * 21 + 3];
				if (num21 >= 0)
				{
					GameData.scenario.addEvent(num21, num22, num23, num24, num25, num26);
				}
				else
				{
					switch (num21)
					{
					case -1:
						GameData.scenario.setGameOverState(num22, num23, num26);
						break;
					case -2:
					{
						int nowDate = num22 * 10000 + num23;
						int num28 = num24 * 10000 + num25;
						bool flag3 = false;
						if (GameData.Instance.game_type == 0 && GameData.Instance.mission_level == 8 && num28 > 3000000)
						{
							flag3 = true;
						}
						if (!flag3)
						{
							GameData.scenario.setEndGameTimer(num21, nowDate, num28, num26);
						}
						break;
					}
					}
				}
				if (num27 > 0)
				{
					GameData.scenario.setAutoLose();
				}
				break;
			}
			case 14:
			{
				int num57 = sourceMap[j * 21 + 3];
				int num58 = sourceMap[j * 21 + 11] + sourceMap[j * 21 + 12] * 10000;
				int data2 = sourceMap[j * 21 + 13] + sourceMap[j * 21 + 14] * 10000;
				int data3 = sourceMap[j * 21 + 15] + sourceMap[j * 21 + 16] * 10000;
				int data4 = sourceMap[j * 21 + 17] + sourceMap[j * 21 + 18] * 10000;
				int data5 = sourceMap[j * 21 + 8] + sourceMap[j * 21 + 9] * 10000;
				if (num57 == 21 && (num58 < 0 || num58 >= 10))
				{
					OnScreenText.Instance.removeOSTEntry((Enums.eOnScreenText)num57);
				}
				else
				{
					OnScreenText.Instance.addOSTEntry((Enums.eOnScreenText)num57, num58, data2, data3, data4, data5);
				}
				break;
			}
			case 20:
			{
				int num46 = sourceMap[j * 21 + 11];
				int data = sourceMap[j * 21 + 12];
				_ = sourceMap[j * 21 + 13];
				_ = sourceMap[j * 21 + 14];
				if (num46 != 0)
				{
					OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_MESSAGE_BAR, num46, data);
				}
				break;
			}
			case 15:
			{
				int ostID = sourceMap[j * 21 + 3];
				OnScreenText.Instance.removeOSTEntry((Enums.eOnScreenText)ostID);
				break;
			}
			case 16:
			{
				int soundID = sourceMap[j * 21 + 3];
				int num33 = sourceMap[j * 21 + 11];
				int num34 = sourceMap[j * 21 + 12];
				int num35 = sourceMap[j * 21 + 13];
				if (num33 >= 0 && num34 >= 0)
				{
					SFXManager.instance.playSound(soundID, (float)num33 / 100f, ((float)num34 - 64f) / 64f);
				}
				else
				{
					if (mapTile == null)
					{
						break;
					}
					Vector3 spritePosVector = getSpritePosVector(tileMapX, tileMapY);
					Vector3 vector = Camera.main.WorldToScreenPoint(spritePosVector);
					float num36 = Screen.width + 200;
					float num37 = Screen.height + 150;
					vector.x += 100f;
					vector.y += 75f;
					if (vector.x >= 0f && vector.y >= 0f && vector.x < num36 && vector.y < num37)
					{
						float num38 = vector.x / (num36 / 2f) - 1f;
						float num39 = (1f - Math.Abs(num38)) * 0.66f + 0.33f;
						if (num38 < 0f)
						{
							num38 = num38 * 0.66f - 0.33f;
						}
						else if (num38 > 0f)
						{
							num38 = num38 * 0.66f + 0.33f;
						}
						float volumeOfset = 1f;
						if (num35 == 0)
						{
							volumeOfset = vector.y / (num37 / 2f) - 1f;
							volumeOfset = (1f - Math.Abs(volumeOfset)) / 0.66f + 0.33f;
							volumeOfset *= num39;
						}
						SFXManager.instance.playSound(soundID, volumeOfset, num38);
					}
				}
				break;
			}
			case 17:
			{
				int soundID2 = sourceMap[j * 21 + 3];
				int num41 = sourceMap[j * 21 + 4];
				int num42 = sourceMap[j * 21 + 11];
				int num43 = sourceMap[j * 21 + 12];
				switch (num41)
				{
				case 0:
				case 1:
					SFXManager.instance.playAmbient(num43, soundID2, (float)num42 / 100f, num41 == 1);
					break;
				case 10:
					MyAudioManager.Instance.stopAmbient(num43);
					break;
				case 5:
					MyAudioManager.Instance.setAmbientVolume(num43, (float)num42 / 100f);
					break;
				}
				break;
			}
			case 18:
			{
				int channel = sourceMap[j * 21 + 12];
				int num30 = sourceMap[j * 21 + 11];
				string fullpath2 = string.Copy(state.speechFileName);
				SFXManager.instance.playSpeech(channel, fullpath2, (float)num30 / 100f);
				break;
			}
			case 19:
			{
				int num14 = sourceMap[j * 21 + 12];
				int num15 = sourceMap[j * 21 + 11];
				int num16 = sourceMap[j * 21 + 3];
				if (MainViewModel.Instance.Show_HUD_Briefing || MainViewModel.Instance.Show_BlackOut)
				{
					break;
				}
				if (num14 <= 1)
				{
					string fullpath = string.Copy(state.musicFileName);
					SFXManager.instance.playMusic(fullpath, (float)num15 / 100f, num16 > 0, num14 > 0);
					break;
				}
				switch (num14)
				{
				case 5:
					MyAudioManager.Instance.setMusicVolume((float)num15 / 100f);
					break;
				case 10:
					MyAudioManager.Instance.stopMusic();
					break;
				}
				break;
			}
			case 21:
			{
				int num63 = sourceMap[j * 21 + 3];
				int num64 = sourceMap[j * 21 + 11];
				if (num63 < 0)
				{
					SFXManager.instance.stopBink();
					break;
				}
				string binkName = string.Copy(state.binkFileName);
				SFXManager.instance.playBink(binkName, num63 > 0, num64 > 0);
				break;
			}
			case 22:
				MainControls.instance.setUIState(state: true);
				break;
			case 23:
			{
				int num59 = sourceMap[j * 21 + 4];
				int num60 = sourceMap[j * 21 + 3];
				int num61 = sourceMap[j * 21 + 11];
				switch (num59)
				{
				case 0:
				{
					int num62 = sourceMap[j * 21 + 12];
					int bodyTextSection = sourceMap[j * 21 + 13];
					int bodyTextLine = sourceMap[j * 21 + 14];
					MainViewModel.Instance.HUDTutorial.setTutorialText(num60, num61, bodyTextSection, bodyTextLine, num62 > 0);
					break;
				}
				case 1:
				{
					int iD = sourceMap[j * 21 + 13];
					if (num61 < 0)
					{
						MainViewModel.Instance.HUDTutorial.clearTutorialButton();
					}
					else
					{
						MainViewModel.Instance.HUDTutorial.showTutorialButton(num60, num61, iD);
					}
					break;
				}
				}
				break;
			}
			case 24:
			{
				int messageID = sourceMap[j * 21 + 17];
				int value = sourceMap[j * 21 + 18];
				AchievementsCommon.Instance.UpdateValue(messageID, value);
				break;
			}
			case 25:
			{
				if ((GameData.Instance.game_type == 2 && GameData.Instance.mapType == Enums.GameModes.SIEGE) || GameData.Instance.game_type == 4 || MainViewModel.Instance.FreezeMainControls || (GameData.Instance.lastGameState.app_mode == 14 && (GameData.Instance.lastGameState.app_sub_mode == 61 || GameData.Instance.lastGameState.app_sub_mode == 62)) || (GameData.Instance.app_sub_mode == 14 && (GameData.Instance.app_sub_mode == 48 || GameData.Instance.app_sub_mode == 49 || GameData.Instance.app_sub_mode == 13)))
				{
					break;
				}
				int num13 = sourceMap[j * 21 + 17];
				bool flag2 = true;
				switch (num13)
				{
				case 29:
				case 39:
				case 40:
				case 41:
				case 42:
				case 43:
				case 44:
				case 50:
				case 52:
				case 54:
				case 55:
				case 71:
				case 72:
				case 73:
				case 79:
				case 80:
				case 81:
				case 82:
				case 83:
				case 84:
				case 85:
				case 86:
				case 87:
				case 88:
				case 89:
					flag2 = false;
					break;
				case 46:
					num13 = 131;
					break;
				case 45:
					num13 = 133;
					break;
				case 47:
					num13 = 127;
					break;
				}
				if (flag2)
				{
					Enums.eStructs eStructs = (Enums.eStructs)num13;
					Enums.eMappers structToMapperEnum = MainViewModel.Instance.getStructToMapperEnum(eStructs.ToString());
					if (structToMapperEnum != Enums.eMappers.MAPPER_NULL && EngineInterface.IsMapperAvailable((int)structToMapperEnum))
					{
						EditorDirector.instance.placeBuilding(structToMapperEnum);
					}
				}
				break;
			}
			}
		}
		if (!flag && mouseCursorGO != null)
		{
			mouseCursorGO.SetActive(value: false);
			mouseCursorGO2.SetActive(value: false);
		}
		GameData.Instance.setGameState(state);
		if (postUpdateFunction != null)
		{
			postUpdateFunction();
			postUpdateFunction = null;
		}
	}

	public void SetPostUpdateFunction(Action action)
	{
		postUpdateFunction = action;
	}

	public void ApplyRadarMap()
	{
		if (lastRadarMap != null)
		{
			radarTexture.SetPixelData(lastRadarMap, 0);
			radarTexture.Apply();
			MainViewModel.Instance.TestRadarMap();
			lastRadarMap = null;
		}
	}

	public GameMapTile getMapTile(int x, int y)
	{
		if (x > 0 && y > 0 && x < tilemapSize && y < tilemapSize)
		{
			return gameMap[x, y];
		}
		return null;
	}

	public int getMapTileSize()
	{
		return tilemapSize / 2;
	}

	public float getLandHeight(int xPos, int yPos)
	{
		return getMapTile(xPos, yPos)?.height ?? 0f;
	}

	public int getRow(int xPos, int yPos)
	{
		return getMapTile(xPos, yPos)?.row ?? 0;
	}

	public int getRow(int xPos, int yPos, int layerDelay)
	{
		GameMapTile mapTile = getMapTile(xPos, yPos);
		if (mapTile != null)
		{
			int num = mapTile.row + layerDelay;
			if (num > tilemapSize)
			{
				num = tilemapSize;
			}
			return num;
		}
		return 0;
	}

	public int getColumn(int xPos, int yPos)
	{
		return getMapTile(xPos, yPos)?.column ?? 0;
	}

	public Vector3 getSpritePosVector(int x, int y, int tileX = 0, int tileY = 0, int objectID = 0, int halfPixelX = 0, int halfPixelY = 0)
	{
		if (tileX > 128)
		{
			tileX -= 256;
		}
		if (tileY > 128)
		{
			tileY -= 256;
		}
		Vector3 cellCentre = MainControls.instance.getCellCentre(x, y);
		cellCentre.x -= 0.5f;
		cellCentre.y += 0.25f;
		cellCentre.x += ((float)tileX + 1f) / 32f + (float)halfPixelX / 64f;
		cellCentre.y -= ((float)tileY + 1f) / 16f / 2f + (float)halfPixelY / 128f;
		cellCentre.z = 200f + cellCentre.y + (float)objectID % 10000f / 10000f;
		return cellCentre;
	}

	private Vector2 getPivotOffset(int troopType)
	{
		switch (troopType)
		{
		case 85:
			return new Vector2(0f, 0.125f);
		case 84:
			return new Vector2(0f, 0.0625f);
		case 81:
			return new Vector2(0f, 5f / 64f);
		case 80:
			return new Vector2(0f, 5f / 64f);
		case 79:
			return new Vector2(0f, 5f / 64f);
		case 78:
			return new Vector2(0f, 3f / 32f);
		case 83:
			return new Vector2(0f, 0.0625f);
		case 82:
			return new Vector2(0f, 0.0625f);
		case 70:
			return new Vector2(0f, 1f / 32f);
		case 71:
			return new Vector2(0f, 1f / 32f);
		case 72:
			return new Vector2(0f, 5f / 64f);
		case 76:
			return new Vector2(0f, 0.0625f);
		case 22:
			return new Vector2(0f, 0.0625f);
		case 23:
			return new Vector2(3f / 64f, -0.0625f);
		case 25:
			return new Vector2(0f, -1f / 32f);
		case 27:
			return new Vector2(0f, 0.0625f);
		case 30:
			return new Vector2(0f, 0.0625f);
		case 5:
			return new Vector2(0f, 0.0625f);
		case 29:
			return new Vector2(0f, 3f / 64f);
		case 37:
			return new Vector2(1f / 32f, 0f);
		case 1:
			return new Vector2(-3f / 64f, 3f / 64f);
		case 3:
			return new Vector2(0f, -3f / 64f);
		case 7:
			return new Vector2(-3f / 64f, -0.0625f);
		case 11:
		case 12:
		case 13:
		case 14:
			return new Vector2(-1f / 32f, 0f);
		case 16:
			return new Vector2(0f, 5f / 64f);
		case 17:
			return new Vector2(0f, 1f / 32f);
		case 19:
			return new Vector2(0f, 3f / 64f);
		case 31:
		case 32:
			return new Vector2(0f, 0.0625f);
		case 51:
			return new Vector2(0f, 0.0625f);
		case 10:
			return new Vector2(0f, 3f / 64f);
		case 18:
			return new Vector2(0f, 3f / 32f);
		case 21:
			return new Vector2(0f, 3f / 32f);
		case 42:
			return new Vector2(0f, 3f / 64f);
		case 43:
			return new Vector2(0f, 0.25f);
		case 6:
			return new Vector2(0f, -1f / 64f);
		case 44:
			return new Vector2(0f, 0.1875f);
		case 45:
			return new Vector2(0f, -3f / 64f);
		case 46:
			return new Vector2(5f / 64f, -3f / 64f);
		case 47:
			return new Vector2(0f, 13f / 64f);
		case 52:
			return new Vector2(0f, 3f / 32f);
		case 33:
			return new Vector2(0f, 0.0625f);
		case 56:
			return new Vector2(0f, 1f / 32f);
		case 55:
			return new Vector2(0f, 0.0625f);
		case 57:
			return new Vector2(1f / 32f, 3f / 32f);
		case 20:
			return new Vector2(1f / 32f, -1f / 32f);
		case 2:
			return new Vector2(0f, -3f / 64f);
		case 60:
			return new Vector2(0f, 5f / 32f);
		case 62:
			return new Vector2(3f / 64f, 0f);
		case 64:
			return new Vector2(0f, 0.0625f);
		case 65:
			return new Vector2(0f, 5f / 64f);
		case 66:
			return new Vector2(0f, 3f / 64f);
		case 67:
			return new Vector2(0f, -3f / 64f);
		case 41:
			return new Vector2(9f / 64f, 5f / 32f);
		case 88:
			return new Vector2(1f / 32f, 9f / 64f);
		case 74:
			return new Vector2(-5f / 32f, 15f / 64f);
		default:
			return Vector2.zero;
		}
	}

	public void addUpdateChimp(int _objectID, int x, int y, int tile_x, int tile_y, float heightAboveGround, int file, int image, int _colour, int _transparency, int _layerDelay, int file2, int image2, int image3, int yOffset1, int yOffset2, int yOffset3, int siegeFlag_x, int siegeFlag_y, int siegeFlag_frame, int siegeFlag_colour, int siegeFlag_file, int hpsFrame, int hpsX, int hpsY, int oilFrame, int oilY, int targetFrame, int targetX, int targetY, bool interp, int nextx, int nexty, int nexttilex, int nexttiley, float nexthi, int nextnumframes, int nextlayerdelay, int _gameObjectType, int _chopFeet, int _tele_x_adjust, int _colour2, int assassin_rope_x, int assassin_rope_y, int assassin_rope_fade, int recharge, bool inJourney)
	{
		bool flag = false;
		if (_layerDelay >= 50)
		{
			_layerDelay -= 50;
			flag = true;
		}
		int num = 0;
		if (hpsFrame != 0 && recharge > 0)
		{
			num = 11 - (recharge * 11 / 400 + 17);
			if (num < 17)
			{
				num = 17;
			}
			else if (num > 27)
			{
				num = 27;
			}
		}
		if (_transparency < 0)
		{
			_transparency = 0;
		}
		Vector3 spritePosVector = getSpritePosVector(x, y, tile_x, tile_y, _objectID, _tele_x_adjust * 2);
		spritePosVector.y += heightAboveGround;
		Vector3 nextPos;
		if (interp)
		{
			nextPos = getSpritePosVector(nextx, nexty, nexttilex, nexttiley, _objectID);
			nextPos.y += nexthi;
		}
		else
		{
			nextPos = spritePosVector;
		}
		Vector2 pivotOffset = getPivotOffset(_gameObjectType);
		if (assassin_rope_y != 0)
		{
			yOffset2 += assassin_rope_y;
		}
		Chimp chimp = null;
		int row = getRow(x, y, _layerDelay);
		int column = 0;
		int num2 = -20000 + row * 49;
		int num3 = 0;
		if (_layerDelay > 0 || flag)
		{
			num3 = 34;
		}
		if (!chimps.ContainsKey(_objectID))
		{
			GameObject objectForType = ObjectPool.instance.GetObjectForType(0, "simpleSprite", ObjectPool.getContainerID(row, column), _objectID | 0x8000000);
			if (objectForType != null)
			{
				SpriteRenderer component = objectForType.GetComponent<SpriteRenderer>();
				component.sortingOrder = num2 + 8 + num3;
				SpriteMapping.SetBodySprite(component, file, image, _colour, altFrame: false, _chopFeet, _transparency);
				chimp = new Chimp
				{
					gameObject = objectForType,
					sprRenderer = component,
					mapX = x,
					mapY = y,
					tileX = tile_x,
					tileY = tile_y,
					file1 = file,
					image1 = image,
					colour1 = _colour,
					objectID = _objectID,
					layerDelay = _layerDelay,
					transparency = _transparency,
					gameObjectType = _gameObjectType,
					chopFeet = _chopFeet,
					tele_x_adjust = _tele_x_adjust,
					row = row,
					column = column
				};
				chimp.bodyWidth = body_actual_dimensions[chimp.gameObjectType, 0] / 64f * 2f;
				chimp.bodyHeight = body_actual_dimensions[chimp.gameObjectType, 1] / 64f * 2f;
				chimp.bodyHeightBelow = body_actual_dimensions[chimp.gameObjectType, 2] / 64f * 2f;
				switch (_gameObjectType)
				{
				case 58:
					chimp.rotation8TargetFacing = (chimp.rotation8LastFacing = getSiegeTowerFacing(image, image2, image3));
					chimp.rotating = false;
					break;
				case 39:
					chimp.rotation8TargetFacing = (chimp.rotation8LastFacing = getCatapultFacing(image));
					chimp.rotating = false;
					break;
				case 59:
					chimp.rotation8TargetFacing = (chimp.rotation8LastFacing = getBatteringRamFacing(image));
					chimp.rotating = false;
					break;
				case 77:
					chimp.rotation8TargetFacing = (chimp.rotation8LastFacing = getFireBallistaFacing(image));
					chimp.rotating = false;
					break;
				}
				chimps[_objectID] = chimp;
			}
		}
		else
		{
			chimp = chimps[_objectID];
			chimp.gameObjectType = _gameObjectType;
			int containerID = ObjectPool.getContainerID(row, column);
			int containerID2 = ObjectPool.getContainerID(chimp.row, chimp.column);
			if (containerID != containerID2)
			{
				ObjectPool.instance.moveRow(0, chimp.gameObject, containerID2, containerID, chimp.objectID | 0x8000000);
				chimp.row = row;
				chimp.column = column;
			}
			switch (_gameObjectType)
			{
			case 58:
			{
				if (chimp.rotating)
				{
					int num7 = lerpSiegeTower(chimp);
					if (chimp.rotating)
					{
						image2 = 0;
						image3 = 0;
						image = num7;
					}
					break;
				}
				int siegeTowerFacing = getSiegeTowerFacing(image, image2, image3);
				if (siegeTowerFacing != chimp.rotation8LastFacing && siegeTowerFacing != -1)
				{
					chimp.rotation8TargetFacing = siegeTowerFacing;
					int[] siegeTowerRotationArray2 = getSiegeTowerRotationArray(chimp.rotation8LastFacing, siegeTowerFacing);
					int num8 = siegeTowerRotationArray2.Length - 1;
					if (siegeTowerRotationArray2.Length > 1)
					{
						float num9 = (float)num8 * (1f / 60f) * (Director.instance.EngineFrameTime * 100f);
						chimp.rotationStart = DateTime.UtcNow;
						chimp.rotationEnd = chimp.rotationStart.AddSeconds(num9);
						chimp.rotating = true;
						image2 = 0;
						image3 = 0;
						image = siegeTowerRotationArray2[0] + 90 - 1;
					}
					else
					{
						chimp.rotation8LastFacing = siegeTowerFacing;
					}
				}
				break;
			}
			case 39:
			{
				if (chimp.rotating)
				{
					int num10 = lerpCatapult(chimp);
					if (chimp.rotating)
					{
						image = num10;
					}
					break;
				}
				int catapultFacing = getCatapultFacing(image);
				if (catapultFacing != chimp.rotation8LastFacing && catapultFacing != -1)
				{
					chimp.rotation8TargetFacing = catapultFacing;
					int[] siegeTowerRotationArray3 = getSiegeTowerRotationArray(chimp.rotation8LastFacing, catapultFacing);
					int num11 = siegeTowerRotationArray3.Length - 1;
					if (siegeTowerRotationArray3.Length > 1)
					{
						float num12 = (float)num11 * (1f / 60f) * (Director.instance.EngineFrameTime * 100f);
						chimp.rotationStart = DateTime.UtcNow;
						chimp.rotationEnd = chimp.rotationStart.AddSeconds(num12);
						chimp.rotating = true;
						image = siegeTowerRotationArray3[0] + 713 - 1;
					}
					else
					{
						chimp.rotation8LastFacing = catapultFacing;
					}
				}
				break;
			}
			case 59:
			{
				if (chimp.rotating)
				{
					int num13 = lerpBatteringRam(chimp);
					if (chimp.rotating)
					{
						image = num13;
					}
					break;
				}
				int batteringRamFacing = getBatteringRamFacing(image);
				if (batteringRamFacing != chimp.rotation8LastFacing && batteringRamFacing != -1)
				{
					chimp.rotation8TargetFacing = batteringRamFacing;
					int[] siegeTowerRotationArray4 = getSiegeTowerRotationArray(chimp.rotation8LastFacing, batteringRamFacing);
					int num14 = siegeTowerRotationArray4.Length - 1;
					if (siegeTowerRotationArray4.Length > 1)
					{
						float num15 = (float)num14 * (1f / 60f) * (Director.instance.EngineFrameTime * 100f);
						chimp.rotationStart = DateTime.UtcNow;
						chimp.rotationEnd = chimp.rotationStart.AddSeconds(num15);
						chimp.rotating = true;
						image = siegeTowerRotationArray4[0] + 274 - 1;
					}
					else
					{
						chimp.rotation8LastFacing = batteringRamFacing;
					}
				}
				break;
			}
			case 77:
			{
				if (chimp.rotating)
				{
					int num4 = lerpFireBallista(chimp);
					if (chimp.rotating)
					{
						image = num4;
					}
					break;
				}
				int fireBallistaFacing = getFireBallistaFacing(image);
				if (fireBallistaFacing != chimp.rotation8LastFacing && fireBallistaFacing != -1)
				{
					chimp.rotation8TargetFacing = fireBallistaFacing;
					int[] siegeTowerRotationArray = getSiegeTowerRotationArray(chimp.rotation8LastFacing, fireBallistaFacing);
					int num5 = siegeTowerRotationArray.Length - 1;
					if (siegeTowerRotationArray.Length > 1)
					{
						float num6 = (float)num5 * (1f / 60f) * (Director.instance.EngineFrameTime * 100f);
						chimp.rotationStart = DateTime.UtcNow;
						chimp.rotationEnd = chimp.rotationStart.AddSeconds(num6);
						chimp.rotating = true;
						image = siegeTowerRotationArray[0] + 313 - 1;
					}
					else
					{
						chimp.rotation8LastFacing = fireBallistaFacing;
					}
				}
				break;
			}
			}
		}
		chimp.baseSortLayer = num2;
		if (file2 > 0 || image2 != 0 || image3 != 0 || hpsFrame != 0 || oilFrame != 0 || targetFrame != 0)
		{
			if (image2 != 0 && !chimp.gameObject2_enabled)
			{
				GameObject objectForType2 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType2 != null)
				{
					objectForType2.transform.parent = chimp.gameObject.transform;
					SpriteRenderer component2 = objectForType2.GetComponent<SpriteRenderer>();
					if (file2 == 198)
					{
						int row2 = getRow(x, y, 0);
						int num16 = -20000 + row2 * 49;
						component2.sortingOrder = num16 + 9 + num3;
					}
					else
					{
						component2.sortingOrder = num2 + 10 + num3;
					}
					if (image2 > 0)
					{
						int transparency = _transparency;
						if (file2 == 198)
						{
							transparency = assassin_rope_fade;
						}
						SpriteMapping.SetBodySprite(component2, file2, image2, _colour2, altFrame: false, _chopFeet, transparency);
						objectForType2.SetActive(value: true);
					}
					else
					{
						component2.sprite = null;
						objectForType2.SetActive(value: false);
					}
					chimp.gameObject2 = objectForType2;
					chimp.gameObject2_enabled = true;
					chimp.sprRenderer2 = component2;
					chimp.file2 = file2;
					chimp.image2 = image2;
					chimp.colour2 = _colour2;
					chimp.assassin_rope_x = assassin_rope_x;
					chimp.assassin_rope_y = assassin_rope_y;
					chimp.assassin_rope_fade = assassin_rope_fade;
				}
			}
			if (image3 != 0 && !chimp.gameObject3_enabled)
			{
				GameObject objectForType3 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType3 != null)
				{
					objectForType3.transform.parent = chimp.gameObject.transform;
					SpriteRenderer component3 = objectForType3.GetComponent<SpriteRenderer>();
					component3.sortingOrder = num2 + 10 + num3;
					if (image3 > 0)
					{
						SpriteMapping.SetBodySprite(component3, file2, image3, 0, altFrame: false, _chopFeet, _transparency);
						objectForType3.SetActive(value: true);
					}
					else
					{
						component3.sprite = null;
						objectForType3.SetActive(value: false);
					}
					chimp.gameObject3 = objectForType3;
					chimp.gameObject3_enabled = true;
					chimp.sprRenderer3 = component3;
					chimp.image3 = image3;
				}
			}
			if (siegeFlag_file != 0 && !chimp.gameObject4_enabled)
			{
				GameObject objectForType4 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType4 != null)
				{
					objectForType4.transform.parent = chimp.gameObject.transform;
					SpriteRenderer component4 = objectForType4.GetComponent<SpriteRenderer>();
					component4.sortingOrder = num2 + 11 + num3;
					if (siegeFlag_frame > 0)
					{
						SpriteMapping.SetBodySprite(component4, siegeFlag_file, siegeFlag_frame, siegeFlag_colour, altFrame: false, _chopFeet, _transparency);
						objectForType4.SetActive(value: true);
					}
					else
					{
						component4.sprite = null;
						objectForType4.SetActive(value: false);
					}
					chimp.gameObject4 = objectForType4;
					chimp.gameObject4_enabled = true;
					chimp.sprRenderer4 = component4;
					chimp.image4 = siegeFlag_frame;
					chimp.colour4 = siegeFlag_colour;
				}
			}
			if ((hpsFrame != 0 || num != 0) && !chimp.gameObject5_enabled)
			{
				GameObject objectForType5 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType5 != null)
				{
					objectForType5.transform.parent = chimp.gameObject.transform;
					SpriteRenderer component5 = objectForType5.GetComponent<SpriteRenderer>();
					chimp.hpsRealSortOrder = num2 + 12 + num3;
					if (!healthbarsTopMost)
					{
						component5.sortingOrder = chimp.hpsRealSortOrder;
					}
					else
					{
						component5.sortingOrder = 32767;
					}
					if (hpsFrame > 0)
					{
						SpriteMapping.SetBodySprite(component5, 16, hpsFrame, 0, altFrame: false, _chopFeet, 0);
						objectForType5.SetActive(value: true);
					}
					else
					{
						component5.sprite = null;
						objectForType5.SetActive(value: false);
					}
					chimp.gameObject5 = objectForType5;
					chimp.gameObject5_enabled = true;
					chimp.sprRenderer5 = component5;
					chimp.image5 = hpsFrame;
				}
			}
			if (oilFrame != 0 && chimp.gameObject6 == null)
			{
				GameObject objectForType6 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType6 != null)
				{
					objectForType6.transform.parent = chimp.gameObject.transform;
					SpriteRenderer component6 = objectForType6.GetComponent<SpriteRenderer>();
					chimp.hps2RealSortOrder = num2 + 13 + num3;
					component6.sortingOrder = chimp.hps2RealSortOrder;
					if (oilFrame > 0)
					{
						int transparency2 = 0;
						if (oilFrame > 400)
						{
							SpriteMapping.SetBodySprite(component6, 16, oilFrame - 400, 0, altFrame: false, _chopFeet, transparency2);
							if (healthbarsTopMost)
							{
								component6.sortingOrder = 32767;
							}
						}
						else
						{
							if (oilFrame == 332)
							{
								transparency2 = 16;
							}
							SpriteMapping.SetBodySprite(component6, 172, oilFrame, 0, altFrame: false, _chopFeet, transparency2);
						}
						objectForType6.SetActive(value: true);
					}
					else
					{
						component6.sprite = null;
						objectForType6.SetActive(value: false);
					}
					chimp.gameObject6 = objectForType6;
					chimp.gameObject6_enabled = true;
					chimp.sprRenderer6 = component6;
					chimp.image6 = oilFrame;
				}
			}
			if (targetFrame != 0 && !chimp.gameObject7_enabled)
			{
				GameObject objectForType7 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType7 != null)
				{
					objectForType7.transform.parent = chimp.gameObject.transform;
					SpriteRenderer component7 = objectForType7.GetComponent<SpriteRenderer>();
					component7.sortingOrder = num2 + 14 + num3;
					if (targetFrame > 0)
					{
						SpriteMapping.SetBodySprite(component7, 172, targetFrame, 0, altFrame: false, _chopFeet, 0);
						objectForType7.SetActive(value: true);
					}
					else
					{
						component7.sprite = null;
						objectForType7.SetActive(value: false);
					}
					chimp.gameObject7 = objectForType7;
					chimp.gameObject7_enabled = true;
					chimp.sprRenderer7 = component7;
					chimp.image6 = targetFrame;
				}
			}
		}
		chimp.altFrame1Set = false;
		bool flag2 = false;
		if (chimp.transparency != _transparency || chimp.chopFeet != _chopFeet)
		{
			flag2 = true;
			chimp.chopFeet = _chopFeet;
			chimp.transparency = _transparency;
		}
		if (chimp.file1 != file || chimp.image1 != image || chimp.colour1 != _colour || flag2)
		{
			chimp.file1 = file;
			chimp.image1 = image;
			chimp.colour1 = _colour;
			SpriteMapping.SetBodySprite(chimp.sprRenderer, file, image, _colour, altFrame: false, _chopFeet, _transparency);
		}
		bool flag3 = false;
		if ((chimp.file2 != file2 || chimp.image2 != image2 || chimp.colour2 != _colour2 || flag2 || chimp.assassin_rope_y != assassin_rope_y || chimp.assassin_rope_fade != assassin_rope_fade) && chimp.gameObject2_enabled)
		{
			if (chimp.file2 != file2)
			{
				flag3 = true;
			}
			chimp.file2 = file2;
			chimp.image2 = image2;
			chimp.colour2 = _colour2;
			chimp.assassin_rope_x = assassin_rope_x;
			chimp.assassin_rope_y = assassin_rope_y;
			chimp.assassin_rope_fade = assassin_rope_fade;
			if (image2 > 0)
			{
				if (file2 == 198)
				{
					int row3 = getRow(x, y, 0);
					int num17 = -20000 + row3 * 49;
					chimp.sprRenderer2.sortingOrder = num17 + 9 + num3;
				}
				else
				{
					chimp.sprRenderer2.sortingOrder = num2 + 9 + num3;
				}
				SpriteMapping.SetBodySprite(chimp.sprRenderer2, file2, image2, _colour2, altFrame: false, _chopFeet, _transparency);
				if (!chimp.gameObject2.activeSelf)
				{
					chimp.gameObject2.SetActive(value: true);
				}
			}
			else
			{
				chimp.sprRenderer2.sprite = null;
				if (chimp.gameObject2.activeSelf)
				{
					chimp.gameObject2.SetActive(value: false);
				}
			}
		}
		if ((flag3 || chimp.image3 != image3 || flag2) && chimp.gameObject3_enabled)
		{
			chimp.image3 = image3;
			chimp.colour3 = _colour;
			if (image3 > 0)
			{
				SpriteMapping.SetBodySprite(chimp.sprRenderer3, file2, image3, 0, altFrame: false, _chopFeet, _transparency);
				chimp.gameObject3.SetActive(value: true);
			}
			else
			{
				chimp.sprRenderer3.sprite = null;
				chimp.gameObject3.SetActive(value: false);
			}
		}
		if ((chimp.image4 != siegeFlag_frame || chimp.colour4 != siegeFlag_colour || flag2) && chimp.gameObject4_enabled)
		{
			chimp.image4 = siegeFlag_frame;
			chimp.colour4 = siegeFlag_colour;
			if (siegeFlag_frame > 0)
			{
				SpriteMapping.SetBodySprite(chimp.sprRenderer4, siegeFlag_file, siegeFlag_frame, siegeFlag_colour, altFrame: false, _chopFeet, _transparency);
				chimp.gameObject4.SetActive(value: true);
			}
			else
			{
				chimp.sprRenderer4.sprite = null;
				chimp.gameObject4.SetActive(value: false);
			}
		}
		if (chimp.image5 != hpsFrame && chimp.gameObject5_enabled)
		{
			chimp.image5 = hpsFrame;
			if (hpsFrame > 0)
			{
				SpriteMapping.SetBodySprite(chimp.sprRenderer5, 16, hpsFrame, 0, altFrame: false, _chopFeet, 0);
				chimp.gameObject5.SetActive(value: true);
			}
			else
			{
				chimp.sprRenderer5.sprite = null;
				chimp.gameObject5.SetActive(value: false);
			}
		}
		if (chimp.image6 != oilFrame && chimp.gameObject6_enabled)
		{
			chimp.image6 = oilFrame;
			if (oilFrame > 0)
			{
				int transparency3 = 0;
				if (oilFrame > 400)
				{
					SpriteMapping.SetBodySprite(chimp.sprRenderer6, 16, oilFrame - 400, 0, altFrame: false, _chopFeet, transparency3);
				}
				else
				{
					if (oilFrame == 332)
					{
						transparency3 = 16;
					}
					SpriteMapping.SetBodySprite(chimp.sprRenderer6, 172, oilFrame, 0, altFrame: false, _chopFeet, transparency3);
				}
				chimp.gameObject6.SetActive(value: true);
			}
			else
			{
				chimp.sprRenderer6.sprite = null;
				chimp.gameObject6.SetActive(value: false);
			}
		}
		if (chimp.image7 != targetFrame && chimp.gameObject7_enabled)
		{
			chimp.image7 = targetFrame;
			if (targetFrame > 0)
			{
				SpriteMapping.SetBodySprite(chimp.sprRenderer7, 172, targetFrame, 0, altFrame: false, _chopFeet, 0);
				chimp.gameObject7.SetActive(value: true);
			}
			else
			{
				chimp.sprRenderer7.sprite = null;
				chimp.gameObject7.SetActive(value: false);
			}
		}
		if (chimp.mapX != x || chimp.mapY != y || chimp.layerDelay != _layerDelay)
		{
			chimp.sprRenderer.sortingOrder = num2 + 8 + num3;
			if (chimp.sprRenderer2 != null)
			{
				if (file2 == 198)
				{
					int row4 = getRow(x, y, 0);
					int num18 = -20000 + row4 * 49;
					chimp.sprRenderer2.sortingOrder = num18 + 9 + num3;
				}
				else
				{
					chimp.sprRenderer2.sortingOrder = num2 + 9 + num3;
				}
			}
			if (chimp.sprRenderer3 != null)
			{
				chimp.sprRenderer3.sortingOrder = num2 + 10 + num3;
			}
			if (chimp.sprRenderer4 != null)
			{
				chimp.sprRenderer4.sortingOrder = num2 + 11 + num3;
			}
			if (chimp.sprRenderer5 != null)
			{
				chimp.hpsRealSortOrder = num2 + 12 + num3;
				if (!healthbarsTopMost)
				{
					chimp.sprRenderer5.sortingOrder = chimp.hpsRealSortOrder;
				}
				else
				{
					chimp.sprRenderer5.sortingOrder = 32767;
				}
			}
			if (chimp.sprRenderer6 != null)
			{
				chimp.hps2RealSortOrder = num2 + 13 + num3;
				if (!healthbarsTopMost || oilFrame < 400)
				{
					chimp.sprRenderer6.sortingOrder = chimp.hps2RealSortOrder;
				}
				else
				{
					chimp.sprRenderer6.sortingOrder = 32767;
				}
			}
			if (chimp.sprRenderer7 != null)
			{
				chimp.sprRenderer7.sortingOrder = num2 + 14 + num3;
			}
			chimp.mapX = x;
			chimp.mapY = y;
			chimp.layerDelay = _layerDelay;
		}
		float num19 = 0f;
		if (inJourney)
		{
			num19 = Director.instance.EngineFrameTime;
		}
		bool flag4 = false;
		if (!interp && chimp.interp && DateTime.UtcNow < chimp.nextTimeEnd.AddSeconds(num19))
		{
			flag4 = true;
		}
		else if (interp && Math.Abs(heightAboveGround - nexthi) > 1.25f)
		{
			chimp.interp = (interp = false);
		}
		else
		{
			chimp.interp = interp;
		}
		if (interp)
		{
			chimp.pos = spritePosVector;
			chimp.nextPos = nextPos;
			chimp.numNextFrames = nextnumframes;
			chimp.nextTimeStart = DateTime.UtcNow;
			chimp.nextTimeEnd = chimp.nextTimeStart.AddSeconds(((float)nextnumframes + 1f) * Director.instance.EngineFrameTime);
			chimp.nextHi = nexthi;
			chimp.nextBaseSortLayer = -20000 + getRow(nextx, nexty, chimp.nextLayerDelay) * 49;
			chimp.nextBaseSortLayerApplied = chimp.nextBaseSortLayer == chimp.baseSortLayer;
			chimp.nextLayerDelay = nextlayerdelay;
		}
		chimp.yOffset1 = yOffset1;
		chimp.yOffset2 = yOffset2;
		chimp.yOffset3 = yOffset3;
		chimp.siegeFlag_x = siegeFlag_x;
		chimp.siegeFlag_y = siegeFlag_y;
		chimp.hpsX = hpsX;
		chimp.hpsY = hpsY;
		chimp.oilY = oilY;
		chimp.oilX = 0;
		if (oilFrame > 400)
		{
			chimp.oilX = hpsX;
		}
		chimp.targetX = targetX;
		chimp.targetY = targetY;
		chimp.tele_x_adjust = _tele_x_adjust;
		chimp.tileX = tile_x;
		chimp.tileY = tile_y;
		if (!flag4)
		{
			chimp.position = new Vector3(spritePosVector.x + pivotOffset.x, spritePosVector.y + pivotOffset.y + (float)yOffset1 / 32f, spritePosVector.z);
			chimp.gameObject.transform.position = chimp.position;
			if (chimp.gameObject2_enabled)
			{
				chimp.gameObject2.transform.position = new Vector3(spritePosVector.x + pivotOffset.x, spritePosVector.y + (float)yOffset2 / 32f + pivotOffset.y, spritePosVector.z);
			}
			if (chimp.gameObject3_enabled)
			{
				chimp.gameObject3.transform.position = new Vector3(spritePosVector.x + pivotOffset.x, spritePosVector.y + (float)yOffset3 / 32f + pivotOffset.y, spritePosVector.z);
			}
			if (chimp.gameObject4_enabled)
			{
				chimp.gameObject4.transform.position = new Vector3(spritePosVector.x + (float)siegeFlag_x / 32f, spritePosVector.y + (float)siegeFlag_y / 32f, spritePosVector.z);
			}
			if (chimp.gameObject5_enabled)
			{
				chimp.gameObject5.transform.position = new Vector3(spritePosVector.x + (float)hpsX / 32f, spritePosVector.y + (float)hpsY / 32f, spritePosVector.z);
			}
			if (chimp.gameObject6_enabled)
			{
				chimp.gameObject6.transform.position = new Vector3(spritePosVector.x + (float)chimp.oilX / 32f, spritePosVector.y + (float)oilY / 32f, spritePosVector.z);
			}
			if (chimp.gameObject7_enabled)
			{
				chimp.gameObject7.transform.position = new Vector3(spritePosVector.x + (float)targetX / 32f, spritePosVector.y + (float)targetY / 32f, spritePosVector.z);
			}
		}
		else
		{
			interpChimp(chimp, force: true);
		}
	}

	private void lerpChimps()
	{
		foreach (KeyValuePair<int, Chimp> chimp in chimps)
		{
			interpChimp(chimp.Value);
		}
	}

	public void interpChimp(Chimp this_chimp, bool force = false)
	{
		if (this_chimp.interp || force)
		{
			DateTime utcNow = DateTime.UtcNow;
			float num = 0f;
			if (utcNow <= this_chimp.nextTimeStart)
			{
				num = 0f;
			}
			else if (utcNow >= this_chimp.nextTimeEnd)
			{
				this_chimp.interp = false;
				num = 1f;
			}
			else
			{
				num = (float)((utcNow - this_chimp.nextTimeStart).TotalMilliseconds / (this_chimp.nextTimeEnd - this_chimp.nextTimeStart).TotalMilliseconds);
			}
			if (num > 0.5f && !this_chimp.nextBaseSortLayerApplied)
			{
				int num2 = 0;
				if (this_chimp.nextLayerDelay > 0)
				{
					num2 = 34;
				}
				this_chimp.nextBaseSortLayerApplied = true;
				this_chimp.sprRenderer.sortingOrder = this_chimp.nextBaseSortLayer + 8 + num2;
				if (this_chimp.sprRenderer2 != null)
				{
					this_chimp.sprRenderer2.sortingOrder = this_chimp.nextBaseSortLayer + 9 + num2;
				}
				if (this_chimp.sprRenderer3 != null)
				{
					this_chimp.sprRenderer3.sortingOrder = this_chimp.nextBaseSortLayer + 10 + num2;
				}
				if (this_chimp.sprRenderer4 != null)
				{
					this_chimp.sprRenderer4.sortingOrder = this_chimp.nextBaseSortLayer + 11 + num2;
				}
				if (this_chimp.sprRenderer5 != null)
				{
					this_chimp.hpsRealSortOrder = this_chimp.nextBaseSortLayer + 12 + num2;
					if (!healthbarsTopMost)
					{
						this_chimp.sprRenderer5.sortingOrder = this_chimp.hpsRealSortOrder;
					}
					else
					{
						this_chimp.sprRenderer5.sortingOrder = 32767;
					}
				}
				if (this_chimp.sprRenderer6 != null)
				{
					this_chimp.hps2RealSortOrder = this_chimp.nextBaseSortLayer + 13 + num2;
					if (!healthbarsTopMost || this_chimp.image6 < 400)
					{
						this_chimp.sprRenderer6.sortingOrder = this_chimp.hps2RealSortOrder;
					}
					else
					{
						this_chimp.sprRenderer6.sortingOrder = 32767;
					}
				}
				if (this_chimp.sprRenderer7 != null)
				{
					this_chimp.sprRenderer7.sortingOrder = this_chimp.nextBaseSortLayer + 14 + num2;
				}
			}
			Vector3 vector = Vector3.Lerp(this_chimp.pos, this_chimp.nextPos, num);
			Vector2 pivotOffset = getPivotOffset(this_chimp.gameObjectType);
			this_chimp.position = new Vector3(vector.x + pivotOffset.x, vector.y + (float)this_chimp.yOffset1 / 32f + pivotOffset.y, vector.z);
			this_chimp.gameObject.transform.position = this_chimp.position;
			if (this_chimp.gameObject2_enabled)
			{
				this_chimp.gameObject2.transform.position = new Vector3(vector.x + pivotOffset.x, vector.y + (float)this_chimp.yOffset2 / 32f + pivotOffset.y, vector.z);
			}
			if (this_chimp.gameObject3_enabled)
			{
				this_chimp.gameObject3.transform.position = new Vector3(vector.x + pivotOffset.x, vector.y + (float)this_chimp.yOffset3 / 32f + pivotOffset.y, vector.z);
			}
			if (this_chimp.gameObject4_enabled)
			{
				this_chimp.gameObject4.transform.position = new Vector3(vector.x + (float)this_chimp.siegeFlag_x / 32f, vector.y + (float)this_chimp.siegeFlag_y / 32f, vector.z);
			}
			if (this_chimp.gameObject5_enabled)
			{
				this_chimp.gameObject5.transform.position = new Vector3(vector.x + (float)this_chimp.hpsX / 32f, vector.y + (float)this_chimp.hpsY / 32f, vector.z);
			}
			if (this_chimp.gameObject6_enabled)
			{
				this_chimp.gameObject6.transform.position = new Vector3(vector.x + (float)this_chimp.oilX / 32f, vector.y + (float)this_chimp.oilY / 32f, vector.z);
			}
			if (this_chimp.gameObject7_enabled)
			{
				this_chimp.gameObject7.transform.position = new Vector3(vector.x + (float)this_chimp.targetX / 32f, vector.y + (float)this_chimp.targetY / 32f, vector.z);
			}
			if ((double)num > 0.5 && !this_chimp.altFrame1Set && !this_chimp.rotating)
			{
				this_chimp.altFrame1Set = true;
				SpriteMapping.SetBodySprite(this_chimp.sprRenderer, this_chimp.file1, this_chimp.image1, this_chimp.colour1, altFrame: true, this_chimp.chopFeet, this_chimp.transparency);
			}
		}
		if (this_chimp.rotating)
		{
			int num3 = -1;
			if (this_chimp.gameObjectType == 58)
			{
				num3 = lerpSiegeTower(this_chimp);
			}
			else if (this_chimp.gameObjectType == 39)
			{
				num3 = lerpCatapult(this_chimp);
			}
			else if (this_chimp.gameObjectType == 59)
			{
				num3 = lerpBatteringRam(this_chimp);
			}
			else if (this_chimp.gameObjectType == 77)
			{
				num3 = lerpFireBallista(this_chimp);
			}
			if (this_chimp.rotating && num3 >= 0)
			{
				this_chimp.image1 = num3;
				SpriteMapping.SetBodySprite(this_chimp.sprRenderer, this_chimp.file1, num3, this_chimp.colour1, altFrame: false, this_chimp.chopFeet, this_chimp.transparency);
			}
		}
	}

	private int lerpSiegeTower(Chimp this_chimp)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow >= this_chimp.rotationEnd)
		{
			this_chimp.rotating = false;
			this_chimp.rotation8LastFacing = this_chimp.rotation8TargetFacing;
			return 0;
		}
		int[] siegeTowerRotationArray = getSiegeTowerRotationArray(this_chimp.rotation8LastFacing, this_chimp.rotation8TargetFacing);
		int num = (int)((float)((utcNow - this_chimp.rotationStart).TotalSeconds / (this_chimp.rotationEnd - this_chimp.rotationStart).TotalSeconds) * (float)(siegeTowerRotationArray.Length - 1));
		return siegeTowerRotationArray[num] + 90 - 1;
	}

	private int lerpCatapult(Chimp this_chimp)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow >= this_chimp.rotationEnd)
		{
			this_chimp.rotating = false;
			this_chimp.rotation8LastFacing = this_chimp.rotation8TargetFacing;
			return 0;
		}
		int[] siegeTowerRotationArray = getSiegeTowerRotationArray(this_chimp.rotation8LastFacing, this_chimp.rotation8TargetFacing);
		int num = (int)((float)((utcNow - this_chimp.rotationStart).TotalSeconds / (this_chimp.rotationEnd - this_chimp.rotationStart).TotalSeconds) * (float)(siegeTowerRotationArray.Length - 1));
		return siegeTowerRotationArray[num] + 713 - 1;
	}

	private int lerpBatteringRam(Chimp this_chimp)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow >= this_chimp.rotationEnd)
		{
			this_chimp.rotating = false;
			this_chimp.rotation8LastFacing = this_chimp.rotation8TargetFacing;
			return 0;
		}
		int[] siegeTowerRotationArray = getSiegeTowerRotationArray(this_chimp.rotation8LastFacing, this_chimp.rotation8TargetFacing);
		int num = (int)((float)((utcNow - this_chimp.rotationStart).TotalSeconds / (this_chimp.rotationEnd - this_chimp.rotationStart).TotalSeconds) * (float)(siegeTowerRotationArray.Length - 1));
		return siegeTowerRotationArray[num] + 274 - 1;
	}

	private int lerpFireBallista(Chimp this_chimp)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow >= this_chimp.rotationEnd)
		{
			this_chimp.rotating = false;
			this_chimp.rotation8LastFacing = this_chimp.rotation8TargetFacing;
			return 0;
		}
		int[] siegeTowerRotationArray = getSiegeTowerRotationArray(this_chimp.rotation8LastFacing, this_chimp.rotation8TargetFacing);
		int num = (int)((float)((utcNow - this_chimp.rotationStart).TotalSeconds / (this_chimp.rotationEnd - this_chimp.rotationStart).TotalSeconds) * (float)(siegeTowerRotationArray.Length - 1));
		return siegeTowerRotationArray[num] + 313 - 1;
	}

	public void deleteChimp(int objectID)
	{
		if (chimps.TryGetValue(objectID, out var value))
		{
			deleteChimp(value);
		}
	}

	public void deleteChimp(Chimp this_chimp, bool removeFromDictionary = true)
	{
		if (this_chimp.gameObject7_enabled)
		{
			ObjectPool.instance.PoolObject(0, this_chimp.gameObject7);
			this_chimp.sprRenderer7 = null;
			this_chimp.gameObject7 = null;
			this_chimp.gameObject7_enabled = false;
		}
		if (this_chimp.gameObject6_enabled)
		{
			ObjectPool.instance.PoolObject(0, this_chimp.gameObject6);
			this_chimp.sprRenderer6 = null;
			this_chimp.gameObject6 = null;
			this_chimp.gameObject6_enabled = false;
		}
		if (this_chimp.gameObject5_enabled)
		{
			ObjectPool.instance.PoolObject(0, this_chimp.gameObject5);
			this_chimp.sprRenderer5 = null;
			this_chimp.gameObject5 = null;
			this_chimp.gameObject5_enabled = false;
		}
		if (this_chimp.gameObject4_enabled)
		{
			ObjectPool.instance.PoolObject(0, this_chimp.gameObject4);
			this_chimp.sprRenderer4 = null;
			this_chimp.gameObject4 = null;
			this_chimp.gameObject4_enabled = false;
		}
		if (this_chimp.gameObject3_enabled)
		{
			ObjectPool.instance.PoolObject(0, this_chimp.gameObject3);
			this_chimp.sprRenderer3 = null;
			this_chimp.gameObject3 = null;
			this_chimp.gameObject3_enabled = false;
		}
		if (this_chimp.gameObject2_enabled)
		{
			ObjectPool.instance.PoolObject(0, this_chimp.gameObject2);
			this_chimp.sprRenderer2 = null;
			this_chimp.gameObject2 = null;
			this_chimp.gameObject2_enabled = false;
		}
		ObjectPool.instance.PoolObject(0, this_chimp.gameObject, ObjectPool.getContainerID(this_chimp.row, this_chimp.column), this_chimp.objectID | 0x8000000);
		this_chimp.sprRenderer = null;
		this_chimp.gameObject = null;
		if (removeFromDictionary)
		{
			chimps.Remove(this_chimp.objectID);
		}
	}

	private int getCatapultFacing(int image)
	{
		if (image >= 1 && image < 225)
		{
			return (image - 1) % 8 + 1;
		}
		if (image >= 449 && image < 578)
		{
			return (image - 449) % 8 + 1;
		}
		if (image >= 705 && image < 713)
		{
			return -1;
		}
		return 1;
	}

	private int getFireBallistaFacing(int image)
	{
		if (image >= 1 && image < 305)
		{
			return (image - 1) % 8 + 1;
		}
		return -1;
	}

	private int getBatteringRamFacing(int image)
	{
		if (image >= 1 && image < 9)
		{
			return (image - 1) % 8 + 1;
		}
		if (image >= 17 && image < 273)
		{
			return (image - 17) % 8 + 1;
		}
		if (image == 273)
		{
			return -1;
		}
		return 1;
	}

	private int getSiegeTowerFacing(int image, int image2, int image3)
	{
		if (image == 85 || image2 == 85 || image3 == 85)
		{
			return -1;
		}
		if (image >= 1 && image < 9)
		{
			return image;
		}
		if (image2 >= 1 && image2 < 9)
		{
			return image2;
		}
		if (image3 >= 1 && image3 < 9)
		{
			return image3;
		}
		return 1;
	}

	private int[] getSiegeTowerRotationArray(int currentFacing, int targetFacing)
	{
		currentFacing--;
		targetFacing--;
		if (currentFacing < 0 || currentFacing >= 8)
		{
			currentFacing = ((targetFacing < 0 || targetFacing >= 8) ? (targetFacing = 0) : targetFacing);
		}
		else if (targetFacing < 0 || targetFacing >= 8)
		{
			targetFacing = currentFacing;
		}
		return siegeTowerRotations[currentFacing * 8 + targetFacing];
	}

	public void addUpdateFly(int _objectID, int x, int y, int tile_x, int tile_y, float heightAboveGround, int file, int image, int _colour, int _transparency, int _layerDelay, int _gameObjectType, int _infoType, int _infoData)
	{
		if (_transparency < 0)
		{
			_transparency = 0;
		}
		Vector3 spritePosVector = getSpritePosVector(x, y, tile_x, tile_y, _objectID);
		int row = getRow(x, y, _layerDelay);
		int column = 0;
		spritePosVector.y += heightAboveGround;
		if (flies.ContainsKey(_objectID))
		{
			Fly fly = flies[_objectID];
			if (fly.type != file || fly.state != image || fly.colour != _colour || fly.transparency != _transparency)
			{
				switch (_gameObjectType)
				{
				case 5:
				case 6:
				case 40:
				case 41:
				case 42:
				case 43:
				case 44:
				case 45:
				case 46:
				case 47:
				{
					float a = (float)(32 - _transparency) / 32f;
					fly.transparency = _transparency;
					UnityEngine.Color color = fly.sprRenderer.color;
					color.a = a;
					fly.sprRenderer.color = color;
					if (fly.gameObject2 != null)
					{
						fly.sprRenderer2.color = color;
						UnityEngine.Color color2 = new UnityEngine.Color(0f, 0f, 0f, a);
						if (fly.gameObjectDigit1 != null)
						{
							fly.sprRendererDigit1.color = color2;
						}
						if (fly.gameObjectDigit2 != null)
						{
							fly.sprRendererDigit2.color = color2;
						}
						if (fly.gameObjectDigit3 != null)
						{
							fly.sprRendererDigit3.color = color2;
						}
						if (fly.gameObjectDigit4 != null)
						{
							fly.sprRendererDigit4.color = color2;
						}
						if (fly.gameObjectDigit5 != null)
						{
							fly.sprRendererDigit5.color = color2;
						}
					}
					if (fly.type != file || fly.state != image)
					{
						fly.type = file;
						fly.state = image;
						fly.colour = _colour;
						SpriteMapping.SetBodySprite(fly.sprRenderer, file, image, _colour, altFrame: false, 0, _transparency);
						if (_objectID >= 10000)
						{
							fly.sprRenderer.color = new UnityEngine.Color(0f, 0f, 0f, (float)(32 - _transparency) / 32f);
						}
					}
					break;
				}
				default:
				{
					fly.type = file;
					fly.state = image;
					fly.colour = _colour;
					fly.transparency = _transparency;
					bool flag = true;
					if ((file == 118 || file == 144) && !fly.gameObject.transform.parent.gameObject.activeSelf)
					{
						flag = false;
					}
					if (flag)
					{
						SpriteMapping.SetBodySprite(fly.sprRenderer, file, image, _colour, altFrame: false, 0, _transparency);
						if (_objectID >= 10000)
						{
							fly.sprRenderer.color = new UnityEngine.Color(0f, 0f, 0f, (float)(32 - _transparency) / 32f);
						}
					}
					break;
				}
				}
			}
			if (fly.mapX != x || fly.mapY != y || fly.layerDelay != _layerDelay)
			{
				fly.sprRenderer.sortingOrder = -20000 + row * 49 + 15;
				fly.mapX = x;
				fly.mapY = y;
				fly.layerDelay = _layerDelay;
			}
			fly.tileX = tile_x;
			fly.tileY = tile_y;
			fly.gameObject.transform.position = spritePosVector;
			int containerID = ObjectPool.getContainerID(row, column);
			int containerID2 = ObjectPool.getContainerID(fly.row, fly.column);
			if (containerID != containerID2)
			{
				ObjectPool.instance.moveRow(0, fly.gameObject, containerID2, containerID, fly.objectID | 0x4000000);
				fly.row = row;
				fly.column = column;
			}
			return;
		}
		GameObject objectForType = ObjectPool.instance.GetObjectForType(0, "simpleSprite", ObjectPool.getContainerID(row, column), _objectID | 0x4000000);
		if (!(objectForType != null))
		{
			return;
		}
		objectForType.transform.position = spritePosVector;
		SpriteRenderer component = objectForType.GetComponent<SpriteRenderer>();
		component.sortingOrder = -20000 + row * 49 + 15;
		SpriteMapping.SetBodySprite(component, file, image, _colour, altFrame: false, 0, _transparency);
		if (_objectID >= 10000)
		{
			component.color = new UnityEngine.Color(0f, 0f, 0f, (float)(32 - _transparency) / 32f);
		}
		Fly fly2 = new Fly
		{
			gameObject = objectForType,
			sprRenderer = component,
			mapX = x,
			mapY = y,
			tileX = tile_x,
			tileY = tile_y,
			type = file,
			state = image,
			colour = _colour,
			objectID = _objectID,
			layerDelay = _layerDelay,
			transparency = _transparency,
			gameObjectType = _gameObjectType,
			infoType = _infoType,
			infoData = _infoData,
			row = row,
			column = column
		};
		if (_gameObjectType >= 40 && _gameObjectType <= 47)
		{
			if (fly2.gameObject2 == null)
			{
				GameObject objectForType2 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType2 != null)
				{
					objectForType2.transform.parent = fly2.gameObject.transform;
					switch (_infoType)
					{
					case 128:
					case 129:
					case 130:
						objectForType2.transform.localPosition = new Vector3(-0.75f, 0f, 0f);
						objectForType2.transform.localScale = new Vector3(1f, 1f, 1f);
						break;
					case 171:
						objectForType2.transform.localPosition = new Vector3(-0.75f, 0f, 0f);
						objectForType2.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
						break;
					default:
						objectForType2.transform.localPosition = new Vector3(-0.75f, 0f, 0f);
						objectForType2.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
						break;
					}
					SpriteRenderer component2 = objectForType2.GetComponent<SpriteRenderer>();
					component2.sortingOrder = -20000 + row * 49 + 15 + 1;
					_transparency -= 2;
					if (_transparency < 0)
					{
						_transparency = 0;
					}
					SpriteMapping.SetBodySprite(component2, 202, _infoType, 0, altFrame: false, 0, _transparency);
					fly2.gameObject2 = objectForType2;
					fly2.sprRenderer2 = component2;
				}
			}
			bool flag2 = false;
			if (_infoData < 0)
			{
				_infoData = -_infoData;
				flag2 = true;
			}
			int num = 1;
			if (_infoData >= 10)
			{
				num++;
			}
			if (_infoData >= 100)
			{
				num++;
			}
			if (_infoData >= 1000)
			{
				num++;
			}
			if (_infoData >= 10000)
			{
				num++;
				if (_infoData >= 100000)
				{
					_infoData = 99999;
				}
			}
			float a2 = (float)(32 - _transparency) / 32f;
			bool flag3 = false;
			if (num >= 1)
			{
				GameObject objectForType3 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType3 != null)
				{
					objectForType3.transform.parent = fly2.gameObject.transform;
					objectForType3.transform.localPosition = new Vector3(0.95f, 0f, 0f);
					objectForType3.transform.localScale = new Vector3(1f, 1f, 1f);
					SpriteRenderer component3 = objectForType3.GetComponent<SpriteRenderer>();
					component3.sortingOrder = -20000 + row * 49 + 15 + 2;
					_transparency -= 2;
					if (_transparency < 0)
					{
						_transparency = 0;
					}
					SpriteMapping.SetBodySprite(component3, 202, _infoData % 10 + 2, 0, altFrame: false, 0, _transparency);
					component3.color = new UnityEngine.Color(0f, 0f, 0f, a2);
					fly2.gameObjectDigit1 = objectForType3;
					fly2.sprRendererDigit1 = component3;
				}
			}
			if (num == 1 && flag2)
			{
				flag3 = true;
			}
			if (num >= 2 || (num == 1 && flag3))
			{
				GameObject objectForType4 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType4 != null)
				{
					objectForType4.transform.parent = fly2.gameObject.transform;
					objectForType4.transform.localPosition = new Vector3(0.65f, 0f, 0f);
					objectForType4.transform.localScale = new Vector3(1f, 1f, 1f);
					SpriteRenderer component4 = objectForType4.GetComponent<SpriteRenderer>();
					component4.sortingOrder = -20000 + row * 49 + 15 + 2;
					_transparency -= 2;
					if (_transparency < 0)
					{
						_transparency = 0;
					}
					if (flag3)
					{
						SpriteMapping.SetBodySprite(component4, 202, 12, 0, altFrame: false, 0, _transparency);
					}
					else
					{
						SpriteMapping.SetBodySprite(component4, 202, _infoData / 10 % 10 + 2, 0, altFrame: false, 0, _transparency);
					}
					component4.color = new UnityEngine.Color(0f, 0f, 0f, a2);
					fly2.gameObjectDigit2 = objectForType4;
					fly2.sprRendererDigit2 = component4;
				}
			}
			if (num == 2 && flag2)
			{
				flag3 = true;
			}
			if (num >= 3 || (num == 2 && flag3))
			{
				GameObject objectForType5 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType5 != null)
				{
					objectForType5.transform.parent = fly2.gameObject.transform;
					objectForType5.transform.localPosition = new Vector3(0.35f, 0f, 0f);
					objectForType5.transform.localScale = new Vector3(1f, 1f, 1f);
					SpriteRenderer component5 = objectForType5.GetComponent<SpriteRenderer>();
					component5.sortingOrder = -20000 + row * 49 + 15 + 2;
					_transparency -= 2;
					if (_transparency < 0)
					{
						_transparency = 0;
					}
					if (flag3)
					{
						SpriteMapping.SetBodySprite(component5, 202, 12, 0, altFrame: false, 0, _transparency);
					}
					else
					{
						SpriteMapping.SetBodySprite(component5, 202, _infoData / 100 % 10 + 2, 0, altFrame: false, 0, _transparency);
					}
					component5.color = new UnityEngine.Color(0f, 0f, 0f, a2);
					fly2.gameObjectDigit3 = objectForType5;
					fly2.sprRendererDigit3 = component5;
				}
			}
			if (num == 3 && flag2)
			{
				flag3 = true;
			}
			if (num >= 4 || (num == 3 && flag3))
			{
				GameObject objectForType6 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType6 != null)
				{
					objectForType6.transform.parent = fly2.gameObject.transform;
					objectForType6.transform.localPosition = new Vector3(0.05f, 0f, 0f);
					objectForType6.transform.localScale = new Vector3(1f, 1f, 1f);
					SpriteRenderer component6 = objectForType6.GetComponent<SpriteRenderer>();
					component6.sortingOrder = -20000 + row * 49 + 15 + 2;
					_transparency -= 2;
					if (_transparency < 0)
					{
						_transparency = 0;
					}
					if (flag3)
					{
						SpriteMapping.SetBodySprite(component6, 202, 12, 0, altFrame: false, 0, _transparency);
					}
					else
					{
						SpriteMapping.SetBodySprite(component6, 202, _infoData / 1000 % 10 + 2, 0, altFrame: false, 0, _transparency);
					}
					component6.color = new UnityEngine.Color(0f, 0f, 0f, a2);
					fly2.gameObjectDigit4 = objectForType6;
					fly2.sprRendererDigit4 = component6;
				}
			}
			if (num == 4 && flag2)
			{
				flag3 = true;
			}
			if (num >= 5 || (num == 4 && flag3))
			{
				GameObject objectForType7 = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
				if (objectForType7 != null)
				{
					objectForType7.transform.parent = fly2.gameObject.transform;
					objectForType7.transform.localPosition = new Vector3(-0.25f, 0f, 0f);
					objectForType7.transform.localScale = new Vector3(1f, 1f, 1f);
					SpriteRenderer component7 = objectForType7.GetComponent<SpriteRenderer>();
					component7.sortingOrder = -20000 + row * 49 + 15 + 2;
					_transparency -= 2;
					if (_transparency < 0)
					{
						_transparency = 0;
					}
					if (flag3)
					{
						SpriteMapping.SetBodySprite(component7, 202, 12, 0, altFrame: false, 0, _transparency);
					}
					else
					{
						SpriteMapping.SetBodySprite(component7, 202, _infoData / 10000 % 10 + 2, 0, altFrame: false, 0, _transparency);
					}
					component7.color = new UnityEngine.Color(0f, 0f, 0f, a2);
					fly2.gameObjectDigit5 = objectForType7;
					fly2.sprRendererDigit5 = component7;
				}
			}
		}
		flies[_objectID] = fly2;
	}

	public void deleteFly(int objectID)
	{
		if (flies.TryGetValue(objectID, out var value))
		{
			deleteFly(value);
		}
	}

	public void deleteFly(Fly this_fly, bool removeFromDictionary = true)
	{
		if (this_fly.gameObject2 != null)
		{
			ObjectPool.instance.PoolObject(0, this_fly.gameObject2);
			this_fly.sprRenderer2 = null;
			this_fly.gameObject2 = null;
		}
		if (this_fly.gameObjectDigit1 != null)
		{
			ObjectPool.instance.PoolObject(0, this_fly.gameObjectDigit1);
			this_fly.sprRendererDigit1 = null;
			this_fly.gameObjectDigit1 = null;
		}
		if (this_fly.gameObjectDigit2 != null)
		{
			ObjectPool.instance.PoolObject(0, this_fly.gameObjectDigit2);
			this_fly.sprRendererDigit2 = null;
			this_fly.gameObjectDigit2 = null;
		}
		if (this_fly.gameObjectDigit3 != null)
		{
			ObjectPool.instance.PoolObject(0, this_fly.gameObjectDigit3);
			this_fly.sprRendererDigit3 = null;
			this_fly.gameObjectDigit3 = null;
		}
		if (this_fly.gameObjectDigit4 != null)
		{
			ObjectPool.instance.PoolObject(0, this_fly.gameObjectDigit4);
			this_fly.sprRendererDigit4 = null;
			this_fly.gameObjectDigit4 = null;
		}
		if (this_fly.gameObjectDigit5 != null)
		{
			ObjectPool.instance.PoolObject(0, this_fly.gameObjectDigit5);
			this_fly.sprRendererDigit5 = null;
			this_fly.gameObjectDigit5 = null;
		}
		ObjectPool.instance.PoolObject(0, this_fly.gameObject, ObjectPool.getContainerID(this_fly.row, this_fly.column), this_fly.objectID | 0x4000000);
		this_fly.sprRenderer = null;
		this_fly.gameObject = null;
		if (removeFromDictionary)
		{
			flies.Remove(this_fly.objectID);
		}
	}

	public void addUpdateOrg(int objectID, int x, int y, float heightAboveGround, int file, int image, int colour, int _transparency)
	{
		if (_transparency < 0)
		{
			_transparency = -1 - _transparency;
		}
		int tileX = 14;
		int tileY = 8;
		Vector3 spritePosVector = getSpritePosVector(x, y, tileX, tileY, objectID);
		int row = getRow(x, y);
		int num = 0;
		spritePosVector.y += heightAboveGround;
		if (orgs.ContainsKey(objectID))
		{
			Org org = orgs[objectID];
			if (org.type != file || org.state != image || org.colour != colour || org.transparency != _transparency)
			{
				org.type = file;
				org.state = image;
				org.colour = colour;
				org.transparency = _transparency;
				SpriteMapping.SetBodySprite(org.sprRenderer, file, image, colour, altFrame: false, 0, _transparency);
			}
			if (org.mapX != x || org.mapY != y)
			{
				GameMapTile mapTile = getMapTile(x, y);
				if (mapTile != null)
				{
					org.sprRenderer.sortingOrder = -20000 + mapTile.row * 49 + 4 + mapTile.column % 4;
				}
				org.mapX = x;
				org.mapY = y;
			}
			org.tileX = tileX;
			org.tileY = tileY;
			org.gameObject.transform.position = spritePosVector;
			int containerID = ObjectPool.getContainerID(row, num);
			int containerID2 = ObjectPool.getContainerID(org.row, org.column);
			if (containerID != containerID2)
			{
				ObjectPool.instance.moveRow(0, org.gameObject, containerID2, containerID, org.objectID | 0x40000000);
				org.row = row;
				org.column = num;
			}
		}
		else
		{
			GameObject objectForType = ObjectPool.instance.GetObjectForType(1, "simpleSprite", ObjectPool.getContainerID(row, num), objectID | 0x40000000);
			if (objectForType != null)
			{
				objectForType.transform.position = spritePosVector;
				SpriteRenderer component = objectForType.GetComponent<SpriteRenderer>();
				component.sortingOrder = -20000 + row * 49 + 4 + num % 4;
				SpriteMapping.SetBodySprite(component, file, image, colour, altFrame: false, 0, _transparency);
				Org value = new Org
				{
					gameObject = objectForType,
					sprRenderer = component,
					mapX = x,
					mapY = y,
					tileX = tileX,
					tileY = tileY,
					type = file,
					state = image,
					colour = colour,
					objectID = objectID,
					transparency = _transparency,
					row = row,
					column = num
				};
				orgs[objectID] = value;
			}
		}
	}

	public void addUpdateWhiteCap(int objectID, int x, int y, int image)
	{
		int tileX = 14;
		int tileY = 8;
		Vector3 spritePosVector = getSpritePosVector(x, y, tileX, tileY, objectID);
		if (orgs.ContainsKey(objectID))
		{
			Org org = orgs[objectID];
			if (org.state != image)
			{
				org.state = image;
				SpriteMapping.SetBodySprite(org.sprRenderer, 173, image, 0, altFrame: false, 0, 0);
			}
			return;
		}
		int row = getRow(x, y);
		int column = getColumn(x, y);
		GameObject objectForType = ObjectPool.instance.GetObjectForType(1, "simpleSprite", ObjectPool.getContainerID(row, column), objectID | 0x40000000);
		if (objectForType != null)
		{
			objectForType.transform.position = spritePosVector;
			SpriteRenderer component = objectForType.GetComponent<SpriteRenderer>();
			component.sortingOrder = -20000 + row * 49 + 4;
			SpriteMapping.SetBodySprite(component, 173, image, 0, altFrame: false, 0, 0);
			Org value = new Org
			{
				gameObject = objectForType,
				sprRenderer = component,
				mapX = x,
				mapY = y,
				tileX = tileX,
				tileY = tileY,
				state = image,
				objectID = objectID,
				row = row,
				column = column
			};
			orgs[objectID] = value;
		}
	}

	public void deleteOrg(int objectID)
	{
		if (orgs.TryGetValue(objectID, out var value))
		{
			deleteOrg(value);
		}
	}

	public void deleteOrg(Org this_org, bool removeFromDictionary = true)
	{
		ObjectPool.instance.PoolObject(1, this_org.gameObject, ObjectPool.getContainerID(this_org.row, this_org.column), this_org.objectID | 0x40000000);
		this_org.sprRenderer = null;
		this_org.gameObject = null;
		if (removeFromDictionary)
		{
			orgs.Remove(this_org.objectID);
		}
	}

	public void addUpdateBuildingAnim(int _objectID, int x, int y, int tile_x, int tile_y, int animLayer, int file, int image, int _colour, int _transparency, int _layerDelay, bool hasSubSpecial = false, int halfPixelX = 0, int halfPixelY = 0)
	{
		if (_transparency < 0)
		{
			_transparency = 0;
		}
		int num = -20000;
		num = ((animLayer >= 25) ? 1 : (16 + animLayer));
		Vector3 spritePosVector = getSpritePosVector(x, y, 0, 0, _objectID, halfPixelX, halfPixelY);
		BuildingAnim buildingAnim = null;
		if (buildingAnims.ContainsKey(_objectID))
		{
			buildingAnim = buildingAnims[_objectID];
			if (buildingAnim.type != file || buildingAnim.state != image || buildingAnim.colour != _colour || buildingAnim.transparency != _transparency)
			{
				buildingAnim.type = file;
				buildingAnim.state = image;
				buildingAnim.colour = _colour;
				buildingAnim.transparency = _transparency;
				SpriteMapping.SetBodySprite(buildingAnim.sprRenderer, file, image, _colour, altFrame: false, 0, _transparency);
			}
			if (buildingAnim.mapX != x || buildingAnim.mapY != y || buildingAnim.layerDelay != _layerDelay)
			{
				getMapTile(x, y);
				buildingAnim.sprRenderer.sortingOrder = -20000 + getRow(x, y, _layerDelay) * 49 + num;
				buildingAnim.mapX = x;
				buildingAnim.mapY = y;
				buildingAnim.layerDelay = _layerDelay;
			}
			buildingAnim.tileX = tile_x;
			buildingAnim.tileY = tile_y;
		}
		else
		{
			int row = getRow(x, y, _layerDelay);
			int column = getColumn(x, y);
			GameObject objectForType = ObjectPool.instance.GetObjectForType(0, "simpleSprite", ObjectPool.getContainerID(row, column), _objectID | 0x10000000);
			if (objectForType != null)
			{
				SpriteRenderer component = objectForType.GetComponent<SpriteRenderer>();
				component.sortingOrder = -20000 + row * 49 + num;
				SpriteMapping.SetBodySprite(component, file, image, _colour, altFrame: false, 0, _transparency);
				buildingAnim = new BuildingAnim
				{
					gameObject = objectForType,
					sprRenderer = component,
					mapX = x,
					mapY = y,
					tileX = tile_x,
					tileY = tile_y,
					type = file,
					state = image,
					colour = _colour,
					objectID = _objectID,
					layerDelay = _layerDelay,
					transparency = _transparency,
					subSpecial = hasSubSpecial,
					row = row,
					column = column
				};
				buildingAnims[_objectID] = buildingAnim;
			}
		}
		if (buildingAnim != null && buildingAnim.gameObject != null)
		{
			buildingAnim.gameObject.transform.position = new Vector3(spritePosVector.x + (float)tile_x / 32f, spritePosVector.y + (float)tile_y / 32f, spritePosVector.z);
		}
	}

	public void deleteBuildingAnim(int objectID)
	{
		if (buildingAnims.TryGetValue(objectID, out var value))
		{
			if (value.subSpecial)
			{
				deleteBuildingAnim(objectID + 1000000);
			}
			deleteBuildingAnim(value);
		}
	}

	public void deleteBuildingAnim(BuildingAnim this_building, bool removeFromDictionary = true)
	{
		ObjectPool.instance.PoolObject(0, this_building.gameObject, ObjectPool.getContainerID(this_building.row, this_building.column), this_building.objectID | 0x10000000);
		this_building.sprRenderer = null;
		this_building.gameObject = null;
		if (removeFromDictionary)
		{
			buildingAnims.Remove(this_building.objectID);
		}
	}

	public void addUpdateWallFillin(int _objectID, int x, int y, float heightAboveGround, int image, int xoffset)
	{
		Vector3 spritePosVector = getSpritePosVector(x, y, 0, 0, _objectID);
		spritePosVector.x += (float)xoffset / 32f;
		spritePosVector.y += heightAboveGround;
		if (wallFillins.ContainsKey(_objectID))
		{
			WallFillin wallFillin = wallFillins[_objectID];
			if (wallFillin.state != image)
			{
				wallFillin.state = image;
				SpriteMapping.SetBodySprite(wallFillin.sprRenderer, 54, image, 0, altFrame: false, 0, 0);
			}
			if (wallFillin.mapX != x || wallFillin.mapY != y)
			{
				wallFillin.sprRenderer.sortingOrder = -20000 + getRow(x, y) * 49 + 2;
				wallFillin.mapX = x;
				wallFillin.mapY = y;
			}
			wallFillin.gameObject.transform.position = spritePosVector;
			return;
		}
		int row = getRow(x, y);
		int column = getColumn(x, y);
		GameObject objectForType = ObjectPool.instance.GetObjectForType(0, "simpleSprite", ObjectPool.getContainerID(row, column), _objectID + 536870912);
		if (objectForType != null)
		{
			objectForType.transform.position = spritePosVector;
			SpriteRenderer component = objectForType.GetComponent<SpriteRenderer>();
			component.sortingOrder = -20000 + row * 49 + 2;
			SpriteMapping.SetBodySprite(component, 54, image, 0, altFrame: false, 0, 0);
			WallFillin value = new WallFillin
			{
				gameObject = objectForType,
				sprRenderer = component,
				mapX = x,
				mapY = y,
				state = image,
				objectID = _objectID,
				row = row,
				column = column
			};
			wallFillins[_objectID] = value;
		}
	}

	public void deleteWallFillin(int objectID)
	{
		if (wallFillins.TryGetValue(objectID, out var value))
		{
			deleteWallFillin(value);
		}
	}

	public void deleteWallFillin(WallFillin this_wallfillin, bool removeFromDictionary = true)
	{
		ObjectPool.instance.PoolObject(0, this_wallfillin.gameObject, ObjectPool.getContainerID(this_wallfillin.row, this_wallfillin.column), this_wallfillin.objectID | 0x20000000);
		this_wallfillin.sprRenderer = null;
		this_wallfillin.gameObject = null;
		if (removeFromDictionary)
		{
			wallFillins.Remove(this_wallfillin.objectID);
		}
	}

	public void addUpdatePixie(int _objectID, int x, int y, int tile_x, int tile_y, float heightAboveGround, int file, int image, bool hiMode, int _transparency, int _layerDelay, int _colour)
	{
		Vector3 spritePosVector = getSpritePosVector(x, y, 0, 0, _objectID);
		int troopType = 0;
		switch (file)
		{
		case 194:
		case 199:
			troopType = 74;
			break;
		case 215:
			troopType = 85;
			break;
		case 214:
			troopType = 84;
			break;
		case 211:
			troopType = 81;
			break;
		case 210:
			troopType = 80;
			break;
		case 209:
			troopType = 79;
			break;
		case 208:
			troopType = 78;
			break;
		case 213:
			troopType = 83;
			break;
		case 212:
			troopType = 82;
			break;
		case 190:
			troopType = 70;
			break;
		case 191:
			troopType = 71;
			break;
		case 192:
			troopType = 72;
			break;
		case 196:
			troopType = 76;
			break;
		case 18:
			troopType = 22;
			break;
		case 41:
			troopType = 23;
			break;
		case 40:
			troopType = 25;
			break;
		case 42:
			troopType = 27;
			break;
		case 86:
			troopType = 30;
			break;
		case 106:
			troopType = 5;
			break;
		case 35:
			troopType = 29;
			break;
		case 109:
			troopType = 37;
			break;
		case 121:
			troopType = 60;
			break;
		}
		Vector2 pivotOffset = getPivotOffset(troopType);
		spritePosVector.y += heightAboveGround;
		if (pixies.ContainsKey(_objectID))
		{
			Pixie pixie = pixies[_objectID];
			if (pixie.type != file || pixie.state != image || pixie.transparency != _transparency || pixie.colour != _colour)
			{
				pixie.type = file;
				pixie.state = image;
				pixie.transparency = _transparency;
				pixie.colour = _colour;
				SpriteMapping.SetBodySprite(pixie.sprRenderer, file, image, _colour, altFrame: false, 0, _transparency);
			}
			if (pixie.mapX != x || pixie.mapY != y || pixie.layerDelay != _layerDelay)
			{
				if (hiMode)
				{
					pixie.sprRenderer.sortingOrder = -20000 + getRow(x, y, _layerDelay) * 49 + 41;
				}
				else
				{
					pixie.sprRenderer.sortingOrder = -20000 + getRow(x, y, _layerDelay) * 49 + 3;
				}
				pixie.mapX = x;
				pixie.mapY = y;
				pixie.layerDelay = _layerDelay;
			}
			pixie.gameObject.transform.position = new Vector3(spritePosVector.x + (float)tile_x / 32f + pivotOffset.x, spritePosVector.y + (float)tile_y / 32f + pivotOffset.y, spritePosVector.z);
			return;
		}
		GameObject objectForType = ObjectPool.instance.GetObjectForType(0, "simpleSprite");
		if (objectForType != null)
		{
			objectForType.transform.position = new Vector3(spritePosVector.x + (float)tile_x / 32f + pivotOffset.x, spritePosVector.y + (float)tile_y / 32f + pivotOffset.y, spritePosVector.z);
			SpriteRenderer component = objectForType.GetComponent<SpriteRenderer>();
			if (hiMode)
			{
				component.sortingOrder = -20000 + getRow(x, y, _layerDelay) * 49 + 41;
			}
			else
			{
				component.sortingOrder = -20000 + getRow(x, y, _layerDelay) * 49 + 3;
			}
			SpriteMapping.SetBodySprite(component, file, image, _colour, altFrame: false, 0, _transparency);
			Pixie value = new Pixie
			{
				gameObject = objectForType,
				sprRenderer = component,
				mapX = x,
				mapY = y,
				type = file,
				state = image,
				objectID = _objectID,
				transparency = _transparency,
				colour = _colour
			};
			pixies[_objectID] = value;
		}
	}

	public void deletePixie(int objectID)
	{
		if (pixies.TryGetValue(objectID, out var value))
		{
			deletePixie(value);
		}
	}

	public void deletePixie(Pixie this_pixie, bool removeFromDictionary = true)
	{
		ObjectPool.instance.PoolObject(0, this_pixie.gameObject);
		this_pixie.sprRenderer = null;
		this_pixie.gameObject = null;
		if (removeFromDictionary)
		{
			pixies.Remove(this_pixie.objectID);
		}
	}

	private void Update()
	{
		if (mouseCursorGO != null)
		{
			Vector2 vector = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			Vector3 position = new Vector3(vector.x + (float)mouseXOffset / 32f, vector.y - (float)mouseYOffset / 32f, 0f);
			mouseCursorGO.transform.position = position;
			mouseCursorGO2.transform.position = position;
			float num = 0.5f;
			mouseCursorGO.transform.localScale = new Vector3(num, num, num);
			mouseCursorGO2.transform.localScale = new Vector3(num, num, num);
		}
		else
		{
			createMouseCursors();
		}
		if (Director.instance.SimRunning)
		{
			lerpChimps();
			screenTilesWide = (int)((float)((Screen.width + 63) / 64) / PerfectPixelWithZoom.instance.getZoom());
			screenTilesHigh = (int)((float)((Screen.height + 15) / 16) / PerfectPixelWithZoom.instance.getZoom());
			Vector2Int screenCentreTileXY = CameraControls2D.instance.getScreenCentreTileXY(ScreenTilesWide, ScreenTilesHigh);
			screenCentreTileScreenSpaceX = screenCentreTileXY.x;
			screenCentreTileScreenSpaceY = screenCentreTileXY.y;
			if (PerfectPixelWithZoom.instance.getZoom() < 0.5f)
			{
				screenZoom = 0;
			}
			else
			{
				screenZoom = 1;
			}
			radarZoom = 1;
			_ = cachecCentreMapVector;
			Vector3Int vector3Int = cachecCentreTileMapVector;
			GameMapTile mapTile = instance.getMapTile(vector3Int.x, vector3Int.y);
			if (mapTile != null)
			{
				screenCentreTileX = mapTile.gameMapX;
				screenCentreTileY = mapTile.gameMapY;
			}
		}
	}

	public void mapGameTileToTilemapCoord(int gameX, int gameY, out int tileMapX, out int tileMapY)
	{
		int num = tilemapSize;
		int num2 = (800 - num) / 2;
		int num3 = num / 2;
		int num4 = num;
		tileMapX = gameX - num2;
		tileMapY = gameY - num2;
		switch (currentRotation)
		{
		case Enums.Dircs.East:
		{
			int num6 = tileMapY;
			tileMapY = tilemapSize - tileMapX;
			tileMapX = num6;
			tileMapX--;
			break;
		}
		case Enums.Dircs.South:
			tileMapX = tilemapSize - tileMapX;
			tileMapY = tilemapSize - tileMapY;
			tileMapX--;
			tileMapY--;
			break;
		case Enums.Dircs.West:
		{
			int num5 = tileMapY;
			tileMapY = tileMapX;
			tileMapX = tilemapSize - num5;
			tileMapY--;
			break;
		}
		}
		int num7 = tileMapX + tileMapY - num3;
		int num8 = tileMapX - num7 / 2;
		num7 = num4 - num7;
		tileMapX = num8 + num7 / 2;
		tileMapY = num7 + num3 - tileMapX;
		tileMapX--;
		tileMapY--;
	}

	public Enums.Dircs CurrentRotation()
	{
		return currentRotation;
	}

	public Enums.Dircs PendingRotation()
	{
		return pendingRotation;
	}

	public void setMapRotation(Enums.Dircs rotation, int centreX = -1, int centreY = -1, bool force = false)
	{
		if (currentRotation != rotation || force)
		{
			pendingRotation = rotation;
			pendingRotationtriggered = true;
			if (centreX == -1 || centreY == -1)
			{
				getRotationCentre(ref centreX, ref centreY);
			}
			EngineInterface.SetMapRotation(pendingRotation, centreX, centreY);
			Director.instance.forceEarlyEngine();
		}
	}

	public void getRotationCentre(ref int centreX, ref int centreY)
	{
		Vector3 mouseMapVector = Vector3.zero;
		Vector3Int mouseTileMapVector = Vector3Int.zero;
		int clickDepth = 0;
		CalcMapTileFromMousePos(new Vector3(Screen.width / 2, Screen.height / 2, 0f), ref mouseMapVector, ref mouseTileMapVector, ref clickDepth, useBuildingHeight: false);
		GameMapTile mapTile = instance.getMapTile(mouseTileMapVector.x, mouseTileMapVector.y);
		if (mapTile != null)
		{
			centreX = mapTile.gameMapX;
			centreY = mapTile.gameMapY;
		}
	}

	private void finishPendingRotate()
	{
		currentRotation = pendingRotation;
		setGameCoordMapping();
		clearSprites();
		pendingRotationtriggered = false;
		if (debugLayerRendering >= 0)
		{
			setupDebugRenderLayerMap(force: true);
			setDebugRendering(debugLayerRendering, force: true);
		}
		MainViewModel.Instance.IngameUI.setRotationImage(currentRotation);
	}

	public void RotateMapRight()
	{
		if (!Director.instance.MultiplayerGame || !Platform_Multiplayer.Instance.resyncingOrSaving)
		{
			switch (currentRotation)
			{
			case Enums.Dircs.North:
				setMapRotation(Enums.Dircs.West);
				break;
			case Enums.Dircs.East:
				setMapRotation(Enums.Dircs.North);
				break;
			case Enums.Dircs.South:
				setMapRotation(Enums.Dircs.East);
				break;
			case Enums.Dircs.West:
				setMapRotation(Enums.Dircs.South);
				break;
			}
			if (GameData.Instance.game_type == 4)
			{
				EngineInterface.TutorialAction(2);
			}
		}
	}

	public void RotateMapLeft()
	{
		if (!Director.instance.MultiplayerGame || !Platform_Multiplayer.Instance.resyncingOrSaving)
		{
			switch (currentRotation)
			{
			case Enums.Dircs.North:
				setMapRotation(Enums.Dircs.East);
				break;
			case Enums.Dircs.East:
				setMapRotation(Enums.Dircs.South);
				break;
			case Enums.Dircs.South:
				setMapRotation(Enums.Dircs.West);
				break;
			case Enums.Dircs.West:
				setMapRotation(Enums.Dircs.North);
				break;
			}
			if (GameData.Instance.game_type == 4)
			{
				EngineInterface.TutorialAction(2);
			}
		}
	}

	public void ToggleHealthBars()
	{
		healthbarsTopMost = !healthbarsTopMost;
		foreach (KeyValuePair<int, Chimp> chimp in chimps)
		{
			Chimp value = chimp.Value;
			if (value.sprRenderer5 != null)
			{
				if (!healthbarsTopMost)
				{
					value.sprRenderer5.sortingOrder = value.hpsRealSortOrder;
				}
				else
				{
					value.sprRenderer5.sortingOrder = 32767;
				}
			}
			if (value.sprRenderer6 != null)
			{
				if (!healthbarsTopMost || value.image6 < 400)
				{
					value.sprRenderer6.sortingOrder = value.hps2RealSortOrder;
				}
				else
				{
					value.sprRenderer6.sortingOrder = 32767;
				}
			}
		}
	}

	public void CalcMapTileFromMousePos(Vector3 mousePosition, ref Vector3 mouseMapVector, ref Vector3Int mouseTileMapVector, ref int clickDepth, bool useBuildingHeight = true, bool useHeight = true)
	{
		lastMouseLandscapeHeight = 8f;
		clickDepth = -32767;
		Vector3 position = new Vector3(mousePosition.x, mousePosition.y, mousePosition.z);
		int num = -800;
		if (!useBuildingHeight && !useHeight)
		{
			num = 0;
		}
		float zoom = PerfectPixelWithZoom.instance.getZoom();
		for (int i = num; i <= 0; i += 2)
		{
			position.y = mousePosition.y + (float)i;
			mouseMapVector = Camera.main.ScreenToWorldPoint(position);
			mouseTileMapVector = TilemapManager.instance.gameTileMap.WorldToCell(mouseMapVector);
			GameMapTile mapTile = getMapTile(mouseTileMapVector.x, mouseTileMapVector.y);
			if (mapTile == null)
			{
				continue;
			}
			float num2 = 0f;
			float num3 = 0f;
			if (useHeight)
			{
				if (useBuildingHeight)
				{
					if (mapTile.buildingHeight == 0f)
					{
						Sprite sprite = mapTile.mirrorTileImage;
						if (mapTile.constructionOrigImage != null)
						{
							sprite = mapTile.constructionOrigImage;
						}
						if (sprite != null && sprite.rect.height > 32f)
						{
							num2 = sprite.rect.height - 32f;
						}
					}
					else
					{
						num2 = mapTile.buildingHeight * 64f;
					}
				}
				num3 = mapTile.testHeight * 64f + num2;
			}
			if (!((float)i / zoom + num3 >= 0f))
			{
				continue;
			}
			if (useHeight)
			{
				lastMouseLandscapeHeight = -i;
				overTopHalf = true;
				if (mapTile.buildingHeight > 0f)
				{
					float num4 = mapTile.testHeight * 64f + mapTile.buildingHeight * 64f / 2f;
					if ((float)i / zoom + num4 >= 0f)
					{
						overTopHalf = false;
					}
					else
					{
						overTopHalf = true;
					}
				}
				else if (num2 > 0f)
				{
					float num5 = mapTile.testHeight * 64f - 90f;
					if ((float)i / zoom + num5 >= 0f)
					{
						overTopHalf = false;
					}
					else
					{
						overTopHalf = true;
					}
				}
			}
			clickDepth = -20000 + mapTile.row * 49;
			return;
		}
		mouseTileMapVector = new Vector3Int(-1, -1, -1);
	}

	public void getFixedHeightMouseOver(Vector3 mousePosition, out Vector3Int mouseTileMapVector, float fixedHeight)
	{
		Vector3 position = new Vector3(mousePosition.x, mousePosition.y, mousePosition.z);
		position.y = mousePosition.y - fixedHeight;
		Vector3 worldPosition = Camera.main.ScreenToWorldPoint(position);
		mouseTileMapVector = TilemapManager.instance.gameTileMap.WorldToCell(worldPosition);
		if (getMapTile(mouseTileMapVector.x, mouseTileMapVector.y) == null)
		{
			mouseTileMapVector = new Vector3Int(-1, -1, -1);
		}
	}

	public int[] grabTroopsOnScreen(Vector2 startPos, Vector2 endPos, ref int[] chimpsUnderCursor, Vector2 cursorPos, ref int depthFromUnder)
	{
		depthFromUnder = -32767;
		List<int> list = new List<int>();
		List<int> list2 = new List<int>();
		List<int> list3 = new List<int>();
		debugGrabbedChimps = 0;
		Vector3 vector = Camera.main.ScreenToWorldPoint(startPos);
		Vector3 vector2 = Camera.main.ScreenToWorldPoint(endPos);
		UnityEngine.Rect a = new UnityEngine.Rect(Math.Min(vector.x, vector2.x), Math.Min(vector.y, vector2.y), Math.Abs(vector.x - vector2.x), Math.Abs(vector.y - vector2.y));
		Vector3 vector3 = Camera.main.ScreenToWorldPoint(cursorPos);
		Vector2 point = new Vector2(vector3.x, vector3.y);
		bool flag = true;
		if (startPos != Vector2.zero || endPos != Vector2.zero)
		{
			flag = false;
		}
		if (flag)
		{
			point.y += 3f / 32f;
			foreach (KeyValuePair<int, Chimp> chimp in chimps)
			{
				Chimp value = chimp.Value;
				Vector3 position = value.position;
				if (!(point.x < position.x - value.bodyWidth / 2f) && !(point.x > position.x + value.bodyWidth / 2f) && !(point.y < position.y - value.bodyHeightBelow) && !(point.y > position.y + value.bodyHeight))
				{
					list2.Add(value.objectID);
					list3.Add(value.baseSortLayer);
				}
			}
		}
		else
		{
			UnityEngine.Rect b = new UnityEngine.Rect(0f, 0f, 0f, 0f);
			foreach (KeyValuePair<int, Chimp> chimp2 in chimps)
			{
				Chimp value2 = chimp2.Value;
				Vector3 position2 = value2.position;
				b.width = value2.bodyWidth;
				b.height = value2.bodyHeight + value2.bodyHeightBelow;
				b.x = position2.x - value2.bodyWidth / 2f;
				b.y = position2.y - 3f / 32f - value2.bodyHeightBelow;
				if (Intersect(a, b))
				{
					list.Add(value2.objectID);
				}
				if (b.Contains(point))
				{
					list2.Add(value2.objectID);
					list3.Add(value2.baseSortLayer);
				}
			}
		}
		if (list.Count > 1)
		{
			list.Sort();
		}
		if (list2.Count > 1)
		{
			int count = list2.Count;
			for (int i = 0; i < count - 1; i++)
			{
				bool flag2 = false;
				for (int j = 0; j < count - 1 - i; j++)
				{
					if (list3[j] < list3[j + 1])
					{
						int value3 = list3[j];
						list3[j] = list3[j + 1];
						list3[j + 1] = value3;
						value3 = list2[j];
						list2[j] = list2[j + 1];
						list2[j + 1] = value3;
						flag2 = true;
					}
				}
				if (!flag2)
				{
					break;
				}
			}
		}
		if (list2.Count > 0)
		{
			depthFromUnder = list3[0];
		}
		chimpsUnderCursor = list2.ToArray();
		debugGrabbedChimps = Math.Max(list.Count, list2.Count);
		return list.ToArray();
	}

	public int[] getChimpTypesFromChimpList(int[,] chimpList)
	{
		int[] array = new int[89];
		int length = chimpList.GetLength(0);
		for (int i = 0; i < length; i++)
		{
			array[chimpList[i, 1]]++;
		}
		return array;
	}

	public int[] filterChimpTypes(int[] chimpIDs, int[] chimpTypes)
	{
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		foreach (int num in chimpIDs)
		{
			dictionary[num] = num;
		}
		foreach (KeyValuePair<int, Chimp> chimp in chimps)
		{
			bool flag = true;
			if (chimp.Value.gameObjectType > 0 && chimp.Value.gameObjectType < chimpTypes.Length && chimpTypes[chimp.Value.gameObjectType] > 0)
			{
				flag = false;
			}
			if (flag)
			{
				dictionary.Remove(chimp.Key);
			}
		}
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, int> item in dictionary)
		{
			list.Add(item.Key);
		}
		return list.ToArray();
	}

	public static bool Intersect(UnityEngine.Rect a, UnityEngine.Rect b)
	{
		float num = Math.Max(a.x, b.x);
		if (Math.Min(a.x + a.width, b.x + b.width) >= num)
		{
			float num2 = Math.Max(a.y, b.y);
			if (Math.Min(a.y + a.height, b.y + b.height) >= num2)
			{
				return true;
			}
		}
		return false;
	}

	public bool setMouseTile(int mouseTileX, int mouseTileY, bool setValue)
	{
		GameMapTile mapTile = getMapTile(mouseTileX, mouseTileY);
		if (mapTile != null)
		{
			mapTile.mouseOver = setValue;
			TilemapManager.instance.startTileRefresh();
			TilemapManager.instance.triggerTMTileRefresh(new Vector2Int(mouseTileX, mouseTileY), mapTile.row, mapTile.column);
			TilemapManager.instance.endTileRefresh();
			return true;
		}
		return false;
	}

	public void setColourMapping(int[] colours)
	{
		SpriteMapping.SetRemapColours(colours);
	}

	public void PreCalcScreenCentre()
	{
		Vector3 mouseMapVector = Vector3.zero;
		Vector3Int mouseTileMapVector = Vector3Int.zero;
		int clickDepth = 0;
		CalcMapTileFromMousePos(new Vector3(Screen.width / 2, Screen.height / 2, 0f), ref mouseMapVector, ref mouseTileMapVector, ref clickDepth, useBuildingHeight: false, useHeight: false);
		cachecCentreMapVector = mouseMapVector;
		cachecCentreTileMapVector = mouseTileMapVector;
		float targetZoom = PerfectPixelWithZoom.instance.getTargetZoom();
		int num = (int)((float)((Screen.width + 63) / 64) / targetZoom);
		int num2 = (int)((float)((Screen.height + 15) / 16) / targetZoom);
		int row = getRow(mouseTileMapVector.x, mouseTileMapVector.y);
		int num3 = row - num2 / 2 - 2;
		int num4 = row + num2 / 2 + 4;
		int num5 = num4 + 51;
		if (num3 < 1)
		{
			num3 = 1;
		}
		if (num5 >= tilemapSize)
		{
			num5 = tilemapSize;
		}
		int column = getColumn(mouseTileMapVector.x, mouseTileMapVector.y);
		int num6 = column - num / 2 - 1;
		int num7 = column + num / 2 + 1;
		if (CameraControls2D.instance.horzMovement > 0)
		{
			num7 += (int)(5f / targetZoom);
		}
		else if (CameraControls2D.instance.horzMovement < 0)
		{
			num6 -= (int)(5f / targetZoom);
		}
		if (CameraControls2D.instance.vertMovement > 0)
		{
			num3 -= (int)(5f / targetZoom);
		}
		else if (CameraControls2D.instance.vertMovement < 0)
		{
			num5 += (int)(5f / targetZoom);
			num4 += (int)(5f / targetZoom);
		}
		if (num6 < 1)
		{
			num6 = 1;
		}
		if (num7 > tilemapSize)
		{
			num7 = tilemapSize;
		}
		if (!ignoreNextCachedBounds)
		{
			cachedRenderBoundsLeft = num6;
			cachedRenderBoundsRight = num7;
			cachedRenderBoundsTop = num3;
			cachedRenderBoundsBottom = num5;
		}
	}

	public void cameraMovedRecalcBounds(int cameraX, int cameraY, int newRotation = -1)
	{
		int tileMapX;
		int tileMapY;
		if (newRotation > 0)
		{
			Enums.Dircs dircs = currentRotation;
			currentRotation = (Enums.Dircs)newRotation;
			mapGameTileToTilemapCoord(cameraX, cameraY, out tileMapX, out tileMapY);
			currentRotation = dircs;
		}
		else
		{
			mapGameTileToTilemapCoord(cameraX, cameraY, out tileMapX, out tileMapY);
		}
		GameMapTile mapTile = getMapTile(tileMapX, tileMapY);
		if (mapTile != null)
		{
			int num = (int)((float)((Screen.width + 63) / 64) / PerfectPixelWithZoom.instance.getZoom());
			int num2 = (int)((float)((Screen.height + 15) / 16) / PerfectPixelWithZoom.instance.getZoom());
			int num3 = mapTile.row - num2 / 2 - 2;
			int num4 = mapTile.row + num2 / 2 + 4 + 51;
			num3 -= num2;
			num4 += num2;
			if (num3 < 1)
			{
				num3 = 1;
			}
			if (num4 >= tilemapSize)
			{
				num4 = tilemapSize;
			}
			int num5 = mapTile.column - num / 2 - 1;
			int num6 = mapTile.column + num / 2 + 1;
			num5 -= num;
			num6 += num;
			if (num5 < 1)
			{
				num5 = 1;
			}
			if (num6 > tilemapSize)
			{
				num6 = tilemapSize;
			}
			cachedRenderBoundsLeft = num5;
			cachedRenderBoundsRight = num6;
			cachedRenderBoundsTop = num3;
			cachedRenderBoundsBottom = num4;
			ignoreNextCachedBounds = true;
		}
	}

	public void experimentalRowManager()
	{
		int num = (int)((float)((Screen.width + 63) / 64) / PerfectPixelWithZoom.instance.getTargetZoom());
		int num2 = (int)((float)((Screen.height + 15) / 16) / PerfectPixelWithZoom.instance.getTargetZoom());
		Vector3Int vector3Int = cachecCentreTileMapVector;
		int row = getRow(vector3Int.x, vector3Int.y);
		int num3 = row - num2 / 2 - 2;
		int num4 = row + num2 / 2 + 4;
		int num5 = num4 + 51;
		if (num3 < 1)
		{
			num3 = 1;
		}
		if (num4 >= tilemapSize)
		{
			num4 = tilemapSize;
		}
		int column = getColumn(vector3Int.x, vector3Int.y);
		int num6 = column - num / 2 - 1;
		int num7 = column + num / 2 + 1;
		if (num6 < 1)
		{
			num6 = 1;
		}
		if (num7 > tilemapSize)
		{
			num7 = tilemapSize;
		}
		List<bool> list = new List<bool>();
		int num8 = num4;
		int num9 = 0;
		int num10 = num4 + 1;
		while (num9 < 52 && num10 < tilemapSize)
		{
			int num11 = 0;
			for (int i = num6; i < num7; i++)
			{
				if (num11 < pixelHeightMap[num10, i])
				{
					num11 = pixelHeightMap[num10, i];
				}
			}
			if (num11 > num9 * 8)
			{
				list.Add(item: true);
				num8 = num10;
			}
			else
			{
				list.Add(item: false);
			}
			num9++;
			num10++;
		}
		if (num8 + 14 < num5)
		{
			num5 = num8 + 14;
		}
		if (num5 >= tilemapSize)
		{
			num5 = tilemapSize;
		}
		TilemapManager.instance.filterRows(num3, num4, num6, num7, list);
		ObjectPool.instance.filterRows(num3, num5, num6, num7);
	}

	public static UnityEngine.Color Lighten(UnityEngine.Color inColor, float inAmount)
	{
		return new UnityEngine.Color(Math.Min(1f, inColor.r + 1f * inAmount), Math.Min(1f, inColor.g + 1f * inAmount), Math.Min(1f, inColor.b + 1f * inAmount), inColor.a);
	}

	public void debugoutput(string output)
	{
	}
}
