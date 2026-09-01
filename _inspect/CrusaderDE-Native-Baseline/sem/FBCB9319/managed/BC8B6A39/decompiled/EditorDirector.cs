using System;
using System.Collections;
using System.IO;
using CrusaderDE;
using Noesis;
using Steamworks;
using UnityEngine;

public class EditorDirector : MonoBehaviour
{
	public static EditorDirector instance;

	private static int DEBUG_UNIQUE;

	private Resolution lastRes;

	private bool isActive = true;

	private float mousePosX;

	private float mousePosY;

	private float mouseTileX;

	private float mouseTileY;

	[HideInInspector]
	public bool shiftPressed;

	[HideInInspector]
	public bool ctrlPressed;

	[HideInInspector]
	public bool altPressed;

	[HideInInspector]
	public bool allowLockedMapEditing;

	private int editorPlayerID = 1;

	private int gameLocalPlayerID = 1;

	[HideInInspector]
	public bool mapChanged;

	private float constantEditorPlacementRate = 1f / 60f;

	private Vector2 lastConstantPlacementPos = Vector2.zero;

	private bool constantEditorLimitSameTileRepeat;

	private Vector2 troopSelectMouseStart = Vector2.zero;

	private Vector2 troopSelectMouseCurrent = Vector2.zero;

	private int[] selectedChimpList;

	private int[] underCursorChimpList;

	private int[] onScreenChimpsList;

	private int[,] dllSelectedChimpList;

	private bool chimpListChanged;

	private bool scheduleTroopSelectionEnd;

	private bool troopSelectionBoxOn;

	public int lastTroopOverDepth = -1;

	private bool gotNewSelectionInfo;

	private int[] lastSelectedChimpList;

	private Vector3Int constantMousePosTile;

	private double actionLimitingTime;

	private int leftMouseStateForEngine;

	private int mousePosXForEngine = -1;

	private int mousePosYForEngine = -1;

	private bool stateRead = true;

	private bool rightDownForEngine;

	private bool rightUpForEngine;

	private bool upPending;

	private bool overNoesisUI;

	private DateTime mouseStateSetTime = DateTime.MinValue;

	private bool ignoreNextMouseDown;

	private DateTime ignoreNextMouseDownTime = DateTime.MinValue;

	public bool pendingMPExit = true;

	private DateTime recentFlattenLandscape = DateTime.MinValue;

	private int editorMouseState;

	private float editorOverLandHeight;

	private int lastConstantX = -1;

	private int lastConstantY = -1;

	private int lastConstantState;

	private DateTime lastConstantTime = DateTime.MinValue;

	private int lockedRulerX = -1;

	private int lockedRulerY = -1;

	private bool allowContinuousNoMouseConstant;

	public double ftime;

	private int currentFrames;

	private int lastFrames;

	private int currentTicks;

	private int lastTicks;

	public bool IsActive => isActive;

