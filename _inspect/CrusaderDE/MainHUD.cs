using Noesis;
using NoesisApp;
using UnityEngine;

namespace CrusaderDE;

public class MainHUD : UserControl
{
	public MediaElement RefRadarME;

	public Grid RefRadarMapGrid;

	public Image RefRadarMapImage;

	public Grid RefReportsControlGrid;

	public bool RadarMEPlayOnLoad;

	public TextBlock RefMainPopularityValue;

	public Storyboard RefPulse;

	public TextBlock RefGameHoverText;

	public int numPulsesToDo;

	public MainHUD()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Expected O, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Expected O, but got Unknown
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Expected O, but got Unknown
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Expected O, but got Unknown
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Expected O, but got Unknown
		((FrameworkElement)this).DataContext = MainViewModel.Instance;
		InitializeComponent();
		MainViewModel.Instance.HUDRoot = this;
		RefRadarME = (MediaElement)((FrameworkElement)this).FindName("RadarME");
		MainViewModel.Instance.RadarME = RefRadarME;
		RefRadarME.MediaEnded += new RoutedEventHandler(RadarME_Ended);
		RefRadarME.MediaOpened += new RoutedEventHandler(RadarME_Loaded);
		RefRadarMapGrid = (Grid)((FrameworkElement)this).FindName("RadarMapGrid");
		RefRadarMapImage = (Image)((FrameworkElement)this).FindName("RadarMapImage");
		RefReportsControlGrid = (Grid)((FrameworkElement)this).FindName("ReportsControl");
		RefMainPopularityValue = (TextBlock)((FrameworkElement)this).FindName("MainPopularityValue");
		RefPulse = (Storyboard)((FrameworkElement)this).TryFindResource((object)"Pulse");
		((Timeline)RefPulse).Completed += new CompletedHandler(PulseCompleted);
		RefGameHoverText = (TextBlock)((FrameworkElement)this).FindName("GameHoverText");
		MainViewModel.Instance.HUDmain.RefTutorialArrow5 = (Image)((FrameworkElement)this).FindName("TutorialArrow5");
		if ((Object)(object)FatControler.instance != (Object)null && (BaseComponent)(object)RefRadarMapImage != (BaseComponent)null)
		{
			FatControler.instance.SHRadarRectSize = (int)((FrameworkElement)RefRadarMapImage).Width;
		}
		switch (FatControler.locale)
		{
		case "jajp":
		case "kokr":
		case "zhcn":
		case "zhhk":
		case "thth":
			MainViewModel.Instance.BookPopularityFontSize = 24;
			MainViewModel.Instance.BookGoldLargeFontSize = 14;
			MainViewModel.Instance.BookGoldSmallFontSize = 12;
			MainViewModel.Instance.BookPopulationFontSize = 14;
			break;
		case "dede":
			RefGameHoverText.FontSize = 18f;
			break;
		}
	}

	public void resetPulse()
	{
		numPulsesToDo = 0;
		RefPulse.Stop();
		FatControler.instance.lastPopularity = 100;
	}

	public void setPulsing(int numPulses)
	{
		if (numPulses == -1)
		{
			if (numPulsesToDo == 1000)
			{
				numPulsesToDo = 0;
			}
			return;
		}
		numPulsesToDo = numPulses;
		if (numPulses > 0)
		{
			if (!RefPulse.IsPlaying())
			{
				RefPulse.Begin();
			}
		}
		else if (RefPulse.IsPlaying())
		{
			RefPulse.Stop();
		}
	}

	public void PulseCompleted(object sender, EventArgs e)
	{
		if (numPulsesToDo > 0)
		{
			numPulsesToDo--;
			RefPulse.Begin();
		}
		else
		{
			RefPulse.Stop();
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAML/MainHUD.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		if (eventName == "Loaded" && handlerName == "OnLoadRadarGrid")
		{
			((FrameworkElement)(Grid)source).Loaded += new RoutedEventHandler(OnLoadRadarGrid);
			return true;
		}
		if (eventName == "Unloaded" && handlerName == "OnUnLoadRadarGrid")
		{
			((FrameworkElement)(Grid)source).Unloaded += new RoutedEventHandler(OnUnLoadRadarGrid);
			return true;
		}
		return false;
	}

	public void OnLoadRadarGrid(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.RadarLoaded = true;
	}

	public void OnUnLoadRadarGrid(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.RadarLoaded = false;
	}

	public void RadarME_Ended(object sender, RoutedEventArgs args)
	{
		if (SFXManager.instance.requestBinkPlayState == 1)
		{
			if (SFXManager.instance.binkWaitForSpeech && MyAudioManager.Instance.isSpeechPlaying(1))
			{
				SFXManager.instance.requestBinkPlayState = 3;
				((UIElement)RefRadarME).Opacity = 0f;
				return;
			}
			RefRadarME.Stop();
			RefRadarME.Source = null;
			RefRadarME.Close();
			SFXManager.instance.binkIsPlaying = false;
			SFXManager.instance.requestBinkPlayState = 0;
			((UIElement)RefRadarME).Opacity = 0f;
		}
		else
		{
			_ = SFXManager.instance.requestBinkPlayState;
			_ = 2;
		}
	}

	public void RadarME_Ended()
	{
		RefRadarME.Stop();
		RefRadarME.Source = null;
		RefRadarME.Close();
		SFXManager.instance.binkIsPlaying = false;
		SFXManager.instance.requestBinkPlayState = 0;
		((UIElement)RefRadarME).Opacity = 0f;
	}

	public void RadarME_Loaded(object sender, RoutedEventArgs args)
	{
	}
}
