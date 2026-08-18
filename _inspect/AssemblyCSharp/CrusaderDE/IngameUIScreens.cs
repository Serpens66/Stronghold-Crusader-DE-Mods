using System;
using Noesis;
using NoesisApp;
using UnityEngine;

namespace CrusaderDE;

public class IngameUIScreens : UserControl
{
	public Button RefToggleScenarioButton;

	private Rectangle RefGuideRect;

	public Noesis.Grid RefHUD_ObjectivesPanel;

	public Noesis.Grid refHUD_Objectives;

	public Noesis.Grid refHUD_Goods;

	public Noesis.Grid refCompass;

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

	private bool videosTriggered;

	public IngameUIScreens()
	{
		base.DataContext = MainViewModel.Instance;
		InitializeComponent();
		MainViewModel.Instance.IngameUI = this;
		RefToggleScenarioButton = (Button)FindName("ButtonToggleScenerioEditor");
		RefGuideRect = (Rectangle)FindName("GuideRect");
		RefHUD_ObjectivesPanel = (Noesis.Grid)FindName("HUD_ObjectivesPanel");
		refHUD_Objectives = (Noesis.Grid)FindName("HUD_Objectives");
		refHUD_Goods = (Noesis.Grid)FindName("HUD_Goods");
		refCompass = (Noesis.Grid)FindName("Compass");
		refCompassImg = (Image)FindName("CompassImg");
		refMissionOverVideo = (MediaElement)FindName("MissionOverVideo");
		refMPLogo = (Image)FindName("MPLogo");
		refMPLogo.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_FRONTEND_LOGO);
		refBannerLoopText = (TextBlock)FindName("BannerLoopText");
		refBannerDropVideoCV = (MediaElement)FindName("BannerDropVideoCV");
		refBannerDropVideoAV = (MediaElement)FindName("BannerDropVideoAV");
		refBannerDropVideoBV = (MediaElement)FindName("BannerDropVideoBV");
		refBannerDropVideoCD = (MediaElement)FindName("BannerDropVideoCD");
		refBannerDropVideoAD = (MediaElement)FindName("BannerDropVideoAD");
		refBannerDropVideoBD = (MediaElement)FindName("BannerDropVideoBD");
		refBannerDropVideoCV.MediaEnded += DropVideo_Ended;
		refBannerDropVideoAV.MediaEnded += DropVideo_Ended;
		refBannerDropVideoBV.MediaEnded += DropVideo_Ended;
		refBannerDropVideoCV.MediaEnded += DropVideo_Ended;
		refBannerDropVideoAD.MediaEnded += DropVideo_Ended;
		refBannerDropVideoBD.MediaEnded += DropVideo_Ended;
		refBannerDropVideoCV.MediaOpened += DropVideo_OpenedCV;
		refBannerDropVideoAV.MediaOpened += DropVideo_OpenedAV;
		refBannerDropVideoBV.MediaOpened += DropVideo_OpenedBV;
		refBannerDropVideoCD.MediaOpened += DropVideo_OpenedCD;
		refBannerDropVideoAD.MediaOpened += DropVideo_OpenedAD;
		refBannerDropVideoBD.MediaOpened += DropVideo_OpenedBD;
		refBannerDropVideoCV.Source = new Uri("Assets/GUI/Video/Banner-Crusader-Victory.webm*", UriKind.Relative);
		refBannerDropVideoAV.Source = new Uri("Assets/GUI/Video/Banner-Arab-Victory.webm*", UriKind.Relative);
		refBannerDropVideoBV.Source = new Uri("Assets/GUI/Video/Banner-Bedouin-Victory.webm*", UriKind.Relative);
		refBannerDropVideoCD.Source = new Uri("Assets/GUI/Video/Banner-Crusader-Defeat.webm*", UriKind.Relative);
		refBannerDropVideoAD.Source = new Uri("Assets/GUI/Video/Banner-Arab-Defeat.webm*", UriKind.Relative);
		refBannerDropVideoBD.Source = new Uri("Assets/GUI/Video/Banner-Bedouin-Defeat.webm*", UriKind.Relative);
		refBannerDropVideoCV.Visibility = Visibility.Hidden;
		refBannerDropVideoAV.Visibility = Visibility.Hidden;
		refBannerDropVideoBV.Visibility = Visibility.Hidden;
		refBannerDropVideoCD.Visibility = Visibility.Hidden;
		refBannerDropVideoAD.Visibility = Visibility.Hidden;
		refBannerDropVideoBD.Visibility = Visibility.Hidden;
		refBannerTextFade = (Storyboard)TryFindResource("BannerTextFade");
		RefOST_Sands_Target_Bar_1 = (Image)FindName("OST_Sands_Target_Bar_1");
		RefOST_Sands_Target_Bar_2 = (Image)FindName("OST_Sands_Target_Bar_2");
		RefOST_Sands_Target_Bar_3 = (Image)FindName("OST_Sands_Target_Bar_3");
		RefOST_Sands_Target_Bar_4 = (Image)FindName("OST_Sands_Target_Bar_4");
		RefOST_Sands_Target_Bar_5 = (Image)FindName("OST_Sands_Target_Bar_5");
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAML/IngameUIScreens.xaml");
	}

	public void findUIlowerPoint()
	{
		if (!(RefGuideRect == null) && FatControler.instance.SHLowerUIPoint == 0f && RefGuideRect.View != null)
		{
			Point point = RefGuideRect.PointToScreen(new Point(-2f, -2f));
			FatControler.instance.SHLowerUIPoint = (float)Screen.height - point.Y;
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

	private void DropVideo_OpenedCV(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoCV.Stop();
		refBannerDropVideoCV.Visibility = Visibility.Hidden;
	}

	private void DropVideo_OpenedAV(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoAV.Stop();
		refBannerDropVideoAV.Visibility = Visibility.Hidden;
	}

	private void DropVideo_OpenedBV(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoBV.Stop();
		refBannerDropVideoBV.Visibility = Visibility.Hidden;
	}

	private void DropVideo_OpenedCD(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoCD.Stop();
		refBannerDropVideoCD.Visibility = Visibility.Hidden;
	}

	private void DropVideo_OpenedAD(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoAD.Stop();
		refBannerDropVideoAD.Visibility = Visibility.Hidden;
	}

	private void DropVideo_OpenedBD(object sender, RoutedEventArgs args)
	{
		refBannerDropVideoBD.Stop();
		refBannerDropVideoBD.Visibility = Visibility.Hidden;
	}

	private void DropVideo_Ended(object sender, RoutedEventArgs args)
	{
	}

	public void clearVideos()
	{
		refBannerDropVideoCV.Visibility = Visibility.Hidden;
		refBannerDropVideoCV.Stop();
		refBannerDropVideoAV.Visibility = Visibility.Hidden;
		refBannerDropVideoAV.Stop();
		refBannerDropVideoBV.Visibility = Visibility.Hidden;
		refBannerDropVideoBV.Stop();
		refBannerDropVideoCD.Visibility = Visibility.Hidden;
		refBannerDropVideoCD.Stop();
		refBannerDropVideoAD.Visibility = Visibility.Hidden;
		refBannerDropVideoAD.Stop();
		refBannerDropVideoBD.Visibility = Visibility.Hidden;
		refBannerDropVideoBD.Stop();
		refBannerLoopText.Visibility = Visibility.Hidden;
		refBannerTextFade.Stop();
		refBannerLoopText.Opacity = 0f;
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
				refBannerDropVideoCV.Visibility = Visibility.Visible;
				refBannerDropVideoCV.Play();
			}
			else
			{
				refBannerDropVideoCD.Visibility = Visibility.Visible;
				refBannerDropVideoCD.Play();
			}
			break;
		case 1:
			if (victory)
			{
				refBannerDropVideoAV.Visibility = Visibility.Visible;
				refBannerDropVideoAV.Play();
			}
			else
			{
				refBannerDropVideoAD.Visibility = Visibility.Visible;
				refBannerDropVideoAD.Play();
			}
			break;
		case 2:
			if (victory)
			{
				refBannerDropVideoBV.Visibility = Visibility.Visible;
				refBannerDropVideoBV.Play();
			}
			else
			{
				refBannerDropVideoBD.Visibility = Visibility.Visible;
				refBannerDropVideoBD.Play();
			}
			break;
		}
		refBannerLoopText.Visibility = Visibility.Visible;
		refBannerLoopText.Opacity = 0f;
		refBannerTextFade.Begin();
	}
}
