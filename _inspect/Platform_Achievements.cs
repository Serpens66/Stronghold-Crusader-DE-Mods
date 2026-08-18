using System;
using System.Collections.Generic;
using CrusaderDE;
using Steamworks;
using UnityEngine;

public class Platform_Achievements
{
	public static readonly Platform_Achievements instance;

	public const int VersionNumber = 4;

	public int STAT_Units;

	public int STAT_Lions;

	public int STAT_DairyFarms;

	public int STAT_Beat_Lord_Rat;

	public int STAT_Beat_Lord_Snake;

	public int STAT_Beat_Lord_Pig;

	public int STAT_Beat_Lord_Wolf;

	public int STAT_Beat_Lord_Saladin;

	public int STAT_Beat_Lord_Caliph;

	public int STAT_Beat_Lord_Sultan;

	public int STAT_Beat_Lord_Richard;

	public int STAT_Beat_Lord_Frederick;

	public int STAT_Beat_Lord_Phillip;

	public int STAT_Beat_Lord_Wazir;

	public int STAT_Beat_Lord_Emir;

	public int STAT_Beat_Lord_Nizar;

	public int STAT_Beat_Lord_Sheriff;

	public int STAT_Beat_Lord_Marshall;

	public int STAT_Beat_Lord_Abbot;

	public int STAT_Beat_Lord_Jewel;

	public int STAT_Beat_Lord_Sentinel;

	public int STAT_Beat_Lord_Nomad;

	public int STAT_Beat_Lord_Kahin;

	public DateTime StatsChanged = DateTime.MinValue;

	public static Platform_Achievements Instance => instance;

	static Platform_Achievements()
	{
		instance = new Platform_Achievements();
	}

