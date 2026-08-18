using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_Leaderboard : UserControl
{
	public class AvatarCallback
	{
		public int row;

		public ulong steamID;
	}

	public static HUD_Leaderboard Instance = null;

	public static string LeaderboardName = "";

	public RadioButton refFriendsButton;

	public RadioButton refGlobalButton;

	public bool pageTypeFriends = true;

	public int currentPage = -1;

	public bool retryGlobal;

	public bool pending;

	public bool pending_Friends;

	public int pending_Page;

	public Queue<AvatarCallback> avatarCallbacks = new Queue<AvatarCallback>();

	public HUD_Leaderboard()
	{
		InitializeComponent();
	}

	public static void OpenLeaderboard(string leaderboardName, string leaderboardTitle)
	{
		MainViewModel.Instance.Show_Leaderboard = true;
		MainViewModel.Instance.LeaderboardTitle = " - " + leaderboardTitle;
		Instance = (HUD_Leaderboard)(object)FatControler.instance.FindVisibleUIElement(typeof(HUD_Leaderboard));
		Instance.Init();
		Instance.UpdateLeaderboard(leaderboardName, friendsOnly: true, -1);
	}

	public void Init()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		refFriendsButton = (RadioButton)((FrameworkElement)this).FindName("FriendsButton");
		refGlobalButton = (RadioButton)((FrameworkElement)this).FindName("GlobalButton");
		((ToggleButton)refFriendsButton).IsChecked = true;
		pageTypeFriends = true;
		MainViewModel.Instance.Leaderboard_Pos_Column_Width = 140;
		currentPage = -1;
		retryGlobal = false;
		pending = false;
		pending_Friends = false;
		pending_Page = 0;
		MainViewModel.Instance.Show_Leaderboard_OptOut = ConfigSettings.Settings_Leaderboard_OptOut;
		for (int i = 0; i < 10; i++)
		{
			SetLeaderboardRow(i, "", "", "", "", null);
		}
	}

	public static void CloseLeaderboard()
	{
		if (MainViewModel.Instance.Show_Leaderboard)
		{
			if ((BaseComponent)(object)Instance != (BaseComponent)null)
			{
				Instance.Init();
			}
			MainViewModel.Instance.Show_Leaderboard = false;
			Platform_Multiplayer.Instance.ClearSteamAvatarCache();
		}
	}

	public static void ChangeLeaderboard(string leaderboardName, string leaderboardTitle)
	{
		MainViewModel.Instance.LeaderboardTitle = " - " + leaderboardTitle;
		Instance.pending = false;
		Instance.pending_Friends = false;
		Instance.pending_Page = 0;
		Instance.UpdateLeaderboard(leaderboardName, Instance.pageTypeFriends, -1, force: false);
	}

	public bool UpdateLeaderboard(string leaderboardName, bool friendsOnly, int page, bool force = true)
	{
		if (LeaderboardName != leaderboardName || force)
		{
			currentPage = page;
			retryGlobal = false;
			LeaderboardName = leaderboardName;
			avatarCallbacks.Clear();
			return Platform_Leaderboards.GetScores(friendsOnly, currentPage, leaderboardName, delegate
			{
				if (pageTypeFriends || currentPage < 0)
				{
					int num = 0;
					if (currentPage < 0)
					{
						int ownPosition = Platform_Leaderboards.GetOwnPosition(leaderboardName, pageTypeFriends);
						if (ownPosition >= 0)
						{
							num = (currentPage = (ownPosition - 1) / 10);
						}
						else
						{
							currentPage = 0;
							if (!pageTypeFriends)
							{
								retryGlobal = true;
								return;
							}
						}
					}
					else
					{
						num = currentPage;
					}
					List<Platform_Leaderboards.LeaderboardEntry> entriesForPage = Platform_Leaderboards.GetEntriesForPage(leaderboardName, pageTypeFriends, num);
					for (int i = 0; i < 10; i++)
					{
						if (i < entriesForPage.Count)
						{
							ImageSource avatar = null;
							if (!ConfigSettings.Settings_Leaderboard_Images)
							{
								avatar = requestAvatar(i, entriesForPage[i].steamID);
							}
							if (pageTypeFriends)
							{
								SetLeaderboardRow(i, (num * 10 + i + 1).ToString(), "(" + entriesForPage[i].position + ")", entriesForPage[i].name, GameData.GetTimeString(entriesForPage[i].time), avatar);
							}
							else
							{
								SetLeaderboardRow(i, "", entriesForPage[i].position.ToString(), entriesForPage[i].name, GameData.GetTimeString(entriesForPage[i].time), avatar);
							}
						}
						else
						{
							SetLeaderboardRow(i, "", "", "", "", null);
						}
					}
				}
				else
				{
					int num2 = currentPage;
					List<Platform_Leaderboards.LeaderboardEntry> entriesForPage2;
					while (true)
					{
						entriesForPage2 = Platform_Leaderboards.GetEntriesForPage(leaderboardName, pageTypeFriends, num2);
						if (entriesForPage2.Count != 0 || num2 <= 0)
						{
							break;
						}
						num2--;
						currentPage--;
					}
					for (int j = 0; j < 10; j++)
					{
						if (j < entriesForPage2.Count)
						{
							ImageSource avatar2 = null;
							if (!ConfigSettings.Settings_Leaderboard_Images)
							{
								avatar2 = requestAvatar(j, entriesForPage2[j].steamID);
							}
							SetLeaderboardRow(j, "", entriesForPage2[j].position.ToString(), entriesForPage2[j].name, GameData.GetTimeString(entriesForPage2[j].time), avatar2);
						}
						else
						{
							SetLeaderboardRow(j, "", "", "", "", null);
						}
					}
				}
			});
		}
		return true;
	}

	public ImageSource requestAvatar(int _row, ulong _steamID)
	{
		ImageSource userAvatar = Platform_Multiplayer.Instance.GetUserAvatar(_steamID);
		if ((BaseComponent)(object)userAvatar != (BaseComponent)null)
		{
			return userAvatar;
		}
		Platform_Multiplayer.Instance.RequestUserAvatar(_steamID);
		avatarCallbacks.Enqueue(new AvatarCallback
		{
			row = _row,
			steamID = _steamID
		});
		return null;
	}

	public void Update()
	{
		if (retryGlobal)
		{
			retryGlobal = false;
			UpdateLeaderboard(LeaderboardName, friendsOnly: false, currentPage);
		}
		else if (pending && !Platform_Leaderboards.IsLeaderboardPending(LeaderboardName))
		{
			pageTypeFriends = pending_Friends;
			if (pending_Friends)
			{
				MainViewModel.Instance.Leaderboard_Pos_Column_Width = 140;
			}
			else
			{
				MainViewModel.Instance.Leaderboard_Pos_Column_Width = 140;
			}
			currentPage = pending_Page;
			UpdateLeaderboard(LeaderboardName, pending_Friends, pending_Page);
			pending = false;
		}
		if (avatarCallbacks.Count <= 0)
		{
			return;
		}
		AvatarCallback avatarCallback = avatarCallbacks.Peek();
		ImageSource userAvatar = Platform_Multiplayer.Instance.GetUserAvatar(avatarCallback.steamID);
		if ((BaseComponent)(object)userAvatar != (BaseComponent)null)
		{
			avatarCallbacks.Dequeue();
			if (!ConfigSettings.Settings_Leaderboard_Images)
			{
				SetLeaderboardRowAvatar(avatarCallback.row, userAvatar);
			}
		}
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "Exit":
			CloseLeaderboard();
			break;
		case "Friends":
			if (!pageTypeFriends)
			{
				if (!Platform_Leaderboards.IsLeaderboardPending(LeaderboardName))
				{
					pageTypeFriends = true;
					MainViewModel.Instance.Leaderboard_Pos_Column_Width = 140;
					currentPage = -1;
					UpdateLeaderboard(LeaderboardName, friendsOnly: true, -1);
				}
				else
				{
					pending_Friends = true;
					pending_Page = -1;
					pending = true;
				}
			}
			break;
		case "Global":
			if (pageTypeFriends)
			{
				if (!Platform_Leaderboards.IsLeaderboardPending(LeaderboardName))
				{
					pageTypeFriends = false;
					MainViewModel.Instance.Leaderboard_Pos_Column_Width = 140;
					currentPage = -1;
					UpdateLeaderboard(LeaderboardName, friendsOnly: false, -1);
				}
				else
				{
					pending_Friends = false;
					pending_Page = -1;
					pending = true;
				}
			}
			break;
		case "Top":
			if (!Platform_Leaderboards.IsLeaderboardPending(LeaderboardName))
			{
				currentPage = 0;
				UpdateLeaderboard(LeaderboardName, pageTypeFriends, currentPage);
			}
			else
			{
				pending_Friends = pageTypeFriends;
				pending_Page = 0;
				pending = true;
			}
			break;
		case "Up":
			if (currentPage <= 0)
			{
				break;
			}
			if (!Platform_Leaderboards.IsLeaderboardPending(LeaderboardName))
			{
				currentPage--;
				UpdateLeaderboard(LeaderboardName, pageTypeFriends, currentPage);
			}
			else if (pending)
			{
				if (pending_Page > 1)
				{
					pending_Page = currentPage - 1;
				}
			}
			else
			{
				pending_Friends = pageTypeFriends;
				pending_Page = currentPage - 1;
				pending = true;
			}
			break;
		case "Down":
			if (!Platform_Leaderboards.IsLeaderboardPending(LeaderboardName))
			{
				currentPage++;
				UpdateLeaderboard(LeaderboardName, pageTypeFriends, currentPage);
			}
			else if (pending)
			{
				pending_Page++;
			}
			else
			{
				pending_Friends = pageTypeFriends;
				pending_Page = currentPage + 1;
				pending = true;
			}
			break;
		}
	}

	public void SetLeaderboardRow(int row, string friendsPosition, string position, string name, string time, ImageSource avatar)
	{
		switch (row)
		{
		case 0:
			MainViewModel.Instance.Leaderboard_FPos_1 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_1 = position;
			MainViewModel.Instance.Leaderboard_Name_1 = name;
			MainViewModel.Instance.Leaderboard_Time_1 = time;
			MainViewModel.Instance.Leaderboard_Image_1 = avatar;
			break;
		case 1:
			MainViewModel.Instance.Leaderboard_FPos_2 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_2 = position;
			MainViewModel.Instance.Leaderboard_Name_2 = name;
			MainViewModel.Instance.Leaderboard_Time_2 = time;
			MainViewModel.Instance.Leaderboard_Image_2 = avatar;
			break;
		case 2:
			MainViewModel.Instance.Leaderboard_FPos_3 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_3 = position;
			MainViewModel.Instance.Leaderboard_Name_3 = name;
			MainViewModel.Instance.Leaderboard_Time_3 = time;
			MainViewModel.Instance.Leaderboard_Image_3 = avatar;
			break;
		case 3:
			MainViewModel.Instance.Leaderboard_FPos_4 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_4 = position;
			MainViewModel.Instance.Leaderboard_Name_4 = name;
			MainViewModel.Instance.Leaderboard_Time_4 = time;
			MainViewModel.Instance.Leaderboard_Image_4 = avatar;
			break;
		case 4:
			MainViewModel.Instance.Leaderboard_FPos_5 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_5 = position;
			MainViewModel.Instance.Leaderboard_Name_5 = name;
			MainViewModel.Instance.Leaderboard_Time_5 = time;
			MainViewModel.Instance.Leaderboard_Image_5 = avatar;
			break;
		case 5:
			MainViewModel.Instance.Leaderboard_FPos_6 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_6 = position;
			MainViewModel.Instance.Leaderboard_Name_6 = name;
			MainViewModel.Instance.Leaderboard_Time_6 = time;
			MainViewModel.Instance.Leaderboard_Image_6 = avatar;
			break;
		case 6:
			MainViewModel.Instance.Leaderboard_FPos_7 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_7 = position;
			MainViewModel.Instance.Leaderboard_Name_7 = name;
			MainViewModel.Instance.Leaderboard_Time_7 = time;
			MainViewModel.Instance.Leaderboard_Image_7 = avatar;
			break;
		case 7:
			MainViewModel.Instance.Leaderboard_FPos_8 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_8 = position;
			MainViewModel.Instance.Leaderboard_Name_8 = name;
			MainViewModel.Instance.Leaderboard_Time_8 = time;
			MainViewModel.Instance.Leaderboard_Image_8 = avatar;
			break;
		case 8:
			MainViewModel.Instance.Leaderboard_FPos_9 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_9 = position;
			MainViewModel.Instance.Leaderboard_Name_9 = name;
			MainViewModel.Instance.Leaderboard_Time_9 = time;
			MainViewModel.Instance.Leaderboard_Image_9 = avatar;
			break;
		case 9:
			MainViewModel.Instance.Leaderboard_FPos_10 = friendsPosition;
			MainViewModel.Instance.Leaderboard_Pos_10 = position;
			MainViewModel.Instance.Leaderboard_Name_10 = name;
			MainViewModel.Instance.Leaderboard_Time_10 = time;
			MainViewModel.Instance.Leaderboard_Image_10 = avatar;
			break;
		}
	}

	public void SetLeaderboardRowAvatar(int row, ImageSource avatar)
	{
		switch (row)
		{
		case 0:
			MainViewModel.Instance.Leaderboard_Image_1 = avatar;
			break;
		case 1:
			MainViewModel.Instance.Leaderboard_Image_2 = avatar;
			break;
		case 2:
			MainViewModel.Instance.Leaderboard_Image_3 = avatar;
			break;
		case 3:
			MainViewModel.Instance.Leaderboard_Image_4 = avatar;
			break;
		case 4:
			MainViewModel.Instance.Leaderboard_Image_5 = avatar;
			break;
		case 5:
			MainViewModel.Instance.Leaderboard_Image_6 = avatar;
			break;
		case 6:
			MainViewModel.Instance.Leaderboard_Image_7 = avatar;
			break;
		case 7:
			MainViewModel.Instance.Leaderboard_Image_8 = avatar;
			break;
		case 8:
			MainViewModel.Instance.Leaderboard_Image_9 = avatar;
			break;
		case 9:
			MainViewModel.Instance.Leaderboard_Image_10 = avatar;
			break;
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Leaderboard.xaml");
	}
}
