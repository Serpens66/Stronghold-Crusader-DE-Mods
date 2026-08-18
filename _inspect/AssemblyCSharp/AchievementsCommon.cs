using System;
using System.Collections.Generic;

public class AchievementsCommon
{
	private class AchievementStatus
	{
		public Enums.Achievements achievement;

		private bool complete;

		private int value;

		public bool Completed => complete;

		public int GetValue()
		{
			return value;
		}

		public void AddValue(int diff)
		{
			if (!complete)
			{
				value += diff;
				int maxAchievementValue = GetMaxAchievementValue();
				if (maxAchievementValue > 0 && value >= maxAchievementValue)
				{
					complete = true;
					Platform_Achievements.Instance.SetAchievementComplete(achievement);
				}
			}
		}

		public void SetValue(int newValue, bool fromGame = true)
		{
			if (complete || newValue <= value)
			{
				return;
			}
			value = newValue;
			int maxAchievementValue = GetMaxAchievementValue();
			if (maxAchievementValue > 0 && value >= maxAchievementValue)
			{
				complete = true;
				if (fromGame)
				{
					Platform_Achievements.Instance.SetAchievementComplete(achievement);
				}
			}
		}

		public void SetComplete(bool fromGame = true)
		{
			if (!complete)
			{
				complete = true;
				if (fromGame)
				{
					Platform_Achievements.Instance.SetAchievementComplete(achievement);
				}
			}
		}

		public void Reset()
		{
			if (!complete)
			{
				value = 0;
			}
		}

		public int GetMaxAchievementValue()
		{
			return achievement switch
			{
				Enums.Achievements.Kill_Units_10k => 10000, 
				Enums.Achievements.Kill_Units_100k => 100000, 
				Enums.Achievements.Kill_Units_1M => 1000000, 
				Enums.Achievements.Kill_1000_Lions => 1000, 
				Enums.Achievements.Store_1000_Food => 1000, 
				Enums.Achievements.Store_1000_Weapons => 1000, 
				Enums.Achievements.Store_10000_Wood => 10000, 
				Enums.Achievements.Amass_10000_Gold => 50000, 
				Enums.Achievements.Population_300 => 300, 
				Enums.Achievements.Place_Dairy_Farms => 30, 
				_ => -1, 
			};
		}
	}

	private static readonly AchievementsCommon instance;

	private readonly Dictionary<Enums.Achievements, AchievementStatus> achievements = new Dictionary<Enums.Achievements, AchievementStatus>();

	public static AchievementsCommon Instance => instance;

	static AchievementsCommon()
	{
		instance = new AchievementsCommon();
	}

	private AchievementsCommon()
	{
	}

	public void Init()
	{
		this.achievements.Clear();
		foreach (Enums.Achievements value3 in Enum.GetValues(typeof(Enums.Achievements)))
		{
			AchievementStatus value = new AchievementStatus
			{
				achievement = value3
			};
			achievements[value3] = value;
		}
		foreach (KeyValuePair<Enums.Achievements, int> item in Platform_Achievements.Instance.LoadAchievements())
		{
			Enums.Achievements key = item.Key;
			int value2 = item.Value;
			if (value2 == -1)
			{
				achievements[key].SetComplete();
			}
			else if (value2 > 0)
			{
				achievements[key].SetValue(value2);
			}
		}
	}

	public void ResetOnMissionStart()
	{
		achievements[Enums.Achievements.Store_10000_Wood].Reset();
		achievements[Enums.Achievements.Store_1000_Food].Reset();
		achievements[Enums.Achievements.Store_1000_Weapons].Reset();
		achievements[Enums.Achievements.Amass_10000_Gold].Reset();
		achievements[Enums.Achievements.Population_300].Reset();
	}

	public void UpdateAfterLoadingASave(int food, int wood, int weapons)
	{
		if (GameData.Instance.multiplayerMap)
		{
			achievements[Enums.Achievements.Store_1000_Food].Reset();
			achievements[Enums.Achievements.Store_10000_Wood].Reset();
			achievements[Enums.Achievements.Store_1000_Weapons].Reset();
		}
		else
		{
			achievements[Enums.Achievements.Store_1000_Food].SetValue(food);
			achievements[Enums.Achievements.Store_10000_Wood].SetValue(wood);
			achievements[Enums.Achievements.Store_1000_Weapons].SetValue(weapons);
		}
		achievements[Enums.Achievements.Amass_10000_Gold].Reset();
		achievements[Enums.Achievements.Population_300].Reset();
		EngineInterface.SetAchData(food, wood, weapons);
	}

	public void CompleteAchievement(Enums.Achievements achievement)
	{
		if (!ConfigSettings.AchievementsDisabled)
		{
			achievements[achievement].SetComplete();
		}
	}

	public bool IsAchievementComplete(Enums.Achievements achievement)
	{
		return achievements[achievement].Completed;
	}

	public void UpdateValue(int messageID, int value)
	{
		if (!ConfigSettings.AchievementsDisabled)
		{
			switch ((Enums.AchievementMessage)messageID)
			{
			case Enums.AchievementMessage.Wood_Stored:
				achievements[Enums.Achievements.Store_10000_Wood].AddValue(value);
				StoreValuesInEngine();
				break;
			case Enums.AchievementMessage.Food_Stored:
				achievements[Enums.Achievements.Store_1000_Food].AddValue(value);
				StoreValuesInEngine();
				break;
			case Enums.AchievementMessage.Weapon_Stored:
				achievements[Enums.Achievements.Store_1000_Weapons].AddValue(value);
				StoreValuesInEngine();
				break;
			case Enums.AchievementMessage.Gold_Level:
				achievements[Enums.Achievements.Amass_10000_Gold].SetValue(value);
				break;
			case Enums.AchievementMessage.Lion_Killed:
				achievements[Enums.Achievements.Kill_1000_Lions].AddValue(value);
				Platform_Achievements.Instance.addStat(Enums.AchievementStat.LionsKilled, value);
				break;
			case Enums.AchievementMessage.Dairy_Farms:
				achievements[Enums.Achievements.Place_Dairy_Farms].AddValue(value);
				Platform_Achievements.Instance.addStat(Enums.AchievementStat.DairyFarms, value);
				break;
			case Enums.AchievementMessage.Unit_Killed_By_Player:
				achievements[Enums.Achievements.Kill_Units_10k].AddValue(value);
				achievements[Enums.Achievements.Kill_Units_100k].AddValue(value);
				achievements[Enums.Achievements.Kill_Units_1M].AddValue(value);
				Platform_Achievements.Instance.addStat(Enums.AchievementStat.UnitsKilled, value);
				break;
			case Enums.AchievementMessage.Population:
				achievements[Enums.Achievements.Population_300].SetValue(value);
				break;
			case Enums.AchievementMessage.Jester_Killed:
			case (Enums.AchievementMessage)9:
				break;
			}
		}
	}

	private void StoreValuesInEngine()
	{
		EngineInterface.SetAchData(achievements[Enums.Achievements.Store_1000_Food].GetValue(), achievements[Enums.Achievements.Store_10000_Wood].GetValue(), achievements[Enums.Achievements.Store_1000_Weapons].GetValue());
	}
}