	public Dictionary<Enums.Achievements, int> LoadAchievements()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<Enums.Achievements, int> dictionary = new Dictionary<Enums.Achievements, int>();
		if (SteamManager.Initialized)
		{
			SteamUserStats.RequestUserStats(SteamUser.GetSteamID());
			int num = default(int);
			if (!SteamUserStats.GetStat("stat_VersionNumber", ref num))
			{
				SteamUserStats.SetStat("stat_VersionNumber", 4);
			}
			SteamUserStats.GetStat("stat_Units_Killed", ref STAT_Units);
			SteamUserStats.GetStat("stat_Lions_Killed", ref STAT_Lions);
			SteamUserStats.GetStat("stat_Dairy_Farms", ref STAT_DairyFarms);
			SteamUserStats.GetStat("stat_Beat_Lord_Rat", ref STAT_Beat_Lord_Rat);
			SteamUserStats.GetStat("stat_Beat_Lord_Snake", ref STAT_Beat_Lord_Snake);
			SteamUserStats.GetStat("stat_Beat_Lord_Pig", ref STAT_Beat_Lord_Pig);
			SteamUserStats.GetStat("stat_Beat_Lord_Wolf", ref STAT_Beat_Lord_Wolf);
			SteamUserStats.GetStat("stat_Beat_Lord_Saladin", ref STAT_Beat_Lord_Saladin);
			SteamUserStats.GetStat("stat_Beat_Lord_Caliph", ref STAT_Beat_Lord_Caliph);
			SteamUserStats.GetStat("stat_Beat_Lord_Sultan", ref STAT_Beat_Lord_Sultan);
			SteamUserStats.GetStat("stat_Beat_Lord_Richard", ref STAT_Beat_Lord_Richard);
			SteamUserStats.GetStat("stat_Beat_Lord_Frederick", ref STAT_Beat_Lord_Frederick);
			SteamUserStats.GetStat("stat_Beat_Lord_Phillip", ref STAT_Beat_Lord_Phillip);
			SteamUserStats.GetStat("stat_Beat_Lord_Wazir", ref STAT_Beat_Lord_Wazir);
			SteamUserStats.GetStat("stat_Beat_Lord_Emir", ref STAT_Beat_Lord_Emir);
			SteamUserStats.GetStat("stat_Beat_Lord_Nizar", ref STAT_Beat_Lord_Nizar);
			SteamUserStats.GetStat("stat_Beat_Lord_Sheriff", ref STAT_Beat_Lord_Sheriff);
			SteamUserStats.GetStat("stat_Beat_Lord_Marshall", ref STAT_Beat_Lord_Marshall);
			SteamUserStats.GetStat("stat_Beat_Lord_Abbot", ref STAT_Beat_Lord_Abbot);
			SteamUserStats.GetStat("stat_Beat_Lord_Jewel", ref STAT_Beat_Lord_Jewel);
			SteamUserStats.GetStat("stat_Beat_Lord_Sentinel", ref STAT_Beat_Lord_Sentinel);
			SteamUserStats.GetStat("stat_Beat_Lord_Nomad", ref STAT_Beat_Lord_Nomad);
			SteamUserStats.GetStat("stat_Beat_Lord_Kahin", ref STAT_Beat_Lord_Kahin);
			dictionary[Enums.Achievements.Complete_Tutorial] = getAchievementValue("ACHIEVEMENT_Tut_Complete");
			dictionary[Enums.Achievements.Complete_Campaign_1] = getAchievementValue("ACHIEVEMENT_Campaign_1");
			dictionary[Enums.Achievements.Complete_Campaign_2] = getAchievementValue("ACHIEVEMENT_Campaign_2");
			dictionary[Enums.Achievements.Complete_Campaign_3] = getAchievementValue("ACHIEVEMENT_Campaign_3");
			dictionary[Enums.Achievements.Complete_Campaign_4] = getAchievementValue("ACHIEVEMENT_Campaign_4");
			dictionary[Enums.Achievements.Complete_Campaign_5] = getAchievementValue("ACHIEVEMENT_Campaign_5");
			dictionary[Enums.Achievements.Complete_Campaign_6] = getAchievementValue("ACHIEVEMENT_Campaign_6");
			dictionary[Enums.Achievements.Complete_Campaign_7] = getAchievementValue("ACHIEVEMENT_Campaign_7");
			dictionary[Enums.Achievements.Complete_FirstEdition_Trail] = getAchievementValue("ACHIEVEMENT_Trail_1");
			dictionary[Enums.Achievements.Complete_Warchest_Trail] = getAchievementValue("ACHIEVEMENT_Trail_2");
			dictionary[Enums.Achievements.Complete_Extreme_Trail] = getAchievementValue("ACHIEVEMENT_Trail_3");
			dictionary[Enums.Achievements.Complete_Sands_Warrior] = getAchievementValue("ACHIEVEMENT_Sands_Warrior");
			dictionary[Enums.Achievements.Complete_Sands_Champion] = getAchievementValue("ACHIEVEMENT_Sands_Champion");
			dictionary[Enums.Achievements.Complete_Sands_Prince] = getAchievementValue("ACHIEVEMENT_Sands_Prince");
			dictionary[Enums.Achievements.Complete_Sands_Trail_1] = getAchievementValue("ACHIEVEMENT_Sands_Trail_1");
			dictionary[Enums.Achievements.Complete_Sands_Trail_2] = getAchievementValue("ACHIEVEMENT_Sands_Trail_2");
			dictionary[Enums.Achievements.Complete_Sands_Trail_3] = getAchievementValue("ACHIEVEMENT_Sands_Trail_3");
			dictionary[Enums.Achievements.Complete_Sands_Trail_4] = getAchievementValue("ACHIEVEMENT_Sands_Trail_4");
			dictionary[Enums.Achievements.Complete_Sands_Trail_5] = getAchievementValue("ACHIEVEMENT_Sands_Trail_5");
			dictionary[Enums.Achievements.Complete_Sands_Trail_6] = getAchievementValue("ACHIEVEMENT_Sands_Trail_6");
			dictionary[Enums.Achievements.Complete_Sands_Trail_7] = getAchievementValue("ACHIEVEMENT_Sands_Trail_7");
			dictionary[Enums.Achievements.Win_Skirmish_Game] = getAchievementValue("ACHIEVEMENT_Win_Skirmish_Game");
			dictionary[Enums.Achievements.Win_Skirmish_Game_vs_7] = getAchievementValue("ACHIEVEMENT_Win_Skirmish_Game_vs_7");
			dictionary[Enums.Achievements.Win_Skirmish_Game_vs_Team_of_7] = getAchievementValue("ACHIEVEMENT_Win_Skirmish_Game_vs_Team_of_7");
			dictionary[Enums.Achievements.Win_Skirmish_Game_vs_New_Lords] = getAchievementValue("ACHIEVEMENT_Win_Skirmish_Game_vs_New_Lords");
			dictionary[Enums.Achievements.Skirmish_Beating_All_Lords] = getAchievementValue("ACHIEVEMENT_Skirmish_Beating_All_Lords");
			dictionary[Enums.Achievements.Win_Skirmish_No_Ranged] = getAchievementValue("ACHIEVEMENT_Win_Skirmish_No_Ranged");
			dictionary[Enums.Achievements.Win_Skirmish_All_Ranged] = getAchievementValue("ACHIEVEMENT_Win_Skirmish_All_Ranged");
			dictionary[Enums.Achievements.Map_Uploaded_To_Workshop] = getAchievementValue("ACHIEVEMENT_Upload_Map");
			dictionary[Enums.Achievements.Scribe_Unlock] = getAchievementValue("ACHIEVEMENT_Scribe_Unlock");
			dictionary[Enums.Achievements.Store_1000_Food] = getAchievementValue("ACHIEVEMENT_Store_Food_1000");
			dictionary[Enums.Achievements.Store_10000_Wood] = getAchievementValue("ACHIEVEMENT_Store_Wood_10000");
			dictionary[Enums.Achievements.Store_1000_Weapons] = getAchievementValue("ACHIEVEMENT_Store_Weapons_1000");
			dictionary[Enums.Achievements.Amass_10000_Gold] = getAchievementValue("ACHIEVEMENT_Amass_Gold_10000");
			dictionary[Enums.Achievements.Population_300] = getAchievementValue("ACHIEVEMENT_Population_300");
			dictionary[Enums.Achievements.Place_Dairy_Farms] = getAchievementValue("ACHIEVEMENT_Dairy_Farms", STAT_DairyFarms);
			dictionary[Enums.Achievements.Kill_1000_Lions] = getAchievementValue("ACHIEVEMENT_Kill_1000_Lions", STAT_Lions);
			dictionary[Enums.Achievements.Kill_Units_10k] = getAchievementValue("ACHIEVEMENT_Kill_10k_Units", STAT_Units);
			dictionary[Enums.Achievements.Kill_Units_100k] = getAchievementValue("ACHIEVEMENT_Kill_100k_Units", STAT_Units);
			if (dictionary[Enums.Achievements.Scribe_Unlock] < 0)
			{
				FrontendMenus.newsletterSignUp = true;
			}
			else if (ConfigSettings.Settings_NewsletterEmail.Length > 0)
			{
				Director.instance.SignupNewsletter(ConfigSettings.Settings_NewsletterEmail, delegate
				{
					FrontendMenus.newsletterSignUp = true;
				}, showRequester: false, checkCall: true);
			}
			else
			{
				ConfigSettings.validateLordType();
			}
		}
		return dictionary;
	}

	public int getAchievementValue(string name)
	{
		bool flag = default(bool);
		if (SteamUserStats.GetAchievement(name, ref flag))
		{
			if (flag)
			{
				return -1;
			}
		}
		else
		{
			Debug.Log((object)("Unknown Achievement on Steam : " + name));
		}
		return 0;
	}

	public int getAchievementValue(string name, int statValue)
	{
		bool flag = default(bool);
		if (SteamUserStats.GetAchievement(name, ref flag))
		{
			if (flag)
			{
				return -1;
			}
			return statValue;
		}
		Debug.Log((object)("Unknown Achievement on Steam : " + name));
		return 0;
	}

	public void monitorStats()
	{
		if (StatsChanged != DateTime.MinValue && StatsChanged < DateTime.UtcNow)
		{
			StatsChanged = DateTime.MinValue;
			SteamUserStats.StoreStats();
		}
	}

	public void addStat(Enums.AchievementStat stat, int value)
	{
		if (!SteamManager.Initialized)
		{
			return;
		}
		switch (stat)
		{
		case Enums.AchievementStat.UnitsKilled:
			STAT_Units += value;
			SteamUserStats.SetStat("stat_Units_Killed", STAT_Units);
			if (StatsChanged == DateTime.MinValue)
			{
				StatsChanged = DateTime.UtcNow.AddSeconds(5.0);
			}
			break;
		case Enums.AchievementStat.LionsKilled:
			STAT_Lions += value;
			SteamUserStats.SetStat("stat_Lions_Killed", STAT_Lions);
			if (StatsChanged == DateTime.MinValue)
			{
				StatsChanged = DateTime.UtcNow.AddSeconds(5.0);
			}
			break;
		case Enums.AchievementStat.DairyFarms:
			STAT_DairyFarms += value;
			SteamUserStats.SetStat("stat_Dairy_Farms", STAT_DairyFarms);
			if (StatsChanged == DateTime.MinValue)
			{
				StatsChanged = DateTime.UtcNow.AddSeconds(5.0);
			}
			break;
		}
	}

	public void setLordKilledStat(int lordType)
	{
		if (SteamManager.Initialized)
		{
			switch (lordType)
			{
			case 1:
				STAT_Beat_Lord_Rat = 1;
				SteamUserStats.SetStat("stat_Beat_Lord_Rat", STAT_Beat_Lord_Rat);
				SteamUserStats.StoreStats();
				break;
			case 2:
				STAT_Beat_Lord_Snake = 1;
				SteamUserStats.SetStat("stat_beat_lord_snake", STAT_Beat_Lord_Snake);
				SteamUserStats.StoreStats();
				break;
			case 3:
				STAT_Beat_Lord_Pig = 1;
				SteamUserStats.SetStat("stat_beat_lord_pig", STAT_Beat_Lord_Pig);
				SteamUserStats.StoreStats();
				break;
			case 4:
				STAT_Beat_Lord_Wolf = 1;
				SteamUserStats.SetStat("stat_Beat_Lord_Wolf", STAT_Beat_Lord_Wolf);
				SteamUserStats.StoreStats();
				break;
			case 5:
				STAT_Beat_Lord_Saladin = 1;
				SteamUserStats.SetStat("stat_beat_lord_saladin", STAT_Beat_Lord_Saladin);
				SteamUserStats.StoreStats();
				break;
			case 6:
				STAT_Beat_Lord_Caliph = 1;
				SteamUserStats.SetStat("stat_beat_lord_caliph", STAT_Beat_Lord_Caliph);
				SteamUserStats.StoreStats();
				break;
			case 7:
				STAT_Beat_Lord_Sultan = 1;
				SteamUserStats.SetStat("stat_beat_lord_sultan", STAT_Beat_Lord_Sultan);
				SteamUserStats.StoreStats();
				break;
			case 8:
				STAT_Beat_Lord_Richard = 1;
				SteamUserStats.SetStat("stat_beat_lord_richard", STAT_Beat_Lord_Richard);
				SteamUserStats.StoreStats();
				break;
			case 9:
				STAT_Beat_Lord_Frederick = 1;
				SteamUserStats.SetStat("stat_beat_lord_frederick", STAT_Beat_Lord_Frederick);
				SteamUserStats.StoreStats();
				break;
			case 10:
				STAT_Beat_Lord_Phillip = 1;
				SteamUserStats.SetStat("stat_beat_lord_phillip", STAT_Beat_Lord_Phillip);
				SteamUserStats.StoreStats();
				break;
			case 11:
				STAT_Beat_Lord_Wazir = 1;
				SteamUserStats.SetStat("stat_beat_lord_wazir", STAT_Beat_Lord_Wazir);
				SteamUserStats.StoreStats();
				break;
			case 12:
				STAT_Beat_Lord_Emir = 1;
				SteamUserStats.SetStat("stat_beat_lord_emir", STAT_Beat_Lord_Emir);
				SteamUserStats.StoreStats();
				break;
			case 13:
				STAT_Beat_Lord_Nizar = 1;
				SteamUserStats.SetStat("stat_beat_lord_nizar", STAT_Beat_Lord_Nizar);
				SteamUserStats.StoreStats();
				break;
			case 14:
				STAT_Beat_Lord_Sheriff = 1;
				SteamUserStats.SetStat("stat_beat_lord_sheriff", STAT_Beat_Lord_Sheriff);
				SteamUserStats.StoreStats();
				break;
			case 15:
				STAT_Beat_Lord_Marshall = 1;
				SteamUserStats.SetStat("stat_beat_lord_marshall", STAT_Beat_Lord_Marshall);
				SteamUserStats.StoreStats();
				break;
			case 16:
				STAT_Beat_Lord_Abbot = 1;
				SteamUserStats.SetStat("stat_beat_lord_abbot", STAT_Beat_Lord_Abbot);
				SteamUserStats.StoreStats();
				break;
			case 17:
				STAT_Beat_Lord_Jewel = 1;
				SteamUserStats.SetStat("stat_beat_lord_jewel", STAT_Beat_Lord_Jewel);
				SteamUserStats.StoreStats();
				break;
			case 18:
				STAT_Beat_Lord_Sentinel = 1;
				SteamUserStats.SetStat("stat_beat_lord_sentinel", STAT_Beat_Lord_Sentinel);
				SteamUserStats.StoreStats();
				break;
			case 19:
				STAT_Beat_Lord_Nomad = 1;
				SteamUserStats.SetStat("stat_beat_lord_nomad", STAT_Beat_Lord_Nomad);
				SteamUserStats.StoreStats();
				break;
			case 20:
				STAT_Beat_Lord_Kahin = 1;
				SteamUserStats.SetStat("stat_beat_lord_kahin", STAT_Beat_Lord_Kahin);
				SteamUserStats.StoreStats();
				break;
			}
			if (STAT_Beat_Lord_Rat > 0 && STAT_Beat_Lord_Snake > 0 && STAT_Beat_Lord_Pig > 0 && STAT_Beat_Lord_Wolf > 0 && STAT_Beat_Lord_Saladin > 0 && STAT_Beat_Lord_Caliph > 0 && STAT_Beat_Lord_Sultan > 0 && STAT_Beat_Lord_Richard > 0 && STAT_Beat_Lord_Frederick > 0 && STAT_Beat_Lord_Phillip > 0 && STAT_Beat_Lord_Wazir > 0 && STAT_Beat_Lord_Emir > 0 && STAT_Beat_Lord_Nizar > 0 && STAT_Beat_Lord_Sheriff > 0 && STAT_Beat_Lord_Marshall > 0 && STAT_Beat_Lord_Abbot > 0 && STAT_Beat_Lord_Jewel > 0 && STAT_Beat_Lord_Sentinel > 0 && STAT_Beat_Lord_Nomad > 0 && STAT_Beat_Lord_Kahin > 0)
			{
				AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Skirmish_Beating_All_Lords);
			}
		}
	}

	public void SetAchievementComplete(Enums.Achievements achType)
	{
		if (SteamManager.Initialized)
		{
			switch (achType)
			{
			case Enums.Achievements.Complete_Tutorial:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Tut_Complete");
				break;
			case Enums.Achievements.Complete_Campaign_1:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Campaign_1");
				break;
			case Enums.Achievements.Complete_Campaign_2:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Campaign_2");
				break;
			case Enums.Achievements.Complete_Campaign_3:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Campaign_3");
				break;
			case Enums.Achievements.Complete_Campaign_4:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Campaign_4");
				break;
			case Enums.Achievements.Complete_Campaign_5:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Campaign_5");
				break;
			case Enums.Achievements.Complete_Campaign_6:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Campaign_6");
				break;
			case Enums.Achievements.Complete_Campaign_7:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Campaign_7");
				break;
			case Enums.Achievements.Complete_FirstEdition_Trail:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Trail_1");
				break;
			case Enums.Achievements.Complete_Warchest_Trail:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Trail_2");
				break;
			case Enums.Achievements.Complete_Extreme_Trail:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Trail_3");
				break;
			case Enums.Achievements.Complete_Sands_Warrior:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Warrior");
				break;
			case Enums.Achievements.Complete_Sands_Champion:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Champion");
				break;
			case Enums.Achievements.Complete_Sands_Prince:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Prince");
				break;
			case Enums.Achievements.Complete_Sands_Trail_1:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Trail_1");
				break;
			case Enums.Achievements.Complete_Sands_Trail_2:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Trail_2");
				break;
			case Enums.Achievements.Complete_Sands_Trail_3:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Trail_3");
				break;
			case Enums.Achievements.Complete_Sands_Trail_4:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Trail_4");
				break;
			case Enums.Achievements.Complete_Sands_Trail_5:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Trail_5");
				break;
			case Enums.Achievements.Complete_Sands_Trail_6:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Trail_6");
				break;
			case Enums.Achievements.Complete_Sands_Trail_7:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Sands_Trail_7");
				break;
			case Enums.Achievements.Win_Skirmish_Game:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Win_Skirmish_Game");
				break;
			case Enums.Achievements.Win_Skirmish_Game_vs_7:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Win_Skirmish_Game_vs_7");
				break;
			case Enums.Achievements.Win_Skirmish_Game_vs_Team_of_7:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Win_Skirmish_Game_vs_Team_of_7");
				break;
			case Enums.Achievements.Win_Skirmish_Game_vs_New_Lords:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Win_Skirmish_Game_vs_New_Lords");
				break;
			case Enums.Achievements.Skirmish_Beating_All_Lords:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Skirmish_Beating_All_Lords");
				break;
			case Enums.Achievements.Win_Skirmish_No_Ranged:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Win_Skirmish_No_Ranged");
				break;
			case Enums.Achievements.Win_Skirmish_All_Ranged:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Win_Skirmish_All_Ranged");
				break;
			case Enums.Achievements.Map_Uploaded_To_Workshop:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Upload_Map");
				break;
			case Enums.Achievements.Scribe_Unlock:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Scribe_Unlock");
				break;
			case Enums.Achievements.Kill_Units_10k:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Kill_10k_Units");
				break;
			case Enums.Achievements.Kill_Units_100k:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Kill_100k_Units");
				break;
			case Enums.Achievements.Kill_Units_1M:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Kill_1M_Units");
				break;
			case Enums.Achievements.Kill_1000_Lions:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Kill_1000_Lions");
				break;
			case Enums.Achievements.Store_1000_Food:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Store_Food_1000");
				break;
			case Enums.Achievements.Store_1000_Weapons:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Store_Weapons_1000");
				break;
			case Enums.Achievements.Store_10000_Wood:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Store_Wood_10000");
				break;
			case Enums.Achievements.Amass_10000_Gold:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Amass_Gold_10000");
				break;
			case Enums.Achievements.Population_300:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Population_300");
				break;
			case Enums.Achievements.Place_Dairy_Farms:
				SteamUserStats.SetAchievement("ACHIEVEMENT_Dairy_Farms");
				break;
			}
			SteamUserStats.StoreStats();
		}
	}
}
