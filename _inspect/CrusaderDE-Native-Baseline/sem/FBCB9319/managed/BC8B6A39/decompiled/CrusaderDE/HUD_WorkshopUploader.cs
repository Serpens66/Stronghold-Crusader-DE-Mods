using System.Collections.Generic;
using System.IO;
using System.Linq;
using Noesis;
using Steamworks;
using UnityEngine;

namespace CrusaderDE;

public class HUD_WorkshopUploader : UserControl
{
	private TextBox RefWorkshopMapTitle;

	private TextBox RefWorkshopMapDescription;

	private Button RefWorkshopUpload;

	private Noesis.Grid RefUploadPanel;

	private RadioButton RefEasyTag;

	private RadioButton RefNormalTag;

	private RadioButton RefHardTag;

	private RadioButton RefVeryHardTag;

	private CheckBox RefBalancedCheck;

	private TextBlock RefWorkshopTOSText;

	public static bool CanClose = true;

	private string WORKSHOP_UploadContentFolder = "";

	private string WORKSHOP_UploadImage = "";

	private string[] workshopTags_Sizes = new string[8] { "Small (160x160)", "Medium (200x200)", "Large (300x300)", "Very Large (400x400)", "Extra Large (500x500)", "Huge (600x600)", "Immense (700x700)", "Colossal (800x800)" };

	private string[] workshopTags_Types = new string[3] { "Custom Scenario", "Skirmish/Multiplayer", "Free Build" };

	private string[] workshopTags_Difficulty = new string[5] { "Easy", "Normal", "Hard", "Very Hard", "n/a" };

	public HUD_WorkshopUploader()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDWorkshopUploader = this;
		RefWorkshopMapTitle = (TextBox)FindName("WorkshopMapTitle");
		RefWorkshopMapTitle.IsKeyboardFocusedChanged += TextInputFocus;
		RefWorkshopMapTitle.TextChanged += TextChangedHandler;
		RefWorkshopMapTitle.Loaded += TextBoxLoaded;
		RefWorkshopMapTitle.PreviewTextInput += FileNameValidationTextBox;
		RefWorkshopMapDescription = (TextBox)FindName("WorkshopMapDescription");
		RefWorkshopMapDescription.IsKeyboardFocusedChanged += TextInputFocus;
		RefWorkshopMapDescription.TextChanged += TextChangedHandler;
		RefWorkshopTOSText = (TextBlock)FindName("WorkshopTOSText");
		RefWorkshopUpload = (Button)FindName("WorkshopUpload");
		RefEasyTag = (RadioButton)FindName("EasyTag");
		RefNormalTag = (RadioButton)FindName("NormalTag");
		RefHardTag = (RadioButton)FindName("HardTag");
		RefVeryHardTag = (RadioButton)FindName("VeryHardTag");
		RefBalancedCheck = (CheckBox)FindName("BalancedCheck");
		RefUploadPanel = (Noesis.Grid)FindName("UploadPanel");
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

