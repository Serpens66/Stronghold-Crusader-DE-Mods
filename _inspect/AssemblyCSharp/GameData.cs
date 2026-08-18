using System;
using System.Collections.Generic;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class GameData
{
	public class Scenarios
	{
		public List<ScenarioEvent> events = new List<ScenarioEvent>();

		public bool autoLose;

		public int gameOverState;

		public int skirmishMissionDuration;

		public bool inGameoverSituationEvents;

		public bool inGameoverSituationVideos;

		private ScenarioEvent endMapEvent;

		public bool InGameoverSituation
		{
			get
			{
				if (!inGameoverSituationEvents)
				{
					return inGameoverSituationVideos;
				}
				return true;
			}
		}

		public void reset()
		{
			if (!inGameoverSituationEvents)
			{
				events.Clear();
				autoLose = false;
				gameOverState = 0;
				endMapEvent = null;
			}
		}

		public List<ScenarioEvent> getEvents()
		{
			return events;
		}

		public void addEvent(int _eventID, int _valid, int _complete, int _eventType, int _targetAmount, int _currentAmount)
		{
			if (!InGameoverSituation)
			{
				ScenarioEvent item = new ScenarioEvent
				{
					eventID = _eventID,
					valid = _valid,
					complete = _complete,
					eventType = _eventType,
					targetAmount = _targetAmount,
					currentAmount = _currentAmount
				};
				events.Add(item);
			}
		}

		public void updateEvent(int _eventID, int _valid, int _complete, int _eventType, int _targetAmount, int _currentAmount)
		{
			foreach (ScenarioEvent @event in events)
			{
				if (@event.eventID == _eventID)
				{
					@event.targetAmount = _targetAmount;
					break;
				}
			}
		}

		public void setAutoLose()
		{
			autoLose = true;
		}

		public void setGameOverState(int state, int screen, int skirmishDate)
		{
			if (!inGameoverSituationEvents)
			{
				gameOverState = state;
				skirmishMissionDuration = skirmishDate;
				if (gameOverState > 0)
				{
					inGameoverSituationEvents = true;
					MainViewModel.Instance.HUDIngameMenu.Hide();
					Director.instance.initGameOver(state, screen, gameOverState == 1);
				}
			}
		}

		public void ManageGameOver(int state, int screen)
		{
			if (!Director.instance.SimRunning)
			{
				return;
			}
			EngineInterface.MPScoreData mPScoreData = null;
			if (Director.instance.MultiplayerGame || Director.instance.SkirmishModeGame)
			{
				mPScoreData = EngineInterface.GetMPScoreData();
			}
			MyAudioManager.Instance.StopAllGameSounds(leaveMusicPlaying: true);
			MainViewModel.Instance.IngameUI.clearVideos();
			Director.instance.stopSimThread();
			MainViewModel.Instance.Show_HUD_Options = false;
			Platform_Multiplayer.Instance.gameMembers = null;
			HUD_MissionOver.FutureVoiceLine = "";
			System.Random random = new System.Random();
			if (state == 1)
			{
				if (random.Next(3) < 2)
				{
					SFXManager.instance.playSpeech(1, "general_victory1.wav", 1f, ignoreSpeechMuting: true);
				}
				else
				{
					SFXManager.instance.playSpeech(1, "general_victory2.wav", 1f, ignoreSpeechMuting: true);
				}
				SFXManager.instance.PlayWinTune();
				if (!instance.multiplayerMap)
				{
					if (instance.game_type == 0)
					{
						if (instance.mission_level <= 5)
						{
							if (instance.mission_level == 5)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Campaign_1);
							}
							else
							{
								if (instance.mission_level == ConfigSettings.Settings_Progress_Historical1Campaign && !ConfigSettings.TempMissionUnlock)
								{
									ConfigSettings.Settings_Progress_Historical1Campaign++;
								}
								FrontendMenus.CurrentSelectedHistorical1Mission = instance.mission_level + 1 + 10;
							}
						}
						else if (instance.mission_level <= 10)
						{
							if (instance.mission_level == 10)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Campaign_2);
							}
							else
							{
								if (instance.mission_level - 5 == ConfigSettings.Settings_Progress_Historical2Campaign && !ConfigSettings.TempMissionUnlock)
								{
									ConfigSettings.Settings_Progress_Historical2Campaign++;
								}
								FrontendMenus.CurrentSelectedHistorical2Mission = instance.mission_level + 1 - 5 + 20;
							}
						}
						else if (instance.mission_level <= 15)
						{
							if (instance.mission_level == 15)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Campaign_3);
							}
							else
							{
								if (instance.mission_level - 10 == ConfigSettings.Settings_Progress_Historical3Campaign && !ConfigSettings.TempMissionUnlock)
								{
									ConfigSettings.Settings_Progress_Historical3Campaign++;
								}
								FrontendMenus.CurrentSelectedHistorical3Mission = instance.mission_level + 1 - 10 + 30;
							}
						}
						else if (instance.mission_level <= 20)
						{
							if (instance.mission_level == 20)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Campaign_4);
							}
							else
							{
								if (instance.mission_level - 15 == ConfigSettings.Settings_Progress_Historical4Campaign && !ConfigSettings.TempMissionUnlock)
								{
									ConfigSettings.Settings_Progress_Historical4Campaign++;
								}
								FrontendMenus.CurrentSelectedHistorical4Mission = instance.mission_level + 1 - 15 + 40;
							}
						}
						else if (instance.mission_level <= 25)
						{
							if (instance.mission_level == 25)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Campaign_5);
							}
							else
							{
								if (instance.mission_level - 20 == ConfigSettings.Settings_Progress_Historical5Campaign && !ConfigSettings.TempMissionUnlock)
								{
									ConfigSettings.Settings_Progress_Historical5Campaign++;
								}
								FrontendMenus.CurrentSelectedHistorical5Mission = instance.mission_level + 1 - 20 + 50;
							}
						}
						else if (instance.mission_level <= 30)
						{
							if (instance.mission_level == 30)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Campaign_6);
							}
							else
							{
								if (instance.mission_level - 25 == ConfigSettings.Settings_Progress_Historical6Campaign && !ConfigSettings.TempMissionUnlock)
								{
									ConfigSettings.Settings_Progress_Historical6Campaign++;
								}
								FrontendMenus.CurrentSelectedHistorical6Mission = instance.mission_level + 1 - 25 + 60;
							}
						}
						else if (instance.mission_level <= 35)
						{
							if (instance.mission_level == 35)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Campaign_7);
							}
							else
							{
								if (instance.mission_level - 30 == ConfigSettings.Settings_Progress_Historical7Campaign && !ConfigSettings.TempMissionUnlock)
								{
									ConfigSettings.Settings_Progress_Historical7Campaign++;
								}
								FrontendMenus.CurrentSelectedHistorical7Mission = instance.mission_level + 1 - 30 + 70;
							}
						}
					}
					else
					{
						_ = Instance.mapType;
						_ = 3;
					}
					ConfigSettings.SaveSettings();
					EngineInterface.ScoreData scoreData = EngineInterface.GetScoreData();
					HUD_MissionOver.ShowVictory((Enums.VictoryScreens)screen, scoreData);
					return;
				}
				bool sandsTrail = false;
				if (Instance.game_type == 3 && Director.instance.WasSkirmishModeGame)
				{
					if (Instance.SkirmishGameType == 3)
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						if (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null)
						{
							FRONT_Multiplayer.Open(skirmishSetup: true, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo, coopSetup: false, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTestMission);
							MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
							MainViewModel.Instance.Show_Frontend_MainMenu = false;
							MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = null;
							MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
						}
						return;
					}
					if (Instance.coopTrailID == 0)
					{
						AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Win_Skirmish_Game);
						if (mPScoreData.ranged_made == 1 && mPScoreData.melee_made == 0)
						{
							AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Win_Skirmish_All_Ranged);
						}
						if (mPScoreData.ranged_made == 0 && mPScoreData.melee_made == 1)
						{
							AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Win_Skirmish_No_Ranged);
						}
						int num = 0;
						int num2 = 0;
						int num3 = -1;
						bool flag = false;
						bool flag2 = false;
						bool flag3 = false;
						bool flag4 = false;
						int num4 = -1;
						for (int i = 1; i < 9; i++)
						{
							if (mPScoreData.valid[i] > 0 && mPScoreData.computer_register[i] <= 0)
							{
								num4 = mPScoreData.teams[i];
								break;
							}
						}
						for (int j = 1; j < 9; j++)
						{
							if (mPScoreData.valid[j] > 0 && mPScoreData.computer_register[j] > 0 && num4 != mPScoreData.teams[j])
							{
								num++;
								if (num3 == -1)
								{
									num3 = mPScoreData.teams[j];
									num2++;
								}
								else if (num3 == mPScoreData.teams[j])
								{
									num2++;
								}
								switch (mPScoreData.computer_register[j])
								{
								case 17:
									flag = true;
									break;
								case 18:
									flag2 = true;
									break;
								case 19:
									flag3 = true;
									break;
								case 20:
									flag4 = true;
									break;
								}
								if (j != 1)
								{
									Platform_Achievements.Instance.setLordKilledStat(mPScoreData.computer_register[j]);
								}
							}
						}
						if (num2 >= 4 && flag && flag4 && flag2 && flag3)
						{
							flag = false;
							flag2 = false;
							flag3 = false;
							flag4 = false;
							for (int k = 1; k < 9; k++)
							{
								if (mPScoreData.valid[k] > 0 && mPScoreData.computer_register[k] > 0 && num3 == mPScoreData.teams[k])
								{
									switch (mPScoreData.computer_register[k])
									{
									case 17:
										flag = true;
										break;
									case 18:
										flag2 = true;
										break;
									case 19:
										flag3 = true;
										break;
									case 20:
										flag4 = true;
										break;
									}
								}
							}
							if (flag && flag4 && flag2 && flag3)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Win_Skirmish_Game_vs_New_Lords);
							}
						}
						if (num == 7)
						{
							AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Win_Skirmish_Game_vs_7);
							if (num2 == 7)
							{
								AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Win_Skirmish_Game_vs_Team_of_7);
							}
						}
					}
					if (Instance.coopTrailID <= 0)
					{
						if (instance.SkirmishGameType == 1)
						{
							switch (Instance.SkirmishTrailType)
							{
							case 0:
							{
								if (ConfigSettings.Settings_Trail1Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail1Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
								{
									ConfigSettings.Settings_Trail1Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
								}
								if (Instance.SkirmishTrailLevel == 49)
								{
									ConfigSettings.Settings_Progress_Trail = 51;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail++;
									}
									FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2;
								}
								int num5 = 0;
								for (int l = 0; l < 50; l++)
								{
									if (ConfigSettings.Settings_Trail1Times[l] >= 0)
									{
										num5++;
									}
								}
								if (num5 == 50)
								{
									AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_FirstEdition_Trail);
								}
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							}
							case 1:
							{
								if (ConfigSettings.Settings_Trail2Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail2Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
								{
									ConfigSettings.Settings_Trail2Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
								}
								if (Instance.SkirmishTrailLevel == 29)
								{
									ConfigSettings.Settings_Progress_Trail2 = 31;
								}
								else
								{
									if (Instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail2 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail2++;
									}
									FrontendMenus.CurrentSelectedTrail2Mission = Instance.SkirmishTrailLevel + 2;
								}
								int num6 = 0;
								for (int m = 0; m < 30; m++)
								{
									if (ConfigSettings.Settings_Trail2Times[m] >= 0)
									{
										num6++;
									}
								}
								if (num6 == 30)
								{
									AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Warchest_Trail);
								}
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							}
							case 2:
							{
								if (ConfigSettings.Settings_Trail3Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail3Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
								{
									ConfigSettings.Settings_Trail3Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
								}
								if (Instance.SkirmishTrailLevel == 19)
								{
									ConfigSettings.Settings_Progress_Trail3 = 21;
								}
								else
								{
									if (Instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail3 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail3++;
									}
									FrontendMenus.CurrentSelectedTrail3Mission = Instance.SkirmishTrailLevel + 2;
								}
								int num7 = 0;
								for (int n = 0; n < 20; n++)
								{
									if (ConfigSettings.Settings_Trail3Times[n] >= 0)
									{
										num7++;
									}
								}
								if (num7 == 20)
								{
									AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Extreme_Trail);
								}
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							}
							case 11:
								sandsTrail = true;
								if (!ConfigSettings.Settings_HideSoTTiming)
								{
									Platform_Leaderboards.UploadScore(Platform_Leaderboards.GetSandsLeaderboardName(Instance.SkirmishTrailType - 10, Instance.SkirmishTrailLevel), skirmishMissionDuration / 40);
									if (ConfigSettings.Settings_Trail_Sands1_Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail_Sands1_Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
									{
										ConfigSettings.Settings_Trail_Sands1_Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
									}
								}
								else if (ConfigSettings.Settings_Trail_Sands1_Times[Instance.SkirmishTrailLevel] <= 0)
								{
									ConfigSettings.Settings_Trail_Sands1_Times[Instance.SkirmishTrailLevel] = int.MaxValue;
								}
								if (Instance.SkirmishTrailLevel == 4)
								{
									ConfigSettings.Settings_Progress_Trail_Sands1 = 6;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail_Sands1 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail_Sands1++;
									}
									FrontendMenus.CurrentSelectedTrailSands1Mission = (FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2);
								}
								TestSandsRankAchievement();
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							case 12:
								sandsTrail = true;
								if (!ConfigSettings.Settings_HideSoTTiming)
								{
									Platform_Leaderboards.UploadScore(Platform_Leaderboards.GetSandsLeaderboardName(Instance.SkirmishTrailType - 10, Instance.SkirmishTrailLevel), skirmishMissionDuration / 40);
									if (ConfigSettings.Settings_Trail_Sands2_Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail_Sands2_Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
									{
										ConfigSettings.Settings_Trail_Sands2_Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
									}
								}
								else if (ConfigSettings.Settings_Trail_Sands2_Times[Instance.SkirmishTrailLevel] <= 0)
								{
									ConfigSettings.Settings_Trail_Sands2_Times[Instance.SkirmishTrailLevel] = int.MaxValue;
								}
								if (Instance.SkirmishTrailLevel == 6)
								{
									ConfigSettings.Settings_Progress_Trail_Sands2 = 8;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail_Sands2 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail_Sands2++;
									}
									FrontendMenus.CurrentSelectedTrailSands2Mission = (FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2);
								}
								TestSandsRankAchievement();
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							case 13:
								sandsTrail = true;
								if (!ConfigSettings.Settings_HideSoTTiming)
								{
									Platform_Leaderboards.UploadScore(Platform_Leaderboards.GetSandsLeaderboardName(Instance.SkirmishTrailType - 10, Instance.SkirmishTrailLevel), skirmishMissionDuration / 40);
									if (ConfigSettings.Settings_Trail_Sands3_Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail_Sands3_Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
									{
										ConfigSettings.Settings_Trail_Sands3_Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
									}
								}
								else if (ConfigSettings.Settings_Trail_Sands3_Times[Instance.SkirmishTrailLevel] <= 0)
								{
									ConfigSettings.Settings_Trail_Sands3_Times[Instance.SkirmishTrailLevel] = int.MaxValue;
								}
								if (Instance.SkirmishTrailLevel == 8)
								{
									ConfigSettings.Settings_Progress_Trail_Sands3 = 10;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail_Sands3 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail_Sands3++;
									}
									FrontendMenus.CurrentSelectedTrailSands3Mission = (FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2);
								}
								TestSandsRankAchievement();
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							case 14:
								sandsTrail = true;
								if (!ConfigSettings.Settings_HideSoTTiming)
								{
									Platform_Leaderboards.UploadScore(Platform_Leaderboards.GetSandsLeaderboardName(Instance.SkirmishTrailType - 10, Instance.SkirmishTrailLevel), skirmishMissionDuration / 40);
									if (ConfigSettings.Settings_Trail_Sands4_Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail_Sands4_Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
									{
										ConfigSettings.Settings_Trail_Sands4_Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
									}
								}
								else if (ConfigSettings.Settings_Trail_Sands4_Times[Instance.SkirmishTrailLevel] <= 0)
								{
									ConfigSettings.Settings_Trail_Sands4_Times[Instance.SkirmishTrailLevel] = int.MaxValue;
								}
								if (Instance.SkirmishTrailLevel == 10)
								{
									ConfigSettings.Settings_Progress_Trail_Sands4 = 12;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail_Sands4 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail_Sands4++;
									}
									FrontendMenus.CurrentSelectedTrailSands4Mission = (FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2);
								}
								TestSandsRankAchievement();
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							case 15:
								sandsTrail = true;
								if (!ConfigSettings.Settings_HideSoTTiming)
								{
									Platform_Leaderboards.UploadScore(Platform_Leaderboards.GetSandsLeaderboardName(Instance.SkirmishTrailType - 10, Instance.SkirmishTrailLevel), skirmishMissionDuration / 40);
									if (ConfigSettings.Settings_Trail_Sands5_Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail_Sands5_Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
									{
										ConfigSettings.Settings_Trail_Sands5_Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
									}
								}
								else if (ConfigSettings.Settings_Trail_Sands5_Times[Instance.SkirmishTrailLevel] <= 0)
								{
									ConfigSettings.Settings_Trail_Sands5_Times[Instance.SkirmishTrailLevel] = int.MaxValue;
								}
								if (Instance.SkirmishTrailLevel == 8)
								{
									ConfigSettings.Settings_Progress_Trail_Sands5 = 10;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail_Sands5 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail_Sands5++;
									}
									FrontendMenus.CurrentSelectedTrailSands5Mission = (FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2);
								}
								TestSandsRankAchievement();
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							case 16:
								sandsTrail = true;
								if (!ConfigSettings.Settings_HideSoTTiming)
								{
									Platform_Leaderboards.UploadScore(Platform_Leaderboards.GetSandsLeaderboardName(Instance.SkirmishTrailType - 10, Instance.SkirmishTrailLevel), skirmishMissionDuration / 40);
									if (ConfigSettings.Settings_Trail_Sands6_Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail_Sands6_Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
									{
										ConfigSettings.Settings_Trail_Sands6_Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
									}
								}
								else if (ConfigSettings.Settings_Trail_Sands6_Times[Instance.SkirmishTrailLevel] <= 0)
								{
									ConfigSettings.Settings_Trail_Sands6_Times[Instance.SkirmishTrailLevel] = int.MaxValue;
								}
								if (Instance.SkirmishTrailLevel == 8)
								{
									ConfigSettings.Settings_Progress_Trail_Sands6 = 10;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail_Sands6 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail_Sands6++;
									}
									FrontendMenus.CurrentSelectedTrailSands6Mission = (FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2);
								}
								TestSandsRankAchievement();
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							case 17:
								sandsTrail = true;
								if (!ConfigSettings.Settings_HideSoTTiming)
								{
									Platform_Leaderboards.UploadScore(Platform_Leaderboards.GetSandsLeaderboardName(Instance.SkirmishTrailType - 10, Instance.SkirmishTrailLevel), skirmishMissionDuration / 40);
									if (ConfigSettings.Settings_Trail_Sands7_Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail_Sands7_Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
									{
										ConfigSettings.Settings_Trail_Sands7_Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
									}
								}
								else if (ConfigSettings.Settings_Trail_Sands7_Times[Instance.SkirmishTrailLevel] <= 0)
								{
									ConfigSettings.Settings_Trail_Sands7_Times[Instance.SkirmishTrailLevel] = int.MaxValue;
								}
								if (Instance.SkirmishTrailLevel == 8)
								{
									ConfigSettings.Settings_Progress_Trail_Sands7 = 10;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail_Sands7 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail_Sands7++;
									}
									FrontendMenus.CurrentSelectedTrailSands7Mission = (FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2);
								}
								TestSandsRankAchievement();
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							case 18:
								sandsTrail = true;
								if (!ConfigSettings.Settings_HideSoTTiming)
								{
									Platform_Leaderboards.UploadScore(Platform_Leaderboards.GetSandsLeaderboardName(Instance.SkirmishTrailType - 10, Instance.SkirmishTrailLevel), skirmishMissionDuration / 40);
									if (ConfigSettings.Settings_Trail_Sands8_Times[Instance.SkirmishTrailLevel] <= 0 || ConfigSettings.Settings_Trail_Sands8_Times[Instance.SkirmishTrailLevel] > skirmishMissionDuration)
									{
										ConfigSettings.Settings_Trail_Sands8_Times[Instance.SkirmishTrailLevel] = skirmishMissionDuration;
									}
								}
								else if (ConfigSettings.Settings_Trail_Sands8_Times[Instance.SkirmishTrailLevel] <= 0)
								{
									ConfigSettings.Settings_Trail_Sands8_Times[Instance.SkirmishTrailLevel] = int.MaxValue;
								}
								if (Instance.SkirmishTrailLevel == 8)
								{
									ConfigSettings.Settings_Progress_Trail_Sands8 = 10;
								}
								else
								{
									if (instance.SkirmishTrailLevel + 1 == ConfigSettings.Settings_Progress_Trail_Sands8 && !ConfigSettings.TempMissionUnlock)
									{
										ConfigSettings.Settings_Progress_Trail_Sands8++;
									}
									FrontendMenus.CurrentSelectedTrailSands8Mission = (FrontendMenus.CurrentSelectedTrailMission = Instance.SkirmishTrailLevel + 2);
								}
								TestSandsRankAchievement();
								ConfigSettings.SetDirty();
								ConfigSettings.SaveSettings();
								break;
							}
						}
						else if (instance.SkirmishGameType == 2 && MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null && MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrail)
						{
							ConfigSettings.AddCustomTrailScore(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailName, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailLevel - 1, completed: true, cheated: false);
						}
					}
				}
				else if (mPScoreData.winners[instance.playerID] > 0)
				{
					switch (random.Next(3))
					{
					case 0:
						HUD_MissionOver.setFutureVoiceLine("MP_Victory_1.wav");
						break;
					case 1:
						HUD_MissionOver.setFutureVoiceLine("MP_Victory_2.wav");
						break;
					case 2:
						HUD_MissionOver.setFutureVoiceLine("MP_Victory_3.wav");
						break;
					}
				}
				HUD_MissionOver.ShowMPVictory((Enums.VictoryScreens)screen, mPScoreData, sandsTrail);
				return;
			}
			SFXManager.instance.playSpeech(1, "general_warning2.wav", 1f, ignoreSpeechMuting: true);
			SFXManager.instance.PlayLoseTune();
			if (instance.multiplayerMap)
			{
				switch (random.Next(6))
				{
				case 0:
					HUD_MissionOver.setFutureVoiceLine("MP_Defeat_1.wav");
					break;
				case 1:
					HUD_MissionOver.setFutureVoiceLine("MP_Defeat_2.wav");
					break;
				case 2:
					HUD_MissionOver.setFutureVoiceLine("MP_Defeat_3.wav");
					break;
				case 3:
					HUD_MissionOver.setFutureVoiceLine("MP_Defeat_4.wav");
					break;
				case 4:
					HUD_MissionOver.setFutureVoiceLine("MP_Defeat_5.wav");
					break;
				case 5:
					HUD_MissionOver.setFutureVoiceLine("MP_Defeat_6.wav");
					break;
				}
				HUD_MissionOver.ShowMPDefeat((Enums.DefeatScreens)screen, mPScoreData);
			}
			else
			{
				HUD_MissionOver.ShowDefeat((Enums.DefeatScreens)screen);
			}
		}

		private void TestSandsRankAchievement()
		{
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				return;
			}
			int seconds = 0;
			MainViewModel.Instance.TrailTarget = Instance.GetSandsOfTimeTargetTime(Instance.SkirmishTrailType, Instance.SkirmishTrailLevel, ref seconds);
			int rank = 0;
			Instance.GetSandsOfTimeRankImage(skirmishMissionDuration, seconds, ref rank);
			switch (rank)
			{
			case 4:
				AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Prince);
				AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Champion);
				AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Warrior);
				break;
			case 3:
				AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Champion);
				AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Warrior);
				break;
			case 2:
				AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Warrior);
				break;
			}
			int skirmishTrailType = Instance.SkirmishTrailType;
			if (Instance.SandsUsedChicken(skirmishTrailType))
			{
				return;
			}
			int trailStartDate = ConfigSettings.getTrailStartDate(skirmishTrailType, -1);
			int seconds2 = 0;
			MainViewModel.Instance.TrailDate = GetTimeString(trailStartDate / 40);
			MainViewModel.Instance.TrailTarget = Instance.GetSandsOfTimeTargetTime(skirmishTrailType, -1000, ref seconds2);
			int rank2 = 0;
			Instance.GetSandsOfTimeRankImage(trailStartDate, seconds2, ref rank2);
			if ((uint)(rank2 - 2) <= 2u)
			{
				switch (skirmishTrailType)
				{
				case 11:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Trail_1);
					break;
				case 12:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Trail_2);
					break;
				case 13:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Trail_3);
					break;
				case 14:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Trail_4);
					break;
				case 15:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Trail_5);
					break;
				case 16:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Trail_6);
					break;
				case 17:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Trail_7);
					break;
				case 18:
					AchievementsCommon.Instance.CompleteAchievement(Enums.Achievements.Complete_Sands_Trail_8);
					break;
				}
			}
		}

		public int getGameOverState()
		{
			return gameOverState;
		}

		public void setEndGameTimer(int _eventID, int _nowDate, int _endDate, int _text)
		{
			if (endMapEvent == null)
			{
				endMapEvent = new ScenarioEvent
				{
					eventID = _eventID,
					valid = _text,
					complete = 0,
					eventType = 0,
					targetAmount = _endDate,
					currentAmount = _nowDate
				};
			}
			else
			{
				endMapEvent.eventID = _eventID;
				endMapEvent.valid = _text;
				endMapEvent.targetAmount = _endDate;
				endMapEvent.currentAmount = _nowDate;
			}
		}

		public string getWinTimer(ref int startDate, ref int nowDate, ref int endDate)
		{
			if (endMapEvent == null)
			{
				return null;
			}
			int valid = endMapEvent.valid;
			if (valid <= 0)
			{
				return null;
			}
			startDate = 0;
			endDate = endMapEvent.targetAmount;
			nowDate = endMapEvent.currentAmount;
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_OBJECTIVES, valid);
		}
	}

	public class ScenarioEvent
	{
		public int eventID;

		public int valid;

		public int complete;

		public int eventType;

		public int targetAmount;

		public int currentAmount;
	}

	public class MissionBriefingData
	{
		public string missionTitle = "";

		public string briefingText = "";

		public bool hasOutposts;

		public bool balanced;

		public int mapSize;

		public int playerCount;

		public int hostileAnimalCount;
	}

	private static readonly GameData instance;

	private static readonly int[] game_data_txt;

	public static int[,,] starting_gold_table;

	public static Scenarios scenario;

	public int[] resources = new int[25];

	private int _app_mode;

	private int _app_sub_mode;

	private int _last_app_sub_mode;

	private int _current_buildingchimp_itemID = -1;

	private int _debug_value_1;

	private int _debug_value_2;

	private EngineInterface.PlayState _lastGameState;

	private Enums.GameModes _mapType;

	private bool _multiplayerMap;

	private bool _multiplayerKOTHMap;

	private int _playerID;

	private string _currentFileName = "";

	private string _currentMapName = "";

	private int _game_type;

	private int _mission_level;

	private int _mission_text_id;

	private int _skirmishGameType;

	private int _skirmishTrailType;

	private int _skirmishTrailLevel;

	private int _coopTrailID;

	private int _coopMissionID;

	private int _coopMissionAlly;

	public int[,] Keep_Locations = new int[8, 2];

	public int[] start_keep_location_order = new int[8];

	public int[] extendedLordMapping = new int[8];

	private Enums.GameDifficulty _difficulty_level;

	private int numHintsUnlockedForMission;

	public const int MAX_TOTAL_TROOPS_IN_INVASION = 500;

	public static readonly int[] buildingAvailbleOrder;

	public static readonly Enums.eChimps[] scenarioBarracksTroopsAvailableTypes;

	public static readonly Enums.eChimps[] scenarioMercPostTroopsAvailableTypes;

	public static readonly Enums.eChimps[] scenarioBedouinTroopsAvailableTypes;

	public static readonly int[] startingGoodsLimits;

	public static readonly int[] scn_start_troops_max;

	public static readonly int[] scn_start_siege_equipment_max;

	public static readonly int[] scn_max_invasion_sizes;

	public EngineInterface.ScenarioOverview scenarioOverview;

	public string ansiMissionText;

	public string unicodeMissionText;

	public string utf8MissionText;

	public bool showAlternateMissionTextForBriefing;

	public static readonly int[] start_event_min;

	public static readonly int[] start_event_max;

	public static readonly int[] start_event_multiplier;

	public static readonly int[] start_event_types;

	public static readonly int[][] start_event_goods;

	public static readonly int[] start_event_goods_text;

	public static readonly int[] lord_killed_list;

	public static readonly int[] scenarioActionsOrder;

	public static readonly int[] scenarioEventsOrder;

	public static readonly int[] freeBuildEventsOrder;

	private static readonly int[] nhints;

	private string cachedTrailBriefing = "";

	public string cachedMissionName = "";

	private int[] sands1_TargetTimes = new int[5] { 50, 60, 75, 80, 100 };

	private int[] sands2_TargetTimes = new int[7] { 80, 100, 70, 60, 110, 70, 60 };

	private int[] sands3_TargetTimes = new int[9] { 80, 80, 70, 120, 90, 110, 120, 70, 80 };

	private int[] sands4_TargetTimes = new int[11]
	{
		110, 60, 80, 90, 120, 80, 100, 80, 70, 60,
		140
	};

	private int[] sands5_TargetTimes = new int[9] { 50, 50, 70, 110, 120, 180, 90, 50, 190 };

	private int[] sands6_TargetTimes = new int[9] { 45, 105, 100, 100, 80, 80, 50, 90, 120 };

	private int[] sands7_TargetTimes = new int[9] { 100, 60, 100, 110, 120, 90, 150, 80, 100 };

	private int[] sands8_TargetTimes = new int[9] { 60, 45, 60, 200, 90, 70, 65, 120, 100 };

	public static GameData Instance => instance;

	public int app_mode
	{
		get
		{
			return _app_mode;
		}
		set
		{
			_app_mode = value;
		}
	}

	public int app_sub_mode
	{
		get
		{
			return _app_sub_mode;
		}
		set
		{
			_app_sub_mode = value;
		}
	}

	public int last_app_sub_mode
	{
		get
		{
			return _last_app_sub_mode;
		}
		set
		{
			_last_app_sub_mode = value;
		}
	}

	public int current_buildingchimp_itemID
	{
		get
		{
			return _current_buildingchimp_itemID;
		}
		set
		{
			_current_buildingchimp_itemID = value;
		}
	}

	public int debug_value_1
	{
		get
		{
			return _debug_value_1;
		}
		set
		{
			_debug_value_1 = value;
		}
	}

	public int debug_value_2
	{
		get
		{
			return _debug_value_2;
		}
		set
		{
			_debug_value_2 = value;
		}
	}

	public EngineInterface.PlayState lastGameState
	{
		get
		{
			return _lastGameState;
		}
		set
		{
			_lastGameState = value;
		}
	}

	public Enums.GameModes mapType
	{
		get
		{
			return _mapType;
		}
		set
		{
			_mapType = value;
		}
	}

	public bool multiplayerMap
	{
		get
		{
			return _multiplayerMap;
		}
		set
		{
			_multiplayerMap = value;
		}
	}

	public bool multiplayerKOTHMap
	{
		get
		{
			return _multiplayerKOTHMap;
		}
		set
		{
			_multiplayerKOTHMap = value;
		}
	}

	public int playerID
	{
		get
		{
			return _playerID;
		}
		set
		{
			_playerID = value;
		}
	}

	public string currentFileName
	{
		get
		{
			return _currentFileName;
		}
		set
		{
			_currentFileName = value;
		}
	}

	public string currentMapName
	{
		get
		{
			return _currentMapName;
		}
		set
		{
			_currentMapName = value;
		}
	}

	public int game_type
	{
		get
		{
			return _game_type;
		}
		set
		{
			_game_type = value;
		}
	}

	public int mission_level
	{
		get
		{
			return _mission_level;
		}
		set
		{
			_mission_level = value;
		}
	}

	public int mission_text_id
	{
		get
		{
			return _mission_text_id;
		}
		set
		{
			_mission_text_id = value;
		}
	}

	public int SkirmishGameType
	{
		get
		{
			return _skirmishGameType;
		}
		set
		{
			_skirmishGameType = value;
		}
	}

	public int SkirmishTrailType
	{
		get
		{
			return _skirmishTrailType;
		}
		set
		{
			_skirmishTrailType = value;
		}
	}

	public int SkirmishTrailLevel
	{
		get
		{
			return _skirmishTrailLevel;
		}
		set
		{
			_skirmishTrailLevel = value;
		}
	}

	public int coopTrailID
	{
		get
		{
			return _coopTrailID;
		}
		set
		{
			_coopTrailID = value;
		}
	}

	public int coopMissionID
	{
		get
		{
			return _coopMissionID;
		}
		set
		{
			_coopMissionID = value;
		}
	}

	public int coopMissionAlly
	{
		get
		{
			return _coopMissionAlly;
		}
		set
		{
			_coopMissionAlly = value;
		}
	}

	public Enums.GameDifficulty difficulty_level
	{
		get
		{
			return _difficulty_level;
		}
		set
		{
			_difficulty_level = value;
		}
	}

	static GameData()
	{
		instance = new GameData();
		game_data_txt = new int[545]
		{
			0, 0, 0, 0, 0, 6, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 3, 0, 0, 0, 0,
			5, 0, 0, 0, 0, 20, 0, 0, 0, 0,
			20, 0, 0, 0, 0, 5, 0, 0, 0, 0,
			10, 0, 0, 0, 0, 0, 15, 0, 0, 0,
			0, 0, 0, 0, 0, 5, 0, 0, 0, 0,
			20, 0, 0, 0, 100, 20, 0, 0, 0, 200,
			10, 0, 0, 0, 100, 20, 0, 0, 0, 100,
			10, 0, 0, 0, 100, 10, 0, 0, 0, 0,
			10, 0, 0, 0, 0, 5, 0, 0, 0, 0,
			20, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			20, 0, 0, 0, 100, 20, 0, 0, 0, 150,
			10, 0, 0, 0, 100, 10, 0, 0, 0, 100,
			5, 0, 0, 0, 0, 0, 0, 0, 0, 30,
			0, 0, 10, 0, 100, 10, 0, 0, 0, 0,
			15, 0, 0, 0, 0, 15, 0, 0, 0, 0,
			5, 0, 0, 0, 0, 10, 0, 0, 0, 0,
			20, 0, 0, 0, 0, 20, 0, 0, 0, 400,
			0, 0, 0, 0, 250, 0, 0, 0, 0, 500,
			0, 0, 0, 0, 1000, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 20, 0, 0, 0,
			0, 10, 0, 0, 0, 10, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 10, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 150, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 50, 0, 0, 0, 0, 45,
			0, 0, 0, 0, 40, 0, 0, 0, 0, 25,
			0, 0, 0, 0, 30, 6, 0, 0, 0, 0,
			0, 0, 0, 1, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 60, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 10, 0, 0, 0, 0, 10, 0, 0, 0,
			0, 15, 0, 0, 0, 0, 35, 0, 0, 0,
			0, 40, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 150, 0, 0, 0, 0, 150,
			0, 0, 0, 0, 150, 0, 0, 0, 0, 150,
			0, 0, 0, 0, 5, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 50, 0, 0, 0, 0, 50,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 40,
			0, 0, 0, 0, 45, 0, 0, 0, 0, 50,
			0, 0, 0, 0, 40, 0, 0, 0, 0, 45,
			0, 0, 0, 0, 50, 0, 0, 0, 0, 45,
			0, 0, 0, 0, 40, 10, 0, 0, 0, 100,
			0, 0, 0, 0, 30, 0, 0, 0, 0, 30,
			0, 0, 0, 0, 30, 0, 0, 0, 0, 20,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 30,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			10, 0, 0, 0, 0
		};
		starting_gold_table = new int[4, 5, 2]
		{
			{
				{ 8000, 2000 },
				{ 4000, 2000 },
				{ 2000, 2000 },
				{ 2000, 4000 },
				{ 2000, 8000 }
			},
			{
				{ 8000, 2000 },
				{ 4000, 2000 },
				{ 2000, 2000 },
				{ 2000, 4000 },
				{ 2000, 8000 }
			},
			{
				{ 40000, 3000 },
				{ 20000, 7000 },
				{ 10000, 10000 },
				{ 7000, 20000 },
				{ 3000, 40000 }
			},
			{
				{ 4000, 500 },
				{ 2000, 500 },
				{ 500, 500 },
				{ 500, 2000 },
				{ 500, 4000 }
			}
		};
		scenario = new Scenarios();
		buildingAvailbleOrder = new int[67]
		{
			32, 33, 34, 35, 36, 21, 22, 23, 24, 25,
			12, 15, 10, 9, 8, 27, 29, 91, 31, 30,
			16, 17, 204, 279, 11, 66, 67, 139, 47, 332,
			134, 135, 136, 137, 138, 133, 197, 45, 41, 43,
			44, 49, 51, 52, 53, 54, 55, 62, 58, 59,
			60, 61, 46, 48, 56, 57, 64, 42, 39, 65,
			101, 102, 103, 93, 125, 322, 348
		};
		scenarioBarracksTroopsAvailableTypes = new Enums.eChimps[7]
		{
			Enums.eChimps.CHIMP_TYPE_ARCHER,
			Enums.eChimps.CHIMP_TYPE_XBOWMAN,
			Enums.eChimps.CHIMP_TYPE_SPEARMAN,
			Enums.eChimps.CHIMP_TYPE_PIKEMAN,
			Enums.eChimps.CHIMP_TYPE_MACEMAN,
			Enums.eChimps.CHIMP_TYPE_SWORDSMAN,
			Enums.eChimps.CHIMP_TYPE_KNIGHT
		};
		scenarioMercPostTroopsAvailableTypes = new Enums.eChimps[7]
		{
			Enums.eChimps.CHIMP_TYPE_ARAB_BOW,
			Enums.eChimps.CHIMP_TYPE_ARAB_SLAVE,
			Enums.eChimps.CHIMP_TYPE_ARAB_SLINGER,
			Enums.eChimps.CHIMP_TYPE_ARAB_ASSASIN,
			Enums.eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
			Enums.eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
			Enums.eChimps.CHIMP_TYPE_ARAB_GRENADIER
		};
		scenarioBedouinTroopsAvailableTypes = new Enums.eChimps[8]
		{
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_SAPPER,
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER
		};
		startingGoodsLimits = new int[25]
		{
			0, 0, 5000, 200, 1000, 0, 200, 0, 200, 200,
			1000, 1000, 1000, 1000, 200, 50000, 200, 200, 200, 200,
			200, 200, 200, 200, 200
		};
		scn_start_troops_max = new int[10] { 200, 100, 400, 100, 200, 100, 100, 100, 100, 100 };
		scn_start_siege_equipment_max = new int[7] { 10, 10, 5, 5, 20, 20, 5 };
		scn_max_invasion_sizes = new int[32]
		{
			200, 100, 200, 100, 100, 100, 50, 100, 50, 10,
			10, 10, 10, 10, 50, 10, 200, 200, 200, 50,
			100, 100, 50, 10, 100, 20, 20, 50, 100, 50,
			200, 100
		};
		start_event_min = new int[40]
		{
			0, 10, 0, 0, 1, 1, 1, 1, 0, 0,
			0, 0, 0, 1, 1, 0, 1, 1, 0, 0,
			0, 0, 1, 1, 1, 0, 1, 1, 1, 1,
			1, 0, 1, 1, 0, 0, 0, 0, 0, 0
		};
		start_event_max = new int[40]
		{
			0, 500, 0, 0, 25000, 10000, 10000, 10000, 0, 0,
			0, 0, 0, 1000, 1000, 0, 100, 10000, 0, 0,
			100, 100, 5, 5, 10, 0, 10, 10, 10, 10,
			10, 0, 1000, 100, 0, 0, 0, 0, 0, 60
		};
		start_event_multiplier = new int[40]
		{
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1
		};
		start_event_types = new int[40]
		{
			0, 0, 2, 2, 0, 1, 1, 1, 0, 0,
			0, 0, 0, 2, 2, 0, 0, 1, 0, 0,
			0, 0, 0, 0, 3, 0, 3, 3, 3, 0,
			3, 0, 1, 1, 0, 0, 0, 0, 0, 0
		};
		start_event_goods = new int[40][]
		{
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[20]
			{
				10, 11, 12, 13, 14, 2, 3, 4, 6, 8,
				9, 17, 18, 19, 20, 21, 22, 23, 24, -1
			},
			new int[20]
			{
				10, 11, 12, 13, 14, 2, 3, 4, 6, 8,
				9, 17, 18, 19, 20, 21, 22, 23, 24, -1
			},
			new int[20]
			{
				10, 11, 12, 13, 14, 2, 3, 4, 6, 8,
				9, 17, 18, 19, 20, 21, 22, 23, 24, -1
			},
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[20]
			{
				10, 11, 12, 13, 14, 2, 3, 4, 6, 8,
				9, 17, 18, 19, 20, 21, 22, 23, 24, -1
			},
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1],
			new int[1]
		};
		start_event_goods_text = new int[25]
		{
			156, 0, 50, 51, 52, 0, 53, 0, 54, 55,
			56, 57, 58, 59, 60, 61, 0, 62, 63, 64,
			65, 66, 67, 68, 69
		};
		lord_killed_list = new int[6] { 156, 9, 10, 11, 12, 213 };
		scenarioActionsOrder = new int[30]
		{
			128, 129, 133, 139, 140, 141, 142, 143, 144, 147,
			145, 131, 132, 146, 148, 149, 150, 177, 178, 179,
			180, 181, 183, 184, 134, 135, 136, 152, 153, 182
		};
		scenarioEventsOrder = new int[32]
		{
			106, 107, 108, 109, 110, 111, 112, 113, 123, 114,
			201, 115, 116, 117, 118, 119, 120, 121, 190, 191,
			192, 193, 195, 194, 196, 197, 198, 200, 199, 202,
			203, 209
		};
		freeBuildEventsOrder = new int[10] { 133, 139, 143, 144, 145, 146, 148, 149, 150, 179 };
		nhints = new int[35]
		{
			2, 3, 3, 3, 2, 2, 3, 3, 1, 3,
			3, 4, 3, 4, 2, 2, 1, 1, 3, 1,
			4, 4, 3, 3, 3, 4, 3, 3, 4, 4,
			4, 3, 3, 4, 3
		};
	}

	private GameData()
	{
	}

	public static void getStructureCosts(int structure, ref int wood, ref int stone, ref int iron, ref int pitch, ref int gold)
	{
		if (Instance.lastGameState == null || ((structure != 3 || Instance.lastGameState.freeWoodcutter <= 0) && (structure != 19 || Instance.lastGameState.freeGranary <= 0) && (structure != 26 || (!Director.instance.SkirmishModeGame && !Director.instance.MultiplayerGame && Instance.game_type != 2))))
		{
			switch (structure)
			{
			case 127:
			case 128:
			case 129:
			case 130:
				structure = 47;
				break;
			case 131:
			case 132:
				structure = 46;
				break;
			case 133:
			case 134:
				structure = 45;
				break;
			case 118:
			case 119:
			case 120:
				structure = 66;
				break;
			case 121:
			case 122:
			case 184:
			case 185:
			case 186:
			case 187:
				structure = 104;
				break;
			case 115:
				structure = 86;
				break;
			case 116:
				structure = 87;
				break;
			}
			int num = structure * 5;
			if (num >= 0 && num + 4 < game_data_txt.Length)
			{
				wood = game_data_txt[num];
				stone = game_data_txt[num + 1];
				iron = game_data_txt[num + 2];
				pitch = game_data_txt[num + 3];
				gold = game_data_txt[num + 4];
			}
		}
	}

	public static void getStructureMapperCosts(int mapper, ref int wood, ref int stone, ref int iron, ref int pitch, ref int gold)
	{
		getStructureCosts(getStructFromMapper(mapper), ref wood, ref stone, ref iron, ref pitch, ref gold);
	}

	public static int getStructFromMapper(int mapper)
	{
		switch (mapper)
		{
		case 50:
			return 12;
		case 82:
			return 14;
		case 83:
			return 13;
		case 84:
			return 15;
		case 85:
			return 16;
		case 51:
			return 3;
		case 55:
			return 4;
		case 56:
			return 20;
		case 52:
			return 10;
		case 80:
			return 19;
		case 81:
			return 11;
		case 65:
			return 35;
		case 180:
			return 28;
		case 86:
			return 8;
		case 79:
			return 108;
		case 87:
			return 9;
		case 53:
			return 2;
		case 54:
			return 1;
		case 70:
			return 30;
		case 71:
			return 31;
		case 72:
			return 32;
		case 73:
			return 33;
		case 74:
			return 34;
		case 77:
			return 26;
		case 78:
			return 7;
		case 75:
			return 17;
		case 76:
			return 18;
		case 92:
			return 22;
		case 93:
			return 23;
		case 90:
			return 5;
		case 91:
			return 6;
		case 28:
			return 61;
		case 101:
		case 146:
		case 147:
			return 45;
		case 102:
		case 144:
		case 145:
			return 46;
		case 140:
			return 47;
		case 141:
			return 47;
		case 142:
			return 47;
		case 143:
			return 47;
		case 104:
			return 48;
		case 105:
			return 49;
		case 95:
			return 36;
		case 96:
			return 37;
		case 97:
			return 38;
		case 88:
			return 24;
		case 89:
			return 25;
		case 66:
			return 85;
		case 57:
			return 50;
		case 59:
			return 52;
		case 60:
			return 40;
		case 61:
			return 41;
		case 62:
			return 42;
		case 63:
			return 43;
		case 64:
			return 44;
		case 110:
			return 74;
		case 111:
			return 75;
		case 112:
			return 76;
		case 113:
			return 77;
		case 114:
			return 78;
		case 115:
			return 86;
		case 116:
			return 87;
		case 117:
			return 88;
		case 118:
			return 89;
		case 119:
			return 79;
		case 160:
		case 161:
		case 162:
		case 163:
		case 164:
		case 165:
		case 166:
		case 167:
		case 168:
		case 169:
		case 170:
		case 171:
			return 66;
		case 410:
		case 411:
		case 412:
		case 413:
		case 414:
		case 415:
		case 416:
		case 417:
		case 418:
		case 419:
		case 420:
		case 421:
		case 422:
		case 423:
		case 424:
		case 425:
		case 426:
		case 427:
		case 428:
		case 429:
		case 430:
		case 431:
		case 432:
		case 433:
		case 434:
		case 435:
		case 436:
		case 437:
		case 438:
		case 439:
		case 440:
		case 441:
		case 442:
		case 443:
			return 39;
		case 195:
		case 196:
		case 197:
		case 198:
			return 109;
		case 175:
			return 65;
		case 176:
			return 62;
		case 177:
			return 63;
		case 178:
			return 106;
		case 179:
			return 107;
		case 109:
			return 21;
		case 190:
			return 80;
		case 191:
			return 81;
		case 192:
			return 82;
		case 193:
			return 83;
		case 194:
			return 84;
		case 358:
			return 54;
		case 98:
			return 67;
		case 99:
			return 68;
		case 94:
			return 69;
		case 301:
		case 302:
		case 303:
		case 304:
			return 91;
		case 305:
			return 92;
		case 306:
			return 93;
		case 307:
			return 94;
		case 308:
			return 95;
		case 309:
			return 96;
		case 310:
			return 97;
		case 311:
			return 98;
		case 312:
			return 99;
		case 313:
		case 314:
		case 315:
		case 316:
		case 317:
			return 100;
		case 318:
		case 319:
		case 320:
		case 321:
		case 322:
			return 101;
		case 323:
			return 102;
		case 324:
			return 103;
		case 265:
		case 266:
		case 267:
		case 268:
		case 325:
		case 326:
		case 327:
		case 328:
		case 444:
		case 445:
		case 446:
		case 447:
		case 448:
		case 449:
		case 450:
		case 451:
		case 452:
		case 453:
		case 454:
		case 455:
			return 104;
		case 329:
			return 105;
		case 330:
			return 27;
		case 210:
			return 86;
		case 211:
			return 87;
		default:
			return 0;
		}
	}

	public static int getChimpGoldCost(int troopChimpType)
	{
		if (Director.instance.MultiplayerGame && !Instance.lastGameState.MP_TroopsCostGold)
		{
			return troopChimpType switch
			{
				30 => 30, 
				5 => 30, 
				29 => Instance.lastGameState.laddermanCost, 
				37 => 10, 
				_ => 0, 
			};
		}
		return troopChimpType switch
		{
			30 => 30, 
			5 => 30, 
			29 => Instance.lastGameState.laddermanCost, 
			37 => 10, 
			22 => 12, 
			23 => 20, 
			24 => 8, 
			25 => 20, 
			26 => 20, 
			27 => 40, 
			28 => 40, 
			70 => 75, 
			71 => 5, 
			72 => 12, 
			73 => 60, 
			74 => 80, 
			75 => 80, 
			76 => 100, 
			77 => 50, 
			78 => 40, 
			79 => 100, 
			80 => instance.lastGameState.eunuchCost, 
			81 => 130, 
			82 => 25, 
			83 => 100, 
			84 => 50, 
			85 => 80, 
			_ => 0, 
		};
	}

	public bool IsSandsOfTime()
	{
		if (game_type == 3 && Director.instance.SkirmishModeGame && SkirmishTrailType >= 11)
		{
			return SkirmishTrailType <= 18;
		}
		return false;
	}

	public Thickness getKeepPosition(int keepID, bool scaled = false)
	{
		if (!scaled)
		{
			if (Keep_Locations[keepID, 0] < 0 || Keep_Locations[keepID, 1] < 0)
			{
				return new Thickness(-1000f, -1000f, 999f, 999f);
			}
			int num = Keep_Locations[keepID, 0] - 4;
			int num2 = Keep_Locations[keepID, 1] - 6;
			if (num < 1)
			{
				num = 1;
			}
			if (num2 < 1)
			{
				num2 = 1;
			}
			return new Thickness(num, num2, -1000f, -1000f);
		}
		if (Keep_Locations[keepID, 0] < 0 || Keep_Locations[keepID, 1] < 0)
		{
			return new Thickness(-1000f, -1000f, 999f, 999f);
		}
		int num3 = (Keep_Locations[keepID, 0] - 4) * 232 / 200;
		int num4 = (Keep_Locations[keepID, 1] - 6) * 232 / 200;
		if (num3 < 4)
		{
			num3 = 4;
		}
		if (num4 < 4)
		{
			num4 = 4;
		}
		if (num3 > 212)
		{
			num3 = 212;
		}
		if (num4 > 212)
		{
			num4 = 212;
		}
		return new Thickness(num3, num4, -1000f, -1000f);
	}

	public ImageSource getKeepShield(int keepID, bool mpSetupMapping = false, bool showBlankShields = true)
	{
		int num = start_keep_location_order[keepID];
		if (num < 0 || num >= 8)
		{
			if (showBlankShields && Keep_Locations[keepID, 0] > 0)
			{
				return MainViewModel.Instance.GameSprites[576];
			}
			return null;
		}
		return GetColourShield(num + 1, mpSetupMapping);
	}

	public ImageSource getLiveKeepShield(int playerID)
	{
		if (Keep_Locations[playerID, 0] > 0)
		{
			return GetColourShield(playerID + 1);
		}
		return null;
	}

	public ImageSource getKeepTeamShield(int keepID)
	{
		int num = start_keep_location_order[keepID];
		if (num < 0 || num >= 8)
		{
			return null;
		}
		if (lastGameState.team_shield[num + 1] > 0)
		{
			return MainViewModel.Instance.getTeamAlliesShield(lastGameState.team_shield[num + 1]);
		}
		return null;
	}

	public ImageSource getLiveKeepTeamShield(int playerID)
	{
		if (Keep_Locations[playerID, 0] > 0 && lastGameState.team_shield[playerID + 1] > 0)
		{
			return MainViewModel.Instance.getTeamAlliesShield(lastGameState.team_shield[playerID + 1]);
		}
		return null;
	}

	public ImageSource GetColourShield(int this_player, bool mpSetupMapping = false, bool highlighted = false)
	{
		if (this_player < 0 || this_player > 8)
		{
			return null;
		}
		this_player = (mpSetupMapping ? FRONT_Multiplayer.MP_orig_remap_colour_order[this_player] : SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(this_player)]);
		if (!highlighted)
		{
			switch (this_player)
			{
			case 1:
				return MainViewModel.Instance.GameSprites[466];
			case 2:
				return MainViewModel.Instance.GameSprites[463];
			case 3:
				return MainViewModel.Instance.GameSprites[464];
			case 4:
				return MainViewModel.Instance.GameSprites[465];
			case 5:
				return MainViewModel.Instance.GameSprites[468];
			case 6:
				return MainViewModel.Instance.GameSprites[467];
			case 7:
				return MainViewModel.Instance.GameSprites[469];
			case 8:
				return MainViewModel.Instance.GameSprites[470];
			}
		}
		else
		{
			switch (this_player)
			{
			case 1:
				return MainViewModel.Instance.GameSprites[363];
			case 2:
				return MainViewModel.Instance.GameSprites[360];
			case 3:
				return MainViewModel.Instance.GameSprites[361];
			case 4:
				return MainViewModel.Instance.GameSprites[362];
			case 5:
				return MainViewModel.Instance.GameSprites[365];
			case 6:
				return MainViewModel.Instance.GameSprites[364];
			case 7:
				return MainViewModel.Instance.GameSprites[366];
			case 8:
				return MainViewModel.Instance.GameSprites[367];
			}
		}
		return null;
	}

	public void InitGameInfo(EngineInterface.LoadMapReturnData initData)
	{
		Platform_Multiplayer.MPGameActive = false;
		numHintsUnlockedForMission = 0;
		game_type = initData.game_type;
		multiplayerMap = initData.multiplayerMap != 0;
		multiplayerKOTHMap = initData.multiplayerKOTHMap != 0;
		switch (initData.siege_or_invasion)
		{
		case 0:
			mapType = Enums.GameModes.SIEGE;
			break;
		case 1:
			mapType = Enums.GameModes.INVASION;
			break;
		case 2:
			mapType = Enums.GameModes.ECO;
			break;
		case 3:
			mapType = Enums.GameModes.BUILD;
			break;
		}
		mission_level = initData.mission_level;
		coopTrailID = initData.coopTrailID;
		coopMissionID = initData.coopMissionID;
		coopMissionAlly = initData.coopMissionAlly;
		mission_text_id = initData.textID;
		difficulty_level = (Enums.GameDifficulty)initData.difficulty_level;
		playerID = initData.playerID;
		EditorDirector.instance.SetLocalPlayer(playerID);
		scenario.inGameoverSituationEvents = false;
		scenario.inGameoverSituationVideos = false;
		SkirmishGameType = initData.skirmishGameType;
		if (!MainViewModel.Instance.IsMapEditorMode)
		{
			if (SkirmishGameType >= 0)
			{
				Director.instance.StartSkirmishModeGame();
				if (initData.skirmishTrail >= 0 && SkirmishGameType == 1)
				{
					SkirmishTrailType = initData.skirmishTrail;
					SkirmishTrailLevel = initData.skirmishTrailLevel;
				}
				else
				{
					SkirmishTrailType = -1;
				}
			}
			if (Director.instance.MultiplayerGame || coopTrailID > 0)
			{
				MainViewModel.Instance.HUDmain.RefGameInfoButton.IsEnabled = false;
			}
		}
		Keep_Locations[0, 0] = initData.keep_positions0x;
		Keep_Locations[0, 1] = initData.keep_positions0y;
		Keep_Locations[1, 0] = initData.keep_positions1x;
		Keep_Locations[1, 1] = initData.keep_positions1y;
		Keep_Locations[2, 0] = initData.keep_positions2x;
		Keep_Locations[2, 1] = initData.keep_positions2y;
		Keep_Locations[3, 0] = initData.keep_positions3x;
		Keep_Locations[3, 1] = initData.keep_positions3y;
		Keep_Locations[4, 0] = initData.keep_positions4x;
		Keep_Locations[4, 1] = initData.keep_positions4y;
		Keep_Locations[5, 0] = initData.keep_positions5x;
		Keep_Locations[5, 1] = initData.keep_positions5y;
		Keep_Locations[6, 0] = initData.keep_positions6x;
		Keep_Locations[6, 1] = initData.keep_positions6y;
		Keep_Locations[7, 0] = initData.keep_positions7x;
		Keep_Locations[7, 1] = initData.keep_positions7y;
		start_keep_location_order[0] = initData.start_keep_location_order0;
		start_keep_location_order[1] = initData.start_keep_location_order1;
		start_keep_location_order[2] = initData.start_keep_location_order2;
		start_keep_location_order[3] = initData.start_keep_location_order3;
		start_keep_location_order[4] = initData.start_keep_location_order4;
		start_keep_location_order[5] = initData.start_keep_location_order5;
		start_keep_location_order[6] = initData.start_keep_location_order6;
		start_keep_location_order[7] = initData.start_keep_location_order7;
		extendedLordMapping[0] = initData.computer_extended_lords_names0;
		extendedLordMapping[1] = initData.computer_extended_lords_names1;
		extendedLordMapping[2] = initData.computer_extended_lords_names2;
		extendedLordMapping[3] = initData.computer_extended_lords_names3;
		extendedLordMapping[4] = initData.computer_extended_lords_names4;
		extendedLordMapping[5] = initData.computer_extended_lords_names5;
		extendedLordMapping[6] = initData.computer_extended_lords_names6;
		extendedLordMapping[7] = initData.computer_extended_lords_names7;
	}

	public void setKeepLocationsFromHeader(FileHeader header)
	{
		if (header != null)
		{
			for (int i = 0; i < 8; i++)
			{
				Keep_Locations[i, 0] = header.keep_locations[i, 0];
				Keep_Locations[i, 1] = header.keep_locations[i, 1];
			}
		}
	}

	public bool setKeepOrder(int[] keepOrder)
	{
		bool result = false;
		for (int i = 0; i < 8; i++)
		{
			if (start_keep_location_order[i] != keepOrder[i])
			{
				result = true;
			}
			start_keep_location_order[i] = keepOrder[i];
		}
		return result;
	}

	public void setGameState(EngineInterface.PlayState gameState)
	{
		if (lastGameState == null)
		{
			if (gameState.force_app_mode == 0)
			{
				gameState.force_app_mode = 5;
			}
			if (gameState.game_type == 3 && SkirmishGameType == 0 && gameState.is_skirmish_player(1))
			{
				EditorDirector.instance.SetLocalPlayer(-1);
			}
			Director.instance.CapFrameRateOnLoading(gameState.spectatorMode != 0);
		}
		lastGameState = gameState;
		resources = gameState.resources;
		AchievementsCommon.Instance.UpdateValue(4, resources[15]);
		AchievementsCommon.Instance.UpdateValue(10, gameState.population);
		MainViewModel.Instance.Show_HUD_Extreme = gameState.extremeEnabled > 0 && MainViewModel.Instance.UIVisible;
		if (gameState.extremeEnabled > 0)
		{
			bool num = MainViewModel.Instance.ExtremePower1_Enabled == "False";
			MainViewModel.Instance.ExtremePoints = Math.Min(236, gameState.extremeCount * 242 / 7000).ToString();
			MainViewModel.Instance.ExtremePower1_Enabled = ((gameState.extremeCount >= 636) ? "True" : "False");
			if (num && ConfigSettings.Settings_ShowExtremeHelp && MainViewModel.Instance.ExtremePower1_Enabled == "True")
			{
				HUD_ExtremePowers.RefStory_ShowExtremeHelp.Begin();
				ConfigSettings.Settings_ShowExtremeHelp = false;
				ConfigSettings.SaveSettings();
			}
			MainViewModel.Instance.ExtremePower2_Enabled = ((gameState.extremeCount >= 1272) ? "True" : "False");
			MainViewModel.Instance.ExtremePower3_Enabled = ((gameState.extremeCount >= 1908) ? "True" : "False");
			MainViewModel.Instance.ExtremePower4_Enabled = ((gameState.extremeCount >= 2544) ? "True" : "False");
			MainViewModel.Instance.ExtremePower5_Enabled = ((gameState.extremeCount >= 3180) ? "True" : "False");
			MainViewModel.Instance.ExtremePower6_Enabled = ((gameState.extremeCount >= 3816) ? "True" : "False");
			MainViewModel.Instance.ExtremePower7_Enabled = ((gameState.extremeCount >= 4452) ? "True" : "False");
			MainViewModel.Instance.ExtremePower8_Enabled = ((gameState.extremeCount >= 5088) ? "True" : "False");
		}
		debug_value_1 = gameState.debug_value1;
		debug_value_2 = gameState.game_time;
		int num2 = app_mode;
		int num3 = app_sub_mode;
		app_mode = gameState.app_mode;
		app_sub_mode = gameState.app_sub_mode;
		game_type = gameState.game_type;
		if (gameState.completeSelectionBox > 0)
		{
			MainControls.instance.CurrentAction = 8;
			TroopSelector.instance.startSelection(Input.mousePosition, Input.mousePosition);
			TroopSelector.instance.selection_on = true;
			EditorDirector.instance.triggerTroopsSelection();
		}
		bool inTroopUI = false;
		if (app_mode == 14 && (app_sub_mode == 61 || app_sub_mode == 62))
		{
			inTroopUI = true;
			if (num2 != 14 || (num3 != 61 && num3 != 62))
			{
				MainViewModel.Instance.TroopsSelectedGameAction(fromInitialOpening: true);
			}
			EditorDirector.instance.mapChanged = true;
		}
		else if (app_mode == 16)
		{
			if (num2 != 16 || num3 != app_sub_mode || (app_sub_mode == 70 && current_buildingchimp_itemID != gameState.in_chimp) || (app_sub_mode != 70 && current_buildingchimp_itemID != gameState.in_structure))
			{
				if (app_sub_mode == 70)
				{
					current_buildingchimp_itemID = gameState.in_chimp;
				}
				else
				{
					current_buildingchimp_itemID = gameState.in_structure;
				}
				MainViewModel.Instance.InBuildingGameAction();
			}
		}
		else if (app_mode == 14 && (num2 != 14 || num3 == 61 || num3 == 62))
		{
			if (gameState.game_type != 1 && gameState.game_type != 6)
			{
				MainViewModel.Instance.DefaultGameUIGameAction();
				if (MainViewModel.Instance.buildScreenID == 17 && !MainViewModel.Instance.FreezeMainControls)
				{
					MainViewModel.Instance.HUDmain.NewBuildScreenIndustry();
					MainViewModel.Instance.HUDmain.RefTabBuildIndustry.IsChecked = true;
				}
			}
			else if (num2 != 12)
			{
				MainViewModel.Instance.DefaultMapEditorUIGameAction();
			}
		}
		EditorDirector.instance.updateDLLSelectedTroops(gameState, inTroopUI);
		if (MainViewModel.Instance.IsMapEditorMode)
		{
			gameState.MAPEDITOR_numshieldsToDisplay = getNumShieldsToDisplayInEditor(ref gameState.MAPEDITOR_allowLandscapeEditing);
		}
		OnScreenText.Instance.updateOST(gameState);
	}

	public void SetCameraFromGameState(EngineInterface.PlayState gameState)
	{
		if (gameState == null || gameState.camera_target_x <= 0 || gameState.camera_target_y <= 0)
		{
			return;
		}
		GameMap.instance.mapGameTileToTilemapCoord(gameState.camera_target_x, gameState.camera_target_y, out var tileMapX, out var tileMapY);
		GameMapTile mapTile = GameMap.instance.getMapTile(tileMapX, tileMapY);
		if (mapTile == null)
		{
			return;
		}
		Vector3 spritePosVector = GameMap.instance.getSpritePosVector(tileMapX, tileMapY);
		if (!EngineInterface.FlattenedLandscape && gameState.camera_target_flat == 0)
		{
			float height = mapTile.height;
			spritePosVector.y += height;
			if (gameState.camera_target_z != -123)
			{
				spritePosVector.y += (float)gameState.camera_target_z / 32f;
			}
		}
		CameraControls2D.instance.setCameraPos(spritePosVector.x + 0.5f, spritePosVector.y - 0.5f);
		GameMap.instance.PreCalcScreenCentre();
		GameMap.instance.ignoreNextCachedBounds = false;
	}

	private int getNumShieldsToDisplayInEditor(ref bool allowLandscapeEditing)
	{
		allowLandscapeEditing = true;
		if (multiplayerMap)
		{
			return 8;
		}
		return mapType switch
		{
			Enums.GameModes.BUILD => 1, 
			Enums.GameModes.ECO => 1, 
			Enums.GameModes.INVASION => 5, 
			Enums.GameModes.SIEGE => 1, 
			_ => 1, 
		};
	}

	public static int getTroopOfsetForInvasion(Enums.eChimps troopType)
	{
		return troopType switch
		{
			Enums.eChimps.CHIMP_TYPE_ARCHER => 0, 
			Enums.eChimps.CHIMP_TYPE_XBOWMAN => 1, 
			Enums.eChimps.CHIMP_TYPE_SPEARMAN => 2, 
			Enums.eChimps.CHIMP_TYPE_PIKEMAN => 3, 
			Enums.eChimps.CHIMP_TYPE_MACEMAN => 4, 
			Enums.eChimps.CHIMP_TYPE_SWORDSMAN => 5, 
			Enums.eChimps.CHIMP_TYPE_KNIGHT => 6, 
			Enums.eChimps.CHIMP_TYPE_MONK => 14, 
			Enums.eChimps.CHIMP_TYPE_ENGINEER => 8, 
			Enums.eChimps.CHIMP_TYPE_LADDERMAN => 7, 
			Enums.eChimps.CHIMP_TYPE_TUNNELER => 15, 
			Enums.eChimps.CHIMP_TYPE_CATAPULT => 9, 
			Enums.eChimps.CHIMP_TYPE_TREBUCHET => 10, 
			Enums.eChimps.CHIMP_TYPE_SIEGE_TOWER => 11, 
			Enums.eChimps.CHIMP_TYPE_BATTERING_RAM => 12, 
			Enums.eChimps.CHIMP_TYPE_PORTABLE_SHIELD => 13, 
			Enums.eChimps.CHIMP_TYPE_ARAB_BOW => 16, 
			Enums.eChimps.CHIMP_TYPE_ARAB_SLAVE => 17, 
			Enums.eChimps.CHIMP_TYPE_ARAB_SLINGER => 18, 
			Enums.eChimps.CHIMP_TYPE_ARAB_ASSASIN => 19, 
			Enums.eChimps.CHIMP_TYPE_ARAB_HORSEMAN => 20, 
			Enums.eChimps.CHIMP_TYPE_ARAB_SWORDSMAN => 21, 
			Enums.eChimps.CHIMP_TYPE_ARAB_GRENADIER => 22, 
			Enums.eChimps.CHIMP_TYPE_ARAB_BALLISTA => 23, 
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER => 24, 
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_HEALER => 25, 
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH => 26, 
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER => 27, 
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER => 28, 
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL => 29, 
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_SAPPER => 30, 
			Enums.eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER => 31, 
			_ => -1, 
		};
	}

	public static int getMaxTroopsForInvasion(Enums.eChimps troopType)
	{
		int troopOfsetForInvasion = getTroopOfsetForInvasion(troopType);
		if (troopOfsetForInvasion < 0)
		{
			return 0;
		}
		if (troopOfsetForInvasion < 32)
		{
			return scn_max_invasion_sizes[troopOfsetForInvasion];
		}
		return 0;
	}

	public static int getTroopOfsetForSiegeSetup(Enums.eChimps troopType)
	{
		return troopType switch
		{
			Enums.eChimps.CHIMP_TYPE_ARCHER => 0, 
			Enums.eChimps.CHIMP_TYPE_XBOWMAN => 1, 
			Enums.eChimps.CHIMP_TYPE_SPEARMAN => 2, 
			Enums.eChimps.CHIMP_TYPE_PIKEMAN => 3, 
			Enums.eChimps.CHIMP_TYPE_MACEMAN => 4, 
			Enums.eChimps.CHIMP_TYPE_SWORDSMAN => 5, 
			Enums.eChimps.CHIMP_TYPE_KNIGHT => 6, 
			Enums.eChimps.CHIMP_TYPE_MONK => 9, 
			Enums.eChimps.CHIMP_TYPE_ENGINEER => 8, 
			Enums.eChimps.CHIMP_TYPE_LADDERMAN => 7, 
			Enums.eChimps.CHIMP_TYPE_TUNNELER => 10, 
			_ => -1, 
		};
	}

	public void SetScenarioOverview(EngineInterface.ScenarioOverview data)
	{
		scenarioOverview = data;
	}

	public int getNumEntries()
	{
		if (scenarioOverview != null)
		{
			return scenarioOverview.entries.Count;
		}
		return 0;
	}

	public bool getScenarioEntryOverviewText(int entryID, ref string date, ref string body, ref string repeat, ref int entryType)
	{
		if (entryID < getNumEntries())
		{
			EngineInterface.ScenarioOverviewEntry scenarioOverviewEntry = scenarioOverview.entries[entryID];
			date = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, scenarioOverviewEntry.month) + " " + scenarioOverviewEntry.year;
			repeat = "";
			entryType = scenarioOverviewEntry.entryType;
			switch (scenarioOverviewEntry.entryType)
			{
			case 1:
			{
				int player = 2;
				int data = scenarioOverviewEntry.data2;
				if (data == 2)
				{
					player = 3;
				}
				else if (data >= 10)
				{
					player = data % 10;
				}
				if (HUD_Scenario.getStartingTeamForInvasions(player) == 1)
				{
					body = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 26) + " " + scenarioOverviewEntry.data1;
				}
				else
				{
					body = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_INVASION) + " " + scenarioOverviewEntry.data1;
				}
				if (scenarioOverviewEntry.repeatCount > 0)
				{
					if (scenarioOverviewEntry.repeatCount == 1)
					{
						repeat = "(x" + scenarioOverviewEntry.repeatCount + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_MONTH) + ")";
					}
					else
					{
						repeat = "(x" + scenarioOverviewEntry.repeatCount + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_MONTHS) + ")";
					}
				}
				break;
			}
			case 2:
				body = Translate.Instance.getMessageLibraryText(scenarioOverviewEntry.message);
				break;
			case 3:
				if (scenarioOverviewEntry.data1 == 2)
				{
					body = Translate.Instance.getMessageLibraryText(scenarioOverviewEntry.message);
					break;
				}
				body = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, scenarioOverviewEntry.message);
				switch (scenarioOverviewEntry.data1)
				{
				case 33:
					if (scenarioOverviewEntry.data2 == 0)
					{
						body = body + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 14);
					}
					else
					{
						body = body + " : " + scenarioOverviewEntry.action_data_marker;
					}
					break;
				case 4:
				case 11:
				case 17:
				case 18:
				case 20:
				case 30:
					body = body + " : " + scenarioOverviewEntry.data2;
					break;
				case 32:
					body = body + " : " + scenarioOverviewEntry.action_data_marker + " / " + HUD_Scenario.getReinforcementsName(scenarioOverviewEntry.action_data_reinforcement);
					break;
				case 31:
					body = body + " : " + scenarioOverviewEntry.action_data_marker + " / " + HUD_Scenario.getAllegianceTeamName(scenarioOverviewEntry.action_data_reinforcement);
					break;
				}
				if (scenarioOverviewEntry.repeatDuration > 0 && scenarioOverviewEntry.repeatCount != 1)
				{
					repeat = "(";
					if (scenarioOverviewEntry.repeatCount != 10)
					{
						repeat += scenarioOverviewEntry.repeatCount;
					}
					if (scenarioOverviewEntry.repeatDuration == 1)
					{
						repeat = repeat + "x" + scenarioOverviewEntry.repeatDuration + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_MONTH) + ")";
					}
					else
					{
						repeat = repeat + "x" + scenarioOverviewEntry.repeatDuration + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_MONTHS) + ")";
					}
				}
				break;
			}
		}
		return false;
	}

	public void NewMapForEditor()
	{
		ansiMissionText = (unicodeMissionText = (utf8MissionText = ""));
		showAlternateMissionTextForBriefing = false;
	}

	public void SetMissionTextFromHeader(FileHeader header)
	{
		ansiMissionText = header.ansiMissionText;
		unicodeMissionText = header.unicodeMissionText;
		utf8MissionText = header.utf8MissionText;
		showAlternateMissionTextForBriefing = header.showAlternateMissionTextForBriefing;
		if (header.missionTextType != 0)
		{
			mission_text_id = header.missionTextNumber;
		}
		else
		{
			mission_text_id = 0;
		}
	}

	public static int getInvasionSizeTroopTypeFromIndex(int index)
	{
		return index switch
		{
			1 => 23, 
			2 => 24, 
			3 => 25, 
			4 => 26, 
			5 => 27, 
			6 => 28, 
			7 => 29, 
			8 => 30, 
			14 => 37, 
			9 => 39, 
			10 => 40, 
			12 => 58, 
			11 => 59, 
			13 => 60, 
			15 => 5, 
			16 => 70, 
			17 => 71, 
			18 => 72, 
			19 => 73, 
			20 => 74, 
			21 => 75, 
			22 => 76, 
			23 => 77, 
			24 => 78, 
			25 => 79, 
			26 => 80, 
			27 => 81, 
			28 => 82, 
			29 => 83, 
			30 => 84, 
			31 => 85, 
			_ => 22, 
		};
	}

	public int GetNumHintsForCurrentMission()
	{
		if (game_type == 0)
		{
			return nhints[mission_level - 1];
		}
		return 0;
	}

	public int GetHintTextSectionForCurrentMission()
	{
		if (game_type == 0)
		{
			if (mission_level <= 20)
			{
				return 102 + (mission_level - 1 << 2);
			}
			return 273 + (mission_level - 21 << 2);
		}
		return -1;
	}

	public void getCachedMissionName(FileHeader header)
	{
		string userMapName = header.standAlone_filename.Replace(".map", "");
		cachedMissionName = Translate.Instance.translateMapNames(userMapName, ref cachedTrailBriefing);
	}

	public void getCachedTrailBriefingFromSave(FileHeader header)
	{
		cachedMissionName = "";
		cachedTrailBriefing = "";
		if (header != null)
		{
			getCachedMissionName(header);
		}
	}

	public string GetMissionBriefing(FileHeader header = null, bool fromBriefing = false)
	{
		string result = "";
		if (header != null)
		{
			if (game_type == 3)
			{
				string text = "";
				if (header != null && header.builtinMap)
				{
					return text + header.mission_description;
				}
				return header.utf8MissionText;
			}
			if (game_type == 2)
			{
				getCachedMissionName(header);
				if (header != null && header.builtinMap)
				{
					return header.mission_description;
				}
				return header.utf8MissionText;
			}
			getCachedMissionName(header);
			return cachedTrailBriefing;
		}
		if (game_type == 0)
		{
			int section = ((mission_level > 20) ? (271 + (mission_level - 21) * 4) : (100 + (mission_level - 1) * 4));
			int num = 0;
			num++;
			result = Translate.Instance.lookUpText((Enums.eTextSections)section, num);
		}
		return result;
	}

	public string GetStrategyText()
	{
		int hintTextSectionForCurrentMission = GetHintTextSectionForCurrentMission();
		if (hintTextSectionForCurrentMission >= 0)
		{
			return Translate.Instance.lookUpText((Enums.eTextSections)hintTextSectionForCurrentMission, 1);
		}
		return "";
	}

	public string GetHintText(int hintLine)
	{
		int hintTextSectionForCurrentMission = GetHintTextSectionForCurrentMission();
		if (hintTextSectionForCurrentMission >= 0 && hintLine < numHintsUnlockedForMission)
		{
			return Translate.Instance.lookUpText((Enums.eTextSections)hintTextSectionForCurrentMission, 2 + hintLine);
		}
		return "";
	}

	public int GetNumHintsUnlocked()
	{
		return numHintsUnlockedForMission;
	}

	public void UnlockHint()
	{
		numHintsUnlockedForMission++;
	}

	public static string GetTimeString(int seconds)
	{
		if (seconds < 3600)
		{
			return $"{seconds / 60,2}:{seconds % 60,2:D2}";
		}
		return $"{seconds / 3600,2}:{seconds / 60 % 60,2:D2}:{seconds % 60,2:D2}";
	}

	public string GetSandsOfTimeTargetTime(int trailType, int missionID, ref int seconds)
	{
		try
		{
			seconds = 0;
			if (missionID < 0)
			{
				missionID = -missionID;
				int num = 1;
				switch (trailType)
				{
				case 11:
				{
					int[] array = sands1_TargetTimes;
					foreach (int num9 in array)
					{
						if (num == missionID)
						{
							break;
						}
						seconds += num9 * 60;
						num++;
					}
					break;
				}
				case 12:
				{
					int[] array = sands2_TargetTimes;
					foreach (int num5 in array)
					{
						if (num == missionID)
						{
							break;
						}
						seconds += num5 * 60;
						num++;
					}
					break;
				}
				case 13:
				{
					int[] array = sands3_TargetTimes;
					foreach (int num7 in array)
					{
						if (num == missionID)
						{
							break;
						}
						seconds += num7 * 60;
						num++;
					}
					break;
				}
				case 14:
				{
					int[] array = sands4_TargetTimes;
					foreach (int num3 in array)
					{
						if (num == missionID)
						{
							break;
						}
						seconds += num3 * 60;
						num++;
					}
					break;
				}
				case 15:
				{
					int[] array = sands5_TargetTimes;
					foreach (int num8 in array)
					{
						if (num == missionID)
						{
							break;
						}
						seconds += num8 * 60;
						num++;
					}
					break;
				}
				case 16:
				{
					int[] array = sands6_TargetTimes;
					foreach (int num6 in array)
					{
						if (num == missionID)
						{
							break;
						}
						seconds += num6 * 60;
						num++;
					}
					break;
				}
				case 17:
				{
					int[] array = sands7_TargetTimes;
					foreach (int num4 in array)
					{
						if (num == missionID)
						{
							break;
						}
						seconds += num4 * 60;
						num++;
					}
					break;
				}
				case 18:
				{
					int[] array = sands8_TargetTimes;
					foreach (int num2 in array)
					{
						if (num == missionID)
						{
							break;
						}
						seconds += num2 * 60;
						num++;
					}
					break;
				}
				}
			}
			else
			{
				switch (trailType)
				{
				case 11:
					seconds = sands1_TargetTimes[missionID] * 60;
					break;
				case 12:
					seconds = sands2_TargetTimes[missionID] * 60;
					break;
				case 13:
					seconds = sands3_TargetTimes[missionID] * 60;
					break;
				case 14:
					seconds = sands4_TargetTimes[missionID] * 60;
					break;
				case 15:
					seconds = sands5_TargetTimes[missionID] * 60;
					break;
				case 16:
					seconds = sands6_TargetTimes[missionID] * 60;
					break;
				case 17:
					seconds = sands7_TargetTimes[missionID] * 60;
					break;
				case 18:
					seconds = sands8_TargetTimes[missionID] * 60;
					break;
				}
			}
			return GetTimeString(seconds);
		}
		catch (Exception)
		{
			return "";
		}
	}

	public bool SandsUsedChicken(int trailID)
	{
		int num = 5;
		switch (trailID)
		{
		case 11:
			num = 5;
			break;
		case 12:
			num = 7;
			break;
		case 13:
			num = 9;
			break;
		case 14:
			num = 11;
			break;
		case 15:
			num = 9;
			break;
		case 16:
			num = 9;
			break;
		case 17:
			num = 9;
			break;
		case 18:
			num = 9;
			break;
		}
		for (int i = 0; i < num; i++)
		{
			if (ConfigSettings.getTrailStartDate(trailID, i) <= 0)
			{
				return true;
			}
		}
		return false;
	}

	public ImageSource GetSandsOfTimeRankImage(int timeTaken, int targetTime, bool greyedImage = false)
	{
		int rank = 0;
		return GetSandsOfTimeRankImage(timeTaken, targetTime, ref rank, greyedImage);
	}

	public ImageSource GetSandsOfTimeRankImage(int timeTaken, int targetTime, ref int rank, bool greyedImage = false)
	{
		timeTaken /= 40;
		if (timeTaken < targetTime)
		{
			if (timeTaken < targetTime / 2)
			{
				rank = 4;
			}
			else if (timeTaken < targetTime * 3 / 4)
			{
				rank = 3;
			}
			else
			{
				rank = 2;
			}
		}
		else if (timeTaken > targetTime * 2)
		{
			rank = 0;
		}
		else
		{
			rank = 1;
		}
		return GetSandsOfTimeImage((Enums.SandsRanks)rank, greyedImage);
	}

	public static int GetSandsOfTimeRankTime(int targetTime, Enums.SandsRanks rank)
	{
		return rank switch
		{
			Enums.SandsRanks.Prince => targetTime / 2, 
			Enums.SandsRanks.Champion => targetTime * 3 / 4, 
			Enums.SandsRanks.Warrior => targetTime, 
			Enums.SandsRanks.Tribesman => targetTime * 2, 
			_ => targetTime * 4, 
		};
	}

	public static ImageSource GetSandsOfTimeImage(Enums.SandsRanks rank, bool greyedImage = false, bool large = false, bool small = false)
	{
		switch (rank)
		{
		case Enums.SandsRanks.Prince:
			if (small)
			{
				return MainViewModel.Instance.GameSprites[713];
			}
			if (large)
			{
				return MainViewModel.Instance.GameSprites[702];
			}
			if (!greyedImage)
			{
				return MainViewModel.Instance.GameSprites[588];
			}
			return MainViewModel.Instance.GameSprites[697];
		case Enums.SandsRanks.Champion:
			if (small)
			{
				return MainViewModel.Instance.GameSprites[714];
			}
			if (large)
			{
				return MainViewModel.Instance.GameSprites[703];
			}
			if (!greyedImage)
			{
				return MainViewModel.Instance.GameSprites[589];
			}
			return MainViewModel.Instance.GameSprites[698];
		case Enums.SandsRanks.Warrior:
			if (small)
			{
				return MainViewModel.Instance.GameSprites[715];
			}
			if (large)
			{
				return MainViewModel.Instance.GameSprites[704];
			}
			if (!greyedImage)
			{
				return MainViewModel.Instance.GameSprites[590];
			}
			return MainViewModel.Instance.GameSprites[699];
		case Enums.SandsRanks.Tribesman:
			if (small)
			{
				return MainViewModel.Instance.GameSprites[716];
			}
			if (large)
			{
				return MainViewModel.Instance.GameSprites[705];
			}
			if (!greyedImage)
			{
				return MainViewModel.Instance.GameSprites[591];
			}
			return MainViewModel.Instance.GameSprites[700];
		default:
			if (small)
			{
				return MainViewModel.Instance.GameSprites[717];
			}
			if (large)
			{
				return MainViewModel.Instance.GameSprites[706];
			}
			if (!greyedImage)
			{
				return MainViewModel.Instance.GameSprites[592];
			}
			return MainViewModel.Instance.GameSprites[701];
		}
	}
}
