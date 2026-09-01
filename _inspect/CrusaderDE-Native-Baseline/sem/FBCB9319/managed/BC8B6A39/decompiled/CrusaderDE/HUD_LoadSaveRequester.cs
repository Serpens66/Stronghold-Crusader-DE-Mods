using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class HUD_LoadSaveRequester : UserControl
{
	private WGT_Heading RefHeading;

	private Noesis.Grid RefViewRoot;

	private ListView RefFileLists;

	private TextBox RefFileName;

	private TextBox RefSearchFilter;

	private Noesis.Grid RefRadarGrid;

	private Button RefActionButton;

	private CheckBox RefHideQuicksaveCheck;

	private Button RefButtonOpenFolder;

	private bool loadRequester;

	private bool saveNotMapRequester;

	private FileHeader selectedHeader;

	private static string rememberedSaveName = "";

	private static string rememberedMapName = "";

	private static HUD_LoadSaveRequester instance1 = null;

	private static HUD_LoadSaveRequester instance2 = null;

	private static HUD_LoadSaveRequester instance3 = null;

	private static HUD_LoadSaveRequester instance4 = null;

	private static int MPCrcCount = 0;

	private bool panelActive;

	private DateTime lastScrollTest = DateTime.MinValue;

	private ObservableCollection<FileRow> rows = new ObservableCollection<FileRow>();

	private Action<string, FileHeader> OKAction;

	private Action CancelAction;

	private Enums.RequesterTypes requesterType;

	private int sortByColumn;

	private bool sortByAscending = true;

	private bool coopOnly;

	private List<FileHeader> headerlist;

	public HUD_LoadSaveRequester()
	{
		InitializeComponent();
		if (instance1 == null)
		{
			instance1 = this;
		}
		else if (instance2 == null)
		{
			instance2 = this;
		}
		else if (instance3 == null)
		{
			instance3 = this;
		}
		else if (instance4 == null)
		{
			instance4 = this;
		}
		RefHeading = (WGT_Heading)FindName("RequesterHeader");
		RefViewRoot = (Noesis.Grid)FindName("LayoutRoot");
		RefFileLists = (ListView)FindName("FileList");
		RefActionButton = (Button)FindName("ButtonAction");
		RefButtonOpenFolder = (Button)FindName("ButtonOpenFolder");
		RefFileName = (TextBox)FindName("FileName");
		RefFileName.IsKeyboardFocusedChanged += TextInputFocus;
		RefFileName.TextChanged += TextChangedHandler;
		RefFileName.Loaded += TextBoxLoaded;
		RefFileName.PreviewTextInput += FileNameValidationTextBox;
		RefFileName.PreviewKeyDown += TextBoxCheckForEscape;
		RefSearchFilter = (TextBox)FindName("SearchFilter");
		RefSearchFilter.IsKeyboardFocusedChanged += FilterTextInputFocus;
		RefSearchFilter.TextChanged += FilterTextChangedHandler;
		RefSearchFilter.PreviewKeyDown += TextBoxCheckForEscape;
		RefSearchFilter.PreviewTextInput += TextBoxEnterCheck;
		RefHideQuicksaveCheck = (CheckBox)FindName("HideQuicksaveCheck");
		RefHideQuicksaveCheck.Checked += QuickSave_ValueChanged;
		RefHideQuicksaveCheck.Unchecked += QuickSave_ValueChanged;
		RefRadarGrid = (Noesis.Grid)FindName("RadarRequesterGrid");
		RefFileLists.MouseDoubleClick += delegate
		{
			if (RefFileLists.SelectedItem != null && loadRequester)
			{
				FileHeader fileHeader = ((FileRow)RefFileLists.SelectedItem).fileHeader;
				if (fileHeader != null)
				{
					updateRadarTexture(fileHeader);
					selectedHeader = fileHeader;
					RefActionButton.IsEnabled = true;
					ButtonClicked(1, fromDoubleClick: true);
				}
			}
		};
		GridView obj = (GridView)RefFileLists.View;
		GridViewColumnHeader obj2 = (GridViewColumnHeader)obj.Columns[0].Header;
		obj2.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		obj2.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj3 = (GridViewColumnHeader)obj.Columns[1].Header;
		obj3.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 28);
		obj3.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj4 = (GridViewColumnHeader)obj.Columns[2].Header;
		obj4.Content = "";
		obj4.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj5 = (GridViewColumnHeader)obj.Columns[3].Header;
		obj5.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_TYPE);
		obj5.Click += FileListHeaderClickedHandler;
		RefFileLists.SelectionChanged += delegate
		{
			if (RefFileLists.SelectedItem != null)
			{
				if (loadRequester)
				{
					FileHeader fileHeader = ((FileRow)RefFileLists.SelectedItem).fileHeader;
					if (fileHeader != null)
					{
						updateRadarTexture(fileHeader);
						selectedHeader = fileHeader;
						RefActionButton.IsEnabled = true;
					}
				}
				else
				{
					FileHeader fileHeader2 = ((FileRow)RefFileLists.SelectedItem).fileHeader;
					if (fileHeader2 != null)
					{
						MainViewModel.Instance.LoadSaveFileName = fileHeader2.display_filename;
					}
				}
			}
		};
		if (FatControler.portuguese || FatControler.czech)
		{
			PropEx.SetGlowButtonFontSize(RefButtonOpenFolder, 14);
			PropEx.SetGlowButtonTextHeight(RefButtonOpenFolder, 20);
		}
		if (FatControler.german)
		{
			RefHideQuicksaveCheck.Margin = new Thickness(25f, 440f, 67f, 0f);
		}
		if (FatControler.arabic && ConfigSettings.Settings_ArabicL2R)
		{
			RefHideQuicksaveCheck.Width = 200f;
		}
	}

	public void Update()
	{
		if (!((DateTime.UtcNow - lastScrollTest).TotalMilliseconds > 150.0))
		{
			return;
		}
		if (requesterType == Enums.RequesterTypes.LoadMultiplayerGame || requesterType == Enums.RequesterTypes.LoadMultiplayerCoopGame)
		{
			UpdateMPRows();
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

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_LoadSaveRequester.xaml");
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

	public static void OpenLoadSaveRequester(Enums.RequesterTypes reqType, Action<string, FileHeader> _OKAction, Action _cancelAction, int MPCrcCount = -1, bool skirmishScreen = false, bool trailsScreen = false)
	{
		if (trailsScreen)
		{
			MainViewModel.Instance.Show_HUD_LoadSaveRequesterTrails = true;
		}
		else if (reqType != Enums.RequesterTypes.LoadMultiplayerGame && !skirmishScreen && reqType != Enums.RequesterTypes.LoadMultiplayerCoopGame)
		{
			MainViewModel.Instance.Show_HUD_LoadSaveRequester = true;
		}
		else
		{
			MainViewModel.Instance.Show_HUD_LoadSaveRequesterMP = true;
		}
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDLoadSaveRequester = instance1;
		}
		else if (instance2.IsVisible)
		{
			MainViewModel.Instance.HUDLoadSaveRequester = instance2;
		}
		else if (instance3.IsVisible)
		{
			MainViewModel.Instance.HUDLoadSaveRequester = instance3;
		}
		else if (instance4.IsVisible)
		{
			MainViewModel.Instance.HUDLoadSaveRequester = instance4;
		}
		MainViewModel.Instance.HUDLoadSaveRequester._OpenLoadSaveRequester(reqType, _OKAction, _cancelAction, MPCrcCount);
	}

	private void _OpenLoadSaveRequester(Enums.RequesterTypes reqType, Action<string, FileHeader> _OKAction, Action _cancelAction, int _MPCrcCount = -1)
	{
		MPCrcCount = _MPCrcCount;
		panelActive = false;
		requesterType = reqType;
		OKAction = _OKAction;
		CancelAction = _cancelAction;
		bool saveHideQuicksaveVisible = false;
		MainViewModel.Instance.LoadSaveFilter = "";
		MainViewModel.Instance.LoadSaveFilterLabelVis = Visibility.Visible;
		MainViewModel.Instance.LoadSaveFilterButtonVis = Visibility.Hidden;
		coopOnly = false;
		sortByColumn = 1;
		sortByAscending = false;
		RefRadarGrid.Visibility = Visibility.Hidden;
		MainViewModel.Instance.LoadSaveDepthSorting = 0;
		MainViewModel.Instance.ColumnWidthName = "314";
		MainViewModel.Instance.ColumnWidthDate = "150";
		MainViewModel.Instance.ColumnWidthSize = "0";
		MainViewModel.Instance.ColumnWidthType = "200";
		MainViewModel.Instance.Show_Radar160Border = false;
		MainViewModel.Instance.Show_Radar300Border = false;
		MainViewModel.Instance.Show_Radar500Border = false;
		MainViewModel.Instance.Show_Radar700Border = false;
		switch (reqType)
		{
		case Enums.RequesterTypes.LoadSinglePlayerGame:
		{
			loadRequester = true;
			saveNotMapRequester = true;
			saveHideQuicksaveVisible = true;
			MainViewModel instance9 = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, Enums.eTextValues.TEXT_SCN_HELP));
			instance9.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			break;
		}
		case Enums.RequesterTypes.LoadSinglePlayerCoopGame:
		{
			loadRequester = true;
			saveNotMapRequester = true;
			saveHideQuicksaveVisible = true;
			coopOnly = true;
			MainViewModel instance8 = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, Enums.eTextValues.TEXT_SCN_HELP));
			instance8.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			break;
		}
		case Enums.RequesterTypes.LoadMultiplayerGame:
		{
			loadRequester = true;
			saveNotMapRequester = true;
			MainViewModel instance7 = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 38));
			instance7.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			break;
		}
		case Enums.RequesterTypes.LoadMultiplayerCoopGame:
		{
			loadRequester = true;
			saveNotMapRequester = true;
			coopOnly = true;
			MainViewModel instance6 = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 38));
			instance6.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			break;
		}
		case Enums.RequesterTypes.LoadEditorMap:
		{
			loadRequester = true;
			saveNotMapRequester = false;
			MainViewModel instance5 = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, 2));
			instance5.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			MainViewModel.Instance.ColumnWidthName = "284";
			MainViewModel.Instance.ColumnWidthDate = "140";
			MainViewModel.Instance.ColumnWidthSize = "40";
			MainViewModel.Instance.ColumnWidthType = "200";
			break;
		}
		case Enums.RequesterTypes.LoadUserWorkshopMap:
		{
			loadRequester = true;
			saveNotMapRequester = false;
			MainViewModel instance4 = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, 2));
			instance4.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			break;
		}
		case Enums.RequesterTypes.SaveSinglePlayerGame:
		{
			loadRequester = false;
			saveNotMapRequester = true;
			saveHideQuicksaveVisible = true;
			MainViewModel instance3 = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 3));
			instance3.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			break;
		}
		case Enums.RequesterTypes.SaveMultiplayerGame:
		{
			loadRequester = false;
			saveNotMapRequester = true;
			MainViewModel instance2 = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 3));
			instance2.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			break;
		}
		case Enums.RequesterTypes.SaveEditorMap:
		{
			loadRequester = false;
			saveNotMapRequester = false;
			MainViewModel instance = MainViewModel.Instance;
			string buttonLoadSaveActionText = (RefHeading.HeadingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, 3));
			instance.ButtonLoadSaveActionText = buttonLoadSaveActionText;
			MainViewModel.Instance.LoadSaveDepthSorting = 4;
			MainViewModel.Instance.ColumnWidthName = "284";
			MainViewModel.Instance.ColumnWidthDate = "140";
			MainViewModel.Instance.ColumnWidthSize = "40";
			MainViewModel.Instance.ColumnWidthType = "200";
			break;
		}
		}
		if (loadRequester)
		{
			RefFileName.Visibility = Visibility.Hidden;
			MainViewModel.Instance.Show_RequesterRadar = true;
		}
		else
		{
			RefFileName.Visibility = Visibility.Visible;
			updateSaveRadarTexture();
			MainViewModel.Instance.Show_RequesterRadar = true;
			if (saveNotMapRequester)
			{
				MainViewModel.Instance.LoadSaveFileName = rememberedSaveName;
			}
			else
			{
				MainViewModel.Instance.LoadSaveFileName = rememberedMapName;
			}
			RefFileName.Focus();
			Director.instance.GenericDelayCoroutine(delegate
			{
				RefFileName.Select(0, MainViewModel.Instance.LoadSaveFileName.Length);
			}, 0.2f);
		}
		if (saveNotMapRequester)
		{
			MainViewModel.Instance.LoadSave_FolderText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 163);
		}
		else
		{
			MainViewModel.Instance.LoadSave_FolderText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 164);
		}
		MainViewModel.Instance.SaveHideQuicksaveVisible = saveHideQuicksaveVisible;
		RefHideQuicksaveCheck.IsChecked = false;
		if (MainViewModel.Instance.LoadSaveFileName.Length > 0)
		{
			RefActionButton.IsEnabled = true;
		}
		else
		{
			RefActionButton.IsEnabled = false;
		}
		selectedHeader = null;
		populateList();
		if (RefFileLists.Items.Count > 0)
		{
			RefFileLists.ScrollIntoView(RefFileLists.Items[0]);
			if (loadRequester)
			{
				RefFileLists.SelectedItem = RefFileLists.Items[0];
			}
		}
		panelActive = true;
	}

	public void ButtonClicked(int function, bool fromDoubleClick = false)
	{
		switch (function)
		{
		case 1:
			MainViewModel.Instance.Show_HUD_LoadSaveRequester = false;
			MainViewModel.Instance.Show_HUD_LoadSaveRequesterMP = false;
			MainViewModel.Instance.Show_HUD_LoadSaveRequesterTrails = false;
			if (!loadRequester && headerlist != null)
			{
				bool flag = false;
				string text = MainViewModel.Instance.LoadSaveFileName.ToLower();
				foreach (FileHeader item in headerlist)
				{
					if (text == item.display_filename.ToLower())
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					SFXManager.instance.playSpeech(1, "General_Message11.wav", 1f);
					HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 30), delegate
					{
						if (!Director.instance.MultiplayerGame)
						{
							SFXManager.instance.playSpeech(1, "General_Saving.wav", 1f);
						}
						RunOKAction();
					}, delegate
					{
						MainViewModel.Instance.Show_HUD_LoadSaveRequester = true;
					});
					break;
				}
			}
			if (loadRequester)
			{
				SFXManager.instance.playSpeech(1, "General_Loading.wav", 1f);
			}
			else if (!Director.instance.MultiplayerGame)
			{
				SFXManager.instance.playSpeech(1, "General_Saving.wav", 1f);
			}
			RunOKAction();
			if (fromDoubleClick)
			{
				EditorDirector.instance.IgnoreNextMouseDown();
			}
			break;
		case 2:
			MainViewModel.Instance.Show_HUD_LoadSaveRequester = false;
			MainViewModel.Instance.Show_HUD_LoadSaveRequesterMP = false;
			MainViewModel.Instance.Show_HUD_LoadSaveRequesterTrails = false;
			CloseRequester();
			break;
		case 3:
			if (saveNotMapRequester)
			{
				try
				{
					string savesPath = ConfigSettings.GetSavesPath();
					Application.OpenURL("file://" + savesPath);
					break;
				}
				catch (Exception)
				{
					break;
				}
			}
			try
			{
				string userMapsPath = ConfigSettings.GetUserMapsPath();
				Application.OpenURL("file://" + userMapsPath);
				break;
			}
			catch (Exception)
			{
				break;
			}
		case 4:
			MainViewModel.Instance.LoadSaveFilter = "";
			MainViewModel.Instance.LoadSaveFilterLabelVis = Visibility.Visible;
			MainViewModel.Instance.LoadSaveFilterButtonVis = Visibility.Hidden;
			break;
		}
	}

	private void RunOKAction()
	{
		if (OKAction != null)
		{
			OKAction(MainViewModel.Instance.LoadSaveFileName, selectedHeader);
		}
		if (loadRequester)
		{
			if (selectedHeader != null)
			{
				if (saveNotMapRequester)
				{
					rememberedSaveName = selectedHeader.display_filename;
				}
				else
				{
					rememberedMapName = selectedHeader.display_filename;
				}
				GameData.Instance.currentFileName = selectedHeader.display_filename;
			}
		}
		else
		{
			if (saveNotMapRequester)
			{
				rememberedSaveName = MainViewModel.Instance.LoadSaveFileName;
			}
			else
			{
				rememberedMapName = MainViewModel.Instance.LoadSaveFileName;
			}
			GameData.Instance.currentFileName = MainViewModel.Instance.LoadSaveFileName;
		}
	}

	public void CloseRequester()
	{
		MainViewModel.Instance.Show_HUD_LoadSaveRequester = false;
		MainViewModel.Instance.Show_HUD_LoadSaveRequesterMP = false;
		MainViewModel.Instance.Show_HUD_LoadSaveRequesterTrails = false;
		if (CancelAction != null)
		{
			CancelAction();
		}
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
		case "Type":
			if (sortByColumn == 2)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 2;
			sortByAscending = true;
			break;
		}
		populateList();
	}

	private void populateList()
	{
		headerlist = null;
		bool flag = false;
		switch (requesterType)
		{
		case Enums.RequesterTypes.LoadSinglePlayerGame:
		case Enums.RequesterTypes.SaveSinglePlayerGame:
		case Enums.RequesterTypes.LoadSinglePlayerCoopGame:
			switch (sortByColumn)
			{
			case 0:
				headerlist = MapFileManager.Instance.GetSaves(0, sortByAscending, RefHideQuicksaveCheck.IsChecked.Value, coopOnly);
				break;
			case 1:
				headerlist = MapFileManager.Instance.GetSaves(1, sortByAscending, RefHideQuicksaveCheck.IsChecked.Value, coopOnly);
				break;
			case 2:
				headerlist = MapFileManager.Instance.GetSaves(4, sortByAscending, RefHideQuicksaveCheck.IsChecked.Value, coopOnly);
				break;
			}
			break;
		case Enums.RequesterTypes.LoadMultiplayerGame:
		case Enums.RequesterTypes.SaveMultiplayerGame:
		case Enums.RequesterTypes.LoadMultiplayerCoopGame:
			switch (sortByColumn)
			{
			case 0:
				headerlist = MapFileManager.Instance.GetMPSaves(0, sortByAscending, coopOnly);
				break;
			case 1:
				headerlist = MapFileManager.Instance.GetMPSaves(1, sortByAscending, coopOnly);
				break;
			case 2:
				headerlist = MapFileManager.Instance.GetMPSaves(4, sortByAscending, coopOnly);
				break;
			}
			flag = true;
			break;
		case Enums.RequesterTypes.LoadEditorMap:
		case Enums.RequesterTypes.SaveEditorMap:
			switch (sortByColumn)
			{
			case 0:
				headerlist = MapFileManager.Instance.GetMapEditableMaps(0, sortByAscending);
				break;
			case 1:
				headerlist = MapFileManager.Instance.GetMapEditableMaps(1, sortByAscending);
				break;
			case 2:
				headerlist = MapFileManager.Instance.GetMapEditableMaps(4, sortByAscending);
				break;
			case 3:
				headerlist = MapFileManager.Instance.GetMapEditableMaps(3, sortByAscending);
				break;
			}
			break;
		case Enums.RequesterTypes.LoadUserWorkshopMap:
			switch (sortByColumn)
			{
			case 0:
				headerlist = MapFileManager.Instance.GetUserWorkshopUploads(0, sortByAscending);
				break;
			case 1:
				headerlist = MapFileManager.Instance.GetUserWorkshopUploads(1, sortByAscending);
				break;
			case 2:
				headerlist = MapFileManager.Instance.GetUserWorkshopUploads(4, sortByAscending);
				break;
			}
			break;
		}
		if (headerlist == null)
		{
			return;
		}
		string text = RefSearchFilter.Text;
		string value = text.ToLowerInvariant();
		rows.Clear();
		foreach (FileHeader item in headerlist)
		{
			if ((requesterType == Enums.RequesterTypes.LoadMultiplayerGame || requesterType == Enums.RequesterTypes.LoadMultiplayerCoopGame) && item.retrieveCRCChecks != MPCrcCount)
			{
				item.rowVisible = false;
			}
			else
			{
				if (text.Length > 0 && !item.display_filename.Contains(text) && !item.display_filename.ToLowerInvariant().Contains(value))
				{
					continue;
				}
				FileRow fileRow = new FileRow();
				fileRow.Text1 = item.display_filename;
				fileRow.Text2 = item.getDateString();
				if (item.world_size > 0)
				{
					fileRow.Text4 = item.world_size.ToString();
				}
				else
				{
					fileRow.Text4 = "";
				}
				if (flag)
				{
					if (item.coopTrailID > 0)
					{
						fileRow.Text3 = item.getGameTypeString();
					}
					else
					{
						fileRow.Text3 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, 13);
					}
				}
				else
				{
					fileRow.Text3 = item.getGameTypeString();
				}
				fileRow.fileHeader = item;
				rows.Add(fileRow);
			}
		}
		RefFileLists.ItemsSource = rows;
	}

	private void UpdateMPRows()
	{
		bool flag = false;
		foreach (FileHeader item in headerlist)
		{
			if (!item.rowVisible && item.retrieveCRCChecks == MPCrcCount)
			{
				FileRow fileRow = new FileRow();
				fileRow.Text1 = item.display_filename;
				fileRow.Text2 = item.getDateString();
				if (item.coopTrailID > 0)
				{
					fileRow.Text3 = item.getGameTypeString();
				}
				else
				{
					fileRow.Text3 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, 13);
				}
				fileRow.fileHeader = item;
				rows.Add(fileRow);
				item.rowVisible = true;
			}
		}
		if (flag)
		{
			RefFileLists.ItemsSource = rows;
		}
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
				TextureSource radarRequesterImage = new TextureSource(MapFileManager.Instance.GetLoadSaveRadarPreview(radarFromFile));
				MainViewModel.Instance.RadarRequesterImage = radarRequesterImage;
				RefRadarGrid.Visibility = Visibility.Visible;
			}
			else
			{
				RefRadarGrid.Visibility = Visibility.Hidden;
			}
		}
		else
		{
			RefRadarGrid.Visibility = Visibility.Hidden;
		}
	}

	private void updateSaveRadarTexture()
	{
		MainViewModel.Instance.Show_Radar160Border = false;
		MainViewModel.Instance.Show_Radar300Border = false;
		MainViewModel.Instance.Show_Radar500Border = false;
		MainViewModel.Instance.Show_Radar700Border = false;
		int world_size = 0;
		int[] keep_locations = null;
		byte[] saveRadar = EngineInterface.getSaveRadar(ref keep_locations, ref world_size);
		if (saveRadar != null)
		{
			switch (world_size)
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
			TextureSource radarRequesterImage = new TextureSource(MapFileManager.Instance.GetLoadSaveRadarPreview(saveRadar));
			MainViewModel.Instance.RadarRequesterImage = radarRequesterImage;
			RefRadarGrid.Visibility = Visibility.Visible;
		}
		else
		{
			RefRadarGrid.Visibility = Visibility.Hidden;
		}
	}

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	private void FilterTextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
		if ((bool)e.NewValue)
		{
			MainViewModel.Instance.LoadSaveFilterLabelVis = Visibility.Hidden;
		}
		else if (RefSearchFilter.Text.Length == 0)
		{
			MainViewModel.Instance.LoadSaveFilterLabelVis = Visibility.Visible;
		}
	}

	private void TextBoxLoaded(object sender, RoutedEventArgs e)
	{
		if (!loadRequester)
		{
			RefFileName.Focus();
		}
	}

	private void TextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (!loadRequester)
		{
			if (RefFileName.Text.Length > 0)
			{
				RefActionButton.IsEnabled = true;
			}
			else
			{
				RefActionButton.IsEnabled = false;
			}
		}
	}

	private void FilterTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateList();
			if (RefSearchFilter.Text.Length == 0)
			{
				MainViewModel.Instance.LoadSaveFilterButtonVis = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.LoadSaveFilterButtonVis = Visibility.Visible;
			}
		}
	}

	private void FileNameValidationTextBox(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			e.Handled = true;
			base.Keyboard.ClearFocus();
			return;
		}
		char[] invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
		string text = e.Text;
		foreach (char value in text)
		{
			if (invalidFileNameChars.Contains(value))
			{
				e.Handled = true;
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

	private void QuickSave_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateList();
		}
	}

	public static void ClearSavedName(string mapName = "")
	{
		if (mapName.Length > 0)
		{
			List<FileHeader> saves = MapFileManager.Instance.GetSaves(0, sortAscend: true, excludeQuicksaves: true);
			if (saves != null)
			{
				for (int i = 1; i < 1000; i++)
				{
					string text = mapName + "-" + i;
					bool flag = false;
					string text2 = text.ToLower();
					foreach (FileHeader item in saves)
					{
						if (text2 == item.display_filename.ToLower())
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						mapName = text;
						break;
					}
				}
			}
		}
		rememberedSaveName = mapName;
	}
}
