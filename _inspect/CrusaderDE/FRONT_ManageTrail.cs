using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_ManageTrail : UserControl
{
	public ListView RefImportList;

	public ListView RefExportList;

	public RadioButton RefTrail_1;

	public RadioButton RefTrail_2;

	public RadioButton RefTrail_3;

	public RadioButton RefTrail_4;

	public RadioButton RefTrail_5;

	public RadioButton RefTrail_6;

	public RadioButton RefTrail_7;

	public RadioButton RefTrail_8;

	public RadioButton RefTrail_9;

	public RadioButton RefTrail_10;

	public RadioButton RefTrail_11;

	public RadioButton RefTrail_12;

	public RadioButton RefTrail_13;

	public RadioButton RefTrail_14;

	public RadioButton RefTrail_15;

	public RadioButton RefTrail_16;

	public RadioButton RefTrail_17;

	public RadioButton RefTrail_18;

	public RadioButton RefTrail_19;

	public RadioButton RefTrail_20;

	public RadioButton RefTrail_21;

	public RadioButton RefTrail_22;

	public RadioButton RefTrail_23;

	public RadioButton RefTrail_24;

	public RadioButton RefTrail_25;

	public RadioButton RefTrail_26;

	public RadioButton RefTrail_27;

	public RadioButton RefTrail_28;

	public RadioButton RefTrail_29;

	public RadioButton RefTrail_30;

	public RadioButton RefTrail_31;

	public RadioButton RefTrail_32;

	public RadioButton RefTrail_33;

	public RadioButton RefTrail_34;

	public RadioButton RefTrail_35;

	public RadioButton RefTrail_36;

	public RadioButton RefTrail_37;

	public RadioButton RefTrail_38;

	public RadioButton RefTrail_39;

	public RadioButton RefTrail_40;

	public RadioButton RefTrail_41;

	public RadioButton RefTrail_42;

	public RadioButton RefTrail_43;

	public RadioButton RefTrail_44;

	public RadioButton RefTrail_45;

	public RadioButton RefTrail_46;

	public RadioButton RefTrail_47;

	public RadioButton RefTrail_48;

	public RadioButton RefTrail_49;

	public RadioButton RefTrail_50;

	public Button RefLoad;

	public Button RefSave;

	public Button RefImport;

	public Button RefExport;

	public Button RefClear;

	public Button RefClearMission;

	public Button RefImportImportButton;

	public Button RefExportExportButton;

	public CheckBox RefExportBackup;

	public CheckBox RefImportBackup;

	public TextBox RefExportTrailName;

	public RadioButton lastMissionButton;

	public int lastMissionID = -1;

	public bool clearMakeBackup = true;

	public static FRONT_ManageTrail instance1 = null;

	public FRONT_Multiplayer.MPAIVInfo AIVInfo;

	public int SelectedMission = -1;

	public ObservableCollection<FileRow> importRows = new ObservableCollection<FileRow>();

	public ObservableCollection<FileRow> exportRows = new ObservableCollection<FileRow>();

	public List<FileHeader> makerFiles;

	public bool[] exists = new bool[50];

	public static string[] makerFileNames = new string[50]
	{
		"Trail_Mission_1", "Trail_Mission_2", "Trail_Mission_3", "Trail_Mission_4", "Trail_Mission_5", "Trail_Mission_6", "Trail_Mission_7", "Trail_Mission_8", "Trail_Mission_9", "Trail_Mission_10",
		"Trail_Mission_11", "Trail_Mission_12", "Trail_Mission_13", "Trail_Mission_14", "Trail_Mission_15", "Trail_Mission_16", "Trail_Mission_17", "Trail_Mission_18", "Trail_Mission_19", "Trail_Mission_20",
		"Trail_Mission_21", "Trail_Mission_22", "Trail_Mission_23", "Trail_Mission_24", "Trail_Mission_25", "Trail_Mission_26", "Trail_Mission_27", "Trail_Mission_28", "Trail_Mission_29", "Trail_Mission_30",
		"Trail_Mission_31", "Trail_Mission_32", "Trail_Mission_33", "Trail_Mission_34", "Trail_Mission_35", "Trail_Mission_36", "Trail_Mission_37", "Trail_Mission_38", "Trail_Mission_39", "Trail_Mission_40",
		"Trail_Mission_41", "Trail_Mission_42", "Trail_Mission_43", "Trail_Mission_44", "Trail_Mission_45", "Trail_Mission_46", "Trail_Mission_47", "Trail_Mission_48", "Trail_Mission_49", "Trail_Mission_50"
	};

	public static FRONT_ManageTrail Instance => instance1;

	public FRONT_ManageTrail()
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Expected O, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Expected O, but got Unknown
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Expected O, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected O, but got Unknown
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Expected O, but got Unknown
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Expected O, but got Unknown
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Expected O, but got Unknown
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Expected O, but got Unknown
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Expected O, but got Unknown
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Expected O, but got Unknown
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Expected O, but got Unknown
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Expected O, but got Unknown
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Expected O, but got Unknown
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Expected O, but got Unknown
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Expected O, but got Unknown
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Expected O, but got Unknown
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Expected O, but got Unknown
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Expected O, but got Unknown
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Expected O, but got Unknown
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Expected O, but got Unknown
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Expected O, but got Unknown
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Expected O, but got Unknown
		//IL_02be: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Expected O, but got Unknown
		//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Expected O, but got Unknown
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Expected O, but got Unknown
		//IL_0300: Unknown result type (might be due to invalid IL or missing references)
		//IL_030a: Expected O, but got Unknown
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Expected O, but got Unknown
		//IL_032c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Expected O, but got Unknown
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		//IL_034c: Expected O, but got Unknown
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Expected O, but got Unknown
		//IL_036e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0378: Expected O, but got Unknown
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Expected O, but got Unknown
		//IL_039a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Expected O, but got Unknown
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ba: Expected O, but got Unknown
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Expected O, but got Unknown
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e6: Expected O, but got Unknown
		//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fc: Expected O, but got Unknown
		//IL_0408: Unknown result type (might be due to invalid IL or missing references)
		//IL_0412: Expected O, but got Unknown
		//IL_041e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0428: Expected O, but got Unknown
		//IL_0434: Unknown result type (might be due to invalid IL or missing references)
		//IL_043e: Expected O, but got Unknown
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0454: Expected O, but got Unknown
		//IL_0460: Unknown result type (might be due to invalid IL or missing references)
		//IL_046a: Expected O, but got Unknown
		//IL_0476: Unknown result type (might be due to invalid IL or missing references)
		//IL_0480: Expected O, but got Unknown
		//IL_048c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0496: Expected O, but got Unknown
		//IL_04a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Expected O, but got Unknown
		//IL_04b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c2: Expected O, but got Unknown
		//IL_04ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d8: Expected O, but got Unknown
		//IL_04e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ee: Expected O, but got Unknown
		//IL_04fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0504: Expected O, but got Unknown
		//IL_0510: Unknown result type (might be due to invalid IL or missing references)
		//IL_051a: Expected O, but got Unknown
		//IL_0526: Unknown result type (might be due to invalid IL or missing references)
		//IL_0530: Expected O, but got Unknown
		//IL_053c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0546: Expected O, but got Unknown
		//IL_0552: Unknown result type (might be due to invalid IL or missing references)
		//IL_055c: Expected O, but got Unknown
		//IL_0568: Unknown result type (might be due to invalid IL or missing references)
		//IL_0572: Expected O, but got Unknown
		//IL_057e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0588: Expected O, but got Unknown
		//IL_0593: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d7: Expected O, but got Unknown
		//IL_05e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ed: Expected O, but got Unknown
		//IL_05f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_060d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0632: Unknown result type (might be due to invalid IL or missing references)
		//IL_063c: Expected O, but got Unknown
		//IL_0648: Unknown result type (might be due to invalid IL or missing references)
		//IL_0652: Expected O, but got Unknown
		//IL_065f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0669: Expected O, but got Unknown
		//IL_0676: Unknown result type (might be due to invalid IL or missing references)
		//IL_0680: Expected O, but got Unknown
		//IL_068d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0697: Expected O, but got Unknown
		//IL_06a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ae: Expected O, but got Unknown
		//IL_06bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c5: Expected O, but got Unknown
		instance1 = this;
		InitializeComponent();
		RefTrail_1 = (RadioButton)((FrameworkElement)this).FindName("Trail_1");
		RefTrail_2 = (RadioButton)((FrameworkElement)this).FindName("Trail_2");
		RefTrail_3 = (RadioButton)((FrameworkElement)this).FindName("Trail_3");
		RefTrail_4 = (RadioButton)((FrameworkElement)this).FindName("Trail_4");
		RefTrail_5 = (RadioButton)((FrameworkElement)this).FindName("Trail_5");
		RefTrail_6 = (RadioButton)((FrameworkElement)this).FindName("Trail_6");
		RefTrail_7 = (RadioButton)((FrameworkElement)this).FindName("Trail_7");
		RefTrail_8 = (RadioButton)((FrameworkElement)this).FindName("Trail_8");
		RefTrail_9 = (RadioButton)((FrameworkElement)this).FindName("Trail_9");
		RefTrail_10 = (RadioButton)((FrameworkElement)this).FindName("Trail_10");
		RefTrail_11 = (RadioButton)((FrameworkElement)this).FindName("Trail_11");
		RefTrail_12 = (RadioButton)((FrameworkElement)this).FindName("Trail_12");
		RefTrail_13 = (RadioButton)((FrameworkElement)this).FindName("Trail_13");
		RefTrail_14 = (RadioButton)((FrameworkElement)this).FindName("Trail_14");
		RefTrail_15 = (RadioButton)((FrameworkElement)this).FindName("Trail_15");
		RefTrail_16 = (RadioButton)((FrameworkElement)this).FindName("Trail_16");
		RefTrail_17 = (RadioButton)((FrameworkElement)this).FindName("Trail_17");
		RefTrail_18 = (RadioButton)((FrameworkElement)this).FindName("Trail_18");
		RefTrail_19 = (RadioButton)((FrameworkElement)this).FindName("Trail_19");
		RefTrail_20 = (RadioButton)((FrameworkElement)this).FindName("Trail_20");
		RefTrail_21 = (RadioButton)((FrameworkElement)this).FindName("Trail_21");
		RefTrail_22 = (RadioButton)((FrameworkElement)this).FindName("Trail_22");
		RefTrail_23 = (RadioButton)((FrameworkElement)this).FindName("Trail_23");
		RefTrail_24 = (RadioButton)((FrameworkElement)this).FindName("Trail_24");
		RefTrail_25 = (RadioButton)((FrameworkElement)this).FindName("Trail_25");
		RefTrail_26 = (RadioButton)((FrameworkElement)this).FindName("Trail_26");
		RefTrail_27 = (RadioButton)((FrameworkElement)this).FindName("Trail_27");
		RefTrail_28 = (RadioButton)((FrameworkElement)this).FindName("Trail_28");
		RefTrail_29 = (RadioButton)((FrameworkElement)this).FindName("Trail_29");
		RefTrail_30 = (RadioButton)((FrameworkElement)this).FindName("Trail_30");
		RefTrail_31 = (RadioButton)((FrameworkElement)this).FindName("Trail_31");
		RefTrail_32 = (RadioButton)((FrameworkElement)this).FindName("Trail_32");
		RefTrail_33 = (RadioButton)((FrameworkElement)this).FindName("Trail_33");
		RefTrail_34 = (RadioButton)((FrameworkElement)this).FindName("Trail_34");
		RefTrail_35 = (RadioButton)((FrameworkElement)this).FindName("Trail_35");
		RefTrail_36 = (RadioButton)((FrameworkElement)this).FindName("Trail_36");
		RefTrail_37 = (RadioButton)((FrameworkElement)this).FindName("Trail_37");
		RefTrail_38 = (RadioButton)((FrameworkElement)this).FindName("Trail_38");
		RefTrail_39 = (RadioButton)((FrameworkElement)this).FindName("Trail_39");
		RefTrail_40 = (RadioButton)((FrameworkElement)this).FindName("Trail_40");
		RefTrail_41 = (RadioButton)((FrameworkElement)this).FindName("Trail_41");
		RefTrail_42 = (RadioButton)((FrameworkElement)this).FindName("Trail_42");
		RefTrail_43 = (RadioButton)((FrameworkElement)this).FindName("Trail_43");
		RefTrail_44 = (RadioButton)((FrameworkElement)this).FindName("Trail_44");
		RefTrail_45 = (RadioButton)((FrameworkElement)this).FindName("Trail_45");
		RefTrail_46 = (RadioButton)((FrameworkElement)this).FindName("Trail_46");
		RefTrail_47 = (RadioButton)((FrameworkElement)this).FindName("Trail_47");
		RefTrail_48 = (RadioButton)((FrameworkElement)this).FindName("Trail_48");
		RefTrail_49 = (RadioButton)((FrameworkElement)this).FindName("Trail_49");
		RefTrail_50 = (RadioButton)((FrameworkElement)this).FindName("Trail_50");
		RefLoad = (Button)((FrameworkElement)this).FindName("Load");
		RefSave = (Button)((FrameworkElement)this).FindName("Save");
		RefImport = (Button)((FrameworkElement)this).FindName("Import");
		RefExport = (Button)((FrameworkElement)this).FindName("Export");
		RefClear = (Button)((FrameworkElement)this).FindName("Clear");
		RefClearMission = (Button)((FrameworkElement)this).FindName("ClearMission");
		RefImportImportButton = (Button)((FrameworkElement)this).FindName("ImportImportButton");
		RefExportExportButton = (Button)((FrameworkElement)this).FindName("ExportExportButton");
		RefExportBackup = (CheckBox)((FrameworkElement)this).FindName("ExportBackup");
		RefImportBackup = (CheckBox)((FrameworkElement)this).FindName("ImportBackup");
		RefImportList = (ListView)((FrameworkElement)this).FindName("ImportList");
		((ContentControl)(GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)((GridView)RefImportList.View).Columns)[2].Header).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		((Selector)RefImportList).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefImportList).SelectedItem != null)
			{
				((UIElement)RefImportImportButton).IsEnabled = true;
				((UIElement)RefImportImportButton).Opacity = 1f;
			}
		};
		RefExportList = (ListView)((FrameworkElement)this).FindName("ExportList");
		((ContentControl)(GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)((GridView)RefExportList.View).Columns)[2].Header).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		((Selector)RefExportList).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefExportList).SelectedItem != null)
			{
				RefExportTrailName.Text = ((FileRow)((Selector)RefExportList).SelectedItem).Text1;
			}
		};
		RefExportTrailName = (TextBox)((FrameworkElement)this).FindName("ExportTrailName");
		((UIElement)RefExportTrailName).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefExportTrailName).TextChanged += new RoutedEventHandler(TextChangedHandler);
		((FrameworkElement)RefExportTrailName).Loaded += new RoutedEventHandler(TextBoxLoaded);
		((UIElement)RefExportTrailName).PreviewTextInput += new TextCompositionEventHandler(FileNameValidationTextBox);
		((UIElement)RefExportTrailName).PreviewKeyDown += new KeyEventHandler(TextBoxCheckForEscape);
	}

	public static void Show(bool canSave)
	{
		MainViewModel.Instance.Show_ManageTrail = true;
		Instance.Init(canSave);
	}

	public void Init(bool canSave)
	{
		((UIElement)RefSave).IsEnabled = canSave;
		((UIElement)RefSave).Opacity = (canSave ? 1f : 0.7f);
		MainViewModel.Instance.Show_TM_Import = false;
		MainViewModel.Instance.Show_TM_Export = false;
		if (MapFileManager.Instance.GetCustomTrailsCount() > 0)
		{
			((UIElement)RefImport).IsEnabled = true;
			((UIElement)RefImport).Opacity = 1f;
		}
		else
		{
			((UIElement)RefImport).IsEnabled = false;
			((UIElement)RefImport).Opacity = 0.7f;
		}
		lastMissionButton = null;
		lastMissionID = -1;
		makerFiles = MapFileManager.Instance.GetTrailMakerFiles(0, sortAscend: true);
		for (int i = 0; i < 50; i++)
		{
			exists[i] = false;
		}
		foreach (FileHeader makerFile in makerFiles)
		{
			switch (makerFile.fileName.ToLowerInvariant())
			{
			case "trail_mission_1":
				exists[0] = true;
				break;
			case "trail_mission_2":
				exists[1] = true;
				break;
			case "trail_mission_3":
				exists[2] = true;
				break;
			case "trail_mission_4":
				exists[3] = true;
				break;
			case "trail_mission_5":
				exists[4] = true;
				break;
			case "trail_mission_6":
				exists[5] = true;
				break;
			case "trail_mission_7":
				exists[6] = true;
				break;
			case "trail_mission_8":
				exists[7] = true;
				break;
			case "trail_mission_9":
				exists[8] = true;
				break;
			case "trail_mission_10":
				exists[9] = true;
				break;
			case "trail_mission_11":
				exists[10] = true;
				break;
			case "trail_mission_12":
				exists[11] = true;
				break;
			case "trail_mission_13":
				exists[12] = true;
				break;
			case "trail_mission_14":
				exists[13] = true;
				break;
			case "trail_mission_15":
				exists[14] = true;
				break;
			case "trail_mission_16":
				exists[15] = true;
				break;
			case "trail_mission_17":
				exists[16] = true;
				break;
			case "trail_mission_18":
				exists[17] = true;
				break;
			case "trail_mission_19":
				exists[18] = true;
				break;
			case "trail_mission_20":
				exists[19] = true;
				break;
			case "trail_mission_21":
				exists[20] = true;
				break;
			case "trail_mission_22":
				exists[21] = true;
				break;
			case "trail_mission_23":
				exists[22] = true;
				break;
			case "trail_mission_24":
				exists[23] = true;
				break;
			case "trail_mission_25":
				exists[24] = true;
				break;
			case "trail_mission_26":
				exists[25] = true;
				break;
			case "trail_mission_27":
				exists[26] = true;
				break;
			case "trail_mission_28":
				exists[27] = true;
				break;
			case "trail_mission_29":
				exists[28] = true;
				break;
			case "trail_mission_30":
				exists[29] = true;
				break;
			case "trail_mission_31":
				exists[30] = true;
				break;
			case "trail_mission_32":
				exists[31] = true;
				break;
			case "trail_mission_33":
				exists[32] = true;
				break;
			case "trail_mission_34":
				exists[33] = true;
				break;
			case "trail_mission_35":
				exists[34] = true;
				break;
			case "trail_mission_36":
				exists[35] = true;
				break;
			case "trail_mission_37":
				exists[36] = true;
				break;
			case "trail_mission_38":
				exists[37] = true;
				break;
			case "trail_mission_39":
				exists[38] = true;
				break;
			case "trail_mission_40":
				exists[39] = true;
				break;
			case "trail_mission_41":
				exists[40] = true;
				break;
			case "trail_mission_42":
				exists[41] = true;
				break;
			case "trail_mission_43":
				exists[42] = true;
				break;
			case "trail_mission_44":
				exists[43] = true;
				break;
			case "trail_mission_45":
				exists[44] = true;
				break;
			case "trail_mission_46":
				exists[45] = true;
				break;
			case "trail_mission_47":
				exists[46] = true;
				break;
			case "trail_mission_48":
				exists[47] = true;
				break;
			case "trail_mission_49":
				exists[48] = true;
				break;
			case "trail_mission_50":
				exists[49] = true;
				break;
			}
		}
		for (int j = 0; j < 50; j++)
		{
			RadioButton button = GetButton(j);
			ImageSource value;
			if (exists[j])
			{
				value = MainViewModel.Instance.GameSprites[734];
				((UIElement)button).Opacity = 1f;
			}
			else
			{
				value = null;
				((UIElement)button).Opacity = 0.7f;
			}
			PropEx.SetSprite1((UIElement)(object)button, value);
			if (((ToggleButton)button).IsChecked.Value)
			{
				SelectMission(j);
			}
		}
		UpdateMainButtons();
	}

	public RadioButton GetButton(int index)
	{
		return (RadioButton)(index switch
		{
			0 => RefTrail_1, 
			1 => RefTrail_2, 
			2 => RefTrail_3, 
			3 => RefTrail_4, 
			4 => RefTrail_5, 
			5 => RefTrail_6, 
			6 => RefTrail_7, 
			7 => RefTrail_8, 
			8 => RefTrail_9, 
			9 => RefTrail_10, 
			10 => RefTrail_11, 
			11 => RefTrail_12, 
			12 => RefTrail_13, 
			13 => RefTrail_14, 
			14 => RefTrail_15, 
			15 => RefTrail_16, 
			16 => RefTrail_17, 
			17 => RefTrail_18, 
			18 => RefTrail_19, 
			19 => RefTrail_20, 
			20 => RefTrail_21, 
			21 => RefTrail_22, 
			22 => RefTrail_23, 
			23 => RefTrail_24, 
			24 => RefTrail_25, 
			25 => RefTrail_26, 
			26 => RefTrail_27, 
			27 => RefTrail_28, 
			28 => RefTrail_29, 
			29 => RefTrail_30, 
			30 => RefTrail_31, 
			31 => RefTrail_32, 
			32 => RefTrail_33, 
			33 => RefTrail_34, 
			34 => RefTrail_35, 
			35 => RefTrail_36, 
			36 => RefTrail_37, 
			37 => RefTrail_38, 
			38 => RefTrail_39, 
			39 => RefTrail_40, 
			40 => RefTrail_41, 
			41 => RefTrail_42, 
			42 => RefTrail_43, 
			43 => RefTrail_44, 
			44 => RefTrail_45, 
			45 => RefTrail_46, 
			46 => RefTrail_47, 
			47 => RefTrail_48, 
			48 => RefTrail_49, 
			49 => RefTrail_50, 
			_ => null, 
		});
	}

	public static string GetMakerFileName(int index)
	{
		if (index >= 0 && index < 50)
		{
			return makerFileNames[index];
		}
		return "";
	}

	public void SelectMission(int index)
	{
		if ((BaseComponent)(object)lastMissionButton != (BaseComponent)null && lastMissionID >= 0 && !exists[lastMissionID])
		{
			((UIElement)lastMissionButton).Opacity = 0.7f;
		}
		((UIElement)RefLoad).IsEnabled = exists[index];
		((UIElement)RefLoad).Opacity = (exists[index] ? 1f : 0.7f);
		((UIElement)RefClearMission).IsEnabled = exists[index];
		((UIElement)RefClearMission).Opacity = (exists[index] ? 1f : 0.7f);
		SelectedMission = index;
		MainViewModel.Instance.TM_SelectMissionText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 39) + " " + (index + 1);
		RadioButton button = GetButton(index);
		((UIElement)button).Opacity = 1f;
		lastMissionButton = button;
		lastMissionID = index;
		MainViewModel.Instance.TM_SelectedMap = "";
		MainViewModel.Instance.TM_SelectedOpponent1 = "";
		MainViewModel.Instance.TM_SelectedOpponent2 = "";
		MainViewModel.Instance.TM_SelectedOpponent3 = "";
		MainViewModel.Instance.TM_SelectedOpponent4 = "";
		MainViewModel.Instance.TM_SelectedOpponent5 = "";
		MainViewModel.Instance.TM_SelectedOpponent6 = "";
		MainViewModel.Instance.TM_SelectedOpponent7 = "";
		if (!exists[index])
		{
			return;
		}
		FileHeader headerFromTrailMaker = MapFileManager.Instance.GetHeaderFromTrailMaker(GetMakerFileName(SelectedMission));
		if (headerFromTrailMaker == null || !headerFromTrailMaker.hasRestartSkirmishInfo)
		{
			return;
		}
		FileHeader fileInfoFromFileName = MapFileManager.Instance.GetFileInfoFromFileName(headerFromTrailMaker.filePath, headerFromTrailMaker.filePath, 0, loadRestartInfo: true);
		if (fileInfoFromFileName.restartSkirmishInfo == null)
		{
			return;
		}
		MainViewModel.Instance.TM_SelectedMap = fileInfoFromFileName.restartSkirmishInfo.selectedHeader.display_filename;
		for (int i = 1; i < fileInfoFromFileName.restartSkirmishInfo.lordTypes.Count; i++)
		{
			string text = "";
			if (fileInfoFromFileName.restartSkirmishInfo.aivs[i].lordName.Length > 0)
			{
				text = fileInfoFromFileName.restartSkirmishInfo.aivs[i].lordName;
			}
			else
			{
				switch ((fileInfoFromFileName.restartSkirmishInfo.lordTypes[i] - 1) / 8)
				{
				case 0:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_SELECT_SWORDSMEN);
					break;
				case 1:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_XBOWMEN);
					break;
				case 2:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_REPAIR);
					break;
				case 3:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_FRONTEND_BUILDER_SHIELD1);
					break;
				case 4:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_STRETCHING_RACK);
					break;
				case 5:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_POND);
					break;
				case 6:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_SELECT_CATAPULTS);
					break;
				case 7:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_BRIEF_STARTGAME);
					break;
				case 8:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_FE_SECTION_2);
					break;
				case 9:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_ARAB_SWORDSMAN);
					break;
				case 10:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_SELECT_ARAB_SWORDSMAN);
					break;
				case 11:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_ADD_CPU);
					break;
				case 12:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_FRONTEND_SHIELDX3);
					break;
				case 13:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_BEDOUIN_DEMOLISHER);
					break;
				case 14:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_WR_AI_TYPE15);
					break;
				case 15:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_WR_AI_TYPE16);
					break;
				case 16:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 221);
					break;
				case 17:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 222);
					break;
				case 18:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 223);
					break;
				case 19:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 224);
					break;
				case 20:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 225);
					break;
				case 21:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 226);
					break;
				case 22:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 227);
					break;
				case 23:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 228);
					break;
				case 24:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 229);
					break;
				case 25:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 453);
					break;
				case 26:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 470);
					break;
				case 27:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 487);
					break;
				case 28:
					text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 504);
					break;
				}
			}
			switch (i)
			{
			case 1:
				MainViewModel.Instance.TM_SelectedOpponent1 = text;
				break;
			case 2:
				MainViewModel.Instance.TM_SelectedOpponent2 = text;
				break;
			case 3:
				MainViewModel.Instance.TM_SelectedOpponent3 = text;
				break;
			case 4:
				MainViewModel.Instance.TM_SelectedOpponent4 = text;
				break;
			case 5:
				MainViewModel.Instance.TM_SelectedOpponent5 = text;
				break;
			case 6:
				MainViewModel.Instance.TM_SelectedOpponent6 = text;
				break;
			case 7:
				MainViewModel.Instance.TM_SelectedOpponent7 = text;
				break;
			}
		}
	}

	public void UpdateMainButtons()
	{
		int num = 0;
		for (int i = 0; i < 50; i++)
		{
			if (exists[i])
			{
				num++;
			}
		}
		if (num > 0)
		{
			Button refClear = RefClear;
			bool isEnabled = (((UIElement)RefExport).IsEnabled = true);
			((UIElement)refClear).IsEnabled = isEnabled;
			Button refClear2 = RefClear;
			float opacity = (((UIElement)RefExport).Opacity = 1f);
			((UIElement)refClear2).Opacity = opacity;
		}
		else
		{
			Button refClear3 = RefClear;
			bool isEnabled = (((UIElement)RefExport).IsEnabled = false);
			((UIElement)refClear3).IsEnabled = isEnabled;
			Button refClear4 = RefClear;
			float opacity = (((UIElement)RefExport).Opacity = 0.7f);
			((UIElement)refClear4).Opacity = opacity;
		}
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "Load":
		{
			if (SelectedMission < 0 || SelectedMission >= 50)
			{
				break;
			}
			FileHeader headerFromTrailMaker = MapFileManager.Instance.GetHeaderFromTrailMaker(GetMakerFileName(SelectedMission));
			if (headerFromTrailMaker == null)
			{
				makerFiles = MapFileManager.Instance.GetTrailMakerFiles(0, sortAscend: true);
				headerFromTrailMaker = MapFileManager.Instance.GetHeaderFromTrailMaker(GetMakerFileName(SelectedMission));
			}
			if (headerFromTrailMaker == null || !headerFromTrailMaker.hasRestartSkirmishInfo)
			{
				break;
			}
			FileHeader fileInfoFromFileName = MapFileManager.Instance.GetFileInfoFromFileName(headerFromTrailMaker.filePath, headerFromTrailMaker.filePath, 3, loadRestartInfo: true);
			if (fileInfoFromFileName.restartSkirmishInfo == null)
			{
				break;
			}
			if (fileInfoFromFileName.restartSkirmishInfo.aivs != null)
			{
				for (int num = 0; num < 8; num++)
				{
					if (!fileInfoFromFileName.restartSkirmishInfo.aivs[num].builtIn && fileInfoFromFileName.restartSkirmishInfo.aivs[num].lordName.Length > 0)
					{
						byte[] imageData = null;
						TextureSource customLordImage = CustomisationFileManager.Instance.GetCustomLordImage(fileInfoFromFileName.restartSkirmishInfo.aivs[num].lordName, ref imageData);
						if ((BaseComponent)(object)customLordImage != (BaseComponent)null)
						{
							fileInfoFromFileName.restartSkirmishInfo.aivs[num].image = customLordImage;
							fileInfoFromFileName.restartSkirmishInfo.aivs[num].imageData = imageData;
						}
					}
				}
			}
			FRONT_Multiplayer.Open(skirmishSetup: true, fileInfoFromFileName.restartSkirmishInfo, coopSetup: false, trailMaker: true);
			break;
		}
		case "Save":
			if (SelectedMission < 0 || SelectedMission >= 50)
			{
				break;
			}
			if (MapFileManager.Instance.GetHeaderFromTrailMaker(GetMakerFileName(SelectedMission)) != null)
			{
				HUD_ConfirmationPopup.ShowConfirmationMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 37), delegate
				{
					SaveMission();
				}, delegate
				{
				}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 38), MPConf: true);
			}
			else
			{
				SaveMission();
			}
			break;
		case "ClearMission":
			HUD_ConfirmationPopup.ShowConfirmationMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 72) + " : " + (SelectedMission + 1), delegate
			{
				_ = ((ToggleButton)RefImportBackup).IsChecked.Value;
				try
				{
					File.Delete(Path.Combine(ConfigSettings.GetUserTrailMakerPath(), GetMakerFileName(SelectedMission) + ".trail"));
				}
				catch (Exception)
				{
				}
				MapFileManager.Instance.RescanTrailMakerFolder();
				Init(((UIElement)RefSave).IsEnabled);
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 73), MPConf: true, tall: true);
			break;
		case "OpenMaker":
			try
			{
				string userTrailMakerPath = ConfigSettings.GetUserTrailMakerPath();
				Application.OpenURL("file://" + userTrailMakerPath);
				break;
			}
			catch (Exception)
			{
				break;
			}
		case "OpenCustom":
			try
			{
				string userCustomTrailsPath = ConfigSettings.GetUserCustomTrailsPath();
				Application.OpenURL("file://" + userCustomTrailsPath);
				break;
			}
			catch (Exception)
			{
				break;
			}
		case "Import":
			importRows.Clear();
			foreach (MapFileManager.CustomTrailInfo customTrail in MapFileManager.Instance.GetCustomTrails(ignoreWorkshopTrails: true))
			{
				FileRow fileRow = new FileRow();
				fileRow.Text1 = customTrail.Name;
				fileRow.Text2 = customTrail.Count.ToString();
				if (customTrail.workshopUploadInfoAvailable)
				{
					fileRow.TypeImage = MainViewModel.Instance.GameSprites[89];
				}
				importRows.Add(fileRow);
			}
			((ItemsControl)RefImportList).ItemsSource = importRows;
			((Selector)RefImportList).SelectedItem = null;
			((UIElement)RefImportImportButton).IsEnabled = false;
			((UIElement)RefImportImportButton).Opacity = 0.7f;
			MainViewModel.Instance.Show_TM_Import = true;
			break;
		case "DoImport":
			if (((Selector)RefImportList).SelectedItem == null)
			{
				break;
			}
			HUD_ConfirmationPopup.ShowConfirmationMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 40), delegate
			{
				if (((ToggleButton)RefImportBackup).IsChecked.Value)
				{
					BackupMakerFolder();
				}
				ClearMakerFolder();
				ImportTrailMissions(((FileRow)((Selector)RefImportList).SelectedItem).Text1);
				MapFileManager.Instance.RescanTrailMakerFolder();
				Init(((UIElement)RefSave).IsEnabled);
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 43), MPConf: true, tall: true);
			break;
		case "CloseImport":
			MainViewModel.Instance.Show_TM_Import = false;
			break;
		case "Export":
			exportRows.Clear();
			foreach (MapFileManager.CustomTrailInfo customTrail2 in MapFileManager.Instance.GetCustomTrails(ignoreWorkshopTrails: true))
			{
				FileRow fileRow2 = new FileRow();
				fileRow2.Text1 = customTrail2.Name;
				fileRow2.Text2 = customTrail2.Count.ToString();
				if (customTrail2.workshop)
				{
					fileRow2.TypeImage = MainViewModel.Instance.GameSprites[89];
				}
				exportRows.Add(fileRow2);
			}
			((ItemsControl)RefExportList).ItemsSource = exportRows;
			((Selector)RefExportList).SelectedItem = null;
			((UIElement)RefExportExportButton).IsEnabled = false;
			((UIElement)RefExportExportButton).Opacity = 0.7f;
			RefExportTrailName.Text = "";
			MainViewModel.Instance.Show_TM_Export = true;
			((UIElement)RefExportTrailName).Focus();
			break;
		case "DoExport":
			HUD_ConfirmationPopup.ShowConfirmationMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 41), delegate
			{
				string text = Path.Combine(ConfigSettings.GetUserCustomTrailsPath(), RefExportTrailName.Text);
				if (!Directory.Exists(text))
				{
					try
					{
						Directory.CreateDirectory(text);
					}
					catch (Exception)
					{
						return;
					}
				}
				else if (((ToggleButton)RefExportBackup).IsChecked.Value)
				{
					BackupCustomFolder(text);
				}
				ExportTrailMissions(text);
				MapFileManager.Instance.RescanCustomTrailsFolder();
				MainViewModel.Instance.Show_TM_Export = false;
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 44), MPConf: true, tall: true);
			break;
		case "CloseExport":
			MainViewModel.Instance.Show_TM_Export = false;
			break;
		case "Clear":
			HUD_ConfirmationPopup.ShowConfirmationMessageCheck(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 42), delegate
			{
				if (clearMakeBackup)
				{
					BackupMakerFolder();
				}
				ClearMakerFolder();
				makerFiles = MapFileManager.Instance.GetTrailMakerFiles(0, sortAscend: true);
				for (int i = 0; i < 50; i++)
				{
					exists[i] = false;
					RadioButton button = GetButton(i);
					((UIElement)button).Opacity = 0.7f;
					PropEx.SetSprite1((UIElement)(object)button, null);
				}
				SelectMission(SelectedMission);
				UpdateMainButtons();
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 45), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 49), initialCheckState: true, delegate(bool state)
			{
				clearMakeBackup = state;
			}, MPConf: true);
			break;
		case "1":
		case "2":
		case "3":
		case "4":
		case "5":
		case "6":
		case "7":
		case "8":
		case "9":
		case "10":
		case "11":
		case "12":
		case "13":
		case "14":
		case "15":
		case "16":
		case "17":
		case "18":
		case "19":
		case "20":
		case "21":
		case "22":
		case "23":
		case "24":
		case "25":
		case "26":
		case "27":
		case "28":
		case "29":
		case "30":
		case "31":
		case "32":
		case "33":
		case "34":
		case "35":
		case "36":
		case "37":
		case "38":
		case "39":
		case "40":
		case "41":
		case "42":
		case "43":
		case "44":
		case "45":
		case "46":
		case "47":
		case "48":
		case "49":
		case "50":
		{
			int index = int.Parse(param) - 1;
			SelectMission(index);
			break;
		}
		}
	}

	public void SaveMission()
	{
		HUD_IngameMenu.RestartSkirmishMapInfo restartSkirmishMapInfo = MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo;
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		EditorDirector.instance.SaveCustomTrailMap(restartSkirmishMapInfo.selectedHeader.filePath, restartSkirmishMapInfo.selectedHeader.fileName, ConfigSettings.GetUserTrailMakerPath() + "\\" + GetMakerFileName(SelectedMission) + ".trail", restartSkirmishMapInfo);
		exists[SelectedMission] = true;
		MapFileManager.Instance.RescanTrailMakerFolder();
		makerFiles = MapFileManager.Instance.GetTrailMakerFiles(0, sortAscend: true);
		RadioButton button = GetButton(SelectedMission);
		((UIElement)button).Opacity = 1f;
		PropEx.SetSprite1((UIElement)(object)button, MainViewModel.Instance.GameSprites[734]);
		SelectMission(SelectedMission);
		UpdateMainButtons();
	}

	public void BackupMakerFolder()
	{
		DoBackup(ConfigSettings.GetUserTrailMakerPath(), ConfigSettings.GetUserTrailMakerBackupPath());
	}

	public void BackupCustomFolder(string customFolder)
	{
		DoBackup(customFolder, ConfigSettings.GetUserTrailMakerBackupPath());
	}

	public void DoBackup(string source, string dest)
	{
		if (!Directory.Exists(dest))
		{
			try
			{
				Directory.CreateDirectory(dest);
			}
			catch (Exception)
			{
				return;
			}
		}
		string[] files = Directory.GetFiles(source, "*.trail");
		foreach (string obj in files)
		{
			File.Copy(obj, obj.Replace(source, dest), overwrite: true);
		}
	}

	public void ImportTrailMissions(string customFolderName)
	{
		string text = Path.Combine(ConfigSettings.GetUserCustomTrailsPath(), customFolderName);
		string userTrailMakerPath = ConfigSettings.GetUserTrailMakerPath();
		string[] files = Directory.GetFiles(text, "*.trail");
		foreach (string obj in files)
		{
			File.Copy(obj, obj.Replace(text, userTrailMakerPath));
		}
	}

	public void ExportTrailMissions(string dest)
	{
		string[] files = Directory.GetFiles(dest, "*.trail");
		for (int i = 0; i < files.Length; i++)
		{
			File.Delete(files[i]);
		}
		int num = 0;
		string[] files2 = Directory.GetFiles(ConfigSettings.GetUserTrailMakerPath(), "*.trail");
		for (int j = 0; j < 50; j++)
		{
			string text = GetMakerFileName(j) + ".trail";
			files = files2;
			foreach (string text2 in files)
			{
				if (text2.ToLowerInvariant().Contains(text.ToLowerInvariant()))
				{
					string makerFileName = GetMakerFileName(num);
					num++;
					string destFileName = Path.Combine(dest, makerFileName + ".trail");
					File.Copy(text2, destFileName);
					break;
				}
			}
		}
	}

	public void ClearMakerFolder()
	{
		string[] files = Directory.GetFiles(ConfigSettings.GetUserTrailMakerPath(), "*.trail");
		for (int i = 0; i < files.Length; i++)
		{
			File.Delete(files[i]);
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_ManageTrail.xaml");
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

	public void TextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (RefExportTrailName.Text.Length > 0)
		{
			((UIElement)RefExportExportButton).IsEnabled = true;
			((UIElement)RefExportExportButton).Opacity = 1f;
		}
		else
		{
			((UIElement)RefExportExportButton).IsEnabled = false;
			((UIElement)RefExportExportButton).Opacity = 0.7f;
		}
	}

	public void TextBoxLoaded(object sender, RoutedEventArgs e)
	{
		((UIElement)RefExportTrailName).Focus();
	}

	public void FileNameValidationTextBox(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			((RoutedEventArgs)e).Handled = true;
			((UIElement)this).Keyboard.ClearFocus();
			return;
		}
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		string text = e.Text;
		foreach (char value in text)
		{
			if (invalidFileNameChars.Contains(value))
			{
				((RoutedEventArgs)e).Handled = true;
			}
		}
	}

	public void TextBoxCheckForEscape(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)e.Key == 13)
		{
			((UIElement)this).Keyboard.ClearFocus();
			KeyManager.instance.ignoreEscape();
		}
	}
}
