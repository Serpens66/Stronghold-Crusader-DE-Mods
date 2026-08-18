using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Noesis;

namespace CrusaderDE;

public class FRONT_Multiplayer_AISettings : UserControl
{
	public Grid RefAIV_Mode;

	public Grid RefLord_Mode;

	public ListView RefFileLists;

	public RadioButton RefAIV_Default;

	public RadioButton RefAIV_Community;

	public RadioButton RefAIV_Historical;

	public RadioButton RefAIV_User;

	public RadioButton RefNo_Rotation;

	public RadioButton RefNorth_Rotation;

	public RadioButton RefEast_Rotation;

	public RadioButton RefSouth_Rotation;

	public RadioButton RefWest_Rotation;

	public Grid RefSelectionDisabledOverlay;

	public Grid RefSelectionDisabledOverlayMP;

	public Button RefPlayer1_Kick;

	public Button RefPlayer2_Kick;

	public Button RefPlayer3_Kick;

	public Button RefPlayer4_Kick;

	public Button RefPlayer5_Kick;

	public Button RefPlayer6_Kick;

	public Button RefPlayer7_Kick;

	public Button RefPlayer8_Kick;

	public TextBlock RefCastlesHeading;

	public ListView RefLordList;

	public RadioButton RefLord_Default;

	public RadioButton RefLord_User;

	public Grid RefLordSelectionDisabledOverlay;

	public static FRONT_Multiplayer_AISettings instance1;

	public static FRONT_Multiplayer_AISettings instance2;

	public FRONT_Multiplayer.MPAIVInfo AIVInfo;

	public ObservableCollection<FileRow> fileRows = new ObservableCollection<FileRow>();

	public ObservableCollection<FileRow> lordRows = new ObservableCollection<FileRow>();

	public List<CustomisationFileManager.CustomAIV> aivList;

	public List<CustomisationFileManager.CustomLordConfig> lordList;

	public bool MPMode;

	public static FRONT_Multiplayer_AISettings Instance
	{
		get
		{
			if (((UIElement)instance1).IsVisible)
			{
				return instance1;
			}
			return instance2;
		}
	}

