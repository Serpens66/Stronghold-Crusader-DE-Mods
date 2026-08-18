using System;
using Noesis;

namespace CrusaderDE;

public class FRONT_Story : UserControl
{
	public Grid RefStoryGrid;

	public Grid RefCampaignGrid;

	public TextBlock RefStoryCampaignText;

	public TextBlock RefStoryMissionText;

	private Storyboard outtroAnimation;

	private Storyboard introAnimation;

	public static int CurrentMissionID;

	private bool showingCampaignIntro;

	private static bool CampaignAfterMath;

	private bool displayStoryText;

	private bool displayCampaignText;

	private DateTime displayTextNextLetter = DateTime.MinValue;

	private Enums.eTextSections[] storyText = new Enums.eTextSections[35]
	{
		Enums.eTextSections.TEXT_MISSION1_STORY,
		Enums.eTextSections.TEXT_MISSION2_STORY,
		Enums.eTextSections.TEXT_MISSION3_STORY,
		Enums.eTextSections.TEXT_MISSION4_STORY,
		Enums.eTextSections.TEXT_MISSION5_STORY,
		Enums.eTextSections.TEXT_MISSION6_STORY,
		Enums.eTextSections.TEXT_MISSION7_STORY,
		Enums.eTextSections.TEXT_MISSION8_STORY,
		Enums.eTextSections.TEXT_MISSION9_STORY,
		Enums.eTextSections.TEXT_MISSION10_STORY,
		Enums.eTextSections.TEXT_MISSION11_STORY,
		Enums.eTextSections.TEXT_MISSION12_STORY,
		Enums.eTextSections.TEXT_MISSION13_STORY,
		Enums.eTextSections.TEXT_MISSION14_STORY,
		Enums.eTextSections.TEXT_MISSION15_STORY,
		Enums.eTextSections.TEXT_MISSION16_STORY,
		Enums.eTextSections.TEXT_MISSION17_STORY,
		Enums.eTextSections.TEXT_MISSION18_STORY,
		Enums.eTextSections.TEXT_MISSION19_STORY,
		Enums.eTextSections.TEXT_MISSION20_STORY,
		Enums.eTextSections.TEXT_MISSION21_STORY,
		Enums.eTextSections.TEXT_MISSION22_STORY,
		Enums.eTextSections.TEXT_MISSION23_STORY,
		Enums.eTextSections.TEXT_MISSION24_STORY,
		Enums.eTextSections.TEXT_MISSION25_STORY,
		Enums.eTextSections.TEXT_MISSION26_STORY,
		Enums.eTextSections.TEXT_MISSION27_STORY,
		Enums.eTextSections.TEXT_MISSION28_STORY,
		Enums.eTextSections.TEXT_MISSION29_STORY,
		Enums.eTextSections.TEXT_MISSION30_STORY,
		Enums.eTextSections.TEXT_MISSION31_STORY,
		Enums.eTextSections.TEXT_MISSION32_STORY,
		Enums.eTextSections.TEXT_MISSION33_STORY,
		Enums.eTextSections.TEXT_MISSION34_STORY,
		Enums.eTextSections.TEXT_MISSION35_STORY
	};

