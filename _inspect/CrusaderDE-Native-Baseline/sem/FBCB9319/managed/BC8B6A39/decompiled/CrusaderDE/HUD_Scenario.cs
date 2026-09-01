using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Noesis;

namespace CrusaderDE;

public class HUD_Scenario : UserControl
{
	private WGT_Heading RefHeading;

	private Grid RefViewRoot;

	private Grid RefViewMain;

	private Grid RefViewStartingGoods;

	private Grid RefViewTradedGoods;

	private Grid RefViewBuildingAvailibity;

	private Grid RefViewInvasions;

	private Grid RefViewEvents;

	private Grid RefViewEventsConditions;

	private Grid RefViewEventsActions;

	private Grid RefViewEditMessage;

	private Grid RefScenarioViewEditTeams;

	private Grid RefScenarioViewAdjustDates;

	private ListView RefScenarioActionList;

	private ListView RefScenarioBuildingList;

	private TextBox RefStartingYear;

	private TextBox RefEditMessage;

	private Slider RefStartingPop;

	private Slider RefStartingSpecialGold;

	public Slider RefStartingGoods;

	private RadioButton RefStartingGoodsGoldDefault;

	private CheckBox RefFasterGoodsCheck;

	private StackPanel RefLeftPanel;

	private TextBox RefInvasionYear;

	public Slider RefInvasionRepeatSlider;

	private Button RefSignpost;

	public Slider RefInvasionSize;

	private RadioButton RefInvasionSizeArcherDefault;

	private TextBox RefEventYear;

	private ListView RefScenarioEventActionList;

	public Slider RefActionRepeatMonthsSlider;

	public Slider RefActionRepeatSlider;

	public Slider RefActionValueSlider;

	public Slider RefActionValue2Slider;

	private ListView RefScenarioEventConditionList;

	public Slider RefConditionValueSlider;

	public Slider RefSliderEditTeam1;

	public Slider RefSliderEditTeam2;

	public Slider RefSliderEditTeam3;

	public Slider RefSliderEditTeam4;

	public Slider RefSliderEditTeam5;

	public Slider RefSliderEditTeam6;

	public Slider RefSliderEditTeam7;

	public Slider RefSliderEditTeam8;

	private TextBox RefAdjustStartingYear;

	private Button RefButtonEditText;

	private Button RefButtonStartingGoodsPresetLow;

	private Button RefButtonStartingGoodsPresetMedium;

	private Button RefButtonStartingGoodsPresetHigh;

	private RadioButton RefButtonInvasionInvPlayer1;

	private RadioButton RefButtonInvasionInvPlayer2;

	private RadioButton RefButtonInvasionInvPlayer3;

	private RadioButton RefButtonInvasionInvPlayer4;

	private RadioButton RefButtonInvasionInvPlayer5;

	private RadioButton RefButtonInvasionInvPlayer6;

	private RadioButton RefButtonInvasionInvPlayer7;

	private RadioButton RefButtonInvasionInvPlayer8;

	private RadioButton RefButtonInvasionReinPlayer1;

	private RadioButton RefButtonInvasionReinPlayer2;

	private RadioButton RefButtonInvasionReinPlayer3;

	private RadioButton RefButtonInvasionReinPlayer4;

	private RadioButton RefButtonInvasionReinPlayer5;

	private RadioButton RefButtonInvasionReinPlayer6;

	private RadioButton RefButtonInvasionReinPlayer7;

	private RadioButton RefButtonInvasionReinPlayer8;

