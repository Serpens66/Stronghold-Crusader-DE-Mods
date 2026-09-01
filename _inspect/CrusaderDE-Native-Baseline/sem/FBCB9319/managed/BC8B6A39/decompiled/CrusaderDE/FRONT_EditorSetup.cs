using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Noesis;
using Steamworks;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_EditorSetup : UserControl
{
	private enum WorkshopMode
	{
		Trail,
		CustomLord,
		AIV,
		LordConfig
	}

	private WorkshopMode uploadMode;

	private int mapSize = 160;

	private int mode = 4;

	private Button refSize160;

	private Button refSize200;

	private Button refSize300;

	private Button refSize400;

	private Button refSize500;

	private Button refSize600;

	private Button refSize700;

	private Button refSize800;

	private TextBlock refMapEditorTypeHelpTB;

	private ListView RefUploadList;

	private TextBox RefWorkshopMapDescription;

	private Button RefWorkshopUpload;

	private Noesis.Grid RefUploadPanel;

	private Noesis.Grid RefLordSelectorPanel;

	private string WORKSHOP_UploadContentFolder = "";

	private string WORKSHOP_UploadImage = "";

	private MapFileManager.CustomTrailInfo selectedTrail;

	private int SelectedLordType = 1;

	public static bool canCloseWorkshop = true;

	private ObservableCollection<FileRow> uploadRows = new ObservableCollection<FileRow>();

	private CustomisationFileManager.CustomLord selectedCustomLord;

	private CustomisationFileManager.CustomAIV selectedAIV;

	private CustomisationFileManager.CustomLordConfig selectedLordConfig;

	public FRONT_EditorSetup()
	{
		InitializeComponent();
		MainViewModel.Instance.FRONTEditorSetup = this;
		refSize160 = (Button)FindName("Size160");
		refSize200 = (Button)FindName("Size200");
		refSize300 = (Button)FindName("Size300");
		refSize400 = (Button)FindName("Size400");
		refSize500 = (Button)FindName("Size500");
		refSize600 = (Button)FindName("Size600");
		refSize700 = (Button)FindName("Size700");
		refSize800 = (Button)FindName("Size800");
		refMapEditorTypeHelpTB = (TextBlock)FindName("MapEditorTypeHelpTB");
		RefUploadList = (ListView)FindName("UploadList");
		((GridViewColumnHeader)((GridView)RefUploadList.View).Columns[1].Header).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		RefUploadList.SelectionChanged += delegate
		{
			_ = RefUploadList.SelectedItem;
		};
		RefWorkshopMapDescription = (TextBox)FindName("WorkshopMapDescription");
		RefWorkshopMapDescription.IsKeyboardFocusedChanged += TextInputFocus;
		RefWorkshopMapDescription.TextChanged += TextChangedHandler;
		RefWorkshopUpload = (Button)FindName("WorkshopUpload");
		RefUploadPanel = (Noesis.Grid)FindName("UploadPanel");
		RefLordSelectorPanel = (Noesis.Grid)FindName("LordSelectorPanel");
		if (FatControler.thai)
		{
			refMapEditorTypeHelpTB.FontSize = 14f;
		}
	}

	public static void Open()
	{
		MainViewModel.Instance.Show_MapEditor = true;
		Platform_Workshop.Instance.clearMetaData();
		MainViewModel.Instance.FRONTEditorSetup.doOpen();
		canCloseWorkshop = true;
	}

	private void doOpen()
	{
		mapSize = 160;
		mode = 4;
		RefUploadPanel.Visibility = Visibility.Hidden;
		RefLordSelectorPanel.Visibility = Visibility.Collapsed;
		UpdateButtons();
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "Back":
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
			break;
		case "TrailMaker":
			FrontendMenus.ClearUIPanels();
			FRONT_Multiplayer.Open(skirmishSetup: true, null, coopSetup: false, trailMaker: true);
			break;
		case "Start":
			Director.instance.SetWaitCursor();
			MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
			switch (mode)
			{
			case 1:
				EditorDirector.instance.createNewMap(mapSize, Enums.GameModes.INVASION, siege_that: false);
				break;
			case 3:
				EditorDirector.instance.createNewMap(mapSize, Enums.GameModes.BUILD, siege_that: false);
				break;
			case 4:
				EditorDirector.instance.createNewMap(mapSize, Enums.GameModes.BUILD, siege_that: false, multiPlayerMap: true);
				break;
			case 2:
				break;
			}
			break;
		case "160":
			if (mode != 5)
			{
				mapSize = 160;
				UpdateButtons();
			}
			break;
		case "200":
			if (mode != 5)
			{
				mapSize = 200;
				UpdateButtons();
			}
			break;
		case "300":
			if (mode != 5)
			{
				mapSize = 300;
				UpdateButtons();
			}
			break;
		case "400":
			if (mode != 5)
			{
				mapSize = 400;
				UpdateButtons();
			}
			break;
		case "500":
			if (mode != 5)
			{
				mapSize = 500;
				UpdateButtons();
			}
			break;
		case "600":
			if (mode != 5)
			{
				mapSize = 600;
				UpdateButtons();
			}
			break;
		case "700":
			if (mode != 5)
			{
				mapSize = 700;
				UpdateButtons();
			}
			break;
		case "800":
			if (mode != 5)
			{
				mapSize = 800;
				UpdateButtons();
			}
			break;
		case "Invasion":
			mode = 1;
			UpdateButtons();
			break;
		case "Freebuild":
			mode = 3;
			UpdateButtons();
			break;
		case "Multiplayer":
			mode = 4;
			UpdateButtons();
			break;
		case "Load":
			MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
			HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadEditorMap, delegate(string filename, FileHeader header)
			{
				if (header.isMapEditable())
				{
					GameData.Instance.SetMissionTextFromHeader(header);
					MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
					EditorDirector.instance.loadMapIntoEditor(header.filePath, header.standAlone_filename);
				}
			}, delegate
			{
			});
			break;
		case "Help":
			SteamFriends.ActivateGameOverlayToWebPage("https://store.steampowered.com/manual/3024040");
			break;
		case "LoadWorkshop":
			MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
			HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadUserWorkshopMap, delegate(string filename, FileHeader header)
			{
				if (header.isMapEditable())
				{
					GameData.Instance.SetMissionTextFromHeader(header);
					MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
					EditorDirector.instance.loadMapIntoEditor(header.filePath, header.standAlone_filename);
					string path5 = header.filePath.Replace(".map", ".data");
					try
					{
						string[] array5 = File.ReadAllLines(path5);
						if (array5 != null && array5.Length >= 2)
						{
							bool balanced5 = false;
							ulong publishID5 = ulong.Parse(array5[0], Director.defaultCulture);
							if (array5[1][0] == '-')
							{
								balanced5 = true;
								array5[1] = array5[1].Substring(1);
							}
							int difficulty5 = int.Parse(array5[1], Director.defaultCulture);
							string text15 = "";
							for (int i = 2; i < array5.Length; i++)
							{
								if (i > 2 && (i != array5.Length || array5[i].Length > 0))
								{
									text15 += "\n";
								}
								text15 += array5[i];
							}
							Platform_Workshop.Instance.importMetaData(publishID5, header.standAlone_filename, difficulty5, text15, balanced5);
						}
					}
					catch (Exception)
					{
					}
				}
			}, delegate
			{
			});
			break;
		case "UploadTrail":
		{
			MainViewModel.Instance.EditorUploadInfo = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 60);
			MainViewModel.Instance.Show_EditorWorkshop_Uploader = false;
			MainViewModel.Instance.Show_EditorWorkshop_Requester = true;
			RefLordSelectorPanel.Visibility = Visibility.Collapsed;
			uploadMode = WorkshopMode.Trail;
			List<MapFileManager.CustomTrailInfo> customTrails = MapFileManager.Instance.GetCustomTrails(ignoreWorkshopTrails: true);
			uploadRows.Clear();
			foreach (MapFileManager.CustomTrailInfo item in customTrails)
			{
				FileRow fileRow2 = new FileRow();
				fileRow2.Text1 = item.Name;
				fileRow2.Text2 = item.Count.ToString();
				fileRow2.trail = item;
				if (item.workshopUploadInfoAvailable)
				{
					fileRow2.TypeImage = MainViewModel.Instance.GameSprites[746];
				}
				uploadRows.Add(fileRow2);
			}
			RefUploadList.ItemsSource = uploadRows;
			if (uploadRows.Count > 0)
			{
				RefUploadList.SelectedIndex = 0;
			}
			else
			{
				RefUploadList.SelectedItem = null;
			}
			break;
		}
		case "UploadAIV":
			MainViewModel.Instance.EditorUploadInfo = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 61);
			MainViewModel.Instance.Show_EditorWorkshop_Uploader = false;
			MainViewModel.Instance.Show_EditorWorkshop_Requester = true;
			RefLordSelectorPanel.Visibility = Visibility.Visible;
			uploadMode = WorkshopMode.AIV;
			SelectedLordType = 1;
			UpdateUploadAIVList();
			break;
		case "UploadLordConfig":
			MainViewModel.Instance.EditorUploadInfo = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 62);
			MainViewModel.Instance.Show_EditorWorkshop_Uploader = false;
			MainViewModel.Instance.Show_EditorWorkshop_Requester = true;
			RefLordSelectorPanel.Visibility = Visibility.Visible;
			uploadMode = WorkshopMode.LordConfig;
			SelectedLordType = 1;
			UpdateUploadLordList();
			break;
		case "UploadCustomLord":
		{
			MainViewModel.Instance.EditorUploadInfo = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 63);
			MainViewModel.Instance.Show_EditorWorkshop_Uploader = false;
			MainViewModel.Instance.Show_EditorWorkshop_Requester = true;
			RefLordSelectorPanel.Visibility = Visibility.Collapsed;
			uploadMode = WorkshopMode.CustomLord;
			List<CustomisationFileManager.CustomLord> customLords = CustomisationFileManager.Instance.GetCustomLords(includeWorkshop: false);
			uploadRows.Clear();
			foreach (CustomisationFileManager.CustomLord item2 in customLords)
			{
				FileRow fileRow = new FileRow();
				fileRow.Text1 = item2.lordDisplayName;
				fileRow.Text2 = "";
				fileRow.lord = item2;
				if (item2.workshopUploadInfoAvailable)
				{
					fileRow.TypeImage = MainViewModel.Instance.GameSprites[746];
				}
				uploadRows.Add(fileRow);
			}
			RefUploadList.ItemsSource = uploadRows;
			if (uploadRows.Count > 0)
			{
				RefUploadList.SelectedIndex = 0;
			}
			else
			{
				RefUploadList.SelectedItem = null;
			}
			break;
		}
		case "CloseUpload":
			MainViewModel.Instance.Show_EditorWorkshop_Requester = false;
			MainViewModel.Instance.Show_EditorWorkshop_Uploader = false;
			break;
		case "Upload":
			if (RefUploadList.SelectedItem == null)
			{
				break;
			}
			MainViewModel.Instance.Show_EditorWorkshop_Uploader = true;
			RefWorkshopUpload.IsEnabled = false;
			Platform_Workshop.Instance.clearMetaData();
			switch (uploadMode)
			{
			case WorkshopMode.Trail:
			{
				selectedTrail = ((FileRow)RefUploadList.SelectedItem).trail;
				MainViewModel.Instance.EditorDoUploadHeader = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 65) + " " + selectedTrail.Name;
				string path4 = System.IO.Path.Combine(ConfigSettings.GetUserCustomTrailsPath(), selectedTrail.Name, selectedTrail.Name + ".data");
				try
				{
					string[] array4 = File.ReadAllLines(path4);
					if (array4 == null || array4.Length < 2)
					{
						break;
					}
					bool balanced4 = false;
					ulong publishID4 = ulong.Parse(array4[0], Director.defaultCulture);
					if (array4[1][0] == '-')
					{
						balanced4 = true;
						array4[1] = array4[1].Substring(1);
					}
					int difficulty4 = int.Parse(array4[1], Director.defaultCulture);
					string text14 = "";
					for (int num5 = 2; num5 < array4.Length; num5++)
					{
						if (num5 > 2 && (num5 != array4.Length || array4[num5].Length > 0))
						{
							text14 += "\n";
						}
						text14 += array4[num5];
					}
					Platform_Workshop.Instance.importMetaData(publishID4, selectedTrail.Name, difficulty4, text14, balanced4);
					UpdateDescriptionText(text14);
					MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 127);
					break;
				}
				catch (Exception)
				{
					UpdateDescriptionText("");
					MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 114);
					break;
				}
			}
			case WorkshopMode.CustomLord:
			{
				selectedCustomLord = ((FileRow)RefUploadList.SelectedItem).lord;
				MainViewModel.Instance.EditorDoUploadHeader = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 68) + " " + selectedCustomLord.lordName;
				string path3 = System.IO.Path.Combine(selectedCustomLord.customPath, selectedCustomLord.lordName + ".data");
				try
				{
					string[] array3 = File.ReadAllLines(path3);
					if (array3 == null || array3.Length < 2)
					{
						break;
					}
					bool balanced3 = false;
					ulong publishID3 = ulong.Parse(array3[0], Director.defaultCulture);
					if (array3[1][0] == '-')
					{
						balanced3 = true;
						array3[1] = array3[1].Substring(1);
					}
					int difficulty3 = int.Parse(array3[1], Director.defaultCulture);
					string text13 = "";
					for (int num4 = 2; num4 < array3.Length; num4++)
					{
						if (num4 > 2 && (num4 != array3.Length || array3[num4].Length > 0))
						{
							text13 += "\n";
						}
						text13 += array3[num4];
					}
					Platform_Workshop.Instance.importMetaData(publishID3, selectedCustomLord.lordName, difficulty3, text13, balanced3);
					UpdateDescriptionText(text13);
					MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 127);
					break;
				}
				catch (Exception)
				{
					UpdateDescriptionText("");
					MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 114);
					break;
				}
			}
			case WorkshopMode.AIV:
			{
				selectedAIV = ((FileRow)RefUploadList.SelectedItem).aiv;
				MainViewModel.Instance.EditorDoUploadHeader = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 66) + " " + Translate.Instance.GetLordName(SelectedLordType - 1) + " / " + selectedAIV.AIVName;
				string path2 = System.IO.Path.Combine(selectedAIV.path, selectedAIV.AIVName + ".data");
				try
				{
					string[] array2 = File.ReadAllLines(path2);
					if (array2 == null || array2.Length < 2)
					{
						break;
					}
					bool balanced2 = false;
					ulong publishID2 = ulong.Parse(array2[0], Director.defaultCulture);
					if (array2[1][0] == '-')
					{
						balanced2 = true;
						array2[1] = array2[1].Substring(1);
					}
					int difficulty2 = int.Parse(array2[1], Director.defaultCulture);
					string text12 = "";
					for (int num3 = 2; num3 < array2.Length; num3++)
					{
						if (num3 > 2 && (num3 != array2.Length || array2[num3].Length > 0))
						{
							text12 += "\n";
						}
						text12 += array2[num3];
					}
					Platform_Workshop.Instance.importMetaData(publishID2, selectedAIV.AIVName, difficulty2, text12, balanced2);
					UpdateDescriptionText(text12);
					MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 127);
					break;
				}
				catch (Exception)
				{
					UpdateDescriptionText("");
					MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 114);
					break;
				}
			}
			case WorkshopMode.LordConfig:
			{
				selectedLordConfig = ((FileRow)RefUploadList.SelectedItem).lordConfig;
				MainViewModel.Instance.EditorDoUploadHeader = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 67) + " " + Translate.Instance.GetLordName(SelectedLordType - 1) + " / " + selectedLordConfig.name;
				string path = System.IO.Path.Combine(selectedLordConfig.path, selectedLordConfig.name + ".ldata");
				try
				{
					string[] array = File.ReadAllLines(path);
					if (array == null || array.Length < 2)
					{
						break;
					}
					bool balanced = false;
					ulong publishID = ulong.Parse(array[0], Director.defaultCulture);
					if (array[1][0] == '-')
					{
						balanced = true;
						array[1] = array[1].Substring(1);
					}
					int difficulty = int.Parse(array[1], Director.defaultCulture);
					string text11 = "";
					for (int num2 = 2; num2 < array.Length; num2++)
					{
						if (num2 > 2 && (num2 != array.Length || array[num2].Length > 0))
						{
							text11 += "\n";
						}
						text11 += array[num2];
					}
					Platform_Workshop.Instance.importMetaData(publishID, selectedLordConfig.name, difficulty, text11, balanced);
					UpdateDescriptionText(text11);
					MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 127);
					break;
				}
				catch (Exception)
				{
					UpdateDescriptionText("");
					MainViewModel.Instance.WorkshopUploadText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 114);
					break;
				}
			}
			}
			break;
		case "CloseDoUpload":
			MainViewModel.Instance.Show_EditorWorkshop_Uploader = false;
			break;
		case "DoUpload":
		{
			MainViewModel.Instance.Show_EditorWorkshop_Uploader = false;
			WORKSHOP_UploadContentFolder = ConfigSettings.GetWorkshopUploadContentPath();
			List<string> list = new List<string>();
			switch (uploadMode)
			{
			case WorkshopMode.Trail:
			{
				list.Add("Custom Trail");
				string text8 = System.IO.Path.Combine(Application.streamingAssetsPath, "WorkshopImages") + "\\";
				int count = selectedTrail.Count;
				if (count <= 20)
				{
					text8 += "Short.png";
					list.Add("Short (1-20)");
				}
				else if (count <= 30)
				{
					text8 += "Medium.png";
					list.Add("Medium (21-30)");
				}
				else
				{
					text8 += "Long.png";
					list.Add("Long (31-50)");
				}
				string text9 = System.IO.Path.Combine(ConfigSettings.GetWorkshopUploadRootPath(), "Upload.png");
				File.Copy(text8, text9, overwrite: true);
				string source4 = System.IO.Path.Combine(ConfigSettings.GetUserCustomTrailsPath(), selectedTrail.Name);
				string text10 = System.IO.Path.Combine(WORKSHOP_UploadContentFolder, selectedTrail.Name);
				try
				{
					Directory.CreateDirectory(text10);
				}
				catch (Exception)
				{
					break;
				}
				string[] files = Directory.GetFiles(source4, "*.trail");
				foreach (string obj3 in files)
				{
					File.Copy(obj3, obj3.Replace(source4, text10));
				}
				canCloseWorkshop = false;
				RefUploadPanel.Visibility = Visibility.Visible;
				WORKSHOP_UploadImage = text9;
				Platform_Workshop.Instance.UploadWorkshopMap(WORKSHOP_UploadContentFolder, selectedTrail.Name, RefWorkshopMapDescription.Text, list.ToArray(), publicMap: true, WORKSHOP_UploadImage, delegate
				{
					ulong publishID5 = Platform_Workshop.Instance.GetPublishID();
					ConfigSettings.GetUserWorkshopPath();
					string path5 = System.IO.Path.Combine(source4, selectedTrail.Name + ".data");
					string text15 = publishID5 + "\n" + 0 + "\n";
					text15 += RefWorkshopMapDescription.Text;
					File.WriteAllText(path5, text15);
					selectedTrail.workshopUploadInfoAvailable = true;
					HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 124), delegate
					{
						canCloseWorkshop = true;
						RefUploadPanel.Visibility = Visibility.Hidden;
						ButtonClicked("UploadTrail");
					});
				}, delegate
				{
					HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125), delegate
					{
						canCloseWorkshop = true;
						RefUploadPanel.Visibility = Visibility.Hidden;
					});
				});
				break;
			}
			case WorkshopMode.CustomLord:
			{
				list.Add("Custom Lord");
				string text3 = System.IO.Path.Combine(Application.streamingAssetsPath, "WorkshopImages") + "\\";
				text3 = selectedCustomLord.configs[0].lordData.lord_gfx_type switch
				{
					1 => text3 + "Lord Arabic Male.png", 
					2 => text3 + "Lord Bedouin Male.png", 
					4 => text3 + "Lord Crusader Female.png", 
					6 => text3 + "Lord Arabic Female.png", 
					7 => text3 + "Lord Bedouin Female.png", 
					_ => text3 + "Lord Crusader Male.png", 
				};
				if (selectedCustomLord.imagePath != null)
				{
					text3 = selectedCustomLord.imagePath;
				}
				string text4 = System.IO.Path.Combine(ConfigSettings.GetWorkshopUploadRootPath(), "Upload.png");
				File.Copy(text3, text4, overwrite: true);
				string source2 = selectedCustomLord.customPath;
				string text5 = System.IO.Path.Combine(WORKSHOP_UploadContentFolder, selectedCustomLord.lordName);
				try
				{
					Directory.CreateDirectory(text5);
				}
				catch (Exception)
				{
					break;
				}
				string[] files = Directory.GetFiles(source2, "*.aivjson");
				foreach (string obj in files)
				{
					File.Copy(obj, obj.Replace(source2, text5));
				}
				files = Directory.GetFiles(source2, "*.lordjson");
				foreach (string obj2 in files)
				{
					File.Copy(obj2, obj2.Replace(source2, text5));
				}
				if (selectedCustomLord.imagePath != null)
				{
					text3 = selectedCustomLord.imagePath;
					string destFileName = System.IO.Path.Combine(text5, "avatar.png");
					File.Copy(text3, destFileName, overwrite: true);
				}
				canCloseWorkshop = false;
				RefUploadPanel.Visibility = Visibility.Visible;
				WORKSHOP_UploadImage = text4;
				Platform_Workshop.Instance.UploadWorkshopMap(WORKSHOP_UploadContentFolder, selectedCustomLord.lordName, RefWorkshopMapDescription.Text, list.ToArray(), publicMap: true, WORKSHOP_UploadImage, delegate
				{
					ulong publishID5 = Platform_Workshop.Instance.GetPublishID();
					ConfigSettings.GetUserWorkshopPath();
					string path5 = System.IO.Path.Combine(source2, selectedCustomLord.lordName + ".data");
					string text15 = publishID5 + "\n" + 0 + "\n";
					text15 += RefWorkshopMapDescription.Text;
					File.WriteAllText(path5, text15);
					selectedCustomLord.workshopUploadInfoAvailable = true;
					HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 124), delegate
					{
						canCloseWorkshop = true;
						RefUploadPanel.Visibility = Visibility.Hidden;
						ButtonClicked("UploadCustomLord");
					});
				}, delegate
				{
					HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125), delegate
					{
						canCloseWorkshop = true;
						RefUploadPanel.Visibility = Visibility.Hidden;
					});
				});
				break;
			}
			case WorkshopMode.AIV:
			{
				list.Add("Extended AIV Castle");
				string sourceFileName2 = string.Concat(System.IO.Path.Combine(Application.streamingAssetsPath, "WorkshopImages") + "\\", "Face ", SelectedLordType.ToString("00"), " Castle.png");
				list.Add(ConfigSettings.extendedLordPaths[SelectedLordType - 1]);
				string text6 = System.IO.Path.Combine(ConfigSettings.GetWorkshopUploadRootPath(), "Upload.png");
				File.Copy(sourceFileName2, text6, overwrite: true);
				string source3 = selectedAIV.path;
				string text7 = System.IO.Path.Combine(WORKSHOP_UploadContentFolder, ConfigSettings.extendedLordPaths[SelectedLordType - 1]);
				try
				{
					Directory.CreateDirectory(text7);
				}
				catch (Exception)
				{
					break;
				}
				File.Copy(System.IO.Path.Combine(selectedAIV.path, selectedAIV.AIVName + ".aivjson"), System.IO.Path.Combine(text7, selectedAIV.AIVName + ".aivjson"));
				canCloseWorkshop = false;
				RefUploadPanel.Visibility = Visibility.Visible;
				WORKSHOP_UploadImage = text6;
				Platform_Workshop.Instance.UploadWorkshopMap(WORKSHOP_UploadContentFolder, selectedAIV.AIVName, RefWorkshopMapDescription.Text, list.ToArray(), publicMap: true, WORKSHOP_UploadImage, delegate
				{
					ulong publishID5 = Platform_Workshop.Instance.GetPublishID();
					ConfigSettings.GetUserWorkshopPath();
					string path5 = System.IO.Path.Combine(source3, selectedAIV.AIVName + ".data");
					string text15 = publishID5 + "\n" + 0 + "\n";
					text15 += RefWorkshopMapDescription.Text;
					File.WriteAllText(path5, text15);
					selectedAIV.workshopUploadInfoAvailable = true;
					HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 124), delegate
					{
						canCloseWorkshop = true;
						RefUploadPanel.Visibility = Visibility.Hidden;
						ButtonClicked("UploadAIV");
					});
				}, delegate
				{
					HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125), delegate
					{
						canCloseWorkshop = true;
						RefUploadPanel.Visibility = Visibility.Hidden;
					});
				});
				break;
			}
			case WorkshopMode.LordConfig:
			{
				list.Add("Extended CPU Lord");
				string sourceFileName = string.Concat(System.IO.Path.Combine(Application.streamingAssetsPath, "WorkshopImages") + "\\", "Face ", SelectedLordType.ToString("00"), " Lord.png");
				list.Add(ConfigSettings.extendedLordPaths[SelectedLordType - 1]);
				string text = System.IO.Path.Combine(ConfigSettings.GetWorkshopUploadRootPath(), "Upload.png");
				File.Copy(sourceFileName, text, overwrite: true);
				string source = selectedLordConfig.path;
				string text2 = System.IO.Path.Combine(WORKSHOP_UploadContentFolder, ConfigSettings.extendedLordPaths[SelectedLordType - 1]);
				try
				{
					Directory.CreateDirectory(text2);
				}
				catch (Exception)
				{
					break;
				}
				File.Copy(System.IO.Path.Combine(selectedLordConfig.path, selectedLordConfig.name + ".lordjson"), System.IO.Path.Combine(text2, selectedLordConfig.name + ".lordjson"));
				canCloseWorkshop = false;
				RefUploadPanel.Visibility = Visibility.Visible;
				WORKSHOP_UploadImage = text;
				Platform_Workshop.Instance.UploadWorkshopMap(WORKSHOP_UploadContentFolder, selectedLordConfig.name, RefWorkshopMapDescription.Text, list.ToArray(), publicMap: true, WORKSHOP_UploadImage, delegate
				{
					ulong publishID5 = Platform_Workshop.Instance.GetPublishID();
					ConfigSettings.GetUserWorkshopPath();
					string path5 = System.IO.Path.Combine(source, selectedLordConfig.name + ".ldata");
					string text15 = publishID5 + "\n" + 0 + "\n";
					text15 += RefWorkshopMapDescription.Text;
					File.WriteAllText(path5, text15);
					selectedLordConfig.workshopUploadInfoAvailable = true;
					HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 124), delegate
					{
						canCloseWorkshop = true;
						RefUploadPanel.Visibility = Visibility.Hidden;
						ButtonClicked("UploadLordConfig");
					});
				}, delegate
				{
					HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125), delegate
					{
						canCloseWorkshop = true;
						RefUploadPanel.Visibility = Visibility.Hidden;
					});
				});
				break;
			}
			}
			break;
		}
		case "Lord_01":
		case "Lord_02":
		case "Lord_03":
		case "Lord_04":
		case "Lord_05":
		case "Lord_06":
		case "Lord_07":
		case "Lord_08":
		case "Lord_09":
		case "Lord_10":
		case "Lord_11":
		case "Lord_12":
		case "Lord_13":
		case "Lord_14":
		case "Lord_15":
		case "Lord_16":
		case "Lord_17":
		case "Lord_18":
		case "Lord_19":
		case "Lord_20":
		case "Lord_21":
		case "Lord_22":
		case "Lord_23":
		case "Lord_24":
		case "Lord_25":
		case "Lord_26":
		case "Lord_27":
		case "Lord_28":
		case "Lord_29":
		case "Lord_30":
			SelectedLordType = int.Parse(param.Substring(param.Length - 2, 2));
			if (uploadMode == WorkshopMode.AIV)
			{
				UpdateUploadAIVList();
			}
			else if (uploadMode == WorkshopMode.LordConfig)
			{
				UpdateUploadLordList();
			}
			break;
		case "ToS":
			SteamFriends.ActivateGameOverlayToWebPage("http://steamcommunity.com/sharedfiles/workshoplegalagreement");
			break;
		}
	}

	private void UpdateButtons()
	{
		switch (mapSize)
		{
		case 160:
			MainViewModel.Instance.MapEditorSetup160 = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetup200 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup300 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup400 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup500 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup600 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup700 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup800 = Visibility.Hidden;
			break;
		case 200:
			MainViewModel.Instance.MapEditorSetup160 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup200 = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetup300 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup400 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup500 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup600 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup700 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup800 = Visibility.Hidden;
			break;
		case 300:
			MainViewModel.Instance.MapEditorSetup160 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup200 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup300 = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetup400 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup500 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup600 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup700 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup800 = Visibility.Hidden;
			break;
		case 400:
			MainViewModel.Instance.MapEditorSetup160 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup200 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup300 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup400 = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetup500 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup600 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup700 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup800 = Visibility.Hidden;
			break;
		case 500:
			MainViewModel.Instance.MapEditorSetup160 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup200 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup300 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup400 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup500 = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetup600 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup700 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup800 = Visibility.Hidden;
			break;
		case 600:
			MainViewModel.Instance.MapEditorSetup160 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup200 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup300 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup400 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup500 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup600 = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetup700 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup800 = Visibility.Hidden;
			break;
		case 700:
			MainViewModel.Instance.MapEditorSetup160 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup200 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup300 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup400 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup500 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup600 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup700 = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetup800 = Visibility.Hidden;
			break;
		case 800:
			MainViewModel.Instance.MapEditorSetup160 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup200 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup300 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup400 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup500 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup600 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup700 = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetup800 = Visibility.Visible;
			break;
		}
		switch (mode)
		{
		case 1:
			MainViewModel.Instance.MapEditorSetupInvasion = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetupFree = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetupMulti = Visibility.Hidden;
			refSize160.IsEnabled = true;
			refSize200.IsEnabled = true;
			refSize300.IsEnabled = true;
			refSize400.IsEnabled = true;
			refSize500.IsEnabled = true;
			refSize600.IsEnabled = true;
			refSize700.IsEnabled = true;
			refSize800.IsEnabled = true;
			break;
		case 3:
			MainViewModel.Instance.MapEditorSetupInvasion = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetupFree = Visibility.Visible;
			MainViewModel.Instance.MapEditorSetupMulti = Visibility.Hidden;
			refSize160.IsEnabled = true;
			refSize200.IsEnabled = true;
			refSize300.IsEnabled = true;
			refSize400.IsEnabled = true;
			refSize500.IsEnabled = true;
			refSize600.IsEnabled = true;
			refSize700.IsEnabled = true;
			refSize800.IsEnabled = true;
			break;
		case 4:
			MainViewModel.Instance.MapEditorSetupInvasion = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetupFree = Visibility.Hidden;
			MainViewModel.Instance.MapEditorSetupMulti = Visibility.Visible;
			refSize160.IsEnabled = true;
			refSize200.IsEnabled = true;
			refSize300.IsEnabled = true;
			refSize400.IsEnabled = true;
			refSize500.IsEnabled = true;
			refSize600.IsEnabled = true;
			refSize700.IsEnabled = true;
			refSize800.IsEnabled = true;
			break;
		case 2:
			break;
		}
	}

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	private void TextBoxLoaded(object sender, RoutedEventArgs e)
	{
		RefWorkshopMapDescription.Focus();
	}

	private void TextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (RefWorkshopMapDescription.Text.Length > 20)
		{
			RefWorkshopUpload.IsEnabled = true;
		}
		else
		{
			RefWorkshopUpload.IsEnabled = false;
		}
	}

	private void UpdateDescriptionText(string text)
	{
		RefWorkshopMapDescription.Text = text;
		TextChangedHandler(null, null);
	}

	private void UpdateUploadAIVList()
	{
		List<CustomisationFileManager.CustomAIV> lordAIVList = CustomisationFileManager.Instance.getLordAIVList(SelectedLordType - 1);
		uploadRows.Clear();
		if (lordAIVList != null)
		{
			foreach (CustomisationFileManager.CustomAIV item in lordAIVList)
			{
				if (!item.builtIn && !item.workshop)
				{
					FileRow fileRow = new FileRow();
					fileRow.Text1 = item.AIVName;
					fileRow.Text2 = "";
					fileRow.aiv = item;
					if (item.workshopUploadInfoAvailable)
					{
						fileRow.TypeImage = MainViewModel.Instance.GameSprites[746];
					}
					uploadRows.Add(fileRow);
				}
			}
		}
		RefUploadList.ItemsSource = uploadRows;
		if (uploadRows.Count > 0)
		{
			RefUploadList.SelectedIndex = 0;
		}
		else
		{
			RefUploadList.SelectedItem = null;
		}
		MainViewModel.Instance.EditorUploadLordName = Translate.Instance.GetLordName(SelectedLordType - 1);
	}

	private void UpdateUploadLordList()
	{
		List<CustomisationFileManager.CustomLordConfig> lordLordList = CustomisationFileManager.Instance.getLordLordList(SelectedLordType - 1);
		uploadRows.Clear();
		if (lordLordList != null)
		{
			foreach (CustomisationFileManager.CustomLordConfig item in lordLordList)
			{
				if (!item.workshop)
				{
					FileRow fileRow = new FileRow();
					fileRow.Text1 = item.name;
					fileRow.Text2 = "";
					fileRow.lordConfig = item;
					if (item.workshopUploadInfoAvailable)
					{
						fileRow.TypeImage = MainViewModel.Instance.GameSprites[746];
					}
					uploadRows.Add(fileRow);
				}
			}
		}
		RefUploadList.ItemsSource = uploadRows;
		if (uploadRows.Count > 0)
		{
			RefUploadList.SelectedIndex = 0;
		}
		else
		{
			RefUploadList.SelectedItem = null;
		}
		MainViewModel.Instance.EditorUploadLordName = Translate.Instance.GetLordName(SelectedLordType - 1);
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_EditorSetup.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "MouseEnterTypeHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MouseEnterTypeHandler;
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveTypeHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseLeave += MouseLeaveTypeHandler;
			}
			return true;
		}
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

	private void MouseEnterTypeHandler(object sender, MouseEventArgs e)
	{
		if (e.Source is Button && ((Button)e.Source).CommandParameter != null && ((Button)e.Source).CommandParameter is string)
		{
			switch ((string)((Button)e.Source).CommandParameter)
			{
			case "Siege":
				MainViewModel.Instance.MapEditorTypeHelp = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEWMAP_TYPES_HELP, 1);
				MainViewModel.Instance.CommonRedButtonEnter(null, null);
				break;
			case "Invasion":
				MainViewModel.Instance.MapEditorTypeHelp = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEWMAP_TYPES_HELP, 3);
				MainViewModel.Instance.CommonRedButtonEnter(null, null);
				break;
			case "Economic":
				MainViewModel.Instance.MapEditorTypeHelp = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEWMAP_TYPES_HELP, 5);
				MainViewModel.Instance.CommonRedButtonEnter(null, null);
				break;
			case "Freebuild":
				MainViewModel.Instance.MapEditorTypeHelp = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEWMAP_TYPES_HELP, 7);
				MainViewModel.Instance.CommonRedButtonEnter(null, null);
				break;
			case "Multiplayer":
				MainViewModel.Instance.MapEditorTypeHelp = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 109);
				MainViewModel.Instance.CommonRedButtonEnter(null, null);
				break;
			case "SiegeThat":
				MainViewModel.Instance.MapEditorTypeHelp = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, 37);
				MainViewModel.Instance.CommonRedButtonEnter(null, null);
				break;
			}
		}
	}

	private void MouseLeaveTypeHandler(object sender, MouseEventArgs e)
	{
		MainViewModel.Instance.MapEditorTypeHelp = "";
	}
}
