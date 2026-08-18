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

		private string _name;

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
		private string s_leaderboardName = "";

		private const ELeaderboardUploadScoreMethod s_leaderboardMethod = ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest;

		private SteamLeaderboard_t m_currentLeaderboard;

		private bool m_initialized;

		public bool m_gettingScores;

		private bool m_gettingScoresForFriends;

		private int m_gettingPage = -1;

		public int maxPages = -1;

		private CallResult<LeaderboardFindResult_t> m_findResult = new CallResult<LeaderboardFindResult_t>();

		private CallResult<LeaderboardScoreUploaded_t> m_uploadResult = new CallResult<LeaderboardScoreUploaded_t>();

		private CallResult<LeaderboardScoresDownloaded_t> m_downloadResult = new CallResult<LeaderboardScoresDownloaded_t>();

		public Dictionary<int, LeaderboardEntry> m_leaderData = new Dictionary<int, LeaderboardEntry>();

		public List<LeaderboardEntry> m_friendsData = new List<LeaderboardEntry>();

		private DateTime m_UploadStartAttempt;

		private DateTime m_DownloadStartAttempt;

		private Action<LeaderboardEntry[], bool> m_getScoresDelegate;

		public void InitLeaderboard(string leaderboardName)
		{
			if (SteamManager.Initialized)
			{
				m_initialized = false;
				s_leaderboardName = leaderboardName;
				SteamAPICall_t hAPICall = SteamUserStats.FindLeaderboard(s_leaderboardName);
				m_findResult.Set(hAPICall, OnLeaderboardFindResult);
			}
		}

		private void OnLeaderboardFindResult(LeaderboardFindResult_t pCallback, bool failure)
		{
			m_currentLeaderboard = pCallback.m_hSteamLeaderboard;
			m_initialized = true;
		}

		public void UpdateScore(int score, bool initialCall = false)
		{
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
				SteamAPICall_t hAPICall = SteamUserStats.UploadLeaderboardScore(m_currentLeaderboard, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, score, null, 0);
				m_uploadResult.Set(hAPICall, OnLeaderboardUploadResult);
			}
		}

		private void OnLeaderboardUploadResult(LeaderboardScoreUploaded_t pCallback, bool failure)
		{
			if (pCallback.m_bScoreChanged != 0)
			{
				m_friendsData.Clear();
				m_leaderData.Clear();
			}
		}

		public bool GetScores(Action<LeaderboardEntry[], bool> getScoresDelegate, bool friendsOnly, int page, bool initialCall = false)
		{
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
				SteamAPICall_t hAPICall;
				if (m_gettingScoresForFriends)
				{
					hAPICall = SteamUserStats.DownloadLeaderboardEntries(m_currentLeaderboard, ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends, -4, 5);
				}
				else if (page < 0)
				{
					hAPICall = SteamUserStats.DownloadLeaderboardEntries(m_currentLeaderboard, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, -10, 10);
				}
				else
				{
					int num = page * 10;
					hAPICall = SteamUserStats.DownloadLeaderboardEntries(m_currentLeaderboard, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, num + 1, num + 10);
				}
				m_downloadResult.Set(hAPICall, OnLeaderboardDownloadResult);
			}
			return true;
		}

		private void OnLeaderboardDownloadResult(LeaderboardScoresDownloaded_t pCallback, bool failure)
		{
			if (pCallback.m_cEntryCount == 0 && !m_gettingScoresForFriends && m_gettingPage >= 0)
			{
				maxPages = m_gettingPage - 1;
			}
			LeaderboardEntry[] array = new LeaderboardEntry[pCallback.m_cEntryCount];
			for (int i = 0; i < pCallback.m_cEntryCount; i++)
			{
				SteamUserStats.GetDownloadedLeaderboardEntry(pCallback.m_hSteamLeaderboardEntries, i, out var pLeaderboardEntry, null, 0);
				LeaderboardEntry leaderboardEntry = new LeaderboardEntry();
				leaderboardEntry.steamID = pLeaderboardEntry.m_steamIDUser.m_SteamID;
				leaderboardEntry.name = SteamFriends.GetFriendPersonaName(pLeaderboardEntry.m_steamIDUser);
				leaderboardEntry.position = pLeaderboardEntry.m_nGlobalRank;
				leaderboardEntry.time = pLeaderboardEntry.m_nScore;
				array[i] = leaderboardEntry;
			}
			m_gettingScores = false;
			if (m_getScoresDelegate != null)
			{
				m_getScoresDelegate(array, m_gettingScoresForFriends);
			}
		}
	}

	private static Dictionary<string, Leaderboard> leaderboards = new Dictionary<string, Leaderboard>();

	public static string GetSandsLeaderboardName(int trailID, int missionID)
	{
		return "Sands" + trailID + "." + (missionID + 1);
	}

	private static Leaderboard InitLeaderboard(string leaderboardName)
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
