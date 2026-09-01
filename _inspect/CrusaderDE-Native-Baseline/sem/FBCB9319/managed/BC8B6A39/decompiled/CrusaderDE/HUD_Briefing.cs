using System;
using Noesis;
using Steamworks;
using UnityEngine;
using Vuplex.WebView;

namespace CrusaderDE;

public class HUD_Briefing : UserControl
{
	public Noesis.Grid RefObjectivesSubPanel;

	public Noesis.Grid RefHintsSubPanel;

	public Noesis.Grid RefHelpSubPanel;

	public WGT_Objective[] RefWGTObjectives = new WGT_Objective[20];

	public Noesis.Grid RefObjectiveTimer;

	public Button RefBriefingDifficultyButton;

	public Button RefBriefingQuitButton;

	public Button[] RefBriefingHintButtons = new Button[5];

	public TextBlock[] RefBriefingHintTexts = new TextBlock[5];

	public RadioButton RefBriefingObjectivesButton;

	public RadioButton RefBriefingHintsButton;

	public RadioButton RefBriefingTutorialButton;

	public Noesis.Grid RefBriefingStrategySection;

	public Image RefBriefingHelpTexture;

	public Button RefBriefingHelpBackButton;

	public Image RefHintsTitleStamp;

	public TextBlock RefStrategyTextBlock;

	public Image RefRadarShield1;

	public Image RefRadarShield2;

	public Image RefRadarShield3;

	public Image RefRadarShield4;

	public Image RefRadarShield5;

	public Image RefRadarShield6;

	public Image RefRadarShield7;

	public Image RefRadarShield8;

	public Image RefRadarShieldTeam1;

	public Image RefRadarShieldTeam2;

	public Image RefRadarShieldTeam3;

	public Image RefRadarShieldTeam4;

	public Image RefRadarShieldTeam5;

	public Image RefRadarShieldTeam6;

	public Image RefRadarShieldTeam7;

	public Image RefRadarShieldTeam8;

	private IWebView webView;

	private bool webBrowserOpen;

	public bool webBrowserLoaded;

	private bool browserThumbHeld;

	public static bool mouseIsUpStroke = false;

	public static bool mouseIsDownStroke = false;

	public static float ViewportScale = 1f;

	private bool canGoBackValue;

