using System;
using Noesis;
using Steamworks;
using UnityEngine;
using Vuplex.WebView;

namespace CrusaderDE;

public class HUD_Briefing : UserControl
{
	public Grid RefObjectivesSubPanel;

	public Grid RefHintsSubPanel;

	public Grid RefHelpSubPanel;

	public WGT_Objective[] RefWGTObjectives = new WGT_Objective[20];

	public Grid RefObjectiveTimer;

	public Button RefBriefingDifficultyButton;

	public Button RefBriefingQuitButton;

	public Button[] RefBriefingHintButtons = (Button[])(object)new Button[5];

	public TextBlock[] RefBriefingHintTexts = (TextBlock[])(object)new TextBlock[5];

	public RadioButton RefBriefingObjectivesButton;

	public RadioButton RefBriefingHintsButton;

	public RadioButton RefBriefingTutorialButton;

	public Grid RefBriefingStrategySection;

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

	public IWebView webView;

	public bool webBrowserOpen;

	public bool webBrowserLoaded;

	public bool browserThumbHeld;

	public static bool mouseIsUpStroke = false;

	public static bool mouseIsDownStroke = false;

	public static float ViewportScale = 1f;

	public bool canGoBackValue;

	public HUD_Briefing()
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Expected O, but got Unknown
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Expected O, but got Unknown
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Expected O, but got Unknown
		//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Expected O, but got Unknown
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e1: Expected O, but got Unknown
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Expected O, but got Unknown
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Expected O, but got Unknown
		//IL_0323: Unknown result type (might be due to invalid IL or missing references)
		//IL_0329: Expected O, but got Unknown
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Expected O, but got Unknown
		//IL_0353: Unknown result type (might be due to invalid IL or missing references)
		//IL_0359: Expected O, but got Unknown
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		//IL_036f: Expected O, but got Unknown
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Expected O, but got Unknown
		//IL_0391: Unknown result type (might be due to invalid IL or missing references)
		//IL_039b: Expected O, but got Unknown
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b1: Expected O, but got Unknown
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Expected O, but got Unknown
		//IL_03d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Expected O, but got Unknown
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f3: Expected O, but got Unknown
		//IL_03ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0409: Expected O, but got Unknown
		//IL_0415: Unknown result type (might be due to invalid IL or missing references)
		//IL_041f: Expected O, but got Unknown
		//IL_053d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0547: Expected O, but got Unknown
		//IL_0553: Unknown result type (might be due to invalid IL or missing references)
		//IL_055d: Expected O, but got Unknown
		//IL_0569: Unknown result type (might be due to invalid IL or missing references)
		//IL_0573: Expected O, but got Unknown
		//IL_057f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0589: Expected O, but got Unknown
		//IL_0595: Unknown result type (might be due to invalid IL or missing references)
		//IL_059f: Expected O, but got Unknown
		//IL_05ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b5: Expected O, but got Unknown
		//IL_05c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05cb: Expected O, but got Unknown
		//IL_05d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e1: Expected O, but got Unknown
		//IL_05ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f7: Expected O, but got Unknown
		//IL_0603: Unknown result type (might be due to invalid IL or missing references)
		//IL_060d: Expected O, but got Unknown
		//IL_0619: Unknown result type (might be due to invalid IL or missing references)
		//IL_0623: Expected O, but got Unknown
		//IL_062f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0639: Expected O, but got Unknown
		//IL_0645: Unknown result type (might be due to invalid IL or missing references)
		//IL_064f: Expected O, but got Unknown
		//IL_065b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0665: Expected O, but got Unknown
		//IL_0671: Unknown result type (might be due to invalid IL or missing references)
		//IL_067b: Expected O, but got Unknown
		//IL_0687: Unknown result type (might be due to invalid IL or missing references)
		//IL_0691: Expected O, but got Unknown
		//IL_069d: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a7: Expected O, but got Unknown
		//IL_06b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_06bd: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDBriefingPanel = this;
		RefObjectivesSubPanel = (Grid)((FrameworkElement)this).FindName("BriefingObjectivesPanel");
		RefHintsSubPanel = (Grid)((FrameworkElement)this).FindName("BriefingHintsPanel");
		RefHelpSubPanel = (Grid)((FrameworkElement)this).FindName("BriefingHelpPanel");
		RefWGTObjectives[0] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective1");
		RefWGTObjectives[1] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective2");
		RefWGTObjectives[2] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective3");
		RefWGTObjectives[3] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective4");
		RefWGTObjectives[4] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective5");
		RefWGTObjectives[5] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective6");
		RefWGTObjectives[6] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective7");
		RefWGTObjectives[7] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective8");
		RefWGTObjectives[8] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective9");
		RefWGTObjectives[9] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective10");
		RefWGTObjectives[10] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective11");
		RefWGTObjectives[11] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective12");
		RefWGTObjectives[12] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective13");
		RefWGTObjectives[13] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective14");
		RefWGTObjectives[14] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective15");
		RefWGTObjectives[15] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective16");
		RefWGTObjectives[16] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective17");
		RefWGTObjectives[17] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective18");
		RefWGTObjectives[18] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective19");
		RefWGTObjectives[19] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective20");
		RefBriefingHintButtons[0] = (Button)((FrameworkElement)this).FindName("BriefingHint1Button");
		RefBriefingHintButtons[1] = (Button)((FrameworkElement)this).FindName("BriefingHint2Button");
		RefBriefingHintButtons[2] = (Button)((FrameworkElement)this).FindName("BriefingHint3Button");
		RefBriefingHintButtons[3] = (Button)((FrameworkElement)this).FindName("BriefingHint4Button");
		RefBriefingHintButtons[4] = (Button)((FrameworkElement)this).FindName("BriefingHint5Button");
		RefBriefingHintTexts[0] = (TextBlock)((FrameworkElement)this).FindName("BriefingHint1Text");
		RefBriefingHintTexts[1] = (TextBlock)((FrameworkElement)this).FindName("BriefingHint2Text");
		RefBriefingHintTexts[2] = (TextBlock)((FrameworkElement)this).FindName("BriefingHint3Text");
		RefBriefingHintTexts[3] = (TextBlock)((FrameworkElement)this).FindName("BriefingHint4Text");
		RefBriefingHintTexts[4] = (TextBlock)((FrameworkElement)this).FindName("BriefingHint5Text");
		RefObjectiveTimer = (Grid)((FrameworkElement)this).FindName("ObjectiveTimer");
		RefBriefingDifficultyButton = (Button)((FrameworkElement)this).FindName("ButtonBriefingDifficulyLevel");
		RefBriefingQuitButton = (Button)((FrameworkElement)this).FindName("BriefingQuitButton");
		RefBriefingObjectivesButton = (RadioButton)((FrameworkElement)this).FindName("BriefingButton");
		RefBriefingHintsButton = (RadioButton)((FrameworkElement)this).FindName("HintsButton");
		RefBriefingTutorialButton = (RadioButton)((FrameworkElement)this).FindName("HelpButton");
		RefBriefingHelpBackButton = (Button)((FrameworkElement)this).FindName("HelpBackButton");
		RefBriefingHelpTexture = (Image)((FrameworkElement)this).FindName("BriefingHelpTexture");
		RefHintsTitleStamp = (Image)((FrameworkElement)this).FindName("HintsTitleStamp");
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
		RefBriefingStrategySection = (Grid)((FrameworkElement)this).FindName("BriefingStrategySection");
		RefStrategyTextBlock = (TextBlock)((FrameworkElement)this).FindName("StrategyTextBlock");
		RefRadarShield1 = (Image)((FrameworkElement)this).FindName("RadarShield1");
		RefRadarShield2 = (Image)((FrameworkElement)this).FindName("RadarShield2");
		RefRadarShield3 = (Image)((FrameworkElement)this).FindName("RadarShield3");
		RefRadarShield4 = (Image)((FrameworkElement)this).FindName("RadarShield4");
		RefRadarShield5 = (Image)((FrameworkElement)this).FindName("RadarShield5");
		RefRadarShield6 = (Image)((FrameworkElement)this).FindName("RadarShield6");
		RefRadarShield7 = (Image)((FrameworkElement)this).FindName("RadarShield7");
		RefRadarShield8 = (Image)((FrameworkElement)this).FindName("RadarShield8");
		RefRadarShieldTeam1 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam1");
		RefRadarShieldTeam2 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam2");
		RefRadarShieldTeam3 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam3");
		RefRadarShieldTeam4 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam4");
		RefRadarShieldTeam5 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam5");
		RefRadarShieldTeam6 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam6");
		RefRadarShieldTeam7 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam7");
		RefRadarShieldTeam8 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam8");
		if (FatControler.thai)
		{
			RefStrategyTextBlock.FontSize = 14f;
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Briefing.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
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
		SteamFriends.ActivateGameOverlayToWebPage(text, (EActivateGameOverlayToWebPageMode)0);
	}

