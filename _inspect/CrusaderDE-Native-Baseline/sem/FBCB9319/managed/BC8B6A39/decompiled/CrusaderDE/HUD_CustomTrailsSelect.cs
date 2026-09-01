using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_CustomTrailsSelect : UserControl
{
	public static HUD_CustomTrailsSelect Instance = null;

	private Grid RefRow1;

	private Grid RefRow2;

	private Grid RefRow3;

	private Grid RefRow4;

	private Grid RefRow5;

	private Grid RefRow6;

	private Grid RefRow7;

	private Grid RefRow8;

	private Grid RefRow9;

	private Grid RefRow10;

	public int currentPage = -1;

	private List<MapFileManager.CustomTrailInfo> trails;

	private static SolidColorBrush oddRow_normal = new SolidColorBrush(Color.FromArgb(68, 0, 0, 0));

	private static SolidColorBrush evenRow_normal = new SolidColorBrush(Color.FromArgb(34, 0, 0, 0));

	private static SolidColorBrush rowHighlighted = new SolidColorBrush(Color.FromArgb(68, byte.MaxValue, byte.MaxValue, byte.MaxValue));

	public HUD_CustomTrailsSelect()
	{
		InitializeComponent();
		Instance = this;
		RefRow1 = (Grid)FindName("Row1");
		RefRow2 = (Grid)FindName("Row2");
		RefRow3 = (Grid)FindName("Row3");
		RefRow4 = (Grid)FindName("Row4");
		RefRow5 = (Grid)FindName("Row5");
		RefRow6 = (Grid)FindName("Row6");
		RefRow7 = (Grid)FindName("Row7");
		RefRow8 = (Grid)FindName("Row8");
		RefRow9 = (Grid)FindName("Row9");
		RefRow10 = (Grid)FindName("Row10");
	}

	public static void OpenCustomTrails()
	{
		MainViewModel.Instance.Show_HUD_CustomTrails = true;
		Instance.Init();
	}

	private void Init()
	{
		currentPage = 0;
		trails = MapFileManager.Instance.GetCustomTrails();
		UpdateList();
	}

	private void UpdateList()
	{
		int num = currentPage * 10;
		int num2 = 0;
		while (num2 < 10)
		{
			if (num < trails.Count)
			{
				ImageSource avatar = null;
				if (trails[num].workshop)
				{
					avatar = MainViewModel.Instance.GameSprites[737];
				}
				SetRow(num2, trails[num].Count.ToString(), trails[num].DisplayName, avatar);
			}
			else
			{
				SetRow(num2, "", "", null);
			}
			num2++;
			num++;
		}
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "Play":
			break;
		case "Exit":
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Skirmish");
			break;
		case "Top":
			currentPage = 0;
			UpdateList();
			break;
		case "Up":
			if (currentPage > 0)
			{
				currentPage--;
				UpdateList();
			}
			break;
		case "Down":
			if (currentPage < (trails.Count - 1) / 10)
			{
				currentPage++;
				UpdateList();
			}
			break;
		case "Row1":
			if (currentPage * 10 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10].Name);
			}
			break;
		case "Row2":
			if (currentPage * 10 + 1 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 1].Name);
			}
			break;
		case "Row3":
			if (currentPage * 10 + 2 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 2].Name);
			}
			break;
		case "Row4":
			if (currentPage * 10 + 3 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 3].Name);
			}
			break;
		case "Row5":
			if (currentPage * 10 + 4 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 4].Name);
			}
			break;
		case "Row6":
			if (currentPage * 10 + 5 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 5].Name);
			}
			break;
		case "Row7":
			if (currentPage * 10 + 6 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 6].Name);
			}
			break;
		case "Row8":
			if (currentPage * 10 + 7 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 7].Name);
			}
			break;
		case "Row9":
			if (currentPage * 10 + 8 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 8].Name);
			}
			break;
		case "Row10":
			if (currentPage * 10 + 9 < trails.Count)
			{
				MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(trails[currentPage * 10 + 9].Name);
			}
			break;
		}
	}

	private void SetRow(int row, string numMissions, string name, ImageSource avatar)
	{
		switch (row)
		{
		case 0:
			MainViewModel.Instance.Leaderboard_Pos_1 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_1 = name;
			MainViewModel.Instance.Leaderboard_Image_1 = avatar;
			break;
		case 1:
			MainViewModel.Instance.Leaderboard_Pos_2 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_2 = name;
			MainViewModel.Instance.Leaderboard_Image_2 = avatar;
			break;
		case 2:
			MainViewModel.Instance.Leaderboard_Pos_3 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_3 = name;
			MainViewModel.Instance.Leaderboard_Image_3 = avatar;
			break;
		case 3:
			MainViewModel.Instance.Leaderboard_Pos_4 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_4 = name;
			MainViewModel.Instance.Leaderboard_Image_4 = avatar;
			break;
		case 4:
			MainViewModel.Instance.Leaderboard_Pos_5 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_5 = name;
			MainViewModel.Instance.Leaderboard_Image_5 = avatar;
			break;
		case 5:
			MainViewModel.Instance.Leaderboard_Pos_6 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_6 = name;
			MainViewModel.Instance.Leaderboard_Image_6 = avatar;
			break;
		case 6:
			MainViewModel.Instance.Leaderboard_Pos_7 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_7 = name;
			MainViewModel.Instance.Leaderboard_Image_7 = avatar;
			break;
		case 7:
			MainViewModel.Instance.Leaderboard_Pos_8 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_8 = name;
			MainViewModel.Instance.Leaderboard_Image_8 = avatar;
			break;
		case 8:
			MainViewModel.Instance.Leaderboard_Pos_9 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_9 = name;
			MainViewModel.Instance.Leaderboard_Image_9 = avatar;
			break;
		case 9:
			MainViewModel.Instance.Leaderboard_Pos_10 = numMissions;
			MainViewModel.Instance.Leaderboard_Name_10 = name;
			MainViewModel.Instance.Leaderboard_Image_10 = avatar;
			break;
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_CustomTrailsSelect.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "MouseEnterMainButtonHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MouseEnterMainButtonHandler;
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveMainButtonHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseLeave += MouseLeaveMainButtonHandler;
			}
			return true;
		}
		return false;
	}

	private void MouseEnterMainButtonHandler(object sender, MouseEventArgs e)
	{
		if (e.Source is Button)
		{
			switch ((string)((Button)e.Source).CommandParameter)
			{
			case "Row1":
				RefRow1.Background = rowHighlighted;
				break;
			case "Row2":
				RefRow2.Background = rowHighlighted;
				break;
			case "Row3":
				RefRow3.Background = rowHighlighted;
				break;
			case "Row4":
				RefRow4.Background = rowHighlighted;
				break;
			case "Row5":
				RefRow5.Background = rowHighlighted;
				break;
			case "Row6":
				RefRow6.Background = rowHighlighted;
				break;
			case "Row7":
				RefRow7.Background = rowHighlighted;
				break;
			case "Row8":
				RefRow8.Background = rowHighlighted;
				break;
			case "Row9":
				RefRow9.Background = rowHighlighted;
				break;
			case "Row10":
				RefRow10.Background = rowHighlighted;
				break;
			}
		}
	}

	private void MouseLeaveMainButtonHandler(object sender, MouseEventArgs e)
	{
		if (e.Source is Button)
		{
			switch ((string)((Button)e.Source).CommandParameter)
			{
			case "Row1":
				RefRow1.Background = oddRow_normal;
				break;
			case "Row3":
				RefRow3.Background = oddRow_normal;
				break;
			case "Row5":
				RefRow5.Background = oddRow_normal;
				break;
			case "Row7":
				RefRow7.Background = oddRow_normal;
				break;
			case "Row9":
				RefRow9.Background = oddRow_normal;
				break;
			case "Row2":
				RefRow2.Background = evenRow_normal;
				break;
			case "Row4":
				RefRow4.Background = evenRow_normal;
				break;
			case "Row6":
				RefRow6.Background = evenRow_normal;
				break;
			case "Row8":
				RefRow8.Background = evenRow_normal;
				break;
			case "Row10":
				RefRow10.Background = evenRow_normal;
				break;
			}
		}
	}
}
