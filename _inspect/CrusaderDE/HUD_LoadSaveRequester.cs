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
	public WGT_Heading RefHeading;

	public Grid RefViewRoot;

	public ListView RefFileLists;

	public TextBox RefFileName;

	public TextBox RefSearchFilter;

	public Grid RefRadarGrid;

	public Button RefActionButton;

	public CheckBox RefHideQuicksaveCheck;

	public Button RefButtonOpenFolder;

	public bool loadRequester;

	public bool saveNotMapRequester;

	public FileHeader selectedHeader;

	public static string rememberedSaveName = "";

	public static string rememberedMapName = "";

	public static HUD_LoadSaveRequester instance1 = null;

	public static HUD_LoadSaveRequester instance2 = null;

	public static HUD_LoadSaveRequester instance3 = null;

	public static HUD_LoadSaveRequester instance4 = null;

	public static int MPCrcCount = 0;

	public bool panelActive;

	public DateTime lastScrollTest = DateTime.MinValue;

	public ObservableCollection<FileRow> rows = new ObservableCollection<FileRow>();

	public Action<string, FileHeader> OKAction;

	public Action CancelAction;

	public Enums.RequesterTypes requesterType;

	public int sortByColumn;

	public bool sortByAscending = true;

	public bool coopOnly;

	public List<FileHeader> headerlist;

	public HUD_LoadSaveRequester()
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Expected O, but got Unknown
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Expected O, but got Unknown
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Expected O, but got Unknown
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Expected O, but got Unknown
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Expected O, but got Unknown
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Expected O, but got Unknown
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Expected O, but got Unknown
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Expected O, but got Unknown
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Expected O, but got Unknown
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Expected O, but got Unknown
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Expected O, but got Unknown
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Expected O, but got Unknown
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Expected O, but got Unknown
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Expected O, but got Unknown
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Expected O, but got Unknown
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Expected O, but got Unknown
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Expected O, but got Unknown
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Expected O, but got Unknown
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Expected O, but got Unknown
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		//IL_030d: Expected O, but got Unknown
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_0340: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Expected O, but got Unknown
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		//IL_0361: Expected O, but got Unknown
		//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
		InitializeComponent();
		if ((BaseComponent)(object)instance1 == (BaseComponent)null)
		{
			instance1 = this;
		}
		else if ((BaseComponent)(object)instance2 == (BaseComponent)null)
		{
			instance2 = this;
		}
		else if ((BaseComponent)(object)instance3 == (BaseComponent)null)
		{
			instance3 = this;
		}
		else if ((BaseComponent)(object)instance4 == (BaseComponent)null)
		{
			instance4 = this;
		}
		RefHeading = (WGT_Heading)((FrameworkElement)this).FindName("RequesterHeader");
		RefViewRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		RefFileLists = (ListView)((FrameworkElement)this).FindName("FileList");
		RefActionButton = (Button)((FrameworkElement)this).FindName("ButtonAction");
		RefButtonOpenFolder = (Button)((FrameworkElement)this).FindName("ButtonOpenFolder");
		RefFileName = (TextBox)((FrameworkElement)this).FindName("FileName");
		((UIElement)RefFileName).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefFileName).TextChanged += new RoutedEventHandler(TextChangedHandler);
		((FrameworkElement)RefFileName).Loaded += new RoutedEventHandler(TextBoxLoaded);
		((UIElement)RefFileName).PreviewTextInput += new TextCompositionEventHandler(FileNameValidationTextBox);
		((UIElement)RefFileName).PreviewKeyDown += new KeyEventHandler(TextBoxCheckForEscape);
		RefSearchFilter = (TextBox)((FrameworkElement)this).FindName("SearchFilter");
		((UIElement)RefSearchFilter).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(FilterTextInputFocus);
		((TextBoxBase)RefSearchFilter).TextChanged += new RoutedEventHandler(FilterTextChangedHandler);
		((UIElement)RefSearchFilter).PreviewKeyDown += new KeyEventHandler(TextBoxCheckForEscape);
		((UIElement)RefSearchFilter).PreviewTextInput += new TextCompositionEventHandler(TextBoxEnterCheck);
		RefHideQuicksaveCheck = (CheckBox)((FrameworkElement)this).FindName("HideQuicksaveCheck");
		((ToggleButton)RefHideQuicksaveCheck).Checked += new RoutedEventHandler(QuickSave_ValueChanged);
		((ToggleButton)RefHideQuicksaveCheck).Unchecked += new RoutedEventHandler(QuickSave_ValueChanged);
		RefRadarGrid = (Grid)((FrameworkElement)this).FindName("RadarRequesterGrid");
		((Control)RefFileLists).MouseDoubleClick += (MouseButtonEventHandler)delegate
		{
			if (((Selector)RefFileLists).SelectedItem != null && loadRequester)
			{
				FileHeader fileHeader = ((FileRow)((Selector)RefFileLists).SelectedItem).fileHeader;
				if (fileHeader != null)
				{
					updateRadarTexture(fileHeader);
					selectedHeader = fileHeader;
					((UIElement)RefActionButton).IsEnabled = true;
					ButtonClicked(1, fromDoubleClick: true);
				}
			}
		};
		GridView val = (GridView)RefFileLists.View;
		GridViewColumnHeader val2 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[0].Header;
		((ContentControl)val2).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		((ButtonBase)val2).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val3 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[1].Header;
		((ContentControl)val3).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 28);
		((ButtonBase)val3).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val4 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[2].Header;
		((ContentControl)val4).Content = "";
		((ButtonBase)val4).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val5 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[3].Header;
		((ContentControl)val5).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_TYPE);
		((ButtonBase)val5).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		((Selector)RefFileLists).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefFileLists).SelectedItem != null)
			{
				if (loadRequester)
				{
					FileHeader fileHeader = ((FileRow)((Selector)RefFileLists).SelectedItem).fileHeader;
					if (fileHeader != null)
					{
						updateRadarTexture(fileHeader);
						selectedHeader = fileHeader;
						((UIElement)RefActionButton).IsEnabled = true;
					}
				}
				else
				{
					FileHeader fileHeader2 = ((FileRow)((Selector)RefFileLists).SelectedItem).fileHeader;
					if (fileHeader2 != null)
					{
						MainViewModel.Instance.LoadSaveFileName = fileHeader2.display_filename;
					}
				}
			}
		};
		if (FatControler.portuguese || FatControler.czech)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonOpenFolder, 14);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonOpenFolder, 20);
		}
		if (FatControler.german)
		{
			((FrameworkElement)RefHideQuicksaveCheck).Margin = new Thickness(25f, 440f, 67f, 0f);
		}
		if (FatControler.arabic && ConfigSettings.Settings_ArabicL2R)
		{
			((FrameworkElement)RefHideQuicksaveCheck).Width = 200f;
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

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_LoadSaveRequester.xaml");
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
		if (((UIElement)instance1).IsVisible)
		{
			MainViewModel.Instance.HUDLoadSaveRequester = instance1;
		}
		else if (((UIElement)instance2).IsVisible)
		{
			MainViewModel.Instance.HUDLoadSaveRequester = instance2;
		}
		else if (((UIElement)instance3).IsVisible)
		{
			MainViewModel.Instance.HUDLoadSaveRequester = instance3;
		}
		else if (((UIElement)instance4).IsVisible)
		{
			MainViewModel.Instance.HUDLoadSaveRequester = instance4;
		}
		MainViewModel.Instance.HUDLoadSaveRequester._OpenLoadSaveRequester(reqType, _OKAction, _cancelAction, MPCrcCount);
	}

	public void _OpenLoadSaveRequester(Enums.RequesterTypes reqType, Action<string, FileHeader> _OKAction, Action _cancelAction, int _MPCrcCount = -1)
	{
		MPCrcCount = _MPCrcCount;
		panelActive = false;
		requesterType = reqType;
		OKAction = _OKAction;
		CancelAction = _cancelAction;
		bool saveHideQuicksaveVisible = false;
		MainViewModel.Instance.LoadSaveFilter = "";
		MainViewModel.Instance.LoadSaveFilterLabelVis = (Visibility)2;
		MainViewModel.Instance.LoadSaveFilterButtonVis = (Visibility)1;
		coopOnly = false;
		sortByColumn = 1;
		sortByAscending = false;
		((UIElement)RefRadarGrid).Visibility = (Visibility)1;
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
			((UIElement)RefFileName).Visibility = (Visibility)1;
			MainViewModel.Instance.Show_RequesterRadar = true;
		}
		else
		{
			((UIElement)RefFileName).Visibility = (Visibility)2;
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
			((UIElement)RefFileName).Focus();
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
		((ToggleButton)RefHideQuicksaveCheck).IsChecked = false;
		if (MainViewModel.Instance.LoadSaveFileName.Length > 0)
		{
			((UIElement)RefActionButton).IsEnabled = true;
		}
		else
		{
			((UIElement)RefActionButton).IsEnabled = false;
		}
		selectedHeader = null;
		populateList();
		if (((ItemsControl)RefFileLists).Items.Count > 0)
		{
			((ListBox)RefFileLists).ScrollIntoView(((ItemsControl)RefFileLists).Items[0]);
			if (loadRequester)
			{
				((Selector)RefFileLists).SelectedItem = ((ItemsControl)RefFileLists).Items[0];
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
			MainViewModel.Instance.LoadSaveFilterLabelVis = (Visibility)2;
			MainViewModel.Instance.LoadSaveFilterButtonVis = (Visibility)1;
			break;
		}
	}

	public void RunOKAction()
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

	public void populateList()
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
				headerlist = MapFileManager.Instance.GetSaves(0, sortByAscending, ((ToggleButton)RefHideQuicksaveCheck).IsChecked.Value, coopOnly);
				break;
			case 1:
				headerlist = MapFileManager.Instance.GetSaves(1, sortByAscending, ((ToggleButton)RefHideQuicksaveCheck).IsChecked.Value, coopOnly);
				break;
			case 2:
				headerlist = MapFileManager.Instance.GetSaves(4, sortByAscending, ((ToggleButton)RefHideQuicksaveCheck).IsChecked.Value, coopOnly);
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
		((ItemsControl)RefFileLists).ItemsSource = rows;
	}

	public void UpdateMPRows()
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
			((ItemsControl)RefFileLists).ItemsSource = rows;
		}
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
				TextureSource radarRequesterImage = new TextureSource(MapFileManager.Instance.GetLoadSaveRadarPreview(radarFromFile));
				MainViewModel.Instance.RadarRequesterImage = (ImageSource)(object)radarRequesterImage;
				((UIElement)RefRadarGrid).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefRadarGrid).Visibility = (Visibility)1;
			}
		}
		else
		{
			((UIElement)RefRadarGrid).Visibility = (Visibility)1;
		}
	}

	public void updateSaveRadarTexture()
	{
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Expected O, but got Unknown
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
			MainViewModel.Instance.RadarRequesterImage = (ImageSource)(object)radarRequesterImage;
			((UIElement)RefRadarGrid).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefRadarGrid).Visibility = (Visibility)1;
		}
	}

	public void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	public void FilterTextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
		if ((bool)e.NewValue)
		{
			MainViewModel.Instance.LoadSaveFilterLabelVis = (Visibility)1;
		}
		else if (RefSearchFilter.Text.Length == 0)
		{
			MainViewModel.Instance.LoadSaveFilterLabelVis = (Visibility)2;
		}
	}

	public void TextBoxLoaded(object sender, RoutedEventArgs e)
	{
		if (!loadRequester)
		{
			((UIElement)RefFileName).Focus();
		}
	}

	public void TextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (!loadRequester)
		{
			if (RefFileName.Text.Length > 0)
			{
				((UIElement)RefActionButton).IsEnabled = true;
			}
			else
			{
				((UIElement)RefActionButton).IsEnabled = false;
			}
		}
	}

	public void FilterTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateList();
			if (RefSearchFilter.Text.Length == 0)
			{
				MainViewModel.Instance.LoadSaveFilterButtonVis = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.LoadSaveFilterButtonVis = (Visibility)2;
			}
		}
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

	public void TextBoxEnterCheck(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			((RoutedEventArgs)e).Handled = true;
			((UIElement)this).Keyboard.ClearFocus();
		}
	}

	public void QuickSave_ValueChanged(object sender, RoutedEventArgs e)
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
