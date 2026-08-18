using CodeStage.AdvancedFPSCounter;
using CodeStage.AdvancedFPSCounter.CountersData;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class MainControls : MonoBehaviour
{
	public static MainControls instance;

	public int fpsDisplayMode;

	public int UIDisplayMode = 1;

	public Grid grid;

	public int currentAction;

	public int currentItemType;

	public int currentSubAction;

	public int currentSubAction2;

	public int currentSubActionData;

	public int currentSubActionPlayer;

	public int brushSize = 1;

	public bool overGUI;

	public Vector2 mouseMapPos;

	public bool offWorld = true;

	public Vector3 mouseMapVector = Vector3.zero;

	public Vector3 tileCenterVector = Vector3.zero;

	public Vector3Int mouseTileMapVector = Vector3Int.zero;

	public int mouseTileClickDepth;

	[HideInInspector]
	public int selectedEntity;

	[HideInInspector]
	public int selectedBuilding;

	[HideInInspector]
	public int selectedMapElement;

	[HideInInspector]
	public int shownDataMode;

	[HideInInspector]
	public string lastFilePath = "";

	[HideInInspector]
	public string lastFileName = "";

	public bool isActive = true;

	public const int panel1_Width = 300;

	public const int panel1_Height = 840;

	public int panel1_XPos = Screen.width - 300 - 6;

	public int panel1_YPos = 100;

	public const int panel1_Vert_Ofset = 32;

	public const int panel1_UI_Width = 276;

	public const int panel1_UI_Half_Width = 138;

	public const int panel1_UI_XOff = 5;

	public const int panel1_UI_Half_XOff = 143;

	public const int panel1_UI_YOff = 5;

	public Vector2 scrollPosition = Vector2.zero;

	public float lastMoatPitchHeight;

	public int CurrentAction
	{
		get
		{
			return currentAction;
		}
		set
		{
			currentAction = value;
		}
	}

	public int CurrentItemType
	{
		get
		{
			return currentItemType;
		}
		set
		{
			currentItemType = value;
		}
	}

	public int CurrentSubAction
	{
		get
		{
			return currentSubAction;
		}
		set
		{
			currentSubAction = value;
		}
	}

	public int CurrentSubAction2
	{
		get
		{
			return currentSubAction2;
		}
		set
		{
			currentSubAction2 = value;
		}
	}

	public int CurrentSubActionData
	{
		get
		{
			return currentSubActionData;
		}
		set
		{
			currentSubActionData = value;
		}
	}

	public int CurrentSubActionPlayer
	{
		get
		{
			return currentSubActionPlayer;
		}
		set
		{
			currentSubActionPlayer = value;
		}
	}

	public int BrushSize
	{
		get
		{
			return brushSize;
		}
		set
		{
			brushSize = value;
		}
	}

	public bool IsUIVisible => UIDisplayMode == 1;

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
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		EditorDirector.instance.setActive(mode: true);
		isActive = true;
		CameraControls2D.instance.setNewPosition(new Vector3(18f, 122f, 0f));
		FatControler.instance.setInfoDisplayVisible(visible: false);
		((BaseCounterData)AFPSCounter.Instance.deviceInfoCounter).Enabled = false;
		((BaseCounterData)AFPSCounter.Instance.fpsCounter).Enabled = false;
		((BaseCounterData)AFPSCounter.Instance.memoryCounter).Enabled = false;
	}

	public void Update()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		keyboardCommands();
		if (FatControler.currentScene != Enums.SceneIDS.ActualMainGame)
		{
			return;
		}
		computeMouseMapPosition();
		int num = (int)Input.mousePosition.x;
		int num2 = Screen.height - (int)Input.mousePosition.y;
		overGUI = false;
		if (!Director.instance.SimRunning)
		{
			if (num > panel1_XPos && num2 > panel1_YPos)
			{
				CameraControls2D.instance.debarZoom();
				overGUI = true;
			}
			else
			{
				_ = offWorld;
			}
		}
	}

	public void computeMouseMapPosition()
	{
		//IL_031f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
		int clickDepth = -32767;
		if ((CurrentAction == 5 && CurrentSubAction != 210 && CurrentSubAction != 211 && CurrentSubAction != 129 && CurrentSubAction != 120 && CurrentSubAction != 121 && CurrentSubAction != 122 && CurrentSubAction != 123 && CurrentSubAction != 124 && CurrentSubAction != 125 && CurrentSubAction != 126 && CurrentSubAction != 127 && CurrentSubAction != 128 && CurrentSubAction != 332 && CurrentSubAction != 333 && CurrentSubAction != 334 && CurrentSubAction != 335 && CurrentSubAction != 336 && CurrentSubAction != 337 && CurrentSubAction != 338 && CurrentSubAction != 367 && CurrentSubAction != 368 && CurrentSubAction != 369 && CurrentSubAction != 360 && CurrentSubAction != 361 && CurrentSubAction != 362 && CurrentSubAction != 363 && CurrentSubAction != 364 && CurrentSubAction != 365 && CurrentSubAction != 366 && CurrentSubAction != 391 && CurrentSubAction != 392 && CurrentSubAction != 393 && CurrentSubAction != 394 && CurrentSubAction != 395 && CurrentSubAction != 396 && CurrentSubAction != 397 && CurrentSubAction != 398 && CurrentSubAction != 370 && CurrentSubAction != 148) || CurrentAction == 3)
		{
			if (CurrentAction == 5 && (CurrentSubAction == 99 || CurrentSubAction == 106 || CurrentSubAction == 107 || CurrentSubAction == 45 || CurrentSubAction == 44))
			{
				int num = EditorDirector.instance.peekleftMouseStateForEngine();
				if (num == 0 || num == 1)
				{
					GameMap.instance.CalcMapTileFromMousePos(Input.mousePosition, ref mouseMapVector, ref mouseTileMapVector, ref clickDepth, useBuildingHeight: false);
					lastMoatPitchHeight = GameMap.instance.lastMouseLandscapeHeight;
				}
				else
				{
					GameMap.instance.getFixedHeightMouseOver(Input.mousePosition, out mouseTileMapVector, lastMoatPitchHeight);
				}
			}
			else
			{
				GameMap.instance.CalcMapTileFromMousePos(Input.mousePosition, ref mouseMapVector, ref mouseTileMapVector, ref clickDepth, useBuildingHeight: false);
			}
		}
		else
		{
			GameMap.instance.CalcMapTileFromMousePos(Input.mousePosition, ref mouseMapVector, ref mouseTileMapVector, ref clickDepth);
		}
		offWorld = false;
		float ssXPos = 0f;
		float ssYPos = 0f;
		TilemapManager.instance.translateTileToScreenCoords(((Vector3Int)(ref mouseTileMapVector)).x, ((Vector3Int)(ref mouseTileMapVector)).y, 1, ref ssXPos, ref ssYPos);
		float num2 = ssXPos;
		float num3 = ssXPos;
		float num4 = ssYPos;
		float num5 = ssYPos;
		tileCenterVector.x = mouseMapVector.x;
		tileCenterVector.y = mouseMapVector.y;
		if (tileCenterVector.x < num2)
		{
			tileCenterVector.x = num2;
		}
		else if (tileCenterVector.x > num3)
		{
			tileCenterVector.x = num3;
		}
		if (tileCenterVector.y < num4)
		{
			tileCenterVector.y = num4;
		}
		else if (tileCenterVector.y > num5)
		{
			tileCenterVector.y = num5;
		}
		mouseTileClickDepth = clickDepth;
	}

	public void getMouseMapTilePosition(ref float mapX, ref float mapY)
	{
		mapX = ((Vector3Int)(ref mouseTileMapVector)).x;
		mapY = ((Vector3Int)(ref mouseTileMapVector)).y;
	}

	public void getMousePosition(ref float x, ref float y)
	{
		x = mouseMapVector.x;
		y = mouseMapVector.y;
	}

	public void getMouseScreenPosition(ref float x, ref float y)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		x = Input.mousePosition.x;
		y = Input.mousePosition.y;
	}

	public void getMouseTileCentrePosition(ref float x, ref float y)
	{
		x = tileCenterVector.x;
		y = tileCenterVector.y;
	}

	public bool isOffWorld()
	{
		return offWorld;
	}

	public int getSortOrder(Vector3 mapVector)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		Vector3Int val = ((GridLayout)grid).WorldToCell(mapVector);
		return GameMap.instance.getRow(((Vector3Int)(ref val)).x, ((Vector3Int)(ref val)).y) * 2 + 1;
	}

	public Vector3 getCellCentre(int x, int y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		Vector3 result = ((GridLayout)grid).CellToWorld(new Vector3Int(x, y, 0));
		result.y += 0.25f;
		return result;
	}

	public Vector3 getCellTop(int x, int y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		Vector3 result = ((GridLayout)grid).CellToWorld(new Vector3Int(x, y, 0));
		result.y += 0.5f;
		return result;
	}

	public void keyboardCommands()
	{
	}

	public void forceUIState(bool state)
	{
		if (state)
		{
			UIDisplayMode = 0;
		}
		else
		{
			UIDisplayMode = 1;
		}
		toggleUIVisibility();
	}

	public void setUIState(bool state)
	{
		if ((!state || UIDisplayMode != 1) && (state || UIDisplayMode != 0))
		{
			if (state)
			{
				UIDisplayMode = 0;
			}
			else
			{
				UIDisplayMode = 1;
			}
			toggleUIVisibility();
		}
	}

	public void toggleUIVisibility()
	{
		if (++UIDisplayMode > 1)
		{
			UIDisplayMode = 0;
		}
		if (UIDisplayMode == 0)
		{
			EditorDirector.instance.setActive(mode: false);
			isActive = false;
			((BaseCounterData)AFPSCounter.Instance.deviceInfoCounter).Enabled = false;
			((BaseCounterData)AFPSCounter.Instance.fpsCounter).Enabled = false;
			((BaseCounterData)AFPSCounter.Instance.memoryCounter).Enabled = false;
			MainViewModel.Instance.SetVisibleState(state: false);
			GameMap.instance.SetUIVisibleState(state: false);
			CameraControls2D.instance.BoundsCheckCamera();
			if (GameData.Instance.game_type == 4)
			{
				EngineInterface.TutorialAction(17);
			}
		}
		else if (UIDisplayMode == 1)
		{
			EditorDirector.instance.setActive(mode: true);
			isActive = true;
			MainViewModel.Instance.SetVisibleState(state: true);
			GameMap.instance.SetUIVisibleState(state: true);
			if (GameData.Instance.game_type == 4)
			{
				EngineInterface.TutorialAction(17);
			}
		}
		PerfectPixelWithZoom.instance.UpdateCameraScale();
	}

	public void StopAllPlacement()
	{
		LineDrawing.instance.killHUDLines();
		CurrentAction = 0;
		if (MainViewModel.Instance.MEMode == 0)
		{
			for (int i = 0; i < 10; i++)
			{
				MainViewModel.Instance.MarkerSelected[i] = false;
			}
			((UIElement)MainViewModel.Instance.HUDMarkers.RefMarkerInvisible).Visibility = (Visibility)1;
			((UIElement)MainViewModel.Instance.HUDMarkers.RefMarkerVisible).Visibility = (Visibility)1;
			((UIElement)MainViewModel.Instance.HUDMarkers.RefMarkerDisappearing).Visibility = (Visibility)1;
		}
		EngineInterface.StartMapperItem(0);
	}

	public bool performMainClick()
	{
		if (!isActive)
		{
			return false;
		}
		if (CurrentAction == 0)
		{
			return false;
		}
		return true;
	}
}
