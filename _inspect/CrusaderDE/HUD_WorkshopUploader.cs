using System.Collections.Generic;
using System.IO;
using System.Linq;
using Noesis;
using Steamworks;
using UnityEngine;

namespace CrusaderDE;

public class HUD_WorkshopUploader : UserControl
{
	public TextBox RefWorkshopMapTitle;

	public TextBox RefWorkshopMapDescription;

	public Button RefWorkshopUpload;

	public Grid RefUploadPanel;

	public RadioButton RefEasyTag;

	public RadioButton RefNormalTag;

	public RadioButton RefHardTag;

	public RadioButton RefVeryHardTag;

	public CheckBox RefBalancedCheck;

	public TextBlock RefWorkshopTOSText;

	public static bool CanClose = true;

	public string WORKSHOP_UploadContentFolder = "";

	public string WORKSHOP_UploadImage = "";

	public string[] workshopTags_Sizes = new string[8] { "Small (160x160)", "Medium (200x200)", "Large (300x300)", "Very Large (400x400)", "Extra Large (500x500)", "Huge (600x600)", "Immense (700x700)", "Colossal (800x800)" };

	public string[] workshopTags_Types = new string[3] { "Custom Scenario", "Skirmish/Multiplayer", "Free Build" };

	public string[] workshopTags_Difficulty = new string[5] { "Easy", "Normal", "Hard", "Very Hard", "n/a" };

