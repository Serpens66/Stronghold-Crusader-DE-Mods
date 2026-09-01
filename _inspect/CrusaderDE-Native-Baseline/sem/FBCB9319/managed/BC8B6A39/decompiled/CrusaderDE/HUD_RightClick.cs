using System;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class HUD_RightClick : UserControl
{
	private Vector3 clickCentre;

	private const int RotationSpeed = 1;

	private bool ActionTaken;

	private bool FlattenHeld;

	private bool RotationHeld;

	private DateTime RotationTime = DateTime.MinValue;

	private Noesis.Grid RefRotation;

	private Noesis.Grid RefUI;

	private Noesis.Grid RefZoom;

	private Noesis.Grid RefFlatten;

	private Image RefRotationImg;

	private Image RefUIImg;

	private Image RefZoomImg;

	private Image RefFlattenImg;

	public HUD_RightClick()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDRightClick = this;
		RefRotation = (Noesis.Grid)FindName("Rotation");
		RefUI = (Noesis.Grid)FindName("UI");
		RefZoom = (Noesis.Grid)FindName("Zoom");
		RefFlatten = (Noesis.Grid)FindName("Flatten");
		RefRotationImg = (Image)FindName("RotationImg");
		RefUIImg = (Image)FindName("UIImg");
		RefZoomImg = (Image)FindName("ZoomImg");
		RefFlattenImg = (Image)FindName("FlattenImg");
	}

	public void Open()
	{
		bool flag = true;
		if (MainViewModel.Instance.Show_HUD_FreebuildMenu || MainViewModel.Instance.Show_HUD_LoadSaveRequester || MainViewModel.Instance.MPChatVisible || MainViewModel.Instance.Show_HUD_Confirmation || MainViewModel.Instance.Show_HUD_Briefing || MainViewModel.Instance.Show_HUD_Help || MainViewModel.Instance.Show_HUD_Options || MainViewModel.Instance.ShowingScenario || MainViewModel.Instance.Show_HUD_WorkshopUploader || MainViewModel.Instance.Show_HUD_IngameMenu || MainControls.instance.CurrentAction != 0 || (GameData.Instance.lastGameState != null && ((GameData.Instance.lastGameState.app_mode == 14 && (GameData.Instance.lastGameState.app_sub_mode == 61 || GameData.Instance.lastGameState.app_sub_mode == 62)) || GameData.Instance.lastGameState.app_mode == 16 || GameData.Instance.lastGameState.mouse_selector_state >= 4)))
		{
			flag = false;
		}
		if (flag)
		{
			ActionTaken = false;
			FlattenHeld = false;
			RotationHeld = false;
			clickCentre = Input.mousePosition;
			int num = Screen.height - (int)Input.mousePosition.y;
			int num2 = (int)Input.mousePosition.x;
			num2 = ((!FatControler.arabic || ConfigSettings.Settings_ArabicL2R) ? (MainViewModel.iUIScaleValueWidth * num2 / Screen.width) : (MainViewModel.iUIScaleValueWidth * (Screen.width - num2) / Screen.width));
			num = MainViewModel.iUIScaleValueHeight * num / Screen.height;
			MainViewModel.Instance.RightclickMargin = new Thickness(num2, num, -500f, -500f);
			if (MainControls.instance.IsUIVisible)
			{
				RefUIImg.Source = MainViewModel.Instance.GameSprites[100];
			}
			else
			{
				RefUIImg.Source = MainViewModel.Instance.GameSprites[101];
			}
			RefZoomImg.Source = MainViewModel.Instance.GameSprites[97];
			if (EngineInterface.flattenedLandscape)
			{
				RefFlattenImg.Source = MainViewModel.Instance.GameSprites[92];
			}
			else
			{
				RefFlattenImg.Source = MainViewModel.Instance.GameSprites[91];
			}
			setRotationImage(GameMap.instance.CurrentRotation());
			RefRotation.Visibility = Visibility.Visible;
			RefUI.Visibility = Visibility.Visible;
			RefZoom.Visibility = Visibility.Visible;
			RefFlatten.Visibility = Visibility.Visible;
			MainViewModel.Instance.Show_HUD_RightClick = true;
		}
	}

	private void setRotationImage(Enums.Dircs rotation)
	{
		switch (rotation)
		{
		case Enums.Dircs.North:
			RefRotationImg.Source = MainViewModel.Instance.GameSprites[93];
			break;
		case Enums.Dircs.East:
			RefRotationImg.Source = MainViewModel.Instance.GameSprites[94];
			break;
		case Enums.Dircs.South:
			RefRotationImg.Source = MainViewModel.Instance.GameSprites[95];
			break;
		case Enums.Dircs.West:
			RefRotationImg.Source = MainViewModel.Instance.GameSprites[96];
			break;
		case Enums.Dircs.NE:
		case Enums.Dircs.SE:
		case Enums.Dircs.SW:
			break;
		}
	}

	public void Update()
	{
		if (!Input.GetMouseButton(1))
		{
			MainViewModel.Instance.Show_HUD_RightClick = false;
		}
		else if (!ActionTaken)
		{
			Vector3 mousePosition = Input.mousePosition;
			int num = (int)Mathf.Abs(mousePosition.x - clickCentre.x);
			int num2 = (int)Mathf.Abs(mousePosition.y - clickCentre.y);
			if (num <= 20 && num2 <= 20)
			{
				return;
			}
			if (num > num2)
			{
				if (mousePosition.x > clickCentre.x)
				{
					ActionTaken = true;
					if (GameData.Instance.game_type == 4)
					{
						EngineInterface.TutorialAction(3);
					}
					if (ConfigSettings.Settings_ExtraZoom)
					{
						PerfectPixelWithZoom.instance.adjustZoom(-0.5f, loop: true);
					}
					else
					{
						PerfectPixelWithZoom.instance.adjustZoom(-1f, loop: true);
					}
					RefZoomImg.Source = MainViewModel.Instance.GameSprites[99];
					RefRotation.Visibility = Visibility.Hidden;
					RefUI.Visibility = Visibility.Hidden;
					RefZoom.Visibility = Visibility.Visible;
					RefFlatten.Visibility = Visibility.Hidden;
				}
				else
				{
					ActionTaken = true;
					MainControls.instance.toggleUIVisibility();
					if (MainControls.instance.IsUIVisible)
					{
						RefUIImg.Source = MainViewModel.Instance.GameSprites[100];
					}
					else
					{
						RefUIImg.Source = MainViewModel.Instance.GameSprites[101];
					}
					RefRotation.Visibility = Visibility.Hidden;
					RefUI.Visibility = Visibility.Visible;
					RefZoom.Visibility = Visibility.Hidden;
					RefFlatten.Visibility = Visibility.Hidden;
				}
			}
			else
			{
				if (Director.instance.Paused)
				{
					return;
				}
				if (mousePosition.y < clickCentre.y)
				{
					ActionTaken = true;
					EngineInterface.toggleFlattenedLandscapeMode();
					FlattenHeld = true;
					if (EngineInterface.flattenedLandscape)
					{
						RefFlattenImg.Source = MainViewModel.Instance.GameSprites[92];
					}
					else
					{
						clickCentre = mousePosition;
						clickCentre.y -= 60f;
						RefFlattenImg.Source = MainViewModel.Instance.GameSprites[91];
					}
					RefRotation.Visibility = Visibility.Hidden;
					RefUI.Visibility = Visibility.Hidden;
					RefZoom.Visibility = Visibility.Hidden;
					RefFlatten.Visibility = Visibility.Visible;
				}
				else
				{
					ActionTaken = true;
					GameMap.instance.RotateMapRight();
					setRotationImage(GameMap.instance.PendingRotation());
					RefRotation.Visibility = Visibility.Visible;
					RefUI.Visibility = Visibility.Hidden;
					RefZoom.Visibility = Visibility.Hidden;
					RefFlatten.Visibility = Visibility.Hidden;
					RotationHeld = true;
					RotationTime = DateTime.UtcNow.AddSeconds(1.0);
				}
			}
		}
		else if (RotationHeld && DateTime.UtcNow > RotationTime)
		{
			GameMap.instance.RotateMapRight();
			setRotationImage(GameMap.instance.PendingRotation());
			RotationTime = DateTime.UtcNow.AddSeconds(1.0);
		}
		else if (FlattenHeld)
		{
			Vector3 mousePosition2 = Input.mousePosition;
			int num3 = (int)Mathf.Abs(mousePosition2.y - clickCentre.y);
			if (num3 < 10 || mousePosition2.y > clickCentre.y)
			{
				EngineInterface.setFlattenedLandscapeMode(state: true);
				EngineInterface.toggleFlattenedLandscapeMode();
			}
			else if (num3 > 20 && mousePosition2.y < clickCentre.y)
			{
				EngineInterface.setFlattenedLandscapeMode(state: false);
				EngineInterface.toggleFlattenedLandscapeMode();
			}
		}
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_RightClick.xaml");
	}
}