	private SolidColorBrush[] lordColours = new SolidColorBrush[6]
	{
		new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, 240, 121, 30)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, 0, 209, 35)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, 204, 27, 56)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, 0, 0, 0)),
		new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
	};

	public int newStartingMonthValue;

	private ObservableCollection<ScenarioEditorRow> BuildingItems = new ObservableCollection<ScenarioEditorRow>();

	private ObservableCollection<ScenarioEditorRow> EventConditionItems = new ObservableCollection<ScenarioEditorRow>();

	private Dictionary<int, ScenarioEditorRow> BuildingItemsDict = new Dictionary<int, ScenarioEditorRow>();

	private bool barracksItemsShowing;

	private bool mercPostItemsShowing;

	private bool bedouinItemsShowing;

	private int barracksWoodRow = -1;

	private int barracksStoneRow = -1;

	private int barracksBedouinRow = -1;

	private EngineInterface.tl_event scenarioCurrentEvent;

	private EngineInterface.tl_invasion scenarioCurrentInvasion;

	private int scenarioCurrentLine = -1;

	private ScenarioEditorRow activeEventActionRow;

	private ScenarioEditorRow activeEventConditionRow;

	private int[] SliderCurve500 = new int[101]
	{
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
		10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
		20, 22, 24, 26, 28, 30, 32, 34, 36, 38,
		40, 42, 44, 46, 48, 50, 52, 54, 56, 58,
		60, 62, 64, 66, 68, 70, 72, 74, 76, 78,
		80, 82, 84, 86, 88, 90, 92, 94, 96, 98,
		100, 110, 120, 130, 140, 150, 160, 170, 180, 190,
		200, 210, 220, 230, 240, 250, 260, 270, 280, 290,
		300, 310, 320, 330, 340, 350, 360, 370, 380, 390,
		400, 410, 420, 430, 440, 450, 460, 470, 480, 490,
		500
	};

	public static int[] SliderCurve1000 = new int[101]
	{
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
		10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
		20, 21, 22, 24, 26, 28, 30, 32, 34, 36,
		38, 40, 42, 44, 46, 48, 50, 54, 58, 62,
		66, 70, 74, 78, 82, 86, 90, 94, 98, 100,
		105, 110, 120, 130, 140, 150, 160, 170, 180, 190,
		200, 220, 240, 260, 280, 300, 320, 340, 360, 380,
		400, 420, 440, 460, 480, 500, 520, 540, 560, 580,
		600, 620, 640, 660, 680, 700, 720, 740, 760, 780,
		800, 820, 840, 860, 880, 900, 920, 940, 960, 980,
		1000
	};

	private int[] SliderCurve10000 = new int[146]
	{
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
		10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
		20, 21, 22, 24, 26, 28, 30, 32, 34, 36,
		38, 40, 42, 44, 46, 48, 50, 54, 58, 62,
		66, 70, 74, 78, 82, 86, 90, 94, 98, 100,
		105, 110, 120, 130, 140, 150, 160, 170, 180, 190,
		200, 220, 240, 260, 280, 300, 320, 340, 360, 380,
		400, 420, 440, 460, 480, 500, 520, 540, 560, 580,
		600, 620, 640, 660, 680, 700, 720, 740, 760, 780,
		800, 820, 840, 860, 880, 900, 920, 940, 960, 980,
		1000, 1200, 1400, 1600, 1800, 2000, 2200, 2400, 2600, 2800,
		3000, 3200, 3400, 3600, 3800, 4000, 4200, 4400, 4600, 4800,
		5000, 5200, 5400, 5600, 5800, 6000, 6200, 6400, 6600, 6800,
		7000, 7200, 7400, 7600, 7800, 8000, 8200, 8400, 8600, 8800,
		9000, 9200, 9400, 9600, 9800, 10000
	};

	private int[] SliderCurve25000 = new int[161]
	{
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
		10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
		20, 21, 22, 24, 26, 28, 30, 32, 34, 36,
		38, 40, 42, 44, 46, 48, 50, 54, 58, 62,
		66, 70, 74, 78, 82, 86, 90, 94, 98, 100,
		105, 110, 120, 130, 140, 150, 160, 170, 180, 190,
		200, 220, 240, 260, 280, 300, 320, 340, 360, 380,
		400, 420, 440, 460, 480, 500, 520, 540, 560, 580,
		600, 620, 640, 660, 680, 700, 720, 740, 760, 780,
		800, 820, 840, 860, 880, 900, 920, 940, 960, 980,
		1000, 1200, 1400, 1600, 1800, 2000, 2200, 2400, 2600, 2800,
		3000, 3200, 3400, 3600, 3800, 4000, 4200, 4400, 4600, 4800,
		5000, 5200, 5400, 5600, 5800, 6000, 6200, 6400, 6600, 6800,
		7000, 7200, 7400, 7600, 7800, 8000, 8200, 8400, 8600, 8800,
		9000, 9200, 9400, 9600, 9800, 10000, 11000, 12000, 13000, 14000,
		15000, 16000, 17000, 18000, 19000, 20000, 21000, 22000, 23000, 24000,
		25000
	};

	public HUD_Scenario()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDScenario = this;
		RefHeading = (WGT_Heading)FindName("ScenarioHeader");
		RefViewRoot = (Grid)FindName("LayoutRoot");
		RefViewRoot.Visibility = Visibility.Hidden;
		RefLeftPanel = (StackPanel)FindName("LeftPanel");
		RefViewMain = (Grid)FindName("ScenarioViewMain");
		RefViewStartingGoods = (Grid)FindName("ScenarioViewStartingGoods");
		RefViewTradedGoods = (Grid)FindName("ScenarioViewTradedGoods");
		RefViewBuildingAvailibity = (Grid)FindName("ScenarioViewBuildingAvailibility");
		RefViewInvasions = (Grid)FindName("ScenarioViewInvasions");
		RefViewEvents = (Grid)FindName("ScenarioViewEvents");
		RefViewEventsConditions = (Grid)FindName("ScenarioViewEventsConditions");
		RefViewEventsActions = (Grid)FindName("ScenarioViewEventsActions");
		RefViewEditMessage = (Grid)FindName("ScenarioEditMessage");
		RefScenarioViewEditTeams = (Grid)FindName("ScenarioViewEditTeams");
		RefScenarioViewAdjustDates = (Grid)FindName("ScenarioViewAdjustDates");
		RefStartingGoodsGoldDefault = (RadioButton)FindName("StartingGoodsGoldDefault");
		RefFasterGoodsCheck = (CheckBox)FindName("FasterGoodsCheck");
		RefFasterGoodsCheck.Checked += FasterGoodsCheck_ValueChanged;
		RefFasterGoodsCheck.Unchecked += FasterGoodsCheck_ValueChanged;
		RefScenarioActionList = (ListView)FindName("ScenarioActionList");
		RefScenarioBuildingList = (ListView)FindName("ScenarioBuildingList");
		RefScenarioActionList.SelectionChanged += delegate
		{
			if (RefScenarioActionList.SelectedItem != null)
			{
				SelectActionRow(int.Parse(((ScenarioEditorRow)RefScenarioActionList.SelectedItem).DataValue, Director.defaultCulture));
				RefScenarioActionList.SelectedItem = null;
			}
		};
		RefStartingYear = (TextBox)FindName("TextBoxStartingYear");
		RefStartingYear.PreviewTextInput += NumberValidationTextBox;
		RefStartingYear.IsKeyboardFocusedChanged += TextInputFocus;
		RefAdjustStartingYear = (TextBox)FindName("TextBoxAdjustStartingYear");
		RefAdjustStartingYear.PreviewTextInput += NumberValidationTextBox;
		RefAdjustStartingYear.IsKeyboardFocusedChanged += TextInputFocus;
		RefEditMessage = (TextBox)FindName("TextBoxScenarioMessage");
		RefEditMessage.IsKeyboardFocusedChanged += TextInputFocus;
		RefEditMessage.TextChanged += EditMessageTextChanged;
		RefStartingPop = (Slider)FindName("PopSlider");
		RefStartingPop.ValueChanged += ScenarioPopularitySlider_ValueChanged;
		RefStartingSpecialGold = (Slider)FindName("SpecialGoldSlider");
		RefStartingSpecialGold.ValueChanged += ScenarioSpecialGoldSlider_ValueChanged;
		RefStartingGoods = (Slider)FindName("StartingGoodsSlider");
		RefStartingGoods.ValueChanged += ScenarioStartingGoodsSlider_ValueChanged;
		((Storyboard)base.Resources["Outtro"]).Completed += delegate
		{
			RefViewRoot.Visibility = Visibility.Hidden;
		};
		RefSignpost = (Button)FindName("Signpost");
		RefInvasionYear = (TextBox)FindName("TextBoxStartingYearInvasion");
		RefInvasionYear.PreviewTextInput += NumberValidationTextBox;
		RefInvasionYear.IsKeyboardFocusedChanged += TextInputFocus;
		RefInvasionRepeatSlider = (Slider)FindName("InvasionRepeatSlider");
		RefInvasionRepeatSlider.ValueChanged += ScenarioInvasionRepeatSlider_ValueChanged;
		RefInvasionSize = (Slider)FindName("InvasionSlider");
		RefInvasionSize.ValueChanged += ScenarioInvasionSizeSlider_ValueChanged;
		RefInvasionSizeArcherDefault = (RadioButton)FindName("InvasionSizeArcherDefault");
		RefEventYear = (TextBox)FindName("TextBoxStartingYearEvent");
		RefEventYear.PreviewTextInput += NumberValidationTextBox;
		RefEventYear.IsKeyboardFocusedChanged += TextInputFocus;
		RefScenarioEventActionList = (ListView)FindName("ScenarioEventActionList");
		RefScenarioEventActionList.SelectionChanged += delegate
		{
			if (RefScenarioEventActionList.SelectedItem != null)
			{
				RefScenarioEventActionList.SelectedItem = null;
			}
		};
		RefActionRepeatMonthsSlider = (Slider)FindName("ActionRepeatMonthsSlider");
		RefActionRepeatMonthsSlider.ValueChanged += ActionRepeatMonthsSlider_ValueChanged;
		RefActionRepeatSlider = (Slider)FindName("ActionRepeatSlider");
		RefActionRepeatSlider.ValueChanged += ActionRepeatSlider_ValueChanged;
		RefActionValueSlider = (Slider)FindName("ActionValueSlider");
		RefActionValueSlider.ValueChanged += ActionValueSlider_ValueChanged;
		RefActionValue2Slider = (Slider)FindName("ActionValue2Slider");
		RefActionValue2Slider.ValueChanged += ActionValue2Slider_ValueChanged;
		RefScenarioEventConditionList = (ListView)FindName("ScenarioEventConditionList");
		RefScenarioEventConditionList.SelectionChanged += delegate
		{
			if (RefScenarioEventConditionList.SelectedItem != null)
			{
				RefScenarioEventConditionList.SelectedItem = null;
			}
		};
		RefConditionValueSlider = (Slider)FindName("ConditionValueSlider");
		RefConditionValueSlider.ValueChanged += ConditionValueSlider_ValueChanged;
		RefSliderEditTeam1 = (Slider)FindName("SliderEditTeam1");
		RefSliderEditTeam1.ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam2 = (Slider)FindName("SliderEditTeam2");
		RefSliderEditTeam2.ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam3 = (Slider)FindName("SliderEditTeam3");
		RefSliderEditTeam3.ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam4 = (Slider)FindName("SliderEditTeam4");
		RefSliderEditTeam4.ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam5 = (Slider)FindName("SliderEditTeam5");
		RefSliderEditTeam5.ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam6 = (Slider)FindName("SliderEditTeam6");
		RefSliderEditTeam6.ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam7 = (Slider)FindName("SliderEditTeam7");
		RefSliderEditTeam7.ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam8 = (Slider)FindName("SliderEditTeam8");
		RefSliderEditTeam8.ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefButtonEditText = (Button)FindName("ButtonEditText");
		RefButtonStartingGoodsPresetLow = (Button)FindName("ButtonStartingGoodsPresetLow");
		RefButtonStartingGoodsPresetMedium = (Button)FindName("ButtonStartingGoodsPresetMedium");
		RefButtonStartingGoodsPresetHigh = (Button)FindName("ButtonStartingGoodsPresetHigh");
		RefButtonInvasionInvPlayer1 = (RadioButton)FindName("ButtonInvasionInvPlayer1");
		RefButtonInvasionInvPlayer2 = (RadioButton)FindName("ButtonInvasionInvPlayer2");
		RefButtonInvasionInvPlayer3 = (RadioButton)FindName("ButtonInvasionInvPlayer3");
		RefButtonInvasionInvPlayer4 = (RadioButton)FindName("ButtonInvasionInvPlayer4");
		RefButtonInvasionInvPlayer5 = (RadioButton)FindName("ButtonInvasionInvPlayer5");
		RefButtonInvasionInvPlayer6 = (RadioButton)FindName("ButtonInvasionInvPlayer6");
		RefButtonInvasionInvPlayer7 = (RadioButton)FindName("ButtonInvasionInvPlayer7");
		RefButtonInvasionInvPlayer8 = (RadioButton)FindName("ButtonInvasionInvPlayer8");
		RefButtonInvasionReinPlayer1 = (RadioButton)FindName("ButtonInvasionReinPlayer1");
		RefButtonInvasionReinPlayer2 = (RadioButton)FindName("ButtonInvasionReinPlayer2");
		RefButtonInvasionReinPlayer3 = (RadioButton)FindName("ButtonInvasionReinPlayer3");
		RefButtonInvasionReinPlayer4 = (RadioButton)FindName("ButtonInvasionReinPlayer4");
		RefButtonInvasionReinPlayer5 = (RadioButton)FindName("ButtonInvasionReinPlayer5");
		RefButtonInvasionReinPlayer6 = (RadioButton)FindName("ButtonInvasionReinPlayer6");
		RefButtonInvasionReinPlayer7 = (RadioButton)FindName("ButtonInvasionReinPlayer7");
		RefButtonInvasionReinPlayer8 = (RadioButton)FindName("ButtonInvasionReinPlayer8");
		if (FatControler.italian)
		{
			PropEx.SetGlowButtonFontSize(RefButtonEditText, 14);
			PropEx.SetGlowButtonTextHeight(RefButtonEditText, 20);
			PropEx.SetGlowButtonFontSize(RefButtonStartingGoodsPresetLow, 14);
			PropEx.SetGlowButtonTextHeight(RefButtonStartingGoodsPresetLow, 20);
			PropEx.SetGlowButtonFontSize(RefButtonStartingGoodsPresetMedium, 14);
			PropEx.SetGlowButtonTextHeight(RefButtonStartingGoodsPresetMedium, 20);
			PropEx.SetGlowButtonFontSize(RefButtonStartingGoodsPresetHigh, 12);
			PropEx.SetGlowButtonTextHeight(RefButtonStartingGoodsPresetHigh, 18);
		}
		if (FatControler.portuguese)
		{
			PropEx.SetGlowButtonFontSize(RefButtonStartingGoodsPresetLow, 12);
			PropEx.SetGlowButtonTextHeight(RefButtonStartingGoodsPresetLow, 18);
			PropEx.SetGlowButtonFontSize(RefButtonStartingGoodsPresetMedium, 12);
			PropEx.SetGlowButtonTextHeight(RefButtonStartingGoodsPresetMedium, 18);
			PropEx.SetGlowButtonFontSize(RefButtonStartingGoodsPresetHigh, 12);
			PropEx.SetGlowButtonTextHeight(RefButtonStartingGoodsPresetHigh, 18);
			MainViewModel.Instance.ScenarioTradeTextSize = "12";
			MainViewModel.Instance.ScenarioTradeTextHeight = "18";
			PropEx.SetGlowButtonFontSize(RefSignpost, 12);
			PropEx.SetGlowButtonTextHeight(RefSignpost, 18);
		}
		if (FatControler.japanese)
		{
			PropEx.SetGlowButtonFontSize(RefButtonEditText, 12);
			PropEx.SetGlowButtonTextHeight(RefButtonEditText, 20);
		}
		if (FatControler.french)
		{
			PropEx.SetGlowButtonFontSize(RefButtonEditText, 14);
			PropEx.SetGlowButtonTextHeight(RefButtonEditText, 20);
			MainViewModel.Instance.ScenarioTradeTextSize = "12";
			MainViewModel.Instance.ScenarioTradeTextHeight = "18";
		}
		if (FatControler.ukrainian)
		{
			MainViewModel.Instance.ScenarioTradeTextSize = "12";
			MainViewModel.Instance.ScenarioTradeTextHeight = "20";
		}
		if (FatControler.czech)
		{
			MainViewModel.Instance.ScenarioTradeTextSize = "12";
			MainViewModel.Instance.ScenarioTradeTextHeight = "20";
		}
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Scenario.xaml");
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

	public void StartEntryAnim()
	{
		MainViewModel.Instance.ShowingScenario = true;
		MainViewModel.Instance.HUD_Markers_Vis = false;
		MainViewModel.Instance.HUDScenario.IsEnabled = true;
		MainViewModel.Instance.HUDScenarioPopup.IsEnabled = true;
		RefViewRoot.Visibility = Visibility.Visible;
		((Storyboard)base.Resources["Intro"]).Begin(this);
		MainViewModel.Instance.HUDScenarioPopup.StartEntryAnim();
		MainViewModel.Instance.ScenarioEditorButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 90);
	}

	public void StartExitAnim()
	{
		MainViewModel.Instance.ShowingScenario = false;
		((Storyboard)base.Resources["Outtro"]).Begin(this);
		MainViewModel.Instance.HUDScenarioPopup.StartExitAnim();
		MainViewModel.Instance.ScenarioEditorButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_SCENARIO_EDITOR);
	}

	public void initScenarioControls()
	{
		int.Parse(MainViewModel.Instance.ScenarioStartingGoldText, Director.defaultCulture);
		RefStartingGoods.Value = MainViewModel.Instance.initSliderScenarioStartingGoods();
		FatControler.instance.InitScenarioEditorValues();
		RefStartingPop.Value = int.Parse(MainViewModel.Instance.ScenarioStartingPopText, Director.defaultCulture);
		int currentValue = int.Parse(MainViewModel.Instance.ScenarioStartingSpecialGoldText, Director.defaultCulture);
		int maxValue = 10000;
		int freq = 1;
		currentValue = getLogSliderValue(currentValue, 1, ref maxValue, ref freq);
		RefStartingSpecialGold.Value = currentValue;
		if (!GameData.Instance.multiplayerMap)
		{
			changeScenarioView(Enums.ScenarioViews.Main);
		}
		else
		{
			changeScenarioView(Enums.ScenarioViews.EditMessage);
		}
		RefFasterGoodsCheck.IsChecked = GameData.Instance.scenarioOverview.fast_goods_feedin > 0;
		SetupScenarioActionsList();
	}

	public void changeScenarioView(Enums.ScenarioViews newView, bool fromButton = true)
	{
		RefViewMain.Visibility = Visibility.Hidden;
		RefViewStartingGoods.Visibility = Visibility.Hidden;
		RefViewTradedGoods.Visibility = Visibility.Hidden;
		RefViewBuildingAvailibity.Visibility = Visibility.Hidden;
		RefViewInvasions.Visibility = Visibility.Hidden;
		MainViewModel.Instance.InvasionFromVis = false;
		RefViewEvents.Visibility = Visibility.Hidden;
		RefViewEventsConditions.Visibility = Visibility.Hidden;
		RefViewEventsActions.Visibility = Visibility.Hidden;
		RefViewEditMessage.Visibility = Visibility.Hidden;
		RefScenarioViewEditTeams.Visibility = Visibility.Hidden;
		RefScenarioViewAdjustDates.Visibility = Visibility.Hidden;
		RefLeftPanel.Visibility = Visibility.Visible;
		MainViewModel.Instance.ScenarioBuildingTogglesVis = false;
		string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_SCENARIO_EDITOR);
		switch (newView)
		{
		case Enums.ScenarioViews.Main:
			RefreshScenarioActions();
			RefScenarioActionList.SelectedItem = null;
			ResetListViewPosition(RefScenarioActionList);
			RefViewMain.Visibility = Visibility.Visible;
			break;
		case Enums.ScenarioViews.StartingGoods:
			RefStartingGoodsGoldDefault.IsChecked = true;
			MainViewModel.Instance.ButtonScenarioStartingGoodSelect("15");
			RefViewStartingGoods.Visibility = Visibility.Visible;
			break;
		case Enums.ScenarioViews.TradedGoods:
			RefViewTradedGoods.Visibility = Visibility.Visible;
			break;
		case Enums.ScenarioViews.BuildingAvailibilty:
			ResetListViewPosition(RefScenarioBuildingList);
			RefViewBuildingAvailibity.Visibility = Visibility.Visible;
			MainViewModel.Instance.ScenarioBuildingTogglesVis = true;
			break;
		case Enums.ScenarioViews.Invasions:
			text = text + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_INVASION);
			if (fromButton)
			{
				NewInvasion();
			}
			RefInvasionSizeArcherDefault.IsChecked = true;
			MainViewModel.Instance.ButtonSelectInvasionSize("0");
			RefLeftPanel.Visibility = Visibility.Hidden;
			RefViewInvasions.Visibility = Visibility.Visible;
			break;
		case Enums.ScenarioViews.Events:
			text = text + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_EVENT);
			if (fromButton)
			{
				NewEvent();
			}
			RefLeftPanel.Visibility = Visibility.Hidden;
			RefViewEvents.Visibility = Visibility.Visible;
			break;
		case Enums.ScenarioViews.EventsConditions:
			text = text + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_EVENT_CONDITIONS);
			RefLeftPanel.Visibility = Visibility.Hidden;
			RefViewEventsConditions.Visibility = Visibility.Visible;
			PopulateEventConditions();
			break;
		case Enums.ScenarioViews.EventsActions:
			text = text + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_EVENT_ACTIONS);
			RefLeftPanel.Visibility = Visibility.Hidden;
			RefViewEventsActions.Visibility = Visibility.Visible;
			PopulateEventActions();
			break;
		case Enums.ScenarioViews.EditMessage:
			RefViewEditMessage.Visibility = Visibility.Visible;
			MainViewModel.Instance.ScenarioEditMessageText = GameData.Instance.utf8MissionText;
			if (GameData.Instance.showAlternateMissionTextForBriefing)
			{
				RefEditMessage.Height = 200f;
				MainViewModel.Instance.ScenarioMessageAltTextIVisibilityBool = true;
				MainViewModel.Instance.ScenarioAltANSIMessage = GameData.Instance.ansiMissionText;
				MainViewModel.Instance.ScenarioAltUNICODEMessage = GameData.Instance.unicodeMissionText;
				GameData.Instance.showAlternateMissionTextForBriefing = false;
				GameData.Instance.ansiMissionText = "";
				GameData.Instance.unicodeMissionText = "";
			}
			else
			{
				RefEditMessage.Height = 346f;
				MainViewModel.Instance.ScenarioMessageAltTextIVisibilityBool = false;
			}
			RefEditMessage.Focus();
			break;
		case Enums.ScenarioViews.EditTeams:
			RefScenarioViewEditTeams.Visibility = Visibility.Visible;
			if (GameData.Instance.lastGameState != null)
			{
				if (GameData.Instance.lastGameState.starting_teams[1] == 0)
				{
					EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, 1, 1);
					EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, 2, 9);
					EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, 3, 9);
					EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, 4, 9);
					EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, 5, 9);
					EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, 6, 6);
					EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, 7, 7);
					EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, 8, 8);
					GameData.Instance.lastGameState.starting_teams[1] = 1;
					GameData.Instance.lastGameState.starting_teams[2] = 9;
					GameData.Instance.lastGameState.starting_teams[3] = 9;
					GameData.Instance.lastGameState.starting_teams[4] = 9;
					GameData.Instance.lastGameState.starting_teams[5] = 9;
					GameData.Instance.lastGameState.starting_teams[6] = 6;
					GameData.Instance.lastGameState.starting_teams[7] = 7;
					GameData.Instance.lastGameState.starting_teams[8] = 8;
				}
				RefSliderEditTeam1.Value = (int)GameData.Instance.lastGameState.starting_teams[1];
				RefSliderEditTeam2.Value = (int)GameData.Instance.lastGameState.starting_teams[2];
				RefSliderEditTeam3.Value = (int)GameData.Instance.lastGameState.starting_teams[3];
				RefSliderEditTeam4.Value = (int)GameData.Instance.lastGameState.starting_teams[4];
				RefSliderEditTeam5.Value = (int)GameData.Instance.lastGameState.starting_teams[5];
				RefSliderEditTeam6.Value = (int)GameData.Instance.lastGameState.starting_teams[6];
				RefSliderEditTeam7.Value = (int)GameData.Instance.lastGameState.starting_teams[7];
				RefSliderEditTeam8.Value = (int)GameData.Instance.lastGameState.starting_teams[8];
				for (int i = 1; i < 9; i++)
				{
					MainViewModel.Instance.ScenarioEditTeams[i] = GameData.Instance.lastGameState.starting_teams[i].ToString();
				}
			}
			break;
		case Enums.ScenarioViews.AdjustDates:
			RefScenarioViewAdjustDates.Visibility = Visibility.Visible;
			MainViewModel.Instance.ScenarioAdjustStartingYearText = MainViewModel.Instance.ScenarioStartingYearText;
			newStartingMonthValue = GameData.Instance.scenarioOverview.startMonth;
			MainViewModel.Instance.ScenarioAdjustStartingMonthText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, newStartingMonthValue);
			break;
		}
		MainViewModel.Instance.ScenarioEditorMode = newView;
		RefHeading.HeadingText = text;
	}

	private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
	{
		Regex regex = new Regex("[^0-9]+");
		e.Handled = regex.IsMatch(e.Text);
	}

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	private void EditMessageTextChanged(object sender, RoutedEventArgs e)
	{
		string text = RefEditMessage.Text;
		GameData.Instance.utf8MissionText = text;
		EngineInterface.SetUTF8MissionText(text);
	}

	private void ScenarioPopularitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int newStartingPop = (int)RefStartingPop.Value;
		MainViewModel.Instance.SliderScenarioStartPop(newStartingPop);
	}

	private void ScenarioSpecialGoldSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int sliderPos = (int)RefStartingSpecialGold.Value;
		sliderPos = getLogSliderDislayValue(sliderPos, 10000);
		MainViewModel.Instance.SliderScenarioStartSpecialGold(sliderPos);
	}

	private void ScenarioStartingGoodsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int newStartingAmount = (int)RefStartingGoods.Value;
		MainViewModel.Instance.SliderScenarioStartingGoods(newStartingAmount);
	}

	private void RefreshScenarioActions()
	{
		EngineInterface.ScenarioOverview scenarioOverview = GameData.Instance.scenarioOverview;
		List<ScenarioEditorRow> list = new List<ScenarioEditorRow>();
		for (int i = 0; i < scenarioOverview.entries.Count; i++)
		{
			string date = "";
			string body = "";
			string repeat = "";
			int entryType = 0;
			GameData.Instance.getScenarioEntryOverviewText(i, ref date, ref body, ref repeat, ref entryType);
			list.Add(new ScenarioEditorRow(this)
			{
				Text1 = date,
				Text2 = body,
				Text3 = repeat,
				DataValue = i.ToString()
			});
		}
		RefScenarioActionList.ItemsSource = list;
		ResetListViewPosition(RefScenarioActionList);
	}

	public void ApplyAltMissionText(bool ansi)
	{
		if (ansi)
		{
			MainViewModel.Instance.ScenarioEditMessageText = MainViewModel.Instance.ScenarioAltANSIMessage;
		}
		else
		{
			MainViewModel.Instance.ScenarioEditMessageText = MainViewModel.Instance.ScenarioAltUNICODEMessage;
		}
		RefEditMessage.Height = 346f;
		MainViewModel.Instance.ScenarioMessageAltTextIVisibilityBool = false;
	}

	public void SetupScenarioActionsList()
	{
		BuildingItems.Clear();
		BuildingItemsDict.Clear();
		RefreshScenarioActions();
		EngineInterface.ScenarioOverview scenarioOverview = GameData.Instance.scenarioOverview;
		barracksItemsShowing = false;
		mercPostItemsShowing = false;
		bedouinItemsShowing = false;
		for (int i = 0; i < GameData.buildingAvailbleOrder.Length; i++)
		{
			int num = GameData.buildingAvailbleOrder[i];
			if (num == 27 || num == 33 || num == 35)
			{
				continue;
			}
			string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, GameData.buildingAvailbleOrder[i]) + " ";
			ScenarioEditorRow scenarioEditorRow = new ScenarioEditorRow(this);
			scenarioEditorRow.DataValue = i.ToString();
			if (scenarioOverview.scenario_buildings_available[i] > 0)
			{
				scenarioEditorRow.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
				scenarioEditorRow.Text1HL = text;
				scenarioEditorRow.Text1 = "";
			}
			else
			{
				scenarioEditorRow.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
				scenarioEditorRow.Text1 = text;
				scenarioEditorRow.Text1HL = "";
			}
			BuildingItems.Add(scenarioEditorRow);
			BuildingItemsDict[i] = scenarioEditorRow;
			if (GameData.buildingAvailbleOrder[i] == 9)
			{
				barracksStoneRow = i;
				if (scenarioOverview.scenario_buildings_available[i] <= 0)
				{
					continue;
				}
				barracksItemsShowing = true;
				for (int j = 0; j < 7; j++)
				{
					Enums.eChimps index = GameData.scenarioBarracksTroopsAvailableTypes[j];
					string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, (int)index);
					ScenarioEditorRow scenarioEditorRow2 = new ScenarioEditorRow(this);
					scenarioEditorRow2.DataValue = (-1 - j).ToString();
					if (GameData.Instance.scenarioOverview.sa_troop_availability[j] > 0)
					{
						scenarioEditorRow2.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow2.Text3HL = text2;
						scenarioEditorRow2.Text3 = "";
					}
					else
					{
						scenarioEditorRow2.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow2.Text3 = text2;
						scenarioEditorRow2.Text3HL = "";
					}
					BuildingItems.Add(scenarioEditorRow2);
					BuildingItemsDict[-1 - j] = scenarioEditorRow2;
				}
			}
			else if (GameData.buildingAvailbleOrder[i] == 10)
			{
				barracksWoodRow = i;
				if (scenarioOverview.scenario_buildings_available[i] <= 0)
				{
					continue;
				}
				mercPostItemsShowing = true;
				for (int k = 0; k < 7; k++)
				{
					Enums.eChimps index2 = GameData.scenarioMercPostTroopsAvailableTypes[k];
					string text3 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, (int)index2);
					ScenarioEditorRow scenarioEditorRow3 = new ScenarioEditorRow(this);
					scenarioEditorRow3.DataValue = (-100 - k).ToString();
					if (GameData.Instance.scenarioOverview.sa_merc_availability[k] > 0)
					{
						scenarioEditorRow3.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow3.Text3HL = text3;
						scenarioEditorRow3.Text3 = "";
					}
					else
					{
						scenarioEditorRow3.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow3.Text3 = text3;
						scenarioEditorRow3.Text3HL = "";
					}
					BuildingItems.Add(scenarioEditorRow3);
					BuildingItemsDict[-100 - k] = scenarioEditorRow3;
				}
			}
			else if (GameData.buildingAvailbleOrder[i] == 348)
			{
				barracksBedouinRow = i;
				if (scenarioOverview.scenario_buildings_available[i] <= 0)
				{
					continue;
				}
				bedouinItemsShowing = true;
				for (int l = 0; l < 8; l++)
				{
					Enums.eChimps index3 = GameData.scenarioBedouinTroopsAvailableTypes[l];
					string text4 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, (int)index3);
					ScenarioEditorRow scenarioEditorRow4 = new ScenarioEditorRow(this);
					scenarioEditorRow4.DataValue = (-120 - l).ToString();
					if (GameData.Instance.scenarioOverview.sa_bed_availability[l] > 0)
					{
						scenarioEditorRow4.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow4.Text3HL = text4;
						scenarioEditorRow4.Text3 = "";
					}
					else
					{
						scenarioEditorRow4.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow4.Text3 = text4;
						scenarioEditorRow4.Text3HL = "";
					}
					BuildingItems.Add(scenarioEditorRow4);
					BuildingItemsDict[-120 - l] = scenarioEditorRow4;
				}
			}
			else if (GameData.buildingAvailbleOrder[i] == 54)
			{
				if (scenarioOverview.scenario_buildings_available[i] > 0)
				{
					string text5 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 17);
					ScenarioEditorRow scenarioEditorRow5 = new ScenarioEditorRow(this);
					scenarioEditorRow5.DataValue = "-50";
					if (scenarioOverview.sa_fletcher_bow > 0)
					{
						scenarioEditorRow5.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow5.Text3HL = text5;
						scenarioEditorRow5.Text3 = "";
					}
					else
					{
						scenarioEditorRow5.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow5.Text3 = text5;
						scenarioEditorRow5.Text3HL = "";
					}
					BuildingItems.Add(scenarioEditorRow5);
					BuildingItemsDict[-50] = scenarioEditorRow5;
					string text6 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 18);
					ScenarioEditorRow scenarioEditorRow6 = new ScenarioEditorRow(this);
					scenarioEditorRow6.DataValue = "-20";
					if (scenarioOverview.sa_fletcher_xbow > 0)
					{
						scenarioEditorRow6.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow6.Text3HL = text6;
						scenarioEditorRow6.Text3 = "";
					}
					else
					{
						scenarioEditorRow6.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow6.Text3 = text6;
						scenarioEditorRow6.Text3HL = "";
					}
					BuildingItems.Add(scenarioEditorRow6);
					BuildingItemsDict[-20] = scenarioEditorRow6;
				}
			}
			else if (GameData.buildingAvailbleOrder[i] == 55)
			{
				if (scenarioOverview.scenario_buildings_available[i] > 0)
				{
					string text7 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 19);
					ScenarioEditorRow scenarioEditorRow7 = new ScenarioEditorRow(this);
					scenarioEditorRow7.DataValue = "-60";
					if (scenarioOverview.sa_poleturner_spear > 0)
					{
						scenarioEditorRow7.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow7.Text3HL = text7;
						scenarioEditorRow7.Text3 = "";
					}
					else
					{
						scenarioEditorRow7.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow7.Text3 = text7;
						scenarioEditorRow7.Text3HL = "";
					}
					BuildingItems.Add(scenarioEditorRow7);
					BuildingItemsDict[-60] = scenarioEditorRow7;
					string text8 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 20);
					ScenarioEditorRow scenarioEditorRow8 = new ScenarioEditorRow(this);
					scenarioEditorRow8.DataValue = "-30";
					if (scenarioOverview.sa_poleturner_pike > 0)
					{
						scenarioEditorRow8.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow8.Text3HL = text8;
						scenarioEditorRow8.Text3 = "";
					}
					else
					{
						scenarioEditorRow8.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow8.Text3 = text8;
						scenarioEditorRow8.Text3HL = "";
					}
					BuildingItems.Add(scenarioEditorRow8);
					BuildingItemsDict[-30] = scenarioEditorRow8;
				}
			}
			else if (GameData.buildingAvailbleOrder[i] == 51 && scenarioOverview.scenario_buildings_available[i] > 0)
			{
				string text9 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 21);
				ScenarioEditorRow scenarioEditorRow9 = new ScenarioEditorRow(this);
				scenarioEditorRow9.DataValue = "-70";
				if (scenarioOverview.sa_blacksmith_mace > 0)
				{
					scenarioEditorRow9.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
					scenarioEditorRow9.Text3HL = text9;
					scenarioEditorRow9.Text3 = "";
				}
				else
				{
					scenarioEditorRow9.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
					scenarioEditorRow9.Text3 = text9;
					scenarioEditorRow9.Text3HL = "";
				}
				BuildingItems.Add(scenarioEditorRow9);
				BuildingItemsDict[-70] = scenarioEditorRow9;
				string text10 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 22);
				ScenarioEditorRow scenarioEditorRow10 = new ScenarioEditorRow(this);
				scenarioEditorRow10.DataValue = "-40";
				if (scenarioOverview.sa_blacksmith_sword > 0)
				{
					scenarioEditorRow10.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
					scenarioEditorRow10.Text3HL = text10;
					scenarioEditorRow10.Text3 = "";
				}
				else
				{
					scenarioEditorRow10.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
					scenarioEditorRow10.Text3 = text10;
					scenarioEditorRow10.Text3HL = "";
				}
				BuildingItems.Add(scenarioEditorRow10);
				BuildingItemsDict[-40] = scenarioEditorRow10;
			}
		}
		RefScenarioBuildingList.ItemsSource = BuildingItems;
		ResetListViewPosition(RefScenarioBuildingList);
	}

	public void ButtonScenarioBuildingAvailToggle(object parameter)
	{
		EngineInterface.ScenarioOverview scenarioOverview = GameData.Instance.scenarioOverview;
		int num = Convert.ToInt32(parameter as string);
		if (num >= 1000)
		{
			int num2 = 0;
			if (num == 1000)
			{
				num2 = 1;
			}
			for (int i = 0; i < GameData.buildingAvailbleOrder.Length; i++)
			{
				scenarioOverview.scenario_buildings_available[i] = num2;
				EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_BuildingAvailable, i, scenarioOverview.scenario_buildings_available[i]);
			}
			scenarioOverview.sa_poleturner_pike = num2;
			scenarioOverview.sa_fletcher_bow = num2;
			scenarioOverview.sa_blacksmith_mace = num2;
			scenarioOverview.sa_poleturner_spear = num2;
			scenarioOverview.sa_fletcher_xbow = num2;
			scenarioOverview.sa_blacksmith_sword = num2;
			for (int j = 0; j < 7; j++)
			{
				scenarioOverview.sa_troop_availability[j] = num2;
				EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_TroopAvailable, j, scenarioOverview.sa_troop_availability[j]);
				scenarioOverview.sa_merc_availability[j] = num2;
				EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_MercTroopAvailable, j, scenarioOverview.sa_merc_availability[j]);
			}
			for (int k = 0; k < 8; k++)
			{
				scenarioOverview.sa_bed_availability[k] = num2;
				EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_BedouinTroopAvailable, k, scenarioOverview.sa_bed_availability[k]);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_PikeAvailable, 0, scenarioOverview.sa_poleturner_pike);
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_XbowAvailable, 0, scenarioOverview.sa_fletcher_xbow);
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_SwordAvailable, 0, scenarioOverview.sa_blacksmith_sword);
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_SpearAvailable, 0, scenarioOverview.sa_poleturner_spear);
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_bowAvailable, 0, scenarioOverview.sa_fletcher_bow);
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_MaceAvailable, 0, scenarioOverview.sa_blacksmith_mace);
			SetupScenarioActionsList();
			return;
		}
		if (num >= 0)
		{
			if (scenarioOverview.scenario_buildings_available[num] > 0)
			{
				scenarioOverview.scenario_buildings_available[num] = 0;
				SetBuildingAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.scenario_buildings_available[num] = 1;
				SetBuildingAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_BuildingAvailable, num, scenarioOverview.scenario_buildings_available[num]);
			if (GameData.buildingAvailbleOrder[num] == 9)
			{
				barracksStoneRow = findBuildingItemRow(num);
				bool flag = scenarioOverview.scenario_buildings_available[num] > 0;
				if (flag == barracksItemsShowing)
				{
					return;
				}
				barracksItemsShowing = flag;
				if (barracksItemsShowing)
				{
					for (int l = 0; l < 7; l++)
					{
						Enums.eChimps index = GameData.scenarioBarracksTroopsAvailableTypes[l];
						string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, (int)index) + " ";
						ScenarioEditorRow scenarioEditorRow = new ScenarioEditorRow(this);
						scenarioEditorRow.DataValue = (-1 - l).ToString();
						if (GameData.Instance.scenarioOverview.sa_troop_availability[l] > 0)
						{
							scenarioEditorRow.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
							scenarioEditorRow.Text3HL = text;
							scenarioEditorRow.Text3 = "";
						}
						else
						{
							scenarioEditorRow.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
							scenarioEditorRow.Text3 = text;
							scenarioEditorRow.Text3HL = "";
						}
						BuildingItems.Insert(barracksStoneRow + 1 + l, scenarioEditorRow);
						BuildingItemsDict[-1 - l] = scenarioEditorRow;
					}
				}
				else
				{
					for (int m = 0; m < 7; m++)
					{
						BuildingItems.RemoveAt(barracksStoneRow + 1);
					}
				}
			}
			else if (GameData.buildingAvailbleOrder[num] == 10)
			{
				barracksWoodRow = findBuildingItemRow(num);
				bool flag2 = scenarioOverview.scenario_buildings_available[num] > 0;
				if (flag2 == mercPostItemsShowing)
				{
					return;
				}
				mercPostItemsShowing = flag2;
				if (mercPostItemsShowing)
				{
					for (int n = 0; n < 7; n++)
					{
						Enums.eChimps index2 = GameData.scenarioMercPostTroopsAvailableTypes[n];
						string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, (int)index2) + " ";
						ScenarioEditorRow scenarioEditorRow2 = new ScenarioEditorRow(this);
						scenarioEditorRow2.DataValue = (-100 - n).ToString();
						if (GameData.Instance.scenarioOverview.sa_merc_availability[n] > 0)
						{
							scenarioEditorRow2.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
							scenarioEditorRow2.Text3HL = text2;
							scenarioEditorRow2.Text3 = "";
						}
						else
						{
							scenarioEditorRow2.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
							scenarioEditorRow2.Text3 = text2;
							scenarioEditorRow2.Text3HL = "";
						}
						BuildingItems.Insert(barracksWoodRow + 1 + n, scenarioEditorRow2);
						BuildingItemsDict[-100 - n] = scenarioEditorRow2;
					}
				}
				else
				{
					for (int num3 = 0; num3 < 7; num3++)
					{
						BuildingItems.RemoveAt(barracksWoodRow + 1);
					}
				}
			}
			else if (GameData.buildingAvailbleOrder[num] == 348)
			{
				barracksBedouinRow = findBuildingItemRow(num);
				bool flag3 = scenarioOverview.scenario_buildings_available[num] > 0;
				if (flag3 == bedouinItemsShowing)
				{
					return;
				}
				bedouinItemsShowing = flag3;
				if (bedouinItemsShowing)
				{
					for (int num4 = 0; num4 < 8; num4++)
					{
						Enums.eChimps index3 = GameData.scenarioBedouinTroopsAvailableTypes[num4];
						string text3 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, (int)index3) + " ";
						ScenarioEditorRow scenarioEditorRow3 = new ScenarioEditorRow(this);
						scenarioEditorRow3.DataValue = (-120 - num4).ToString();
						if (GameData.Instance.scenarioOverview.sa_bed_availability[num4] > 0)
						{
							scenarioEditorRow3.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
							scenarioEditorRow3.Text3HL = text3;
							scenarioEditorRow3.Text3 = "";
						}
						else
						{
							scenarioEditorRow3.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
							scenarioEditorRow3.Text3 = text3;
							scenarioEditorRow3.Text3HL = "";
						}
						BuildingItems.Insert(barracksBedouinRow + 1 + num4, scenarioEditorRow3);
						BuildingItemsDict[-120 - num4] = scenarioEditorRow3;
					}
				}
				else
				{
					for (int num5 = 0; num5 < 8; num5++)
					{
						BuildingItems.RemoveAt(barracksBedouinRow + 1);
					}
				}
			}
			else if (GameData.buildingAvailbleOrder[num] == 54)
			{
				int num6 = findBuildingItemRow(num);
				if (scenarioOverview.scenario_buildings_available[num] > 0)
				{
					string text4 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 17);
					ScenarioEditorRow scenarioEditorRow4 = new ScenarioEditorRow(this);
					scenarioEditorRow4.DataValue = "-50";
					if (scenarioOverview.sa_fletcher_bow > 0)
					{
						scenarioEditorRow4.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow4.Text3HL = text4;
						scenarioEditorRow4.Text3 = "";
					}
					else
					{
						scenarioEditorRow4.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow4.Text3 = text4;
						scenarioEditorRow4.Text3HL = "";
					}
					BuildingItems.Insert(num6 + 1, scenarioEditorRow4);
					BuildingItemsDict[-50] = scenarioEditorRow4;
					string text5 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 18);
					ScenarioEditorRow scenarioEditorRow5 = new ScenarioEditorRow(this);
					scenarioEditorRow5.DataValue = "-20";
					if (scenarioOverview.sa_fletcher_xbow > 0)
					{
						scenarioEditorRow5.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow5.Text3HL = text5;
						scenarioEditorRow5.Text3 = "";
					}
					else
					{
						scenarioEditorRow5.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow5.Text3 = text5;
						scenarioEditorRow5.Text3HL = "";
					}
					BuildingItems.Insert(num6 + 2, scenarioEditorRow5);
					BuildingItemsDict[-20] = scenarioEditorRow5;
				}
				else
				{
					BuildingItems.RemoveAt(num6 + 1);
					BuildingItems.RemoveAt(num6 + 1);
				}
			}
			else if (GameData.buildingAvailbleOrder[num] == 55)
			{
				int num7 = findBuildingItemRow(num);
				if (scenarioOverview.scenario_buildings_available[num] > 0)
				{
					string text6 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 19);
					ScenarioEditorRow scenarioEditorRow6 = new ScenarioEditorRow(this);
					scenarioEditorRow6.DataValue = "-60";
					if (scenarioOverview.sa_poleturner_spear > 0)
					{
						scenarioEditorRow6.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow6.Text3HL = text6;
						scenarioEditorRow6.Text3 = "";
					}
					else
					{
						scenarioEditorRow6.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow6.Text3 = text6;
						scenarioEditorRow6.Text3HL = "";
					}
					BuildingItems.Insert(num7 + 1, scenarioEditorRow6);
					BuildingItemsDict[-60] = scenarioEditorRow6;
					string text7 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 21);
					ScenarioEditorRow scenarioEditorRow7 = new ScenarioEditorRow(this);
					scenarioEditorRow7.DataValue = "-30";
					if (scenarioOverview.sa_poleturner_pike > 0)
					{
						scenarioEditorRow7.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow7.Text3HL = text7;
						scenarioEditorRow7.Text3 = "";
					}
					else
					{
						scenarioEditorRow7.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow7.Text3 = text7;
						scenarioEditorRow7.Text3HL = "";
					}
					BuildingItems.Insert(num7 + 2, scenarioEditorRow7);
					BuildingItemsDict[-30] = scenarioEditorRow7;
				}
				else
				{
					BuildingItems.RemoveAt(num7 + 1);
					BuildingItems.RemoveAt(num7 + 1);
				}
			}
			else
			{
				if (GameData.buildingAvailbleOrder[num] != 51)
				{
					return;
				}
				int num8 = findBuildingItemRow(num);
				if (scenarioOverview.scenario_buildings_available[num] > 0)
				{
					string text8 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 21);
					ScenarioEditorRow scenarioEditorRow8 = new ScenarioEditorRow(this);
					scenarioEditorRow8.DataValue = "-70";
					if (scenarioOverview.sa_blacksmith_mace > 0)
					{
						scenarioEditorRow8.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow8.Text3HL = text8;
						scenarioEditorRow8.Text3 = "";
					}
					else
					{
						scenarioEditorRow8.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow8.Text3 = text8;
						scenarioEditorRow8.Text3HL = "";
					}
					BuildingItems.Insert(num8 + 1, scenarioEditorRow8);
					BuildingItemsDict[-70] = scenarioEditorRow8;
					string text9 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 22);
					ScenarioEditorRow scenarioEditorRow9 = new ScenarioEditorRow(this);
					scenarioEditorRow9.DataValue = "-40";
					if (scenarioOverview.sa_blacksmith_sword > 0)
					{
						scenarioEditorRow9.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
						scenarioEditorRow9.Text3HL = text9;
						scenarioEditorRow9.Text3 = "";
					}
					else
					{
						scenarioEditorRow9.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
						scenarioEditorRow9.Text3 = text9;
						scenarioEditorRow9.Text3HL = "";
					}
					BuildingItems.Insert(num8 + 2, scenarioEditorRow9);
					BuildingItemsDict[-40] = scenarioEditorRow9;
				}
				else
				{
					BuildingItems.RemoveAt(num8 + 1);
					BuildingItems.RemoveAt(num8 + 1);
				}
			}
			return;
		}
		if (num >= -10)
		{
			int num9 = -1 - num;
			if (scenarioOverview.sa_troop_availability[num9] > 0)
			{
				scenarioOverview.sa_troop_availability[num9] = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_troop_availability[num9] = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_TroopAvailable, num9, scenarioOverview.sa_troop_availability[num9]);
			return;
		}
		switch (num)
		{
		case -20:
			if (scenarioOverview.sa_fletcher_xbow > 0)
			{
				scenarioOverview.sa_fletcher_xbow = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_fletcher_xbow = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_XbowAvailable, 0, scenarioOverview.sa_fletcher_xbow);
			return;
		case -30:
			if (scenarioOverview.sa_poleturner_pike > 0)
			{
				scenarioOverview.sa_poleturner_pike = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_poleturner_pike = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_PikeAvailable, 0, scenarioOverview.sa_poleturner_pike);
			return;
		case -40:
			if (scenarioOverview.sa_blacksmith_sword > 0)
			{
				scenarioOverview.sa_blacksmith_sword = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_blacksmith_sword = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_SwordAvailable, 0, scenarioOverview.sa_blacksmith_sword);
			return;
		case -50:
			if (scenarioOverview.sa_fletcher_bow > 0)
			{
				scenarioOverview.sa_fletcher_bow = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_fletcher_bow = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_bowAvailable, 0, scenarioOverview.sa_fletcher_bow);
			return;
		case -60:
			if (scenarioOverview.sa_poleturner_spear > 0)
			{
				scenarioOverview.sa_poleturner_spear = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_poleturner_spear = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_SpearAvailable, 0, scenarioOverview.sa_poleturner_spear);
			return;
		case -70:
			if (scenarioOverview.sa_blacksmith_mace > 0)
			{
				scenarioOverview.sa_blacksmith_mace = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_blacksmith_mace = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_MaceAvailable, 0, scenarioOverview.sa_blacksmith_mace);
			return;
		case -110:
		case -109:
		case -108:
		case -107:
		case -106:
		case -105:
		case -104:
		case -103:
		case -102:
		case -101:
		case -100:
		{
			int num10 = -100 - num;
			if (scenarioOverview.sa_merc_availability[num10] > 0)
			{
				scenarioOverview.sa_merc_availability[num10] = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_merc_availability[num10] = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_MercTroopAvailable, num10, scenarioOverview.sa_merc_availability[num10]);
			return;
		}
		}
		if (num >= -130 && num <= -120)
		{
			int num11 = -120 - num;
			if (scenarioOverview.sa_bed_availability[num11] > 0)
			{
				scenarioOverview.sa_bed_availability[num11] = 0;
				SetTroopAvailRow(num, state: false);
			}
			else
			{
				scenarioOverview.sa_bed_availability[num11] = 1;
				SetTroopAvailRow(num, state: true);
			}
			EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_BedouinTroopAvailable, num11, scenarioOverview.sa_bed_availability[num11]);
		}
	}

	public void SetBuildingAvailRow(int row, bool state)
	{
		if (row >= BuildingItems.Count || !BuildingItemsDict.TryGetValue(row, out var value))
		{
			return;
		}
		if (state)
		{
			value.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
			if (value.Text1HL == "")
			{
				value.Text1HL = value.Text1;
				value.Text1 = "";
			}
		}
		else
		{
			value.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
			if (value.Text1 == "")
			{
				value.Text1 = value.Text1HL;
				value.Text1HL = "";
			}
		}
	}

	public void SetTroopAvailRow(int row, bool state)
	{
		if (!BuildingItemsDict.TryGetValue(row, out var value))
		{
			return;
		}
		if (state)
		{
			value.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
			if (value.Text3HL == "")
			{
				value.Text3HL = value.Text3;
				value.Text3 = "";
			}
		}
		else
		{
			value.Text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
			if (value.Text3 == "")
			{
				value.Text3 = value.Text3HL;
				value.Text3HL = "";
			}
		}
	}

	private int findBuildingItemRow(int ident)
	{
		string text = ident.ToString();
		for (int i = 0; i < BuildingItems.Count; i++)
		{
			if (BuildingItems[i].DataValue == text)
			{
				return i;
			}
		}
		return -1;
	}

	private void ResetListViewPosition(ListView lv)
	{
		if (lv.Items.Count > 0)
		{
			lv.ScrollIntoView(lv.Items[0]);
		}
	}

	public bool isEventPanelOpen()
	{
		return scenarioCurrentEvent != null;
	}

	private void SelectActionRow(int row)
	{
		string date = "";
		string body = "";
		string repeat = "";
		int entryType = 0;
		GameData.Instance.getScenarioEntryOverviewText(row, ref date, ref body, ref repeat, ref entryType);
		switch (entryType)
		{
		case 3:
			scenarioCurrentEvent = EngineInterface.GetScenarioEvent(row);
			scenarioCurrentInvasion = null;
			scenarioCurrentLine = row;
			PopulateEvent();
			MainViewModel.Instance.ButtonScenarioView("7", fromButton: false);
			break;
		case 1:
			scenarioCurrentInvasion = EngineInterface.GetScenarioInvasion(row);
			scenarioCurrentEvent = null;
			scenarioCurrentLine = row;
			MainViewModel.Instance.ButtonScenarioView("5", fromButton: false);
			PopulateInvasion();
			break;
		}
	}

	private void NewInvasion()
	{
		scenarioCurrentEvent = null;
		scenarioCurrentInvasion = EngineInterface.CreateNewScenarioInvasion(ref scenarioCurrentLine);
		PopulateInvasion();
	}

	private void NewEvent()
	{
		scenarioCurrentInvasion = null;
		scenarioCurrentEvent = EngineInterface.CreateNewScenarioEvent(ref scenarioCurrentLine);
		PopulateEvent();
	}

	public void PopulateInvasion(bool fromUpdate = false)
	{
		if (scenarioCurrentInvasion == null)
		{
			return;
		}
		if (!fromUpdate)
		{
			MainViewModel.Instance.ScenarioInvasionYearText = scenarioCurrentInvasion.year.ToString();
			UpdateInvasionFromTitle();
			RefInvasionRepeatSlider.Value = scenarioCurrentInvasion.repeat;
			for (int i = 0; i < 11; i++)
			{
				MainViewModel.Instance.InvasionSpawnMarkersHighlight[i] = false;
			}
			if (scenarioCurrentInvasion.markerID < 0)
			{
				scenarioCurrentInvasion.markerID = 0;
			}
			else if (scenarioCurrentInvasion.markerID > 10)
			{
				scenarioCurrentInvasion.markerID = 10;
			}
			MainViewModel.Instance.InvasionSpawnMarkersHighlight[scenarioCurrentInvasion.markerID] = true;
		}
		MainViewModel.Instance.ScenarioInvasionMonthText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, scenarioCurrentInvasion.month);
		int num = int.Parse(MainViewModel.Instance.ScenarioInvasionYearText, Director.defaultCulture);
		if (num != scenarioCurrentInvasion.year)
		{
			scenarioCurrentInvasion.year = num;
		}
		int num2 = 0;
		for (int j = 0; j < scenarioCurrentInvasion._size.Length; j++)
		{
			num2 += scenarioCurrentInvasion._size[j];
		}
		scenarioCurrentInvasion.total = num2;
		MainViewModel.Instance.ScenarioInvasionTotalTroopsText = num2.ToString();
		MainViewModel.Instance.ScenarioInvasionRepeatText = scenarioCurrentInvasion.repeat.ToString();
		int invasionSizeTroopTypeFromIndex = GameData.getInvasionSizeTroopTypeFromIndex(MainViewModel.Instance.ScenarioInvasionSizeType);
		MainViewModel.Instance.ScenarioInvasionSizeText = scenarioCurrentInvasion._size[MainViewModel.Instance.ScenarioInvasionSizeType] + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, invasionSizeTroopTypeFromIndex);
		for (int k = 0; k < 32; k++)
		{
			if (scenarioCurrentInvasion._size[k] > 0)
			{
				MainViewModel.Instance.InvasionSize[k] = scenarioCurrentInvasion._size[k].ToString();
			}
			else
			{
				MainViewModel.Instance.InvasionSize[k] = "";
			}
		}
	}

	public static int getStartingTeamForInvasions(int player)
	{
		if (GameData.Instance.lastGameState.starting_teams[player] == 0)
		{
			if (player == 1)
			{
				return 1;
			}
			return 9;
		}
		return GameData.Instance.lastGameState.starting_teams[player];
	}

	private void UpdateInvasionFromTitle()
	{
		bool flag = true;
		bool flag2 = true;
		int num = 2;
		if (scenarioCurrentInvasion.from == 1)
		{
			num = 3;
			flag2 = false;
		}
		else if (scenarioCurrentInvasion.from > 2)
		{
			flag2 = scenarioCurrentInvasion.from < 200;
			num = scenarioCurrentInvasion.from % 10;
			if (getStartingTeamForInvasions(num) == 1)
			{
				flag = false;
			}
		}
		string text;
		if (flag)
		{
			text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_INVASION) + " : ";
			text = ((!flag2) ? (text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, 224)) : (text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, 223)));
		}
		else
		{
			text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 26);
		}
		MainViewModel.Instance.InvasionFromDescription = text;
		switch (num)
		{
		case 1:
			MainViewModel.Instance.InvasionFromShield = MainViewModel.Instance.GameSprites[110];
			break;
		case 2:
			MainViewModel.Instance.InvasionFromShield = MainViewModel.Instance.GameSprites[108];
			break;
		case 3:
			MainViewModel.Instance.InvasionFromShield = MainViewModel.Instance.GameSprites[109];
			break;
		case 4:
			MainViewModel.Instance.InvasionFromShield = MainViewModel.Instance.GameSprites[107];
			break;
		case 5:
			MainViewModel.Instance.InvasionFromShield = MainViewModel.Instance.GameSprites[111];
			break;
		case 6:
			MainViewModel.Instance.InvasionFromShield = MainViewModel.Instance.GameSprites[112];
			break;
		case 7:
			MainViewModel.Instance.InvasionFromShield = MainViewModel.Instance.GameSprites[113];
			break;
		case 8:
			MainViewModel.Instance.InvasionFromShield = MainViewModel.Instance.GameSprites[114];
			break;
		}
	}

	public void PopulateEvent(bool fromUpdate = false)
	{
		if (scenarioCurrentEvent == null)
		{
			return;
		}
		if (!fromUpdate)
		{
			MainViewModel.Instance.ScenarioEventYearText = scenarioCurrentEvent.year.ToString();
			int num = 0;
			for (int i = 0; i < 40; i++)
			{
				if (scenarioCurrentEvent.event_value[i].onoff <= 0)
				{
					continue;
				}
				string value = "";
				int num2 = 106 + i;
				if (num2 > 125)
				{
					num2 += 64;
				}
				if (num2 == 111 || num2 == 112 || num2 == 113 || num2 == 123)
				{
					num2 = 113;
				}
				string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, num2);
				if (num2 == 114)
				{
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 8);
				}
				if (GameData.start_event_types[i] == 1)
				{
					text = text + " (" + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.start_event_goods_text[scenarioCurrentEvent.event_value[i].type]) + ")";
				}
				else if (GameData.start_event_types[i] == 2)
				{
					value = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.lord_killed_list[scenarioCurrentEvent.event_value[i].type]);
				}
				else if (GameData.start_event_types[i] == 3)
				{
					if (scenarioCurrentEvent.event_value[i].type < 0)
					{
						scenarioCurrentEvent.event_value[i].type = 0;
					}
					value = ((scenarioCurrentEvent.event_value[i].type == 31) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 166) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 132 + scenarioCurrentEvent.event_value[i].type));
				}
				if (GameData.start_event_max[i] != 0)
				{
					value = (scenarioCurrentEvent.event_value[i].value * GameData.start_event_multiplier[i]).ToString();
				}
				MainViewModel.Instance.EventTextList[num + 1] = text;
				MainViewModel.Instance.EventTextList[num + 11] = value;
				num++;
				if (num >= 8)
				{
					break;
				}
			}
			if (num < 8)
			{
				for (int j = num; j < 8; j++)
				{
					MainViewModel.Instance.EventTextList[j + 1] = "";
					MainViewModel.Instance.EventTextList[j + 11] = "";
				}
			}
			if (scenarioCurrentEvent.action >= 0)
			{
				int num2 = 128 + scenarioCurrentEvent.action;
				if (num2 > 155)
				{
					num2 += 21;
				}
				if (num2 == 148)
				{
					num2 = 146;
				}
				else
				{
					_ = 146;
				}
				string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, num2);
				if (scenarioCurrentEvent.action == 33)
				{
					text2 = ((scenarioCurrentEvent.action_data_marker != 0) ? (text2 + " : " + scenarioCurrentEvent.action_data_marker) : (text2 + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 14)));
				}
				else if (scenarioCurrentEvent.action == 11 || scenarioCurrentEvent.action == 17 || scenarioCurrentEvent.action == 4 || scenarioCurrentEvent.action == 18 || scenarioCurrentEvent.action == 20 || scenarioCurrentEvent.action == 30)
				{
					text2 = text2 + " : " + scenarioCurrentEvent.action_data;
				}
				else if (scenarioCurrentEvent.action == 32)
				{
					text2 = text2 + " : " + scenarioCurrentEvent.action_data_marker + " / " + getReinforcementsName(scenarioCurrentEvent.action_data_reinforcement);
				}
				else if (scenarioCurrentEvent.action == 31)
				{
					text2 = text2 + " : " + scenarioCurrentEvent.action_data_marker + "  " + getAllegianceTeamName(scenarioCurrentEvent.action_data_reinforcement);
				}
				MainViewModel.Instance.EventTextList[0] = text2;
			}
			else
			{
				MainViewModel.Instance.EventTextList[0] = "";
			}
		}
		MainViewModel.Instance.ScenarioEventMonthText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, scenarioCurrentEvent.month);
		int num3 = int.Parse(MainViewModel.Instance.ScenarioEventYearText, Director.defaultCulture);
		if (num3 != scenarioCurrentEvent.year)
		{
			scenarioCurrentEvent.year = num3;
		}
	}

	public void PopulateEventConditions(bool fromUpdate = false)
	{
		if (scenarioCurrentEvent == null)
		{
			return;
		}
		if (!fromUpdate)
		{
			MainViewModel.Instance.ScenarioEventConditionTitle = "";
			ObservableCollection<ScenarioEditorRow> observableCollection = new ObservableCollection<ScenarioEditorRow>();
			for (int i = 0; i < GameData.scenarioEventsOrder.Length; i++)
			{
				int num = GameData.scenarioEventsOrder[i];
				int num2 = num;
				if (num2 != 209)
				{
					if (num >= 190)
					{
						num -= 64;
					}
					num -= 106;
					ScenarioEditorRow scenarioEditorRow = new ScenarioEditorRow(this);
					scenarioEditorRow.DataValue = num.ToString();
					scenarioEditorRow.DataValue2 = num2;
					activeEventConditionRow = scenarioEditorRow;
					UpdateEventConditionRow();
					observableCollection.Add(scenarioEditorRow);
				}
			}
			activeEventConditionRow = null;
			RefScenarioEventConditionList.ItemsSource = observableCollection;
			MainViewModel.Instance.ConditionValueNameText = "";
			EventConditionItems = observableCollection;
			ManageEventConditionWinTimer();
			PopulateEventConditionLeftSide();
		}
		if (scenarioCurrentEvent.and_or > 0)
		{
			MainViewModel.Instance.ScenarionEventConditionAndOrText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ALL_OF_THESE);
		}
		else
		{
			MainViewModel.Instance.ScenarionEventConditionAndOrText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ANY_OF_THESE);
		}
	}

	private void ManageEventConditionWinTimer()
	{
		int num = 209;
		int num2 = num;
		if (num >= 190)
		{
			num -= 64;
		}
		num -= 106;
		bool flag = true;
		if (num2 == 209)
		{
			flag = false;
			if (scenarioCurrentEvent.event_value[1].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[4].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[5].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[6].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[7].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[17].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[20].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[21].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[22].onoff > 0)
			{
				flag = true;
			}
			if (scenarioCurrentEvent.event_value[23].onoff > 0)
			{
				flag = true;
			}
		}
		if (flag)
		{
			ScenarioEditorRow scenarioEditorRow = null;
			foreach (ScenarioEditorRow eventConditionItem in EventConditionItems)
			{
				if (eventConditionItem.DataValue2 == num2)
				{
					scenarioEditorRow = eventConditionItem;
					break;
				}
			}
			if (scenarioEditorRow == null)
			{
				scenarioEditorRow = new ScenarioEditorRow(this);
				EventConditionItems.Add(scenarioEditorRow);
			}
			scenarioEditorRow.DataValue = num.ToString();
			scenarioEditorRow.DataValue2 = num2;
			ScenarioEditorRow scenarioEditorRow2 = activeEventConditionRow;
			activeEventConditionRow = scenarioEditorRow;
			UpdateEventConditionRow();
			activeEventConditionRow = scenarioEditorRow2;
			return;
		}
		ScenarioEditorRow scenarioEditorRow3 = null;
		foreach (ScenarioEditorRow eventConditionItem2 in EventConditionItems)
		{
			if (eventConditionItem2.DataValue2 == num2)
			{
				scenarioEditorRow3 = eventConditionItem2;
				break;
			}
		}
		if (scenarioEditorRow3 != null)
		{
			EventConditionItems.Remove(scenarioEditorRow3);
		}
	}

	private void PopulateEventConditionLeftSide()
	{
		if (activeEventConditionRow == null)
		{
			MainViewModel.Instance.ScenarioEventConditionTitle = "";
			MainViewModel.Instance.ScenarionEventConditionToggleVisibleBool = false;
			MainViewModel.Instance.ScenarionEventConditionToggleColourVisibleBool = false;
			MainViewModel.Instance.ScenarioEventConditionValueVisibleBool = false;
			MainViewModel.Instance.ScenarionEventConditionOnOffVisibleBool = false;
			return;
		}
		int num = activeEventConditionRow.DataValue2;
		if (num == 111 || num == 112 || num == 113 || num == 123)
		{
			num = 113;
		}
		string scenarioEventConditionTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, num);
		if (num == 114)
		{
			scenarioEventConditionTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 8);
		}
		MainViewModel.Instance.ScenarioEventConditionTitle = scenarioEventConditionTitle;
		int num2 = int.Parse(activeEventConditionRow.DataValue, Director.defaultCulture);
		if (GameData.start_event_max[num2] != 0)
		{
			MainViewModel.Instance.ScenarioEventConditionValueVisibleBool = true;
			int maxValue = GameData.start_event_max[num2];
			int num3 = scenarioCurrentEvent.event_value[num2].value;
			int num4 = GameData.start_event_min[num2];
			int freq = maxValue / 10;
			if (freq < 1)
			{
				freq = 1;
			}
			if (num3 < num4)
			{
				num3 = num4;
			}
			else if (num3 > maxValue)
			{
				num3 = maxValue;
			}
			RefConditionValueSlider.Value = getLogSliderValue(num3, num4, ref maxValue, ref freq);
			MainViewModel.Instance.ConditionValueMax = maxValue;
			MainViewModel.Instance.ConditionValueFreq = freq;
			MainViewModel.Instance.ConditionValueText = num3.ToString();
		}
		else
		{
			MainViewModel.Instance.ScenarioEventConditionValueVisibleBool = false;
		}
		if (GameData.start_event_types[num2] == 1)
		{
			string scenarionEventConditionToggleText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.start_event_goods_text[scenarioCurrentEvent.event_value[num2].type]);
			MainViewModel.Instance.ScenarionEventConditionToggleText = scenarionEventConditionToggleText;
			MainViewModel.Instance.ScenarionEventConditionToggleVisibleBool = true;
			MainViewModel.Instance.ScenarionEventConditionToggleColourVisibleBool = false;
		}
		else if (GameData.start_event_types[num2] == 2)
		{
			string scenarionEventConditionToggleText2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.lord_killed_list[scenarioCurrentEvent.event_value[num2].type]);
			MainViewModel.Instance.ScenarionEventConditionToggleText = scenarionEventConditionToggleText2;
			MainViewModel.Instance.ScenarionEventConditionToggleVisibleBool = true;
			MainViewModel.Instance.ScenarionEventConditionToggleColourVisibleBool = true;
			MainViewModel.Instance.ScenarionEventConditionToggleColour = lordColours[scenarioCurrentEvent.event_value[num2].type];
		}
		else if (GameData.start_event_types[num2] == 3)
		{
			if (scenarioCurrentEvent.event_value[num2].type < 0)
			{
				scenarioCurrentEvent.event_value[num2].type = 0;
			}
			string scenarionEventConditionToggleText3 = ((scenarioCurrentEvent.event_value[num2].type == 31) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 166) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 132 + scenarioCurrentEvent.event_value[num2].type));
			MainViewModel.Instance.ScenarionEventConditionToggleText = scenarionEventConditionToggleText3;
			MainViewModel.Instance.ScenarionEventConditionToggleVisibleBool = true;
			MainViewModel.Instance.ScenarionEventConditionToggleColourVisibleBool = false;
		}
		else
		{
			MainViewModel.Instance.ScenarionEventConditionToggleVisibleBool = false;
			MainViewModel.Instance.ScenarionEventConditionToggleColourVisibleBool = false;
		}
		MainViewModel.Instance.ScenarionEventConditionOnOffVisibleBool = true;
		if (scenarioCurrentEvent.event_value[num2].onoff > 0)
		{
			MainViewModel.Instance.ScenarionEventConditionOnOffText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ACTIVE);
		}
		else
		{
			MainViewModel.Instance.ScenarionEventConditionOnOffText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_INACTIVE);
		}
		UpdateEventConditionRow();
	}

	public void EventConditionToggle()
	{
		if (activeEventConditionRow == null)
		{
			return;
		}
		int num = int.Parse(activeEventConditionRow.DataValue, Director.defaultCulture);
		if (GameData.start_event_types[num] == 1)
		{
			int num2 = scenarioCurrentEvent.event_value[num].type;
			if (num2 == 0)
			{
				num2 = (scenarioCurrentEvent.event_value[num].type = 10);
			}
			int i;
			for (i = 0; GameData.start_event_goods[num][i] != num2; i++)
			{
			}
			i++;
			if (GameData.start_event_goods[num][i] == -1)
			{
				scenarioCurrentEvent.event_value[num].type = (byte)GameData.start_event_goods[num][0];
			}
			else
			{
				scenarioCurrentEvent.event_value[num].type = (byte)GameData.start_event_goods[num][i];
			}
			string scenarionEventConditionToggleText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.start_event_goods_text[scenarioCurrentEvent.event_value[num].type]);
			MainViewModel.Instance.ScenarionEventConditionToggleText = scenarionEventConditionToggleText;
			UpdateEventConditionRow();
		}
		else if (GameData.start_event_types[num] == 2)
		{
			scenarioCurrentEvent.event_value[num].type++;
			if (scenarioCurrentEvent.event_value[num].type >= GameData.lord_killed_list.Length)
			{
				scenarioCurrentEvent.event_value[num].type = 0;
			}
			string scenarionEventConditionToggleText2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.lord_killed_list[scenarioCurrentEvent.event_value[num].type]);
			MainViewModel.Instance.ScenarionEventConditionToggleText = scenarionEventConditionToggleText2;
			MainViewModel.Instance.ScenarionEventConditionToggleColour = lordColours[scenarioCurrentEvent.event_value[num].type];
			UpdateEventConditionRow();
		}
		else if (GameData.start_event_types[num] == 3)
		{
			if (scenarioCurrentEvent.event_value[num].type < 0)
			{
				scenarioCurrentEvent.event_value[num].type = 0;
			}
			scenarioCurrentEvent.event_value[num].type++;
			if (scenarioCurrentEvent.event_value[num].type >= 32)
			{
				scenarioCurrentEvent.event_value[num].type = 0;
			}
			string scenarionEventConditionToggleText3 = ((scenarioCurrentEvent.event_value[num].type == 31) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 166) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 132 + scenarioCurrentEvent.event_value[num].type));
			MainViewModel.Instance.ScenarionEventConditionToggleText = scenarionEventConditionToggleText3;
			UpdateEventConditionRow();
		}
	}

	public void EventConditionOnOff()
	{
		if (activeEventConditionRow != null)
		{
			int num = int.Parse(activeEventConditionRow.DataValue, Director.defaultCulture);
			if (scenarioCurrentEvent.event_value[num].onoff > 0)
			{
				scenarioCurrentEvent.event_value[num].onoff = 0;
			}
			else
			{
				scenarioCurrentEvent.event_value[num].onoff = 1;
			}
			ManageEventConditionWinTimer();
			PopulateEventConditionLeftSide();
			UpdateEventConditionRow();
		}
	}

	private void UpdateEventConditionRow()
	{
		if (activeEventConditionRow == null)
		{
			return;
		}
		int num = int.Parse(activeEventConditionRow.DataValue, Director.defaultCulture);
		int num2 = activeEventConditionRow.DataValue2;
		if (num2 == 111 || num2 == 112 || num2 == 113 || num2 == 123)
		{
			num2 = 113;
		}
		string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, num2);
		if (num2 == 114)
		{
			text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 8);
		}
		if (scenarioCurrentEvent.event_value[num].onoff > 0)
		{
			activeEventConditionRow.BorderVisibility = Visibility.Visible;
			string text2 = "";
			if (GameData.start_event_max[num] != 0)
			{
				if (GameData.start_event_types[num] == 1)
				{
					text = text + " (" + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.start_event_goods_text[scenarioCurrentEvent.event_value[num].type]) + ")";
				}
				text2 = (scenarioCurrentEvent.event_value[num].value * GameData.start_event_multiplier[num]).ToString();
			}
			if (GameData.start_event_types[num] == 2)
			{
				text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.lord_killed_list[scenarioCurrentEvent.event_value[num].type]);
			}
			activeEventConditionRow.Text2 = text2;
		}
		else
		{
			activeEventConditionRow.BorderVisibility = Visibility.Hidden;
			activeEventConditionRow.Text2 = "";
		}
		if (num2 == 194 || num2 == 196 || num2 == 197 || num2 == 198 || num2 == 200)
		{
			MainViewModel.Instance.ConditionValueNameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 24);
		}
		else
		{
			MainViewModel.Instance.ConditionValueNameText = "";
		}
		activeEventConditionRow.Text1 = text;
	}

	public void EventAndOr()
	{
		if (scenarioCurrentEvent != null)
		{
			if (scenarioCurrentEvent.and_or > 0)
			{
				scenarioCurrentEvent.and_or = 0;
			}
			else
			{
				scenarioCurrentEvent.and_or = 1;
			}
		}
	}

	public void PopulateEventActions(bool fromUpdate = false)
	{
		if (scenarioCurrentEvent == null)
		{
			return;
		}
		if (!fromUpdate)
		{
			List<ScenarioEditorRow> list = new List<ScenarioEditorRow>();
			activeEventActionRow = null;
			for (int i = 0; i < GameData.scenarioActionsOrder.Length; i++)
			{
				ScenarioEditorRow scenarioEditorRow = new ScenarioEditorRow(this);
				int num = GameData.scenarioActionsOrder[i];
				if (num > 155)
				{
					num -= 21;
				}
				num -= 128;
				scenarioEditorRow.DataValue2 = i;
				int num2 = GameData.scenarioActionsOrder[i];
				string text = "";
				switch (num2)
				{
				case 148:
					num2 = 146;
					text = text + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 192);
					break;
				case 146:
					text = text + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 193);
					break;
				}
				string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, num2) + text;
				if (num == scenarioCurrentEvent.action)
				{
					activeEventActionRow = scenarioEditorRow;
					UpdateEventActionRow();
					scenarioEditorRow.BorderVisibility = Visibility.Visible;
				}
				else
				{
					scenarioEditorRow.BorderVisibility = Visibility.Hidden;
				}
				scenarioEditorRow.DataValue = num.ToString();
				scenarioEditorRow.Text1 = text2;
				list.Add(scenarioEditorRow);
			}
			PopulateEventActionLeftSide();
			RefScenarioEventActionList.ItemsSource = list;
			if (activeEventActionRow != null)
			{
				RefScenarioEventActionList.ScrollIntoView(activeEventActionRow);
			}
		}
		MainViewModel.Instance.ActionRepeatMonthsText = scenarioCurrentEvent.repeat.ToString();
		if (scenarioCurrentEvent.repeat_count != 10)
		{
			MainViewModel.Instance.ActionRepeatText = scenarioCurrentEvent.repeat_count.ToString();
		}
		else
		{
			MainViewModel.Instance.ActionRepeatText = "∞";
		}
		if (GameData.scenarioActionsOrder[activeEventActionRow.DataValue2] == 178)
		{
			if (!FatControler.turkish && !FatControler.arabic)
			{
				MainViewModel.Instance.ActionValueText = scenarioCurrentEvent.action_data + "%";
			}
			else
			{
				MainViewModel.Instance.ActionValueText = "%" + scenarioCurrentEvent.action_data;
			}
		}
		else if (GameData.scenarioActionsOrder[activeEventActionRow.DataValue2] == 181)
		{
			MainViewModel.Instance.ActionValueText = scenarioCurrentEvent.action_data_marker.ToString();
			MainViewModel.Instance.ActionValue2Text = getReinforcementsName(scenarioCurrentEvent.action_data_reinforcement);
		}
		else if (GameData.scenarioActionsOrder[activeEventActionRow.DataValue2] == 180)
		{
			MainViewModel.Instance.ActionValueText = scenarioCurrentEvent.action_data_marker.ToString();
			MainViewModel.Instance.ActionValue2Text = getAllegianceTeamName(scenarioCurrentEvent.action_data_reinforcement);
		}
		else if (GameData.scenarioActionsOrder[activeEventActionRow.DataValue2] == 182)
		{
			if (scenarioCurrentEvent.action_data_marker == 0)
			{
				MainViewModel.Instance.ActionValueText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 14);
			}
			else
			{
				MainViewModel.Instance.ActionValueText = scenarioCurrentEvent.action_data_marker.ToString();
			}
		}
		else
		{
			MainViewModel.Instance.ActionValueText = scenarioCurrentEvent.action_data.ToString();
		}
	}

	private void PopulateEventActionLeftSide()
	{
		int dataValue = activeEventActionRow.DataValue2;
		int num = GameData.scenarioActionsOrder[dataValue];
		string text = "";
		switch (num)
		{
		case 148:
			num = 146;
			text = text + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 192);
			break;
		case 146:
			text = text + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 193);
			break;
		}
		string scenarioEventActionTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, num) + text;
		MainViewModel.Instance.ScenarioEventActionTitle = scenarioEventActionTitle;
		MainViewModel.Instance.ActionValueNameText = "";
		MainViewModel.Instance.ActionValue2NameText = "";
		switch (scenarioCurrentEvent.action)
		{
		case 5:
		case 11:
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
		case 18:
		case 19:
		case 20:
		case 29:
		case 30:
			MainViewModel.Instance.ScenarioEventActionRepeatVisibleBool = true;
			break;
		case 31:
			MainViewModel.Instance.ActionValueNameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 24);
			MainViewModel.Instance.ActionValue2NameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 30);
			MainViewModel.Instance.ScenarioEventActionRepeatVisibleBool = false;
			break;
		case 32:
			MainViewModel.Instance.ActionValueNameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 24);
			MainViewModel.Instance.ActionValue2NameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 26);
			MainViewModel.Instance.ScenarioEventActionRepeatVisibleBool = false;
			break;
		case 33:
			MainViewModel.Instance.ActionValueNameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 24);
			break;
		default:
			MainViewModel.Instance.ScenarioEventActionRepeatVisibleBool = false;
			break;
		}
		MainViewModel.Instance.ActionValueMin = 1;
		switch (GameData.scenarioActionsOrder[dataValue])
		{
		case 132:
		case 139:
		case 145:
		case 146:
		case 148:
		case 178:
		case 179:
		{
			int num2 = 0;
			switch (GameData.scenarioActionsOrder[dataValue])
			{
			case 132:
				num2 = 10;
				break;
			case 139:
				num2 = 10;
				break;
			case 145:
				num2 = 10;
				break;
			case 146:
				num2 = 50;
				break;
			case 148:
				num2 = 50;
				break;
			case 178:
				num2 = 100;
				break;
			case 179:
				num2 = 10;
				break;
			}
			MainViewModel.Instance.ScenarioEventActionValueVisibleBool = true;
			MainViewModel.Instance.ScenarioEventActionValue2VisibleBool = false;
			MainViewModel.Instance.ActionValueMax = num2;
			int num3 = num2 / 10;
			if (num3 < 1)
			{
				num3 = 1;
			}
			MainViewModel.Instance.ActionValueFreq = num3;
			if (scenarioCurrentEvent.action_data <= 0)
			{
				scenarioCurrentEvent.action_data = 1;
			}
			RefActionValueSlider.Value = scenarioCurrentEvent.action_data;
			break;
		}
		case 130:
		case 154:
		case 155:
			MainViewModel.Instance.ScenarioEventActionValueVisibleBool = false;
			MainViewModel.Instance.ScenarioEventActionValue2VisibleBool = false;
			break;
		case 180:
		case 181:
		{
			int actionValueMax2 = 10;
			MainViewModel.Instance.ScenarioEventActionValueVisibleBool = true;
			MainViewModel.Instance.ScenarioEventActionValue2VisibleBool = true;
			MainViewModel.Instance.ActionValueMax = actionValueMax2;
			MainViewModel.Instance.ActionValueFreq = 1;
			if (scenarioCurrentEvent.action_data_marker <= 0)
			{
				scenarioCurrentEvent.action_data_marker = 1;
			}
			RefActionValueSlider.Value = scenarioCurrentEvent.action_data_marker;
			if (GameData.scenarioActionsOrder[dataValue] == 180)
			{
				MainViewModel.Instance.ActionValue2Min = 2;
				MainViewModel.Instance.ActionValue2Max = 8;
			}
			else
			{
				MainViewModel.Instance.ActionValue2Min = 1;
				MainViewModel.Instance.ActionValue2Max = 34;
			}
			MainViewModel.Instance.ActionValue2Freq = 1;
			if (scenarioCurrentEvent.action_data_reinforcement <= 0)
			{
				scenarioCurrentEvent.action_data_reinforcement = 1;
			}
			RefActionValue2Slider.Value = scenarioCurrentEvent.action_data_reinforcement;
			break;
		}
		case 182:
		{
			int actionValueMax = 10;
			MainViewModel.Instance.ScenarioEventActionValueVisibleBool = true;
			MainViewModel.Instance.ScenarioEventActionValue2VisibleBool = false;
			MainViewModel.Instance.ActionValueMax = actionValueMax;
			MainViewModel.Instance.ActionValueMin = 0;
			MainViewModel.Instance.ActionValueFreq = 1;
			if (scenarioCurrentEvent.action_data_marker < 0)
			{
				scenarioCurrentEvent.action_data_marker = 0;
			}
			RefActionValueSlider.Value = scenarioCurrentEvent.action_data_marker;
			break;
		}
		default:
			MainViewModel.Instance.ScenarioEventActionValueVisibleBool = false;
			MainViewModel.Instance.ScenarioEventActionValue2VisibleBool = false;
			break;
		}
	}

	private void UpdateEventActionRow()
	{
		if (activeEventActionRow == null)
		{
			return;
		}
		int dataValue = activeEventActionRow.DataValue2;
		switch (GameData.scenarioActionsOrder[dataValue])
		{
		case 132:
		case 139:
		case 145:
		case 146:
		case 148:
		case 179:
			activeEventActionRow.Text2 = scenarioCurrentEvent.action_data.ToString();
			break;
		case 180:
			activeEventActionRow.Text2 = scenarioCurrentEvent.action_data_marker + "  " + getAllegianceTeamName(scenarioCurrentEvent.action_data_reinforcement);
			break;
		case 181:
			activeEventActionRow.Text2 = scenarioCurrentEvent.action_data_marker + " / " + getReinforcementsName(scenarioCurrentEvent.action_data_reinforcement);
			break;
		case 182:
			activeEventActionRow.Text2 = scenarioCurrentEvent.action_data_marker.ToString();
			break;
		case 178:
			if (!FatControler.turkish && !FatControler.arabic)
			{
				activeEventActionRow.Text2 = scenarioCurrentEvent.action_data + "%";
			}
			else
			{
				activeEventActionRow.Text2 = "%" + scenarioCurrentEvent.action_data;
			}
			break;
		}
	}

	public void ActionOKButton(ref int mode)
	{
		if (scenarioCurrentInvasion != null)
		{
			EngineInterface.ApplyScenarioInvasion(scenarioCurrentLine, scenarioCurrentInvasion);
		}
		if (scenarioCurrentEvent != null)
		{
			EngineInterface.ApplyScenarioEvent(scenarioCurrentLine, scenarioCurrentEvent);
		}
		GameData.Instance.SetScenarioOverview(EngineInterface.GetScenarioOverview());
	}

	public void ActionDeleteButton()
	{
		EngineInterface.DeleteScenarioEntry(scenarioCurrentLine);
		GameData.Instance.SetScenarioOverview(EngineInterface.GetScenarioOverview());
	}

	public void SetInvasionFrom(int from)
	{
		if (scenarioCurrentInvasion == null)
		{
			return;
		}
		bool flag = false;
		switch (from)
		{
		case -100:
			if (getStartingTeamForInvasions(2) != 1)
			{
				RefButtonInvasionInvPlayer2.Visibility = Visibility.Visible;
				RefButtonInvasionReinPlayer2.Visibility = Visibility.Hidden;
			}
			else
			{
				RefButtonInvasionInvPlayer2.Visibility = Visibility.Hidden;
				RefButtonInvasionReinPlayer2.Visibility = Visibility.Visible;
			}
			if (getStartingTeamForInvasions(3) != 1)
			{
				RefButtonInvasionInvPlayer3.Visibility = Visibility.Visible;
				RefButtonInvasionReinPlayer3.Visibility = Visibility.Hidden;
			}
			else
			{
				RefButtonInvasionInvPlayer3.Visibility = Visibility.Hidden;
				RefButtonInvasionReinPlayer3.Visibility = Visibility.Visible;
			}
			if (getStartingTeamForInvasions(4) != 1)
			{
				RefButtonInvasionInvPlayer4.Visibility = Visibility.Visible;
				RefButtonInvasionReinPlayer4.Visibility = Visibility.Hidden;
			}
			else
			{
				RefButtonInvasionInvPlayer4.Visibility = Visibility.Hidden;
				RefButtonInvasionReinPlayer4.Visibility = Visibility.Visible;
			}
			if (getStartingTeamForInvasions(5) != 1)
			{
				RefButtonInvasionInvPlayer5.Visibility = Visibility.Visible;
				RefButtonInvasionReinPlayer5.Visibility = Visibility.Hidden;
			}
			else
			{
				RefButtonInvasionInvPlayer5.Visibility = Visibility.Hidden;
				RefButtonInvasionReinPlayer5.Visibility = Visibility.Visible;
			}
			if (getStartingTeamForInvasions(6) != 1)
			{
				RefButtonInvasionInvPlayer6.Visibility = Visibility.Visible;
				RefButtonInvasionReinPlayer6.Visibility = Visibility.Hidden;
			}
			else
			{
				RefButtonInvasionInvPlayer6.Visibility = Visibility.Hidden;
				RefButtonInvasionReinPlayer6.Visibility = Visibility.Visible;
			}
			if (getStartingTeamForInvasions(7) != 1)
			{
				RefButtonInvasionInvPlayer7.Visibility = Visibility.Visible;
				RefButtonInvasionReinPlayer7.Visibility = Visibility.Hidden;
			}
			else
			{
				RefButtonInvasionInvPlayer7.Visibility = Visibility.Hidden;
				RefButtonInvasionReinPlayer7.Visibility = Visibility.Visible;
			}
			if (getStartingTeamForInvasions(8) != 1)
			{
				RefButtonInvasionInvPlayer8.Visibility = Visibility.Visible;
				RefButtonInvasionReinPlayer8.Visibility = Visibility.Hidden;
			}
			else
			{
				RefButtonInvasionInvPlayer8.Visibility = Visibility.Hidden;
				RefButtonInvasionReinPlayer8.Visibility = Visibility.Visible;
			}
			if (scenarioCurrentInvasion.from == 0)
			{
				scenarioCurrentInvasion.from = 112;
			}
			else if (scenarioCurrentInvasion.from == 1)
			{
				scenarioCurrentInvasion.from = 212;
			}
			switch (scenarioCurrentInvasion.from % 10)
			{
			case 1:
				if (RefButtonInvasionInvPlayer1.Visibility == Visibility.Visible)
				{
					RefButtonInvasionInvPlayer1.IsChecked = true;
				}
				else
				{
					RefButtonInvasionReinPlayer1.IsChecked = true;
				}
				break;
			case 2:
				if (RefButtonInvasionInvPlayer2.Visibility == Visibility.Visible)
				{
					RefButtonInvasionInvPlayer2.IsChecked = true;
				}
				else
				{
					RefButtonInvasionReinPlayer2.IsChecked = true;
				}
				break;
			case 3:
				if (RefButtonInvasionInvPlayer3.Visibility == Visibility.Visible)
				{
					RefButtonInvasionInvPlayer3.IsChecked = true;
				}
				else
				{
					RefButtonInvasionReinPlayer3.IsChecked = true;
				}
				break;
			case 4:
				if (RefButtonInvasionInvPlayer4.Visibility == Visibility.Visible)
				{
					RefButtonInvasionInvPlayer4.IsChecked = true;
				}
				else
				{
					RefButtonInvasionReinPlayer4.IsChecked = true;
				}
				break;
			case 5:
				if (RefButtonInvasionInvPlayer5.Visibility == Visibility.Visible)
				{
					RefButtonInvasionInvPlayer5.IsChecked = true;
				}
				else
				{
					RefButtonInvasionReinPlayer5.IsChecked = true;
				}
				break;
			case 6:
				if (RefButtonInvasionInvPlayer6.Visibility == Visibility.Visible)
				{
					RefButtonInvasionInvPlayer6.IsChecked = true;
				}
				else
				{
					RefButtonInvasionReinPlayer6.IsChecked = true;
				}
				break;
			case 7:
				if (RefButtonInvasionInvPlayer7.Visibility == Visibility.Visible)
				{
					RefButtonInvasionInvPlayer7.IsChecked = true;
				}
				else
				{
					RefButtonInvasionReinPlayer7.IsChecked = true;
				}
				break;
			case 8:
				if (RefButtonInvasionInvPlayer8.Visibility == Visibility.Visible)
				{
					RefButtonInvasionInvPlayer8.IsChecked = true;
				}
				else
				{
					RefButtonInvasionReinPlayer8.IsChecked = true;
				}
				break;
			}
			flag = true;
			MainViewModel.Instance.InvasionFromVis = true;
			break;
		case -1:
			MainViewModel.Instance.InvasionFromVis = false;
			break;
		case 100:
			scenarioCurrentInvasion.from = scenarioCurrentInvasion.from % 100 + 100;
			flag = true;
			UpdateInvasionFromTitle();
			break;
		case 101:
			scenarioCurrentInvasion.from = scenarioCurrentInvasion.from % 100 + 200;
			flag = true;
			UpdateInvasionFromTitle();
			break;
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
		case 6:
		case 7:
		case 8:
		{
			int num = scenarioCurrentInvasion.from / 100;
			scenarioCurrentInvasion.from = num * 100 + 10 + from;
			UpdateInvasionFromTitle();
			break;
		}
		}
		if (flag)
		{
			if (scenarioCurrentInvasion.from < 200)
			{
				MainViewModel.Instance.InvasionButtonHighlight[1] = true;
				MainViewModel.Instance.InvasionButtonHighlight[2] = false;
			}
			else
			{
				MainViewModel.Instance.InvasionButtonHighlight[1] = false;
				MainViewModel.Instance.InvasionButtonHighlight[2] = true;
			}
		}
	}

	public void InvasionMonthBtn(bool increment)
	{
		if (scenarioCurrentInvasion == null)
		{
			return;
		}
		if (increment)
		{
			if (++scenarioCurrentInvasion.month >= 12)
			{
				scenarioCurrentInvasion.month = 0;
			}
		}
		else if (--scenarioCurrentInvasion.month < 0)
		{
			scenarioCurrentInvasion.month = 11;
		}
	}

	public void SetInvasionMarkerID(int markerID)
	{
		if (scenarioCurrentInvasion != null)
		{
			scenarioCurrentInvasion.markerID = markerID;
			for (int i = 0; i < 11; i++)
			{
				MainViewModel.Instance.InvasionSpawnMarkersHighlight[i] = false;
			}
			MainViewModel.Instance.InvasionSpawnMarkersHighlight[markerID] = true;
		}
	}

	public int GetInvasionSize(int index)
	{
		if (scenarioCurrentInvasion != null)
		{
			return scenarioCurrentInvasion._size[index];
		}
		return 0;
	}

	public void EventMonthBtn(bool increment)
	{
		if (scenarioCurrentEvent == null)
		{
			return;
		}
		if (increment)
		{
			if (++scenarioCurrentEvent.month >= 12)
			{
				scenarioCurrentEvent.month = 0;
			}
		}
		else if (--scenarioCurrentEvent.month < 0)
		{
			scenarioCurrentEvent.month = 11;
		}
	}

	public void EventActionSelected(int actionID)
	{
		if (scenarioCurrentEvent.action == actionID)
		{
			return;
		}
		scenarioCurrentEvent.action_data = 0;
		scenarioCurrentEvent.action = actionID;
		string text = actionID.ToString();
		foreach (ScenarioEditorRow item in RefScenarioEventActionList.ItemsSource)
		{
			item.Text2 = "";
			if (item.DataValue == text)
			{
				item.BorderVisibility = Visibility.Visible;
				activeEventActionRow = item;
			}
			else
			{
				item.BorderVisibility = Visibility.Hidden;
			}
		}
		PopulateEventActionLeftSide();
		UpdateEventActionRow();
	}

	public void EventConditionSelected(int conditionID)
	{
		string text = conditionID.ToString();
		foreach (ScenarioEditorRow item in RefScenarioEventConditionList.ItemsSource)
		{
			if (item.DataValue == text)
			{
				activeEventConditionRow = item;
			}
		}
		PopulateEventConditionLeftSide();
	}

	private void ScenarioInvasionRepeatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int repeat = (int)RefInvasionRepeatSlider.Value;
		if (scenarioCurrentInvasion != null)
		{
			scenarioCurrentInvasion.repeat = repeat;
		}
	}

	private void ScenarioInvasionSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefInvasionSize.Value;
		if (scenarioCurrentInvasion != null)
		{
			scenarioCurrentInvasion._size[MainViewModel.Instance.ScenarioInvasionSizeType] = num;
		}
	}

	private void ActionRepeatMonthsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefActionRepeatMonthsSlider.Value;
		if (scenarioCurrentEvent != null)
		{
			scenarioCurrentEvent.repeat = (byte)num;
		}
	}

	private void ActionRepeatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefActionRepeatSlider.Value;
		if (scenarioCurrentEvent != null)
		{
			scenarioCurrentEvent.repeat_count = (byte)num;
		}
	}

	private void ActionValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefActionValueSlider.Value;
		string actionValueText = num.ToString();
		if (scenarioCurrentEvent != null)
		{
			switch (scenarioCurrentEvent.action)
			{
			case 31:
			case 32:
				scenarioCurrentEvent.action_data_marker = num;
				break;
			case 33:
				scenarioCurrentEvent.action_data_marker = num;
				if (num == 0)
				{
					actionValueText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 14);
				}
				break;
			default:
				scenarioCurrentEvent.action_data = num;
				break;
			}
		}
		UpdateEventActionRow();
		MainViewModel.Instance.ActionValueText = actionValueText;
	}

	private void ActionValue2Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefActionValue2Slider.Value;
		if (scenarioCurrentEvent != null)
		{
			switch (scenarioCurrentEvent.action)
			{
			case 31:
				scenarioCurrentEvent.action_data_reinforcement = num;
				MainViewModel.Instance.ActionValue2Text = getAllegianceTeamName(num);
				break;
			case 32:
				scenarioCurrentEvent.action_data_reinforcement = num;
				MainViewModel.Instance.ActionValue2Text = getReinforcementsName(num);
				break;
			default:
				scenarioCurrentEvent.action_data = num;
				break;
			}
		}
		UpdateEventActionRow();
	}

	private void ConditionValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int sliderPos = (int)RefConditionValueSlider.Value;
		if (scenarioCurrentEvent != null && activeEventConditionRow != null)
		{
			int num = int.Parse(activeEventConditionRow.DataValue, Director.defaultCulture);
			int maxValue = GameData.start_event_max[num];
			sliderPos = getLogSliderDislayValue(sliderPos, maxValue);
			scenarioCurrentEvent.event_value[num].value = (short)sliderPos;
			MainViewModel.Instance.ConditionValueText = sliderPos.ToString();
		}
		UpdateEventConditionRow();
	}

	private void ScenarioRefSliderEditTeam_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (FatControler.currentScene != 0)
		{
			int num = 0;
			switch (((Slider)e.Source).Name)
			{
			case "SliderEditTeam1":
				num = 1;
				break;
			case "SliderEditTeam2":
				num = 2;
				break;
			case "SliderEditTeam3":
				num = 3;
				break;
			case "SliderEditTeam4":
				num = 4;
				break;
			case "SliderEditTeam5":
				num = 5;
				break;
			case "SliderEditTeam6":
				num = 6;
				break;
			case "SliderEditTeam7":
				num = 7;
				break;
			case "SliderEditTeam8":
				num = 8;
				break;
			}
			if (num != 0)
			{
				int state = (int)e.NewValue;
				MainViewModel.Instance.ScenarioEditTeams[num] = state.ToString();
				EngineInterface.GameAction(Enums.GameActionCommand.SetStartingTeam, num, state);
			}
		}
	}

	private void FasterGoodsCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (RefFasterGoodsCheck.IsChecked.Value)
		{
			GameData.Instance.scenarioOverview.fast_goods_feedin = 1;
		}
		else
		{
			GameData.Instance.scenarioOverview.fast_goods_feedin = 0;
		}
		EngineInterface.GameAction(Enums.GameActionCommand.FastGoodsFeedin, GameData.Instance.scenarioOverview.fast_goods_feedin, GameData.Instance.scenarioOverview.fast_goods_feedin);
	}

	public static string getAllegianceTeamName(int data)
	{
		if (data < 1)
		{
			data = 1;
		}
		else if (data > 8)
		{
			data = 9;
		}
		return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 27 + data - 1);
	}

	public static string getReinforcementsName(int data)
	{
		return data switch
		{
			2 => "20 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 22), 
			3 => "50 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 22), 
			4 => "100 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 22), 
			5 => "10 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 23), 
			6 => "20 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 23), 
			7 => "50 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 23), 
			8 => "100 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 23), 
			9 => "10 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 24), 
			10 => "20 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 24), 
			11 => "50 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 24), 
			12 => "100 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 24), 
			13 => "10 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 26), 
			14 => "20 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 26), 
			15 => "50 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 26), 
			16 => "100 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 26), 
			17 => "10 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 25), 
			18 => "20 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 25), 
			19 => "50 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 25), 
			20 => "100 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 25), 
			21 => "10 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 27), 
			22 => "20 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 27), 
			23 => "50 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 27), 
			24 => "100 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 27), 
			25 => "5 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 28), 
			26 => "10 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 28), 
			27 => "20 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 28), 
			28 => "50 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 28), 
			29 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 53), 
			30 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 54), 
			31 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 55), 
			32 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 35), 
			33 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 36), 
			34 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 37), 
			_ => "10 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 22), 
		};
	}

	public int getLogSliderValue(int currentValue, int minValue, ref int maxValue, ref int freq)
	{
		if (maxValue <= 100)
		{
			return currentValue;
		}
		int[] array;
		switch (maxValue)
		{
		case 500:
			freq = 10;
			array = SliderCurve500;
			maxValue = 100;
			break;
		case 1000:
			freq = 10;
			array = SliderCurve1000;
			maxValue = 100;
			break;
		case 10000:
			freq = 15;
			array = SliderCurve10000;
			maxValue = 145;
			break;
		case 25000:
			freq = 16;
			array = SliderCurve25000;
			maxValue = 160;
			break;
		default:
			freq = maxValue / 10;
			if (freq < 1)
			{
				freq = 1;
			}
			return currentValue;
		}
		for (int i = 0; i <= maxValue; i++)
		{
			if (currentValue <= array[i])
			{
				return i;
			}
		}
		return currentValue;
	}

	public int getLogSliderDislayValue(int sliderPos, int maxValue)
	{
		if (maxValue <= 100)
		{
			return sliderPos;
		}
		int[] array;
		switch (maxValue)
		{
		case 500:
			array = SliderCurve500;
			break;
		case 1000:
			array = SliderCurve1000;
			break;
		case 10000:
			array = SliderCurve10000;
			break;
		case 25000:
			array = SliderCurve25000;
			break;
		default:
			return sliderPos;
		}
		return array[sliderPos];
	}
}