	public FRONT_Story()
	{
		InitializeComponent();
		MainViewModel.Instance.FRONTStory = this;
		RefStoryGrid = (Grid)FindName("StoryGrid");
		RefCampaignGrid = (Grid)FindName("CampaignGrid");
		RefStoryCampaignText = (TextBlock)FindName("StoryCampaignText");
		RefStoryMissionText = (TextBlock)FindName("StoryMissionText");
		outtroAnimation = (Storyboard)TryFindResource("Outtro");
		outtroAnimation.Completed += delegate
		{
			MyAudioManager.Instance.StopSpeech(3);
			if (showingCampaignIntro)
			{
				if (CampaignAfterMath)
				{
					MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
					MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical");
				}
				else
				{
					showingCampaignIntro = false;
					ConfigureScreen(campaignScreen: false);
					introAnimation.Begin();
				}
			}
			else
			{
				StartMission();
			}
		};
		introAnimation = (Storyboard)TryFindResource("Intro");
		introAnimation.Completed += delegate
		{
		};
		if (FatControler.russian)
		{
			RefStoryCampaignText.LineHeight = 30f;
			RefStoryCampaignText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
			RefStoryMissionText.LineHeight = 30f;
			RefStoryMissionText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.czech || FatControler.hungarian || FatControler.french)
		{
			MainViewModel.Instance.StoryFontSize = 29;
		}
		if (FatControler.polish)
		{
			MainViewModel.Instance.StoryFontSize = 28;
		}
		if (FatControler.japanese)
		{
			MainViewModel.Instance.StoryFontSize = 28;
			RefStoryCampaignText.LineHeight = 35f;
			RefStoryCampaignText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
			RefStoryMissionText.LineHeight = 35f;
			RefStoryMissionText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_Story.xaml");
	}

	public static void OpenStory(int missionID, bool afterMath = false)
	{
		CampaignAfterMath = afterMath;
		CurrentMissionID = missionID;
		MainViewModel.Instance.FRONTStory.Open();
	}

	private void Open()
	{
		showingCampaignIntro = (CurrentMissionID - 1) % 5 == 0;
		if (CampaignAfterMath)
		{
			showingCampaignIntro = true;
		}
		int num = CurrentMissionID - 1;
		if (!CampaignAfterMath)
		{
			if (num > 19)
			{
				SFXManager.instance.playMusic(116 + num - 20, fadePrevious: false, 0.4f);
			}
			else
			{
				SFXManager.instance.playMusic(47 + num, fadePrevious: false, 0.4f);
			}
		}
		else
		{
			int num2 = (CurrentMissionID - 1) / 5;
			if (num2 >= 4)
			{
				num2 = 4;
			}
			SFXManager.instance.playMusic(67 + num2, fadePrevious: false, 0.4f);
		}
		ConfigureScreen(showingCampaignIntro, CampaignAfterMath);
		introAnimation.Begin();
		MainViewModel.Instance.InitNewScene(Enums.SceneIDS.Story);
	}

	private void ConfigureScreen(bool campaignScreen, bool afterMath = false)
	{
		displayStoryText = false;
		displayCampaignText = false;
		MainViewModel.Instance.Story_StoryBody2 = "";
		MainViewModel.Instance.Story_CampaignBody2 = "";
		for (int i = 0; i < 35; i++)
		{
			MainViewModel.Instance.StoryBook[i] = Visibility.Hidden;
		}
		for (int j = 0; j < 14; j++)
		{
			MainViewModel.Instance.CampaignBook[j] = Visibility.Hidden;
		}
		if (FatControler.german)
		{
			MainViewModel.Instance.StoryFontSize = 30;
			RefStoryCampaignText.LineHeight = 35f;
			RefStoryCampaignText.LineStackingStrategy = LineStackingStrategy.MaxHeight;
			RefStoryMissionText.LineHeight = 35f;
			RefStoryMissionText.LineStackingStrategy = LineStackingStrategy.MaxHeight;
		}
		if (campaignScreen || afterMath)
		{
			RefCampaignGrid.Visibility = Visibility.Visible;
			RefStoryGrid.Visibility = Visibility.Hidden;
			int num = (CurrentMissionID - 1) / 5;
			if (!afterMath)
			{
				MainViewModel.Instance.CampaignBook[num] = Visibility.Visible;
				switch (num)
				{
				case 0:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\Intro_01.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 1:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\Intro_02.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 2:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\Intro_03.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 3:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\Intro_04.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 4:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\Intro_05.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 5:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\Intro_06.wav", 1f, ignoreSpeechMuting: true);
					if (FatControler.german)
					{
						MainViewModel.Instance.StoryFontSize = 27;
						RefStoryCampaignText.LineHeight = 32f;
						RefStoryCampaignText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
						RefStoryMissionText.LineHeight = 32f;
						RefStoryMissionText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
					}
					break;
				case 6:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\Intro_07.wav", 1f, ignoreSpeechMuting: true);
					break;
				}
			}
			else
			{
				MainViewModel.Instance.CampaignBook[num + 7] = Visibility.Visible;
				switch (num)
				{
				case 0:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\After_01.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 1:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\After_02.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 2:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\After_03.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 3:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\After_04.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 4:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\After_05.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 5:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\After_06.wav", 1f, ignoreSpeechMuting: true);
					break;
				case 6:
					SFXManager.instance.delayPlaySpeech(3, "Campaign\\After_07.wav", 1f, ignoreSpeechMuting: true);
					break;
				}
			}
			int num2 = 0;
			if (afterMath)
			{
				num2 = 3;
			}
			MainViewModel.Instance.Story_CampaignTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CAMPAIGN_INFO, 1 + num2 + num * 6);
			MainViewModel.Instance.Story_CampaignBody = "";
			MainViewModel.Instance.Story_CampaignBody2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CAMPAIGN_INFO, 2 + num2 + num * 6);
			displayTextNextLetter = DateTime.UtcNow;
			displayCampaignText = true;
			return;
		}
		RefCampaignGrid.Visibility = Visibility.Hidden;
		RefStoryGrid.Visibility = Visibility.Visible;
		MainViewModel.Instance.StoryBook[CurrentMissionID - 1] = Visibility.Visible;
		MainViewModel.Instance.Story_StoryNumber = Translate.Instance.lookUpText(storyText[CurrentMissionID - 1], 0);
		MainViewModel.Instance.Story_StoryTitle = Translate.Instance.lookUpText(storyText[CurrentMissionID - 1], 1);
		MainViewModel.Instance.Story_StoryBody = "";
		MainViewModel.Instance.Story_StoryBody2 = Translate.Instance.lookUpText(storyText[CurrentMissionID - 1], 2);
		displayTextNextLetter = DateTime.UtcNow;
		displayStoryText = true;
		switch (CurrentMissionID)
		{
		case 1:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M1_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 2:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M2_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 3:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M3_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 4:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M4_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 5:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M5_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 6:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M6_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 7:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M7_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 8:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M8_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 9:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M9_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 10:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M10_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 11:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M11_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 12:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M12_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 13:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M13_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 14:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M14_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 15:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M15_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 16:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M16_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 17:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M17_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 18:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M18_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 19:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M19_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 20:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M20_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 21:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M21_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 22:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M22_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 23:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M23_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 24:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M24_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 25:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M25_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 26:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M26_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 27:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M27_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 28:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M28_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 29:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M29_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 30:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M30_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 31:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M31_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 32:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M32_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 33:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M33_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 34:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M34_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		case 35:
			SFXManager.instance.delayPlaySpeech(3, "Campaign\\M35_Hist.wav", 1f, ignoreSpeechMuting: true);
			break;
		}
	}

	public void ClickStoryAdvance()
	{
		if (displayStoryText && MainViewModel.Instance.Story_StoryBody2.Length > 0)
		{
			MainViewModel.Instance.Story_StoryBody += MainViewModel.Instance.Story_StoryBody2;
			MainViewModel.Instance.Story_StoryBody2 = "";
			displayStoryText = false;
		}
		else if (displayCampaignText && MainViewModel.Instance.Story_CampaignBody2.Length > 0)
		{
			MainViewModel.Instance.Story_CampaignBody += MainViewModel.Instance.Story_CampaignBody2;
			MainViewModel.Instance.Story_CampaignBody2 = "";
			displayCampaignText = false;
		}
		else if (!outtroAnimation.IsPlaying())
		{
			outtroAnimation.Begin();
		}
	}

	public void EscapePressed()
	{
		MyAudioManager.Instance.StopSpeech(3);
		if (CampaignAfterMath)
		{
			MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical");
		}
		else
		{
			StartMission();
		}
	}

	public void StartMission()
	{
		MainViewModel.Instance.StartCampaignMission(CurrentMissionID);
	}

	public void Update()
	{
		if (displayStoryText && DateTime.UtcNow > displayTextNextLetter)
		{
			displayTextNextLetter = DateTime.UtcNow.AddMilliseconds(50.0);
			MainViewModel.Instance.Story_StoryBody += MainViewModel.Instance.Story_StoryBody2[0];
			if (MainViewModel.Instance.Story_StoryBody2.Length <= 1)
			{
				MainViewModel.Instance.Story_StoryBody2 = "";
				displayStoryText = false;
			}
			else
			{
				MainViewModel.Instance.Story_StoryBody2 = MainViewModel.Instance.Story_StoryBody2.Substring(1);
			}
		}
		if (displayCampaignText && DateTime.UtcNow > displayTextNextLetter)
		{
			displayTextNextLetter = DateTime.UtcNow.AddMilliseconds(50.0);
			MainViewModel.Instance.Story_CampaignBody += MainViewModel.Instance.Story_CampaignBody2[0];
			if (MainViewModel.Instance.Story_CampaignBody2.Length <= 1)
			{
				MainViewModel.Instance.Story_CampaignBody2 = "";
				displayCampaignText = false;
			}
			else
			{
				MainViewModel.Instance.Story_CampaignBody2 = MainViewModel.Instance.Story_CampaignBody2.Substring(1);
			}
		}
	}
}
