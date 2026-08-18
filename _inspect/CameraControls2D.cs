using System;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class CameraControls2D : MonoBehaviour
{
	public static CameraControls2D instance;

	public Camera cameraComponent;

	public bool AllowMove = true;

	public float MoveSpeed = 16f;

	public string HorizontalInputAxis = "Horizontal";

	public string VerticalInputAxis = "Vertical";

	public bool AllowZoom = true;

	public float ZoomSpeed = 1f;

	public DateTime lastCycleBookmarks = DateTime.MinValue;

	public DateTime zoomDelay = DateTime.MinValue;

	public bool lastZoomPositive;

	public Vector3 cameraPosition;

	public Vector3 newCameraPosition;

	public bool hasNewPosition;

	public bool mapLocked = true;

	public int horzMovement;

	public int vertMovement;

	public void Awake()
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		instance = this;
		if ((Object)(object)instance == (Object)null)
		{
			instance = this;
		}
		else if ((Object)(object)instance != (Object)(object)this)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		cameraComponent = ((Component)this).gameObject.GetComponent<Camera>();
		setNewPosition(new Vector3(0f, 128f, 0f));
	}

	public void setNewPosition(Vector3 newPosition)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		newCameraPosition = ((Component)cameraComponent).transform.position;
		newCameraPosition.x = newPosition.x;
		newCameraPosition.y = newPosition.y;
		hasNewPosition = true;
	}

	public void Update()
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
		horzMovement = 0;
		vertMovement = 0;
		if ((Object)(object)cameraComponent == (Object)null)
		{
			return;
		}
		float num = MoveSpeed / PerfectPixelWithZoom.instance.getZoom();
		cameraPosition = ((Component)cameraComponent).transform.position;
		if (hasNewPosition)
		{
			cameraPosition = newCameraPosition;
			hasNewPosition = false;
		}
		else if (AllowMove)
		{
			float num2 = KeyManager.instance.HorizontalAxis();
			float num3 = KeyManager.instance.VerticalAxis();
			float hPos = cameraPosition.x;
			float vPos = cameraPosition.y;
			if (num2 > 0f)
			{
				horzMovement = 1;
			}
			else if (num2 < 0f)
			{
				horzMovement = -1;
			}
			if (num3 > 0f)
			{
				vertMovement = 1;
			}
			else if (num3 < 0f)
			{
				vertMovement = -1;
			}
			bool flag = false;
			if (Mathf.Abs(num2) > 0.001f)
			{
				hPos += num2 * num * Time.smoothDeltaTime;
				flag = true;
			}
			if (Mathf.Abs(num3) > 0.001f)
			{
				vPos += num3 * num * Time.smoothDeltaTime;
				flag = true;
			}
			if (flag && GameData.Instance.game_type == 4)
			{
				EngineInterface.TutorialAction(1);
			}
			if (mapLocked)
			{
				boundsFixCamera(ref hPos, ref vPos);
			}
			cameraPosition.x = hPos;
			cameraPosition.y = vPos;
		}
		((Component)cameraComponent).transform.position = cameraPosition;
		if ((!Director.instance.SimRunning && isMapLocked()) || (Director.instance.Paused && !EditorDirector.instance.overUI()))
		{
			AllowZoom = false;
		}
		if (AllowZoom)
		{
			if (!EditorDirector.instance.overUI())
			{
				if (Input.mouseScrollDelta.y != 0f)
				{
					if (Input.mouseScrollDelta.y > 0f)
					{
						if (ConfigSettings.Settings_SH1MouseWheel)
						{
							EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep);
						}
						else if (!lastZoomPositive || DateTime.UtcNow > zoomDelay)
						{
							lastZoomPositive = true;
							if (ConfigSettings.Settings_ExtraZoom)
							{
								zoomDelay = DateTime.UtcNow.AddMilliseconds(150.0);
								PerfectPixelWithZoom.instance.adjustZoom(0.5f);
							}
							else
							{
								zoomDelay = DateTime.UtcNow.AddMilliseconds(300.0);
								PerfectPixelWithZoom.instance.adjustZoom(1f);
							}
						}
					}
					if (Input.mouseScrollDelta.y < 0f)
					{
						if (ConfigSettings.Settings_SH1MouseWheel)
						{
							if ((DateTime.UtcNow - lastCycleBookmarks).TotalMilliseconds > 250.0)
							{
								lastCycleBookmarks = DateTime.UtcNow;
								EngineInterface.GameAction(Enums.GameActionCommand.CycleBookmarks, 0, 0);
							}
						}
						else if (lastZoomPositive || DateTime.UtcNow > zoomDelay)
						{
							lastZoomPositive = false;
							if (ConfigSettings.Settings_ExtraZoom)
							{
								zoomDelay = DateTime.UtcNow.AddMilliseconds(150.0);
								PerfectPixelWithZoom.instance.adjustZoom(-0.5f);
							}
							else
							{
								zoomDelay = DateTime.UtcNow.AddMilliseconds(300.0);
								PerfectPixelWithZoom.instance.adjustZoom(-1f);
							}
						}
					}
				}
			}
			else if (Input.mouseScrollDelta.y != 0f)
			{
				if (MainViewModel.Instance.Show_HUD_Help)
				{
					MainViewModel.Instance.HUDHelp.MouseWheelScrolled(Input.mouseScrollDelta.y);
				}
				else if (MainViewModel.Instance.Show_HUD_Briefing && MainViewModel.Instance.BriefingMode == 3)
				{
					MainViewModel.Instance.HUDBriefingPanel.MouseWheelScrolled(Input.mouseScrollDelta.y);
				}
			}
		}
		AllowZoom = true;
	}

	public void BoundsCheckCamera()
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if (mapLocked)
		{
			float hPos = cameraPosition.x;
			float vPos = cameraPosition.y;
			boundsFixCamera(ref hPos, ref vPos);
			cameraPosition.x = hPos;
			cameraPosition.y = vPos;
			((Component)cameraComponent).transform.position = cameraPosition;
		}
	}

	public void boundsFixCamera(ref float hPos, ref float vPos)
	{
		float num = GameMap.instance.getMapTileSize();
		float orthographicSize = cameraComponent.orthographicSize;
		float zWidth = PerfectPixelWithZoom.instance.getZWidth();
		float zoom = PerfectPixelWithZoom.instance.getZoom();
		float num2 = FatControler.instance.SHLowerUIPoint / 64f / zoom;
		if (!MainControls.instance.IsUIVisible)
		{
			num2 = 0f;
		}
		float top = (float)GameMap.tilemapSize / 4f + num / 4f - 1f - orthographicSize;
		float bottom = (float)GameMap.tilemapSize / 4f - num / 4f + 1f + orthographicSize - num2;
		float bottomNoUI = (float)GameMap.tilemapSize / 4f - num / 4f + 1f + orthographicSize;
		float left = (0f - num) / 2f + zWidth + 1f;
		float right = num / 2f - zWidth - 1f;
		adjustBoundsForRotation(ref left, ref top, ref right, ref bottom, ref bottomNoUI);
		if (num / 2f - 2f < zWidth)
		{
			hPos = 0f;
		}
		else
		{
			if (hPos > right)
			{
				if ((Screen.width & 1) > 0)
				{
					hPos = right - 0.001f;
				}
				else
				{
					hPos = right;
				}
			}
			if (hPos < left)
			{
				if ((Screen.width & 1) > 0)
				{
					hPos = left + 0.001f;
				}
				else
				{
					hPos = left;
				}
			}
		}
		if (bottom >= top)
		{
			vPos = (bottom - top) / 2f + top;
		}
		else
		{
			if (vPos > top)
			{
				vPos = top;
			}
			if (vPos < bottom)
			{
				vPos = bottom;
			}
		}
		if (num2 > 0f && vPos < bottomNoUI)
		{
			float num3 = (vPos - bottom) / (bottomNoUI - bottom);
			if (num3 < 0f)
			{
				num3 = 0f;
			}
			MainViewModel.Instance.MapLowerEdgeMaskHeight = ((int)(125f - 125f * num3)).ToString();
			MainViewModel.Instance.MapLowerEdgeMaskVisible = (Visibility)2;
		}
		else
		{
			MainViewModel.Instance.MapLowerEdgeMaskVisible = (Visibility)1;
		}
	}

	public Vector2Int getScreenCentreTileXY(int scrTilesWide, int scrTilesHigh)
	{
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		float x = cameraPosition.x;
		float y = cameraPosition.y;
		float num = GameMap.instance.getMapTileSize();
		float orthographicSize = cameraComponent.orthographicSize;
		float zWidth = PerfectPixelWithZoom.instance.getZWidth();
		float zoom = PerfectPixelWithZoom.instance.getZoom();
		float num2 = FatControler.instance.SHLowerUIPoint / 64f / zoom;
		if (!MainControls.instance.IsUIVisible)
		{
			num2 = 0f;
		}
		float top = (float)GameMap.tilemapSize / 4f + num / 4f - 1f - orthographicSize;
		float bottom = (float)GameMap.tilemapSize / 4f - num / 4f + 1f + orthographicSize - num2;
		float left = (0f - num) / 2f + zWidth + 1f;
		float right = num / 2f - zWidth - 1f;
		adjustBoundsForRotation(ref left, ref top, ref right, ref bottom);
		float num3 = right - left;
		float num4 = (x - left) / num3;
		float num5 = (float)(GameMap.tilemapSize - scrTilesWide * 2) * num4;
		float num6 = top - bottom;
		float num7 = (y - bottom) / num6;
		float num8 = (float)(GameMap.tilemapSize - scrTilesHigh) * num7;
		return new Vector2Int((int)num5 + scrTilesWide, GameMap.tilemapSize - scrTilesHigh - (int)num8 - 1 + scrTilesHigh / 2);
	}

	public Vector2Int getScreenXYForSaveCentring(int scrTilesWide, int scrTilesHigh)
	{
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		float x = cameraPosition.x;
		float y = cameraPosition.y;
		float num = GameMap.instance.getMapTileSize();
		float orthographicSize = cameraComponent.orthographicSize;
		float zWidth = PerfectPixelWithZoom.instance.getZWidth();
		float zoom = PerfectPixelWithZoom.instance.getZoom();
		float num2 = FatControler.instance.SHLowerUIPoint / 64f / zoom;
		if (!MainControls.instance.IsUIVisible)
		{
			num2 = 0f;
		}
		float top = (float)GameMap.tilemapSize / 4f + num / 4f - 1f - orthographicSize;
		float bottom = (float)GameMap.tilemapSize / 4f - num / 4f + 1f + orthographicSize - num2;
		float left = (0f - num) / 2f + zWidth + 1f;
		float right = num / 2f - zWidth - 1f;
		adjustBoundsForRotation(ref left, ref top, ref right, ref bottom);
		float num3 = right - left;
		float num4 = (x - left) / num3;
		float num5 = (float)(GameMap.tilemapSize - scrTilesWide * 2) * num4;
		float num6 = top - bottom;
		float num7 = (y - bottom) / num6;
		float num8 = (float)(GameMap.tilemapSize - scrTilesHigh) * num7;
		int num9 = ((int)num5 / 3 + 26) * 32;
		int num10 = ((GameMap.tilemapSize - scrTilesHigh - (int)num8 - 1) / 3 + 26) * 16;
		return new Vector2Int(num9, num10);
	}

	public void adjustBoundsForRotation(ref float left, ref float top, ref float right, ref float bottom)
	{
		float bottomNoUI = bottom;
		adjustBoundsForRotation(ref left, ref top, ref right, ref bottom, ref bottomNoUI);
	}

	public void adjustBoundsForRotation(ref float left, ref float top, ref float right, ref float bottom, ref float bottomNoUI)
	{
		switch (GameMap.instance.CurrentRotation())
		{
		case Enums.Dircs.North:
			top += 0.5f;
			if (PerfectPixelWithZoom.instance.getZoom() != 1f)
			{
				bottom -= 0.25f;
				bottomNoUI -= 0.25f;
			}
			break;
		case Enums.Dircs.East:
			top += 0.5f;
			right -= 1f;
			break;
		case Enums.Dircs.South:
			top += 0.5f;
			bottom -= 0.25f;
			bottomNoUI -= 0.25f;
			break;
		case Enums.Dircs.West:
			top += 0.5f;
			bottom -= 0.25f;
			bottomNoUI -= 0.25f;
			left += 1f;
			break;
		case Enums.Dircs.NE:
		case Enums.Dircs.SE:
		case Enums.Dircs.SW:
			break;
		}
	}

	public float getCameraXPos()
	{
		return cameraPosition.x;
	}

	public float getCameraYPos()
	{
		return cameraPosition.y;
	}

	public void setCameraPos(float xPos, float yPos)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (mapLocked)
		{
			boundsFixCamera(ref xPos, ref yPos);
		}
		if ((Screen.width & 1) > 0)
		{
			cameraPosition.x = xPos + 0.001f;
		}
		else
		{
			cameraPosition.x = xPos;
		}
		cameraPosition.y = yPos;
		((Component)cameraComponent).transform.position = cameraPosition;
	}

	public void toggleMapLocked()
	{
		if (mapLocked)
		{
			mapLocked = false;
		}
		else
		{
			mapLocked = true;
		}
	}

	public bool isMapLocked()
	{
		return mapLocked;
	}

	public void debarZoom()
	{
		AllowZoom = false;
	}
}
