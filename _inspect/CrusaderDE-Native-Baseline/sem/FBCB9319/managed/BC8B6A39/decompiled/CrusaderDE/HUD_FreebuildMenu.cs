using System;
using Noesis;

namespace CrusaderDE;

public class HUD_FreebuildMenu : UserControl
{
	private int selectedEvent;

	private int invasionEnemy;

	private WGT_Heading RefHeading;

	private Slider RefEventSlider;

	private Slider RefInvasionSize;

	private Button RefInvadeNowButton;

	private Button RefFreeBuildBanditEvent;

	private Button RefFreeBuildArchersEvent;

	private Button RefFreeBuildWolfEvent;

	private Button RefFreeBuildRabbitEvent;

	private TextBlock RefFreebuildMissingText;

	private RadioButton RefFreebuildSizeArcherDefault;

	private DateTime lockOutInvadeButton = DateTime.MinValue;

	public int FreebuildInvasionSizeType;

	private int[] freeBuildInvasionSize = new int[33];

	public HUD_FreebuildMenu()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDFreebuildMenu = this;
		RefHeading = (WGT_Heading)FindName("ScenarioHeader");
		RefEventSlider = (Slider)FindName("EventSlider");
		RefEventSlider.ValueChanged += EventSlider_ValueChanged;
		RefInvasionSize = (Slider)FindName("InvasionSlider");
		RefInvasionSize.ValueChanged += FreebuildInvasionSizeSlider_ValueChanged;
		RefInvadeNowButton = (Button)FindName("InvadeNowButton");
		RefFreeBuildBanditEvent = (Button)FindName("FreeBuildBanditEvent");
		RefFreeBuildArchersEvent = (Button)FindName("FreeBuildArchersEvent");
		RefFreeBuildWolfEvent = (Button)FindName("FreeBuildWolfEvent");
		RefFreeBuildRabbitEvent = (Button)FindName("FreeBuildRabbitEvent");
		RefFreebuildSizeArcherDefault = (RadioButton)FindName("FreebuildSizeArcherDefault");
		RefFreebuildMissingText = (TextBlock)FindName("FreebuildMissingText");
	}

	public static void ToggleMenu()
	{
		if (MainViewModel.Instance.Show_HUD_FreebuildMenu)
		{
			MainViewModel.Instance.Show_HUD_FreebuildMenu = false;
			return;
		}
		if (MainViewModel.Instance.Show_HUD_LoadSaveRequester)
		{
			MainViewModel.Instance.HUDLoadSaveRequester.CloseRequester();
		}
		if (MainViewModel.Instance.Show_HUD_Confirmation)
		{
			MainViewModel.Instance.HUDConfirmationPopup.ConfirmationClicked(2);
		}
		if (MainViewModel.Instance.Show_HUD_IngameMenu)
		{
			MainViewModel.Instance.HUDmain.InGameOptions(null, null);
		}
		MainViewModel.Instance.HUDFreebuildMenu.Init();
	}

	public void Init()
	{
		selectedEvent = 0;
		invasionEnemy = 0;
		for (int i = 0; i < freeBuildInvasionSize.Length; i++)
		{
			freeBuildInvasionSize[i] = 0;
			if (i < 32)
			{
				MainViewModel.Instance.FreebuildInvasionSize[i] = "";
			}
		}
		if (GameData.Instance.lastGameState != null)
		{
			RefFreeBuildArchersEvent.IsEnabled = GameData.Instance.lastGameState.pingtimes[0] > 0;
			RefFreeBuildBanditEvent.IsEnabled = GameData.Instance.lastGameState.pingtimes[0] > 0;
			RefFreeBuildWolfEvent.IsEnabled = GameData.Instance.lastGameState.pingtimes[1] > 0;
			RefFreeBuildRabbitEvent.IsEnabled = GameData.Instance.lastGameState.pingtimes[2] > 0;
			if (GameData.Instance.lastGameState.pingtimes[0] == 0)
			{
				RefFreebuildMissingText.Visibility = Visibility.Visible;
			}
			else
			{
				RefFreebuildMissingText.Visibility = Visibility.Hidden;
			}
		}
		RefFreebuildSizeArcherDefault.IsChecked = true;
		ButtonSelectInvasionSize(0);
		UpdateInvadeButton();
		MainViewModel.Instance.Show_HUD_FreebuildMenu = true;
		UpdateEventButtons();
		UpdateInvasionButtons();
	}

	public void Update()
	{
		UpdateInvadeButton();
	}

	private void UpdateEventButtons()
	{
		for (int i = 0; i < 10; i++)
		{
			if (i == selectedEvent)
			{
				MainViewModel.Instance.FreebuildEventBorders[i] = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.FreebuildEventBorders[i] = Visibility.Hidden;
			}
		}
		RefEventSlider.Value = 1f;
		switch (selectedEvent)
		{
		case 0:
		case 2:
		case 3:
		case 7:
		case 8:
			MainViewModel.Instance.FreebuildSliderVis = false;
			break;
		case 1:
		case 4:
		case 9:
			MainViewModel.Instance.FreebuildSliderVis = true;
			MainViewModel.Instance.FreebuildSizeMax = 10;
			MainViewModel.Instance.FreebuildSizeFreq = 1;
			MainViewModel.Instance.FreebuildSizeText = "1";
			break;
		case 5:
		case 6:
			MainViewModel.Instance.FreebuildSliderVis = true;
			MainViewModel.Instance.FreebuildSizeMax = 50;
			MainViewModel.Instance.FreebuildSizeFreq = 5;
			MainViewModel.Instance.FreebuildSizeText = "1";
			break;
		}
	}

	private void UpdateInvasionButtons()
	{
		for (int i = 0; i < 4; i++)
		{
			if (i == invasionEnemy)
			{
				MainViewModel.Instance.FreebuildEventBorders[i + 10] = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.FreebuildEventBorders[i + 10] = Visibility.Hidden;
			}
		}
	}

	private void EventSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefEventSlider.Value;
		MainViewModel.Instance.FreebuildSizeText = num.ToString();
	}

	public void ButtonClicked(int param)
	{
		switch (param)
		{
		case -10:
			MainViewModel.Instance.Show_HUD_FreebuildMenu = false;
			break;
		case -1:
			EngineInterface.GameAction(Enums.GameActionCommand.FreeBuild_Event, GameData.freeBuildEventsOrder[selectedEvent], (int)RefEventSlider.Value);
			MainViewModel.Instance.Show_HUD_FreebuildMenu = false;
			break;
		case -2:
		{
			EngineInterface.GameAction(Enums.GameActionCommand.FreeBuild_InvasionCharSet, 0, invasionEnemy);
			for (int k = 0; k < freeBuildInvasionSize.Length; k++)
			{
				if (freeBuildInvasionSize[k] > 0)
				{
					EngineInterface.GameAction(Enums.GameActionCommand.FreeBuild_InvasionCount, k, freeBuildInvasionSize[k]);
				}
			}
			EngineInterface.GameAction(Enums.GameActionCommand.FreeBuild_InvasionStart, 0, 0);
			lockOutInvadeButton = DateTime.UtcNow.AddSeconds(2.0);
			break;
		}
		case -3:
			EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 4, 0);
			SFXManager.instance.playSound(326);
			break;
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
			if (selectedEvent != param - 1)
			{
				selectedEvent = param - 1;
				UpdateEventButtons();
			}
			break;
		case 20:
		case 21:
		case 22:
			invasionEnemy = param - 20;
			UpdateInvasionButtons();
			break;
		default:
			if (param >= 100 && param <= 150)
			{
				ButtonSelectInvasionSize(param - 100);
			}
			break;
		case 200:
		case 201:
		case 202:
		case 203:
		case 204:
		case 205:
		{
			for (int i = 0; i < freeBuildInvasionSize.Length; i++)
			{
				freeBuildInvasionSize[i] = 0;
			}
			switch (invasionEnemy)
			{
			case 0:
				switch (param)
				{
				case 200:
					freeBuildInvasionSize[0] = 10;
					break;
				case 201:
					freeBuildInvasionSize[0] = 15;
					freeBuildInvasionSize[2] = 20;
					break;
				case 202:
					freeBuildInvasionSize[0] = 25;
					freeBuildInvasionSize[2] = 20;
					freeBuildInvasionSize[4] = 10;
					freeBuildInvasionSize[1] = 5;
					freeBuildInvasionSize[8] = 5;
					freeBuildInvasionSize[7] = 10;
					freeBuildInvasionSize[13] = 5;
					break;
				case 203:
					freeBuildInvasionSize[0] = 25;
					freeBuildInvasionSize[2] = 30;
					freeBuildInvasionSize[4] = 15;
					freeBuildInvasionSize[1] = 8;
					freeBuildInvasionSize[3] = 8;
					freeBuildInvasionSize[5] = 10;
					freeBuildInvasionSize[14] = 5;
					freeBuildInvasionSize[8] = 10;
					freeBuildInvasionSize[7] = 15;
					freeBuildInvasionSize[9] = 3;
					freeBuildInvasionSize[12] = 1;
					freeBuildInvasionSize[11] = 1;
					freeBuildInvasionSize[13] = 5;
					break;
				case 204:
					freeBuildInvasionSize[0] = 25;
					freeBuildInvasionSize[2] = 30;
					freeBuildInvasionSize[4] = 20;
					freeBuildInvasionSize[1] = 12;
					freeBuildInvasionSize[3] = 15;
					freeBuildInvasionSize[5] = 12;
					freeBuildInvasionSize[6] = 5;
					freeBuildInvasionSize[14] = 5;
					freeBuildInvasionSize[8] = 15;
					freeBuildInvasionSize[7] = 20;
					freeBuildInvasionSize[15] = 3;
					freeBuildInvasionSize[9] = 3;
					freeBuildInvasionSize[12] = 2;
					freeBuildInvasionSize[10] = 1;
					freeBuildInvasionSize[11] = 1;
					freeBuildInvasionSize[13] = 10;
					break;
				case 205:
					freeBuildInvasionSize[0] = 150;
					freeBuildInvasionSize[2] = 120;
					freeBuildInvasionSize[4] = 70;
					freeBuildInvasionSize[1] = 60;
					freeBuildInvasionSize[3] = 50;
					freeBuildInvasionSize[5] = 50;
					freeBuildInvasionSize[6] = 30;
					freeBuildInvasionSize[14] = 50;
					freeBuildInvasionSize[8] = 50;
					freeBuildInvasionSize[7] = 100;
					freeBuildInvasionSize[15] = 10;
					freeBuildInvasionSize[9] = 10;
					freeBuildInvasionSize[12] = 5;
					freeBuildInvasionSize[10] = 5;
					freeBuildInvasionSize[11] = 5;
					freeBuildInvasionSize[13] = 10;
					break;
				}
				break;
			case 1:
				switch (param)
				{
				case 200:
					freeBuildInvasionSize[18] = 10;
					break;
				case 201:
					freeBuildInvasionSize[18] = 15;
					freeBuildInvasionSize[17] = 20;
					break;
				case 202:
					freeBuildInvasionSize[18] = 25;
					freeBuildInvasionSize[17] = 20;
					freeBuildInvasionSize[21] = 6;
					freeBuildInvasionSize[16] = 5;
					freeBuildInvasionSize[20] = 5;
					freeBuildInvasionSize[8] = 5;
					freeBuildInvasionSize[7] = 10;
					freeBuildInvasionSize[13] = 5;
					break;
				case 203:
					freeBuildInvasionSize[18] = 25;
					freeBuildInvasionSize[17] = 30;
					freeBuildInvasionSize[21] = 12;
					freeBuildInvasionSize[16] = 8;
					freeBuildInvasionSize[20] = 8;
					freeBuildInvasionSize[22] = 6;
					freeBuildInvasionSize[23] = 2;
					freeBuildInvasionSize[8] = 10;
					freeBuildInvasionSize[7] = 15;
					freeBuildInvasionSize[9] = 3;
					freeBuildInvasionSize[12] = 1;
					freeBuildInvasionSize[11] = 1;
					freeBuildInvasionSize[13] = 5;
					break;
				case 204:
					freeBuildInvasionSize[18] = 25;
					freeBuildInvasionSize[17] = 30;
					freeBuildInvasionSize[16] = 12;
					freeBuildInvasionSize[20] = 12;
					freeBuildInvasionSize[22] = 12;
					freeBuildInvasionSize[21] = 16;
					freeBuildInvasionSize[23] = 3;
					freeBuildInvasionSize[8] = 15;
					freeBuildInvasionSize[7] = 20;
					freeBuildInvasionSize[15] = 3;
					freeBuildInvasionSize[9] = 3;
					freeBuildInvasionSize[12] = 2;
					freeBuildInvasionSize[10] = 1;
					freeBuildInvasionSize[11] = 1;
					freeBuildInvasionSize[13] = 10;
					break;
				case 205:
					freeBuildInvasionSize[18] = 150;
					freeBuildInvasionSize[17] = 120;
					freeBuildInvasionSize[21] = 70;
					freeBuildInvasionSize[16] = 60;
					freeBuildInvasionSize[20] = 60;
					freeBuildInvasionSize[22] = 60;
					freeBuildInvasionSize[23] = 15;
					freeBuildInvasionSize[8] = 50;
					freeBuildInvasionSize[7] = 100;
					freeBuildInvasionSize[15] = 10;
					freeBuildInvasionSize[9] = 10;
					freeBuildInvasionSize[12] = 5;
					freeBuildInvasionSize[10] = 5;
					freeBuildInvasionSize[11] = 5;
					freeBuildInvasionSize[13] = 10;
					break;
				}
				break;
			case 2:
				switch (param)
				{
				case 200:
					freeBuildInvasionSize[30] = 5;
					freeBuildInvasionSize[28] = 5;
					break;
				case 201:
					freeBuildInvasionSize[30] = 15;
					freeBuildInvasionSize[28] = 20;
					break;
				case 202:
					freeBuildInvasionSize[30] = 25;
					freeBuildInvasionSize[28] = 20;
					freeBuildInvasionSize[24] = 10;
					freeBuildInvasionSize[29] = 5;
					freeBuildInvasionSize[8] = 5;
					freeBuildInvasionSize[7] = 10;
					freeBuildInvasionSize[13] = 5;
					break;
				case 203:
					freeBuildInvasionSize[30] = 25;
					freeBuildInvasionSize[28] = 30;
					freeBuildInvasionSize[24] = 15;
					freeBuildInvasionSize[29] = 12;
					freeBuildInvasionSize[31] = 15;
					freeBuildInvasionSize[8] = 10;
					freeBuildInvasionSize[7] = 15;
					freeBuildInvasionSize[9] = 3;
					freeBuildInvasionSize[12] = 1;
					freeBuildInvasionSize[11] = 1;
					freeBuildInvasionSize[13] = 5;
					break;
				case 204:
					freeBuildInvasionSize[30] = 25;
					freeBuildInvasionSize[28] = 30;
					freeBuildInvasionSize[24] = 20;
					freeBuildInvasionSize[29] = 12;
					freeBuildInvasionSize[31] = 20;
					freeBuildInvasionSize[26] = 5;
					freeBuildInvasionSize[8] = 15;
					freeBuildInvasionSize[7] = 20;
					freeBuildInvasionSize[15] = 3;
					freeBuildInvasionSize[9] = 3;
					freeBuildInvasionSize[12] = 2;
					freeBuildInvasionSize[10] = 1;
					freeBuildInvasionSize[11] = 1;
					freeBuildInvasionSize[13] = 10;
					break;
				case 205:
					freeBuildInvasionSize[30] = 150;
					freeBuildInvasionSize[28] = 120;
					freeBuildInvasionSize[24] = 70;
					freeBuildInvasionSize[29] = 60;
					freeBuildInvasionSize[31] = 70;
					freeBuildInvasionSize[26] = 30;
					freeBuildInvasionSize[8] = 50;
					freeBuildInvasionSize[7] = 100;
					freeBuildInvasionSize[15] = 10;
					freeBuildInvasionSize[9] = 10;
					freeBuildInvasionSize[12] = 5;
					freeBuildInvasionSize[10] = 5;
					freeBuildInvasionSize[11] = 5;
					freeBuildInvasionSize[13] = 10;
					break;
				}
				break;
			}
			for (int j = 0; j < freeBuildInvasionSize.Length; j++)
			{
				if (j < 32)
				{
					if (freeBuildInvasionSize[j] > 0)
					{
						MainViewModel.Instance.FreebuildInvasionSize[j] = freeBuildInvasionSize[j].ToString();
					}
					else
					{
						MainViewModel.Instance.FreebuildInvasionSize[j] = "";
					}
				}
			}
			ButtonSelectInvasionSize(FreebuildInvasionSizeType);
			break;
		}
		}
	}

	public void ButtonSelectInvasionSize(int mode)
	{
		FreebuildInvasionSizeType = mode;
		int invasionSizeTroopTypeFromIndex = GameData.getInvasionSizeTroopTypeFromIndex(mode);
		int num = freeBuildInvasionSize[mode];
		MainViewModel.Instance.FreebuildInvasionSizeMax = GameData.getMaxTroopsForInvasion((Enums.eChimps)invasionSizeTroopTypeFromIndex);
		int num2 = MainViewModel.Instance.FreebuildInvasionSizeMax / 10;
		if (num2 == 0)
		{
			num2 = 1;
		}
		MainViewModel.Instance.FreebuildInvasionSizeFreq = num2;
		RefInvasionSize.Value = num;
		MainViewModel.Instance.FreebuildInvasionSizeText = num + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, invasionSizeTroopTypeFromIndex);
	}

	private void UpdateInvadeButton()
	{
		int num = 0;
		for (int i = 0; i < freeBuildInvasionSize.Length; i++)
		{
			num += freeBuildInvasionSize[i];
		}
		RefInvadeNowButton.IsEnabled = num > 0 && GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.pingtimes[0] > 0 && lockOutInvadeButton < DateTime.UtcNow;
	}

	private void FreebuildInvasionSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefInvasionSize.Value;
		int invasionSizeTroopTypeFromIndex = GameData.getInvasionSizeTroopTypeFromIndex(FreebuildInvasionSizeType);
		freeBuildInvasionSize[FreebuildInvasionSizeType] = num;
		MainViewModel.Instance.FreebuildInvasionSize[FreebuildInvasionSizeType] = num.ToString();
		MainViewModel.Instance.FreebuildInvasionSizeText = num + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, invasionSizeTroopTypeFromIndex);
		UpdateInvadeButton();
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_FreebuildMenu.xaml");
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
