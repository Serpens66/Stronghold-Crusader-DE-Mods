using System;
using Noesis;
using NoesisApp;
using UnityEngine;

namespace CrusaderDE;

public class IntroSequence : UserControl
{
	public static bool forceSkipIntro;

	private Image refLogo;

	private Image refLogo2;

	private Image refPartnerLogo;

	private Image refLogoMain;

	private Noesis.Grid refProgress;

	private Border refProgressBorder;

	private Image refProgressImage;

	private TextBlock refPresents;

	private MediaElement RefIntroVideo;

	private Storyboard RefFadeInLogos;

	private TextBox RefEnterYourNameTB;

	private int stage = -2;

	private bool binkLoaded;

	private bool optinWarningShown;

	private Button RefCrusaderLordButton;

	private Button RefArabicLordButton;

	private Button RefBedouinLordButton;

	private Button RefScribeLordButton;

	private Button RefFemaleLordButton;

	private Button RefArabicLordFemaleButton;

	private Button RefBedouinLordFemaleButton;

	private CheckBox RefLeaderboard_OptOut;

	private CheckBox RefNewsletterCheck;

	private Button RefNewsletterSignupButton;

	private TextBox RefTextBoxNewsletter;

	private Image RefScribeLock;

	private bool panelActive;

	private bool signedUp;

	private bool InOptInChange;

	public static int loadingProgress;

