using System;
using System.Collections.Generic;
using Steamworks;

public class Platform_Leaderboards
{
	public class LeaderboardEntry : IComparable<LeaderboardEntry>
	{
		public ulong steamID;

		public int position;

		public int time;

		public string _name;

		public string name
		{
			get
			{
				if (ConfigSettings.Settings_Leaderboard_Names)
				{
					return "Lord Crusader " + position;
				}
				return _name;
			}
			set
			{
				_name = value;
			}
		}

		public int CompareTo(LeaderboardEntry that)
		{
			return position.CompareTo(that.position);
		}
	}

	public class Leaderboard
	{
		public string s_leaderboardName = "";

		public const ELeaderboardUploadScoreMethod s_leaderboardMethod = (ELeaderboardUploadScoreMethod)1;

		public SteamLeaderboard_t m_currentLeaderboard;

		public bool m_initialized;

		public bool m_gettingScores;

		public bool m_gettingScoresForFriends;

		public int m_gettingPage = -1;

		public int maxPages = -1;

		public CallResult<LeaderboardFindResult_t> m_findResult = new CallResult<LeaderboardFindResult_t>((APIDispatchDelegate<LeaderboardFindResult_t>)null);

		public CallResult<LeaderboardScoreUploaded_t> m_uploadResult = new CallResult<LeaderboardScoreUploaded_t>((APIDispatchDelegate<LeaderboardScoreUploaded_t>)null);

		public CallResult<LeaderboardScoresDownloaded_t> m_downloadResult = new CallResult<LeaderboardScoresDownloaded_t>((APIDispatchDelegate<LeaderboardScoresDownloaded_t>)null);

		public Dictionary<int, LeaderboardEntry> m_leaderData = new Dictionary<int, LeaderboardEntry>();

		public List<LeaderboardEntry> m_friendsData = new List<LeaderboardEntry>();

		public DateTime m_UploadStartAttempt;

		public DateTime m_DownloadStartAttempt;

		public Action<LeaderboardEntry[], bool> m_getScoresDelegate;

		public void InitLeaderboard(string leaderboardName)
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			if (SteamManager.Initialized)
			{
				m_initialized = false;
				s_leaderboardName = leaderboardName;
				SteamAPICall_t val = SteamUserStats.FindLeaderboard(s_leaderboardName);
				m_findResult.Set(val, (APIDispatchDelegate<LeaderboardFindResult_t>)OnLeaderboardFindResult);
			}
		}

