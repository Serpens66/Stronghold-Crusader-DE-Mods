using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Noesis;

namespace CrusaderDE;

public class HUD_Scenario : UserControl
{
	public WGT_Heading RefHeading;

	public Grid RefViewRoot;

	public Grid RefViewMain;

	public Grid RefViewStartingGoods;

	public Grid RefViewTradedGoods;

	public Grid RefViewBuildingAvailibity;

	public Grid RefViewInvasions;

	public Grid RefViewEvents;

	public Grid RefViewEventsConditions;

	public Grid RefViewEventsActions;

	public Grid RefViewEditMessage;

	public Grid RefScenarioViewEditTeams;

	public Grid RefScenarioViewAdjustDates;

	public ListView RefScenarioActionList;

	public ListView RefScenarioBuildingList;

	public TextBox RefStartingYear;

	public TextBox RefEditMessage;

	public Slider RefStartingPop;

	public Slider RefStartingSpecialGold;

	public Slider RefStartingGoods;

	public RadioButton RefStartingGoodsGoldDefault;

	public CheckBox RefFasterGoodsCheck;

	public StackPanel RefLeftPanel;

	public TextBox RefInvasionYear;

	public Slider RefInvasionRepeatSlider;

	public Button RefSignpost;

	public Slider RefInvasionSize;

	public RadioButton RefInvasionSizeArcherDefault;

	public TextBox RefEventYear;

	public ListView RefScenarioEventActionList;

	public Slider RefActionRepeatMonthsSlider;

	public Slider RefActionRepeatSlider;

	public Slider RefActionValueSlider;

	public Slider RefActionValue2Slider;

	public ListView RefScenarioEventConditionList;

	public Slider RefConditionValueSlider;

	public Slider RefSliderEditTeam1;

	public Slider RefSliderEditTeam2;

	public Slider RefSliderEditTeam3;

	public Slider RefSliderEditTeam4;

	public Slider RefSliderEditTeam5;

	public Slider RefSliderEditTeam6;

	public Slider RefSliderEditTeam7;

	public Slider RefSliderEditTeam8;

	public TextBox RefAdjustStartingYear;

	public Button RefButtonEditText;

	public Button RefButtonStartingGoodsPresetLow;

	public Button RefButtonStartingGoodsPresetMedium;

	public Button RefButtonStartingGoodsPresetHigh;

	public RadioButton RefButtonInvasionInvPlayer1;

	public RadioButton RefButtonInvasionInvPlayer2;

	public RadioButton RefButtonInvasionInvPlayer3;

	public RadioButton RefButtonInvasionInvPlayer4;

	public RadioButton RefButtonInvasionInvPlayer5;

	public RadioButton RefButtonInvasionInvPlayer6;

	public RadioButton RefButtonInvasionInvPlayer7;

	public RadioButton RefButtonInvasionInvPlayer8;

	public RadioButton RefButtonInvasionReinPlayer1;

	public RadioButton RefButtonInvasionReinPlayer2;

	public RadioButton RefButtonInvasionReinPlayer3;

	public RadioButton RefButtonInvasionReinPlayer4;

	public RadioButton RefButtonInvasionReinPlayer5;

	public RadioButton RefButtonInvasionReinPlayer6;

	public RadioButton RefButtonInvasionReinPlayer7;

	public RadioButton RefButtonInvasionReinPlayer8;