	public FRONT_Multiplayer_AISettings()
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
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Expected O, but got Unknown
		//IL_029f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Expected O, but got Unknown
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Expected O, but got Unknown
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Expected O, but got Unknown
		//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Expected O, but got Unknown
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Expected O, but got Unknown
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
		if ((BaseComponent)(object)instance1 == (BaseComponent)null)
		{
			instance1 = this;
		}
		else if ((BaseComponent)(object)instance2 == (BaseComponent)null)
		{
			instance2 = this;
		}
		InitializeComponent();
		RefAIV_Mode = (Grid)((FrameworkElement)this).FindName("AIV_Mode");
		RefLord_Mode = (Grid)((FrameworkElement)this).FindName("Lord_Mode");
		RefAIV_Default = (RadioButton)((FrameworkElement)this).FindName("AIV_Default");
		RefAIV_Community = (RadioButton)((FrameworkElement)this).FindName("AIV_Community");
		RefAIV_Historical = (RadioButton)((FrameworkElement)this).FindName("AIV_Historical");
		RefAIV_User = (RadioButton)((FrameworkElement)this).FindName("AIV_User");
		RefNo_Rotation = (RadioButton)((FrameworkElement)this).FindName("No_Rotation");
		RefNorth_Rotation = (RadioButton)((FrameworkElement)this).FindName("North_Rotation");
		RefEast_Rotation = (RadioButton)((FrameworkElement)this).FindName("East_Rotation");
		RefSouth_Rotation = (RadioButton)((FrameworkElement)this).FindName("South_Rotation");
		RefWest_Rotation = (RadioButton)((FrameworkElement)this).FindName("West_Rotation");
		RefPlayer1_Kick = (Button)((FrameworkElement)this).FindName("Player1_Kick");
		RefPlayer2_Kick = (Button)((FrameworkElement)this).FindName("Player2_Kick");
		RefPlayer3_Kick = (Button)((FrameworkElement)this).FindName("Player3_Kick");
		RefPlayer4_Kick = (Button)((FrameworkElement)this).FindName("Player4_Kick");
		RefPlayer5_Kick = (Button)((FrameworkElement)this).FindName("Player5_Kick");
		RefPlayer6_Kick = (Button)((FrameworkElement)this).FindName("Player6_Kick");
		RefPlayer7_Kick = (Button)((FrameworkElement)this).FindName("Player7_Kick");
		RefPlayer8_Kick = (Button)((FrameworkElement)this).FindName("Player8_Kick");
		RefCastlesHeading = (TextBlock)((FrameworkElement)this).FindName("CastlesHeading");
		RefSelectionDisabledOverlay = (Grid)((FrameworkElement)this).FindName("SelectionDisabledOverlay");
		RefSelectionDisabledOverlayMP = (Grid)((FrameworkElement)this).FindName("SelectionDisabledOverlayMP");
		RefFileLists = (ListView)((FrameworkElement)this).FindName("AIVList");
		((ContentControl)(GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)((GridView)RefFileLists.View).Columns)[1].Header).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		((Control)RefFileLists).MouseDoubleClick += (MouseButtonEventHandler)delegate
		{
			AddSelected();
		};
		RefLordList = (ListView)((FrameworkElement)this).FindName("LordList");
		RefLord_Default = (RadioButton)((FrameworkElement)this).FindName("Lord_Default");
		RefLord_User = (RadioButton)((FrameworkElement)this).FindName("Lord_User");
		RefLordSelectionDisabledOverlay = (Grid)((FrameworkElement)this).FindName("LordSelectionDisabledOverlay");
		((Selector)RefLordList).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefLordList).SelectedItem != null)
			{
				AIVInfo.lordConfig = lordList[((Selector)RefLordList).SelectedIndex];
				MainViewModel.Instance.CustomLordName = AIVInfo.lordConfig.name;
			}
		};
		if (FatControler.arabic)
		{
			RefCastlesHeading.FontSize = 27f;
			((FrameworkElement)RefCastlesHeading).Margin = new Thickness(0f, 25f, 0f, 0f);
		}
	}

	public static void Show(int this_player, FRONT_Multiplayer.MPAIVInfo aivInfo, bool MPMode)
	{
		MainViewModel.Instance.Show_MPAISettings = true;
		Instance.Init(aivInfo, MPMode);
	}

	public void Init(FRONT_Multiplayer.MPAIVInfo aivInfo, bool _MPMode)
	{
		MainViewModel.Instance.AI_Settings_Help = "";
		MainViewModel.Instance.Show_AI_Settings_Help = false;
		MainViewModel.Instance.FRONTMultiplayer.hideToolTipTime = DateTime.MinValue;
		if (!aivInfo.builtIn && !aivInfo.community && !aivInfo.historical && (aivInfo.aivs == null || aivInfo.aivs.Count == 0))
		{
			aivInfo.builtIn = true;
		}
		if (aivInfo.lordName != null && aivInfo.lordName.Length > 0)
		{
			MainViewModel.Instance.MPAISettingsHeading = MapFileManager.SplitCustomTrailName(aivInfo.lordName);
			aivList = CustomisationFileManager.Instance.getLordAIVList(-1, aivInfo.lordName);
			lordList = CustomisationFileManager.Instance.getLordLordList(-1, aivInfo.lordName);
			if (aivList != null && lordList != null)
			{
			}
		}
		else
		{
			string lordName = Translate.Instance.GetLordName(aivInfo.lordType);
			MainViewModel.Instance.MPAISettingsHeading = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 0) + " - " + lordName;
			aivList = CustomisationFileManager.Instance.getLordAIVList(aivInfo.lordType);
			lordList = CustomisationFileManager.Instance.getLordLordList(aivInfo.lordType);
		}
		MainViewModel.Instance.Show_MPAI_AIV_Mode = true;
		MainViewModel.Instance.Show_MPAI_Lord_Mode = false;
		((UIElement)RefAIV_Mode).Opacity = 1f;
		((UIElement)RefLord_Mode).Opacity = 0.6f;
		MPMode = _MPMode;
		ulong num = 0uL;
		if (!aivInfo.builtInLord && aivInfo.lordConfig != null)
		{
			num = aivInfo.lordConfig.checksum;
			MainViewModel.Instance.CustomLordName = aivInfo.lordConfig.name;
		}
		else
		{
			MainViewModel.Instance.CustomLordName = "";
		}
		populateList(aivInfo);
		if (num == 0L)
		{
			return;
		}
		for (int i = 0; i < lordList.Count; i++)
		{
			if (lordList[i].checksum == num)
			{
				((Selector)RefLordList).SelectedIndex = i;
				break;
			}
		}
	}

	public void populateList(FRONT_Multiplayer.MPAIVInfo _aivInfo = null, bool doPopulate = true)
	{
		if (_aivInfo != null)
		{
			AIVInfo = _aivInfo;
		}
		_ = AIVInfo.lordType;
		fileRows.Clear();
		lordRows.Clear();
		if (AIVInfo.lordType >= 29)
		{
			((UIElement)RefAIV_Community).IsEnabled = false;
			((UIElement)RefAIV_Historical).IsEnabled = false;
			((UIElement)RefAIV_Community).Opacity = 0.5f;
			((UIElement)RefAIV_Historical).Opacity = 0.5f;
			((UIElement)RefAIV_Default).IsEnabled = false;
			((UIElement)RefAIV_Default).Opacity = 0.5f;
			((UIElement)RefLord_Default).IsEnabled = false;
			((UIElement)RefLord_Default).Opacity = 0.5f;
		}
		else if (AIVInfo.lordType >= 16)
		{
			((UIElement)RefAIV_Community).IsEnabled = false;
			((UIElement)RefAIV_Historical).IsEnabled = false;
			((UIElement)RefAIV_Community).Opacity = 0.5f;
			((UIElement)RefAIV_Historical).Opacity = 0.5f;
			((UIElement)RefAIV_Default).IsEnabled = true;
			((UIElement)RefAIV_Default).Opacity = 1f;
			((UIElement)RefLord_Default).IsEnabled = true;
			((UIElement)RefLord_Default).Opacity = 1f;
		}
		else
		{
			((UIElement)RefAIV_Community).IsEnabled = true;
			((UIElement)RefAIV_Historical).IsEnabled = true;
			((UIElement)RefAIV_Community).Opacity = 1f;
			((UIElement)RefAIV_Historical).Opacity = 1f;
			((UIElement)RefAIV_Default).IsEnabled = true;
			((UIElement)RefAIV_Default).Opacity = 1f;
			((UIElement)RefLord_Default).IsEnabled = true;
			((UIElement)RefLord_Default).Opacity = 1f;
		}
		if (lordList.Count == 0)
		{
			((UIElement)RefLord_User).IsEnabled = false;
			((UIElement)RefLord_User).Opacity = 0.5f;
		}
		else
		{
			((UIElement)RefLord_User).IsEnabled = true;
			((UIElement)RefLord_User).Opacity = 1f;
		}
		bool flag = false;
		if (AIVInfo.builtIn)
		{
			((ToggleButton)RefAIV_Default).IsChecked = true;
			((UIElement)RefFileLists).IsEnabled = false;
			((UIElement)RefSelectionDisabledOverlay).Visibility = (Visibility)2;
			((UIElement)RefSelectionDisabledOverlayMP).Visibility = (Visibility)1;
			if (doPopulate)
			{
				AIVInfo.aivs.Clear();
				for (int i = 0; i < 8; i++)
				{
					AIVInfo.aivs.Add(aivList[i]);
				}
			}
		}
		else if (AIVInfo.community)
		{
			((ToggleButton)RefAIV_Community).IsChecked = true;
			((UIElement)RefFileLists).IsEnabled = false;
			((UIElement)RefSelectionDisabledOverlay).Visibility = (Visibility)2;
			((UIElement)RefSelectionDisabledOverlayMP).Visibility = (Visibility)1;
			if (doPopulate)
			{
				AIVInfo.aivs.Clear();
				for (int j = 0; j < 8; j++)
				{
					AIVInfo.aivs.Add(aivList[j + 8]);
				}
			}
		}
		else if (AIVInfo.historical)
		{
			((ToggleButton)RefAIV_Historical).IsChecked = true;
			((UIElement)RefFileLists).IsEnabled = false;
			((UIElement)RefSelectionDisabledOverlay).Visibility = (Visibility)2;
			((UIElement)RefSelectionDisabledOverlayMP).Visibility = (Visibility)1;
			if (doPopulate)
			{
				AIVInfo.aivs.Clear();
				for (int k = 0; k < 1; k++)
				{
					AIVInfo.aivs.Add(aivList[k + 16]);
				}
			}
		}
		else
		{
			((ToggleButton)RefAIV_User).IsChecked = true;
			((UIElement)RefFileLists).IsEnabled = true;
			((UIElement)RefSelectionDisabledOverlay).Visibility = (Visibility)1;
			flag = true;
			if (MPMode)
			{
				((UIElement)RefSelectionDisabledOverlayMP).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefSelectionDisabledOverlayMP).Visibility = (Visibility)1;
			}
		}
		if (AIVInfo.builtInLord)
		{
			((ToggleButton)RefLord_Default).IsChecked = true;
			((UIElement)RefLordList).IsEnabled = false;
			((UIElement)RefLordSelectionDisabledOverlay).Visibility = (Visibility)2;
		}
		else
		{
			((ToggleButton)RefLord_User).IsChecked = true;
			((UIElement)RefLordList).IsEnabled = true;
			((UIElement)RefLordSelectionDisabledOverlay).Visibility = (Visibility)1;
		}
		switch (AIVInfo.rotation)
		{
		case 0:
			((ToggleButton)RefNo_Rotation).IsChecked = true;
			break;
		case 3:
			((ToggleButton)RefNorth_Rotation).IsChecked = true;
			break;
		case 2:
			((ToggleButton)RefEast_Rotation).IsChecked = true;
			break;
		case 1:
			((ToggleButton)RefSouth_Rotation).IsChecked = true;
			break;
		case 4:
			((ToggleButton)RefWest_Rotation).IsChecked = true;
			break;
		}
		foreach (CustomisationFileManager.CustomAIV aiv in aivList)
		{
			FileRow fileRow = new FileRow();
			fileRow.Text1 = aiv.AIVName;
			if (aiv.builtIn)
			{
				fileRow.TypeImage = MainViewModel.Instance.GameSprites[88];
			}
			else if (aiv.workshop)
			{
				fileRow.TypeImage = MainViewModel.Instance.GameSprites[89];
			}
			else
			{
				fileRow.TypeImage = MainViewModel.Instance.GameSprites[90];
			}
			fileRows.Add(fileRow);
		}
		foreach (CustomisationFileManager.CustomLordConfig lord in lordList)
		{
			FileRow fileRow2 = new FileRow();
			fileRow2.Text1 = lord.name;
			if (lord.workshop)
			{
				fileRow2.TypeImage = MainViewModel.Instance.GameSprites[89];
			}
			else
			{
				fileRow2.TypeImage = MainViewModel.Instance.GameSprites[90];
			}
			lordRows.Add(fileRow2);
		}
		((ItemsControl)RefFileLists).ItemsSource = fileRows;
		((ItemsControl)RefLordList).ItemsSource = lordRows;
		for (int l = 0; l < 8; l++)
		{
			SetSelectedRow(l, "", null);
		}
		int num = 0;
		foreach (CustomisationFileManager.CustomAIV aiv2 in AIVInfo.aivs)
		{
			ImageSource val = null;
			SetSelectedRow(image: (!aiv2.builtIn) ? ((!aiv2.workshop) ? MainViewModel.Instance.GameSprites[90] : MainViewModel.Instance.GameSprites[89]) : MainViewModel.Instance.GameSprites[88], row: num, text: aiv2.AIVName, hideKick: !flag);
			num++;
		}
	}

	public void AddSelected()
	{
		if (((Selector)RefFileLists).SelectedItem == null)
		{
			return;
		}
		if (!MPMode)
		{
			if (((ListBox)RefFileLists).SelectedItems != null && ((ListBox)RefFileLists).SelectedItems.Count > 1)
			{
				foreach (object selectedItem in ((ListBox)RefFileLists).SelectedItems)
				{
					if (AIVInfo.aivs.Count >= 8)
					{
						break;
					}
					int index = ((ItemsControl)RefFileLists).Items.IndexOf(selectedItem);
					CustomisationFileManager.CustomAIV customAIV = aivList[index];
					foreach (CustomisationFileManager.CustomAIV aiv in AIVInfo.aivs)
					{
						if (aiv.checksum == customAIV.checksum)
						{
							return;
						}
					}
					AIVInfo.aivs.Add(customAIV);
				}
				populateList();
			}
			else
			{
				if (AIVInfo.aivs.Count >= 8 || aivList == null)
				{
					return;
				}
				CustomisationFileManager.CustomAIV customAIV2 = aivList[((Selector)RefFileLists).SelectedIndex];
				foreach (CustomisationFileManager.CustomAIV aiv2 in AIVInfo.aivs)
				{
					if (aiv2.checksum == customAIV2.checksum)
					{
						return;
					}
				}
				AIVInfo.aivs.Add(customAIV2);
				populateList();
			}
		}
		else
		{
			while (AIVInfo.aivs.Count > 0)
			{
				AIVInfo.aivs.RemoveAt(0);
			}
			if (aivList != null)
			{
				CustomisationFileManager.CustomAIV item = aivList[((Selector)RefFileLists).SelectedIndex];
				AIVInfo.aivs.Add(item);
				populateList();
			}
		}
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "Back":
			KeyManager.instance.ignoreEscape();
			MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("CancelAISettings");
			break;
		case "Default":
			AIVInfo.builtIn = true;
			AIVInfo.community = false;
			AIVInfo.historical = false;
			populateList();
			break;
		case "Community":
			AIVInfo.builtIn = false;
			AIVInfo.community = true;
			AIVInfo.historical = false;
			populateList();
			break;
		case "Historical":
			AIVInfo.builtIn = false;
			AIVInfo.community = false;
			AIVInfo.historical = true;
			populateList();
			break;
		case "NoRot":
			AIVInfo.rotation = 0;
			break;
		case "North":
			AIVInfo.rotation = 3;
			break;
		case "East":
			AIVInfo.rotation = 2;
			break;
		case "South":
			AIVInfo.rotation = 1;
			break;
		case "West":
			AIVInfo.rotation = 4;
			break;
		case "User":
			AIVInfo.builtIn = false;
			AIVInfo.community = false;
			AIVInfo.historical = false;
			if (MPMode)
			{
				while (AIVInfo.aivs.Count > 1)
				{
					AIVInfo.aivs.RemoveAt(1);
				}
			}
			populateList();
			break;
		case "Kick_1":
			if (AIVInfo.aivs.Count > 0)
			{
				AIVInfo.aivs.RemoveAt(0);
				populateList(null, doPopulate: false);
			}
			break;
		case "Kick_2":
			if (AIVInfo.aivs.Count > 1)
			{
				AIVInfo.aivs.RemoveAt(1);
				populateList(null, doPopulate: false);
			}
			break;
		case "Kick_3":
			if (AIVInfo.aivs.Count > 2)
			{
				AIVInfo.aivs.RemoveAt(2);
				populateList(null, doPopulate: false);
			}
			break;
		case "Kick_4":
			if (AIVInfo.aivs.Count > 3)
			{
				AIVInfo.aivs.RemoveAt(3);
				populateList(null, doPopulate: false);
			}
			break;
		case "Kick_5":
			if (AIVInfo.aivs.Count > 4)
			{
				AIVInfo.aivs.RemoveAt(4);
				populateList(null, doPopulate: false);
			}
			break;
		case "Kick_6":
			if (AIVInfo.aivs.Count > 5)
			{
				AIVInfo.aivs.RemoveAt(5);
				populateList(null, doPopulate: false);
			}
			break;
		case "Kick_7":
			if (AIVInfo.aivs.Count > 6)
			{
				AIVInfo.aivs.RemoveAt(6);
				populateList(null, doPopulate: false);
			}
			break;
		case "Kick_8":
			if (AIVInfo.aivs.Count > 7)
			{
				AIVInfo.aivs.RemoveAt(7);
				populateList(null, doPopulate: false);
			}
			break;
		case "Add_Selected":
			AddSelected();
			break;
		case "Replace_Selected":
			AIVInfo.aivs.Clear();
			AddSelected();
			break;
		case "Clear_Selected":
			if (AIVInfo.aivs.Count > 0)
			{
				AIVInfo.aivs.Clear();
				populateList(null, doPopulate: false);
			}
			break;
		case "AIV_Mode":
			if (!MainViewModel.Instance.Show_MPAI_AIV_Mode)
			{
				MainViewModel.Instance.Show_MPAI_AIV_Mode = true;
				MainViewModel.Instance.Show_MPAI_Lord_Mode = false;
				((UIElement)RefAIV_Mode).Opacity = 1f;
				((UIElement)RefLord_Mode).Opacity = 0.6f;
			}
			break;
		case "Lord_Mode":
			if (!MainViewModel.Instance.Show_MPAI_Lord_Mode)
			{
				MainViewModel.Instance.Show_MPAI_AIV_Mode = false;
				MainViewModel.Instance.Show_MPAI_Lord_Mode = true;
				((UIElement)RefAIV_Mode).Opacity = 0.6f;
				((UIElement)RefLord_Mode).Opacity = 1f;
			}
			break;
		case "LordDefault":
			AIVInfo.builtInLord = true;
			AIVInfo.lordConfig = null;
			MainViewModel.Instance.CustomLordName = "";
			populateList();
			break;
		case "LordUser":
			if (AIVInfo.builtInLord)
			{
				if (lordList.Count > 0 && ((Selector)RefLordList).SelectedItem == null)
				{
					((Selector)RefLordList).SelectedIndex = 0;
				}
				if (AIVInfo.lordConfig != null)
				{
					MainViewModel.Instance.CustomLordName = AIVInfo.lordConfig.name;
				}
			}
			AIVInfo.builtInLord = false;
			populateList();
			break;
		case "MouseEnter_AIV_Mode":
			((UIElement)RefAIV_Mode).Opacity = 1f;
			break;
		case "MouseLeave_AIV_Mode":
			if (!MainViewModel.Instance.Show_MPAI_AIV_Mode)
			{
				((UIElement)RefAIV_Mode).Opacity = 0.6f;
			}
			break;
		case "MouseEnter_Lord_Mode":
			((UIElement)RefLord_Mode).Opacity = 1f;
			break;
		case "MouseLeave_Lord_Mode":
			if (!MainViewModel.Instance.Show_MPAI_Lord_Mode)
			{
				((UIElement)RefLord_Mode).Opacity = 0.6f;
			}
			break;
		case "AIV_Default_Enter":
			MainViewModel.Instance.AI_Settings_Help = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 74);
			MainViewModel.Instance.Show_AI_Settings_Help = true;
			break;
		case "Community_Enter":
			MainViewModel.Instance.AI_Settings_Help = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 75);
			MainViewModel.Instance.Show_AI_Settings_Help = true;
			break;
		case "Historical_Enter":
			MainViewModel.Instance.AI_Settings_Help = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 76);
			MainViewModel.Instance.Show_AI_Settings_Help = true;
			break;
		case "User_Enter":
			MainViewModel.Instance.AI_Settings_Help = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 77);
			MainViewModel.Instance.Show_AI_Settings_Help = true;
			break;
		case "MouseEnter_Add":
			MainViewModel.Instance.AI_Settings_Help = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 78);
			MainViewModel.Instance.Show_AI_Settings_Help = true;
			break;
		case "MouseEnter_Replace":
			MainViewModel.Instance.AI_Settings_Help = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 79);
			MainViewModel.Instance.Show_AI_Settings_Help = true;
			break;
		case "AIV_Default_Leave":
		case "Community_Leave":
		case "Historical_Leave":
		case "User_Leave":
		case "MouseLeave_Replace":
		case "MouseLeave_Add":
			MainViewModel.Instance.FRONTMultiplayer.hideToolTipTime = DateTime.UtcNow.AddSeconds(0.5);
			break;
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_Multiplayer_AISettings.xaml");
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

	public void SetSelectedRow(int row, string text, ImageSource image, bool hideKick = false)
	{
		Button val = null;
		switch (row)
		{
		case 0:
			MainViewModel.Instance.SelectedAIV_Image_1 = image;
			MainViewModel.Instance.SelectedAIV_Text_1 = text;
			val = RefPlayer1_Kick;
			break;
		case 1:
			MainViewModel.Instance.SelectedAIV_Image_2 = image;
			MainViewModel.Instance.SelectedAIV_Text_2 = text;
			val = RefPlayer2_Kick;
			break;
		case 2:
			MainViewModel.Instance.SelectedAIV_Image_3 = image;
			MainViewModel.Instance.SelectedAIV_Text_3 = text;
			val = RefPlayer3_Kick;
			break;
		case 3:
			MainViewModel.Instance.SelectedAIV_Image_4 = image;
			MainViewModel.Instance.SelectedAIV_Text_4 = text;
			val = RefPlayer4_Kick;
			break;
		case 4:
			MainViewModel.Instance.SelectedAIV_Image_5 = image;
			MainViewModel.Instance.SelectedAIV_Text_5 = text;
			val = RefPlayer5_Kick;
			break;
		case 5:
			MainViewModel.Instance.SelectedAIV_Image_6 = image;
			MainViewModel.Instance.SelectedAIV_Text_6 = text;
			val = RefPlayer6_Kick;
			break;
		case 6:
			MainViewModel.Instance.SelectedAIV_Image_7 = image;
			MainViewModel.Instance.SelectedAIV_Text_7 = text;
			val = RefPlayer7_Kick;
			break;
		case 7:
			MainViewModel.Instance.SelectedAIV_Image_8 = image;
			MainViewModel.Instance.SelectedAIV_Text_8 = text;
			val = RefPlayer8_Kick;
			break;
		}
		if ((BaseComponent)(object)val != (BaseComponent)null)
		{
			if ((BaseComponent)(object)image == (BaseComponent)null || hideKick)
			{
				PropEx.SetButtonVisibility((UIElement)(object)val, (Visibility)1);
			}
			else
			{
				PropEx.SetButtonVisibility((UIElement)(object)val, (Visibility)2);
			}
		}
	}
}