	public int ActivePlayerID
	{
		get
		{
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				return editorPlayerID;
			}
			return gameLocalPlayerID;
		}
	}

	public int CurrentFPS => lastFrames;

	public int CurrentTicks => lastTicks;

	public void SetLocalPlayer(int playerID)
	{
		gameLocalPlayerID = playerID;
	}

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
		lastRes = Screen.currentResolution;
		startFPS();
		FatControler.instance.SetNoesisKeyboardState(state: false);
		if (SteamManager.Initialized)
		{
			Platform_Multiplayer.Instance.RequestUserAvatar(SteamUser.GetSteamID());
		}
	}

	private void Start()
	{
	}

	private void OnDestroy()
	{
		if (Director.instance != null)
		{
			Director.instance.stopSimThread();
		}
		if (MyAudioManager.Instance != null)
		{
			MyAudioManager.Instance.StopAllGameSounds();
		}
	}

	public void log(string str)
	{
		Debug.Log(DEBUG_UNIQUE + " " + str);
		DEBUG_UNIQUE++;
	}

	private void Update()
	{
		if (FatControler.currentScene != Enums.SceneIDS.ActualMainGame)
		{
			if (Input.GetMouseButtonDown(0))
			{
				FatControler.MouseIsDownStroke = true;
			}
			else if (Input.GetMouseButtonUp(0))
			{
				FatControler.MouseIsUpStroke = true;
			}
			return;
		}
		updateFPS();
		if (lastRes.width != Screen.currentResolution.width || lastRes.height != Screen.currentResolution.height)
		{
			lastRes = Screen.currentResolution;
		}
		MainControls.instance.getMouseTileCentrePosition(ref mousePosX, ref mousePosY);
		MainControls.instance.getMouseMapTilePosition(ref mouseTileX, ref mouseTileY);
		int num = (int)Input.mousePosition.x;
		int num2 = Screen.height - (int)Input.mousePosition.y;
		mousePosXForEngine = num;
		mousePosYForEngine = num2;
		bool flag = false;
		bool flag2 = false;
		if (FatControler.instance.overNoesisGUI())
		{
			flag = true;
			flag2 = true;
		}
		if (GameData.scenario.InGameoverSituation)
		{
			flag = true;
		}
		overNoesisUI = flag;
		bool flag3 = false;
		int[] chimpsUnderCursor = null;
		if (Input.GetMouseButtonDown(0))
		{
			if (ignoreNextMouseDown && ignoreNextMouseDownTime > DateTime.UtcNow)
			{
				ignoreNextMouseDown = false;
			}
			else
			{
				FatControler.MouseIsDownStroke = true;
				if (!overNoesisUI)
				{
					updateLeftMouseStateForEngine(1);
				}
				if (MainControls.instance.CurrentAction == 9)
				{
					MainControls.instance.CurrentAction = 0;
				}
				if (MainControls.instance.CurrentAction == 0)
				{
					if (!overNoesisUI && (!MainViewModel.Instance.IsMapEditorMode || MainViewModel.Instance.MEMode != 0) && !HUD_Help.browserThumbHeld)
					{
						troopSelectMouseStart = Input.mousePosition;
					}
					else
					{
						troopSelectMouseStart = new Vector2(-1f, -1f);
					}
					int[] chimpsUnderCursor2 = null;
					int depthFromUnder = -1;
					onScreenChimpsList = GameMap.instance.grabTroopsOnScreen(Vector2.zero, new Vector2(Screen.width, Screen.height), ref chimpsUnderCursor2, Vector2.zero, ref depthFromUnder);
				}
				else
				{
					performEditorClick();
				}
				initConstantAction();
			}
		}
		else if (Input.GetMouseButton(0))
		{
			if (!overNoesisUI || MainControls.instance.CurrentAction == 8)
			{
				updateLeftMouseStateForEngine(2);
			}
			double timeAsDouble = Time.timeAsDouble;
			if (timeAsDouble - actionLimitingTime >= (double)constantEditorPlacementRate)
			{
				actionLimitingTime = timeAsDouble;
				GameMap.instance.getFixedHeightMouseOver(Input.mousePosition, out constantMousePosTile, editorOverLandHeight);
			}
			if (MainControls.instance.CurrentAction == 0 && Director.instance.SimRunning)
			{
				if (troopSelectMouseStart.x >= 0f && (Math.Abs(troopSelectMouseStart.x - Input.mousePosition.x) > 3f || Math.Abs(troopSelectMouseStart.y - Input.mousePosition.y) > 3f))
				{
					MainControls.instance.CurrentAction = 8;
					TroopSelector.instance.startSelection(troopSelectMouseStart, Input.mousePosition);
					troopSelectMouseCurrent = Input.mousePosition;
				}
			}
			else if (MainControls.instance.CurrentAction == 8)
			{
				TroopSelector.instance.updateSelection(Input.mousePosition);
				troopSelectMouseCurrent = Input.mousePosition;
				int depthFromUnder2 = lastTroopOverDepth;
				selectedChimpList = GameMap.instance.grabTroopsOnScreen(troopSelectMouseStart, troopSelectMouseCurrent, ref chimpsUnderCursor, Input.mousePosition, ref depthFromUnder2);
				lastTroopOverDepth = depthFromUnder2;
			}
		}
		else if (Input.GetMouseButtonUp(0))
		{
			FatControler.MouseIsUpStroke = true;
			if (MainControls.instance.CurrentAction == 8)
			{
				TroopSelector.instance.updateSelection(Input.mousePosition);
				troopSelectMouseCurrent = Input.mousePosition;
				int depthFromUnder3 = lastTroopOverDepth;
				selectedChimpList = GameMap.instance.grabTroopsOnScreen(troopSelectMouseStart, troopSelectMouseCurrent, ref chimpsUnderCursor, Input.mousePosition, ref depthFromUnder3);
				lastTroopOverDepth = depthFromUnder3;
				if (TroopSelector.instance.selection_on)
				{
					troopSelectionBoxOn = true;
				}
				TroopSelector.instance.endSelection();
				MainControls.instance.CurrentAction = 9;
				updateLeftMouseStateForEngine(3);
			}
			else if (!overNoesisUI)
			{
				updateLeftMouseStateForEngine(3);
			}
			else
			{
				updateLeftMouseStateForEngine(0);
			}
		}
		else
		{
			flag3 = true;
		}
		if (Input.GetMouseButtonDown(2) && (MainControls.instance.CurrentAction == 5 || MainControls.instance.CurrentAction == 3))
		{
			EngineInterface.GameAction(Enums.GameActionCommand.RotateBuilding, 0, 0);
		}
		gotNewSelectionInfo = true;
		allowContinuousNoMouseConstant = flag3;
		if (chimpsUnderCursor == null)
		{
			int depthFromUnder4 = lastTroopOverDepth;
			GameMap.instance.grabTroopsOnScreen(Vector2.zero, Vector2.zero, ref chimpsUnderCursor, Input.mousePosition, ref depthFromUnder4);
			lastTroopOverDepth = depthFromUnder4;
		}
		underCursorChimpList = chimpsUnderCursor;
		bool flag4 = false;
		if (GameData.Instance.game_type == 4 && (MainViewModel.Instance.HUDmain.currentTutorialArrow == 3 || MainViewModel.Instance.HUDmain.currentTutorialArrow == 4))
		{
			flag4 = true;
		}
		if (!overNoesisUI && !flag4)
		{
			if (Input.GetMouseButtonUp(1))
			{
				rightUpForEngine = true;
			}
			else if (Input.GetMouseButtonDown(1))
			{
				bool flag5 = true;
				if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.lordOnlySelected > 0 && !ConfigSettings.Settings_SH1RTSControls)
				{
					flag5 = false;
				}
				if (flag5)
				{
					MainViewModel.Instance.HUDRightClick.Open();
				}
				MainControls.instance.StopAllPlacement();
				rightDownForEngine = true;
			}
		}
		else if (flag2 && FatControler.instance.overBuildingMenu && Input.GetMouseButtonDown(1))
		{
			if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.app_mode == 14)
			{
				switch (GameData.Instance.lastGameState.app_sub_mode)
				{
				case 33:
				case 34:
					MainViewModel.Instance.HUDmain.NewBuildScreenSubTownRtn(null, null);
					break;
				case 17:
				case 18:
				case 19:
					MainViewModel.Instance.HUDmain.NewBuildScreenGates(null, null);
					break;
				case 11:
				case 12:
				case 14:
					MainViewModel.Instance.HUDmain.NewBuildScreenCastle();
					break;
				default:
					if (MainViewModel.Instance.IsMapEditorMode && MainViewModel.Instance.SubMode == 13)
					{
						MainViewModel.Instance.HUDmain.NewBuildScreenCastle();
					}
					break;
				}
			}
			else if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.app_mode == 16)
			{
				int app_sub_mode = GameData.Instance.lastGameState.app_sub_mode;
				if ((uint)(app_sub_mode - 53) <= 4u)
				{
					MainViewModel.Instance.ButtonNewTradeType("0");
				}
			}
		}
		else if (flag2 && Input.GetMouseButtonDown(1))
		{
			if (MainViewModel.Instance.AlliesPanelVisible && ((!MainViewModel.Instance.Allies_SendGoodsViewVis && !MainViewModel.Instance.Allies_RequestGoodsViewVis) || !FatControler.instance.overNoesisGUITag("RightIgnore")))
			{
				MainViewModel.Instance.AlliesPanelVisible = false;
			}
			if (MainViewModel.Instance.MeritPanelVisible)
			{
				MainViewModel.Instance.MeritPanelVisible = false;
			}
		}
		if (scheduleTroopSelectionEnd)
		{
			TroopSelector.instance.endSelection();
			scheduleTroopSelectionEnd = false;
		}
		if (chimpListChanged)
		{
			dllSelectedChimpsChanged();
			chimpListChanged = false;
		}
		shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		altPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
	}

	public void GamePaused()
	{
		TroopSelector.instance.endSelection();
		scheduleTroopSelectionEnd = false;
		selectedChimpList = null;
		MainControls.instance.CurrentAction = 0;
	}

	public void IgnoreNextMouseDown()
	{
		ignoreNextMouseDown = true;
		ignoreNextMouseDownTime = DateTime.UtcNow.AddSeconds(0.5);
	}

	public void clearMouseStateForEngine()
	{
		leftMouseStateForEngine = 0;
		stateRead = true;
		upPending = false;
	}

	public bool overUI()
	{
		return overNoesisUI;
	}

	private int getMouseStateForEngine(ref bool rightDown, ref bool rightUp)
	{
		rightDown = rightDownForEngine;
		rightDownForEngine = false;
		rightUp = rightUpForEngine;
		rightUpForEngine = false;
		int result = leftMouseStateForEngine;
		stateRead = true;
		if (upPending)
		{
			upPending = false;
			leftMouseStateForEngine = 3;
		}
		else if (leftMouseStateForEngine == 1)
		{
			leftMouseStateForEngine = 2;
		}
		else if (leftMouseStateForEngine == 3)
		{
			leftMouseStateForEngine = 0;
		}
		if (overUI() && MainControls.instance.CurrentAction != 8 && MainControls.instance.CurrentAction != 9)
		{
			return 0;
		}
		return result;
	}

	public void updateLeftMouseStateForEngine(int state)
	{
		if ((state == 2 || state == 3) && leftMouseStateForEngine == 0)
		{
			return;
		}
		if (!stateRead && leftMouseStateForEngine > 0 && (DateTime.UtcNow - mouseStateSetTime).TotalMilliseconds < 500.0)
		{
			if (state == 3)
			{
				upPending = true;
			}
		}
		else
		{
			leftMouseStateForEngine = state;
			stateRead = false;
			mouseStateSetTime = DateTime.UtcNow;
		}
	}

	public int peekleftMouseStateForEngine()
	{
		return leftMouseStateForEngine;
	}

	public Vector2Int GetMapMousePos()
	{
		GameMapTile mapTile = GameMap.instance.getMapTile((int)mouseTileX, (int)mouseTileY);
		if (mapTile != null)
		{
			return new Vector2Int(mapTile.gameMapX, mapTile.gameMapY);
		}
		return new Vector2Int(-1, -1);
	}

	public void LoadCampaignMap(int loadMapFile, int difficulty)
	{
		HUD_LoadSaveRequester.ClearSavedName();
		if (loadMapFile <= 20)
		{
			HUD_LoadSaveRequester.ClearSavedName(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MISSION_NAMES, loadMapFile));
		}
		else
		{
			HUD_LoadSaveRequester.ClearSavedName(Translate.Instance.lookUpText((Enums.eTextSections)(270 + (loadMapFile - 21) * 4), 1));
		}
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		EngineInterface.LoadMapReturnData retData = EngineInterface.loadCampaignMap(loadMapFile, difficulty);
		if (retData.errorCode == 1)
		{
			EngineInterface.SetUTF8MapName("mission" + loadMapFile);
			postLoading(retData);
			AchievementsCommon.Instance.ResetOnMissionStart();
		}
	}

	public void LoadSkirmishMap(int trailType, int trailID, int difficulty)
	{
		string mapName = "";
		switch (trailType)
		{
		case 0:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, 1 + trailID);
			break;
		case 1:
			mapName = trailID + 51 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, 51 + trailID);
			break;
		case 2:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, 81 + trailID);
			break;
		case 11:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(7 + trailID));
			break;
		case 12:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(12 + trailID));
			break;
		case 13:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(19 + trailID));
			break;
		case 14:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(28 + trailID));
			break;
		case 15:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(39 + trailID));
			break;
		case 16:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(49 + trailID));
			break;
		case 17:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(82 + trailID));
			break;
		case 18:
			mapName = trailID + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(92 + trailID));
			break;
		}
		HUD_LoadSaveRequester.ClearSavedName(mapName);
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		int[] trailMissionLords = EngineInterface.getTrailMissionLords(trailType, trailID);
		for (int i = 1; i < 9; i++)
		{
			if (trailMissionLords[i] > 0)
			{
				AIVLoader.UploadDefaultAIV(trailMissionLords[i] - 1, i);
			}
		}
		EngineInterface.LoadMapReturnData retData = EngineInterface.loadSkirmishTrailMap(trailType, trailID, difficulty);
		if (retData.errorCode == 1)
		{
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				EngineInterface.GameAction(Enums.GameActionCommand.SandsCheatAllowed, 1, 1);
			}
			Director.instance.StartSkirmishModeGame();
			postLoading(retData);
			AchievementsCommon.Instance.ResetOnMissionStart();
			SpriteMapping.BuildPlayerColourMapping(ConfigSettings.Settings_PlayerColour + 1, force: true);
			EngineInterface.SetMPRadarColours(SpriteMapping.remapColours);
			Director.instance.DelayCentreKeep();
		}
	}

	public bool LoadCustomTrailMap(string CustomTrailName, int missionID, int difficulty)
	{
		HUD_LoadSaveRequester.ClearSavedName(missionID + 1 + ". " + CustomTrailName);
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		FileHeader headerFromCustomTrail = MapFileManager.Instance.GetHeaderFromCustomTrail(CustomTrailName, FRONT_ManageTrail.GetMakerFileName(missionID - 1));
		if (headerFromCustomTrail == null)
		{
			return false;
		}
		if (headerFromCustomTrail.restartInfo != null)
		{
			MainViewModel.Instance.HUDIngameMenu.restartMapInfo = headerFromCustomTrail.restartInfo;
		}
		if (headerFromCustomTrail.hasRestartSkirmishInfo)
		{
			FileHeader fileInfoFromFileName = MapFileManager.Instance.GetFileInfoFromFileName(headerFromCustomTrail.filePath, headerFromCustomTrail.filePath, 4, loadRestartInfo: true);
			if (fileInfoFromFileName.restartSkirmishInfo != null)
			{
				fileInfoFromFileName.restartSkirmishInfo.selectedHeader = headerFromCustomTrail;
				fileInfoFromFileName.restartSkirmishInfo.customTrail = true;
				fileInfoFromFileName.restartSkirmishInfo.customTrailLevel = missionID;
				fileInfoFromFileName.restartSkirmishInfo.customTrailName = CustomTrailName;
				fileInfoFromFileName.restartSkirmishInfo.customTrailDifficulty = difficulty;
				MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = fileInfoFromFileName.restartSkirmishInfo;
				MainViewModel.Instance.FRONTMultiplayer.StartCustomTrailMission(fileInfoFromFileName.restartSkirmishInfo, headerFromCustomTrail);
				return true;
			}
		}
		return false;
	}

	public void createNewMap(int size, Enums.GameModes mode, bool siege_that, bool multiPlayerMap = false)
	{
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		EngineInterface.LoadMapReturnData initData = EngineInterface.newMapEditor(size, (int)mode, siege_that, multiPlayerMap);
		if (!siege_that || initData.errorCode == 1)
		{
			MainViewModel.Instance.HUDmain.sheildButtons[0].IsChecked = true;
			EngineInterface.SetUTF8MapName("");
			GameData.Instance.currentFileName = "";
			GameData.Instance.lastGameState = null;
			GameData.Instance.InitGameInfo(initData);
			GameData.Instance.NewMapForEditor();
			GameMap.instance.newMapLoaded(initData.mapSize);
			SFXManager.instance.resetFreebuildMessages();
			MainViewModel.Instance.IngameUI.setRotationImage(Enums.Dircs.North);
			MainViewModel.Instance.DefaultMapEditorUIGameAction();
			OnScreenText.Instance.initOST();
			GameData.Instance.SetScenarioOverview(EngineInterface.GetScenarioOverview());
			FatControler.instance.InitScenarioEditorValues();
			clearFakeUI();
			Director.instance.startSimThread();
			SetEditorPlayerID(1);
			SetLocalPlayer(1);
			SpriteMapping.BuildPlayerColourMapping(1);
			PerfectPixelWithZoom.instance.ResetZoom();
			manageScenarioEditorButtons();
			InitEditorMode();
			if (siege_that)
			{
				MainViewModel.Instance.HUDmain.SetEditorModeButtonVisibilityForSiegeThatMode(visible: false);
			}
			else
			{
				MainViewModel.Instance.HUDmain.SetEditorModeButtonVisibilityForSiegeThatMode(visible: true);
			}
			mapChanged = false;
			Director.instance.ClearWaitCursor();
			EngineInterface.setFlattenedLandscapeMode(state: false);
		}
	}

	public void changeMapSize(int newMapSize)
	{
		if (Director.instance.SafeToSave(wait: true))
		{
			if (EngineInterface.EditorChangeMap_MapSize(newMapSize) >= 0)
			{
				GameMap.instance.wipeRadar();
				Director.instance.stopSimThread();
				GameMap.instance.changeMapSize(newMapSize);
				clearFakeUI();
				Director.instance.startSimThread();
			}
			Director.instance.SetPausedState(state: false);
		}
	}

	public void stopGameSim(bool leavingScene = false)
	{
		if (Director.instance.SimRunning)
		{
			if (Director.instance.MultiplayerGame)
			{
				pendingMPExit = true;
				StartCoroutine(ExitMP());
			}
			GameData.Instance.NewMapForEditor();
			clearFakeUI();
			MainViewModel.Instance.Show_HUD_MPInviteWarning = false;
			GameMap.instance.wipeRadar();
			MainViewModel.Instance.TestRadarMap();
			SFXManager.instance.stopBink();
			bool leaveMusicPlaying = false;
			if (SFXManager.instance.lastMusic.Contains("astrongspice"))
			{
				leaveMusicPlaying = true;
			}
			MyAudioManager.Instance.StopAllGameSounds(leaveMusicPlaying);
			Director.instance.stopSimThread();
			manageScenarioEditorButtons();
			SFXManager.instance.resetFreebuildMessages();
			if (HUD_MPInviteWarning.PendingMPInvite && !leavingScene)
			{
				HUD_MPInviteWarning.PendingMPInvite = false;
				Platform_Multiplayer.Instance.ResumeInvite();
			}
			MainViewModel.Instance.HUDScenario.StartExitAnim();
		}
	}

	private IEnumerator ExitMP()
	{
		Platform_Multiplayer.Instance.LeaveGame();
		yield return new WaitForSeconds(1f);
		if (pendingMPExit)
		{
			Platform_Multiplayer.Instance.exitMP();
		}
	}

	public void MPGameKilled()
	{
		StartCoroutine(KillMP());
	}

	private IEnumerator KillMP()
	{
		yield return new WaitForSeconds(1f);
		instance.stopGameSim();
		yield return new WaitForSeconds(1f);
		MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
	}

	public void clearFakeUI()
	{
		OnScreenText.Instance.initOST();
	}

	public void loadSaveGame(string filename, string mapName, FileHeader header)
	{
		MainViewModel.Instance.HUDIngameMenu.restartMapInfo = null;
		MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = null;
		MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		EngineInterface.LoadMapReturnData retData = EngineInterface.LoadSaveFile(filename);
		if (retData.errorCode != 1)
		{
			return;
		}
		if (header.restartInfo != null)
		{
			MainViewModel.Instance.HUDIngameMenu.restartMapInfo = header.restartInfo;
		}
		if (header.hasRestartSkirmishInfo)
		{
			FileHeader fileInfoFromFileName = MapFileManager.Instance.GetFileInfoFromFileName(filename, filename, 0, loadRestartInfo: true);
			if (fileInfoFromFileName.restartSkirmishInfo != null)
			{
				MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = fileInfoFromFileName.restartSkirmishInfo;
			}
		}
		GameData.Instance.getCachedTrailBriefingFromSave(header);
		AchievementsCommon.Instance.UpdateAfterLoadingASave(header.achFood, header.achWood, header.achWeapons);
		EngineInterface.SetUTF8MapName(mapName);
		postLoading(retData, startGameThread: true, fromSaveGame: true);
	}

	public bool loadMapIntoEditor(string filename, string mapName)
	{
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		EngineInterface.LoadMapReturnData initData = EngineInterface.LoadMapFile(filename, editorMode: true);
		if (initData.errorCode == 1)
		{
			MainViewModel.Instance.HUDmain.sheildButtons[0].IsChecked = true;
			EngineInterface.SetUTF8MapName(mapName);
			GameData.Instance.SetScenarioOverview(EngineInterface.GetScenarioOverview());
			FatControler.instance.InitScenarioEditorValues();
			GameData.Instance.lastGameState = null;
			GameData.Instance.InitGameInfo(initData);
			GameData.Instance.currentFileName = System.IO.Path.GetFileNameWithoutExtension(filename);
			GameMap.instance.newMapLoaded(initData.mapSize);
			MainViewModel.Instance.DefaultMapEditorUIGameAction();
			OnScreenText.Instance.initOST();
			clearFakeUI();
			Director.instance.startSimThread();
			SetEditorPlayerID(1);
			SetLocalPlayer(1);
			SpriteMapping.BuildPlayerColourMapping(1);
			PerfectPixelWithZoom.instance.ResetZoom();
			GameMap.instance.setMapRotation((Enums.Dircs)initData.mapRotation, initData.mapRotationCentreX, initData.mapRotationCentreY, force: true);
			MainViewModel.Instance.IngameUI.setRotationImage((Enums.Dircs)initData.mapRotation);
			InitEditorMode();
			if (!GameData.Instance.showAlternateMissionTextForBriefing)
			{
				EngineInterface.SetUTF8MissionText(GameData.Instance.utf8MissionText);
			}
			manageScenarioEditorButtons();
			if (GameData.Instance.game_type == 6)
			{
				MainViewModel.Instance.HUDmain.SetEditorModeButtonVisibilityForSiegeThatMode(visible: false);
			}
			else
			{
				MainViewModel.Instance.HUDmain.SetEditorModeButtonVisibilityForSiegeThatMode(visible: true);
			}
			Director.instance.ClearWaitCursor();
			EngineInterface.setFlattenedLandscapeMode(state: false);
			if (initData.loadedVersion < 222 && initData.multiplayerMap == 0)
			{
				MainViewModel.Instance.EditorWarningPanelVisible = true;
			}
			mapChanged = false;
			return true;
		}
		return false;
	}

	public void postLoading(EngineInterface.LoadMapReturnData retData, bool startGameThread = true, bool fromSaveGame = false)
	{
		MainViewModel.Instance.HUDMPChatMessages.ClearMPChat();
		GameData.Instance.lastGameState = null;
		GameData.Instance.InitGameInfo(retData);
		GameMap.instance.newMapLoaded(retData.mapSize);
		MainViewModel.Instance.DefaultGameUIGameAction(force: true);
		MainViewModel.Instance.HUDRoot.RefRadarMapGrid.Visibility = Visibility.Visible;
		MainViewModel.Instance.HUDRoot.RefReportsControlGrid.Visibility = Visibility.Visible;
		MainViewModel.Instance.BriefingFromStory = false;
		MainControls.instance.forceUIState(state: true);
		MainViewModel.Instance.HUDmain.RefreshBuildScreen();
		OnScreenText.Instance.initOST();
		SFXManager.instance.resetFreebuildMessages();
		MainViewModel.Instance.UpdateUITroopSprites(ConfigSettings.Settings_PlayerColour + 1, retData.arabicLord > 0);
		clearFakeUI();
		if (startGameThread)
		{
			Director.instance.startSimThread();
		}
		if (fromSaveGame && Director.instance.SkirmishModeGame && GameData.Instance.game_type != 0)
		{
			SpriteMapping.BuildSkirmishLoadColourMapping(new int[9] { 0, retData.radar_colour_mapping0, retData.radar_colour_mapping1, retData.radar_colour_mapping2, retData.radar_colour_mapping3, retData.radar_colour_mapping4, retData.radar_colour_mapping5, retData.radar_colour_mapping6, retData.radar_colour_mapping7 }, ConfigSettings.Settings_PlayerColour + 1);
		}
		else
		{
			SpriteMapping.BuildPlayerColourMapping(ConfigSettings.Settings_PlayerColour + 1);
		}
		PerfectPixelWithZoom.instance.ResetZoom();
		MainViewModel.Instance.HUDMPChatPanel.InitNewMPGame();
		GameMap.instance.setMapRotation((Enums.Dircs)retData.mapRotation, retData.mapRotationCentreX, retData.mapRotationCentreY, force: true);
		MainViewModel.Instance.IngameUI.setRotationImage((Enums.Dircs)retData.mapRotation);
		if (GameData.Instance.game_type == 2 && GameData.Instance.mapType == Enums.GameModes.BUILD)
		{
			SFXManager.instance.startFreebuildMessages();
		}
		Director.instance.ClearWaitCursor();
		EngineInterface.setFlattenedLandscapeMode(state: false);
		MainViewModel.Instance.IngameUI.clearVideos();
		mapChanged = false;
	}

	public void SaveSaveGameOrMap(string path, string mapName, bool lockMap = false, bool tempLockOnly = false, bool mapSave = false)
	{
		if (mapName != "")
		{
			EngineInterface.SetUTF8MapName(mapName);
		}
		Vector2Int screenXYForSaveCentring = CameraControls2D.instance.getScreenXYForSaveCentring((int)((float)(Screen.width / 64) / PerfectPixelWithZoom.instance.getZoom()), (int)((float)(Screen.height / 16) / PerfectPixelWithZoom.instance.getZoom()));
		int centreX = -1;
		int centreY = -1;
		GameMap.instance.getRotationCentre(ref centreX, ref centreY);
		EngineInterface.SaveSaveGame(path, screenXYForSaveCentring.x, screenXYForSaveCentring.y, centreX, centreY, lockMap, tempLockOnly, mapSave);
		GameData.Instance.currentFileName = System.IO.Path.GetFileNameWithoutExtension(mapName);
		mapChanged = false;
	}

	public void SaveCustomTrailMap(string mappath, string mapName, string trailpath, HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
	{
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		if (EngineInterface.LoadMapFile(mappath, editorMode: true).errorCode == 1)
		{
			if (mapName != "")
			{
				EngineInterface.SetUTF8MapName(mapName);
			}
			byte[] restartInfo2 = restartInfo.encode();
			EngineInterface.CreateTrailMission(trailpath, restartInfo2);
		}
		Director.instance.ClearWaitCursor();
	}

	public void manageScenarioEditorButtons()
	{
		if (Director.instance.SimRunning)
		{
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				MainViewModel.Instance.Show_HUD_Scenario_Button = true;
				MainViewModel.Instance.Show_HUD_Scenario = true;
			}
			else
			{
				MainViewModel.Instance.Show_HUD_Scenario = false;
				MainViewModel.Instance.Show_HUD_Scenario_Button = false;
			}
		}
		else
		{
			MainViewModel.Instance.Show_HUD_Scenario = false;
			MainViewModel.Instance.Show_HUD_Scenario_Button = false;
		}
	}

	public void setActive(bool mode)
	{
		isActive = mode;
	}

	public void preDLLCallActions(ref int mouseOverX, ref int mouseOverY)
	{
		bool rightDown = false;
		bool rightUp = false;
		int num = getMouseStateForEngine(ref rightDown, ref rightUp);
		int x = (int)mouseTileX;
		int y = (int)mouseTileY;
		GameMapTile gameMapTile = GameMap.instance.getMapTile(x, y);
		if (gameMapTile != null && (!overNoesisUI || MainControls.instance.CurrentAction == 9))
		{
			mouseOverX = gameMapTile.gameMapX;
			mouseOverY = gameMapTile.gameMapY;
		}
		else
		{
			gameMapTile = null;
			mouseOverX = -1;
			mouseOverY = -1;
		}
		performContinuousNoMouse();
		if (Director.instance.Paused)
		{
			num = 0;
		}
		if (rightDown && TroopSelector.instance.selection_on)
		{
			troopSelectionBoxOn = false;
			scheduleTroopSelectionEnd = true;
			num = 0;
		}
		if (MainControls.instance.CurrentAction == 9 && num == 3)
		{
			MainControls.instance.CurrentAction = 0;
		}
		if (!gotNewSelectionInfo)
		{
			selectedChimpList = lastSelectedChimpList;
		}
		gotNewSelectionInfo = false;
		int[] array = ((selectedChimpList == null) ? new int[0] : selectedChimpList);
		int[] array2 = new int[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array2[i] = array[i];
		}
		int[] array3 = ((underCursorChimpList == null) ? new int[0] : underCursorChimpList);
		int[] array4 = new int[array3.Length];
		for (int j = 0; j < array3.Length; j++)
		{
			array4[j] = array3[j];
		}
		EngineInterface.TroopSelection(onScreenChimps: (num != 1) ? new int[0] : onScreenChimpsList, mouseState: num, rightDown: rightDown, rightUp: rightUp, selectedChimps: array2, selection_on: TroopSelector.instance.selection_on || troopSelectionBoxOn, selection_established: TroopSelector.instance.selection_established, underCursorChimps: array4, mousePosX: mousePosXForEngine, mousePosY: mousePosYForEngine, overTopHalf: GameMap.instance.overTopHalf);
		if (MainControls.instance.CurrentAction != 9)
		{
			lastSelectedChimpList = array;
			selectedChimpList = null;
		}
		if (MainControls.instance.CurrentAction == 8 || MainControls.instance.CurrentAction == 9)
		{
			return;
		}
		troopSelectionBoxOn = false;
		selectedChimpList = null;
		if (MainControls.instance.CurrentAction == 0)
		{
			MainViewModel.Instance.CrossThreadRollover(0, "");
		}
		else
		{
			if (MainControls.instance.CurrentAction == 5 && (GameMap.instance.pendingRotationtriggered || recentFlattenLandscape > DateTime.UtcNow))
			{
				return;
			}
			if (MainControls.instance.CurrentAction == 5)
			{
				int num2 = GameData.getStructFromMapper(MainControls.instance.CurrentSubAction);
				int mapperToHelp = MainViewModel.Instance.getMapperToHelp(MainControls.instance.CurrentSubAction);
				string structString = "";
				if (mapperToHelp > 0)
				{
					structString = num2 switch
					{
						36 => (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, mapperToHelp) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 0), 
						37 => (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, mapperToHelp) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 1), 
						38 => (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, mapperToHelp) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 2), 
						_ => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, mapperToHelp), 
					};
				}
				else if (MainControls.instance.CurrentSubAction == 265)
				{
					structString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 204);
				}
				else if (MainControls.instance.CurrentSubAction == 266)
				{
					structString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 205);
				}
				else if (MainControls.instance.CurrentSubAction == 267 || MainControls.instance.CurrentSubAction == 268 || (MainControls.instance.CurrentSubAction >= 444 && MainControls.instance.CurrentSubAction <= 455))
				{
					structString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 206);
				}
				else if (MainControls.instance.CurrentSubAction == 195 || MainControls.instance.CurrentSubAction == 196 || MainControls.instance.CurrentSubAction == 197 || MainControls.instance.CurrentSubAction == 198)
				{
					KeyCode keyCode = KeyManager.instance.GetKeyCode(Enums.KeyFunctions.RotateBuilding, 0);
					string newValue = "[ " + HUD_Options.GetKeyCodeString(keyCode) + " ]";
					structString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 211).Replace("[KEY]", newValue);
				}
				if (num2 == 0)
				{
					if (MainControls.instance.CurrentSubAction == 46)
					{
						num2 = 110;
						structString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 15);
					}
					else if (MainControls.instance.CurrentSubAction == 25)
					{
						num2 = 111;
						structString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 12);
					}
					else if (MainControls.instance.CurrentSubAction == 26)
					{
						num2 = 112;
						structString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 13);
					}
					else if (MainControls.instance.CurrentSubAction == 27)
					{
						num2 = 113;
						structString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 14);
					}
				}
				if (!MainViewModel.Instance.Show_HUD_Troops)
				{
					MainViewModel.Instance.CrossThreadRollover(num2, structString);
				}
				else
				{
					MainViewModel.Instance.CrossThreadSiegeEngineRollover(num2);
				}
				if (MainControls.instance.CurrentSubAction == 25 || MainControls.instance.CurrentSubAction == 46 || MainControls.instance.CurrentSubAction == 26 || MainControls.instance.CurrentSubAction == 27 || MainControls.instance.CurrentSubAction == 106 || MainControls.instance.CurrentSubAction == 107 || MainControls.instance.CurrentSubAction == 44 || MainControls.instance.CurrentSubAction == 45 || MainControls.instance.CurrentSubAction == 99)
				{
					if (gameMapTile != null)
					{
						EngineInterface.PlaceMapperItem(MainControls.instance.CurrentSubAction, gameMapTile.gameMapX, gameMapTile.gameMapY, 0, ActivePlayerID, !MainViewModel.Instance.IsMapEditorMode, constructingOnly: true, num);
					}
					return;
				}
			}
			switch (num)
			{
			case 0:
				if (MainControls.instance.CurrentAction == 5 && gameMapTile != null)
				{
					EngineInterface.PlaceMapperItem(MainControls.instance.CurrentSubAction, gameMapTile.gameMapX, gameMapTile.gameMapY, 0, ActivePlayerID, !MainViewModel.Instance.IsMapEditorMode, constructingOnly: true, num);
				}
				if (MainControls.instance.CurrentAction == 7 && gameMapTile != null)
				{
					EngineInterface.PlaceMapperItem(MainControls.instance.CurrentSubAction, gameMapTile.gameMapX, gameMapTile.gameMapY, 0, ActivePlayerID, !MainViewModel.Instance.IsMapEditorMode, constructingOnly: true, num);
				}
				break;
			case 1:
				if (MainControls.instance.CurrentAction == 5 && gameMapTile != null)
				{
					EngineInterface.PlaceMapperItem(MainControls.instance.CurrentSubAction, gameMapTile.gameMapX, gameMapTile.gameMapY, 0, ActivePlayerID, !MainViewModel.Instance.IsMapEditorMode, constructingOnly: false, num);
					clearMouseStateForEngine();
				}
				if (MainControls.instance.CurrentAction == 6 && gameMapTile != null)
				{
					EngineInterface.DeleteBuilding(gameMapTile.gameMapX, gameMapTile.gameMapY, ActivePlayerID, inGameNotEditor: true, 1);
				}
				if (MainControls.instance.CurrentAction == 3 && gameMapTile != null)
				{
					ref Vector3Int reference = ref constantMousePosTile;
					int x2 = (constantMousePosTile.y = 0);
					reference.x = x2;
					int num6 = 0;
					if (MainControls.instance.BrushSize == 9)
					{
						num6 = 1;
					}
					else if (MainControls.instance.BrushSize == 25)
					{
						num6 = 2;
					}
					else if (MainControls.instance.BrushSize == 49)
					{
						num6 = 3;
					}
					else if (MainControls.instance.BrushSize == 81)
					{
						num6 = 4;
					}
					else if (MainControls.instance.BrushSize == 121)
					{
						num6 = 5;
					}
					else if (MainControls.instance.BrushSize == 169)
					{
						num6 = 6;
					}
					else if (MainControls.instance.BrushSize == 400)
					{
						num6 = 7;
					}
					else if (MainControls.instance.BrushSize == 625)
					{
						num6 = 8;
					}
					EngineInterface.PlaceMapperItem(MainControls.instance.CurrentSubAction, gameMapTile.gameMapX, gameMapTile.gameMapY, num6 + 1, ActivePlayerID, !MainViewModel.Instance.IsMapEditorMode, constructingOnly: false, num);
				}
				break;
			case 2:
				if (MainControls.instance.CurrentAction == 6 && gameMapTile != null)
				{
					EngineInterface.DeleteBuilding(gameMapTile.gameMapX, gameMapTile.gameMapY, ActivePlayerID, inGameNotEditor: true, 2);
				}
				performEditorConstantAction();
				break;
			case 3:
				if (MainControls.instance.CurrentAction == 7 && gameMapTile != null)
				{
					int num3 = 1;
					if (KeyManager.instance.isShiftDown())
					{
						num3 = 5;
					}
					else if (KeyManager.instance.isCtrlDown())
					{
						num3 = 20;
					}
					else if (KeyManager.instance.isAltDown())
					{
						num3 = 50;
					}
					for (int k = 0; k < num3; k++)
					{
						EngineInterface.PlaceMapperItem(MainControls.instance.CurrentSubAction, gameMapTile.gameMapX, gameMapTile.gameMapY, 0, ActivePlayerID, !MainViewModel.Instance.IsMapEditorMode, constructingOnly: false, num);
					}
				}
				if (MainControls.instance.CurrentAction == 3 && gameMapTile != null)
				{
					int num4 = 0;
					if (MainControls.instance.BrushSize == 9)
					{
						num4 = 1;
					}
					else if (MainControls.instance.BrushSize == 25)
					{
						num4 = 2;
					}
					else if (MainControls.instance.BrushSize == 49)
					{
						num4 = 3;
					}
					else if (MainControls.instance.BrushSize == 81)
					{
						num4 = 4;
					}
					else if (MainControls.instance.BrushSize == 121)
					{
						num4 = 5;
					}
					else if (MainControls.instance.BrushSize == 169)
					{
						num4 = 6;
					}
					else if (MainControls.instance.BrushSize == 400)
					{
						num4 = 7;
					}
					else if (MainControls.instance.BrushSize == 625)
					{
						num4 = 8;
					}
					EngineInterface.PlaceMapperItem(MainControls.instance.CurrentSubAction, gameMapTile.gameMapX, gameMapTile.gameMapY, num4 + 1, ActivePlayerID, !MainViewModel.Instance.IsMapEditorMode, constructingOnly: false, num);
				}
				break;
			}
		}
	}

	public void FlattenedLandscape()
	{
		recentFlattenLandscape = DateTime.UtcNow.AddMilliseconds(200.0);
	}

	public void EscapeCloseUIPanel()
	{
		if (MainControls.instance.CurrentAction != 8 && MainControls.instance.CurrentAction != 9)
		{
			EngineInterface.GameAction(Enums.GameActionCommand.CloseTroopsPanel, 0, 0);
			rightDownForEngine = true;
		}
	}

	public void CancelPlacement()
	{
		MainControls.instance.CurrentAction = 0;
	}

	public bool performEditorClick()
	{
		if (MainControls.instance.CurrentAction == 0)
		{
			return false;
		}
		if (MainControls.instance.CurrentAction == 3)
		{
			lastConstantX = -1;
			lastConstantState = 0;
			lastConstantTime = DateTime.UtcNow;
			return true;
		}
		_ = mouseTileX;
		_ = mouseTileY;
		return true;
	}

	private void initConstantAction()
	{
		if (MainControls.instance.CurrentAction != 3)
		{
			return;
		}
		int num = (int)mouseTileX;
		int num2 = (int)mouseTileY;
		if (editorMouseState == 0)
		{
			editorOverLandHeight = GameMap.instance.lastMouseLandscapeHeight;
			editorMouseState = 1;
			if (MainViewModel.Instance.MERulerMode == 0)
			{
				lockedRulerX = num;
				lockedRulerY = -1;
			}
			else if (MainViewModel.Instance.MERulerMode == 1)
			{
				lockedRulerX = -1;
				lockedRulerY = num2;
			}
			else
			{
				lockedRulerX = -1;
				lockedRulerY = -1;
			}
		}
	}

	public bool performEditorConstantAction()
	{
		int num = 0;
		if (MainControls.instance.CurrentAction == 0)
		{
			return false;
		}
		if (overUI())
		{
			return false;
		}
		if (MainControls.instance.CurrentAction == 3)
		{
			int num2 = (int)mouseTileX;
			int num3 = (int)mouseTileY;
			if (constantMousePosTile.y > 0 && constantMousePosTile.y > 0)
			{
				num2 = constantMousePosTile.x;
				num3 = constantMousePosTile.y;
				if (lockedRulerX >= 0)
				{
					num2 = lockedRulerX;
				}
				if (lockedRulerY >= 0)
				{
					num3 = lockedRulerY;
				}
			}
			if (num2 == lastConstantX && num3 == lastConstantY)
			{
				bool flag = true;
				if (lastConstantState == 0)
				{
					if ((DateTime.UtcNow - lastConstantTime).TotalSeconds > 1.0)
					{
						flag = false;
						lastConstantState = 1;
					}
				}
				else if (lastConstantState == 1 && (DateTime.UtcNow - lastConstantTime).TotalMilliseconds > 50.0)
				{
					flag = false;
				}
				if (flag && MainControls.instance.CurrentSubAction != 151)
				{
					return true;
				}
			}
			lastConstantTime = DateTime.UtcNow;
			lastConstantX = num2;
			lastConstantY = num3;
			if (MainControls.instance.BrushSize == 9)
			{
				num = 1;
			}
			else if (MainControls.instance.BrushSize == 25)
			{
				num = 2;
			}
			else if (MainControls.instance.BrushSize == 49)
			{
				num = 3;
			}
			else if (MainControls.instance.BrushSize == 81)
			{
				num = 4;
			}
			else if (MainControls.instance.BrushSize == 121)
			{
				num = 5;
			}
			else if (MainControls.instance.BrushSize == 169)
			{
				num = 6;
			}
			else if (MainControls.instance.BrushSize == 400)
			{
				num = 7;
			}
			else if (MainControls.instance.BrushSize == 625)
			{
				num = 8;
			}
			int currentSubAction = MainControls.instance.CurrentSubAction;
			bool flag2 = false;
			int currentSubAction2 = MainControls.instance.CurrentSubAction;
			if ((uint)(currentSubAction2 - 205) > 4u && (uint)(currentSubAction2 - 380) <= 9u && overNoesisUI)
			{
				flag2 = true;
			}
			if (!flag2)
			{
				GameMapTile mapTile = GameMap.instance.getMapTile(num2, num3);
				if (mapTile != null)
				{
					if (constantEditorLimitSameTileRepeat)
					{
						if (lastConstantPlacementPos.x == (float)num2 && lastConstantPlacementPos.y == (float)num3)
						{
							return true;
						}
						lastConstantPlacementPos = new Vector2(num2, num3);
					}
					EngineInterface.PlaceMapperItem(currentSubAction, mapTile.gameMapX, mapTile.gameMapY, num + 1, ActivePlayerID, !MainViewModel.Instance.IsMapEditorMode, constructingOnly: false, 2);
				}
			}
			return true;
		}
		return true;
	}

	public bool performContinuousNoMouse()
	{
		if (!allowContinuousNoMouseConstant)
		{
			return false;
		}
		if (MainControls.instance.CurrentAction == 0)
		{
			return false;
		}
		if (!MainViewModel.Instance.IsMapEditorMode)
		{
			return false;
		}
		int x = (int)mouseTileX;
		int y = (int)mouseTileY;
		if (MainControls.instance.CurrentAction == 3)
		{
			editorMouseState = 0;
			int num = 0;
			if (MainControls.instance.BrushSize == 9)
			{
				num = 1;
			}
			else if (MainControls.instance.BrushSize == 25)
			{
				num = 2;
			}
			else if (MainControls.instance.BrushSize == 49)
			{
				num = 3;
			}
			else if (MainControls.instance.BrushSize == 81)
			{
				num = 4;
			}
			else if (MainControls.instance.BrushSize == 121)
			{
				num = 5;
			}
			else if (MainControls.instance.BrushSize == 169)
			{
				num = 6;
			}
			else if (MainControls.instance.BrushSize == 400)
			{
				num = 7;
			}
			else if (MainControls.instance.BrushSize == 625)
			{
				num = 8;
			}
			GameMapTile mapTile = GameMap.instance.getMapTile(x, y);
			if (mapTile != null)
			{
				EngineInterface.PlaceMapperItem(MainControls.instance.CurrentSubAction, mapTile.gameMapX, mapTile.gameMapY, num + 1, ActivePlayerID, inGameNotEditor: false, constructingOnly: true, 0);
			}
		}
		return true;
	}

	public void InitEditorMode()
	{
		MainControls.instance.BrushSize = 169;
		MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(6);
	}

	public void mapEditorInteraction(Enums.eMappers command)
	{
		if (Director.instance.Paused)
		{
			return;
		}
		constantEditorPlacementRate = 1f / 60f;
		lastConstantPlacementPos = Vector2.zero;
		constantEditorLimitSameTileRepeat = false;
		mapChanged = true;
		switch (command)
		{
		case Enums.eMappers.MAPPER_DELETE_EDITOR:
			if (MainControls.instance.CurrentAction == 3 && MainControls.instance.CurrentSubAction == (int)command)
			{
				MainControls.instance.CurrentAction = 0;
			}
			else
			{
				changeLand(command);
			}
			break;
		case Enums.eMappers.MAPPER_RAISE:
		case Enums.eMappers.MAPPER_LOWER:
		case Enums.eMappers.MAPPER_MIN:
		case Enums.eMappers.MAPPER_MAX:
		case Enums.eMappers.MAPPER_EQUALISE:
		case Enums.eMappers.MAPPER_MOUNTAIN:
		case Enums.eMappers.MAPPER_HILL:
		case Enums.eMappers.MAPPER_PLAIN1:
		case Enums.eMappers.MAPPER_PLAIN2:
			changeLand(command);
			break;
		case Enums.eMappers.MAPPER_DEER:
		case Enums.eMappers.MAPPER_LION:
		case Enums.eMappers.MAPPER_RABBIT:
		case Enums.eMappers.MAPPER_BEAR:
		case Enums.eMappers.MAPPER_CROW:
		case Enums.eMappers.MAPPER_SEAGULL:
		case Enums.eMappers.MAPPER_GOAT:
		case Enums.eMappers.MAPPER_HYENA:
		case Enums.eMappers.MAPPER_CONDOR:
		case Enums.eMappers.MAPPER_CROCODILE:
			constantEditorPlacementRate = 0.1f;
			constantEditorLimitSameTileRepeat = true;
			changeLand(command);
			break;
		case Enums.eMappers.MAPPER_LAND:
		case Enums.eMappers.MAPPER_SCRUB:
		case Enums.eMappers.MAPPER_ROCKY:
		case Enums.eMappers.MAPPER_STONES:
		case Enums.eMappers.MAPPER_BOULDERS:
		case Enums.eMappers.MAPPER_PEBBLES:
		case Enums.eMappers.MAPPER_IRON:
		case Enums.eMappers.MAPPER_DIRT:
		case Enums.eMappers.MAPPER_GRASS:
		case Enums.eMappers.MAPPER_DUNES:
		case Enums.eMappers.MAPPER_SCRUBGRASS:
			changeLand(command);
			break;
		case Enums.eMappers.MAPPER_SEA:
		case Enums.eMappers.MAPPER_BEACH:
		case Enums.eMappers.MAPPER_RIVER:
		case Enums.eMappers.MAPPER_FORD:
		case Enums.eMappers.MAPPER_MARSH:
		case Enums.eMappers.MAPPER_OIL:
		case Enums.eMappers.MAPPER_FOAM:
		case Enums.eMappers.MAPPER_RIPPLE:
			changeLand(command);
			break;
		case Enums.eMappers.MAPPER_CHESTNUT:
		case Enums.eMappers.MAPPER_OAK:
		case Enums.eMappers.MAPPER_PINE:
		case Enums.eMappers.MAPPER_BIRCH:
		case Enums.eMappers.MAPPER_SHRUB1A:
		case Enums.eMappers.MAPPER_SHRUB1B:
		case Enums.eMappers.MAPPER_SHRUB1C:
		case Enums.eMappers.MAPPER_SHRUB1D:
		case Enums.eMappers.MAPPER_SHRUB1E:
		case Enums.eMappers.MAPPER_SHRUB2A:
		case Enums.eMappers.MAPPER_SHRUB3A:
		case Enums.eMappers.MAPPER_SHRUB3B:
		case Enums.eMappers.MAPPER_SHRUB3C:
		case Enums.eMappers.MAPPER_SHRUB3D:
			constantEditorPlacementRate = 0.05f;
			constantEditorLimitSameTileRepeat = true;
			changeLand(command);
			break;
		case Enums.eMappers.MAPPER_SIGNPOST:
			changeLand(command);
			break;
		case Enums.eMappers.MAPPER_MARKER_POINT1:
		case Enums.eMappers.MAPPER_MARKER_POINT2:
		case Enums.eMappers.MAPPER_MARKER_POINT3:
		case Enums.eMappers.MAPPER_MARKER_POINT4:
		case Enums.eMappers.MAPPER_MARKER_POINT5:
		case Enums.eMappers.MAPPER_MARKER_POINT6:
		case Enums.eMappers.MAPPER_MARKER_POINT7:
		case Enums.eMappers.MAPPER_MARKER_POINT8:
		case Enums.eMappers.MAPPER_MARKER_POINT9:
		case Enums.eMappers.MAPPER_MARKER_POINT10:
		{
			changeLand(command);
			int num = (int)(command - 380);
			for (int i = 0; i < 10; i++)
			{
				if (i == num)
				{
					MainViewModel.Instance.MarkerSelected[i] = true;
				}
				else
				{
					MainViewModel.Instance.MarkerSelected[i] = false;
				}
			}
			MainViewModel.Instance.HUDMarkers.RefMarkerInvisible.Visibility = Visibility.Visible;
			MainViewModel.Instance.HUDMarkers.RefMarkerVisible.Visibility = Visibility.Visible;
			MainViewModel.Instance.HUDMarkers.RefMarkerDisappearing.Visibility = Visibility.Visible;
			MainViewModel.Instance.HUDMarkers.DisableRadios = true;
			switch (GameData.Instance.lastGameState.markers_start_points[num, 3])
			{
			case 0:
			case 1:
				MainViewModel.Instance.HUDMarkers.RefMarkerInvisible.IsChecked = true;
				break;
			case 2:
				MainViewModel.Instance.HUDMarkers.RefMarkerVisible.IsChecked = true;
				break;
			case 3:
				MainViewModel.Instance.HUDMarkers.RefMarkerDisappearing.IsChecked = true;
				break;
			}
			MainViewModel.Instance.HUDMarkers.DisableRadios = false;
			if (GameData.Instance.lastGameState.markers_start_points[num, 2] > 0)
			{
				EngineInterface.GameAction(Enums.GameActionCommand.CentreMarker, num, 0);
			}
			break;
		}
		case Enums.eMappers.MAPPER_BIGROCK1:
		case Enums.eMappers.MAPPER_BIGROCK2:
		case Enums.eMappers.MAPPER_BIGROCK3:
		case Enums.eMappers.MAPPER_BIGROCK4:
		case Enums.eMappers.MAPPER_BIGROCK5:
			changeLand(command);
			if (MainControls.instance.BrushSize == 9)
			{
				MainControls.instance.BrushSize = 25;
				MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(2);
			}
			else if (MainControls.instance.BrushSize == 81)
			{
				MainControls.instance.BrushSize = 121;
				MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(5);
			}
			break;
		default:
			changeLand(Enums.eMappers.MAPPER_NULL);
			break;
		}
	}

	public void placeBuildingInteraction(Enums.eMappers command)
	{
		if (Director.instance.Paused)
		{
			return;
		}
		switch (command)
		{
		case Enums.eMappers.MAPPER_DELETE:
			if (EngineInterface.StartMapperItem(39) >= 0)
			{
				deleteBuilding();
			}
			break;
		case Enums.eMappers.MAPPER_PEOPLE_ARCHERS:
		case Enums.eMappers.MAPPER_PEOPLE_SPEARMEN:
		case Enums.eMappers.MAPPER_PEOPLE_PIKEMEN:
		case Enums.eMappers.MAPPER_PEOPLE_MACEMEN:
		case Enums.eMappers.MAPPER_PEOPLE_XBOWMEN:
		case Enums.eMappers.MAPPER_PEOPLE_SWORDSMEN:
		case Enums.eMappers.MAPPER_PEOPLE_KNIGHTS:
		case Enums.eMappers.MAPPER_PEOPLE_LADDERMEN:
		case Enums.eMappers.MAPPER_PEOPLE_ENGINEERS:
		case Enums.eMappers.MAPPER_PEOPLE_ENGINEERS_POTS:
		case Enums.eMappers.MAPPER_PEOPLE_MONKS:
		case Enums.eMappers.MAPPER_PEOPLE_CATAPULTS:
		case Enums.eMappers.MAPPER_PEOPLE_TREBUCHETS:
		case Enums.eMappers.MAPPER_PEOPLE_BATTERING_RAMS:
		case Enums.eMappers.MAPPER_PEOPLE_SIEGE_TOWERS:
		case Enums.eMappers.MAPPER_PEOPLE_PORTABLE_SHIELDS:
		case Enums.eMappers.MAPPER_PEOPLE_TUNNELERS:
		case Enums.eMappers.MAPPER_PEOPLE_ARAB_BOW:
		case Enums.eMappers.MAPPER_PEOPLE_ARAB_SLAVE:
		case Enums.eMappers.MAPPER_PEOPLE_ARAB_SLINGER:
		case Enums.eMappers.MAPPER_PEOPLE_ARAB_ASSASIN:
		case Enums.eMappers.MAPPER_PEOPLE_ARAB_HORSEMAN:
		case Enums.eMappers.MAPPER_PEOPLE_ARAB_SWORDSMAN:
		case Enums.eMappers.MAPPER_PEOPLE_ARAB_GRENADIER:
		case Enums.eMappers.MAPPER_PEOPLE_ARAB_BALLISTA:
		case Enums.eMappers.MAPPER_ARAB_BALLISTA:
		case Enums.eMappers.MAPPER_PEOPLE_BEDOUIN_CAMEL_LANCER:
		case Enums.eMappers.MAPPER_PEOPLE_BEDOUIN_HEALER:
		case Enums.eMappers.MAPPER_PEOPLE_BEDOUIN_EUNUCH:
		case Enums.eMappers.MAPPER_PEOPLE_BEDOUIN_AMBUSHER:
		case Enums.eMappers.MAPPER_PEOPLE_BEDOUIN_SKIRMISHER:
		case Enums.eMappers.MAPPER_PEOPLE_BEDOUIN_HEAVY_CAMEL:
		case Enums.eMappers.MAPPER_PEOPLE_BEDOUIN_SAPPER:
		case Enums.eMappers.MAPPER_PEOPLE_BEDOUIN_DEMOLISHER:
			placeEditorTroop(command);
			break;
		default:
			placeBuilding(command);
			break;
		}
	}

	public void SetEditorPlayerID(int playerID)
	{
		if (!Director.instance.Paused)
		{
			editorPlayerID = playerID;
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				MainViewModel.Instance.UpdateUITroopSprites(playerID, arabic: false);
				EngineInterface.SetEditorPlayer(playerID);
			}
		}
	}

	public void SetMEBrushSize(int newSize, bool rightClick = false)
	{
		if (MainControls.instance.CurrentAction == 3 && (MainControls.instance.CurrentSubAction == 205 || MainControls.instance.CurrentSubAction == 206 || MainControls.instance.CurrentSubAction == 207 || MainControls.instance.CurrentSubAction == 208 || MainControls.instance.CurrentSubAction == 209))
		{
			if (!rightClick)
			{
				if (newSize == 1)
				{
					newSize = 2;
					MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(newSize);
				}
				else if (newSize == 4)
				{
					newSize = 5;
					MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(newSize);
				}
				else if (newSize >= 7)
				{
					newSize = 0;
					MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(newSize);
				}
			}
			else if (newSize == 1)
			{
				newSize = 0;
				MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(newSize);
			}
			else if (newSize == 4)
			{
				newSize = 3;
				MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(newSize);
			}
			else if (newSize >= 7)
			{
				newSize = 6;
				MainViewModel.Instance.HUDmain.UpdateMEDrawingSize(newSize);
			}
		}
		switch (newSize)
		{
		case 0:
			MainControls.instance.BrushSize = 1;
			break;
		case 1:
			MainControls.instance.BrushSize = 9;
			break;
		case 2:
			MainControls.instance.BrushSize = 25;
			break;
		case 3:
			MainControls.instance.BrushSize = 49;
			break;
		case 4:
			MainControls.instance.BrushSize = 81;
			break;
		case 5:
			MainControls.instance.BrushSize = 121;
			break;
		case 6:
			MainControls.instance.BrushSize = 169;
			break;
		case 7:
			MainControls.instance.BrushSize = 400;
			break;
		case 8:
			MainControls.instance.BrushSize = 625;
			break;
		}
	}

	public int[] getSelectedChimpTypes()
	{
		if (dllSelectedChimpList == null || dllSelectedChimpList.GetLength(0) == 0)
		{
			return new int[89];
		}
		return GameMap.instance.getChimpTypesFromChimpList(dllSelectedChimpList);
	}

	private void dllSelectedChimpsChanged()
	{
		MainViewModel.Instance.TroopsSelectedGameAction(fromInitialOpening: false);
	}

	public void triggerTroopsSelection()
	{
		troopSelectMouseStart = (troopSelectMouseCurrent = Input.mousePosition);
		int[] chimpsUnderCursor = null;
		int depthFromUnder = lastTroopOverDepth;
		selectedChimpList = GameMap.instance.grabTroopsOnScreen(troopSelectMouseStart, troopSelectMouseCurrent, ref chimpsUnderCursor, Input.mousePosition, ref depthFromUnder);
		lastTroopOverDepth = depthFromUnder;
		underCursorChimpList = chimpsUnderCursor;
	}

	public void updateDLLSelectedTroops(EngineInterface.PlayState gameState, bool inTroopUI)
	{
		if (gameState.numSelectedChimps > 0 && inTroopUI)
		{
			if (dllSelectedChimpList == null || dllSelectedChimpList.GetLength(0) != gameState.numSelectedChimps)
			{
				chimpListChanged = true;
				dllSelectedChimpList = new int[gameState.numSelectedChimps, 2];
				for (int i = 0; i < gameState.numSelectedChimps; i++)
				{
					dllSelectedChimpList[i, 0] = gameState.selectedChimps[i];
					dllSelectedChimpList[i, 1] = gameState.selectedChimpTypes[i];
				}
				return;
			}
			for (int j = 0; j < gameState.numSelectedChimps; j++)
			{
				if (dllSelectedChimpList[j, 0] != gameState.selectedChimps[j])
				{
					chimpListChanged = true;
					dllSelectedChimpList[j, 0] = gameState.selectedChimps[j];
					dllSelectedChimpList[j, 1] = gameState.selectedChimpTypes[j];
				}
			}
		}
		else if (dllSelectedChimpList != null && dllSelectedChimpList.GetLength(0) > 0)
		{
			chimpListChanged = false;
			dllSelectedChimpList = null;
		}
	}

	public void removeSelectedChimpTypes(Enums.eChimps type, int mode)
	{
		if (!Director.instance.Paused)
		{
			if (mode == 0)
			{
				EngineInterface.GameAction(Enums.GameActionCommand.Troops_DeSelectExceptType, 0, (int)type);
			}
			else
			{
				EngineInterface.GameAction(Enums.GameActionCommand.Troops_DeSelectType, 0, (int)type);
			}
		}
	}

	public void directSetAppSubMode(int newMode)
	{
		GameData.Instance.app_sub_mode = newMode;
		EngineInterface.SetAppMode(GameData.Instance.app_mode, newMode);
		tutorialAppModes();
	}

	public void directSetAppMode(int new_app_mode, int new_sub_mode)
	{
		EngineInterface.SetAppMode(new_app_mode, new_sub_mode);
		tutorialAppModes();
	}

	public void reportSubModeTabChange(int newMode)
	{
		GameData.Instance.app_sub_mode = newMode;
		EngineInterface.SetAppMode(GameData.Instance.app_mode, GameData.Instance.app_sub_mode);
		tutorialAppModes();
	}

	private void tutorialAppModes()
	{
		if (GameData.Instance.game_type == 4)
		{
			if (GameData.Instance.app_mode == 16 && GameData.Instance.app_sub_mode == 71)
			{
				EngineInterface.TutorialAction(14, 1);
			}
			if (GameData.Instance.app_mode == 16 && GameData.Instance.app_sub_mode == 72)
			{
				EngineInterface.TutorialAction(14, 72);
			}
		}
	}

	public void changeEditorMode()
	{
		MainControls.instance.StopAllPlacement();
		if (MainViewModel.Instance.MEMode == 0)
		{
			directSetAppMode(12, 0);
		}
		else
		{
			directSetAppMode(14, 10);
		}
	}

	public void placeBuilding(Enums.eMappers buildingType)
	{
		int num = EngineInterface.StartMapperItem((int)buildingType);
		if (num >= 0)
		{
			MainControls.instance.CurrentAction = 5;
			if (num > 0)
			{
				MainControls.instance.CurrentSubAction = num;
				mapChanged = true;
			}
		}
	}

	private void placeEditorTroop(Enums.eMappers buildingType)
	{
		if (EngineInterface.StartMapperItem((int)buildingType) >= 0)
		{
			MainControls.instance.CurrentAction = 7;
			MainControls.instance.CurrentSubAction = (int)buildingType;
		}
	}

	private void deleteBuilding()
	{
		MainControls.instance.CurrentAction = 6;
		mapChanged = true;
	}

	private void changeLand(Enums.eMappers mapperAction)
	{
		if (EngineInterface.StartMapperItem((int)mapperAction) >= 0)
		{
			MainControls.instance.CurrentAction = 3;
			MainControls.instance.CurrentSubAction = (int)mapperAction;
			editorMouseState = 0;
			mapChanged = true;
		}
	}

	public void toggleOSTFrameRate()
	{
		if (OnScreenText.Instance.isOSTActive(Enums.eOnScreenText.OST_FRAMERATE))
		{
			OnScreenText.Instance.removeOSTEntry(Enums.eOnScreenText.OST_FRAMERATE);
		}
		else
		{
			OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_FRAMERATE, 0);
		}
	}

	private void startFPS()
	{
		currentFrames = 0;
		lastFrames = 0;
		currentTicks = 0;
		lastTicks = 0;
		StartCoroutine(fpsCounterSub());
	}

	private void updateFPS()
	{
		currentFrames++;
	}

	public void updateTick()
	{
		currentTicks++;
	}

	private IEnumerator fpsCounterSub()
	{
		yield return new WaitForSeconds(1f);
		lastFrames = currentFrames;
		currentFrames = 0;
		lastTicks = currentTicks;
		currentTicks = 0;
		StartCoroutine(fpsCounterSub());
	}

	public static int getIntFromString(string str)
	{
		try
		{
			return int.Parse(str, Director.defaultCulture);
		}
		catch (Exception)
		{
		}
		return -1;
	}

	public static ulong getuLongFromString(string str)
	{
		try
		{
			return ulong.Parse(str, Director.defaultCulture);
		}
		catch (Exception)
		{
		}
		return 0uL;
	}
}
