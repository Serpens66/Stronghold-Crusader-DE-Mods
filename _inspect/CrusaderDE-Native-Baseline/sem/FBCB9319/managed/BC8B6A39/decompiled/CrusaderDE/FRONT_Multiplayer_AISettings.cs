using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Noesis;

namespace CrusaderDE;

public class FRONT_Multiplayer_AISettings : UserControl
{
	private Grid RefAIV_Mode;

	private Grid RefLord_Mode;

	private ListView RefFileLists;

	private RadioButton RefAIV_Default;

	private RadioButton RefAIV_Community;

	private RadioButton RefAIV_Historical;

	private RadioButton RefAIV_User;

	private RadioButton RefNo_Rotation;

	private RadioButton RefNorth_Rotation;

	private RadioButton RefEast_Rotation;

	private RadioButton RefSouth_Rotation;

	private RadioButton RefWest_Rotation;

	private Grid RefSelectionDisabledOverlay;

	private Grid RefSelectionDisabledOverlayMP;

	private Button RefPlayer1_Kick;

	private Button RefPlayer2_Kick;

	private Button RefPlayer3_Kick;

	private Button RefPlayer4_Kick;

	private Button RefPlayer5_Kick;

	private Button RefPlayer6_Kick;

	private Button RefPlayer7_Kick;

	private Button RefPlayer8_Kick;

	private TextBlock RefCastlesHeading;

	private ListView RefLordList;

	private RadioButton RefLord_Default;

	private RadioButton RefLord_User;

	private Grid RefLordSelectionDisabledOverlay;

	public static FRONT_Multiplayer_AISettings instance1;

	public static FRONT_Multiplayer_AISettings instance2;

	private FRONT_Multiplayer.MPAIVInfo AIVInfo;

	private ObservableCollection<FileRow> fileRows = new ObservableCollection<FileRow>();

	private ObservableCollection<FileRow> lordRows = new ObservableCollection<FileRow>();

	private List<CustomisationFileManager.CustomAIV> aivList;

	private List<CustomisationFileManager.CustomLordConfig> lordList;

	private bool MPMode;

	public static FRONT_Multiplayer_AISettings Instance
	{
		get
		{
			if (instance1.IsVisible)
			{
				return instance1;
			}
			return instance2;
		}
	}

	public FRONT_Multiplayer_AISettings()
	{
		if (instance1 == null)
		{
			instance1 = this;
		}
		else if (instance2 == null)
		{
			instance2 = this;
		}
		InitializeComponent();
		RefAIV_Mode = (Grid)FindName("AIV_Mode");
		RefLord_Mode = (Grid)FindName("Lord_Mode");
		RefAIV_Default = (RadioButton)FindName("AIV_Default");
		RefAIV_Community = (RadioButton)FindName("AIV_Community");
		RefAIV_Historical = (RadioButton)FindName("AIV_Historical");
		RefAIV_User = (RadioButton)FindName("AIV_User");
		RefNo_Rotation = (RadioButton)FindName("No_Rotation");
		RefNorth_Rotation = (RadioButton)FindName("North_Rotation");
		RefEast_Rotation = (RadioButton)FindName("East_Rotation");
		RefSouth_Rotation = (RadioButton)FindName("South_Rotation");
		RefWest_Rotation = (RadioButton)FindName("West_Rotation");
		RefPlayer1_Kick = (Button)FindName("Player1_Kick");
		RefPlayer2_Kick = (Button)FindName("Player2_Kick");
		RefPlayer3_Kick = (Button)FindName("Player3_Kick");
		RefPlayer4_Kick = (Button)FindName("Player4_Kick");
		RefPlayer5_Kick = (Button)FindName("Player5_Kick");
		RefPlayer6_Kick = (Button)FindName("Player6_Kick");
		RefPlayer7_Kick = (Button)FindName("Player7_Kick");
		RefPlayer8_Kick = (Button)FindName("Player8_Kick");
		RefCastlesHeading = (TextBlock)FindName("CastlesHeading");
		RefSelectionDisabledOverlay = (Grid)FindName("SelectionDisabledOverlay");
		RefSelectionDisabledOverlayMP = (Grid)FindName("SelectionDisabledOverlayMP");
		RefFileLists = (ListView)FindName("AIVList");
		((GridViewColumnHeader)((GridView)RefFileLists.View).Columns[1].Header).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		RefFileLists.MouseDoubleClick += delegate
		{
			AddSelected();
		};
		RefLordList = (ListView)FindName("LordList");
		RefLord_Default = (RadioButton)FindName("Lord_Default");
		RefLord_User = (RadioButton)FindName("Lord_User");
		RefLordSelectionDisabledOverlay = (Grid)FindName("LordSelectionDisabledOverlay");
		RefLordList.SelectionChanged += delegate
		{
			if (RefLordList.SelectedItem != null)
			{
				AIVInfo.lordConfig = lordList[RefLordList.SelectedIndex];
				MainViewModel.Instance.CustomLordName = AIVInfo.lordConfig.name;
			}
		};
		if (FatControler.arabic)
		{
			RefCastlesHeading.FontSize = 27f;
			RefCastlesHeading.Margin = new Thickness(0f, 25f, 0f, 0f);
		}
	}

