using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using CrusaderDE;
using Noesis;
using UnityEngine;
using UnityEngine.Networking;

public class Director : MonoBehaviour
{
	public static Director instance = null;

	public static CultureInfo defaultCulture = CultureInfo.CreateSpecificCulture("en-US");

	[HideInInspector]
	public static bool gameStarted = false;

	[HideInInspector]
	public static int gameOver = 0;

	public Texture2D swordCursor;

	public Texture2D scimitarCursor;

	public Texture2D handCursor;

	public Texture2D deleteCursor;

	public Texture2D deleteNotCursor;

	public Texture2D waitCursor;

	public Texture2D swordCursorX;

	public Texture2D scimitarCursorX;

	public Texture2D handCursorX;

	public Texture2D deleteCursorX;

	public Texture2D deleteNotCursorX;

	public Texture2D waitCursorX;

	public Texture2D swordCursor32;

	public Texture2D scimitarCursor32;

	public Texture2D handCursor32;

	public Texture2D deleteCursor32;

	public Texture2D deleteNotCursor32;

	public Texture2D waitCursor32;

	public Texture2D deleteOldCursor;

	public CursorMode cursorMode;

	public int simTickCount;

	public bool simThreadRunning;

	public bool engineRunning;

	public Thread simThread;

	public int FramesToProcess;

	public int FrameToSkipMP;

	public bool InEngine;

	public Stopwatch debugStopwatch;

	public int tickTimingCount;

	public long totalMS;

	public long highestTickTime;

	public double engineFrameTime = 0.025;

	public int lowFPSCompensationLevel = 1;

	public int lowFPSCount;

	public bool wasPaused;

	public bool paused;

	public int numTiles;

	public Timer fixedTimer;

	public double pendingTimerChange;

	public Action postUpdateCallback;

	public bool inPostCallbackPeriod;

	public DateTime lastNoesisGUICheck = DateTime.MinValue;

	public bool waitCursorSet;

	public int currentCursorType = -1;

	public bool multiplayerGame;

	public bool skirmishModeGame;

	public bool wasSkirmishModeGame;

	public int EngineFrameRate => (int)(1.0 / engineFrameTime);

	public float EngineFrameTime => (float)engineFrameTime;

	public bool Paused => paused;

	public int NumTiles => numTiles;

	public bool SimRunning => engineRunning;

	public bool MultiplayerGame => multiplayerGame;

	public bool SkirmishModeGame => skirmishModeGame;

