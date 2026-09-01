using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class HUD_Tutorial : UserControl
{
	private int lastBodyTextSection = -1;

	private int lastBodyTextLine = -1;

	public HUD_Tutorial()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDTutorial = this;
	}

	public void StartTutorial()
	{
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		EngineInterface.LoadMapReturnData retData = EngineInterface.loadTutorial();
		if (retData.errorCode == 1)
		{
			AchievementsCommon.Instance.ResetOnMissionStart();
			EditorDirector.instance.postLoading(retData);
			OpenTutorial();
		}
	}

	public void OpenTutorial()
	{
		lastBodyTextSection = -1;
		lastBodyTextLine = -1;
		MainViewModel.Instance.TutorialButton1Visible = false;
		MainViewModel.Instance.TutorialButton2Visible = false;
		MainViewModel.Instance.Show_HUD_Tutorial = true;
	}

	public void setTutorialText(int headingTextSection, int headingTextLine, int bodyTextSection, int bodyTextLine, bool warning)
	{
		bool flag = false;
		bool flag2 = false;
		if (!ConfigSettings.Settings_PushMapScrolling && bodyTextSection == 95 && bodyTextLine == 6)
		{
			bodyTextSection = 233;
			bodyTextLine = 1;
			flag = true;
		}
		if (bodyTextSection == 95 && bodyTextLine == 62)
		{
			bodyTextSection = 233;
			bodyTextLine = 2;
		}
		if (!ConfigSettings.Settings_SH1MouseWheel && bodyTextSection == 95 && bodyTextLine == 99)
		{
			bodyTextSection = 233;
			bodyTextLine = 10;
		}
		if (bodyTextSection == 95 && bodyTextLine == 106)
		{
			bodyTextSection = 233;
			bodyTextLine = 11;
			flag2 = true;
		}
		MainViewModel.Instance.TutorialHeading = Translate.Instance.lookUpText((Enums.eTextSections)headingTextSection, headingTextLine);
		MainViewModel.Instance.TutorialBody = Translate.Instance.lookUpText((Enums.eTextSections)bodyTextSection, bodyTextLine);
		if (flag)
		{
			string tutorialBody = MainViewModel.Instance.TutorialBody;
			KeyCode keyCode = KeyManager.instance.GetKeyCode(Enums.KeyFunctions.Up, 0);
			KeyCode keyCode2 = KeyManager.instance.GetKeyCode(Enums.KeyFunctions.Down, 0);
			KeyCode keyCode3 = KeyManager.instance.GetKeyCode(Enums.KeyFunctions.Left, 0);
			KeyCode keyCode4 = KeyManager.instance.GetKeyCode(Enums.KeyFunctions.Right, 0);
			string newValue = "[ " + HUD_Options.GetKeyCodeString(keyCode) + ", " + HUD_Options.GetKeyCodeString(keyCode3) + ", " + HUD_Options.GetKeyCodeString(keyCode2) + ", " + HUD_Options.GetKeyCodeString(keyCode4) + " ]";
			MainViewModel.Instance.TutorialBody = tutorialBody.Replace("[KEY]", newValue);
		}
		if (flag2)
		{
			string tutorialBody2 = MainViewModel.Instance.TutorialBody;
			KeyCode keyCode5 = KeyManager.instance.GetKeyCode(Enums.KeyFunctions.FlattenLandscape, 0);
			string newValue2 = "[ " + HUD_Options.GetKeyCodeString(keyCode5) + " ]";
			MainViewModel.Instance.TutorialBody = tutorialBody2.Replace("[KEY]", newValue2);
		}
		if (!warning && (lastBodyTextSection != bodyTextSection || lastBodyTextLine != bodyTextLine))
		{
			lastBodyTextSection = bodyTextSection;
			lastBodyTextLine = bodyTextLine;
			if (bodyTextSection == 95 && bodyTextLine == 9)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(1, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 22)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(2, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 33)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(3, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 42)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(4, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 46)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(5, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 49)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(6, state: true);
			}
			else if ((bodyTextSection == 95 && bodyTextLine == 63) || (bodyTextSection == 95 && bodyTextLine == 64))
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(7, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 66)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(8, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 120)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(10, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 87)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(12, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 133)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(13, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 136)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(14, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 139)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(15, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 142)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(16, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 146)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(17, state: true);
			}
			else if (bodyTextSection == 95 && bodyTextLine == 149)
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(18, state: true);
			}
			else
			{
				MainViewModel.Instance.HUDmain.ShowTutorialArrow(-1, state: false);
			}
		}
	}

	public void showTutorialButton(int textSection, int textLine, int ID)
	{
		string text = Translate.Instance.lookUpText((Enums.eTextSections)textSection, textLine);
		if (ID == 1)
		{
			MainViewModel.Instance.TutorialButton1Text = text;
			MainViewModel.Instance.TutorialButton1Visible = true;
		}
		else
		{
			MainViewModel.Instance.TutorialButton2Text = text;
			MainViewModel.Instance.TutorialButton2Visible = true;
			AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Tutorial);
		}
		if (MainViewModel.Instance.HUDmain.currentTutorialArrow == 3 || MainViewModel.Instance.HUDmain.currentTutorialArrow == 4)
		{
			MainViewModel.Instance.HUDmain.ShowTutorialArrow(-1, state: false);
		}
	}

	public void clearTutorialButton()
	{
		MainViewModel.Instance.TutorialButton1Visible = false;
		MainViewModel.Instance.TutorialButton2Visible = false;
	}

	public void ButtonClicked(int button)
	{
		switch (button)
		{
		case 1:
			EngineInterface.TutorialAction(20);
			if (!MainViewModel.Instance.TutorialButton2Visible)
			{
				EditorDirector.instance.updateLeftMouseStateForEngine(0);
			}
			else
			{
				MainViewModel.Instance.Show_HUD_Tutorial = false;
			}
			break;
		case 2:
			EditorDirector.instance.stopGameSim();
			MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
			break;
		}
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Tutorial.xaml");
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
}
