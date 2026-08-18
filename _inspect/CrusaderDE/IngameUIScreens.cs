using System;
using Noesis;
using NoesisApp;
using UnityEngine;

namespace CrusaderDE;

public class IngameUIScreens : UserControl
{
	public Button RefToggleScenarioButton;

	public Rectangle RefGuideRect;

	public Grid RefHUD_ObjectivesPanel;

	public Grid refHUD_Objectives;

	public Grid refHUD_Goods;

	public Grid refCompass;

	public Image refCompassImg;

	public MediaElement refMissionOverVideo;

	public Image refMPLogo;

	public MediaElement refBannerDropVideoCV;

	public MediaElement refBannerDropVideoAV;

	public MediaElement refBannerDropVideoBV;

	public MediaElement refBannerDropVideoCD;

	public MediaElement refBannerDropVideoAD;

	public MediaElement refBannerDropVideoBD;

	public TextBlock refBannerLoopText;

	public Storyboard refBannerTextFade;

	public Image RefOST_Sands_Target_Bar_1;

	public Image RefOST_Sands_Target_Bar_2;

	public Image RefOST_Sands_Target_Bar_3;

	public Image RefOST_Sands_Target_Bar_4;

	public Image RefOST_Sands_Target_Bar_5;

	public bool videosTriggered;

