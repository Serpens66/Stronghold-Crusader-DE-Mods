using System;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class HUD_RightClick : UserControl
{
	public Vector3 clickCentre;

	public const int RotationSpeed = 1;

	public bool ActionTaken;

	public bool FlattenHeld;

	public bool RotationHeld;

	public DateTime RotationTime = DateTime.MinValue;

	public Grid RefRotation;

	public Grid RefUI;

	public Grid RefZoom;

	public Grid RefFlatten;

	public Image RefRotationImg;

	public Image RefUIImg;

	public Image RefZoomImg;

	public Image RefFlattenImg;

	public HUD_RightClick()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Expected O, but got Unknown
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Expected O, but got Unknown
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDRightClick = this;
		RefRotation = (Grid)((FrameworkElement)this).FindName("Rotation");
		RefUI = (Grid)((FrameworkElement)this).FindName("UI");
		RefZoom = (Grid)((FrameworkElement)this).FindName("Zoom");
		RefFlatten = (Grid)((FrameworkElement)this).FindName("Flatten");
		RefRotationImg = (Image)((FrameworkElement)this).FindName("RotationImg");
		RefUIImg = (Image)((FrameworkElement)this).FindName("UIImg");
		RefZoomImg = (Image)((FrameworkElement)this).FindName("ZoomImg");
		RefFlattenImg = (Image)((FrameworkElement)this).FindName("FlattenImg");
	}

	public void Open()
	{
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
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
			MainViewModel.Instance.RightclickMargin = new Thickness((float)num2, (float)num, -500f, -500f);
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
			((UIElement)RefRotation).Visibility = (Visibility)2;
			((UIElement)RefUI).Visibility = (Visibility)2;
			((UIElement)RefZoom).Visibility = (Visibility)2;
			((UIElement)RefFlatten).Visibility = (Visibility)2;
			MainViewModel.Instance.Show_HUD_RightClick = true;
		}
	}

	public void setRotationImage(Enums.Dircs rotation)
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
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_032d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0332: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
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
					((UIElement)RefRotation).Visibility = (Visibility)1;
					((UIElement)RefUI).Visibility = (Visibility)1;
					((UIElement)RefZoom).Visibility = (Visibility)2;
					((UIElement)RefFlatten).Visibility = (Visibility)1;
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
					((UIElement)RefRotation).Visibility = (Visibility)1;
					((UIElement)RefUI).Visibility = (Visibility)2;
					((UIElement)RefZoom).Visibility = (Visibility)1;
					((UIElement)RefFlatten).Visibility = (Visibility)1;
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
					((UIElement)RefRotation).Visibility = (Visibility)1;
					((UIElement)RefUI).Visibility = (Visibility)1;
					((UIElement)RefZoom).Visibility = (Visibility)1;
					((UIElement)RefFlatten).Visibility = (Visibility)2;
				}
				else
				{
					ActionTaken = true;
					GameMap.instance.RotateMapRight();
					setRotationImage(GameMap.instance.PendingRotation());
					((UIElement)RefRotation).Visibility = (Visibility)2;
					((UIElement)RefUI).Visibility = (Visibility)1;
					((UIElement)RefZoom).Visibility = (Visibility)1;
					((UIElement)RefFlatten).Visibility = (Visibility)1;
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

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_RightClick.xaml");
	}
}