	public static void Show(int this_player, FRONT_Multiplayer.MPAIVInfo aivInfo, bool MPMode)
	{
		MainViewModel.Instance.Show_MPAISettings = true;
		Instance.Init(aivInfo, MPMode);
	}

	private void Init(FRONT_Multiplayer.MPAIVInfo aivInfo, bool _MPMode)
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
		RefAIV_Mode.Opacity = 1f;
		RefLord_Mode.Opacity = 0.6f;
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
				RefLordList.SelectedIndex = i;
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
			RefAIV_Community.IsEnabled = false;
			RefAIV_Historical.IsEnabled = false;
			RefAIV_Community.Opacity = 0.5f;
			RefAIV_Historical.Opacity = 0.5f;
			RefAIV_Default.IsEnabled = false;
			RefAIV_Default.Opacity = 0.5f;
			RefLord_Default.IsEnabled = false;
			RefLord_Default.Opacity = 0.5f;
		}
		else if (AIVInfo.lordType >= 16)
		{
			RefAIV_Community.IsEnabled = false;
			RefAIV_Historical.IsEnabled = false;
			RefAIV_Community.Opacity = 0.5f;
			RefAIV_Historical.Opacity = 0.5f;
			RefAIV_Default.IsEnabled = true;
			RefAIV_Default.Opacity = 1f;
			RefLord_Default.IsEnabled = true;
			RefLord_Default.Opacity = 1f;
		}
		else
		{
			RefAIV_Community.IsEnabled = true;
			RefAIV_Historical.IsEnabled = true;
			RefAIV_Community.Opacity = 1f;
			RefAIV_Historical.Opacity = 1f;
			RefAIV_Default.IsEnabled = true;
			RefAIV_Default.Opacity = 1f;
			RefLord_Default.IsEnabled = true;
			RefLord_Default.Opacity = 1f;
		}
		if (lordList.Count == 0)
		{
			RefLord_User.IsEnabled = false;
			RefLord_User.Opacity = 0.5f;
		}
		else
		{
			RefLord_User.IsEnabled = true;
			RefLord_User.Opacity = 1f;
		}
		bool flag = false;
		if (AIVInfo.builtIn)
		{
			RefAIV_Default.IsChecked = true;
			RefFileLists.IsEnabled = false;
			RefSelectionDisabledOverlay.Visibility = Visibility.Visible;
			RefSelectionDisabledOverlayMP.Visibility = Visibility.Hidden;
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
			RefAIV_Community.IsChecked = true;
			RefFileLists.IsEnabled = false;
			RefSelectionDisabledOverlay.Visibility = Visibility.Visible;
			RefSelectionDisabledOverlayMP.Visibility = Visibility.Hidden;
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
			RefAIV_Historical.IsChecked = true;
			RefFileLists.IsEnabled = false;
			RefSelectionDisabledOverlay.Visibility = Visibility.Visible;
			RefSelectionDisabledOverlayMP.Visibility = Visibility.Hidden;
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
			RefAIV_User.IsChecked = true;
			RefFileLists.IsEnabled = true;
			RefSelectionDisabledOverlay.Visibility = Visibility.Hidden;
			flag = true;
			if (MPMode)
			{
				RefSelectionDisabledOverlayMP.Visibility = Visibility.Visible;
			}
			else
			{
				RefSelectionDisabledOverlayMP.Visibility = Visibility.Hidden;
			}
		}
		if (AIVInfo.builtInLord)
		{
			RefLord_Default.IsChecked = true;
			RefLordList.IsEnabled = false;
			RefLordSelectionDisabledOverlay.Visibility = Visibility.Visible;
		}
		else
		{
			RefLord_User.IsChecked = true;
			RefLordList.IsEnabled = true;
			RefLordSelectionDisabledOverlay.Visibility = Visibility.Hidden;
		}
		switch (AIVInfo.rotation)
		{
		case 0:
			RefNo_Rotation.IsChecked = true;
			break;
		case 3:
			RefNorth_Rotation.IsChecked = true;
			break;
		case 2:
			RefEast_Rotation.IsChecked = true;
			break;
		case 1:
			RefSouth_Rotation.IsChecked = true;
			break;
		case 4:
			RefWest_Rotation.IsChecked = true;
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
		RefFileLists.ItemsSource = fileRows;
		RefLordList.ItemsSource = lordRows;
		for (int l = 0; l < 8; l++)
		{
			SetSelectedRow(l, "", null);
		}
		int num = 0;
		foreach (CustomisationFileManager.CustomAIV aiv2 in AIVInfo.aivs)
		{
			ImageSource imageSource = null;
			SetSelectedRow(image: (!aiv2.builtIn) ? ((!aiv2.workshop) ? MainViewModel.Instance.GameSprites[90] : MainViewModel.Instance.GameSprites[89]) : MainViewModel.Instance.GameSprites[88], row: num, text: aiv2.AIVName, hideKick: !flag);
			num++;
		}
	}

	private void AddSelected()
	{
		if (RefFileLists.SelectedItem == null)
		{
			return;
		}
		if (!MPMode)
		{
			if (RefFileLists.SelectedItems != null && RefFileLists.SelectedItems.Count > 1)
			{
				foreach (object selectedItem in RefFileLists.SelectedItems)
				{
					if (AIVInfo.aivs.Count >= 8)
					{
						break;
					}
					int index = RefFileLists.Items.IndexOf(selectedItem);
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
				CustomisationFileManager.CustomAIV customAIV2 = aivList[RefFileLists.SelectedIndex];
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
				CustomisationFileManager.CustomAIV item = aivList[RefFileLists.SelectedIndex];
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
				RefAIV_Mode.Opacity = 1f;
				RefLord_Mode.Opacity = 0.6f;
			}
			break;
		case "Lord_Mode":
			if (!MainViewModel.Instance.Show_MPAI_Lord_Mode)
			{
				MainViewModel.Instance.Show_MPAI_AIV_Mode = false;
				MainViewModel.Instance.Show_MPAI_Lord_Mode = true;
				RefAIV_Mode.Opacity = 0.6f;
				RefLord_Mode.Opacity = 1f;
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
				if (lordList.Count > 0 && RefLordList.SelectedItem == null)
				{
					RefLordList.SelectedIndex = 0;
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
			RefAIV_Mode.Opacity = 1f;
			break;
		case "MouseLeave_AIV_Mode":
			if (!MainViewModel.Instance.Show_MPAI_AIV_Mode)
			{
				RefAIV_Mode.Opacity = 0.6f;
			}
			break;
		case "MouseEnter_Lord_Mode":
			RefLord_Mode.Opacity = 1f;
			break;
		case "MouseLeave_Lord_Mode":
			if (!MainViewModel.Instance.Show_MPAI_Lord_Mode)
			{
				RefLord_Mode.Opacity = 0.6f;
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

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_Multiplayer_AISettings.xaml");
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

	private void SetSelectedRow(int row, string text, ImageSource image, bool hideKick = false)
	{
		Button button = null;
		switch (row)
		{
		case 0:
			MainViewModel.Instance.SelectedAIV_Image_1 = image;
			MainViewModel.Instance.SelectedAIV_Text_1 = text;
			button = RefPlayer1_Kick;
			break;
		case 1:
			MainViewModel.Instance.SelectedAIV_Image_2 = image;
			MainViewModel.Instance.SelectedAIV_Text_2 = text;
			button = RefPlayer2_Kick;
			break;
		case 2:
			MainViewModel.Instance.SelectedAIV_Image_3 = image;
			MainViewModel.Instance.SelectedAIV_Text_3 = text;
			button = RefPlayer3_Kick;
			break;
		case 3:
			MainViewModel.Instance.SelectedAIV_Image_4 = image;
			MainViewModel.Instance.SelectedAIV_Text_4 = text;
			button = RefPlayer4_Kick;
			break;
		case 4:
			MainViewModel.Instance.SelectedAIV_Image_5 = image;
			MainViewModel.Instance.SelectedAIV_Text_5 = text;
			button = RefPlayer5_Kick;
			break;
		case 5:
			MainViewModel.Instance.SelectedAIV_Image_6 = image;
			MainViewModel.Instance.SelectedAIV_Text_6 = text;
			button = RefPlayer6_Kick;
			break;
		case 6:
			MainViewModel.Instance.SelectedAIV_Image_7 = image;
			MainViewModel.Instance.SelectedAIV_Text_7 = text;
			button = RefPlayer7_Kick;
			break;
		case 7:
			MainViewModel.Instance.SelectedAIV_Image_8 = image;
			MainViewModel.Instance.SelectedAIV_Text_8 = text;
			button = RefPlayer8_Kick;
			break;
		}
		if (button != null)
		{
			if (image == null || hideKick)
			{
				PropEx.SetButtonVisibility(button, Visibility.Hidden);
			}
			else
			{
				PropEx.SetButtonVisibility(button, Visibility.Visible);
			}
		}
	}
}