	public HUD_WorkshopUploader()
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Expected O, but got Unknown
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Expected O, but got Unknown
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Expected O, but got Unknown
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Expected O, but got Unknown
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Expected O, but got Unknown
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Expected O, but got Unknown
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Expected O, but got Unknown
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Expected O, but got Unknown
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Expected O, but got Unknown
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Expected O, but got Unknown
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Expected O, but got Unknown
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Expected O, but got Unknown
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Expected O, but got Unknown
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Expected O, but got Unknown
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDWorkshopUploader = this;
		RefWorkshopMapTitle = (TextBox)((FrameworkElement)this).FindName("WorkshopMapTitle");
		((UIElement)RefWorkshopMapTitle).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefWorkshopMapTitle).TextChanged += new RoutedEventHandler(TextChangedHandler);
		((FrameworkElement)RefWorkshopMapTitle).Loaded += new RoutedEventHandler(TextBoxLoaded);
		((UIElement)RefWorkshopMapTitle).PreviewTextInput += new TextCompositionEventHandler(FileNameValidationTextBox);
		RefWorkshopMapDescription = (TextBox)((FrameworkElement)this).FindName("WorkshopMapDescription");
		((UIElement)RefWorkshopMapDescription).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefWorkshopMapDescription).TextChanged += new RoutedEventHandler(TextChangedHandler);
		RefWorkshopTOSText = (TextBlock)((FrameworkElement)this).FindName("WorkshopTOSText");
		RefWorkshopUpload = (Button)((FrameworkElement)this).FindName("WorkshopUpload");
		RefEasyTag = (RadioButton)((FrameworkElement)this).FindName("EasyTag");
		RefNormalTag = (RadioButton)((FrameworkElement)this).FindName("NormalTag");
		RefHardTag = (RadioButton)((FrameworkElement)this).FindName("HardTag");
		RefVeryHardTag = (RadioButton)((FrameworkElement)this).FindName("VeryHardTag");
		RefBalancedCheck = (CheckBox)((FrameworkElement)this).FindName("BalancedCheck");
		RefUploadPanel = (Grid)((FrameworkElement)this).FindName("UploadPanel");
		if (FatControler.german)
		{
			RefWorkshopTOSText.FontSize = 20f;
		}
	}

	public static void Open()
	{
		MainViewModel.Instance.Show_HUD_WorkshopUploader = true;
		MainViewModel.Instance.HUDWorkshopUploader.doOpen();
	}

	public void doOpen()
	{
		((UIElement)RefWorkshopUpload).IsEnabled = false;
		CanClose = true;
		((UIElement)RefUploadPanel).Visibility = (Visibility)1;
		string mapName = "";
		string description = "";
		int difficulty = 0;
		bool balanced = false;
		if (Platform_Workshop.Instance.getMetaData(ref mapName, ref difficulty, ref description, ref balanced))
		{
			RefWorkshopMapTitle.Text = mapName;
			RefWorkshopMapDescription.Text = description;
			((ToggleButton)RefBalancedCheck).IsChecked = balanced;
			switch (difficulty)
			{
			case 0:
				((ToggleButton)RefEasyTag).IsChecked = true;
				break;
			case 1:
				((ToggleButton)RefNormalTag).IsChecked = true;
				break;
			case 2:
				((ToggleButton)RefHardTag).IsChecked = true;
				break;
			case 3:
				((ToggleButton)RefVeryHardTag).IsChecked = true;
				break;
			}
			MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 127);
		}
		else
		{
			RefWorkshopMapTitle.Text = "";
			RefWorkshopMapDescription.Text = "";
			((ToggleButton)RefNormalTag).IsChecked = true;
			((ToggleButton)RefBalancedCheck).IsChecked = GameData.Instance.lastGameState.balanced == 0;
			MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 114);
		}
		if (GameData.Instance.multiplayerMap)
		{
			MainViewModel.Instance.Show_WorkshopDifficulty = false;
			return;
		}
		switch (GameData.Instance.mapType)
		{
		case Enums.GameModes.BUILD:
			MainViewModel.Instance.Show_WorkshopDifficulty = false;
			break;
		case Enums.GameModes.INVASION:
			MainViewModel.Instance.Show_WorkshopDifficulty = true;
			break;
		}
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "Exit":
			MainViewModel.Instance.Show_HUD_WorkshopUploader = false;
			MainViewModel.Instance.Show_HUD_IngameMenu = true;
			break;
		case "ToS":
			SteamFriends.ActivateGameOverlayToWebPage("http://steamcommunity.com/sharedfiles/workshoplegalagreement", (EActivateGameOverlayToWebPageMode)0);
			break;
		case "Upload":
		{
			List<string> list = new List<string>();
			switch (GameMap.tilemapSize)
			{
			case 160:
				list.Add(workshopTags_Sizes[0]);
				break;
			case 200:
				list.Add(workshopTags_Sizes[1]);
				break;
			case 300:
				list.Add(workshopTags_Sizes[2]);
				break;
			case 400:
				list.Add(workshopTags_Sizes[3]);
				break;
			case 500:
				list.Add(workshopTags_Sizes[4]);
				break;
			case 600:
				list.Add(workshopTags_Sizes[5]);
				break;
			case 700:
				list.Add(workshopTags_Sizes[6]);
				break;
			case 800:
				list.Add(workshopTags_Sizes[7]);
				break;
			}
			bool flag = false;
			if (GameData.Instance.multiplayerMap)
			{
				list.Add(workshopTags_Types[1]);
			}
			else
			{
				switch (GameData.Instance.mapType)
				{
				case Enums.GameModes.BUILD:
					list.Add(workshopTags_Types[2]);
					break;
				case Enums.GameModes.INVASION:
					list.Add(workshopTags_Types[0]);
					flag = true;
					break;
				}
			}
			int difficulty = 4;
			if (flag)
			{
				if (((ToggleButton)RefEasyTag).IsChecked == true)
				{
					difficulty = 0;
				}
				else if (((ToggleButton)RefNormalTag).IsChecked == true)
				{
					difficulty = 1;
				}
				else if (((ToggleButton)RefHardTag).IsChecked == true)
				{
					difficulty = 2;
				}
				else if (((ToggleButton)RefVeryHardTag).IsChecked == true)
				{
					difficulty = 3;
				}
				list.Add(workshopTags_Difficulty[difficulty]);
			}
			else
			{
				list.Add("n/a");
			}
			if (((ToggleButton)RefBalancedCheck).IsChecked.Value)
			{
				list.Add("Balanced");
			}
			CanClose = false;
			((UIElement)RefUploadPanel).Visibility = (Visibility)2;
			string mapName = RefWorkshopMapTitle.Text;
			string description = RefWorkshopMapDescription.Text;
			WORKSHOP_UploadContentFolder = ConfigSettings.GetWorkshopUploadContentPath();
			string path = Path.Combine(WORKSHOP_UploadContentFolder, mapName + ".map");
			EditorDirector.instance.SaveSaveGameOrMap(path, mapName, lockMap: true, tempLockOnly: true, mapSave: true);
			byte[] radarFromFile = MapFileManager.Instance.GetRadarFromFile(path);
			byte[] bytes = ImageConversion.EncodeToPNG(MapFileManager.Instance.GetRadarPreview(radarFromFile));
			string text = Path.Combine(ConfigSettings.GetWorkshopUploadRootPath(), "Upload.png");
			File.WriteAllBytes(text, bytes);
			WORKSHOP_UploadImage = text;
			Platform_Workshop.Instance.UploadWorkshopMap(WORKSHOP_UploadContentFolder, mapName, description, list.ToArray(), publicMap: true, WORKSHOP_UploadImage, delegate
			{
				HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 124), delegate
				{
					ulong publishID = Platform_Workshop.Instance.GetPublishID();
					string userWorkshopPath = ConfigSettings.GetUserWorkshopPath();
					string path2 = Path.Combine(userWorkshopPath, mapName + ".map");
					EditorDirector.instance.SaveSaveGameOrMap(path2, mapName, lockMap: false, tempLockOnly: false, mapSave: true);
					string text2 = ((!((ToggleButton)RefBalancedCheck).IsChecked.Value) ? (publishID + "\n" + difficulty + "\n") : (publishID + "\n-" + difficulty + "\n"));
					string path3 = Path.Combine(userWorkshopPath, mapName + ".data");
					text2 += description;
					File.WriteAllText(path3, text2);
					CanClose = true;
					((UIElement)RefUploadPanel).Visibility = (Visibility)1;
					MainViewModel.Instance.Show_HUD_WorkshopUploader = false;
					MainViewModel.Instance.Show_HUD_IngameMenu = true;
				});
			}, delegate
			{
				HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125), delegate
				{
					CanClose = true;
					((UIElement)RefUploadPanel).Visibility = (Visibility)1;
				});
			});
			break;
		}
		}
	}

	public void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	public void TextBoxLoaded(object sender, RoutedEventArgs e)
	{
		((UIElement)RefWorkshopMapTitle).Focus();
	}

	public void FileNameValidationTextBox(object sender, TextCompositionEventArgs e)
	{
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

	public void TextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (RefWorkshopMapTitle.Text.Length > 4 && RefWorkshopMapDescription.Text.Length > 20)
		{
			((UIElement)RefWorkshopUpload).IsEnabled = true;
		}
		else
		{
			((UIElement)RefWorkshopUpload).IsEnabled = false;
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_WorkshopUploader.xaml");
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
}