		public void OnLeaderboardFindResult(LeaderboardFindResult_t pCallback, bool failure)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			m_currentLeaderboard = pCallback.m_hSteamLeaderboard;
			m_initialized = true;
		}

		public void UpdateScore(int score, bool initialCall = false)
		{
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			if (!m_initialized)
			{
				bool flag = true;
				if (initialCall)
				{
					m_UploadStartAttempt = DateTime.UtcNow;
				}
				else if ((DateTime.UtcNow - m_UploadStartAttempt).TotalSeconds > 10.0)
				{
					flag = false;
				}
				if (flag)
				{
					Director.instance.GenericDelayCoroutine(delegate
					{
						UpdateScore(score);
					}, 2f);
				}
			}
			else
			{
				SteamAPICall_t val = SteamUserStats.UploadLeaderboardScore(m_currentLeaderboard, (ELeaderboardUploadScoreMethod)1, score, (int[])null, 0);
				m_uploadResult.Set(val, (APIDispatchDelegate<LeaderboardScoreUploaded_t>)OnLeaderboardUploadResult);
			}
		}

		public void OnLeaderboardUploadResult(LeaderboardScoreUploaded_t pCallback, bool failure)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			if (pCallback.m_bScoreChanged != 0)
			{
				m_friendsData.Clear();
				m_leaderData.Clear();
			}
		}

		public bool GetScores(Action<LeaderboardEntry[], bool> getScoresDelegate, bool friendsOnly, int page, bool initialCall = false)
		{
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			if (m_gettingScores && initialCall)
			{
				return false;
			}
			m_gettingScoresForFriends = friendsOnly;
			m_gettingPage = page;
			if (initialCall)
			{
				m_getScoresDelegate = getScoresDelegate;
				m_gettingScores = true;
			}
			if (!m_initialized)
			{
				bool flag = true;
				if (initialCall)
				{
					m_DownloadStartAttempt = DateTime.UtcNow;
				}
				else if ((DateTime.UtcNow - m_DownloadStartAttempt).TotalSeconds > 10.0)
				{
					flag = false;
					m_gettingScores = false;
					LeaderboardEntry[] arg = new LeaderboardEntry[0];
					if (m_getScoresDelegate != null)
					{
						m_getScoresDelegate(arg, m_gettingScoresForFriends);
					}
				}
				if (flag)
				{
					Director.instance.GenericDelayCoroutine(delegate
					{
						GetScores(null, m_gettingScoresForFriends, m_gettingPage);
					}, 2f);
				}
			}
			else
			{
				SteamAPICall_t val;
				if (m_gettingScoresForFriends)
				{
					val = SteamUserStats.DownloadLeaderboardEntries(m_currentLeaderboard, (ELeaderboardDataRequest)2, -4, 5);
				}
				else if (page < 0)
				{
					val = SteamUserStats.DownloadLeaderboardEntries(m_currentLeaderboard, (ELeaderboardDataRequest)1, -10, 10);
				}
				else
				{
					int num = page * 10;
					val = SteamUserStats.DownloadLeaderboardEntries(m_currentLeaderboard, (ELeaderboardDataRequest)0, num + 1, num + 10);
				}
				m_downloadResult.Set(val, (APIDispatchDelegate<LeaderboardScoresDownloaded_t>)OnLeaderboardDownloadResult);
			}
			return true;
		}

		public void OnLeaderboardDownloadResult(LeaderboardScoresDownloaded_t pCallback, bool failure)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			if (pCallback.m_cEntryCount == 0 && !m_gettingScoresForFriends && m_gettingPage >= 0)
			{
				maxPages = m_gettingPage - 1;
			}
			LeaderboardEntry[] array = new LeaderboardEntry[pCallback.m_cEntryCount];
			LeaderboardEntry_t val = default(LeaderboardEntry_t);
			for (int i = 0; i < pCallback.m_cEntryCount; i++)
			{
				SteamUserStats.GetDownloadedLeaderboardEntry(pCallback.m_hSteamLeaderboardEntries, i, ref val, (int[])null, 0);
				LeaderboardEntry leaderboardEntry = new LeaderboardEntry();
				leaderboardEntry.steamID = val.m_steamIDUser.m_SteamID;
				leaderboardEntry.name = SteamFriends.GetFriendPersonaName(val.m_steamIDUser);
				leaderboardEntry.position = val.m_nGlobalRank;
				leaderboardEntry.time = val.m_nScore;
				array[i] = leaderboardEntry;
			}
			m_gettingScores = false;
			if (m_getScoresDelegate != null)
			{
				m_getScoresDelegate(array, m_gettingScoresForFriends);
			}
		}
	}

	public static Dictionary<string, Leaderboard> leaderboards = new Dictionary<string, Leaderboard>();

	public static string GetSandsLeaderboardName(int trailID, int missionID)
	{
		return "Sands" + trailID + "." + (missionID + 1);
	}

	public static Leaderboard InitLeaderboard(string leaderboardName)
	{
		if (leaderboards.ContainsKey(leaderboardName))
		{
			return leaderboards[leaderboardName];
		}
		Leaderboard leaderboard = new Leaderboard();
		leaderboard.InitLeaderboard(leaderboardName);
		leaderboards[leaderboardName] = leaderboard;
		return leaderboard;
	}

	public static void UploadScore(string leaderboardName, int score)
	{
		if (SteamManager.Initialized && !ConfigSettings.Settings_Leaderboard_OptOut)
		{
			InitLeaderboard(leaderboardName).UpdateScore(score, initialCall: true);
		}
	}

	public static bool GetScores(bool friendsOnly, int page, string leaderboardName, Action scoresUpdatedDelegate)
	{
		if (SteamManager.Initialized)
		{
			Leaderboard leaderboard = InitLeaderboard(leaderboardName);
			if (friendsOnly && leaderboard.m_friendsData.Count > 0)
			{
				scoresUpdatedDelegate();
				return true;
			}
			if (!friendsOnly)
			{
				if (page < 0)
				{
					if (GetOwnPosition(leaderboardName, friendsOnly: false) >= 0)
					{
						scoresUpdatedDelegate();
						return true;
					}
				}
				else if (leaderboard.m_leaderData.ContainsKey(page * 10 + 1))
				{
					scoresUpdatedDelegate();
					return true;
				}
			}
			return leaderboard.GetScores(delegate(LeaderboardEntry[] x, bool friends)
			{
				if (friends)
				{
					leaderboard.m_friendsData.Clear();
					leaderboard.m_friendsData.AddRange(x);
				}
				else
				{
					foreach (LeaderboardEntry leaderboardEntry in x)
					{
						leaderboard.m_leaderData[leaderboardEntry.position] = leaderboardEntry;
					}
				}
				scoresUpdatedDelegate();
			}, friendsOnly, page, initialCall: true);
		}
		return true;
	}

	public static bool IsLeaderboardPending(string leaderboardName)
	{
		if (SteamManager.Initialized)
		{
			return InitLeaderboard(leaderboardName).m_gettingScores;
		}
		return false;
	}

	public static int GetMaxPages(string leaderboardName)
	{
		if (SteamManager.Initialized)
		{
			return InitLeaderboard(leaderboardName).maxPages;
		}
		return -1;
	}

	public static int GetOwnPosition(string leaderboardName, bool friendsOnly)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if (SteamManager.Initialized)
		{
			Leaderboard leaderboard = InitLeaderboard(leaderboardName);
			ulong steamID = SteamUser.GetSteamID().m_SteamID;
			if (friendsOnly)
			{
				int num = 0;
				foreach (LeaderboardEntry friendsDatum in leaderboard.m_friendsData)
				{
					if (friendsDatum.steamID == steamID)
					{
						return num;
					}
					num++;
				}
			}
			else
			{
				foreach (KeyValuePair<int, LeaderboardEntry> leaderDatum in leaderboard.m_leaderData)
				{
					if (leaderDatum.Value.steamID == steamID)
					{
						return leaderDatum.Value.position;
					}
				}
			}
		}
		return -1;
	}

	public static List<LeaderboardEntry> GetEntriesForPage(string leaderboardName, bool friendsOnly, int page)
	{
		List<LeaderboardEntry> list = new List<LeaderboardEntry>();
		if (SteamManager.Initialized)
		{
			Leaderboard leaderboard = InitLeaderboard(leaderboardName);
			if (friendsOnly)
			{
				int num = page * 10;
				for (int i = 0; i < 10; i++)
				{
					if (num + i < leaderboard.m_friendsData.Count)
					{
						list.Add(leaderboard.m_friendsData[num + i]);
					}
				}
			}
			else
			{
				int num2 = page * 10 + 1;
				int num3 = (page + 1) * 10 + 1;
				foreach (KeyValuePair<int, LeaderboardEntry> leaderDatum in leaderboard.m_leaderData)
				{
					if (leaderDatum.Value.position >= num2 && leaderDatum.Value.position < num3)
					{
						list.Add(leaderDatum.Value);
					}
				}
			}
			if (list.Count > 1)
			{
				list.Sort();
			}
		}
		return list;
	}
}