	public bool WasSkirmishModeGame
	{
		get
		{
			return wasSkirmishModeGame;
		}
		set
		{
			wasSkirmishModeGame = value;
		}
	}

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
		MemoryBuffers.init();
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
		fixedTimer = new Timer(MyFixedUpdate, "", TimeSpan.Zero, TimeSpan.FromSeconds(engineFrameTime));
		engineRunning = false;
		InEngine = false;
		simThread = new Thread(runSimTickTest);
		simThread.Name = "Stronghold1Engine";
		simThread.Start();
		debugStopwatch = Stopwatch.StartNew();
		UpdateFixedTimer(engineFrameTime);
		EndMultiplayer();
		LoadImages();
	}

	public void UpdateFixedTimer(double timer)
	{
		pendingTimerChange = timer;
	}

	public void MyFixedUpdate(object state)
	{
		if (engineRunning && !paused)
		{
			FramesToProcess++;
		}
		if (pendingTimerChange != 0.0)
		{
			fixedTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(pendingTimerChange));
			pendingTimerChange = 0.0;
		}
		UpdateMultiplayer(fromUpdate: false);
	}

	public void CapFrameRateOnLoading(bool spectatorMode)
	{
		if (!spectatorMode && Math.Round(1.0 / engineFrameTime) > 90.0)
		{
			SetEngineFrameRate(90.0);
		}
	}

	public void IncreaseFrameRate()
	{
		int num = 90;
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.spectatorMode != 0)
		{
			num = 300;
		}
		double num2 = Math.Round(1.0 / engineFrameTime);
		if (num2 < (double)num)
		{
			num2 += 5.0;
			if (num2 > (double)num)
			{
				num2 = num;
			}
			SetEngineFrameRate(num2);
			OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_GAME_SPEED, (int)num2);
			if (!MultiplayerGame && num2 <= 90.0)
			{
				ConfigSettings.Settings_GameSpeed = (int)num2;
				ConfigSettings.SaveSettings();
			}
		}
	}

	public void DecreaseFrameRate()
	{
		double num = Math.Round(1.0 / engineFrameTime);
		if (num > 10.0)
		{
			num -= 5.0;
			if (num < 10.0)
			{
				num = 10.0;
			}
			SetEngineFrameRate(num);
			OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_GAME_SPEED, (int)num);
			if (!MultiplayerGame && num <= 90.0)
			{
				ConfigSettings.Settings_GameSpeed = (int)num;
				ConfigSettings.SaveSettings();
			}
		}
	}

	public void SetEngineFrameRate(double fps)
	{
		int num = 90;
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.spectatorMode != 0)
		{
			num = 300;
		}
		if (fps < 10.0 || fps > (double)num)
		{
			fps = 40.0;
		}
		if (fps <= 10.0)
		{
			lowFPSCompensationLevel = 4;
		}
		else if (fps <= 15.0)
		{
			lowFPSCompensationLevel = 3;
		}
		else if (fps <= 20.0)
		{
			lowFPSCompensationLevel = 2;
		}
		else
		{
			lowFPSCompensationLevel = 1;
		}
		engineFrameTime = 1.0 / fps;
		UpdateFixedTimer(engineFrameTime / (double)lowFPSCompensationLevel);
	}

	public void ResetFrameRate()
	{
		UpdateFixedTimer(engineFrameTime / (double)lowFPSCompensationLevel);
	}

	public void forceEarlyEngine()
	{
		if (engineRunning && FramesToProcess == 0)
		{
			FramesToProcess++;
		}
	}

	public void MPSkipFrame(int numToSkip)
	{
		if (multiplayerGame)
		{
			FrameToSkipMP += numToSkip;
		}
	}

	public void startSimThread()
	{
		simTickCount = 0;
		FramesToProcess = 0;
		FrameToSkipMP = 0;
		engineRunning = true;
		paused = false;
		wasSkirmishModeGame = false;
	}

	public void stopSimThread()
	{
		if (engineRunning)
		{
			GC.Collect();
			engineRunning = false;
			FramesToProcess = 0;
			FrameToSkipMP = 0;
			EndMultiplayer();
		}
	}

	public void SetPausedState(bool state)
	{
		if (!state || !multiplayerGame)
		{
			paused = state;
			if (paused)
			{
				EditorDirector.instance.GamePaused();
			}
		}
	}

	public void TogglePausedState()
	{
		SetPausedState(!paused);
	}

	public bool SafeToSave(bool wait = false)
	{
		if (engineRunning)
		{
			if (FramesToProcess <= 0 && !InEngine)
			{
				wasPaused = paused;
				paused = true;
				return true;
			}
			if (wait)
			{
				int num = 100;
				while (num > 0)
				{
					num--;
					Thread.Sleep(10);
					if (FramesToProcess <= 0 && !InEngine)
					{
						wasPaused = paused;
						paused = true;
						return true;
					}
				}
			}
		}
		return false;
	}

	public void FinishedSaving()
	{
		paused = wasPaused;
	}

	public int getSimTickCount()
	{
		return simTickCount;
	}

	public int getAverageCalcTime()
	{
		if (tickTimingCount > 0)
		{
			return (int)(totalMS / tickTimingCount);
		}
		return 0;
	}

	public int getHighestCalcTime()
	{
		return (int)highestTickTime;
	}

	public void resetEngineTimer()
	{
		highestTickTime = (totalMS = (tickTimingCount = 0));
	}

	public void runSimTickTest()
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(2.0);
		while (DateTime.UtcNow < dateTime)
		{
			int num = 0;
			for (int i = 0; i < 1000000; i++)
			{
				num++;
			}
		}
		simThreadRunning = true;
		while (simThreadRunning)
		{
			if (FramesToProcess > 0)
			{
				InEngine = true;
				FramesToProcess--;
				simTickCount++;
				long elapsedMilliseconds = debugStopwatch.ElapsedMilliseconds;
				bool flag = false;
				if (MultiplayerGame)
				{
					flag = FrameToSkipMP > 0;
				}
				if (lowFPSCompensationLevel > 1)
				{
					lowFPSCount++;
					if (lowFPSCount >= lowFPSCompensationLevel)
					{
						lowFPSCount = 0;
					}
					else
					{
						flag = true;
					}
				}
				numTiles = EngineInterface.run(flag);
				if (!flag)
				{
					EditorDirector.instance.updateTick();
				}
				if (FrameToSkipMP > 0)
				{
					FrameToSkipMP--;
				}
				long elapsedMilliseconds2 = debugStopwatch.ElapsedMilliseconds;
				tickTimingCount++;
				totalMS += elapsedMilliseconds2 - elapsedMilliseconds;
				if (elapsedMilliseconds2 - elapsedMilliseconds > highestTickTime)
				{
					highestTickTime = elapsedMilliseconds2 - elapsedMilliseconds;
				}
				InEngine = false;
			}
			else
			{
				Thread.Sleep(1);
			}
		}
	}

	public void OnDestroy()
	{
		if (simThreadRunning)
		{
			simThreadRunning = false;
			simThread.Join();
		}
	}

	public void Start()
	{
	}

	public void SetPostUpdateCallback(Action callback)
	{
		inPostCallbackPeriod = true;
		postUpdateCallback = callback;
	}

	public void Update()
	{
		monitorCursors();
		UpdateMultiplayer(fromUpdate: true);
		if (SimRunning)
		{
			GameMap.instance.PreCalcScreenCentre();
			TilemapManager.instance.startTileRefresh();
		}
		int num = 0;
		bool flag = false;
		while (!paused)
		{
			MemoryBuffers.MemBuffer nextBufferToRender = MemoryBuffers.instance.getNextBufferToRender();
			if (nextBufferToRender == null)
			{
				break;
			}
			if (SimRunning)
			{
				GameMap.instance.processTestMap(nextBufferToRender.memory, nextBufferToRender.numTiles, nextBufferToRender.gameState, nextBufferToRender.radarMap);
				Platform_Multiplayer.Instance.SendChores(nextBufferToRender.MPChores);
			}
			MemoryBuffers.instance.returnBuffer(nextBufferToRender);
			FatControler.instance.NoesisGUIUpdateChecksInGame();
			lastNoesisGUICheck = DateTime.UtcNow;
			flag = true;
			num++;
			if (num >= 3 || postUpdateCallback != null)
			{
				break;
			}
		}
		if (flag && postUpdateCallback != null)
		{
			inPostCallbackPeriod = false;
			postUpdateCallback();
			postUpdateCallback = null;
		}
		if (SimRunning)
		{
			GameMap.instance.ApplyRadarMap();
			if (!flag)
			{
				GameMap.instance.experimentalRowManager();
			}
			TilemapManager.instance.endTileRefresh(noGameTick: true);
			if ((DateTime.UtcNow - lastNoesisGUICheck).TotalMilliseconds > 100.0)
			{
				FatControler.instance.NoesisGUIUpdateChecksInGame();
				if (GameData.Instance.lastGameState != null)
				{
					OnScreenText.Instance.updateOST(GameData.Instance.lastGameState, allowExpire: false);
				}
				lastNoesisGUICheck = DateTime.UtcNow;
			}
		}
		FatControler.instance.NoesisGUIUpdateChecksComplete();
	}

	public void monitorCursors()
	{
		if (waitCursorSet)
		{
			return;
		}
		if (FatControler.currentScene != Enums.SceneIDS.ActualMainGame)
		{
			setCursor(0);
		}
		else if ((Object)(object)MainControls.instance != (Object)null && (MainControls.instance.CurrentAction == 6 || (MainControls.instance.CurrentAction == 3 && MainControls.instance.CurrentSubAction == 349)))
		{
			setCursor(2);
		}
		else if (MainViewModel.Instance.IsMapEditorMode)
		{
			setCursor(1);
		}
		else if (GameData.Instance.game_type == 0)
		{
			if ((GameData.Instance.mission_level >= 6 && GameData.Instance.mission_level <= 10) || GameData.Instance.mission_level > 25)
			{
				setCursor(5);
			}
			else
			{
				setCursor(0);
			}
		}
		else if (ConfigSettings.Settings_LordType != 1 && ConfigSettings.Settings_LordType != 2 && ConfigSettings.Settings_LordType != 6 && ConfigSettings.Settings_LordType != 7)
		{
			setCursor(0);
		}
		else
		{
			setCursor(5);
		}
	}

	public void SetWaitCursor()
	{
		waitCursorSet = true;
		setCursor(4);
	}

	public void ClearWaitCursor()
	{
		waitCursorSet = false;
	}

	public void resetCursor()
	{
		setCursor(currentCursorType, force: true);
	}

	public void setCursor(int cursorType, bool force = false)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		if (!(cursorType != currentCursorType || force))
		{
			return;
		}
		if (ConfigSettings.Settings_CursorStyle == 0)
		{
			currentCursorType = cursorType;
			switch (cursorType)
			{
			case 0:
				Cursor.SetCursor(swordCursor32, new Vector2(2f, 1f), (CursorMode)0);
				break;
			case 1:
				Cursor.SetCursor(handCursor32, new Vector2(1f, 1f), (CursorMode)0);
				break;
			case 2:
				Cursor.SetCursor(deleteCursor32, new Vector2(10f, 10f), (CursorMode)0);
				break;
			case 3:
				Cursor.SetCursor(deleteNotCursor32, new Vector2(10f, 10f), (CursorMode)0);
				break;
			case 4:
				Cursor.SetCursor(waitCursor32, new Vector2(16f, 16f), (CursorMode)0);
				break;
			case 5:
				Cursor.SetCursor(scimitarCursor32, new Vector2(1f, 1f), (CursorMode)0);
				break;
			}
		}
		else if (ConfigSettings.Settings_CursorStyle == 3)
		{
			currentCursorType = cursorType;
			switch (cursorType)
			{
			case 0:
				Cursor.SetCursor(swordCursorX, new Vector2(2f, 1f), (CursorMode)1);
				break;
			case 1:
				Cursor.SetCursor(handCursorX, new Vector2(1f, 1f), (CursorMode)1);
				break;
			case 2:
				Cursor.SetCursor(deleteCursorX, new Vector2(10f, 10f), (CursorMode)1);
				break;
			case 3:
				Cursor.SetCursor(deleteNotCursorX, new Vector2(10f, 10f), (CursorMode)1);
				break;
			case 4:
				Cursor.SetCursor(waitCursorX, new Vector2(16f, 16f), (CursorMode)1);
				break;
			case 5:
				Cursor.SetCursor(scimitarCursorX, new Vector2(1f, 1f), (CursorMode)1);
				break;
			}
		}
		else if (ConfigSettings.Settings_CursorStyle == 2)
		{
			currentCursorType = cursorType;
			switch (cursorType)
			{
			case 0:
				Cursor.SetCursor(swordCursor, new Vector2(2f, 1f), (CursorMode)1);
				break;
			case 1:
				Cursor.SetCursor(handCursor, new Vector2(1f, 1f), (CursorMode)1);
				break;
			case 2:
				Cursor.SetCursor(deleteCursor, new Vector2(10f, 10f), (CursorMode)1);
				break;
			case 3:
				Cursor.SetCursor(deleteNotCursor, new Vector2(10f, 10f), (CursorMode)1);
				break;
			case 4:
				Cursor.SetCursor(waitCursor, new Vector2(16f, 16f), (CursorMode)1);
				break;
			case 5:
				Cursor.SetCursor(scimitarCursor, new Vector2(1f, 1f), (CursorMode)1);
				break;
			}
		}
		else if (ConfigSettings.Settings_CursorStyle == 1)
		{
			currentCursorType = cursorType;
			switch (cursorType)
			{
			case 0:
			case 1:
			case 4:
			case 5:
				Cursor.SetCursor((Texture2D)null, Vector2.zero, cursorMode);
				break;
			case 2:
			case 3:
				Cursor.SetCursor(deleteOldCursor, new Vector2(4f, 4f), (CursorMode)0);
				break;
			}
		}
	}

	public void StartMultiplayerGame()
	{
		multiplayerGame = true;
		Platform_Multiplayer.Instance.clearMPMessages();
	}

	public void EndMultiplayer()
	{
		wasSkirmishModeGame = skirmishModeGame;
		multiplayerGame = false;
		skirmishModeGame = false;
	}

	public void StartSkirmishModeGame()
	{
		skirmishModeGame = true;
	}

	public void UpdateMultiplayer(bool fromUpdate)
	{
		if (!multiplayerGame)
		{
			return;
		}
		if ((fromUpdate && !SimRunning) || (!fromUpdate && SimRunning))
		{
			Platform_Multiplayer.Instance.ReceiveGameMessages(!fromUpdate);
		}
		if (fromUpdate)
		{
			Platform_Multiplayer.Instance.processMPMessages();
			if (Platform_Multiplayer.Instance.IsHost)
			{
				Platform_Multiplayer.Instance.monitorHostGameStart();
			}
			Platform_Multiplayer.Instance.monitorForLostPlayers();
		}
	}

	public void initGameOver(int state, int screen, bool victory)
	{
		((MonoBehaviour)this).StartCoroutine(ManageGameOver(state, screen, victory));
	}

	public IEnumerator ManageGameOver(int state, int screen, bool victory)
	{
		if ((!SkirmishModeGame && !MultiplayerGame) || GameData.Instance.game_type == 0)
		{
			string message = ((state != 1) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, Enums.eTextValues.TEXT_SCN_NEW_MESSAGE) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, Enums.eTextValues.TEXT_SCN_MOOD4));
			MainViewModel.Instance.IngameUI.triggerVideos(message, screen, victory);
			yield return (object)new WaitForSeconds(3f);
		}
		if (SFXManager.instance.isBinkPlaying())
		{
			bool finished = false;
			for (int i = 0; i < 15; i++)
			{
				if (SFXManager.instance.isBinkPlaying())
				{
					yield return (object)new WaitForSeconds(1f);
					continue;
				}
				finished = true;
				break;
			}
			if (!finished)
			{
				MainViewModel.Instance.HUDRoot.RadarME_Ended();
				for (int i = 0; i < 15; i++)
				{
					if (MyAudioManager.Instance.isSpeechPlaying(1))
					{
						yield return (object)new WaitForSeconds(1f);
						continue;
					}
					finished = true;
					break;
				}
				if (!finished)
				{
					MyAudioManager.Instance.StopAllGameSounds(leaveMusicPlaying: true);
				}
			}
			yield return (object)new WaitForSeconds(1f);
		}
		GameData.scenario.ManageGameOver(state, screen);
	}

	public void ReGetUIEdge()
	{
		((MonoBehaviour)this).StartCoroutine(doReGetUIEdge());
	}

	public IEnumerator doReGetUIEdge()
	{
		yield return (object)new WaitForSeconds(0.5f);
		FatControler.instance.SHLowerUIPoint = 0f;
		MainViewModel.Instance.IngameUI.findUIlowerPoint();
	}

	public void DelayCentreKeep()
	{
		((MonoBehaviour)this).StartCoroutine(doDelayCentreKeep());
	}

	public IEnumerator doDelayCentreKeep()
	{
		yield return 0;
		yield return 0;
		yield return 0;
		EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep);
	}

	public void DelayShowDisconnect()
	{
		((MonoBehaviour)this).StartCoroutine(doDelayShowDisconnect());
	}

	public IEnumerator doDelayShowDisconnect()
	{
		yield return (object)new WaitForSeconds(9f);
		MainViewModel.Instance.Show_MP_LoadingWarning = true;
		yield return (object)new WaitForSeconds(2f);
		MainViewModel.Instance.Show_MP_LoadingButton = true;
	}

	public void DelayHideConnectionScreen()
	{
		((MonoBehaviour)this).StartCoroutine(doDelayHideConnectionScreen());
	}

	public IEnumerator doDelayHideConnectionScreen()
	{
		yield return (object)new WaitForSeconds(1f);
		MainViewModel.Instance.Show_MP_LoadingBlack = false;
	}

	public void GenericDelayCoroutine(Action action, float seconds)
	{
		((MonoBehaviour)this).StartCoroutine(doGenericDelayCoroutine(action, seconds));
	}

	public IEnumerator doGenericDelayCoroutine(Action action, float seconds)
	{
		yield return (object)new WaitForSeconds(seconds);
		action();
	}

	public void ExitAppFromMP()
	{
		((MonoBehaviour)this).StartCoroutine(DoExitAppFromMP());
	}

	public IEnumerator DoExitAppFromMP()
	{
		Platform_Multiplayer.Instance.LeaveGame();
		yield return (object)new WaitForSeconds(1f);
		Platform_Multiplayer.Instance.exitMP();
		yield return (object)new WaitForSeconds(1f);
		FatControler.instance.ExitApp();
	}

	public void SignupNewsletter(string emailAddress, Action success, bool showRequester = true, bool checkCall = false)
	{
		((MonoBehaviour)this).StartCoroutine(DoSignUp(emailAddress, success, showRequester, checkCall));
	}

	public IEnumerator DoSignUp(string emailAddress, Action success, bool showRequester, bool checkCall)
	{
		WWWForm val = new WWWForm();
		val.AddField("email", emailAddress);
		val.AddField("steamid", Platform_Multiplayer.Instance.GetLocalSteamID().ToString());
		val.AddField("lang", FatControler.locale);
		if (checkCall)
		{
			val.AddField("check", "true");
		}
		else
		{
			val.AddField("check", "false");
		}
		UnityWebRequest www = UnityWebRequest.Post("https://login.strongholdkingdoms.com/sams/api/shcde/registerEmail.php", val);
		try
		{
			www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
			yield return www.SendWebRequest();
			if ((int)www.result != 1)
			{
				yield break;
			}
			try
			{
				switch (int.Parse(www.downloadHandler.text))
				{
				case 2:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Scribe_Unlock);
					ConfigSettings.Settings_NewsletterEmail = "";
					ConfigSettings.SaveSettings();
					success();
					break;
				case 1:
					if (showRequester)
					{
						HUD_ConfirmationPopup.ShowConfirmationOKMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 342), delegate
						{
							ConfigSettings.validateLordType();
							ConfigSettings.Settings_NewsletterEmail = emailAddress;
							ConfigSettings.SaveSettings();
							FrontendMenus.triggerNewsLetterMonitor();
						}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 346));
					}
					else
					{
						ConfigSettings.validateLordType();
					}
					break;
				default:
					ConfigSettings.validateLordType();
					break;
				}
			}
			catch (Exception)
			{
			}
		}
		finally
		{
			((IDisposable)www)?.Dispose();
		}
	}

	public void LoadImages()
	{
		((MonoBehaviour)this).StartCoroutine(DoLoadImages());
	}

	public IEnumerator DoLoadImages()
	{
		bool halfSize = false;
		if (Screen.width < 2000 && Screen.height < 1201)
		{
			halfSize = true;
		}
		for (int imageID = 0; imageID < 108; imageID++)
		{
			if (MainViewModel.imageFileNames[imageID].Length > 0 && MainViewModel.GameImages[imageID] == null)
			{
				while (MainViewModel.GameImageData[imageID] == null)
				{
					yield return null;
				}
				Texture2D val = new Texture2D(2, 2, (TextureFormat)4, false, true);
				ImageConversion.LoadImage(val, MainViewModel.GameImageData[imageID]);
				if (halfSize)
				{
					GPUTextureScaler.Scale(val, ((Texture)val).width / 2, ((Texture)val).height / 2, (FilterMode)2);
				}
				if (MainViewModel.imageProcessingNeeded[imageID])
				{
					Color[] pixels = val.GetPixels();
					for (int i = 0; i < pixels.Length; i++)
					{
						float a = pixels[i].a;
						if (a != 0f)
						{
							if (a != 1f)
							{
								pixels[i].r *= pixels[i].a;
								pixels[i].g *= pixels[i].a;
								pixels[i].b *= pixels[i].a;
							}
						}
						else
						{
							pixels[i].r = (pixels[i].g = (pixels[i].b = 0f));
						}
					}
					val.SetPixels(pixels);
					val.Apply();
				}
				if (IsPowerOfTwo(((Texture)val).width) && IsPowerOfTwo(((Texture)val).height))
				{
					val.Compress(true);
				}
				TextureSource image = new TextureSource(val);
				Object.DestroyImmediate((Object)(object)val);
				MainViewModel.GameImages[imageID] = new AnImage
				{
					_image = image
				};
				MainViewModel.GameImageData[imageID] = null;
			}
			if (imageID % 20 == 19)
			{
				yield return null;
			}
		}
		GC.Collect();
		yield return null;
	}

	public bool IsPowerOfTwo(int x)
	{
		return (x & (x - 1)) == 0;
	}
}
