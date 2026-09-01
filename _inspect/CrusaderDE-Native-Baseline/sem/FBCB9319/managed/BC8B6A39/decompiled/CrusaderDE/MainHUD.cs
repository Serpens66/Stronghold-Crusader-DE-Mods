using Noesis;
using NoesisApp;

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

	private int numPulsesToDo;

	public MainHUD()
	{
		base.DataContext = MainViewModel.Instance;
		InitializeComponent();
		MainViewModel.Instance.HUDRoot = this;
		RefRadarME = (MediaElement)FindName("RadarME");
		MainViewModel.Instance.RadarME = RefRadarME;
		RefRadarME.MediaEnded += RadarME_Ended;
		RefRadarME.MediaOpened += RadarME_Loaded;
		RefRadarMapGrid = (Grid)FindName("RadarMapGrid");
		RefRadarMapImage = (Image)FindName("RadarMapImage");
		RefReportsControlGrid = (Grid)FindName("ReportsControl");
		RefMainPopularityValue = (TextBlock)FindName("MainPopularityValue");
		RefPulse = (Storyboard)TryFindResource("Pulse");
		RefPulse.Completed += PulseCompleted;
		RefGameHoverText = (TextBlock)FindName("GameHoverText");
		MainViewModel.Instance.HUDmain.RefTutorialArrow5 = (Image)FindName("TutorialArrow5");
		if (FatControler.instance != null && RefRadarMapImage != null)
		{
			FatControler.instance.SHRadarRectSize = (int)RefRadarMapImage.Width;
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

	private void PulseCompleted(object sender, EventArgs e)
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

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAML/MainHUD.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "Loaded" && handlerName == "OnLoadRadarGrid")
		{
			((Grid)source).Loaded += OnLoadRadarGrid;
			return true;
		}
		if (eventName == "Unloaded" && handlerName == "OnUnLoadRadarGrid")
		{
			((Grid)source).Unloaded += OnUnLoadRadarGrid;
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

	private void RadarME_Ended(object sender, RoutedEventArgs args)
	{
		if (SFXManager.instance.requestBinkPlayState == 1)
		{
			if (SFXManager.instance.binkWaitForSpeech && MyAudioManager.Instance.isSpeechPlaying(1))
			{
				SFXManager.instance.requestBinkPlayState = 3;
				RefRadarME.Opacity = 0f;
				return;
			}
			RefRadarME.Stop();
			RefRadarME.Source = null;
			RefRadarME.Close();
			SFXManager.instance.binkIsPlaying = false;
			SFXManager.instance.requestBinkPlayState = 0;
			RefRadarME.Opacity = 0f;
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
		RefRadarME.Opacity = 0f;
	}

	private void RadarME_Loaded(object sender, RoutedEventArgs args)
	{
	}
}
