using System;
using Noesis;
using NoesisApp;
using UnityEngine;

namespace CrusaderDE;

public class IntroSequence : UserControl
{
	public static bool forceSkipIntro;

	public Image refLogo;

	public Image refLogo2;

	public Image refPartnerLogo;

	public Image refLogoMain;

	public Grid refProgress;

	public Border refProgressBorder;

	public Image refProgressImage;

	public TextBlock refPresents;

	public MediaElement RefIntroVideo;

	public Storyboard RefFadeInLogos;

	public TextBox RefEnterYourNameTB;

	public int stage = -2;

	public bool binkLoaded;

	public bool optinWarningShown;

	public Button RefCrusaderLordButton;

	public Button RefArabicLordButton;

	public Button RefBedouinLordButton;

	public Button RefScribeLordButton;

	public Button RefFemaleLordButton;

	public Button RefArabicLordFemaleButton;

	public Button RefBedouinLordFemaleButton;

	public CheckBox RefLeaderboard_OptOut;

	public CheckBox RefNewsletterCheck;

	public Button RefNewsletterSignupButton;

	public TextBox RefTextBoxNewsletter;

	public Image RefScribeLock;

	public bool panelActive;

	public bool signedUp;

	public bool InOptInChange;

	public static int loadingProgress;

	public IntroSequence()
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Expected O, but got Unknown
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Expected O, but got Unknown
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected O, but got Unknown
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Expected O, but got Unknown
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
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Expected O, but got Unknown
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Expected O, but got Unknown
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Expected O, but got Unknown
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Expected O, but got Unknown
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Expected O, but got Unknown
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Expected O, but got Unknown
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Expected O, but got Unknown
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Expected O, but got Unknown
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Expected O, but got Unknown
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Expected O, but got Unknown
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Expected O, but got Unknown
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Expected O, but got Unknown
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Expected O, but got Unknown
		//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Expected O, but got Unknown
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Expected O, but got Unknown
		//IL_0301: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Expected O, but got Unknown
		//IL_0318: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Expected O, but got Unknown
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Expected O, but got Unknown
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_034f: Expected O, but got Unknown
		((FrameworkElement)this).DataContext = MainViewModel.Instance;
		MainViewModel.Instance.Intro_Sequence = this;
		MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
		InitializeComponent();
		refLogo = (Image)((FrameworkElement)this).FindName("Logo");
		refLogo2 = (Image)((FrameworkElement)this).FindName("Logo2");
		refPartnerLogo = (Image)((FrameworkElement)this).FindName("PartnerLogo");
		refLogoMain = (Image)((FrameworkElement)this).FindName("LogoMain");
		refLogoMain.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_FRONTEND_LOGO);
		refProgress = (Grid)((FrameworkElement)this).FindName("Progress");
		refProgressBorder = (Border)((FrameworkElement)this).FindName("ProgressBorder");
		refProgressImage = (Image)((FrameworkElement)this).FindName("ProgressImage");
		MainViewModel.Instance.EnterYourName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 48);
		refPresents = (TextBlock)((FrameworkElement)this).FindName("Presents");
		RefFadeInLogos = (Storyboard)((FrameworkElement)this).TryFindResource((object)"FadeInLogos");
		((Timeline)RefFadeInLogos).Completed += (CompletedHandler)delegate
		{
			StartVideo();
		};
		RefCrusaderLordButton = (Button)((FrameworkElement)this).FindName("CrusaderLordButton");
		RefArabicLordButton = (Button)((FrameworkElement)this).FindName("ArabicLordButton");
		RefBedouinLordButton = (Button)((FrameworkElement)this).FindName("BedouinLordButton");
		RefScribeLordButton = (Button)((FrameworkElement)this).FindName("ScribeLordButton");
		RefFemaleLordButton = (Button)((FrameworkElement)this).FindName("FemaleLordButton");
		RefArabicLordFemaleButton = (Button)((FrameworkElement)this).FindName("ArabicLordFemaleButton");
		RefBedouinLordFemaleButton = (Button)((FrameworkElement)this).FindName("BedouinLordFemaleButton");
		RefIntroVideo = (MediaElement)((FrameworkElement)this).FindName("IntroVideo");
		RefIntroVideo.MediaEnded += new RoutedEventHandler(IntroVideo_Ended);
		RefIntroVideo.MediaOpened += new RoutedEventHandler(IntroVideo_Opened);
		((UIElement)RefIntroVideo).Visibility = (Visibility)1;
		RefEnterYourNameTB = (TextBox)((FrameworkElement)this).FindName("EnterYourNameTB");
		((UIElement)RefEnterYourNameTB).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		RefLeaderboard_OptOut = (CheckBox)((FrameworkElement)this).FindName("Leaderboard_OptOut");
		((ToggleButton)RefLeaderboard_OptOut).Checked += new RoutedEventHandler(Leaderboard_OptOut_ValueChanged);
		((ToggleButton)RefLeaderboard_OptOut).Unchecked += new RoutedEventHandler(Leaderboard_OptOut_ValueChanged);
		RefNewsletterSignupButton = (Button)((FrameworkElement)this).FindName("NewsletterSignupButton");
		RefNewsletterCheck = (CheckBox)((FrameworkElement)this).FindName("NewsletterCheck");
		((ToggleButton)RefNewsletterCheck).Checked += new RoutedEventHandler(NewsletterCheck_ValueChanged);
		((ToggleButton)RefNewsletterCheck).Unchecked += new RoutedEventHandler(NewsletterCheck_ValueChanged);
		RefTextBoxNewsletter = (TextBox)((FrameworkElement)this).FindName("TextBoxNewsletter");
		((UIElement)RefTextBoxNewsletter).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefTextBoxNewsletter).TextChanged += new RoutedEventHandler(NewsletterValueChanged);
		RefScribeLock = (Image)((FrameworkElement)this).FindName("ScribeLock");
		MainViewModel.Instance.EnterYourNameVis = false;
	}

	public void InitVideos()
	{
		if (!forceSkipIntro)
		{
			if (Screen.width <= 1920 && Screen.height <= 1080)
			{
				RefIntroVideo.Source = new Uri("Assets/GUI/Video/intro-low.webm", UriKind.Relative);
			}
			else
			{
				RefIntroVideo.Source = new Uri("Assets/GUI/Video/intro.webm", UriKind.Relative);
			}
		}
	}

	public void Init()
	{
		MainViewModel.Instance.Show_IntroSequence = true;
		panelActive = true;
	}

	public void StartVideo()
	{
		RefFadeInLogos.Stop();
		((UIElement)refLogo).Visibility = (Visibility)1;
		((UIElement)refProgress).Visibility = (Visibility)1;
		((UIElement)refProgressBorder).Visibility = (Visibility)1;
		((UIElement)refProgressImage).Visibility = (Visibility)1;
		((UIElement)refPresents).Visibility = (Visibility)1;
		((UIElement)refPartnerLogo).Visibility = (Visibility)1;
		stage++;
		((UIElement)RefIntroVideo).Visibility = (Visibility)2;
		RefIntroVideo.Volume = ConfigSettings.Settings_SpeechVolume * MyAudioManager.GetMasterVolume();
		RefIntroVideo.IsMuted = false;
		RefIntroVideo.Play();
	}

	public void EndVideo()
	{
		RefIntroVideo.Stop();
		RefIntroVideo.Source = null;
		RefIntroVideo.Close();
		if (ConfigSettings.SettingsFileExisted)
		{
			Avatars.Instance.CreateLocalUserAvatar();
			FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
			return;
		}
		ConfigSettings.Settings_LordType = 0;
		HUD_CoatOfArms.Init(ConfigSettings.getAvatar(), intro: true);
		((UIElement)RefNewsletterSignupButton).IsEnabled = false;
		if (FrontendMenus.newsletterSignUp)
		{
			((UIElement)RefScribeLock).Visibility = (Visibility)1;
		}
		else
		{
			((UIElement)RefScribeLock).Visibility = (Visibility)2;
		}
		UpdateLords();
		EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
		MainViewModel.Instance.EnterYourNameVis = true;
		((UIElement)RefEnterYourNameTB).Focus();
		RefEnterYourNameTB.CaretIndex = 50;
	}

	public void ForceStopVideo()
	{
		try
		{
			RefIntroVideo.Stop();
		}
		catch (Exception)
		{
		}
		RefIntroVideo.Source = null;
		try
		{
			RefIntroVideo.Close();
		}
		catch (Exception)
		{
		}
	}

	public void ButtonClicked(bool fromClick)
	{
		if (fromClick)
		{
			if (binkLoaded)
			{
				if (stage == 0)
				{
					StartVideo();
				}
				else if (stage == 1)
				{
					EndVideo();
				}
			}
		}
		else
		{
			RefFadeInLogos.Stop();
			EndVideo();
		}
	}

	public void EnterYourNameClicked(string param)
	{
		switch (param)
		{
		case "OK":
			ConfigSettings.Settings_UserName = RefEnterYourNameTB.Text;
			Avatars.Instance.CreateLocalUserAvatar();
			FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
			break;
		case "OFF":
			MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(ConfigSettings.Settings_LordType);
			break;
		case "CRU_OVER":
			MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(0);
			break;
		case "CRU":
			ConfigSettings.Settings_LordType = 0;
			UpdateLords();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case "ARAB_OVER":
			MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(1);
			break;
		case "ARAB":
			ConfigSettings.Settings_LordType = 1;
			UpdateLords();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case "BED_OVER":
			MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(2);
			break;
		case "BED":
			ConfigSettings.Settings_LordType = 2;
			UpdateLords();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case "WQ_OVER":
			MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(4);
			break;
		case "WQ":
			ConfigSettings.Settings_LordType = 4;
			UpdateLords();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case "SCR_OVER":
			MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(3);
			break;
		case "SCR":
			if (!signedUp)
			{
				MainViewModel.Instance.OptionsNewsletterVis = (Visibility)2;
				break;
			}
			ConfigSettings.Settings_LordType = 3;
			UpdateLords();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case "ARAB_OVERF":
			MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(6);
			break;
		case "ARABF":
			ConfigSettings.Settings_LordType = 6;
			UpdateLords();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case "BED_OVERF":
			MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(7);
			break;
		case "BEDF":
			ConfigSettings.Settings_LordType = 7;
			UpdateLords();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case "NEWS":
			Director.instance.SignupNewsletter(RefTextBoxNewsletter.Text, delegate
			{
				signedUp = true;
				FrontendMenus.newsletterSignUp = true;
				((UIElement)RefScribeLock).Visibility = (Visibility)1;
				EnterYourNameClicked("SCR");
			});
			MainViewModel.Instance.OptionsNewsletterVis = (Visibility)1;
			break;
		case "NEWSCANCEL":
			MainViewModel.Instance.OptionsNewsletterVis = (Visibility)1;
			break;
		}
	}

	public void UpdateLords()
	{
		MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(ConfigSettings.Settings_LordType);
		if (ConfigSettings.Settings_LordType == 0)
		{
			PropEx.SetSprite1((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite2((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite3((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite4((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[396]);
			PropEx.SetSprite2((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite3((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite4((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[396]);
		}
		if (ConfigSettings.Settings_LordType == 1)
		{
			PropEx.SetSprite1((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite2((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite3((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite4((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[398]);
			PropEx.SetSprite2((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite3((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite4((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[398]);
		}
		if (ConfigSettings.Settings_LordType == 2)
		{
			PropEx.SetSprite1((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite2((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite3((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite4((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[659]);
			PropEx.SetSprite2((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite3((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite4((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[659]);
		}
		if (ConfigSettings.Settings_LordType == 3)
		{
			PropEx.SetSprite1((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite2((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite3((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite4((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[661]);
			PropEx.SetSprite2((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite3((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite4((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[661]);
		}
		if (ConfigSettings.Settings_LordType == 4)
		{
			PropEx.SetSprite1((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite2((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite3((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite4((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[663]);
			PropEx.SetSprite2((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite3((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite4((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[663]);
		}
		if (ConfigSettings.Settings_LordType == 6)
		{
			PropEx.SetSprite1((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite2((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite3((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite4((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[709]);
			PropEx.SetSprite2((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite3((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite4((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[709]);
		}
		if (ConfigSettings.Settings_LordType == 7)
		{
			PropEx.SetSprite1((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite2((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite3((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite4((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[711]);
			PropEx.SetSprite2((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite3((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite4((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[711]);
		}
	}

	public void Leaderboard_OptOut_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Leaderboard_OptOut = !((ToggleButton)RefLeaderboard_OptOut).IsChecked.Value;
			MainViewModel.Instance.Show_LeaderboardOptIn = !ConfigSettings.Settings_HideSoTTiming;
			ConfigSettings.SaveSettings();
		}
	}

	public void NewsletterValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			((UIElement)RefNewsletterSignupButton).IsEnabled = HUD_Options.IsValidEmail(RefTextBoxNewsletter.Text) && ((ToggleButton)RefNewsletterCheck).IsChecked.Value;
		}
	}

	public void NewsletterCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			((UIElement)RefNewsletterSignupButton).IsEnabled = HUD_Options.IsValidEmail(RefTextBoxNewsletter.Text) && ((ToggleButton)RefNewsletterCheck).IsChecked.Value;
		}
	}

	public void Update()
	{
		if (stage == -2)
		{
			CustomisationFileManager.Instance.BuildFileLists();
			SFXManager.instance.init2();
			Avatars.InitAvatars();
			spriteLoader.instance.loadSprites();
			stage++;
		}
		else if (stage == -1)
		{
			if (spriteLoader.instance.spritesLoaded)
			{
				((UIElement)refProgress).Visibility = (Visibility)1;
				((UIElement)refProgressBorder).Visibility = (Visibility)1;
				((UIElement)refProgressImage).Visibility = (Visibility)1;
				RefFadeInLogos.Begin();
				stage++;
				if (ConfigSettings.Settings_SkipIntro || forceSkipIntro)
				{
					EndVideo();
				}
			}
		}
		else
		{
			_ = stage;
		}
		if (loadingProgress > 15)
		{
			loadingProgress = 15;
		}
		((FrameworkElement)refProgress).Width = 490 - loadingProgress * 490 / 15;
	}

	public void IntroVideo_Opened(object sender, RoutedEventArgs args)
	{
		RefIntroVideo.Pause();
		binkLoaded = true;
	}

	public void IntroVideo_Ended(object sender, RoutedEventArgs args)
	{
		EndVideo();
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAML/IntroSequence.xaml");
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

	public void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}
}