	public HUD_Briefing()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDBriefingPanel = this;
		RefObjectivesSubPanel = (Noesis.Grid)FindName("BriefingObjectivesPanel");
		RefHintsSubPanel = (Noesis.Grid)FindName("BriefingHintsPanel");
		RefHelpSubPanel = (Noesis.Grid)FindName("BriefingHelpPanel");
		RefWGTObjectives[0] = (WGT_Objective)FindName("WGT_Objective1");
		RefWGTObjectives[1] = (WGT_Objective)FindName("WGT_Objective2");
		RefWGTObjectives[2] = (WGT_Objective)FindName("WGT_Objective3");
		RefWGTObjectives[3] = (WGT_Objective)FindName("WGT_Objective4");
		RefWGTObjectives[4] = (WGT_Objective)FindName("WGT_Objective5");
		RefWGTObjectives[5] = (WGT_Objective)FindName("WGT_Objective6");
		RefWGTObjectives[6] = (WGT_Objective)FindName("WGT_Objective7");
		RefWGTObjectives[7] = (WGT_Objective)FindName("WGT_Objective8");
		RefWGTObjectives[8] = (WGT_Objective)FindName("WGT_Objective9");
		RefWGTObjectives[9] = (WGT_Objective)FindName("WGT_Objective10");
		RefWGTObjectives[10] = (WGT_Objective)FindName("WGT_Objective11");
		RefWGTObjectives[11] = (WGT_Objective)FindName("WGT_Objective12");
		RefWGTObjectives[12] = (WGT_Objective)FindName("WGT_Objective13");
		RefWGTObjectives[13] = (WGT_Objective)FindName("WGT_Objective14");
		RefWGTObjectives[14] = (WGT_Objective)FindName("WGT_Objective15");
		RefWGTObjectives[15] = (WGT_Objective)FindName("WGT_Objective16");
		RefWGTObjectives[16] = (WGT_Objective)FindName("WGT_Objective17");
		RefWGTObjectives[17] = (WGT_Objective)FindName("WGT_Objective18");
		RefWGTObjectives[18] = (WGT_Objective)FindName("WGT_Objective19");
		RefWGTObjectives[19] = (WGT_Objective)FindName("WGT_Objective20");
		RefBriefingHintButtons[0] = (Button)FindName("BriefingHint1Button");
		RefBriefingHintButtons[1] = (Button)FindName("BriefingHint2Button");
		RefBriefingHintButtons[2] = (Button)FindName("BriefingHint3Button");
		RefBriefingHintButtons[3] = (Button)FindName("BriefingHint4Button");
		RefBriefingHintButtons[4] = (Button)FindName("BriefingHint5Button");
		RefBriefingHintTexts[0] = (TextBlock)FindName("BriefingHint1Text");
		RefBriefingHintTexts[1] = (TextBlock)FindName("BriefingHint2Text");
		RefBriefingHintTexts[2] = (TextBlock)FindName("BriefingHint3Text");
		RefBriefingHintTexts[3] = (TextBlock)FindName("BriefingHint4Text");
		RefBriefingHintTexts[4] = (TextBlock)FindName("BriefingHint5Text");
		RefObjectiveTimer = (Noesis.Grid)FindName("ObjectiveTimer");
		RefBriefingDifficultyButton = (Button)FindName("ButtonBriefingDifficulyLevel");
		RefBriefingQuitButton = (Button)FindName("BriefingQuitButton");
		RefBriefingObjectivesButton = (RadioButton)FindName("BriefingButton");
		RefBriefingHintsButton = (RadioButton)FindName("HintsButton");
		RefBriefingTutorialButton = (RadioButton)FindName("HelpButton");
		RefBriefingHelpBackButton = (Button)FindName("HelpBackButton");
		RefBriefingHelpTexture = (Image)FindName("BriefingHelpTexture");
		RefHintsTitleStamp = (Image)FindName("HintsTitleStamp");
		string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HINTS, 1);
		string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HINTS, 2);
		string text3 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MISSION_BUTTONS, 7);
		if (FatControler.arabic || FatControler.thai)
		{
			MainViewModel.Instance.BriefingStrategyS = "";
			MainViewModel.Instance.BriefingStrategytrategy = text;
			MainViewModel.Instance.BriefingHintsH = "";
			MainViewModel.Instance.BriefingHintsints = text2;
			MainViewModel.Instance.BriefingObjectivesO = "";
			MainViewModel.Instance.BriefingObjectivesbjectives = text3;
		}
		else
		{
			MainViewModel.Instance.BriefingStrategyS = text.Substring(0, 1);
			MainViewModel.Instance.BriefingStrategytrategy = text.Substring(1, text.Length - 1);
			MainViewModel.Instance.BriefingHintsH = text2.Substring(0, 1);
			MainViewModel.Instance.BriefingHintsints = text2.Substring(1, text2.Length - 1);
			MainViewModel.Instance.BriefingObjectivesO = text3.Substring(0, 1);
			MainViewModel.Instance.BriefingObjectivesbjectives = text3.Substring(1, text3.Length - 1);
		}
		RefBriefingStrategySection = (Noesis.Grid)FindName("BriefingStrategySection");
		RefStrategyTextBlock = (TextBlock)FindName("StrategyTextBlock");
		RefRadarShield1 = (Image)FindName("RadarShield1");
		RefRadarShield2 = (Image)FindName("RadarShield2");
		RefRadarShield3 = (Image)FindName("RadarShield3");
		RefRadarShield4 = (Image)FindName("RadarShield4");
		RefRadarShield5 = (Image)FindName("RadarShield5");
		RefRadarShield6 = (Image)FindName("RadarShield6");
		RefRadarShield7 = (Image)FindName("RadarShield7");
		RefRadarShield8 = (Image)FindName("RadarShield8");
		RefRadarShieldTeam1 = (Image)FindName("RadarShieldTeam1");
		RefRadarShieldTeam2 = (Image)FindName("RadarShieldTeam2");
		RefRadarShieldTeam3 = (Image)FindName("RadarShieldTeam3");
		RefRadarShieldTeam4 = (Image)FindName("RadarShieldTeam4");
		RefRadarShieldTeam5 = (Image)FindName("RadarShieldTeam5");
		RefRadarShieldTeam6 = (Image)FindName("RadarShieldTeam6");
		RefRadarShieldTeam7 = (Image)FindName("RadarShieldTeam7");
		RefRadarShieldTeam8 = (Image)FindName("RadarShieldTeam8");
		if (FatControler.thai)
		{
			RefStrategyTextBlock.FontSize = 14f;
		}
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Briefing.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			return true;
		}
		return false;
	}

	public void OpenHelpInOverlay()
	{
		string text = "";
		text = ((GameData.Instance.game_type != 0) ? ("file://" + Application.dataPath + "/StreamingAssets/Help/help_main.html") : ("file://" + Application.dataPath + "/StreamingAssets/Help/mission" + GameData.Instance.mission_level + ".html"));
		text = text.Replace('/', '\\');
		SteamFriends.ActivateGameOverlayToWebPage(text);
	}

	public async void OpenHelp()
	{
		string filePath = ((GameData.Instance.game_type != 0) ? ("file://" + Application.dataPath + "/StreamingAssets/Help/help_main.html") : ("file://" + Application.dataPath + "/StreamingAssets/Help/mission" + GameData.Instance.mission_level + ".html"));
		webBrowserOpen = true;
		webView = Web.CreateWebView();
		int width = (int)(RefBriefingHelpTexture.Width * 2f);
		int height = (int)(RefBriefingHelpTexture.Height * 2f);
		await webView.Init(width, height);
		if (webBrowserOpen)
		{
			webView.LoadUrl(filePath);
			mouseIsUpStroke = false;
			mouseIsDownStroke = false;
			webBrowserLoaded = true;
			browserThumbHeld = false;
		}
		else
		{
			try
			{
				webView.Dispose();
			}
			catch (Exception)
			{
			}
			webView = null;
		}
	}

	public void CloseHelp()
	{
		if (webBrowserOpen)
		{
			try
			{
				if (webView.IsInitialized)
				{
					webView.Dispose();
					webView = null;
				}
			}
			catch (Exception)
			{
			}
			webBrowserOpen = false;
			webBrowserLoaded = false;
		}
		mouseIsUpStroke = false;
		mouseIsDownStroke = false;
		browserThumbHeld = false;
	}

	public void Update()
	{
		if (!webBrowserLoaded || RefHelpSubPanel.Visibility != Visibility.Visible)
		{
			return;
		}
		bool flag = FatControler.MouseIsDownStroke;
		bool flag2 = FatControler.MouseIsUpStroke;
		TextureSource briefingHelpImage = new TextureSource(webView.Texture);
		MainViewModel.Instance.BriefingHelpImage = briefingHelpImage;
		Point briefingHelpMousePoint = FatControler.instance.BriefingHelpMousePoint;
		if ((briefingHelpMousePoint.X >= 0f && briefingHelpMousePoint.X < RefBriefingHelpTexture.Width && briefingHelpMousePoint.Y >= 0f && briefingHelpMousePoint.Y < RefBriefingHelpTexture.Height) || browserThumbHeld)
		{
			Vector2 normalizedPoint = new Vector2(briefingHelpMousePoint.X / RefBriefingHelpTexture.Width, 1f - briefingHelpMousePoint.Y / RefBriefingHelpTexture.Height);
			if (normalizedPoint.x < 0f)
			{
				normalizedPoint.x = 0f;
			}
			if (normalizedPoint.y < 0f)
			{
				normalizedPoint.y = 0f;
			}
			if (normalizedPoint.x > 1f)
			{
				normalizedPoint.x = 1f;
			}
			if (normalizedPoint.y > 1f)
			{
				normalizedPoint.y = 1f;
			}
			if (webView is IWithPointerDownAndUp withPointerDownAndUp && !webView.IsDisposed && webView.IsInitialized)
			{
				if (flag)
				{
					browserThumbHeld = true;
					withPointerDownAndUp.PointerDown(normalizedPoint);
				}
				else if (flag2)
				{
					browserThumbHeld = false;
					withPointerDownAndUp.PointerUp(normalizedPoint);
				}
				else
				{
					(webView as IWithMovablePointer).MovePointer(normalizedPoint);
				}
			}
		}
		mouseIsUpStroke = false;
		mouseIsDownStroke = false;
	}

	public void MouseWheelScrolled(float delta)
	{
		if (webBrowserLoaded && webView != null && !webView.IsDisposed && webView.IsInitialized)
		{
			if (delta > 0f)
			{
				webView.Scroll(0, -60);
			}
			else
			{
				webView.Scroll(0, 60);
			}
		}
	}

	public bool canGoBack()
	{
		canGoBackInternal();
		return canGoBackValue;
	}

	private async void canGoBackInternal()
	{
		if (webBrowserLoaded && webView != null && !webView.IsDisposed && webView.IsInitialized)
		{
			canGoBackValue = await webView.CanGoBack();
		}
		else
		{
			canGoBackValue = false;
		}
	}

	public void goBack()
	{
		if (webBrowserLoaded && webView != null && !webView.IsDisposed && webView.IsInitialized)
		{
			webView.GoBack();
		}
	}
}