	private void doOpen()
	{
		RefWorkshopUpload.IsEnabled = false;
		CanClose = true;
		RefUploadPanel.Visibility = Visibility.Hidden;
		string mapName = "";
		string description = "";
		int difficulty = 0;
		bool balanced = false;
		if (Platform_Workshop.Instance.getMetaData(ref mapName, ref difficulty, ref description, ref balanced))
		{
			RefWorkshopMapTitle.Text = mapName;
			RefWorkshopMapDescription.Text = description;
			RefBalancedCheck.IsChecked = balanced;
			switch (difficulty)
			{
			case 0:
				RefEasyTag.IsChecked = true;
				break;
			case 1:
				RefNormalTag.IsChecked = true;
				break;
			case 2:
				RefHardTag.IsChecked = true;
				break;
			case 3:
				RefVeryHardTag.IsChecked = true;
				break;
			}
			MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 127);
		}
		else
		{
			RefWorkshopMapTitle.Text = "";
			RefWorkshopMapDescription.Text = "";
			RefNormalTag.IsChecked = true;
			RefBalancedCheck.IsChecked = GameData.Instance.lastGameState.balanced == 0;
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
			SteamFriends.ActivateGameOverlayToWebPage("http://steamcommunity.com/sharedfiles/workshoplegalagreement");
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
				if (RefEasyTag.IsChecked == true)
				{
					difficulty = 0;
				}
				else if (RefNormalTag.IsChecked == true)
				{
					difficulty = 1;
				}
				else if (RefHardTag.IsChecked == true)
				{
					difficulty = 2;
				}
				else if (RefVeryHardTag.IsChecked == true)
				{
					difficulty = 3;
				}
				list.Add(workshopTags_Difficulty[difficulty]);
			}
			else
			{
				list.Add("n/a");
			}
			if (RefBalancedCheck.IsChecked.Value)
			{
				list.Add("Balanced");
			}
			CanClose = false;
			RefUploadPanel.Visibility = Visibility.Visible;
			string mapName = RefWorkshopMapTitle.Text;
			string description = RefWorkshopMapDescription.Text;
			WORKSHOP_UploadContentFolder = ConfigSettings.GetWorkshopUploadContentPath();
			string path = System.IO.Path.Combine(WORKSHOP_UploadContentFolder, mapName + ".map");
			EditorDirector.instance.SaveSaveGameOrMap(path, mapName, lockMap: true, tempLockOnly: true, mapSave: true);
			byte[] radarFromFile = MapFileManager.Instance.GetRadarFromFile(path);
			byte[] bytes = MapFileManager.Instance.GetRadarPreview(radarFromFile).EncodeToPNG();
			string text = System.IO.Path.Combine(ConfigSettings.GetWorkshopUploadRootPath(), "Upload.png");
			File.WriteAllBytes(text, bytes);
			WORKSHOP_UploadImage = text;
			Platform_Workshop.Instance.UploadWorkshopMap(WORKSHOP_UploadContentFolder, mapName, description, list.ToArray(), publicMap: true, WORKSHOP_UploadImage, delegate
			{
				HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 124), delegate
				{
					ulong publishID = Platform_Workshop.Instance.GetPublishID();
					string userWorkshopPath = ConfigSettings.GetUserWorkshopPath();
					string path2 = System.IO.Path.Combine(userWorkshopPath, mapName + ".map");
					EditorDirector.instance.SaveSaveGameOrMap(path2, mapName, lockMap: false, tempLockOnly: false, mapSave: true);
					string text2 = ((!RefBalancedCheck.IsChecked.Value) ? (publishID + "\n" + difficulty + "\n") : (publishID + "\n-" + difficulty + "\n"));
					string path3 = System.IO.Path.Combine(userWorkshopPath, mapName + ".data");
					text2 += description;
					File.WriteAllText(path3, text2);
					CanClose = true;
					RefUploadPanel.Visibility = Visibility.Hidden;
					MainViewModel.Instance.Show_HUD_WorkshopUploader = false;
					MainViewModel.Instance.Show_HUD_IngameMenu = true;
				});
			}, delegate
			{
				HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125), delegate
				{
					CanClose = true;
					RefUploadPanel.Visibility = Visibility.Hidden;
				});
			});
			break;
		}
		}
	}

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	private void TextBoxLoaded(object sender, RoutedEventArgs e)
	{
		RefWorkshopMapTitle.Focus();
	}

	private void FileNameValidationTextBox(object sender, TextCompositionEventArgs e)
	{
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

	private void TextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (RefWorkshopMapTitle.Text.Length > 4 && RefWorkshopMapDescription.Text.Length > 20)
		{
			RefWorkshopUpload.IsEnabled = true;
		}
		else
		{
			RefWorkshopUpload.IsEnabled = false;
		}
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_WorkshopUploader.xaml");
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
}
