using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_CustomTrailsSelect : UserControl
{
	public static HUD_CustomTrailsSelect Instance = null;

	public Grid RefRow1;

	public Grid RefRow2;

	public Grid RefRow3;

	public Grid RefRow4;

	public Grid RefRow5;

	public Grid RefRow6;

	public Grid RefRow7;

	public Grid RefRow8;

	public Grid RefRow9;

	public Grid RefRow10;

	public int currentPage = -1;

	public List<MapFileManager.CustomTrailInfo> trails;

	public static SolidColorBrush oddRow_normal = new SolidColorBrush(Color.FromArgb((byte)68, (byte)0, (byte)0, (byte)0));

	public static SolidColorBrush evenRow_normal = new SolidColorBrush(Color.FromArgb((byte)34, (byte)0, (byte)0, (byte)0));

	public static SolidColorBrush rowHighlighted = new SolidColorBrush(Color.FromArgb((byte)68, byte.MaxValue, byte.MaxValue, byte.MaxValue));

	public HUD_CustomTrailsSelect()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Expected O, but got Unknown
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Expected O, but got Unknown
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Expected O, but got Unknown
		InitializeComponent();
		Instance = this;
		RefRow1 = (Grid)((FrameworkElement)this).FindName("Row1");
		RefRow2 = (Grid)((FrameworkElement)this).FindName("Row2");
		RefRow3 = (Grid)((FrameworkElement)this).FindName("Row3");
		RefRow4 = (Grid)((FrameworkElement)this).FindName("Row4");
		RefRow5 = (Grid)((FrameworkElement)this).FindName("Row5");
		RefRow6 = (Grid)((FrameworkElement)this).FindName("Row6");
		RefRow7 = (Grid)((FrameworkElement)this).FindName("Row7");
		RefRow8 = (Grid)((FrameworkElement)this).FindName("Row8");
		RefRow9 = (Grid)((FrameworkElement)this).FindName("Row9");
		RefRow10 = (Grid)((FrameworkElement)this).FindName("Row10");
	}

	public static void OpenCustomTrails()
	{
		MainViewModel.Instance.Show_HUD_CustomTrails = true;
		Instance.Init();
	}

	public void Init()
	{
		currentPage = 0;
		trails = MapFileManager.Instance.GetCustomTrails();
		UpdateList();
	}

	public void UpdateList()
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

	public void SetRow(int row, string numMissions, string name, ImageSource avatar)
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

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_CustomTrailsSelect.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "MouseEnterMainButtonHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MouseEnterMainButtonHandler);
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveMainButtonHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseLeave += new MouseEventHandler(MouseLeaveMainButtonHandler);
			}
			return true;
		}
		return false;
	}

	public void MouseEnterMainButtonHandler(object sender, MouseEventArgs e)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (((RoutedEventArgs)e).Source is Button)
		{
			switch ((string)((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter)
			{
			case "Row1":
				((Panel)RefRow1).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row2":
				((Panel)RefRow2).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row3":
				((Panel)RefRow3).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row4":
				((Panel)RefRow4).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row5":
				((Panel)RefRow5).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row6":
				((Panel)RefRow6).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row7":
				((Panel)RefRow7).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row8":
				((Panel)RefRow8).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row9":
				((Panel)RefRow9).Background = (Brush)(object)rowHighlighted;
				break;
			case "Row10":
				((Panel)RefRow10).Background = (Brush)(object)rowHighlighted;
				break;
			}
		}
	}

	public void MouseLeaveMainButtonHandler(object sender, MouseEventArgs e)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (((RoutedEventArgs)e).Source is Button)
		{
			switch ((string)((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter)
			{
			case "Row1":
				((Panel)RefRow1).Background = (Brush)(object)oddRow_normal;
				break;
			case "Row3":
				((Panel)RefRow3).Background = (Brush)(object)oddRow_normal;
				break;
			case "Row5":
				((Panel)RefRow5).Background = (Brush)(object)oddRow_normal;
				break;
			case "Row7":
				((Panel)RefRow7).Background = (Brush)(object)oddRow_normal;
				break;
			case "Row9":
				((Panel)RefRow9).Background = (Brush)(object)oddRow_normal;
				break;
			case "Row2":
				((Panel)RefRow2).Background = (Brush)(object)evenRow_normal;
				break;
			case "Row4":
				((Panel)RefRow4).Background = (Brush)(object)evenRow_normal;
				break;
			case "Row6":
				((Panel)RefRow6).Background = (Brush)(object)evenRow_normal;
				break;
			case "Row8":
				((Panel)RefRow8).Background = (Brush)(object)evenRow_normal;
				break;
			case "Row10":
				((Panel)RefRow10).Background = (Brush)(object)evenRow_normal;
				break;
			}
		}
	}
}