	public async void OpenHelp()
	{
		string filePath = ((GameData.Instance.game_type != 0) ? ("file://" + Application.dataPath + "/StreamingAssets/Help/help_main.html") : ("file://" + Application.dataPath + "/StreamingAssets/Help/mission" + GameData.Instance.mission_level + ".html"));
		webBrowserOpen = true;
		webView = Web.CreateWebView();
		int num = (int)(((FrameworkElement)RefBriefingHelpTexture).Width * 2f);
		int num2 = (int)(((FrameworkElement)RefBriefingHelpTexture).Height * 2f);
		await webView.Init(num, num2);
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
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		if (!webBrowserLoaded || (int)((UIElement)RefHelpSubPanel).Visibility != 2)
		{
			return;
		}
		bool flag = FatControler.MouseIsDownStroke;
		bool flag2 = FatControler.MouseIsUpStroke;
		TextureSource briefingHelpImage = new TextureSource(webView.Texture);
		MainViewModel.Instance.BriefingHelpImage = (ImageSource)(object)briefingHelpImage;
		Point briefingHelpMousePoint = FatControler.instance.BriefingHelpMousePoint;
		if ((((Point)(ref briefingHelpMousePoint)).X >= 0f && ((Point)(ref briefingHelpMousePoint)).X < ((FrameworkElement)RefBriefingHelpTexture).Width && ((Point)(ref briefingHelpMousePoint)).Y >= 0f && ((Point)(ref briefingHelpMousePoint)).Y < ((FrameworkElement)RefBriefingHelpTexture).Height) || browserThumbHeld)
		{
			Vector2 val = default(Vector2);
			((Vector2)(ref val))._002Ector(((Point)(ref briefingHelpMousePoint)).X / ((FrameworkElement)RefBriefingHelpTexture).Width, 1f - ((Point)(ref briefingHelpMousePoint)).Y / ((FrameworkElement)RefBriefingHelpTexture).Height);
			if (val.x < 0f)
			{
				val.x = 0f;
			}
			if (val.y < 0f)
			{
				val.y = 0f;
			}
			if (val.x > 1f)
			{
				val.x = 1f;
			}
			if (val.y > 1f)
			{
				val.y = 1f;
			}
			IWebView obj = webView;
			IWithPointerDownAndUp val2 = (IWithPointerDownAndUp)(object)((obj is IWithPointerDownAndUp) ? obj : null);
			if (val2 != null && !webView.IsDisposed && webView.IsInitialized)
			{
				if (flag)
				{
					browserThumbHeld = true;
					val2.PointerDown(val);
				}
				else if (flag2)
				{
					browserThumbHeld = false;
					val2.PointerUp(val);
				}
				else
				{
					IWebView obj2 = webView;
					((IWithMovablePointer)((obj2 is IWithMovablePointer) ? obj2 : null)).MovePointer(val, false);
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

	public async void canGoBackInternal()
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