	public SolidColorBrush[] lordColours = (SolidColorBrush[])(object)new SolidColorBrush[6]
	{
		new SolidColorBrush(Color.FromArgb((byte)0, (byte)0, (byte)0, (byte)0)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)240, (byte)121, (byte)30)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)209, (byte)35)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)204, (byte)27, (byte)56)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)0, (byte)0)),
		new SolidColorBrush(Color.FromArgb((byte)0, (byte)0, (byte)0, (byte)0))
	};

	public int newStartingMonthValue;

	public ObservableCollection<ScenarioEditorRow> BuildingItems = new ObservableCollection<ScenarioEditorRow>();

	public ObservableCollection<ScenarioEditorRow> EventConditionItems = new ObservableCollection<ScenarioEditorRow>();

	public Dictionary<int, ScenarioEditorRow> BuildingItemsDict = new Dictionary<int, ScenarioEditorRow>();

	public bool barracksItemsShowing;

	public bool mercPostItemsShowing;

	public bool bedouinItemsShowing;

	public int barracksWoodRow = -1;

	public int barracksStoneRow = -1;

	public int barracksBedouinRow = -1;

	public EngineInterface.tl_event scenarioCurrentEvent;

	public EngineInterface.tl_invasion scenarioCurrentInvasion;

	public int scenarioCurrentLine = -1;

	public ScenarioEditorRow activeEventActionRow;

	public ScenarioEditorRow activeEventConditionRow;

	public int[] SliderCurve500 = new int[101]
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

	public int[] SliderCurve10000 = new int[146]
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

	public int[] SliderCurve25000 = new int[161]
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
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Expected O, but got Unknown
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Expected O, but got Unknown
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Expected O, but got Unknown
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Expected O, but got Unknown
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Expected O, but got Unknown
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Expected O, but got Unknown
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Expected O, but got Unknown
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Expected O, but got Unknown
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Expected O, but got Unknown
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Expected O, but got Unknown
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Expected O, but got Unknown
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Expected O, but got Unknown
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Expected O, but got Unknown
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Expected O, but got Unknown
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Expected O, but got Unknown
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a1: Expected O, but got Unknown
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Expected O, but got Unknown
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Expected O, but got Unknown
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Expected O, but got Unknown
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Expected O, but got Unknown
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_0312: Expected O, but got Unknown
		//IL_031e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0328: Expected O, but got Unknown
		//IL_0335: Unknown result type (might be due to invalid IL or missing references)
		//IL_033f: Expected O, but got Unknown
		//IL_034c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Expected O, but got Unknown
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		//IL_036c: Expected O, but got Unknown
		//IL_0379: Unknown result type (might be due to invalid IL or missing references)
		//IL_0383: Expected O, but got Unknown
		//IL_0390: Unknown result type (might be due to invalid IL or missing references)
		//IL_039a: Expected O, but got Unknown
		//IL_03a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b0: Expected O, but got Unknown
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Expected O, but got Unknown
		//IL_03d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03de: Expected O, but got Unknown
		//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Expected O, but got Unknown
		//IL_0417: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Expected O, but got Unknown
		//IL_0444: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Expected O, but got Unknown
		//IL_0475: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_048b: Expected O, but got Unknown
		//IL_0497: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a1: Expected O, but got Unknown
		//IL_04ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b7: Expected O, but got Unknown
		//IL_04c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ce: Expected O, but got Unknown
		//IL_04db: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e5: Expected O, but got Unknown
		//IL_04f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fb: Expected O, but got Unknown
		//IL_051e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0528: Expected O, but got Unknown
		//IL_054b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0555: Expected O, but got Unknown
		//IL_0561: Unknown result type (might be due to invalid IL or missing references)
		//IL_056b: Expected O, but got Unknown
		//IL_0578: Unknown result type (might be due to invalid IL or missing references)
		//IL_0582: Expected O, but got Unknown
		//IL_058f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0599: Expected O, but got Unknown
		//IL_05a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05af: Expected O, but got Unknown
		//IL_05bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c6: Expected O, but got Unknown
		//IL_05d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05dc: Expected O, but got Unknown
		//IL_05ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0609: Expected O, but got Unknown
		//IL_062c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0636: Expected O, but got Unknown
		//IL_0659: Unknown result type (might be due to invalid IL or missing references)
		//IL_0663: Expected O, but got Unknown
		//IL_0686: Unknown result type (might be due to invalid IL or missing references)
		//IL_0690: Expected O, but got Unknown
		//IL_069d: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a7: Expected O, but got Unknown
		//IL_06b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_06bd: Expected O, but got Unknown
		//IL_06e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ea: Expected O, but got Unknown
		//IL_070d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0717: Expected O, but got Unknown
		//IL_073a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0744: Expected O, but got Unknown
		//IL_0767: Unknown result type (might be due to invalid IL or missing references)
		//IL_0771: Expected O, but got Unknown
		//IL_0794: Unknown result type (might be due to invalid IL or missing references)
		//IL_079e: Expected O, but got Unknown
		//IL_07c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_07cb: Expected O, but got Unknown
		//IL_07ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_07f8: Expected O, but got Unknown
		//IL_081b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0825: Expected O, but got Unknown
		//IL_0848: Unknown result type (might be due to invalid IL or missing references)
		//IL_0852: Expected O, but got Unknown
		//IL_085e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0868: Expected O, but got Unknown
		//IL_0874: Unknown result type (might be due to invalid IL or missing references)
		//IL_087e: Expected O, but got Unknown
		//IL_088a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0894: Expected O, but got Unknown
		//IL_08a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_08aa: Expected O, but got Unknown
		//IL_08b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c0: Expected O, but got Unknown
		//IL_08cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d6: Expected O, but got Unknown
		//IL_08e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ec: Expected O, but got Unknown
		//IL_08f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0902: Expected O, but got Unknown
		//IL_090e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0918: Expected O, but got Unknown
		//IL_0924: Unknown result type (might be due to invalid IL or missing references)
		//IL_092e: Expected O, but got Unknown
		//IL_093a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0944: Expected O, but got Unknown
		//IL_0950: Unknown result type (might be due to invalid IL or missing references)
		//IL_095a: Expected O, but got Unknown
		//IL_0966: Unknown result type (might be due to invalid IL or missing references)
		//IL_0970: Expected O, but got Unknown
		//IL_097c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0986: Expected O, but got Unknown
		//IL_0992: Unknown result type (might be due to invalid IL or missing references)
		//IL_099c: Expected O, but got Unknown
		//IL_09a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_09b2: Expected O, but got Unknown
		//IL_09be: Unknown result type (might be due to invalid IL or missing references)
		//IL_09c8: Expected O, but got Unknown
		//IL_09d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_09de: Expected O, but got Unknown
		//IL_09ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_09f4: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDScenario = this;
		RefHeading = (WGT_Heading)((FrameworkElement)this).FindName("ScenarioHeader");
		RefViewRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		((UIElement)RefViewRoot).Visibility = (Visibility)1;
		RefLeftPanel = (StackPanel)((FrameworkElement)this).FindName("LeftPanel");
		RefViewMain = (Grid)((FrameworkElement)this).FindName("ScenarioViewMain");
		RefViewStartingGoods = (Grid)((FrameworkElement)this).FindName("ScenarioViewStartingGoods");
		RefViewTradedGoods = (Grid)((FrameworkElement)this).FindName("ScenarioViewTradedGoods");
		RefViewBuildingAvailibity = (Grid)((FrameworkElement)this).FindName("ScenarioViewBuildingAvailibility");
		RefViewInvasions = (Grid)((FrameworkElement)this).FindName("ScenarioViewInvasions");
		RefViewEvents = (Grid)((FrameworkElement)this).FindName("ScenarioViewEvents");
		RefViewEventsConditions = (Grid)((FrameworkElement)this).FindName("ScenarioViewEventsConditions");
		RefViewEventsActions = (Grid)((FrameworkElement)this).FindName("ScenarioViewEventsActions");
		RefViewEditMessage = (Grid)((FrameworkElement)this).FindName("ScenarioEditMessage");
		RefScenarioViewEditTeams = (Grid)((FrameworkElement)this).FindName("ScenarioViewEditTeams");
		RefScenarioViewAdjustDates = (Grid)((FrameworkElement)this).FindName("ScenarioViewAdjustDates");
		RefStartingGoodsGoldDefault = (RadioButton)((FrameworkElement)this).FindName("StartingGoodsGoldDefault");
		RefFasterGoodsCheck = (CheckBox)((FrameworkElement)this).FindName("FasterGoodsCheck");
		((ToggleButton)RefFasterGoodsCheck).Checked += new RoutedEventHandler(FasterGoodsCheck_ValueChanged);
		((ToggleButton)RefFasterGoodsCheck).Unchecked += new RoutedEventHandler(FasterGoodsCheck_ValueChanged);
		RefScenarioActionList = (ListView)((FrameworkElement)this).FindName("ScenarioActionList");
		RefScenarioBuildingList = (ListView)((FrameworkElement)this).FindName("ScenarioBuildingList");
		((Selector)RefScenarioActionList).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefScenarioActionList).SelectedItem != null)
			{
				SelectActionRow(int.Parse(((ScenarioEditorRow)((Selector)RefScenarioActionList).SelectedItem).DataValue, Director.defaultCulture));
				((Selector)RefScenarioActionList).SelectedItem = null;
			}
		};
		RefStartingYear = (TextBox)((FrameworkElement)this).FindName("TextBoxStartingYear");
		((UIElement)RefStartingYear).PreviewTextInput += new TextCompositionEventHandler(NumberValidationTextBox);
		((UIElement)RefStartingYear).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		RefAdjustStartingYear = (TextBox)((FrameworkElement)this).FindName("TextBoxAdjustStartingYear");
		((UIElement)RefAdjustStartingYear).PreviewTextInput += new TextCompositionEventHandler(NumberValidationTextBox);
		((UIElement)RefAdjustStartingYear).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		RefEditMessage = (TextBox)((FrameworkElement)this).FindName("TextBoxScenarioMessage");
		((UIElement)RefEditMessage).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefEditMessage).TextChanged += new RoutedEventHandler(EditMessageTextChanged);
		RefStartingPop = (Slider)((FrameworkElement)this).FindName("PopSlider");
		((RangeBase)RefStartingPop).ValueChanged += ScenarioPopularitySlider_ValueChanged;
		RefStartingSpecialGold = (Slider)((FrameworkElement)this).FindName("SpecialGoldSlider");
		((RangeBase)RefStartingSpecialGold).ValueChanged += ScenarioSpecialGoldSlider_ValueChanged;
		RefStartingGoods = (Slider)((FrameworkElement)this).FindName("StartingGoodsSlider");
		((RangeBase)RefStartingGoods).ValueChanged += ScenarioStartingGoodsSlider_ValueChanged;
		((Timeline)(Storyboard)((FrameworkElement)this).Resources[(object)"Outtro"]).Completed += (CompletedHandler)delegate
		{
			((UIElement)RefViewRoot).Visibility = (Visibility)1;
		};
		RefSignpost = (Button)((FrameworkElement)this).FindName("Signpost");
		RefInvasionYear = (TextBox)((FrameworkElement)this).FindName("TextBoxStartingYearInvasion");
		((UIElement)RefInvasionYear).PreviewTextInput += new TextCompositionEventHandler(NumberValidationTextBox);
		((UIElement)RefInvasionYear).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		RefInvasionRepeatSlider = (Slider)((FrameworkElement)this).FindName("InvasionRepeatSlider");
		((RangeBase)RefInvasionRepeatSlider).ValueChanged += ScenarioInvasionRepeatSlider_ValueChanged;
		RefInvasionSize = (Slider)((FrameworkElement)this).FindName("InvasionSlider");
		((RangeBase)RefInvasionSize).ValueChanged += ScenarioInvasionSizeSlider_ValueChanged;
		RefInvasionSizeArcherDefault = (RadioButton)((FrameworkElement)this).FindName("InvasionSizeArcherDefault");
		RefEventYear = (TextBox)((FrameworkElement)this).FindName("TextBoxStartingYearEvent");
		((UIElement)RefEventYear).PreviewTextInput += new TextCompositionEventHandler(NumberValidationTextBox);
		((UIElement)RefEventYear).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		RefScenarioEventActionList = (ListView)((FrameworkElement)this).FindName("ScenarioEventActionList");
		((Selector)RefScenarioEventActionList).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefScenarioEventActionList).SelectedItem != null)
			{
				((Selector)RefScenarioEventActionList).SelectedItem = null;
			}
		};
		RefActionRepeatMonthsSlider = (Slider)((FrameworkElement)this).FindName("ActionRepeatMonthsSlider");
		((RangeBase)RefActionRepeatMonthsSlider).ValueChanged += ActionRepeatMonthsSlider_ValueChanged;
		RefActionRepeatSlider = (Slider)((FrameworkElement)this).FindName("ActionRepeatSlider");
		((RangeBase)RefActionRepeatSlider).ValueChanged += ActionRepeatSlider_ValueChanged;
		RefActionValueSlider = (Slider)((FrameworkElement)this).FindName("ActionValueSlider");
		((RangeBase)RefActionValueSlider).ValueChanged += ActionValueSlider_ValueChanged;
		RefActionValue2Slider = (Slider)((FrameworkElement)this).FindName("ActionValue2Slider");
		((RangeBase)RefActionValue2Slider).ValueChanged += ActionValue2Slider_ValueChanged;
		RefScenarioEventConditionList = (ListView)((FrameworkElement)this).FindName("ScenarioEventConditionList");
		((Selector)RefScenarioEventConditionList).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefScenarioEventConditionList).SelectedItem != null)
			{
				((Selector)RefScenarioEventConditionList).SelectedItem = null;
			}
		};
		RefConditionValueSlider = (Slider)((FrameworkElement)this).FindName("ConditionValueSlider");
		((RangeBase)RefConditionValueSlider).ValueChanged += ConditionValueSlider_ValueChanged;
		RefSliderEditTeam1 = (Slider)((FrameworkElement)this).FindName("SliderEditTeam1");
		((RangeBase)RefSliderEditTeam1).ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam2 = (Slider)((FrameworkElement)this).FindName("SliderEditTeam2");
		((RangeBase)RefSliderEditTeam2).ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam3 = (Slider)((FrameworkElement)this).FindName("SliderEditTeam3");
		((RangeBase)RefSliderEditTeam3).ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam4 = (Slider)((FrameworkElement)this).FindName("SliderEditTeam4");
		((RangeBase)RefSliderEditTeam4).ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam5 = (Slider)((FrameworkElement)this).FindName("SliderEditTeam5");
		((RangeBase)RefSliderEditTeam5).ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam6 = (Slider)((FrameworkElement)this).FindName("SliderEditTeam6");
		((RangeBase)RefSliderEditTeam6).ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam7 = (Slider)((FrameworkElement)this).FindName("SliderEditTeam7");
		((RangeBase)RefSliderEditTeam7).ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefSliderEditTeam8 = (Slider)((FrameworkElement)this).FindName("SliderEditTeam8");
		((RangeBase)RefSliderEditTeam8).ValueChanged += ScenarioRefSliderEditTeam_ValueChanged;
		RefButtonEditText = (Button)((FrameworkElement)this).FindName("ButtonEditText");
		RefButtonStartingGoodsPresetLow = (Button)((FrameworkElement)this).FindName("ButtonStartingGoodsPresetLow");
		RefButtonStartingGoodsPresetMedium = (Button)((FrameworkElement)this).FindName("ButtonStartingGoodsPresetMedium");
		RefButtonStartingGoodsPresetHigh = (Button)((FrameworkElement)this).FindName("ButtonStartingGoodsPresetHigh");
		RefButtonInvasionInvPlayer1 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionInvPlayer1");
		RefButtonInvasionInvPlayer2 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionInvPlayer2");
		RefButtonInvasionInvPlayer3 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionInvPlayer3");
		RefButtonInvasionInvPlayer4 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionInvPlayer4");
		RefButtonInvasionInvPlayer5 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionInvPlayer5");
		RefButtonInvasionInvPlayer6 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionInvPlayer6");
		RefButtonInvasionInvPlayer7 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionInvPlayer7");
		RefButtonInvasionInvPlayer8 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionInvPlayer8");
		RefButtonInvasionReinPlayer1 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionReinPlayer1");
		RefButtonInvasionReinPlayer2 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionReinPlayer2");
		RefButtonInvasionReinPlayer3 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionReinPlayer3");
		RefButtonInvasionReinPlayer4 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionReinPlayer4");
		RefButtonInvasionReinPlayer5 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionReinPlayer5");
		RefButtonInvasionReinPlayer6 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionReinPlayer6");
		RefButtonInvasionReinPlayer7 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionReinPlayer7");
		RefButtonInvasionReinPlayer8 = (RadioButton)((FrameworkElement)this).FindName("ButtonInvasionReinPlayer8");
		if (FatControler.italian)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonEditText, 14);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonEditText, 20);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonStartingGoodsPresetLow, 14);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonStartingGoodsPresetLow, 20);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonStartingGoodsPresetMedium, 14);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonStartingGoodsPresetMedium, 20);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonStartingGoodsPresetHigh, 12);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonStartingGoodsPresetHigh, 18);
		}
		if (FatControler.portuguese)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonStartingGoodsPresetLow, 12);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonStartingGoodsPresetLow, 18);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonStartingGoodsPresetMedium, 12);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonStartingGoodsPresetMedium, 18);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonStartingGoodsPresetHigh, 12);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonStartingGoodsPresetHigh, 18);
			MainViewModel.Instance.ScenarioTradeTextSize = "12";
			MainViewModel.Instance.ScenarioTradeTextHeight = "18";
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefSignpost, 12);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefSignpost, 18);
		}
		if (FatControler.japanese)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonEditText, 12);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonEditText, 20);
		}
		if (FatControler.french)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonEditText, 14);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonEditText, 20);
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

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Scenario.xaml");
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

	public void StartEntryAnim()
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		MainViewModel.Instance.ShowingScenario = true;
		MainViewModel.Instance.HUD_Markers_Vis = false;
		((UIElement)MainViewModel.Instance.HUDScenario).IsEnabled = true;
		((UIElement)MainViewModel.Instance.HUDScenarioPopup).IsEnabled = true;
		((UIElement)RefViewRoot).Visibility = (Visibility)2;
		((Storyboard)((FrameworkElement)this).Resources[(object)"Intro"]).Begin((FrameworkElement)(object)this);
		MainViewModel.Instance.HUDScenarioPopup.StartEntryAnim();
		MainViewModel.Instance.ScenarioEditorButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 90);
	}

	public void StartExitAnim()
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		MainViewModel.Instance.ShowingScenario = false;
		((Storyboard)((FrameworkElement)this).Resources[(object)"Outtro"]).Begin((FrameworkElement)(object)this);
		MainViewModel.Instance.HUDScenarioPopup.StartExitAnim();
		MainViewModel.Instance.ScenarioEditorButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_SCENARIO_EDITOR);
	}

	public void initScenarioControls()
	{
		int.Parse(MainViewModel.Instance.ScenarioStartingGoldText, Director.defaultCulture);
		((RangeBase)RefStartingGoods).Value = MainViewModel.Instance.initSliderScenarioStartingGoods();
		FatControler.instance.InitScenarioEditorValues();
		((RangeBase)RefStartingPop).Value = int.Parse(MainViewModel.Instance.ScenarioStartingPopText, Director.defaultCulture);
		int currentValue = int.Parse(MainViewModel.Instance.ScenarioStartingSpecialGoldText, Director.defaultCulture);
		int maxValue = 10000;
		int freq = 1;
		currentValue = getLogSliderValue(currentValue, 1, ref maxValue, ref freq);
		((RangeBase)RefStartingSpecialGold).Value = currentValue;
		if (!GameData.Instance.multiplayerMap)
		{
			changeScenarioView(Enums.ScenarioViews.Main);
		}
		else
		{
			changeScenarioView(Enums.ScenarioViews.EditMessage);
		}
		((ToggleButton)RefFasterGoodsCheck).IsChecked = GameData.Instance.scenarioOverview.fast_goods_feedin > 0;
		SetupScenarioActionsList();
	}

	public void changeScenarioView(Enums.ScenarioViews newView, bool fromButton = true)
	{
		((UIElement)RefViewMain).Visibility = (Visibility)1;
		((UIElement)RefViewStartingGoods).Visibility = (Visibility)1;
		((UIElement)RefViewTradedGoods).Visibility = (Visibility)1;
		((UIElement)RefViewBuildingAvailibity).Visibility = (Visibility)1;
		((UIElement)RefViewInvasions).Visibility = (Visibility)1;
		MainViewModel.Instance.InvasionFromVis = false;
		((UIElement)RefViewEvents).Visibility = (Visibility)1;
		((UIElement)RefViewEventsConditions).Visibility = (Visibility)1;
		((UIElement)RefViewEventsActions).Visibility = (Visibility)1;
		((UIElement)RefViewEditMessage).Visibility = (Visibility)1;
		((UIElement)RefScenarioViewEditTeams).Visibility = (Visibility)1;
		((UIElement)RefScenarioViewAdjustDates).Visibility = (Visibility)1;
		((UIElement)RefLeftPanel).Visibility = (Visibility)2;
		MainViewModel.Instance.ScenarioBuildingTogglesVis = false;
		string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_SCENARIO_EDITOR);
		switch (newView)
		{
		case Enums.ScenarioViews.Main:
			RefreshScenarioActions();
			((Selector)RefScenarioActionList).SelectedItem = null;
			ResetListViewPosition(RefScenarioActionList);
			((UIElement)RefViewMain).Visibility = (Visibility)2;
			break;
		case Enums.ScenarioViews.StartingGoods:
			((ToggleButton)RefStartingGoodsGoldDefault).IsChecked = true;
			MainViewModel.Instance.ButtonScenarioStartingGoodSelect("15");
			((UIElement)RefViewStartingGoods).Visibility = (Visibility)2;
			break;
		case Enums.ScenarioViews.TradedGoods:
			((UIElement)RefViewTradedGoods).Visibility = (Visibility)2;
			break;
		case Enums.ScenarioViews.BuildingAvailibilty:
			ResetListViewPosition(RefScenarioBuildingList);
			((UIElement)RefViewBuildingAvailibity).Visibility = (Visibility)2;
			MainViewModel.Instance.ScenarioBuildingTogglesVis = true;
			break;
		case Enums.ScenarioViews.Invasions:
			text = text + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_INVASION);
			if (fromButton)
			{
				NewInvasion();
			}
			((ToggleButton)RefInvasionSizeArcherDefault).IsChecked = true;
			MainViewModel.Instance.ButtonSelectInvasionSize("0");
			((UIElement)RefLeftPanel).Visibility = (Visibility)1;
			((UIElement)RefViewInvasions).Visibility = (Visibility)2;
			break;
		case Enums.ScenarioViews.Events:
			text = text + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_EVENT);
			if (fromButton)
			{
				NewEvent();
			}
			((UIElement)RefLeftPanel).Visibility = (Visibility)1;
			((UIElement)RefViewEvents).Visibility = (Visibility)2;
			break;
		case Enums.ScenarioViews.EventsConditions:
			text = text + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_EVENT_CONDITIONS);
			((UIElement)RefLeftPanel).Visibility = (Visibility)1;
			((UIElement)RefViewEventsConditions).Visibility = (Visibility)2;
			PopulateEventConditions();
			break;
		case Enums.ScenarioViews.EventsActions:
			text = text + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_EVENT_ACTIONS);
			((UIElement)RefLeftPanel).Visibility = (Visibility)1;
			((UIElement)RefViewEventsActions).Visibility = (Visibility)2;
			PopulateEventActions();
			break;
		case Enums.ScenarioViews.EditMessage:
			((UIElement)RefViewEditMessage).Visibility = (Visibility)2;
			MainViewModel.Instance.ScenarioEditMessageText = GameData.Instance.utf8MissionText;
			if (GameData.Instance.showAlternateMissionTextForBriefing)
			{
				((FrameworkElement)RefEditMessage).Height = 200f;
				MainViewModel.Instance.ScenarioMessageAltTextIVisibilityBool = true;
				MainViewModel.Instance.ScenarioAltANSIMessage = GameData.Instance.ansiMissionText;
				MainViewModel.Instance.ScenarioAltUNICODEMessage = GameData.Instance.unicodeMissionText;
				GameData.Instance.showAlternateMissionTextForBriefing = false;
				GameData.Instance.ansiMissionText = "";
				GameData.Instance.unicodeMissionText = "";
			}
			else
			{
				((FrameworkElement)RefEditMessage).Height = 346f;
				MainViewModel.Instance.ScenarioMessageAltTextIVisibilityBool = false;
			}
			((UIElement)RefEditMessage).Focus();
			break;
		case Enums.ScenarioViews.EditTeams:
			((UIElement)RefScenarioViewEditTeams).Visibility = (Visibility)2;
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
				((RangeBase)RefSliderEditTeam1).Value = (int)GameData.Instance.lastGameState.starting_teams[1];
				((RangeBase)RefSliderEditTeam2).Value = (int)GameData.Instance.lastGameState.starting_teams[2];
				((RangeBase)RefSliderEditTeam3).Value = (int)GameData.Instance.lastGameState.starting_teams[3];
				((RangeBase)RefSliderEditTeam4).Value = (int)GameData.Instance.lastGameState.starting_teams[4];
				((RangeBase)RefSliderEditTeam5).Value = (int)GameData.Instance.lastGameState.starting_teams[5];
				((RangeBase)RefSliderEditTeam6).Value = (int)GameData.Instance.lastGameState.starting_teams[6];
				((RangeBase)RefSliderEditTeam7).Value = (int)GameData.Instance.lastGameState.starting_teams[7];
				((RangeBase)RefSliderEditTeam8).Value = (int)GameData.Instance.lastGameState.starting_teams[8];
				for (int i = 1; i < 9; i++)
				{
					MainViewModel.Instance.ScenarioEditTeams[i] = GameData.Instance.lastGameState.starting_teams[i].ToString();
				}
			}
			break;
		case Enums.ScenarioViews.AdjustDates:
			((UIElement)RefScenarioViewAdjustDates).Visibility = (Visibility)2;
			MainViewModel.Instance.ScenarioAdjustStartingYearText = MainViewModel.Instance.ScenarioStartingYearText;
			newStartingMonthValue = GameData.Instance.scenarioOverview.startMonth;
			MainViewModel.Instance.ScenarioAdjustStartingMonthText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, newStartingMonthValue);
			break;
		}
		MainViewModel.Instance.ScenarioEditorMode = newView;
		RefHeading.HeadingText = text;
	}

	public void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
	{
		Regex regex = new Regex("[^0-9]+");
		((RoutedEventArgs)e).Handled = regex.IsMatch(e.Text);
	}

	public void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	public void EditMessageTextChanged(object sender, RoutedEventArgs e)
	{
		string text = RefEditMessage.Text;
		GameData.Instance.utf8MissionText = text;
		EngineInterface.SetUTF8MissionText(text);
	}

	public void ScenarioPopularitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int newStartingPop = (int)((RangeBase)RefStartingPop).Value;
		MainViewModel.Instance.SliderScenarioStartPop(newStartingPop);
	}

	public void ScenarioSpecialGoldSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int sliderPos = (int)((RangeBase)RefStartingSpecialGold).Value;
		sliderPos = getLogSliderDislayValue(sliderPos, 10000);
		MainViewModel.Instance.SliderScenarioStartSpecialGold(sliderPos);
	}

	public void ScenarioStartingGoodsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int newStartingAmount = (int)((RangeBase)RefStartingGoods).Value;
		MainViewModel.Instance.SliderScenarioStartingGoods(newStartingAmount);
	}

	public void RefreshScenarioActions()
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
		((ItemsControl)RefScenarioActionList).ItemsSource = list;
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
		((FrameworkElement)RefEditMessage).Height = 346f;
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
		((ItemsControl)RefScenarioBuildingList).ItemsSource = BuildingItems;
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

	public int findBuildingItemRow(int ident)
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

	public void ResetListViewPosition(ListView lv)
	{
		if (((ItemsControl)lv).Items.Count > 0)
		{
			((ListBox)lv).ScrollIntoView(((ItemsControl)lv).Items[0]);
		}
	}

	public bool isEventPanelOpen()
	{
		return scenarioCurrentEvent != null;
	}

	public void SelectActionRow(int row)
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

	public void NewInvasion()
	{
		scenarioCurrentEvent = null;
		scenarioCurrentInvasion = EngineInterface.CreateNewScenarioInvasion(ref scenarioCurrentLine);
		PopulateInvasion();
	}

	public void NewEvent()
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
			((RangeBase)RefInvasionRepeatSlider).Value = scenarioCurrentInvasion.repeat;
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

	public void UpdateInvasionFromTitle()
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
			((ItemsControl)RefScenarioEventConditionList).ItemsSource = observableCollection;
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

	public void ManageEventConditionWinTimer()
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

	public void PopulateEventConditionLeftSide()
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
			((RangeBase)RefConditionValueSlider).Value = getLogSliderValue(num3, num4, ref maxValue, ref freq);
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

	public void UpdateEventConditionRow()
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
			activeEventConditionRow.BorderVisibility = (Visibility)2;
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
			activeEventConditionRow.BorderVisibility = (Visibility)1;
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
					scenarioEditorRow.BorderVisibility = (Visibility)2;
				}
				else
				{
					scenarioEditorRow.BorderVisibility = (Visibility)1;
				}
				scenarioEditorRow.DataValue = num.ToString();
				scenarioEditorRow.Text1 = text2;
				list.Add(scenarioEditorRow);
			}
			PopulateEventActionLeftSide();
			((ItemsControl)RefScenarioEventActionList).ItemsSource = list;
			if (activeEventActionRow != null)
			{
				((ListBox)RefScenarioEventActionList).ScrollIntoView((object)activeEventActionRow);
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

	public void PopulateEventActionLeftSide()
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
			((RangeBase)RefActionValueSlider).Value = scenarioCurrentEvent.action_data;
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
			((RangeBase)RefActionValueSlider).Value = scenarioCurrentEvent.action_data_marker;
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
			((RangeBase)RefActionValue2Slider).Value = scenarioCurrentEvent.action_data_reinforcement;
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
			((RangeBase)RefActionValueSlider).Value = scenarioCurrentEvent.action_data_marker;
			break;
		}
		default:
			MainViewModel.Instance.ScenarioEventActionValueVisibleBool = false;
			MainViewModel.Instance.ScenarioEventActionValue2VisibleBool = false;
			break;
		}
	}

	public void UpdateEventActionRow()
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
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Invalid comparison between Unknown and I4
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Invalid comparison between Unknown and I4
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Invalid comparison between Unknown and I4
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Invalid comparison between Unknown and I4
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Invalid comparison between Unknown and I4
		//IL_0350: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Invalid comparison between Unknown and I4
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Invalid comparison between Unknown and I4
		//IL_03b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Invalid comparison between Unknown and I4
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
				((UIElement)RefButtonInvasionInvPlayer2).Visibility = (Visibility)2;
				((UIElement)RefButtonInvasionReinPlayer2).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefButtonInvasionInvPlayer2).Visibility = (Visibility)1;
				((UIElement)RefButtonInvasionReinPlayer2).Visibility = (Visibility)2;
			}
			if (getStartingTeamForInvasions(3) != 1)
			{
				((UIElement)RefButtonInvasionInvPlayer3).Visibility = (Visibility)2;
				((UIElement)RefButtonInvasionReinPlayer3).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefButtonInvasionInvPlayer3).Visibility = (Visibility)1;
				((UIElement)RefButtonInvasionReinPlayer3).Visibility = (Visibility)2;
			}
			if (getStartingTeamForInvasions(4) != 1)
			{
				((UIElement)RefButtonInvasionInvPlayer4).Visibility = (Visibility)2;
				((UIElement)RefButtonInvasionReinPlayer4).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefButtonInvasionInvPlayer4).Visibility = (Visibility)1;
				((UIElement)RefButtonInvasionReinPlayer4).Visibility = (Visibility)2;
			}
			if (getStartingTeamForInvasions(5) != 1)
			{
				((UIElement)RefButtonInvasionInvPlayer5).Visibility = (Visibility)2;
				((UIElement)RefButtonInvasionReinPlayer5).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefButtonInvasionInvPlayer5).Visibility = (Visibility)1;
				((UIElement)RefButtonInvasionReinPlayer5).Visibility = (Visibility)2;
			}
			if (getStartingTeamForInvasions(6) != 1)
			{
				((UIElement)RefButtonInvasionInvPlayer6).Visibility = (Visibility)2;
				((UIElement)RefButtonInvasionReinPlayer6).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefButtonInvasionInvPlayer6).Visibility = (Visibility)1;
				((UIElement)RefButtonInvasionReinPlayer6).Visibility = (Visibility)2;
			}
			if (getStartingTeamForInvasions(7) != 1)
			{
				((UIElement)RefButtonInvasionInvPlayer7).Visibility = (Visibility)2;
				((UIElement)RefButtonInvasionReinPlayer7).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefButtonInvasionInvPlayer7).Visibility = (Visibility)1;
				((UIElement)RefButtonInvasionReinPlayer7).Visibility = (Visibility)2;
			}
			if (getStartingTeamForInvasions(8) != 1)
			{
				((UIElement)RefButtonInvasionInvPlayer8).Visibility = (Visibility)2;
				((UIElement)RefButtonInvasionReinPlayer8).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefButtonInvasionInvPlayer8).Visibility = (Visibility)1;
				((UIElement)RefButtonInvasionReinPlayer8).Visibility = (Visibility)2;
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
				if ((int)((UIElement)RefButtonInvasionInvPlayer1).Visibility == 2)
				{
					((ToggleButton)RefButtonInvasionInvPlayer1).IsChecked = true;
				}
				else
				{
					((ToggleButton)RefButtonInvasionReinPlayer1).IsChecked = true;
				}
				break;
			case 2:
				if ((int)((UIElement)RefButtonInvasionInvPlayer2).Visibility == 2)
				{
					((ToggleButton)RefButtonInvasionInvPlayer2).IsChecked = true;
				}
				else
				{
					((ToggleButton)RefButtonInvasionReinPlayer2).IsChecked = true;
				}
				break;
			case 3:
				if ((int)((UIElement)RefButtonInvasionInvPlayer3).Visibility == 2)
				{
					((ToggleButton)RefButtonInvasionInvPlayer3).IsChecked = true;
				}
				else
				{
					((ToggleButton)RefButtonInvasionReinPlayer3).IsChecked = true;
				}
				break;
			case 4:
				if ((int)((UIElement)RefButtonInvasionInvPlayer4).Visibility == 2)
				{
					((ToggleButton)RefButtonInvasionInvPlayer4).IsChecked = true;
				}
				else
				{
					((ToggleButton)RefButtonInvasionReinPlayer4).IsChecked = true;
				}
				break;
			case 5:
				if ((int)((UIElement)RefButtonInvasionInvPlayer5).Visibility == 2)
				{
					((ToggleButton)RefButtonInvasionInvPlayer5).IsChecked = true;
				}
				else
				{
					((ToggleButton)RefButtonInvasionReinPlayer5).IsChecked = true;
				}
				break;
			case 6:
				if ((int)((UIElement)RefButtonInvasionInvPlayer6).Visibility == 2)
				{
					((ToggleButton)RefButtonInvasionInvPlayer6).IsChecked = true;
				}
				else
				{
					((ToggleButton)RefButtonInvasionReinPlayer6).IsChecked = true;
				}
				break;
			case 7:
				if ((int)((UIElement)RefButtonInvasionInvPlayer7).Visibility == 2)
				{
					((ToggleButton)RefButtonInvasionInvPlayer7).IsChecked = true;
				}
				else
				{
					((ToggleButton)RefButtonInvasionReinPlayer7).IsChecked = true;
				}
				break;
			case 8:
				if ((int)((UIElement)RefButtonInvasionInvPlayer8).Visibility == 2)
				{
					((ToggleButton)RefButtonInvasionInvPlayer8).IsChecked = true;
				}
				else
				{
					((ToggleButton)RefButtonInvasionReinPlayer8).IsChecked = true;
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
		foreach (ScenarioEditorRow item in ((ItemsControl)RefScenarioEventActionList).ItemsSource)
		{
			item.Text2 = "";
			if (item.DataValue == text)
			{
				item.BorderVisibility = (Visibility)2;
				activeEventActionRow = item;
			}
			else
			{
				item.BorderVisibility = (Visibility)1;
			}
		}
		PopulateEventActionLeftSide();
		UpdateEventActionRow();
	}

	public void EventConditionSelected(int conditionID)
	{
		string text = conditionID.ToString();
		foreach (ScenarioEditorRow item in ((ItemsControl)RefScenarioEventConditionList).ItemsSource)
		{
			if (item.DataValue == text)
			{
				activeEventConditionRow = item;
			}
		}
		PopulateEventConditionLeftSide();
	}

	public void ScenarioInvasionRepeatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int repeat = (int)((RangeBase)RefInvasionRepeatSlider).Value;
		if (scenarioCurrentInvasion != null)
		{
			scenarioCurrentInvasion.repeat = repeat;
		}
	}

	public void ScenarioInvasionSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefInvasionSize).Value;
		if (scenarioCurrentInvasion != null)
		{
			scenarioCurrentInvasion._size[MainViewModel.Instance.ScenarioInvasionSizeType] = num;
		}
	}

	public void ActionRepeatMonthsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefActionRepeatMonthsSlider).Value;
		if (scenarioCurrentEvent != null)
		{
			scenarioCurrentEvent.repeat = (byte)num;
		}
	}

	public void ActionRepeatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefActionRepeatSlider).Value;
		if (scenarioCurrentEvent != null)
		{
			scenarioCurrentEvent.repeat_count = (byte)num;
		}
	}

	public void ActionValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefActionValueSlider).Value;
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

	public void ActionValue2Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefActionValue2Slider).Value;
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

	public void ConditionValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int sliderPos = (int)((RangeBase)RefConditionValueSlider).Value;
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

	public void ScenarioRefSliderEditTeam_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (FatControler.currentScene != 0)
		{
			int num = 0;
			switch (((FrameworkElement)(Slider)((RoutedEventArgs)e).Source).Name)
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

	public void FasterGoodsCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (((ToggleButton)RefFasterGoodsCheck).IsChecked.Value)
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