	public IntroSequence()
	{
		base.DataContext = MainViewModel.Instance;
		MainViewModel.Instance.Intro_Sequence = this;
		MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
		InitializeComponent();
		refLogo = (Image)FindName("Logo");
		refLogo2 = (Image)FindName("Logo2");
		refPartnerLogo = (Image)FindName("PartnerLogo");
		refLogoMain = (Image)FindName("LogoMain");
		refLogoMain.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_FRONTEND_LOGO);
		refProgress = (Noesis.Grid)FindName("Progress");
		refProgressBorder = (Border)FindName("ProgressBorder");
		refProgressImage = (Image)FindName("ProgressImage");
		MainViewModel.Instance.EnterYourName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 48);
		refPresents = (TextBlock)FindName("Presents");
		RefFadeInLogos = (Storyboard)TryFindResource("FadeInLogos");
		RefFadeInLogos.Completed += delegate
		{
			StartVideo();
		};
		RefCrusaderLordButton = (Button)FindName("CrusaderLordButton");
		RefArabicLordButton = (Button)FindName("ArabicLordButton");
		RefBedouinLordButton = (Button)FindName("BedouinLordButton");
		RefScribeLordButton = (Button)FindName("ScribeLordButton");
		RefFemaleLordButton = (Button)FindName("FemaleLordButton");
		RefArabicLordFemaleButton = (Button)FindName("ArabicLordFemaleButton");
		RefBedouinLordFemaleButton = (Button)FindName("BedouinLordFemaleButton");
		RefIntroVideo = (MediaElement)FindName("IntroVideo");
		RefIntroVideo.MediaEnded += IntroVideo_Ended;
		RefIntroVideo.MediaOpened += IntroVideo_Opened;
		RefIntroVideo.Visibility = Visibility.Hidden;
		RefEnterYourNameTB = (TextBox)FindName("EnterYourNameTB");
		RefEnterYourNameTB.IsKeyboardFocusedChanged += TextInputFocus;
		RefLeaderboard_OptOut = (CheckBox)FindName("Leaderboard_OptOut");
		RefLeaderboard_OptOut.Checked += Leaderboard_OptOut_ValueChanged;
		RefLeaderboard_OptOut.Unchecked += Leaderboard_OptOut_ValueChanged;
		RefNewsletterSignupButton = (Button)FindName("NewsletterSignupButton");
		RefNewsletterCheck = (CheckBox)FindName("NewsletterCheck");
		RefNewsletterCheck.Checked += NewsletterCheck_ValueChanged;
		RefNewsletterCheck.Unchecked += NewsletterCheck_ValueChanged;
		RefTextBoxNewsletter = (TextBox)FindName("TextBoxNewsletter");
		RefTextBoxNewsletter.IsKeyboardFocusedChanged += TextInputFocus;
		RefTextBoxNewsletter.TextChanged += NewsletterValueChanged;
		RefScribeLock = (Image)FindName("ScribeLock");
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

	private void StartVideo()
	{
		RefFadeInLogos.Stop();
		refLogo.Visibility = Visibility.Hidden;
		refProgress.Visibility = Visibility.Hidden;
		refProgressBorder.Visibility = Visibility.Hidden;
		refProgressImage.Visibility = Visibility.Hidden;
		refPresents.Visibility = Visibility.Hidden;
		refPartnerLogo.Visibility = Visibility.Hidden;
		stage++;
		RefIntroVideo.Visibility = Visibility.Visible;
		RefIntroVideo.Volume = ConfigSettings.Settings_SpeechVolume * MyAudioManager.GetMasterVolume();
		RefIntroVideo.IsMuted = false;
		RefIntroVideo.Play();
	}

	private void EndVideo()
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
		RefNewsletterSignupButton.IsEnabled = false;
		if (FrontendMenus.newsletterSignUp)
		{
			RefScribeLock.Visibility = Visibility.Hidden;
		}
		else
		{
			RefScribeLock.Visibility = Visibility.Visible;
		}
		UpdateLords();
		EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
		MainViewModel.Instance.EnterYourNameVis = true;
		RefEnterYourNameTB.Focus();
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
				MainViewModel.Instance.OptionsNewsletterVis = Visibility.Visible;
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
				RefScribeLock.Visibility = Visibility.Hidden;
				EnterYourNameClicked("SCR");
			});
			MainViewModel.Instance.OptionsNewsletterVis = Visibility.Hidden;
			break;
		case "NEWSCANCEL":
			MainViewModel.Instance.OptionsNewsletterVis = Visibility.Hidden;
			break;
		}
	}

	private void UpdateLords()
	{
		MainViewModel.Instance.Options_CurrentLord = HUD_Options.GetLordName(ConfigSettings.Settings_LordType);
		if (ConfigSettings.Settings_LordType == 0)
		{
			PropEx.SetSprite1(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite2(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite3(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite4(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
		}
		else
		{
			PropEx.SetSprite1(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[396]);
			PropEx.SetSprite2(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite3(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite4(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[396]);
		}
		if (ConfigSettings.Settings_LordType == 1)
		{
			PropEx.SetSprite1(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite2(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite3(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite4(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
		}
		else
		{
			PropEx.SetSprite1(RefArabicLordButton, MainViewModel.Instance.GameSprites[398]);
			PropEx.SetSprite2(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite3(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite4(RefArabicLordButton, MainViewModel.Instance.GameSprites[398]);
		}
		if (ConfigSettings.Settings_LordType == 2)
		{
			PropEx.SetSprite1(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite2(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite3(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite4(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
		}
		else
		{
			PropEx.SetSprite1(RefBedouinLordButton, MainViewModel.Instance.GameSprites[659]);
			PropEx.SetSprite2(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite3(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite4(RefBedouinLordButton, MainViewModel.Instance.GameSprites[659]);
		}
		if (ConfigSettings.Settings_LordType == 3)
		{
			PropEx.SetSprite1(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite2(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite3(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite4(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
		}
		else
		{
			PropEx.SetSprite1(RefScribeLordButton, MainViewModel.Instance.GameSprites[661]);
			PropEx.SetSprite2(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite3(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite4(RefScribeLordButton, MainViewModel.Instance.GameSprites[661]);
		}
		if (ConfigSettings.Settings_LordType == 4)
		{
			PropEx.SetSprite1(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite2(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite3(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite4(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
		}
		else
		{
			PropEx.SetSprite1(RefFemaleLordButton, MainViewModel.Instance.GameSprites[663]);
			PropEx.SetSprite2(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite3(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite4(RefFemaleLordButton, MainViewModel.Instance.GameSprites[663]);
		}
		if (ConfigSettings.Settings_LordType == 6)
		{
			PropEx.SetSprite1(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite2(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite3(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite4(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
		}
		else
		{
			PropEx.SetSprite1(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[709]);
			PropEx.SetSprite2(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite3(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite4(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[709]);
		}
		if (ConfigSettings.Settings_LordType == 7)
		{
			PropEx.SetSprite1(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite2(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite3(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite4(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
		}
		else
		{
			PropEx.SetSprite1(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[711]);
			PropEx.SetSprite2(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite3(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite4(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[711]);
		}
	}

	private void Leaderboard_OptOut_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Leaderboard_OptOut = !RefLeaderboard_OptOut.IsChecked.Value;
			MainViewModel.Instance.Show_LeaderboardOptIn = !ConfigSettings.Settings_HideSoTTiming;
			ConfigSettings.SaveSettings();
		}
	}

	private void NewsletterValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			RefNewsletterSignupButton.IsEnabled = HUD_Options.IsValidEmail(RefTextBoxNewsletter.Text) && RefNewsletterCheck.IsChecked.Value;
		}
	}

	private void NewsletterCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			RefNewsletterSignupButton.IsEnabled = HUD_Options.IsValidEmail(RefTextBoxNewsletter.Text) && RefNewsletterCheck.IsChecked.Value;
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
				refProgress.Visibility = Visibility.Hidden;
				refProgressBorder.Visibility = Visibility.Hidden;
				refProgressImage.Visibility = Visibility.Hidden;
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
		refProgress.Width = 490 - loadingProgress * 490 / 15;
	}

	private void IntroVideo_Opened(object sender, RoutedEventArgs args)
	{
		RefIntroVideo.Pause();
		binkLoaded = true;
	}

	private void IntroVideo_Ended(object sender, RoutedEventArgs args)
	{
		EndVideo();
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAML/IntroSequence.xaml");
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

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}
}
