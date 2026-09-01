using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_StandaloneMission : UserControl
{
	private ListView RefFileLists;

	private CheckBox RefIncludeUser;

	private CheckBox RefIncludeBuiltin;

	private CheckBox RefIncludeWorkshop;

	private Button RefStandaloneDifficulyLevel;

	private Button RefStandalonePlayButton;

	private Button RefStandaloneFreebuildAdvanced;

	private CheckBox RefStandaloneFreebuildHostileAnimals;

	private ToggleButton RefToggleCrusader;

	private ToggleButton RefToggleArabian;

	private ToggleButton RefToggleBedouin;

	private Noesis.Grid RefCarouselNext;

	private TextBox RefSA_SearchFilter;

	private CheckBox RefShowCompleted;

	private CheckBox RefDefeatCheck;

	private int sortByColumn;

	private bool sortByAscending = true;

	private bool includeUser = true;

	private bool includeBuiltIn = true;

	private bool includeWorkshop = true;

	private bool freebuildAdvanced;

	private int freebuild_GoldLevel = -1;

	private int freebuild_FoodLevel = -1;

	private int freebuild_ResourcesLevel = -1;

	private int freebuild_WeaponsLevel = -1;

	private int freebuild_RandomEvents;

	private int freebuild_Invasions;

	private int freebuild_InvasionDifficulty;

	private int freebuild_Peacetime = 4;

	private int freebuild_Opponents = 7;

	private bool freebuild_Extreme_Troops;

	private bool freebuild_Extreme_Powers;

	private Enums.GameDifficulty difficulty = Enums.GameDifficulty.DIFFICULTY_NORMAL;

	private FileHeader selectedHeader;

	private int[] troop_points_data = new int[20]
	{
		5, 10, 4, 10, 10, 30, 35, 1, 5, 8,
		5, 8, 1, 2, 14, 15, 20, 16, 25, 25
	};

	private int[] troop_points_percents = new int[4] { 167, 100, 75, 50 };

	private int[][] adjusted_start_troops_levels;

	private int[] troop_points_levels = new int[4];

	private int[] adjusted_start_troops = new int[21];

	private int troop_points;

	private bool ShowCompletedFlag = true;

	private Enums.eChimps[] siegeTroopsOrder = new Enums.eChimps[20]
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

	private bool panelActive;

	private Enums.StartUpUIPanels missionType;

	private ObservableCollection<FileRow> rows = new ObservableCollection<FileRow>();

	private List<FileHeader> headerlist;

	private DateTime lastScrollTest = DateTime.MinValue;

	public FRONT_StandaloneMission()
	{
		InitializeComponent();
		MainViewModel.Instance.FRONTStandaloneMission = this;
		RefFileLists = (ListView)FindName("MapList");
		RefIncludeUser = (CheckBox)FindName("IncludeUser");
		RefIncludeUser.Checked += Include_ValueChanged;
		RefIncludeUser.Unchecked += Include_ValueChanged;
		RefIncludeBuiltin = (CheckBox)FindName("IncludeBuiltin");
		RefIncludeBuiltin.Checked += Include_ValueChanged;
		RefIncludeBuiltin.Unchecked += Include_ValueChanged;
		RefIncludeWorkshop = (CheckBox)FindName("IncludeWorkshop");
		RefIncludeWorkshop.Checked += Include_ValueChanged;
		RefIncludeWorkshop.Unchecked += Include_ValueChanged;
		RefShowCompleted = (CheckBox)FindName("ShowCompleted");
		RefShowCompleted.Checked += Completed_ValueChanged;
		RefShowCompleted.Unchecked += Completed_ValueChanged;
		RefDefeatCheck = (CheckBox)FindName("DefeatCheck");
		RefStandaloneDifficulyLevel = (Button)FindName("StandaloneDifficulyLevel");
		RefStandaloneFreebuildAdvanced = (Button)FindName("StandaloneFreebuildAdvanced");
		RefStandaloneFreebuildHostileAnimals = (CheckBox)FindName("StandaloneFreebuildHostileAnimals");
		RefToggleCrusader = (ToggleButton)FindName("ToggleCrusader");
		RefToggleArabian = (ToggleButton)FindName("ToggleArabian");
		RefToggleBedouin = (ToggleButton)FindName("ToggleBedouin");
		RefToggleCrusader.Checked += ToggleCrusader_Changed;
		RefToggleCrusader.Unchecked += ToggleCrusader_Changed;
		RefToggleArabian.Checked += ToggleArabian_Changed;
		RefToggleArabian.Unchecked += ToggleArabian_Changed;
		RefToggleBedouin.Checked += ToggleBedouin_Changed;
		RefToggleBedouin.Unchecked += ToggleBedouin_Changed;
		RefStandalonePlayButton = (Button)FindName("StandalonePlayButton");
		RefSA_SearchFilter = (TextBox)FindName("SA_SearchFilter");
		RefSA_SearchFilter.IsKeyboardFocusedChanged += TextInputFocus;
		RefSA_SearchFilter.TextChanged += FilterTextChangedHandler;
		RefSA_SearchFilter.PreviewKeyDown += TextBoxCheckForEscape;
		RefSA_SearchFilter.PreviewTextInput += TextBoxEnterCheck;
		RefCarouselNext = (Noesis.Grid)FindName("CarouselNext");
		GridView obj = (GridView)RefFileLists.View;
		GridViewColumnHeader obj2 = (GridViewColumnHeader)obj.Columns[2].Header;
		obj2.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		obj2.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj3 = (GridViewColumnHeader)obj.Columns[3].Header;
		obj3.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 28);
		obj3.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj4 = (GridViewColumnHeader)obj.Columns[0].Header;
		obj4.Content = "";
		obj4.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj5 = (GridViewColumnHeader)obj.Columns[1].Header;
		obj5.Content = "";
		obj5.Click += FileListHeaderClickedHandler;
		RefFileLists.SelectionChanged += delegate
		{
			if (RefFileLists.SelectedItem != null)
			{
				FileHeader fileHeader = ((FileRow)RefFileLists.SelectedItem).fileHeader;
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
					RefStandalonePlayButton.IsEnabled = true;
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
			RefIncludeBuiltin.IsChecked = true;
			RefIncludeUser.IsChecked = true;
			RefIncludeWorkshop.IsChecked = true;
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
			RefShowCompleted.IsChecked = false;
			RefToggleCrusader.IsChecked = true;
			RefToggleArabian.IsChecked = true;
			RefToggleBedouin.IsChecked = true;
			freebuild_Opponents = 7;
			freebuild_Extreme_Troops = false;
			freebuild_Extreme_Powers = false;
			RefDefeatCheck.IsChecked = false;
			MainViewModel.Instance.StandaloneFilter = "";
			MainViewModel.Instance.StandaloneFilterLabelVis = Visibility.Visible;
			MainViewModel.Instance.StandaloneFilterButtonVis = Visibility.Hidden;
			MainViewModel.Instance.Show_Radar160Border = false;
			MainViewModel.Instance.Show_Radar300Border = false;
			MainViewModel.Instance.Show_Radar500Border = false;
			MainViewModel.Instance.Show_Radar700Border = false;
		}
		missionType = mode;
		selectedHeader = null;
		RefStandalonePlayButton.IsEnabled = false;
		MainViewModel.Instance.RadarStandaloneImage = null;
		MainViewModel.Instance.StandaloneMissionText = "";
		MainViewModel.Instance.StandaloneMissionTitle = "";
		MainViewModel.Instance.Show_StandaloneMissionHasOutposts = false;
		MainViewModel.Instance.StandaloneMissionSize = "";
		MainViewModel.Instance.StandaloneMissionPlayerCount = "";
		MainViewModel.Instance.Show_StandaloneSetup = true;
		RefCarouselNext.Opacity = 0.6f;
		difficulty = Enums.GameDifficulty.DIFFICULTY_NORMAL;
		initTroopPoints();
		UpdateButtons();
		GameData.Instance.game_type = 2;
		MainViewModel.Instance.SiegeThatHelpVis = Visibility.Hidden;
		MainViewModel.Instance.FreeBuildOptionsVis = Visibility.Hidden;
		if (mode == Enums.StartUpUIPanels.FreeBuild)
		{
			RefShowCompleted.Visibility = Visibility.Hidden;
			RefShowCompleted.IsChecked = false;
			ShowCompletedFlag = false;
		}
		else
		{
			RefShowCompleted.Visibility = Visibility.Visible;
		}
		switch (missionType)
		{
		case Enums.StartUpUIPanels.Invasion:
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 30);
			MainViewModel.Instance.StandaloneNext = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 264);
			RefStandaloneDifficulyLevel.Visibility = Visibility.Visible;
			MainViewModel.Instance.SiegeThatHelpButtonVis = Visibility.Hidden;
			break;
		case Enums.StartUpUIPanels.FreeBuild:
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 264);
			MainViewModel.Instance.StandaloneNext = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 30);
			RefStandaloneDifficulyLevel.Visibility = Visibility.Hidden;
			MainViewModel.Instance.SiegeThatHelpButtonVis = Visibility.Hidden;
			break;
		}
		populateList();
		panelActive = true;
	}

	private void FileListHeaderClickedHandler(object sender, RoutedEventArgs e)
	{
		switch (((GridViewColumnHeader)e.Source).Tag as string)
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

	private void populateList()
	{
		includeBuiltIn = RefIncludeBuiltin.IsChecked.Value;
		includeUser = RefIncludeUser.IsChecked.Value;
		includeWorkshop = RefIncludeWorkshop.IsChecked.Value;
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
		RefFileLists.ItemsSource = rows;
	}

	private void updateRadarTexture(FileHeader header)
	{
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
				MainViewModel.Instance.RadarStandaloneImage = radarStandaloneImage;
			}
		}
	}

	private void Include_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateList();
		}
	}

	private void Completed_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ShowCompletedFlag = RefShowCompleted.IsChecked.Value;
			populateList();
		}
	}

	private void ToggleCrusader_Changed(object sender, RoutedEventArgs e)
	{
		if (!RefToggleCrusader.IsChecked.Value && !RefToggleArabian.IsChecked.Value && !RefToggleBedouin.IsChecked.Value)
		{
			RefToggleArabian.IsChecked = true;
		}
		calcFreebuildOpponents();
	}

	private void ToggleArabian_Changed(object sender, RoutedEventArgs e)
	{
		if (!RefToggleCrusader.IsChecked.Value && !RefToggleArabian.IsChecked.Value && !RefToggleBedouin.IsChecked.Value)
		{
			RefToggleBedouin.IsChecked = true;
		}
		calcFreebuildOpponents();
	}

	private void ToggleBedouin_Changed(object sender, RoutedEventArgs e)
	{
		if (!RefToggleCrusader.IsChecked.Value && !RefToggleArabian.IsChecked.Value && !RefToggleBedouin.IsChecked.Value)
		{
			RefToggleCrusader.IsChecked = true;
		}
		calcFreebuildOpponents();
	}

	private void calcFreebuildOpponents()
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
			if (RefStandaloneFreebuildHostileAnimals.IsChecked == true && RefStandaloneFreebuildHostileAnimals.Visibility == Visibility.Visible)
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
			restartMapInfo.freebuild_Defeat_On_Death = RefDefeatCheck.IsChecked.Value;
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
				MainViewModel.Instance.FreeBuildOptionsVis = Visibility.Visible;
				UpdateFreebuildValues();
			}
			else
			{
				MainViewModel.Instance.FreeBuildOptionsVis = Visibility.Hidden;
			}
			break;
		case "MouseEnter_Next":
			RefCarouselNext.Opacity = 0.9f;
			break;
		case "MouseLeave_Next":
			RefCarouselNext.Opacity = 0.6f;
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
			MainViewModel.Instance.StandaloneFilterLabelVis = Visibility.Visible;
			MainViewModel.Instance.StandaloneFilterButtonVis = Visibility.Hidden;
			break;
		}
	}

	private void UpdateButtons()
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
		RefStandaloneFreebuildHostileAnimals.Visibility = Visibility.Hidden;
		if (selectedHeader == null)
		{
			RefStandaloneFreebuildAdvanced.Visibility = Visibility.Hidden;
		}
		else if (missionType == Enums.StartUpUIPanels.FreeBuild)
		{
			MainViewModel.Instance.StandaloneAttackDefendText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_ENGINEER);
			RefStandaloneFreebuildAdvanced.Visibility = Visibility.Visible;
			if (selectedHeader.hostileAnimals > 0)
			{
				RefStandaloneFreebuildHostileAnimals.Visibility = Visibility.Visible;
			}
		}
		else
		{
			MainViewModel.Instance.StandaloneAttackDefendText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_LADDERMAN);
			RefStandaloneFreebuildAdvanced.Visibility = Visibility.Hidden;
		}
	}

	private void initTroopPoints()
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

	private void reset_troop_points()
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

	private void adjust_troops_difficulty_levels(int current_level)
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

	private void set_adjust_troop_difficulty_level(int level)
	{
		for (int i = 0; i < 21; i++)
		{
			adjusted_start_troops[i] = adjusted_start_troops_levels[level][i];
		}
		troop_points = troop_points_levels[level];
	}

	public void Update()
	{
		if (RefFileLists.SelectedItem == null && RefFileLists.Items.Count > 0)
		{
			RefFileLists.SelectedItem = RefFileLists.Items[0];
		}
		if (!((DateTime.UtcNow - lastScrollTest).TotalMilliseconds > 150.0))
		{
			return;
		}
		if (KeyManager.instance.CursorUpHeld)
		{
			lastScrollTest = DateTime.UtcNow;
			ScrollViewer scrollViewer = MainViewModel.GetScrollViewer(RefFileLists) as ScrollViewer;
			if (!(scrollViewer != null))
			{
				return;
			}
			if (RefFileLists.SelectedItem == null)
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 30f);
				return;
			}
			if (RefFileLists.SelectedIndex > 0)
			{
				RefFileLists.SelectedIndex--;
			}
			RefFileLists.ScrollIntoView(RefFileLists.SelectedItem);
		}
		else
		{
			if (!KeyManager.instance.CursorDownHeld)
			{
				return;
			}
			lastScrollTest = DateTime.UtcNow;
			ScrollViewer scrollViewer2 = MainViewModel.GetScrollViewer(RefFileLists) as ScrollViewer;
			if (!(scrollViewer2 != null))
			{
				return;
			}
			if (RefFileLists.SelectedItem == null)
			{
				scrollViewer2.ScrollToVerticalOffset(scrollViewer2.VerticalOffset + 30f);
				return;
			}
			if (RefFileLists.SelectedIndex < RefFileLists.Items.Count - 1)
			{
				RefFileLists.SelectedIndex++;
			}
			RefFileLists.ScrollIntoView(RefFileLists.SelectedItem);
		}
	}

	private void UpdateFreebuildValues()
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

	private string getGoodsLevelText(int level)
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

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_StandaloneMission.xaml");
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
		if ((bool)e.NewValue)
		{
			MainViewModel.Instance.StandaloneFilterLabelVis = Visibility.Hidden;
		}
		else if (RefSA_SearchFilter.Text.Length == 0)
		{
			MainViewModel.Instance.StandaloneFilterLabelVis = Visibility.Visible;
		}
	}

	private void FilterTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateList();
			if (RefSA_SearchFilter.Text.Length == 0)
			{
				MainViewModel.Instance.StandaloneFilterButtonVis = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.StandaloneFilterButtonVis = Visibility.Visible;
			}
		}
	}

	private void TextBoxCheckForEscape(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			base.Keyboard.ClearFocus();
			KeyManager.instance.ignoreEscape();
		}
	}

	private void TextBoxEnterCheck(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			e.Handled = true;
			base.Keyboard.ClearFocus();
		}
	}
}