	public IngameUIScreens()
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
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Expected O, but got Unknown
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Expected O, but got Unknown
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Expected O, but got Unknown
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Expected O, but got Unknown
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Expected O, but got Unknown
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Expected O, but got Unknown
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Expected O, but got Unknown
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Expected O, but got Unknown
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Expected O, but got Unknown
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Expected O, but got Unknown
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Expected O, but got Unknown
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Expected O, but got Unknown
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Expected O, but got Unknown
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Expected O, but got Unknown
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Expected O, but got Unknown
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Expected O, but got Unknown
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Expected O, but got Unknown
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Expected O, but got Unknown
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Expected O, but got Unknown
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Expected O, but got Unknown
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Expected O, but got Unknown
		//IL_039a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Expected O, but got Unknown
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ba: Expected O, but got Unknown
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Expected O, but got Unknown
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e6: Expected O, but got Unknown
		//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fc: Expected O, but got Unknown
		((FrameworkElement)this).DataContext = MainViewModel.Instance;
		InitializeComponent();
		MainViewModel.Instance.IngameUI = this;
		RefToggleScenarioButton = (Button)((FrameworkElement)this).FindName("ButtonToggleScenerioEditor");
		RefGuideRect = (Rectangle)((FrameworkElement)this).FindName("GuideRect");
		RefHUD_ObjectivesPanel = (Grid)((FrameworkElement)this).FindName("HUD_ObjectivesPanel");
		refHUD_Objectives = (Grid)((FrameworkElement)this).FindName("HUD_Objectives");
		refHUD_Goods = (Grid)((FrameworkElement)this).FindName("HUD_Goods");
		refCompass = (Grid)((FrameworkElement)this).FindName("Compass");
		refCompassImg = (Image)((FrameworkElement)this).FindName("CompassImg");
		refMissionOverVideo = (MediaElement)((FrameworkElement)this).FindName("MissionOverVideo");
		refMPLogo = (Image)((FrameworkElement)this).FindName("MPLogo");
		refMPLogo.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_FRONTEND_LOGO);
		refBannerLoopText = (TextBlock)((FrameworkElement)this).FindName("BannerLoopText");
		refBannerDropVideoCV = (MediaElement)((FrameworkElement)this).FindName("BannerDropVideoCV");
		refBannerDropVideoAV = (MediaElement)((FrameworkElement)this).FindName("BannerDropVideoAV");
		refBannerDropVideoBV = (MediaElement)((FrameworkElement)this).FindName("BannerDropVideoBV");
		refBannerDropVideoCD = (MediaElement)((FrameworkElement)this).FindName("BannerDropVideoCD");
		refBannerDropVideoAD = (MediaElement)((FrameworkElement)this).FindName("BannerDropVideoAD");
		refBannerDropVideoBD = (MediaElement)((FrameworkElement)this).FindName("BannerDropVideoBD");
		refBannerDropVideoCV.MediaEnded += new RoutedEventHandler(DropVideo_Ended);
		refBannerDropVideoAV.MediaEnded += new RoutedEventHandler(DropVideo_Ended);
		refBannerDropVideoBV.MediaEnded += new RoutedEventHandler(DropVideo_Ended);
		refBannerDropVideoCV.MediaEnded += new RoutedEventHandler(DropVideo_Ended);
		refBannerDropVideoAD.MediaEnded += new RoutedEventHandler(DropVideo_Ended);
		refBannerDropVideoBD.MediaEnded += new RoutedEventHandler(DropVideo_Ended);
		refBannerDropVideoCV.MediaOpened += new RoutedEventHandler(DropVideo_OpenedCV);
		refBannerDropVideoAV.MediaOpened += new RoutedEventHandler(DropVideo_OpenedAV);
		refBannerDropVideoBV.MediaOpened += new RoutedEventHandler(DropVideo_OpenedBV);
		refBannerDropVideoCD.MediaOpened += new RoutedEventHandler(DropVideo_OpenedCD);
		refBannerDropVideoAD.MediaOpened += new RoutedEventHandler(DropVideo_OpenedAD);
		refBannerDropVideoBD.MediaOpened += new RoutedEventHandler(DropVideo_OpenedBD);
		refBannerDropVideoCV.Source = new Uri("Assets/GUI/Video/Banner-Crusader-Victory.webm*", UriKind.Relative);
		refBannerDropVideoAV.Source = new Uri("Assets/GUI/Video/Banner-Arab-Victory.webm*", UriKind.Relative);
		refBannerDropVideoBV.Source = new Uri("Assets/GUI/Video/Banner-Bedouin-Victory.webm*", UriKind.Relative);
		refBannerDropVideoCD.Source = new Uri("Assets/GUI/Video/Banner-Crusader-Defeat.webm*", UriKind.Relative);
		refBannerDropVideoAD.Source = new Uri("Assets/GUI/Video/Banner-Arab-Defeat.webm*", UriKind.Relative);
		refBannerDropVideoBD.Source = new Uri("Assets/GUI/Video/Banner-Bedouin-Defeat.webm*", UriKind.Relative);
		((UIElement)refBannerDropVideoCV).Visibility = (Visibility)1;
		((UIElement)refBannerDropVideoAV).Visibility = (Visibility)1;
		((UIElement)refBannerDropVideoBV).Visibility = (Visibility)1;
		((UIElement)refBannerDropVideoCD).Visibility = (Visibility)1;
		((UIElement)refBannerDropVideoAD).Visibility = (Visibility)1;
		((UIElement)refBannerDropVideoBD).Visibility = (Visibility)1;
		refBannerTextFade = (Storyboard)((FrameworkElement)this).TryFindResource((object)"BannerTextFade");
		RefOST_Sands_Target_Bar_1 = (Image)((FrameworkElement)this).FindName("OST_Sands_Target_Bar_1");
		RefOST_Sands_Target_Bar_2 = (Image)((FrameworkElement)this).FindName("OST_Sands_Target_Bar_2");
		RefOST_Sands_Target_Bar_3 = (Image)((FrameworkElement)this).FindName("OST_Sands_Target_Bar_3");
		RefOST_Sands_Target_Bar_4 = (Image)((FrameworkElement)this).FindName("OST_Sands_Target_Bar_4");
		RefOST_Sands_Target_Bar_5 = (Image)((FrameworkElement)this).FindName("OST_Sands_Target_Bar_5");
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAML/IngameUIScreens.xaml");
	}

	public void findUIlowerPoint()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		if (!((BaseComponent)(object)RefGuideRect == (BaseComponent)null) && FatControler.instance.SHLowerUIPoint == 0f && (BaseComponent)(object)((Visual)RefGuideRect).View != (BaseComponent)null)
		{
			Point val = ((Visual)RefGuideRect).PointToScreen(new Point(-2f, -2f));
			FatControler.instance.SHLowerUIPoint = (float)Screen.height - ((Point)(ref val)).Y;
		}
	}

	public void setRotationImage(Enums.Dircs rotation)
	{
		switch (rotation)
		{
		case Enums.Dircs.North:
			refCompassImg.Source = MainViewModel.Instance.GameSprites[93];
			break;
		case Enums.Dircs.East:
			refCompassImg.Source = MainViewModel.Instance.GameSprites[94];
			break;
		case Enums.Dircs.South:
			refCompassImg.Source = MainViewModel.Instance.GameSprites[95];
			break;
		case Enums.Dircs.West:
			refCompassImg.Source = MainViewModel.Instance.GameSprites[96];
			break;
		case Enums.Dircs.NE:
		case Enums.Dircs.SE:
		case Enums.Dircs.SW:
			break;
		}
	}

	public void DropVideo_OpenedCV(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoCV.Stop();
		((UIElement)refBannerDropVideoCV).Visibility = (Visibility)1;
	}

	public void DropVideo_OpenedAV(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoAV.Stop();
		((UIElement)refBannerDropVideoAV).Visibility = (Visibility)1;
	}

	public void DropVideo_OpenedBV(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoBV.Stop();
		((UIElement)refBannerDropVideoBV).Visibility = (Visibility)1;
	}

	public void DropVideo_OpenedCD(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoCD.Stop();
		((UIElement)refBannerDropVideoCD).Visibility = (Visibility)1;
	}

	public void DropVideo_OpenedAD(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoAD.Stop();
		((UIElement)refBannerDropVideoAD).Visibility = (Visibility)1;
	}

	public void DropVideo_OpenedBD(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoBD.Stop();
		((UIElement)refBannerDropVideoBD).Visibility = (Visibility)1;
	}

	public void DropVideo_Ended(object sender, RoutedEventArgs args)
	{
	}

	public void clearVideos()
	{
		((UIElement)refBannerDropVideoCV).Visibility = (Visibility)1;
		refBannerDropVideoCV.Stop();
		((UIElement)refBannerDropVideoAV).Visibility = (Visibility)1;
		refBannerDropVideoAV.Stop();
		((UIElement)refBannerDropVideoBV).Visibility = (Visibility)1;
		refBannerDropVideoBV.Stop();
		((UIElement)refBannerDropVideoCD).Visibility = (Visibility)1;
		refBannerDropVideoCD.Stop();
		((UIElement)refBannerDropVideoAD).Visibility = (Visibility)1;
		refBannerDropVideoAD.Stop();
		((UIElement)refBannerDropVideoBD).Visibility = (Visibility)1;
		refBannerDropVideoBD.Stop();
		((UIElement)refBannerLoopText).Visibility = (Visibility)1;
		refBannerTextFade.Stop();
		((UIElement)refBannerLoopText).Opacity = 0f;
		videosTriggered = false;
	}

	public void triggerVideos(string message, int faction, bool victory)
	{
		GameData.scenario.inGameoverSituationVideos = true;
		if (MainViewModel.Instance.Show_HUD_IngameMenu)
		{
			MainViewModel.Instance.HUDmain.InGameOptions(null, null);
		}
		videosTriggered = true;
		refBannerLoopText.Text = message;
		if (victory)
		{
			SFXManager.instance.playSound(320, 1f, 0f, unstoppable: true);
		}
		else
		{
			SFXManager.instance.playSound(321, 1f, 0f, unstoppable: true);
		}
		switch (faction)
		{
		case 0:
			if (victory)
			{
				((UIElement)refBannerDropVideoCV).Visibility = (Visibility)2;
				refBannerDropVideoCV.Play();
			}
			else
			{
				((UIElement)refBannerDropVideoCD).Visibility = (Visibility)2;
				refBannerDropVideoCD.Play();
			}
			break;
		case 1:
			if (victory)
			{
				((UIElement)refBannerDropVideoAV).Visibility = (Visibility)2;
				refBannerDropVideoAV.Play();
			}
			else
			{
				((UIElement)refBannerDropVideoAD).Visibility = (Visibility)2;
				refBannerDropVideoAD.Play();
			}
			break;
		case 2:
			if (victory)
			{
				((UIElement)refBannerDropVideoBV).Visibility = (Visibility)2;
				refBannerDropVideoBV.Play();
			}
			else
			{
				((UIElement)refBannerDropVideoBD).Visibility = (Visibility)2;
				refBannerDropVideoBD.Play();
			}
			break;
		}
		((UIElement)refBannerLoopText).Visibility = (Visibility)2;
		((UIElement)refBannerLoopText).Opacity = 0f;
		refBannerTextFade.Begin();
	}
}
