using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_StandaloneMission : UserControl
{
	public ListView RefFileLists;

	public CheckBox RefIncludeUser;

	public CheckBox RefIncludeBuiltin;

	public CheckBox RefIncludeWorkshop;

	public Button RefStandaloneDifficulyLevel;

	public Button RefStandalonePlayButton;

	public Button RefStandaloneFreebuildAdvanced;

	public CheckBox RefStandaloneFreebuildHostileAnimals;

	public ToggleButton RefToggleCrusader;

	public ToggleButton RefToggleArabian;

	public ToggleButton RefToggleBedouin;

	public Grid RefCarouselNext;

	public TextBox RefSA_SearchFilter;

	public CheckBox RefShowCompleted;

	public CheckBox RefDefeatCheck;

	public int sortByColumn;

	public bool sortByAscending = true;

	public bool includeUser = true;

	public bool includeBuiltIn = true;

	public bool includeWorkshop = true;

	public bool freebuildAdvanced;

	public int freebuild_GoldLevel = -1;

	public int freebuild_FoodLevel = -1;

	public int freebuild_ResourcesLevel = -1;

	public int freebuild_WeaponsLevel = -1;

	public int freebuild_RandomEvents;

	public int freebuild_Invasions;

	public int freebuild_InvasionDifficulty;

	public int freebuild_Peacetime = 4;

	public int freebuild_Opponents = 7;

	public bool freebuild_Extreme_Troops;

	public bool freebuild_Extreme_Powers;

	public Enums.GameDifficulty difficulty = Enums.GameDifficulty.DIFFICULTY_NORMAL;

	public FileHeader selectedHeader;

	public int[] troop_points_data = new int[20]
	{
		5, 10, 4, 10, 10, 30, 35, 1, 5, 8,
		5, 8, 1, 2, 14, 15, 20, 16, 25, 25
	};

	public int[] troop_points_percents = new int[4] { 167, 100, 75, 50 };

	public int[][] adjusted_start_troops_levels;

	public int[] troop_points_levels = new int[4];

	public int[] adjusted_start_troops = new int[21];

	public int troop_points;

	public bool ShowCompletedFlag = true;

	public Enums.eChimps[] siegeTroopsOrder = new Enums.eChimps[20]
	{
		Enums.eChimps.CHIMP_TYPE_ARCHER,
		Enums.eChimps.CHIMP_TYPE_XBOWMAN,
		Enums.eChimps.CHIMP_TYPE_SPEARMAN,
		Enums.eChimps.CHIMP_TYPE_PIKEMAN,
		Enums.eChimps.CHIMP_TYPE_MACEMAN,
		Enums.eChimps.CHIMP_TYPE_SWORDSMAN,
		Enums.eChimps.CHIMP_TYPE_KNIGHT,
		Enums.eChimps.CHIMP_TYPE_LADDERMAN,
		Enums.eChimps.CHIMP_TYPE_ENGINEER,
		Enums.eChimps.CHIMP_TYPE_MONK,
		Enums.eChimps.CHIMP_TYPE_TUNNELER,
		Enums.eChimps.CHIMP_TYPE_ARAB_BOW,
		Enums.eChimps.CHIMP_TYPE_ARAB_SLAVE,
		Enums.eChimps.CHIMP_TYPE_ARAB_SLINGER,
		Enums.eChimps.CHIMP_TYPE_ARAB_ASSASIN,
		Enums.eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
		Enums.eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
		Enums.eChimps.CHIMP_TYPE_ARAB_GRENADIER,
		Enums.eChimps.CHIMP_TYPE_ARAB_BALLISTA,
		Enums.eChimps.CHIMP_TYPE_CATAPULT
	};

	public bool panelActive;

	public Enums.StartUpUIPanels missionType;

	public ObservableCollection<FileRow> rows = new ObservableCollection<FileRow>();

	public List<FileHeader> headerlist;

	public DateTime lastScrollTest = DateTime.MinValue;

	public FRONT_StandaloneMission()
	{
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Expected O, but got Unknown
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Expected O, but got Unknown
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Expected O, but got Unknown
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Expected O, but got Unknown
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Expected O, but got Unknown
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Expected O, but got Unknown
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Expected O, but got Unknown
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Expected O, but got Unknown
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Expected O, but got Unknown
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
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e6: Expected O, but got Unknown
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Expected O, but got Unknown
		//IL_030a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Expected O, but got Unknown
		//IL_0321: Unknown result type (might be due to invalid IL or missing references)
		//IL_032b: Expected O, but got Unknown
		//IL_0337: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Expected O, but got Unknown
		//IL_034d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0357: Expected O, but got Unknown
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_036e: Expected O, but got Unknown
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Expected O, but got Unknown
		//IL_0392: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Expected O, but got Unknown
		//IL_03a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Expected O, but got Unknown
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c9: Expected O, but got Unknown
		//IL_03d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_040a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Expected O, but got Unknown
		//IL_0414: Unknown result type (might be due to invalid IL or missing references)
		//IL_0425: Unknown result type (might be due to invalid IL or missing references)
		//IL_042a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_044f: Expected O, but got Unknown
		//IL_044f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0460: Unknown result type (might be due to invalid IL or missing references)
		//IL_0465: Unknown result type (might be due to invalid IL or missing references)
		//IL_0477: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Expected O, but got Unknown
		//IL_0491: Unknown result type (might be due to invalid IL or missing references)
		//IL_0496: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b2: Expected O, but got Unknown
		//IL_04bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c9: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.FRONTStandaloneMission = this;
		RefFileLists = (ListView)((FrameworkElement)this).FindName("MapList");
		RefIncludeUser = (CheckBox)((FrameworkElement)this).FindName("IncludeUser");
		((ToggleButton)RefIncludeUser).Checked += new RoutedEventHandler(Include_ValueChanged);
		((ToggleButton)RefIncludeUser).Unchecked += new RoutedEventHandler(Include_ValueChanged);
		RefIncludeBuiltin = (CheckBox)((FrameworkElement)this).FindName("IncludeBuiltin");
		((ToggleButton)RefIncludeBuiltin).Checked += new RoutedEventHandler(Include_ValueChanged);
		((ToggleButton)RefIncludeBuiltin).Unchecked += new RoutedEventHandler(Include_ValueChanged);
		RefIncludeWorkshop = (CheckBox)((FrameworkElement)this).FindName("IncludeWorkshop");
		((ToggleButton)RefIncludeWorkshop).Checked += new RoutedEventHandler(Include_ValueChanged);
		((ToggleButton)RefIncludeWorkshop).Unchecked += new RoutedEventHandler(Include_ValueChanged);
		RefShowCompleted = (CheckBox)((FrameworkElement)this).FindName("ShowCompleted");
		((ToggleButton)RefShowCompleted).Checked += new RoutedEventHandler(Completed_ValueChanged);
		((ToggleButton)RefShowCompleted).Unchecked += new RoutedEventHandler(Completed_ValueChanged);
		RefDefeatCheck = (CheckBox)((FrameworkElement)this).FindName("DefeatCheck");
		RefStandaloneDifficulyLevel = (Button)((FrameworkElement)this).FindName("StandaloneDifficulyLevel");
		RefStandaloneFreebuildAdvanced = (Button)((FrameworkElement)this).FindName("StandaloneFreebuildAdvanced");
		RefStandaloneFreebuildHostileAnimals = (CheckBox)((FrameworkElement)this).FindName("StandaloneFreebuildHostileAnimals");
		RefToggleCrusader = (ToggleButton)((FrameworkElement)this).FindName("ToggleCrusader");
		RefToggleArabian = (ToggleButton)((FrameworkElement)this).FindName("ToggleArabian");
		RefToggleBedouin = (ToggleButton)((FrameworkElement)this).FindName("ToggleBedouin");
		RefToggleCrusader.Checked += new RoutedEventHandler(ToggleCrusader_Changed);
		RefToggleCrusader.Unchecked += new RoutedEventHandler(ToggleCrusader_Changed);
		RefToggleArabian.Checked += new RoutedEventHandler(ToggleArabian_Changed);
		RefToggleArabian.Unchecked += new RoutedEventHandler(ToggleArabian_Changed);
		RefToggleBedouin.Checked += new RoutedEventHandler(ToggleBedouin_Changed);
		RefToggleBedouin.Unchecked += new RoutedEventHandler(ToggleBedouin_Changed);
		RefStandalonePlayButton = (Button)((FrameworkElement)this).FindName("StandalonePlayButton");
		RefSA_SearchFilter = (TextBox)((FrameworkElement)this).FindName("SA_SearchFilter");
		((UIElement)RefSA_SearchFilter).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefSA_SearchFilter).TextChanged += new RoutedEventHandler(FilterTextChangedHandler);
		((UIElement)RefSA_SearchFilter).PreviewKeyDown += new KeyEventHandler(TextBoxCheckForEscape);
		((UIElement)RefSA_SearchFilter).PreviewTextInput += new TextCompositionEventHandler(TextBoxEnterCheck);
		RefCarouselNext = (Grid)((FrameworkElement)this).FindName("CarouselNext");
		GridView val = (GridView)RefFileLists.View;
		GridViewColumnHeader val2 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[2].Header;
		((ContentControl)val2).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		((ButtonBase)val2).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val3 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[3].Header;
		((ContentControl)val3).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 28);
		((ButtonBase)val3).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val4 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[0].Header;
		((ContentControl)val4).Content = "";
		((ButtonBase)val4).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val5 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[1].Header;
		((ContentControl)val5).Content = "";
		((ButtonBase)val5).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		((Selector)RefFileLists).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefFileLists).SelectedItem != null)
			{
				FileHeader fileHeader = ((FileRow)((Selector)RefFileLists).SelectedItem).fileHeader;
				if (fileHeader != null)
				{
					updateRadarTexture(fileHeader);
					selectedHeader = fileHeader;
					GameData.Instance.SetMissionTextFromHeader(fileHeader);
					MainViewModel.Instance.StandaloneMissionText = GameData.Instance.GetMissionBriefing(fileHeader);
					MainViewModel.Instance.StandaloneMissionTitle = GameData.Instance.cachedMissionName;
					MainViewModel.Instance.Show_StandaloneMissionHasOutposts = fileHeader.hostileAnimals > 0 && fileHeader.scnMissionSiegeOrInvasion == 3;
					MainViewModel.Instance.Show_StandaloneMissionUnBalanced = !MainViewModel.Instance.Show_StandaloneMissionHasOutposts;
					if (fileHeader.world_size >= 160 && fileHeader.world_size <= 800)
					{
						MainViewModel.Instance.StandaloneMissionSize = fileHeader.world_size.ToString();
					}
					else
					{
						MainViewModel.Instance.StandaloneMissionSize = "?";
					}
					MainViewModel.Instance.StandaloneMissionPlayerCount = fileHeader.hostileAnimals.ToString();
					((UIElement)RefStandalonePlayButton).IsEnabled = true;
					reset_troop_points();
					if (missionType == Enums.StartUpUIPanels.FreeBuild)
					{
						UpdateButtons();
					}
				}
			}
		};
	}

	public static void Open(Enums.StartUpUIPanels mode)
	{
		MainViewModel.Instance.FRONTStandaloneMission.doOpen(mode, fromNew: true);
	}

	public void doOpen(Enums.StartUpUIPanels mode, bool fromNew = false)
	{
		panelActive = false;
		if (fromNew)
		{
			MainViewModel.Instance.HUDIngameMenu.restartMapInfo = null;
			MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = null;
			MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
			sortByColumn = 0;
			sortByAscending = true;
			includeUser = true;
			includeBuiltIn = true;
			includeWorkshop = true;
			((ToggleButton)RefIncludeBuiltin).IsChecked = true;
			((ToggleButton)RefIncludeUser).IsChecked = true;
			((ToggleButton)RefIncludeWorkshop).IsChecked = true;
			freebuildAdvanced = false;
			freebuild_GoldLevel = -1;
			freebuild_FoodLevel = -1;
			freebuild_ResourcesLevel = -1;
			freebuild_WeaponsLevel = -1;
			freebuild_RandomEvents = 0;
			freebuild_Invasions = 0;
			freebuild_InvasionDifficulty = 0;
			freebuild_Peacetime = 4;
			ShowCompletedFlag = false;
			((ToggleButton)RefShowCompleted).IsChecked = false;
			RefToggleCrusader.IsChecked = true;
			RefToggleArabian.IsChecked = true;
			RefToggleBedouin.IsChecked = true;
			freebuild_Opponents = 7;
			freebuild_Extreme_Troops = false;
			freebuild_Extreme_Powers = false;
			((ToggleButton)RefDefeatCheck).IsChecked = false;
			MainViewModel.Instance.StandaloneFilter = "";
			MainViewModel.Instance.StandaloneFilterLabelVis = (Visibility)2;
			MainViewModel.Instance.StandaloneFilterButtonVis = (Visibility)1;
			MainViewModel.Instance.Show_Radar160Border = false;
			MainViewModel.Instance.Show_Radar300Border = false;
			MainViewModel.Instance.Show_Radar500Border = false;
			MainViewModel.Instance.Show_Radar700Border = false;
		}
		missionType = mode;
		selectedHeader = null;
		((UIElement)RefStandalonePlayButton).IsEnabled = false;
		MainViewModel.Instance.RadarStandaloneImage = null;
		MainViewModel.Instance.StandaloneMissionText = "";
		MainViewModel.Instance.StandaloneMissionTitle = "";
		MainViewModel.Instance.Show_StandaloneMissionHasOutposts = false;
		MainViewModel.Instance.StandaloneMissionSize = "";
		MainViewModel.Instance.StandaloneMissionPlayerCount = "";
		MainViewModel.Instance.Show_StandaloneSetup = true;
		((UIElement)RefCarouselNext).Opacity = 0.6f;
		difficulty = Enums.GameDifficulty.DIFFICULTY_NORMAL;
		initTroopPoints();
		UpdateButtons();
		GameData.Instance.game_type = 2;
		MainViewModel.Instance.SiegeThatHelpVis = (Visibility)1;
		MainViewModel.Instance.FreeBuildOptionsVis = (Visibility)1;
		if (mode == Enums.StartUpUIPanels.FreeBuild)
		{
			((UIElement)RefShowCompleted).Visibility = (Visibility)1;
			((ToggleButton)RefShowCompleted).IsChecked = false;
			ShowCompletedFlag = false;
		}
		else
		{
			((UIElement)RefShowCompleted).Visibility = (Visibility)2;
		}
		switch (missionType)
		{
		case Enums.StartUpUIPanels.Invasion:
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 30);
			MainViewModel.Instance.StandaloneNext = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 264);
			((UIElement)RefStandaloneDifficulyLevel).Visibility = (Visibility)2;
			MainViewModel.Instance.SiegeThatHelpButtonVis = (Visibility)1;
			break;
		case Enums.StartUpUIPanels.FreeBuild:
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 264);
			MainViewModel.Instance.StandaloneNext = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 30);
			((UIElement)RefStandaloneDifficulyLevel).Visibility = (Visibility)1;
			MainViewModel.Instance.SiegeThatHelpButtonVis = (Visibility)1;
			break;
		}
		populateList();
		panelActive = true;
	}

	public void FileListHeaderClickedHandler(object sender, RoutedEventArgs e)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		switch (((FrameworkElement)(GridViewColumnHeader)e.Source).Tag as string)
		{
		case "Name":
			if (sortByColumn == 0)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 0;
			sortByAscending = true;
			break;
		case "Date":
			if (sortByColumn == 1)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 1;
			sortByAscending = false;
			break;
		case "Size":
			if (sortByColumn == 3)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 3;
			sortByAscending = false;
			break;
		}
		populateList();
	}

	public void populateList()
	{
		includeBuiltIn = ((ToggleButton)RefIncludeBuiltin).IsChecked.Value;
		includeUser = ((ToggleButton)RefIncludeUser).IsChecked.Value;
		includeWorkshop = ((ToggleButton)RefIncludeWorkshop).IsChecked.Value;
		headerlist = null;
		switch (missionType)
		{
		case Enums.StartUpUIPanels.Invasion:
			headerlist = MapFileManager.Instance.GetInvasionMaps(sortByColumn, sortByAscending, includeBuiltIn, includeUser, includeWorkshop);
			break;
		case Enums.StartUpUIPanels.FreeBuild:
			headerlist = MapFileManager.Instance.GetFreebuildMaps(sortByColumn, sortByAscending, includeBuiltIn, includeUser, includeWorkshop);
			break;
		}
		if (headerlist == null)
		{
			return;
		}
		string text = RefSA_SearchFilter.Text;
		string value = text.ToLowerInvariant();
		rows.Clear();
		foreach (FileHeader item in headerlist)
		{
			if (text.Length > 0 && !item.display_filename.Contains(text) && !item.display_filename.ToLowerInvariant().Contains(value))
			{
				continue;
			}
			FileRow fileRow = new FileRow();
			fileRow.Text1 = item.display_filename;
			fileRow.Text2 = item.getDateString();
			if (item.world_size < 0)
			{
				fileRow.Text3 = "";
			}
			else
			{
				fileRow.Text3 = item.world_size.ToString();
			}
			if (!ShowCompletedFlag)
			{
				if (item.builtinMap)
				{
					fileRow.TypeImage = MainViewModel.Instance.GameSprites[88];
				}
				else if (item.workshopMap)
				{
					fileRow.TypeImage = MainViewModel.Instance.GameSprites[89];
				}
				else if (item.userMap)
				{
					fileRow.TypeImage = MainViewModel.Instance.GameSprites[90];
				}
			}
			else if (ConfigSettings.MapCompleted(item.fileName))
			{
				fileRow.TypeImage = MainViewModel.Instance.GameSprites[368];
			}
			else
			{
				fileRow.TypeImage = MainViewModel.Instance.GameSprites[369];
			}
			fileRow.fileHeader = item;
			rows.Add(fileRow);
		}
		((ItemsControl)RefFileLists).ItemsSource = rows;
	}

	public void updateRadarTexture(FileHeader header)
	{
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Expected O, but got Unknown
		MainViewModel.Instance.Show_Radar160Border = false;
		MainViewModel.Instance.Show_Radar300Border = false;
		MainViewModel.Instance.Show_Radar500Border = false;
		MainViewModel.Instance.Show_Radar700Border = false;
		if (header != null)
		{
			switch (header.world_size)
			{
			case 160:
				MainViewModel.Instance.Show_Radar160Border = true;
				break;
			case 300:
				MainViewModel.Instance.Show_Radar300Border = true;
				break;
			case 500:
				MainViewModel.Instance.Show_Radar500Border = true;
				break;
			case 700:
				MainViewModel.Instance.Show_Radar700Border = true;
				break;
			}
			byte[] radarFromFile = MapFileManager.Instance.GetRadarFromFile(header.filePath);
			if (radarFromFile != null)
			{
				TextureSource radarStandaloneImage = new TextureSource(MapFileManager.Instance.GetRadarPreview(radarFromFile));
				MainViewModel.Instance.RadarStandaloneImage = (ImageSource)(object)radarStandaloneImage;
			}
		}
	}

	public void Include_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateList();
		}
	}

	public void Completed_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ShowCompletedFlag = ((ToggleButton)RefShowCompleted).IsChecked.Value;
			populateList();
		}
	}

	public void ToggleCrusader_Changed(object sender, RoutedEventArgs e)
	{
		if (!RefToggleCrusader.IsChecked.Value && !RefToggleArabian.IsChecked.Value && !RefToggleBedouin.IsChecked.Value)
		{
			RefToggleArabian.IsChecked = true;
		}
		calcFreebuildOpponents();
	}

	public void ToggleArabian_Changed(object sender, RoutedEventArgs e)
	{
		if (!RefToggleCrusader.IsChecked.Value && !RefToggleArabian.IsChecked.Value && !RefToggleBedouin.IsChecked.Value)
		{
			RefToggleBedouin.IsChecked = true;
		}
		calcFreebuildOpponents();
	}

	public void ToggleBedouin_Changed(object sender, RoutedEventArgs e)
	{
		if (!RefToggleCrusader.IsChecked.Value && !RefToggleArabian.IsChecked.Value && !RefToggleBedouin.IsChecked.Value)
		{
			RefToggleCrusader.IsChecked = true;
		}
		calcFreebuildOpponents();
	}

	public void calcFreebuildOpponents()
	{
		freebuild_Opponents = 0;
		if (RefToggleCrusader.IsChecked.Value)
		{
			freebuild_Opponents |= 1;
		}
		if (RefToggleArabian.IsChecked.Value)
		{
			freebuild_Opponents |= 2;
		}
		if (RefToggleBedouin.IsChecked.Value)
		{
			freebuild_Opponents |= 4;
		}
	}

	public void ButtonClicked(string param)
	{
		//IL_040e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Invalid comparison between Unknown and I4
		switch (param)
		{
		case "Back":
			switch (missionType)
			{
			case Enums.StartUpUIPanels.Invasion:
				MainViewModel.Instance.FrontEndMenu.ButtonClicked("Combat");
				break;
			case Enums.StartUpUIPanels.FreeBuild:
				MainViewModel.Instance.FrontEndMenu.ButtonClicked("Combat");
				break;
			}
			break;
		case "Play":
		{
			EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
			MainViewModel.Instance.PreStartMapMission();
			HUD_IngameMenu.RestartMapInfo restartMapInfo = new HUD_IngameMenu.RestartMapInfo();
			MainViewModel.Instance.HUDIngameMenu.restartMapInfo = restartMapInfo;
			MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = null;
			MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
			restartMapInfo.missionType = missionType;
			restartMapInfo.difficulty = difficulty;
			restartMapInfo.selectedHeader = selectedHeader;
			restartMapInfo.removeHostileAnimals = false;
			if (((ToggleButton)RefStandaloneFreebuildHostileAnimals).IsChecked == true && (int)((UIElement)RefStandaloneFreebuildHostileAnimals).Visibility == 2)
			{
				restartMapInfo.removeHostileAnimals = true;
			}
			restartMapInfo.advancedFreebuild = freebuildAdvanced;
			restartMapInfo.freebuild_GoldLevel = freebuild_GoldLevel;
			restartMapInfo.freebuild_FoodLevel = freebuild_FoodLevel;
			restartMapInfo.freebuild_ResourcesLevel = freebuild_ResourcesLevel;
			restartMapInfo.freebuild_WeaponsLevel = freebuild_WeaponsLevel;
			restartMapInfo.freebuild_RandomEvents = freebuild_RandomEvents;
			restartMapInfo.freebuild_Invasions = freebuild_Invasions;
			restartMapInfo.freebuild_InvasionDifficulty = freebuild_InvasionDifficulty;
			restartMapInfo.freebuild_Peacetime = freebuild_Peacetime;
			restartMapInfo.freebuild_Opponents = freebuild_Opponents;
			restartMapInfo.freebuild_Extreme_Troops = freebuild_Extreme_Troops;
			restartMapInfo.freebuild_Extreme_Powers = freebuild_Extreme_Powers;
			restartMapInfo.freebuild_Defeat_On_Death = ((ToggleButton)RefDefeatCheck).IsChecked.Value;
			StartMap(restartMapInfo);
			break;
		}
		case "Next":
			switch (missionType)
			{
			case Enums.StartUpUIPanels.Invasion:
				doOpen(Enums.StartUpUIPanels.FreeBuild);
				break;
			case Enums.StartUpUIPanels.FreeBuild:
				doOpen(Enums.StartUpUIPanels.Invasion);
				break;
			}
			break;
		case "Difficulty":
			if (difficulty == Enums.GameDifficulty.DIFFICULTY_VERYHARD)
			{
				difficulty = Enums.GameDifficulty.DIFFICULTY_EASY;
			}
			else
			{
				difficulty++;
			}
			UpdateButtons();
			break;
		case "FreebuildAdvanced":
			freebuildAdvanced = !freebuildAdvanced;
			if (freebuildAdvanced)
			{
				MainViewModel.Instance.FreeBuildOptionsVis = (Visibility)2;
				UpdateFreebuildValues();
			}
			else
			{
				MainViewModel.Instance.FreeBuildOptionsVis = (Visibility)1;
			}
			break;
		case "MouseEnter_Next":
			((UIElement)RefCarouselNext).Opacity = 0.9f;
			break;
		case "MouseLeave_Next":
			((UIElement)RefCarouselNext).Opacity = 0.6f;
			break;
		case "Freebuild_Gold":
			freebuild_GoldLevel++;
			if (freebuild_GoldLevel >= 6)
			{
				freebuild_GoldLevel = -1;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Gold-":
			freebuild_GoldLevel--;
			if (freebuild_GoldLevel < -1)
			{
				freebuild_GoldLevel = 5;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Food":
			freebuild_FoodLevel++;
			if (freebuild_FoodLevel >= 6)
			{
				freebuild_FoodLevel = -1;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Food-":
			freebuild_FoodLevel--;
			if (freebuild_FoodLevel < -1)
			{
				freebuild_FoodLevel = 5;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Resources":
			freebuild_ResourcesLevel++;
			if (freebuild_ResourcesLevel >= 6)
			{
				freebuild_ResourcesLevel = -1;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Resources-":
			freebuild_ResourcesLevel--;
			if (freebuild_ResourcesLevel < -1)
			{
				freebuild_ResourcesLevel = 5;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Weapons":
			freebuild_WeaponsLevel++;
			if (freebuild_WeaponsLevel >= 6)
			{
				freebuild_WeaponsLevel = -1;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Weapons-":
			freebuild_WeaponsLevel--;
			if (freebuild_WeaponsLevel < -1)
			{
				freebuild_WeaponsLevel = 5;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_RandomEvents":
			freebuild_RandomEvents++;
			if (freebuild_RandomEvents >= 9)
			{
				freebuild_RandomEvents = 0;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_RandomEvents-":
			freebuild_RandomEvents--;
			if (freebuild_RandomEvents < 0)
			{
				freebuild_RandomEvents = 8;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Invasions":
			freebuild_Invasions++;
			if (freebuild_Invasions >= 9)
			{
				freebuild_Invasions = 0;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Invasions-":
			freebuild_Invasions--;
			if (freebuild_Invasions < 0)
			{
				freebuild_Invasions = 8;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_InvasionsDifficulty":
			freebuild_InvasionDifficulty++;
			if (freebuild_InvasionDifficulty >= 9)
			{
				freebuild_InvasionDifficulty = 0;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_InvasionsDifficulty-":
			freebuild_InvasionDifficulty--;
			if (freebuild_InvasionDifficulty < 0)
			{
				freebuild_InvasionDifficulty = 8;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Peacetime":
			freebuild_Peacetime++;
			if (freebuild_Peacetime >= 7)
			{
				freebuild_Peacetime = 0;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_Peacetime-":
			freebuild_Peacetime--;
			if (freebuild_Peacetime < 0)
			{
				freebuild_Peacetime = 6;
			}
			UpdateFreebuildValues();
			break;
		case "Freebuild_ExtremeTroops":
			freebuild_Extreme_Troops = !freebuild_Extreme_Troops;
			UpdateFreebuildValues();
			break;
		case "Freebuild_ExtremePowers":
			freebuild_Extreme_Powers = !freebuild_Extreme_Powers;
			UpdateFreebuildValues();
			break;
		case "ClearFilter":
			RefSA_SearchFilter.Text = "";
			MainViewModel.Instance.StandaloneFilterLabelVis = (Visibility)2;
			MainViewModel.Instance.StandaloneFilterButtonVis = (Visibility)1;
			break;
		}
	}

	public void UpdateButtons()
	{
		switch (difficulty)
		{
		case Enums.GameDifficulty.DIFFICULTY_EASY:
			MainViewModel.Instance.StandaloneDifficultyText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 19);
			break;
		case Enums.GameDifficulty.DIFFICULTY_NORMAL:
			MainViewModel.Instance.StandaloneDifficultyText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 20);
			break;
		case Enums.GameDifficulty.DIFFICULTY_HARD:
			MainViewModel.Instance.StandaloneDifficultyText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 21);
			break;
		case Enums.GameDifficulty.DIFFICULTY_VERYHARD:
			MainViewModel.Instance.StandaloneDifficultyText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 22);
			break;
		}
		((UIElement)RefStandaloneFreebuildHostileAnimals).Visibility = (Visibility)1;
		if (selectedHeader == null)
		{
			((UIElement)RefStandaloneFreebuildAdvanced).Visibility = (Visibility)1;
		}
		else if (missionType == Enums.StartUpUIPanels.FreeBuild)
		{
			MainViewModel.Instance.StandaloneAttackDefendText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_ENGINEER);
			((UIElement)RefStandaloneFreebuildAdvanced).Visibility = (Visibility)2;
			if (selectedHeader.hostileAnimals > 0)
			{
				((UIElement)RefStandaloneFreebuildHostileAnimals).Visibility = (Visibility)2;
			}
		}
		else
		{
			MainViewModel.Instance.StandaloneAttackDefendText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_LADDERMAN);
			((UIElement)RefStandaloneFreebuildAdvanced).Visibility = (Visibility)1;
		}
	}

	public void initTroopPoints()
	{
		if (adjusted_start_troops_levels == null)
		{
			adjusted_start_troops_levels = new int[4][];
			for (int i = 0; i < 4; i++)
			{
				adjusted_start_troops_levels[i] = new int[21];
			}
		}
	}

	public void reset_troop_points()
	{
		for (int i = 0; i < 4; i++)
		{
			troop_points_levels[i] = 0;
			for (int j = 0; j < 21; j++)
			{
				adjusted_start_troops_levels[i][j] = 0;
			}
		}
	}

	public void adjust_troops_difficulty_levels(int current_level)
	{
		int num = troop_points_levels[1];
		int[] array = new int[4];
		for (int i = 0; i < 21; i++)
		{
			num += troop_points_data[i] * adjusted_start_troops_levels[1][i];
		}
		for (int j = 0; j < 4; j++)
		{
			array[j] = num * troop_points_percents[j] / 100;
		}
		for (int j = 0; j < 4; j++)
		{
			troop_points_levels[j] = troop_points;
			for (int i = 0; i < 21; i++)
			{
				adjusted_start_troops_levels[j][i] = adjusted_start_troops[i];
			}
		}
		if (current_level != 1)
		{
			int num2 = 0;
			for (int i = 0; i < 21; i++)
			{
				adjusted_start_troops_levels[1][i] = adjusted_start_troops_levels[current_level][i] * 100 / troop_points_percents[current_level];
				num2 += troop_points_data[i] * adjusted_start_troops_levels[1][i];
			}
			troop_points_levels[1] = num - num2;
			if (troop_points_levels[1] < 0)
			{
				troop_points_levels[1] = 0;
			}
		}
		for (int j = 0; j < 4; j++)
		{
			if (j != current_level && j != 1)
			{
				int num2 = 0;
				for (int i = 0; i < 21; i++)
				{
					adjusted_start_troops_levels[j][i] = adjusted_start_troops_levels[1][i] * troop_points_percents[j] / 100;
					num2 += troop_points_data[i] * adjusted_start_troops_levels[j][i];
				}
				troop_points_levels[j] = array[j] - num2;
				if (troop_points_levels[j] < 0)
				{
					troop_points_levels[j] = 0;
				}
			}
		}
	}

	public void set_adjust_troop_difficulty_level(int level)
	{
		for (int i = 0; i < 21; i++)
		{
			adjusted_start_troops[i] = adjusted_start_troops_levels[level][i];
		}
		troop_points = troop_points_levels[level];
	}

	public void Update()
	{
		if (((Selector)RefFileLists).SelectedItem == null && ((ItemsControl)RefFileLists).Items.Count > 0)
		{
			((Selector)RefFileLists).SelectedItem = ((ItemsControl)RefFileLists).Items[0];
		}
		if (!((DateTime.UtcNow - lastScrollTest).TotalMilliseconds > 150.0))
		{
			return;
		}
		if (KeyManager.instance.CursorUpHeld)
		{
			lastScrollTest = DateTime.UtcNow;
			DependencyObject scrollViewer = MainViewModel.GetScrollViewer((DependencyObject)(object)RefFileLists);
			ScrollViewer val = (ScrollViewer)(object)((scrollViewer is ScrollViewer) ? scrollViewer : null);
			if (!((BaseComponent)(object)val != (BaseComponent)null))
			{
				return;
			}
			if (((Selector)RefFileLists).SelectedItem == null)
			{
				val.ScrollToVerticalOffset(val.VerticalOffset - 30f);
				return;
			}
			if (((Selector)RefFileLists).SelectedIndex > 0)
			{
				ListView refFileLists = RefFileLists;
				int selectedIndex = ((Selector)refFileLists).SelectedIndex;
				((Selector)refFileLists).SelectedIndex = selectedIndex - 1;
			}
			((ListBox)RefFileLists).ScrollIntoView(((Selector)RefFileLists).SelectedItem);
		}
		else
		{
			if (!KeyManager.instance.CursorDownHeld)
			{
				return;
			}
			lastScrollTest = DateTime.UtcNow;
			DependencyObject scrollViewer2 = MainViewModel.GetScrollViewer((DependencyObject)(object)RefFileLists);
			ScrollViewer val2 = (ScrollViewer)(object)((scrollViewer2 is ScrollViewer) ? scrollViewer2 : null);
			if (!((BaseComponent)(object)val2 != (BaseComponent)null))
			{
				return;
			}
			if (((Selector)RefFileLists).SelectedItem == null)
			{
				val2.ScrollToVerticalOffset(val2.VerticalOffset + 30f);
				return;
			}
			if (((Selector)RefFileLists).SelectedIndex < ((ItemsControl)RefFileLists).Items.Count - 1)
			{
				ListView refFileLists2 = RefFileLists;
				int selectedIndex = ((Selector)refFileLists2).SelectedIndex;
				((Selector)refFileLists2).SelectedIndex = selectedIndex + 1;
			}
			((ListBox)RefFileLists).ScrollIntoView(((Selector)RefFileLists).SelectedItem);
		}
	}

	public void UpdateFreebuildValues()
	{
		MainViewModel.Instance.StandaloneFreebuild_Gold_Text = getGoodsLevelText(freebuild_GoldLevel);
		MainViewModel.Instance.StandaloneFreebuild_Food_Text = getGoodsLevelText(freebuild_FoodLevel);
		MainViewModel.Instance.StandaloneFreebuild_Resources_Text = getGoodsLevelText(freebuild_ResourcesLevel);
		MainViewModel.Instance.StandaloneFreebuild_Weapons_Text = getGoodsLevelText(freebuild_WeaponsLevel);
		if (freebuild_RandomEvents == 0)
		{
			MainViewModel.Instance.StandaloneFreebuild_RandomEvents_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 101);
		}
		else if (freebuild_RandomEvents == 1)
		{
			MainViewModel.Instance.StandaloneFreebuild_RandomEvents_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 100);
		}
		else if (freebuild_RandomEvents == 2)
		{
			MainViewModel.Instance.StandaloneFreebuild_RandomEvents_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 119);
		}
		else if (freebuild_RandomEvents == 3)
		{
			MainViewModel.Instance.StandaloneFreebuild_RandomEvents_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 120);
		}
		else if (freebuild_RandomEvents == 8)
		{
			MainViewModel.Instance.StandaloneFreebuild_RandomEvents_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 121);
		}
		else
		{
			MainViewModel.Instance.StandaloneFreebuild_RandomEvents_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 102 + (freebuild_RandomEvents - 4));
		}
		if (freebuild_Invasions == 0)
		{
			MainViewModel.Instance.StandaloneFreebuild_Invasions_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 101);
		}
		else if (freebuild_Invasions == 1)
		{
			MainViewModel.Instance.StandaloneFreebuild_Invasions_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 100);
		}
		else if (freebuild_Invasions == 2)
		{
			MainViewModel.Instance.StandaloneFreebuild_Invasions_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 119);
		}
		else if (freebuild_Invasions == 3)
		{
			MainViewModel.Instance.StandaloneFreebuild_Invasions_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 120);
		}
		else if (freebuild_Invasions == 8)
		{
			MainViewModel.Instance.StandaloneFreebuild_Invasions_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 121);
		}
		else
		{
			MainViewModel.Instance.StandaloneFreebuild_Invasions_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 102 + (freebuild_Invasions - 4));
		}
		if (freebuild_InvasionDifficulty < 6)
		{
			MainViewModel.Instance.StandaloneFreebuild_InvasionsDifficulty_Label = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 98);
			MainViewModel.Instance.StandaloneFreebuild_InvasionsDifficulty_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 112 + freebuild_InvasionDifficulty);
		}
		else
		{
			MainViewModel.Instance.StandaloneFreebuild_InvasionsDifficulty_Label = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 185);
			MainViewModel.Instance.StandaloneFreebuild_InvasionsDifficulty_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 186 + freebuild_InvasionDifficulty - 6);
		}
		if (freebuild_Peacetime == 0)
		{
			MainViewModel.Instance.StandaloneFreebuild_Peacetime_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 101);
		}
		else if (freebuild_Peacetime == 5)
		{
			MainViewModel.Instance.StandaloneFreebuild_Peacetime_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 121);
		}
		else if (freebuild_Peacetime == 6)
		{
			MainViewModel.Instance.StandaloneFreebuild_Peacetime_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 128);
		}
		else
		{
			MainViewModel.Instance.StandaloneFreebuild_Peacetime_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 102 + (freebuild_Peacetime - 1));
		}
		if (freebuild_Extreme_Powers)
		{
			MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[641];
		}
		if (freebuild_Extreme_Troops)
		{
			MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[641];
		}
	}

	public string getGoodsLevelText(int level)
	{
		return level switch
		{
			-1 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 282), 
			0 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 107 + level), 
			1 => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 118), 
			_ => Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 107 + level - 1), 
		};
	}

	public static void StartMap(HUD_IngameMenu.RestartMapInfo restartInfo)
	{
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		EngineInterface.LoadMapReturnData retData = new EngineInterface.LoadMapReturnData
		{
			errorCode = -1
		};
		bool flag = false;
		switch (restartInfo.missionType)
		{
		case Enums.StartUpUIPanels.Invasion:
			HUD_LoadSaveRequester.ClearSavedName(restartInfo.selectedHeader.display_filename);
			retData = EngineInterface.loadInvasionMap(restartInfo.selectedHeader.filePath, restartInfo.difficulty, restartInfo.encode());
			break;
		case Enums.StartUpUIPanels.FreeBuild:
			HUD_LoadSaveRequester.ClearSavedName(restartInfo.selectedHeader.display_filename);
			retData = EngineInterface.loadJustBuildMap(restartInfo.selectedHeader.filePath, restartInfo.advancedFreebuild, restartInfo.freebuild_GoldLevel, restartInfo.freebuild_FoodLevel, restartInfo.freebuild_ResourcesLevel, restartInfo.freebuild_WeaponsLevel, restartInfo.freebuild_RandomEvents, restartInfo.freebuild_Invasions, restartInfo.freebuild_InvasionDifficulty, restartInfo.freebuild_Peacetime, restartInfo.freebuild_Opponents, restartInfo.removeHostileAnimals, restartInfo.freebuild_Extreme_Troops, restartInfo.freebuild_Extreme_Powers, restartInfo.freebuild_Defeat_On_Death, restartInfo.encode());
			break;
		}
		if (retData.errorCode == 1)
		{
			EngineInterface.GameAction(Enums.GameActionCommand.HideObjectiveProgress, 1, 1);
			GameData.Instance.SetMissionTextFromHeader(restartInfo.selectedHeader);
			EngineInterface.SetUTF8MapName(restartInfo.selectedHeader.display_filename);
			AchievementsCommon.Instance.ResetOnMissionStart();
			EditorDirector.instance.postLoading(retData);
			AchievementsCommon.Instance.ResetOnMissionStart();
			if (restartInfo.missionType == Enums.StartUpUIPanels.Invasion)
			{
				MainViewModel.Instance.PostStartMapMission();
			}
			else
			{
				MainViewModel.Instance.InitObjectiveGoodsPanel();
				MainViewModel.Instance.Show_BlackOut = false;
			}
			if (flag && GameData.Instance.playerID == 1)
			{
				Director.instance.DelayCentreKeep();
			}
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_StandaloneMission.xaml");
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
		if ((bool)e.NewValue)
		{
			MainViewModel.Instance.StandaloneFilterLabelVis = (Visibility)1;
		}
		else if (RefSA_SearchFilter.Text.Length == 0)
		{
			MainViewModel.Instance.StandaloneFilterLabelVis = (Visibility)2;
		}
	}

	public void FilterTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateList();
			if (RefSA_SearchFilter.Text.Length == 0)
			{
				MainViewModel.Instance.StandaloneFilterButtonVis = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.StandaloneFilterButtonVis = (Visibility)2;
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

	public void TextBoxEnterCheck(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			((RoutedEventArgs)e).Handled = true;
			((UIElement)this).Keyboard.ClearFocus();
		}
	}
}
