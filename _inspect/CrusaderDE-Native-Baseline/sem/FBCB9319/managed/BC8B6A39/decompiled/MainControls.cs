using CodeStage.AdvancedFPSCounter;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class MainControls : MonoBehaviour
{
	public static MainControls instance;

	private int fpsDisplayMode;

	private int UIDisplayMode = 1;

	public UnityEngine.Grid grid;

	private int currentAction;

	private int currentItemType;

	private int currentSubAction;

	private int currentSubAction2;

	private int currentSubActionData;

	private int currentSubActionPlayer;

	private int brushSize = 1;

	public bool overGUI;

	private Vector2 mouseMapPos;

	private bool offWorld = true;

	private Vector3 mouseMapVector = Vector3.zero;

	private Vector3 tileCenterVector = Vector3.zero;

	private Vector3Int mouseTileMapVector = Vector3Int.zero;

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

	private bool isActive = true;

	private const int panel1_Width = 300;

	private const int panel1_Height = 840;

	private int panel1_XPos = Screen.width - 300 - 6;

	private int panel1_YPos = 100;

	private const int panel1_Vert_Ofset = 32;

	private const int panel1_UI_Width = 276;

	private const int panel1_UI_Half_Width = 138;

	private const int panel1_UI_XOff = 5;

	private const int panel1_UI_Half_XOff = 143;

	private const int panel1_UI_YOff = 5;

	private Vector2 scrollPosition = Vector2.zero;

	private float lastMoatPitchHeight;

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

	private void Awake()
	{
		instance = this;
		if (instance == null)
		{
			instance = this;
		}
		else if (instance != this)
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		EditorDirector.instance.setActive(mode: true);
		isActive = true;
		CameraControls2D.instance.setNewPosition(new Vector3(18f, 122f, 0f));
		FatControler.instance.setInfoDisplayVisible(visible: false);
		AFPSCounter.Instance.deviceInfoCounter.Enabled = false;
		AFPSCounter.Instance.fpsCounter.Enabled = false;
		AFPSCounter.Instance.memoryCounter.Enabled = false;
	}

	private void Update()
	{
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

	private void computeMouseMapPosition()
	{
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
		TilemapManager.instance.translateTileToScreenCoords(mouseTileMapVector.x, mouseTileMapVector.y, 1, ref ssXPos, ref ssYPos);
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
		mapX = mouseTileMapVector.x;
		mapY = mouseTileMapVector.y;
	}

	public void getMousePosition(ref float x, ref float y)
	{
		x = mouseMapVector.x;
		y = mouseMapVector.y;
	}

	public void getMouseScreenPosition(ref float x, ref float y)
	{
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
		Vector3Int vector3Int = grid.WorldToCell(mapVector);
		return GameMap.instance.getRow(vector3Int.x, vector3Int.y) * 2 + 1;
	}

	public Vector3 getCellCentre(int x, int y)
	{
		Vector3 result = grid.CellToWorld(new Vector3Int(x, y, 0));
		result.y += 0.25f;
		return result;
	}

	public Vector3 getCellTop(int x, int y)
	{
		Vector3 result = grid.CellToWorld(new Vector3Int(x, y, 0));
		result.y += 0.5f;
		return result;
	}

	private void keyboardCommands()
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
			AFPSCounter.Instance.deviceInfoCounter.Enabled = false;
			AFPSCounter.Instance.fpsCounter.Enabled = false;
			AFPSCounter.Instance.memoryCounter.Enabled = false;
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
			MainViewModel.Instance.HUDMarkers.RefMarkerInvisible.Visibility = Visibility.Hidden;
			MainViewModel.Instance.HUDMarkers.RefMarkerVisible.Visibility = Visibility.Hidden;
			MainViewModel.Instance.HUDMarkers.RefMarkerDisappearing.Visibility = Visibility.Hidden;
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
