using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CrusaderDE;
using UnityNativeTool;

public class EngineInterface
{
	public struct LoadMapReturnData
	{
		public int errorCode;

		public int mapSize;

		public int mapRotation;

		public int mapRotationCentreX;

		public int mapRotationCentreY;

		public int siege_or_invasion;

		public int multiplayerMap;

		public int multiplayerKOTHMap;

		public int game_type;

		public int mission_level;

		public int coopTrailID;

		public int coopMissionID;

		public int coopMissionAlly;

		public int textID;

		public int difficulty_level;

		public int playerID;

		public int skirmishTrail;

		public int skirmishTrailLevel;

		public int skirmishGameType;

		public int arabicLord;

		public short keep_positions0x;

		public short keep_positions0y;

		public short keep_positions1x;

		public short keep_positions1y;

		public short keep_positions2x;

		public short keep_positions2y;

		public short keep_positions3x;

		public short keep_positions3y;

		public short keep_positions4x;

		public short keep_positions4y;

		public short keep_positions5x;

		public short keep_positions5y;

		public short keep_positions6x;

		public short keep_positions6y;

		public short keep_positions7x;

		public short keep_positions7y;

		public byte start_keep_location_order0;

		public byte start_keep_location_order1;

		public byte start_keep_location_order2;

		public byte start_keep_location_order3;

		public byte start_keep_location_order4;

		public byte start_keep_location_order5;

		public byte start_keep_location_order6;

		public byte start_keep_location_order7;

		public int loadedVersion;

		public byte radar_colour_mapping0;

		public byte radar_colour_mapping1;

		public byte radar_colour_mapping2;

		public byte radar_colour_mapping3;

		public byte radar_colour_mapping4;

		public byte radar_colour_mapping5;

		public byte radar_colour_mapping6;

		public byte radar_colour_mapping7;

		public byte computer_register0;

		public byte computer_register1;

		public byte computer_register2;

		public byte computer_register3;

		public byte computer_register4;

		public byte computer_register5;

		public byte computer_register6;

		public byte computer_register7;

		public byte computer_name0;

		public byte computer_name1;

		public byte computer_name2;

		public byte computer_name3;

		public byte computer_name4;

		public byte computer_name5;

		public byte computer_name6;

		public byte computer_name7;

		public byte computer_extended_lords_names0;

		public byte computer_extended_lords_names1;

		public byte computer_extended_lords_names2;

		public byte computer_extended_lords_names3;

		public byte computer_extended_lords_names4;

		public byte computer_extended_lords_names5;

		public byte computer_extended_lords_names6;

		public byte computer_extended_lords_names7;
	}

	public struct MultiplayerSetupTransferData
	{
		public int fairness;

		public int starting_gamespeed;

		public int starting_goods_level;

		public int win_condition;

		public int allow_autotrading;

		public int no_knockdown_walls;

		public int autosave;

		public int peacetime;

		public int no_cows;

		public int no_dogs;

		public int start_keep_location_order0;

		public int start_keep_location_order1;

		public int start_keep_location_order2;

		public int start_keep_location_order3;

		public int start_keep_location_order4;

		public int start_keep_location_order5;

		public int start_keep_location_order6;

		public int start_keep_location_order7;

		public int extreme_troops;

		public int extreme_powers;

		public int extreme_powers_around_lord;

		public int allow_outposts;

		public int advanced_options;

		public int advanced_skirmish_options;

		public int advopt_pre_build;

		public int advopt_improved_arabswordsmen;

		public int advopt_improved_laddermen;

		public int advopt_improved_spearmen;

		public int advopt_rebalanced_horsearchers;

		public int advopt_improved_fletchers;

		public int advopt_uncapped_peasants;

		public int advopt_faster_peasants;

		public int advopt_enemy_hps;

		public int global_improved_sieging;

		public int advopt_healers;

		public int advopt_eunuchs;

		public int advopt_nogold;

		public int MP_BuildingsAvailable0;

		public int MP_BuildingsAvailable1;

		public int MP_BuildingsAvailable2;

		public int MP_BuildingsAvailable3;

		public int MP_BuildingsAvailable4;

		public int MP_BuildingsAvailable5;

		public int MP_BuildingsAvailable6;

		public int MP_BuildingsAvailable7;

		public int MP_BuildingsAvailable8;

		public int MP_BuildingsAvailable9;

		public int MP_BuildingsAvailable10;

		public int MP_BuildingsAvailable11;

		public int MP_BuildingsAvailable12;

		public int MP_GoodsAvailable0;

		public int MP_GoodsAvailable1;

		public int MP_GoodsAvailable2;

		public int MP_GoodsAvailable3;

		public int MP_GoodsAvailable4;

		public int MP_GoodsAvailable5;

		public int MP_GoodsAvailable6;

		public int MP_GoodsAvailable7;

		public int MP_GoodsAvailable8;

		public int MP_GoodsAvailable9;

		public int MP_GoodsAvailable10;

		public int MP_GoodsAvailable11;

		public int MP_GoodsAvailable12;

		public int MP_GoodsAvailable13;

		public int MP_GoodsAvailable14;

		public int MP_GoodsAvailable15;

		public int MP_GoodsAvailable16;

		public int MP_GoodsAvailable17;

		public int MP_GoodsAvailable18;

		public int MP_GoodsAvailable19;

		public int MP_GoodsAvailable20;

		public int MP_GoodsAvailable21;

		public int MP_GoodsAvailable22;

		public int MP_GoodsAvailable23;

		public int MP_GoodsAvailable24;

		public int MP_TroopsAvailable0;

		public int MP_TroopsAvailable1;

		public int MP_TroopsAvailable2;

		public int MP_TroopsAvailable3;

		public int MP_TroopsAvailable4;

		public int MP_TroopsAvailable5;

		public int MP_TroopsAvailable6;

		public int MP_TroopsAvailable7;

		public int MP_TroopsAvailable8;

		public int MP_TroopsAvailable9;

		public int MP_TroopsAvailable10;

		public int MP_TroopsAvailable11;

		public int MP_TroopsAvailable12;

		public int MP_TroopsAvailable13;

		public int MP_TroopsAvailable14;

		public int MP_TroopsAvailable15;

		public int MP_TroopsAvailable16;

		public int MP_TroopsAvailable17;

		public int MP_TroopsAvailable18;

		public int MP_TroopsAvailable19;

		public int MP_TroopsAvailable20;

		public int MP_TroopsAvailable21;

		public int MP_TroopsAvailable22;

		public int MP_TroopsAvailable23;

		public int MP_TroopsAvailable24;

		public int MP_TroopsAvailable25;

		public int MP_TroopsAvailable26;

		public int MP_TroopsAvailable27;

		public int MP_TroopsAvailable28;

		public int MP_TroopsAvailable29;

		public int MP_TroopsAvailable30;

		public int MP_TroopsAvailable31;

		public int preferredAIVs0;

		public int preferredAIVs1;

		public int preferredAIVs2;

		public int preferredAIVs3;

		public int preferredAIVs4;

		public int preferredAIVs5;

		public int preferredAIVs6;

		public int preferredAIVs7;

		public int global_improved_sieging2;
	}

	public class MultiplayerSetupData
	{
		public int fairness;

		public int starting_gamespeed;

		public int starting_goods_level;

		public int win_condition;

		public int allow_autotrading;

		public int no_knockdown_walls;

		public int autosave;

		public int peacetime;

		public int no_cows;

		public int no_dogs;

		public int[] start_keep_location_order = new int[8];

		public int extreme_troops;

		public int extreme_powers;

		public int extreme_powers_around_lord;

		public int allow_outposts;

		public int advanced_options;

		public int advanced_skirmish_options;

		public int advopt_pre_build;

		public int advopt_improved_arabswordsmen;

		public int advopt_improved_laddermen;

		public int advopt_improved_spearmen;

		public int advopt_rebalanced_horsearchers;

		public int advopt_improved_fletchers;

		public int advopt_uncapped_peasants;

		public int advopt_faster_peasants;

		public int advopt_enemy_hps;

		public int global_improved_sieging;

		public int advopt_healers;

		public int advopt_eunuchs;

		public int advopt_nogold;

		public int global_improved_sieging2;

		public int[] MP_BuildingsAvailable = new int[13];

		public int[] MP_GoodsAvailable = new int[25];

		public int[] MP_TroopsAvailable = new int[32];

		public int[] preferredAIVs = new int[8];

		public bool advancedSkirmishOptionsEnabled()
		{
			if (advopt_pre_build <= 0 && advopt_improved_arabswordsmen <= 0 && advopt_improved_laddermen <= 0 && advopt_improved_spearmen <= 0 && advopt_rebalanced_horsearchers <= 0 && advopt_improved_fletchers <= 0 && advopt_uncapped_peasants <= 0 && advopt_faster_peasants <= 0 && advopt_enemy_hps == 1 && global_improved_sieging <= 0 && advopt_healers <= 0 && advopt_eunuchs <= 0 && advopt_nogold <= 0)
			{
				return global_improved_sieging2 > 0;
			}
			return true;
		}

		public bool FromString(string str, bool ignoreKeepOrder = false)
		{
			bool result = ToString() != str;
			string[] array = str.Split(",", StringSplitOptions.None);
			int num = 0;
			int num2 = EditorDirector.getIntFromString(array[num++]);
			if (num2 >= 0)
			{
				fairness = num2;
				num2 = 0;
			}
			else
			{
				fairness = EditorDirector.getIntFromString(array[num++]);
			}
			starting_gamespeed = EditorDirector.getIntFromString(array[num++]);
			starting_goods_level = EditorDirector.getIntFromString(array[num++]);
			win_condition = EditorDirector.getIntFromString(array[num++]);
			allow_autotrading = EditorDirector.getIntFromString(array[num++]);
			no_knockdown_walls = EditorDirector.getIntFromString(array[num++]);
			autosave = EditorDirector.getIntFromString(array[num++]);
			peacetime = EditorDirector.getIntFromString(array[num++]);
			no_cows = EditorDirector.getIntFromString(array[num++]);
			no_dogs = EditorDirector.getIntFromString(array[num++]);
			if (num2 <= -1)
			{
				extreme_troops = EditorDirector.getIntFromString(array[num++]);
				extreme_powers = EditorDirector.getIntFromString(array[num++]);
				extreme_powers_around_lord = EditorDirector.getIntFromString(array[num++]);
				allow_outposts = EditorDirector.getIntFromString(array[num++]);
			}
			if (num2 <= -2)
			{
				advanced_options = EditorDirector.getIntFromString(array[num++]);
				if (num2 <= -7)
				{
					advanced_skirmish_options = EditorDirector.getIntFromString(array[num++]);
					advopt_pre_build = EditorDirector.getIntFromString(array[num++]);
					advopt_improved_arabswordsmen = EditorDirector.getIntFromString(array[num++]);
					advopt_improved_laddermen = EditorDirector.getIntFromString(array[num++]);
					advopt_improved_spearmen = EditorDirector.getIntFromString(array[num++]);
					advopt_rebalanced_horsearchers = EditorDirector.getIntFromString(array[num++]);
					advopt_improved_fletchers = EditorDirector.getIntFromString(array[num++]);
					advopt_uncapped_peasants = EditorDirector.getIntFromString(array[num++]);
					advopt_faster_peasants = EditorDirector.getIntFromString(array[num++]);
					advopt_enemy_hps = EditorDirector.getIntFromString(array[num++]);
				}
				else
				{
					advanced_skirmish_options = 0;
					advopt_pre_build = 0;
					advopt_improved_arabswordsmen = 0;
					advopt_improved_laddermen = 0;
					advopt_improved_spearmen = 0;
					advopt_rebalanced_horsearchers = 0;
					advopt_improved_fletchers = 0;
					advopt_uncapped_peasants = 0;
					advopt_faster_peasants = 0;
					advopt_enemy_hps = 1;
				}
				if (num2 <= -8)
				{
					global_improved_sieging = EditorDirector.getIntFromString(array[num++]);
				}
				else
				{
					global_improved_sieging = 0;
				}
				if (num2 <= -9)
				{
					advopt_healers = EditorDirector.getIntFromString(array[num++]);
				}
				else
				{
					advopt_healers = 0;
				}
				if (num2 <= -10)
				{
					advopt_eunuchs = EditorDirector.getIntFromString(array[num++]);
					advopt_nogold = EditorDirector.getIntFromString(array[num++]);
				}
				else
				{
					advopt_eunuchs = 0;
					advopt_nogold = 0;
				}
				if (num2 <= -11)
				{
					global_improved_sieging2 = EditorDirector.getIntFromString(array[num++]);
				}
				else
				{
					global_improved_sieging2 = 0;
				}
				if (num2 <= -6)
				{
					for (int i = 0; i < 13; i++)
					{
						MP_BuildingsAvailable[i] = EditorDirector.getIntFromString(array[num++]);
					}
				}
				else if (num2 <= -5)
				{
					for (int j = 0; j < 12; j++)
					{
						MP_BuildingsAvailable[j] = EditorDirector.getIntFromString(array[num++]);
					}
					MP_BuildingsAvailable[12] = 1;
				}
				else if (num2 <= -4)
				{
					for (int k = 0; k < 11; k++)
					{
						MP_BuildingsAvailable[k] = EditorDirector.getIntFromString(array[num++]);
					}
					MP_BuildingsAvailable[11] = 1;
				}
				else
				{
					for (int l = 0; l < 10; l++)
					{
						MP_BuildingsAvailable[l] = EditorDirector.getIntFromString(array[num++]);
					}
					MP_BuildingsAvailable[10] = 1;
					MP_BuildingsAvailable[11] = 1;
				}
				for (int m = 0; m < 25; m++)
				{
					MP_GoodsAvailable[m] = EditorDirector.getIntFromString(array[num++]);
				}
				for (int n = 0; n < 32; n++)
				{
					MP_TroopsAvailable[n] = EditorDirector.getIntFromString(array[num++]);
				}
			}
			else
			{
				advanced_options = 0;
				advanced_skirmish_options = 0;
				advopt_pre_build = 0;
				advopt_improved_arabswordsmen = 0;
				advopt_improved_laddermen = 0;
				advopt_improved_spearmen = 0;
				advopt_rebalanced_horsearchers = 0;
				advopt_improved_fletchers = 0;
				advopt_uncapped_peasants = 0;
				advopt_faster_peasants = 0;
				advopt_enemy_hps = 0;
				global_improved_sieging = 0;
				global_improved_sieging2 = 0;
				advopt_healers = 0;
				for (int num3 = 0; num3 < 12; num3++)
				{
					MP_BuildingsAvailable[num3] = 1;
				}
				for (int num4 = 0; num4 < 25; num4++)
				{
					MP_GoodsAvailable[num4] = 1;
				}
				for (int num5 = 0; num5 < 32; num5++)
				{
					MP_TroopsAvailable[num5] = 1;
				}
			}
			if (num2 <= -3)
			{
				for (int num6 = 0; num6 < 8; num6++)
				{
					preferredAIVs[num6] = EditorDirector.getIntFromString(array[num++]);
				}
			}
			else
			{
				for (int num7 = 0; num7 < 8; num7++)
				{
					preferredAIVs[num7] = -1;
				}
			}
			if (!ignoreKeepOrder)
			{
				for (int num8 = 0; num8 < 8; num8++)
				{
					start_keep_location_order[num8] = EditorDirector.getIntFromString(array[num++]);
				}
			}
			return result;
		}

		public override string ToString()
		{
			string text = "-12," + fairness + "," + starting_gamespeed + "," + starting_goods_level + "," + win_condition + "," + allow_autotrading + "," + no_knockdown_walls + "," + autosave + "," + peacetime + "," + no_cows + "," + no_dogs + "," + extreme_troops + "," + extreme_powers + "," + extreme_powers_around_lord + "," + allow_outposts + "," + advanced_options + "," + advanced_skirmish_options + "," + advopt_pre_build + "," + advopt_improved_arabswordsmen + "," + advopt_improved_laddermen + "," + advopt_improved_spearmen + "," + advopt_rebalanced_horsearchers + "," + advopt_improved_fletchers + "," + advopt_uncapped_peasants + "," + advopt_faster_peasants + "," + advopt_enemy_hps + "," + global_improved_sieging + "," + advopt_healers + "," + advopt_eunuchs + "," + advopt_nogold + "," + global_improved_sieging2 + ",";
			for (int i = 0; i < 13; i++)
			{
				text = text + MP_BuildingsAvailable[i] + ",";
			}
			for (int j = 0; j < 25; j++)
			{
				text = text + MP_GoodsAvailable[j] + ",";
			}
			for (int k = 0; k < 32; k++)
			{
				text = text + MP_TroopsAvailable[k] + ",";
			}
			for (int l = 0; l < 8; l++)
			{
				text = text + preferredAIVs[l] + ",";
			}
			for (int m = 0; m < 8; m++)
			{
				text = text + start_keep_location_order[m] + ",";
			}
			return text;
		}

		public string ToStringCustomSkirmish()
		{
			return "-4," + advanced_skirmish_options + "," + advopt_pre_build + "," + advopt_improved_arabswordsmen + "," + advopt_improved_laddermen + "," + advopt_improved_spearmen + "," + advopt_rebalanced_horsearchers + "," + advopt_improved_fletchers + "," + advopt_uncapped_peasants + "," + advopt_faster_peasants + "," + advopt_enemy_hps + "," + global_improved_sieging + "," + advopt_healers + "," + advopt_eunuchs + "," + advopt_nogold + "," + global_improved_sieging2 + ",";
		}

		public void FromStringCustomSkirmish(string str)
		{
			string[] array = str.Split(",", StringSplitOptions.None);
			int num = 0;
			int intFromString = EditorDirector.getIntFromString(array[num++]);
			advanced_skirmish_options = EditorDirector.getIntFromString(array[num++]);
			advopt_pre_build = EditorDirector.getIntFromString(array[num++]);
			advopt_improved_arabswordsmen = EditorDirector.getIntFromString(array[num++]);
			advopt_improved_laddermen = EditorDirector.getIntFromString(array[num++]);
			advopt_improved_spearmen = EditorDirector.getIntFromString(array[num++]);
			advopt_rebalanced_horsearchers = EditorDirector.getIntFromString(array[num++]);
			advopt_improved_fletchers = EditorDirector.getIntFromString(array[num++]);
			advopt_uncapped_peasants = EditorDirector.getIntFromString(array[num++]);
			advopt_faster_peasants = EditorDirector.getIntFromString(array[num++]);
			advopt_enemy_hps = EditorDirector.getIntFromString(array[num++]);
			global_improved_sieging = EditorDirector.getIntFromString(array[num++]);
			if (intFromString <= -2)
			{
				advopt_healers = EditorDirector.getIntFromString(array[num++]);
			}
			else
			{
				advopt_healers = 0;
			}
			if (intFromString <= -3)
			{
				advopt_eunuchs = EditorDirector.getIntFromString(array[num++]);
				advopt_nogold = EditorDirector.getIntFromString(array[num++]);
			}
			else
			{
				advopt_eunuchs = 0;
				advopt_nogold = 0;
			}
			if (intFromString <= -4)
			{
				global_improved_sieging2 = EditorDirector.getIntFromString(array[num++]);
			}
			else
			{
				global_improved_sieging2 = 0;
			}
		}

		public static bool compareSettingsStrings(string string1, string string2)
		{
			if (string1.EndsWith(','))
			{
				string1 = string1.Substring(0, string1.Length - 1);
			}
			if (string2.EndsWith(','))
			{
				string2 = string2.Substring(0, string2.Length - 1);
			}
			string[] array = string1.Split(',', StringSplitOptions.None);
			string[] array2 = string2.Split(',', StringSplitOptions.None);
			for (int i = 0; i < array.Length - 8; i++)
			{
				if (array[i] != array2[i])
				{
					return true;
				}
			}
			return false;
		}
	}

	public struct AILordConfigTransferData
	{
		public int opponent_type;

		public int opponent_type_for_speech;

		public int lord_gfx_type;

		public int flag_type;

		public int use_of_religion;

		public int use_of_ale;

		public int vlow_popularity;

		public int low_popularity;

		public int high_popularity;

		public int min_tax;

		public int max_tax;

		public int farm_types1;

		public int farm_types2;

		public int farm_types3;

		public int farm_types4;

		public int farm_types5;

		public int farm_types6;

		public int farm_types7;

		public int farm_types8;

		public int people_to_farm_ratio;

		public int extract_wood_ratio;

		public int extract_stone_ratio;

		public int extract_iron_ratio;

		public int extract_pitch_ratio;

		public int max_quarries;

		public int max_mines;

		public int max_woodcutters;

		public int max_pitch_dugouts;

		public int max_farms;

		public int build_rate;

		public int crushed_building_delay;

		public int sell_food_at;

		public int buy_apples_at;

		public int buy_cheese_at;

		public int buy_bread_at;

		public int buy_wheat_at;

		public int buy_hops_at;

		public int buy_food_amount;

		public int buy_weapons;

		public int pester_for_goods_delay;

		public int send_goods_margin;

		public int ration_boost;

		public int trade_wood_at;

		public int trade_stone_at;

		public int trade_resources_at;

		public int trade_flour_at;

		public int trade_weapons_at;

		public int trade_ale_at;

		public int trade_pitch_at;

		public int trade_minimum;

		public int base_gold_reserves;

		public int blacksmiths_make;

		public int fletchers_make;

		public int poleturners_make;

		public int sell_all1;

		public int sell_all2;

		public int sell_all3;

		public int sell_all4;

		public int sell_all5;

		public int sell_all6;

		public int sell_all7;

		public int sell_all8;

		public int sell_all9;

		public int sell_all10;

		public int sell_all11;

		public int sell_all12;

		public int sell_all13;

		public int sell_all14;

		public int sell_all15;

		public int move_mobile_defenders;

		public int max_mobile_groups;

		public int buy_defense_machines_at;

		public int buy_defense_machines_delay;

		public int dog_release_timing;

		public int dog_points_count;

		public int chance_of_defensive1;

		public int chance_of_defensive2;

		public int chance_of_defensive3;

		public int chance_of_harrasment1;

		public int chance_of_harrasment2;

		public int chance_of_harrasment3;

		public int chance_of_seiging1;

		public int chance_of_seiging2;

		public int chance_of_seiging3;

		public int economy_protection_number;

		public int economy_protection_type;

		public int bodyguard_number;

		public int bodyguard_type;

		public int moat_diggers;

		public int moat_digger_type;

		public int troop_production_rate1;

		public int troop_production_rate2;

		public int troop_production_rate3;

		public int defense_patrol_trigger_level;

		public int defense_patrols;

		public int defense_patrol_style;

		public int defense_patrol_delay;

		public int defensive_trigger_level;

		public int defensive_troops1;

		public int defensive_troops2;

		public int defensive_troops3;

		public int defensive_troops4;

		public int defensive_troops5;

		public int defensive_troops6;

		public int defensive_troops7;

		public int defensive_troops8;

		public int harrasment_trigger_level;

		public int harrasment_trigger_variance;

		public int harrasment_troops1;

		public int harrasment_troops2;

		public int harrasment_troops3;

		public int harrasment_troops4;

		public int harrasment_troops5;

		public int harrasment_troops6;

		public int harrasment_troops7;

		public int harrasment_troops8;

		public int harrasment_machines1;

		public int harrasment_machines2;

		public int harrasment_machines3;

		public int harrasment_machines4;

		public int harrasment_machines5;

		public int harrasment_machines6;

		public int harrasment_machines7;

		public int harrasment_machines8;

		public int max_harrasment_machines;

		public int harrass_delay;

		public int siege_trigger_level;

		public int siege_trigger_variance;

		public int siege_troops_before_will_come_to_rescue;

		public int siege_troops_on_site_percent;

		public int siege_troops_at_home_percent;

		public int siege_soften_up_delay;

		public int siege_victory_delay;

		public int percent_chance_waiting_for_joint_attack;

		public int siege_machines1;

		public int siege_machines2;

		public int siege_machines3;

		public int siege_machines4;

		public int siege_machines5;

		public int siege_machines6;

		public int siege_machines7;

		public int siege_machines8;

		public int siege_cow_timer;

		public int siege_eng_amount;

		public int siege_moat_troop;

		public int siege_moat_amount;

		public int siege_herring_troop;

		public int siege_herring_amount;

		public int siege_assasin_amount;

		public int siege_ladder_amount;

		public int siege_tunnel_amount;

		public int siege_storm_troop;

		public int siege_storm_amount;

		public int siege_storm_tribes;

		public int siege_cover_troop;

		public int siege_cover_amount;

		public int siege_cover_tribes;

		public int siege_shock_troop;

		public int siege_shock_amount;

		public int siege_reserve_troop;

		public int siege_reserve_amount;

		public int siege_reserve_tribes;

		public int siege_wall_troops1;

		public int siege_wall_troops2;

		public int siege_wall_troops3;

		public int siege_wall_troops4;

		public int siege_wall_troops5;

		public int siege_wall_troops6;

		public int siege_wall_troops7;

		public int siege_wall_troops8;

		public int siege_wall_troops9;

		public int siege_wall_troops10;

		public int siege_wall_troops11;

		public int siege_wall_troops12;

		public int siege_wall_troops13;

		public int siege_wall_troops14;

		public int siege_wall_troops15;

		public int siege_wall_troops16;

		public int siege_wall_troops17;

		public int siege_wall_troops18;

		public int siege_wall_troops19;

		public int siege_wall_troops20;

		public int siege_wall_troops21;

		public int siege_wall_troops22;

		public int siege_wall_troops23;

		public int siege_wall_troops24;

		public int siege_wall_amount;

		public int siege_wall_tribes;

		public int who_to_pick_on;

		public int use_improved_sieging;

		public int starting_troops_normal1;

		public int starting_troops_normal2;

		public int starting_troops_normal3;

		public int starting_troops_normal4;

		public int starting_troops_normal5;

		public int starting_troops_normal6;

		public int starting_troops_normal7;

		public int starting_troops_normal8;

		public int starting_troops_normal9;

		public int starting_troops_normal10;

		public int starting_troops_normal11;

		public int starting_troops_normal12;

		public int starting_troops_normal13;

		public int starting_troops_normal14;

		public int starting_troops_normal15;

		public int starting_troops_normal16;

		public int starting_troops_normal17;

		public int starting_troops_normal18;

		public int starting_troops_normal19;

		public int starting_troops_normal20;

		public int starting_troops_normal21;

		public int starting_troops_normal22;

		public int starting_troops_normal23;

		public int starting_troops_normal24;

		public int starting_troops_normal25;

		public int starting_troops_normal26;

		public int starting_troops_normal27;

		public int starting_troops_normal28;

		public int starting_troops_deathmatch1;

		public int starting_troops_deathmatch2;

		public int starting_troops_deathmatch3;

		public int starting_troops_deathmatch4;

		public int starting_troops_deathmatch5;

		public int starting_troops_deathmatch6;

		public int starting_troops_deathmatch7;

		public int starting_troops_deathmatch8;

		public int starting_troops_deathmatch9;

		public int starting_troops_deathmatch10;

		public int starting_troops_deathmatch11;

		public int starting_troops_deathmatch12;

		public int starting_troops_deathmatch13;

		public int starting_troops_deathmatch14;

		public int starting_troops_deathmatch15;

		public int starting_troops_deathmatch16;

		public int starting_troops_deathmatch17;

		public int starting_troops_deathmatch18;

		public int starting_troops_deathmatch19;

		public int starting_troops_deathmatch20;

		public int starting_troops_deathmatch21;

		public int starting_troops_deathmatch22;

		public int starting_troops_deathmatch23;

		public int starting_troops_deathmatch24;

		public int starting_troops_deathmatch25;

		public int starting_troops_deathmatch26;

		public int starting_troops_deathmatch27;

		public int starting_troops_deathmatch28;

		public int starting_troops_crusader1;

		public int starting_troops_crusader2;

		public int starting_troops_crusader3;

		public int starting_troops_crusader4;

		public int starting_troops_crusader5;

		public int starting_troops_crusader6;

		public int starting_troops_crusader7;

		public int starting_troops_crusader8;

		public int starting_troops_crusader9;

		public int starting_troops_crusader10;

		public int starting_troops_crusader11;

		public int starting_troops_crusader12;

		public int starting_troops_crusader13;

		public int starting_troops_crusader14;

		public int starting_troops_crusader15;

		public int starting_troops_crusader16;

		public int starting_troops_crusader17;

		public int starting_troops_crusader18;

		public int starting_troops_crusader19;

		public int starting_troops_crusader20;

		public int starting_troops_crusader21;

		public int starting_troops_crusader22;

		public int starting_troops_crusader23;

		public int starting_troops_crusader24;

		public int starting_troops_crusader25;

		public int starting_troops_crusader26;

		public int starting_troops_crusader27;

		public int starting_troops_crusader28;

		public int lord_power_display_level;

		public int lord_hps_percent;

		public int extendedLordParent;

		public int siege_max_troops;

		public int siege_normal_wave_multiplier;

		public int siege_high_gold_wave_multiplier;

		public int free04;

		public int free05;

		public int free06;

		public int free07;

		public int free08;

		public int free09;

		public int free00;

		public int free11;

		public int free12;

		public int free13;

		public int free14;

		public int free15;

		public int free16;

		public int free17;

		public int free18;

		public int free19;

		public int free20;

		public int free21;

		public int free22;

		public int free23;

		public int free24;

		public int free25;

		public int free26;

		public int free27;

		public int free28;

		public int free29;

		public int free30;

		public int free31;

		public int free32;

		public int free33;

		public int free34;

		public int free35;

		public int free36;

		public int free37;

		public int free38;

		public int free39;

		public int free40;

		public int free41;

		public int free42;

		public int free43;

		public int free44;

		public int free45;

		public int free46;

		public int free47;

		public int free48;

		public int free49;

		public int free50;

		public int free51;

		public int free52;

		public int free53;

		public int free54;

		public int free55;

		public int free56;

		public int free57;

		public int free58;

		public int free59;

		public int free60;

		public int free61;

		public int free62;

		public int free63;

		public int free64;

		public int free65;

		public int free66;

		public int free67;

		public int free68;

		public int free69;

		public int free70;

		public int free71;

		public int free72;

		public int free73;

		public int free74;

		public int free75;

		public int free76;

		public int free77;

		public int free78;

		public int free79;

		public int free80;

		public int free81;

		public int free82;

		public int free83;

		public int free84;

		public int free85;

		public int free86;

		public int free87;

		public int free88;

		public int free89;

		public int free90;

		public int free91;

		public int free92;

		public int free93;

		public int free94;

		public int free95;

		public int free96;

		public int free97;

		public int free98;

		public int free99;

		public int free100;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct ScenarioOverviewReturnData
	{
		[FieldOffset(0)]
		public int startMonth;

		[FieldOffset(4)]
		public int startYear;

		[FieldOffset(8)]
		public int numEntries;

		[FieldOffset(12)]
		public unsafe fixed int month[200];

		[FieldOffset(812)]
		public unsafe fixed int year[200];

		[FieldOffset(1612)]
		public unsafe fixed int entryType[200];

		[FieldOffset(2412)]
		public unsafe fixed int data1[200];

		[FieldOffset(3212)]
		public unsafe fixed int message[200];

		[FieldOffset(4012)]
		public unsafe fixed int repeatDuration[200];

		[FieldOffset(4812)]
		public unsafe fixed int repeatCount[200];

		[FieldOffset(5612)]
		public unsafe fixed int scenario_start_goods[25];

		[FieldOffset(5712)]
		public unsafe fixed int scenario_trader_goods_available[25];

		[FieldOffset(5812)]
		public unsafe fixed int scenario_start_troops[20];

		[FieldOffset(5892)]
		public unsafe fixed int scenario_start_siege_equipment[7];

		[FieldOffset(5920)]
		public unsafe fixed int scenario_buildings_available[100];

		[FieldOffset(6320)]
		public unsafe fixed int sa_troop_availability[7];

		[FieldOffset(6348)]
		public int scenario_start_popularity;

		[FieldOffset(6352)]
		public int scenario_buildings_count;

		[FieldOffset(6356)]
		public int sa_fletcher_bow;

		[FieldOffset(6360)]
		public int sa_blacksmith_mace;

		[FieldOffset(6364)]
		public int sa_poleturner_pike;

		[FieldOffset(6368)]
		public int special_start_gold;

		[FieldOffset(6372)]
		public int special_start;

		[FieldOffset(6376)]
		public int special_start_rationing;

		[FieldOffset(6380)]
		public int special_start_tax_rate;

		[FieldOffset(6384)]
		public unsafe fixed int data2[200];

		[FieldOffset(7184)]
		public int fast_goods_feedin;

		[FieldOffset(7188)]
		public int sa_fletcher_xbow;

		[FieldOffset(7192)]
		public int sa_blacksmith_sword;

		[FieldOffset(7196)]
		public int sa_poleturner_spear;

		[FieldOffset(7200)]
		public unsafe fixed int sa_merc_availability[7];

		[FieldOffset(7228)]
		public unsafe fixed int sa_bed_availability[8];
	}

	public class ScenarioOverviewEntry
	{
		public int month;

		public int year;

		public int entryType;

		public int data1;

		public int data2;

		public int message;

		public int repeatDuration;

		public int repeatCount;

		public int action_data_marker => data2 & 0xFFFF;

		public int action_data_reinforcement => data2 >> 16;
	}

	public class ScenarioOverview
	{
		public int startMonth;

		public int startYear;

		public List<ScenarioOverviewEntry> entries = new List<ScenarioOverviewEntry>();

		public int[] scenario_start_goods = new int[25];

		public int[] scenario_trader_goods_available = new int[25];

		public int[] scenario_start_troops = new int[20];

		public int[] scenario_start_siege_equipment = new int[7];

		public int[] scenario_buildings_available = new int[100];

		public int[] sa_troop_availability = new int[7];

		public int[] sa_merc_availability = new int[7];

		public int[] sa_bed_availability = new int[8];

		public int scenario_start_popularity;

		public int scenario_buildings_count;

		public int sa_fletcher_bow;

		public int sa_blacksmith_mace;

		public int sa_poleturner_pike;

		public int special_start_gold;

		public int special_start;

		public int special_start_rationing;

		public int special_start_tax_rate;

		public int fast_goods_feedin;

		public int sa_fletcher_xbow;

		public int sa_blacksmith_sword;

		public int sa_poleturner_spear;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct evF
	{
		[FieldOffset(0)]
		public short value;

		[FieldOffset(2)]
		public byte type;

		[FieldOffset(3)]
		public byte onoff;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct tl_eventF
	{
		[FieldOffset(0)]
		public int month;

		[FieldOffset(4)]
		public int year;

		[FieldOffset(8)]
		public int tl_type;

		[FieldOffset(12)]
		public short done;

		[FieldOffset(14)]
		public short pre_done;

		[FieldOffset(16)]
		public int action_data;

		[FieldOffset(20)]
		public int action;

		[FieldOffset(24)]
		public short and_or;

		[FieldOffset(26)]
		public byte repeat;

		[FieldOffset(27)]
		public byte repeat_count;

		[FieldOffset(28)]
		public evF event_value1;

		[FieldOffset(32)]
		public evF event_value2;

		[FieldOffset(36)]
		public evF event_value3;

		[FieldOffset(40)]
		public evF event_value4;

		[FieldOffset(44)]
		public evF event_value5;

		[FieldOffset(48)]
		public evF event_value6;

		[FieldOffset(52)]
		public evF event_value7;

		[FieldOffset(56)]
		public evF event_value8;

		[FieldOffset(60)]
		public evF event_value9;

		[FieldOffset(64)]
		public evF event_value10;

		[FieldOffset(68)]
		public evF event_value11;

		[FieldOffset(72)]
		public evF event_value12;

		[FieldOffset(76)]
		public evF event_value13;

		[FieldOffset(80)]
		public evF event_value14;

		[FieldOffset(84)]
		public evF event_value15;

		[FieldOffset(88)]
		public evF event_value16;

		[FieldOffset(92)]
		public evF event_value17;

		[FieldOffset(96)]
		public evF event_value18;

		[FieldOffset(100)]
		public evF event_value19;

		[FieldOffset(104)]
		public evF event_value20;

		[FieldOffset(108)]
		public evF event_value21;

		[FieldOffset(112)]
		public evF event_value22;

		[FieldOffset(116)]
		public evF event_value23;

		[FieldOffset(120)]
		public evF event_value24;

		[FieldOffset(124)]
		public evF event_value25;

		[FieldOffset(128)]
		public evF event_value26;

		[FieldOffset(132)]
		public evF event_value27;

		[FieldOffset(136)]
		public evF event_value28;

		[FieldOffset(140)]
		public evF event_value29;

		[FieldOffset(144)]
		public evF event_value30;

		[FieldOffset(148)]
		public evF event_value31;

		[FieldOffset(152)]
		public evF event_value32;

		[FieldOffset(156)]
		public evF event_value33;

		[FieldOffset(160)]
		public evF event_value34;

		[FieldOffset(164)]
		public evF event_value35;

		[FieldOffset(168)]
		public evF event_value36;

		[FieldOffset(172)]
		public evF event_value37;

		[FieldOffset(176)]
		public evF event_value38;

		[FieldOffset(180)]
		public evF event_value39;

		[FieldOffset(184)]
		public evF event_value40;
	}

	public class ev
	{
		public short value;

		public byte type;

		public byte onoff;
	}

	public class tl_event
	{
		public int month;

		public int year;

		public int tl_type;

		public short done;

		public short pre_done;

		public int action_data;

		public int action;

		public short and_or;

		public byte repeat;

		public byte repeat_count;

		public ev[] event_value = new ev[40];

		public int action_data_marker
		{
			get
			{
				return action_data & 0xFFFF;
			}
			set
			{
				action_data = value | (int)(action_data & 0xFFFF0000u);
			}
		}

		public int action_data_reinforcement
		{
			get
			{
				return action_data >> 16;
			}
			set
			{
				action_data = (value << 16) | (action_data & 0xFFFF);
			}
		}
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct tl_messageF
	{
		[FieldOffset(0)]
		public int month;

		[FieldOffset(4)]
		public int year;

		[FieldOffset(8)]
		public int tl_type;

		[FieldOffset(12)]
		public short done;

		[FieldOffset(14)]
		public short pre_done;

		[FieldOffset(16)]
		public int message_id;

		[FieldOffset(20)]
		public int action;
	}

	public class tl_message
	{
		public int month;

		public int year;

		public int tl_type;

		public short done;

		public short pre_done;

		public int message_id;

		public int action;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct tl_invasionF
	{
		[FieldOffset(0)]
		public int month;

		[FieldOffset(4)]
		public int year;

		[FieldOffset(8)]
		public int tl_type;

		[FieldOffset(12)]
		public short done;

		[FieldOffset(14)]
		public short pre_done;

		[FieldOffset(16)]
		public int total;

		[FieldOffset(20)]
		public unsafe fixed int _size[33];

		[FieldOffset(152)]
		public int invasion_point;

		[FieldOffset(156)]
		public int start_year;

		[FieldOffset(160)]
		public int repeat;

		[FieldOffset(164)]
		public int from;

		[FieldOffset(168)]
		public int markerID;
	}

	public class tl_invasion
	{
		public int month;

		public int year;

		public int tl_type;

		public short done;

		public short pre_done;

		public int total;

		public int[] _size = new int[33];

		public int invasion_point;

		public int start_year;

		public int repeat;

		public int from;

		public int markerID;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct PlayStateReturnData
	{
		[FieldOffset(0)]
		public unsafe fixed int resources[25];

		[FieldOffset(100)]
		public int numSelectedChimps;

		[FieldOffset(104)]
		public unsafe fixed int selectedChimps[1];

		[FieldOffset(108)]
		public int popularity;

		[FieldOffset(112)]
		public int population;

		[FieldOffset(116)]
		public int gold;

		[FieldOffset(120)]
		public int housing_cap;

		[FieldOffset(124)]
		public int upcoming_total_popularity;

		[FieldOffset(128)]
		public int rationing_popularity;

		[FieldOffset(132)]
		public int foodsEaten_popularity;

		[FieldOffset(136)]
		public int food_popularity;

		[FieldOffset(140)]
		public int tax_popularity;

		[FieldOffset(144)]
		public int overcrowding_popularity;

		[FieldOffset(148)]
		public int fearFactor_popularity;

		[FieldOffset(152)]
		public int religion_popularity;

		[FieldOffset(156)]
		public int fairs_popularity;

		[FieldOffset(160)]
		public int plague_popularity;

		[FieldOffset(164)]
		public int wolves_popularity;

		[FieldOffset(168)]
		public int bandits_popularity;

		[FieldOffset(172)]
		public int fire_popularity;

		[FieldOffset(176)]
		public int marriage_popularity;

		[FieldOffset(180)]
		public int jester_popularity;

		[FieldOffset(184)]
		public int good_things;

		[FieldOffset(188)]
		public int bad_things;

		[FieldOffset(192)]
		public int fear_factor;

		[FieldOffset(196)]
		public int fear_factor_next_level;

		[FieldOffset(200)]
		public int efficiency;

		[FieldOffset(204)]
		public unsafe fixed short population_graph[300];

		[FieldOffset(804)]
		public unsafe fixed short food_types_not_eatable[4];

		[FieldOffset(812)]
		public unsafe fixed short troop_counts[34];

		[FieldOffset(880)]
		public short num_priests;

		[FieldOffset(882)]
		public short blessed_percent;

		[FieldOffset(888)]
		public short blessed_next_level_at;

		[FieldOffset(884)]
		public int tax_rate;

		[FieldOffset(890)]
		public short tax_amount;

		[FieldOffset(892)]
		public short peasants_available_for_troops;

		[FieldOffset(894)]
		public unsafe fixed byte make_troop_state[10];

		[FieldOffset(904)]
		public int rationing;

		[FieldOffset(908)]
		public int food_clock;

		[FieldOffset(912)]
		public int total_food;

		[FieldOffset(916)]
		public int months_of_food;

		[FieldOffset(920)]
		public int food_types_eaten;

		[FieldOffset(924)]
		public int food_types_available;

		[FieldOffset(928)]
		public int app_mode;

		[FieldOffset(932)]
		public int app_sub_mode;

		[FieldOffset(936)]
		public int debug_value1;

		[FieldOffset(940)]
		public int game_time;

		[FieldOffset(944)]
		public int in_structure;

		[FieldOffset(948)]
		public int in_structure_type;

		[FieldOffset(952)]
		public int completeSelectionBox;

		[FieldOffset(956)]
		public int in_chimp;

		[FieldOffset(960)]
		public int in_chimp_type;

		[FieldOffset(964)]
		public short inchimp_name1;

		[FieldOffset(966)]
		public short inchimp_name2;

		[FieldOffset(968)]
		public short dog_cage_state;

		[FieldOffset(970)]
		public short inchimp_n_text;

		[FieldOffset(972)]
		public int in_chimp_goods;

		[FieldOffset(976)]
		public int gatehouse_state;

		[FieldOffset(980)]
		public short repairs_allowed;

		[FieldOffset(982)]
		public short can_do_repairs;

		[FieldOffset(984)]
		public short building_hps_for_repair;

		[FieldOffset(986)]
		public short building_maxhps_for_repair;

		[FieldOffset(988)]
		public short sleep_allowed;

		[FieldOffset(990)]
		public short building_type_sleeping;

		[FieldOffset(992)]
		public short have_building_stats;

		[FieldOffset(994)]
		public short workers_have;

		[FieldOffset(996)]
		public short job_vacancies;

		[FieldOffset(998)]
		public short workers_needed;

		[FieldOffset(1000)]
		public short got_keep_access;

		[FieldOffset(1002)]
		public short turned_off;

		[FieldOffset(1004)]
		public short working;

		[FieldOffset(1006)]
		public short mill_message;

		[FieldOffset(1008)]
		public int pints_of_ale;

		[FieldOffset(1012)]
		public short barrels_of_ale;

		[FieldOffset(1014)]
		public short working_inns;

		[FieldOffset(1016)]
		public short total_inns;

		[FieldOffset(1018)]
		public short inn_coverage_percent;

		[FieldOffset(1020)]
		public short inn_coverage_popularity;

		[FieldOffset(1022)]
		public short inn_coverage_next;

		[FieldOffset(1024)]
		public byte troops_show_disband;

		[FieldOffset(1025)]
		public byte troops_show_build_menu;

		[FieldOffset(1026)]
		public byte troops_show_make_catapult;

		[FieldOffset(1027)]
		public byte troops_show_make_trebuchet;

		[FieldOffset(1028)]
		public byte troops_show_make_siege_tower;

		[FieldOffset(1029)]
		public byte troops_show_battering_ram;

		[FieldOffset(1030)]
		public byte troops_show_portable_shield;

		[FieldOffset(1031)]
		public byte troops_show_get_ammo;

		[FieldOffset(1032)]
		public byte troops_show_launch_cow_and_num_cows;

		[FieldOffset(1033)]
		public byte troops_show_attack_here_and_type;

		[FieldOffset(1034)]
		public byte troops_show_attack_here_number_rocks;

		[FieldOffset(1035)]
		public byte troops_show_stance;

		[FieldOffset(1036)]
		public byte troops_show_patrol;

		[FieldOffset(1037)]
		public byte troops_patrol_mode;

		[FieldOffset(1038)]
		public byte weapon_being_made_now;

		[FieldOffset(1039)]
		public byte game_type;

		[FieldOffset(1040)]
		public byte can_make_xbows;

		[FieldOffset(1041)]
		public byte can_make_sword;

		[FieldOffset(1042)]
		public byte can_make_pike;

		[FieldOffset(1043)]
		public byte weapon_being_made_next;

		[FieldOffset(1044)]
		public byte production_no_resources;

		[FieldOffset(1045)]
		public byte playerdesc_message;

		[FieldOffset(1046)]
		public byte playerdesc_message2;

		[FieldOffset(1047)]
		public unsafe fixed byte weapon_types_available[9];

		[FieldOffset(1056)]
		public unsafe fixed short trade_buy_costs[25];

		[FieldOffset(1106)]
		public unsafe fixed short trade_sell_costs[25];

		[FieldOffset(1156)]
		public unsafe fixed short trade_buy_amounts[25];

		[FieldOffset(1206)]
		public unsafe fixed short trade_sell_amounts[25];

		[FieldOffset(1256)]
		public short marry_status;

		[FieldOffset(1258)]
		public short marry_male_type;

		[FieldOffset(1260)]
		public short marry_female_type;

		[FieldOffset(1262)]
		public short marry_text;

		[FieldOffset(1264)]
		public short marry_m_name1;

		[FieldOffset(1266)]
		public short marry_m_name2;

		[FieldOffset(1268)]
		public short marry_f_name1;

		[FieldOffset(1270)]
		public short marry_f_name2;

		[FieldOffset(1272)]
		public short blessed_popularity;

		[FieldOffset(1274)]
		public byte church_adjustment;

		[FieldOffset(1275)]
		public byte church_missing;

		[FieldOffset(1276)]
		public short scribe_frame;

		[FieldOffset(1278)]
		public short total_horses_available;

		[FieldOffset(1280)]
		public int action_point_count;

		[FieldOffset(1284)]
		public unsafe fixed short action_points_x[20];

		[FieldOffset(1324)]
		public unsafe fixed short action_points_y[20];

		[FieldOffset(1364)]
		public short camera_target_x;

		[FieldOffset(1366)]
		public short camera_target_y;

		[FieldOffset(1368)]
		public short camera_target_z;

		[FieldOffset(1370)]
		public short rotateHappened;

		[FieldOffset(1372)]
		public unsafe fixed short trade_sell_costs_fixed[25];

		[FieldOffset(1422)]
		public short trading_current_goods;

		[FieldOffset(1424)]
		public short trading_next_goods;

		[FieldOffset(1426)]
		public short trading_prev_goods;

		[FieldOffset(1428)]
		public short force_app_mode;

		[FieldOffset(1430)]
		public short month;

		[FieldOffset(1432)]
		public short year;

		[FieldOffset(1434)]
		public short pop_months;

		[FieldOffset(1436)]
		public unsafe fixed int keep_storage[25];

		[FieldOffset(1536)]
		public unsafe fixed byte speechFileName[128];

		[FieldOffset(1664)]
		public unsafe fixed byte musicFileName[128];

		[FieldOffset(1792)]
		public short chimp_comments;

		[FieldOffset(1794)]
		public short camera_target_flat;

		[FieldOffset(1796)]
		public unsafe fixed byte binkFileName[128];

		[FieldOffset(1924)]
		public short skirmish_map_num_keeps;

		[FieldOffset(1926)]
		public short inbuilding_help_id;

		[FieldOffset(1928)]
		public short MP_Ahead_By;

		[FieldOffset(1930)]
		public short MP_Behind_By;

		[FieldOffset(1932)]
		public short SkipFrame;

		[FieldOffset(1934)]
		public short undoAvailable;

		[FieldOffset(1936)]
		public unsafe fixed int koth_scores[8];

		[FieldOffset(1968)]
		public unsafe fixed short pingtimes[8];

		[FieldOffset(1984)]
		public short chimps_count;

		[FieldOffset(1986)]
		public short chimps_limit;

		[FieldOffset(1988)]
		public short structs_count;

		[FieldOffset(1990)]
		public short structs_limit;

		[FieldOffset(1992)]
		public short orgs_count;

		[FieldOffset(1994)]
		public short orgs_limit;

		[FieldOffset(1996)]
		public short minerals_count;

		[FieldOffset(1998)]
		public short minerals_limit;

		[FieldOffset(2000)]
		public short tribes_count;

		[FieldOffset(2002)]
		public short tribes_limit;

		[FieldOffset(2004)]
		public unsafe fixed byte starting_teams[9];

		[FieldOffset(2013)]
		public byte freeWoodcutter;

		[FieldOffset(2014)]
		public byte freeGranary;

		[FieldOffset(2015)]
		public byte gotSignpost;

		[FieldOffset(2016)]
		public int repair_wood_needed;

		[FieldOffset(2020)]
		public int repair_stone_needed;

		[FieldOffset(2024)]
		public short panel_text_group;

		[FieldOffset(2026)]
		public short panel_text_text;

		[FieldOffset(2028)]
		public unsafe fixed int markers_start_points[40];

		[FieldOffset(2188)]
		public unsafe fixed byte troop_types_available[8];

		[FieldOffset(2196)]
		public byte free_buildingCheat;

		[FieldOffset(2197)]
		public byte editor_time_paused;

		[FieldOffset(2198)]
		public short bld_tiles_built;

		[FieldOffset(2200)]
		public byte game_paused;

		[FieldOffset(2201)]
		public byte numMPChatEntries;

		[FieldOffset(2202)]
		public short ai_clock;

		[FieldOffset(2204)]
		public unsafe fixed short chat_store_data[50];

		[FieldOffset(2304)]
		public unsafe fixed short autotrade_sell_amount[26];

		[FieldOffset(2356)]
		public unsafe fixed short autotrade_buy_amount[26];

		[FieldOffset(2408)]
		public unsafe fixed byte autotrade_onoff[28];

		[FieldOffset(2436)]
		public unsafe fixed byte control_groups_match[10];

		[FieldOffset(2446)]
		public unsafe fixed short control_groups_total[10];

		[FieldOffset(2466)]
		public unsafe fixed byte control_groups_type[40];

		[FieldOffset(2506)]
		public unsafe fixed short control_groups_count[40];

		[FieldOffset(2586)]
		public byte lordOnlySelected;

		[FieldOffset(2587)]
		public byte gotMarket;

		[FieldOffset(2588)]
		public unsafe fixed byte mpkick[8];

		[FieldOffset(2596)]
		public byte keep_enclosed;

		[FieldOffset(2597)]
		public byte can_make_bows;

		[FieldOffset(2598)]
		public byte can_make_mace;

		[FieldOffset(2599)]
		public byte can_make_spear;

		[FieldOffset(2600)]
		public unsafe fixed byte merc_troop_types_available[8];

		[FieldOffset(2608)]
		public byte messageFrom;

		[FieldOffset(2609)]
		public byte troops_show_make_arab_ballista;

		[FieldOffset(2610)]
		public byte starting_goods_level;

		[FieldOffset(2611)]
		public byte fairness;

		[FieldOffset(2612)]
		public unsafe fixed short computer_register[8];

		[FieldOffset(2628)]
		public unsafe fixed short teams[8];

		[FieldOffset(2644)]
		public unsafe fixed short player_register[8];

		[FieldOffset(2660)]
		public unsafe fixed short computer_names[8];

		[FieldOffset(2676)]
		public unsafe fixed short skirmish_needs_help[8];

		[FieldOffset(2692)]
		public unsafe fixed short skirmish_player_requesting_type[10];

		[FieldOffset(2712)]
		public unsafe fixed short skirmish_player_requesting_amount[10];

		[FieldOffset(2732)]
		public unsafe fixed short skirmish_order[8];

		[FieldOffset(2748)]
		public unsafe fixed short skirmish_order_player[8];

		[FieldOffset(2764)]
		public unsafe fixed short skirmish_order_from_player[8];

		[FieldOffset(2780)]
		public unsafe fixed byte mp_stats_valid[8];

		[FieldOffset(2788)]
		public unsafe fixed byte lord_alive[8];

		[FieldOffset(2796)]
		public unsafe fixed byte bed_troop_types_available[8];

		[FieldOffset(2804)]
		public uint elapsedTime;

		[FieldOffset(2808)]
		public byte balanced;

		[FieldOffset(2809)]
		public byte extremeEnabled;

		[FieldOffset(2810)]
		public short extremeCount;

		[FieldOffset(2812)]
		public byte mouse_selector_state;

		[FieldOffset(2813)]
		public byte flattenedHappened;

		[FieldOffset(2814)]
		public byte skirmishInsultFrom;

		[FieldOffset(2815)]
		public byte skirmishInsult;

		[FieldOffset(2816)]
		public byte lord_Type;

		[FieldOffset(2817)]
		public byte monk_available;

		[FieldOffset(2818)]
		public byte engineer_available;

		[FieldOffset(2819)]
		public byte ladderman_available;

		[FieldOffset(2820)]
		public byte team_shield1;

		[FieldOffset(2821)]
		public byte team_shield2;

		[FieldOffset(2822)]
		public byte team_shield3;

		[FieldOffset(2823)]
		public byte team_shield4;

		[FieldOffset(2824)]
		public byte team_shield5;

		[FieldOffset(2825)]
		public byte team_shield6;

		[FieldOffset(2826)]
		public byte team_shield7;

		[FieldOffset(2827)]
		public byte team_shield8;

		[FieldOffset(2828)]
		public byte resyncPercent;

		[FieldOffset(2829)]
		public byte messageFromcharacter;

		[FieldOffset(2830)]
		public short debug_value2;

		[FieldOffset(2832)]
		public byte laddermanCost;

		[FieldOffset(2833)]
		public byte eunuchCost;

		[FieldOffset(2834)]
		public byte spectatorMode;

		[FieldOffset(2835)]
		public byte customisedExtremeTrail;
	}

	public class PlayState
	{
		public int[] resources = new int[25];

		public int[] keep_storage = new int[25];

		public int numSelectedChimps;

		public int[] selectedChimps = new int[10000];

		public int[] selectedChimpTypes = new int[10000];

		public int popularity;

		public int population;

		public int gold;

		public int housing_cap;

		public int upcoming_total_popularity;

		public int rationing_popularity;

		public int foodsEaten_popularity;

		public int food_popularity;

		public int tax_popularity;

		public int overcrowding_popularity;

		public int fearFactor_popularity;

		public int religion_popularity;

		public int fairs_popularity;

		public int plague_popularity;

		public int wolves_popularity;

		public int bandits_popularity;

		public int fire_popularity;

		public int marriage_popularity;

		public int jester_popularity;

		public int good_things;

		public int bad_things;

		public int fear_factor;

		public int fear_factor_next_level;

		public int efficiency;

		public short[] population_graph = new short[300];

		public short[] food_types_not_eatable = new short[4];

		public short[] troop_counts = new short[34];

		public short num_priests;

		public short blessed_percent;

		public short blessed_next_level_at;

		public int tax_rate;

		public short tax_amount;

		public short peasants_available_for_troops;

		public byte[] make_troop_state = new byte[8];

		public int rationing;

		public int food_clock;

		public int total_food;

		public int months_of_food;

		public int food_types_eaten;

		public int food_types_available;

		public int app_mode;

		public int app_sub_mode;

		public int debug_value1;

		public int game_time;

		public int in_structure;

		public int in_structure_type;

		public int completeSelectionBox;

		public int in_chimp;

		public int in_chimp_type;

		public short inchimp_name1;

		public short inchimp_name2;

		public short dog_cage_state;

		public short inchimp_n_text;

		public int in_chimp_goods;

		public int gatehouse_state;

		public short repairs_allowed;

		public short can_do_repairs;

		public short building_hps_for_repair;

		public short building_maxhps_for_repair;

		public short sleep_allowed;

		public short building_type_sleeping;

		public short have_building_stats;

		public short workers_have;

		public short job_vacancies;

		public short workers_needed;

		public short got_keep_access;

		public short turned_off;

		public short working;

		public short mill_message;

		public int pints_of_ale;

		public short barrels_of_ale;

		public short working_inns;

		public short total_inns;

		public short inn_coverage_percent;

		public short inn_coverage_popularity;

		public short inn_coverage_next;

		public byte troops_show_disband;

		public byte troops_show_build_menu;

		public byte troops_show_make_catapult;

		public byte troops_show_make_trebuchet;

		public byte troops_show_make_siege_tower;

		public byte troops_show_battering_ram;

		public byte troops_show_portable_shield;

		public byte troops_show_get_ammo;

		public byte troops_show_launch_cow_and_num_cows;

		public byte troops_show_attack_here_and_type;

		public byte troops_show_attack_here_number_rocks;

		public byte troops_show_stance;

		public byte troops_show_patrol;

		public byte troops_patrol_mode;

		public byte weapon_being_made_now;

		public byte game_type;

		public byte can_make_xbows;

		public byte can_make_sword;

		public byte can_make_pike;

		public byte weapon_being_made_next;

		public byte production_no_resources;

		public byte playerdesc_message;

		public byte playerdesc_message2;

		public byte[] weapon_types_available;

		public short[] trade_buy_costs;

		public short[] trade_sell_costs;

		public short[] trade_buy_amounts;

		public short[] trade_sell_amounts;

		public short marry_status;

		public short marry_male_type;

		public short marry_female_type;

		public short marry_text;

		public short marry_m_name1;

		public short marry_m_name2;

		public short marry_f_name1;

		public short marry_f_name2;

		public short blessed_popularity;

		public sbyte church_adjustment;

		public byte church_missing;

		public short scribe_frame;

		public short total_horses_available;

		public int action_point_count;

		public short[] action_points_x;

		public short[] action_points_y;

		public short camera_target_x;

		public short camera_target_y;

		public short camera_target_z;

		public short rotateHappened;

		public short[] trade_sell_costs_fixed;

		public short trading_current_goods;

		public short trading_next_goods;

		public short trading_prev_goods;

		public short force_app_mode;

		public short month;

		public short year;

		public short pop_months;

		public short chimp_comments;

		public short camera_target_flat;

		public short skirmish_map_num_keeps;

		public short inbuilding_help_id;

		public short MP_Ahead_By;

		public short MP_Behind_By;

		public short SkipFrame;

		public short undoAvailable;

		public int[] koth_scores;

		public short[] pingtimes;

		public short chimps_count;

		public short chimps_limit;

		public short structs_count;

		public short structs_limit;

		public short orgs_count;

		public short orgs_limit;

		public short minerals_count;

		public short minerals_limit;

		public short tribes_count;

		public short tribes_limit;

		public byte[] starting_teams;

		public byte freeWoodcutter;

		public byte freeGranary;

		public byte gotSignpost;

		public int repair_wood_needed;

		public int repair_stone_needed;

		public short panel_text_group;

		public short panel_text_text;

		public int[,] markers_start_points;

		public byte[] troop_types_available;

		public byte[] merc_troop_types_available;

		public byte[] bed_troop_types_available;

		public byte free_buildingCheat;

		public byte editor_time_paused;

		public short bld_tiles_built;

		public byte game_paused;

		public byte numMPChatEntries;

		public short ai_clock;

		public short[,] chat_store_data;

		public short[] autotrade_sell_amount;

		public short[] autotrade_buy_amount;

		public byte[] autotrade_onoff;

		public byte[] control_groups_match;

		public short[] control_groups_total;

		public byte[] control_groups_type;

		public short[] control_groups_count;

		public byte lordOnlySelected;

		public byte gotMarket;

		public byte[] mpkick;

		public short[] computer_register;

		public short[] computer_names;

		public short[] player_register;

		public short[] teams;

		public short[] skirmish_needs_help;

		public short[] skirmish_player_requesting_type;

		public short[] skirmish_player_requesting_amount;

		public short[] skirmish_order;

		public short[] skirmish_order_player;

		public short[] skirmish_order_from_player;

		public byte[] mp_stats_valid;

		public byte[] lord_alive;

		public byte keep_enclosed;

		public byte can_make_bows;

		public byte can_make_mace;

		public byte can_make_spear;

		public byte messageFrom;

		public byte troops_show_make_arab_ballista;

		public byte starting_goods_level;

		public byte fairness;

		public uint elapsedTime;

		public byte balanced;

		public byte extremeEnabled;

		public short extremeCount;

		public byte mouse_selector_state;

		public byte flattenedHappened;

		public byte skirmishInsultFrom;

		public byte skirmishInsult;

		public string speechFileName;

		public string musicFileName;

		public string binkFileName;

		public byte lord_Type;

		public byte monk_available;

		public byte engineer_available;

		public byte ladderman_available;

		public byte[] team_shield;

		public byte resyncPercent;

		public byte messageFromcharacter;

		public short debug_value2;

		public byte laddermanCost;

		public byte eunuchCost;

		public byte spectatorMode;

		public byte customisedExtremeTrail;

		public int MAPEDITOR_numshieldsToDisplay;

		public bool MAPEDITOR_allowLandscapeEditing;

		public bool MP_TroopsCostGold => (gotSignpost & 1) > 0;

		public bool MP_AllowAutoTrading => (gotSignpost & 2) > 0;

		public bool MP_No_Cows => (gotSignpost & 4) > 0;

		public bool MP_No_Dogs => (gotSignpost & 8) > 0;

		public bool is_valid_player(int this_player)
		{
			if (this_player >= 1 && this_player < 9)
			{
				return player_register[this_player] != -1;
			}
			return false;
		}

		public bool is_skirmish_player(int this_player)
		{
			if (this_player >= 1 && this_player < 9)
			{
				return computer_register[this_player] != -1;
			}
			return false;
		}

		public bool is_human_or_skirmish_player(int this_player)
		{
			if (is_valid_player(this_player))
			{
				return true;
			}
			return is_skirmish_player(this_player);
		}
	}

	public struct ScoreReturnData
	{
		public int score_weapons;

		public int score_weapons_points;

		public int score;

		public int levelPoints;

		public int score_months;

		public int score_months_points;

		public int items_count;

		public int items_extra1;

		public int items_extra2;

		public int items_extra3;

		public int items_extra4;

		public int items_extra5;

		public int items_extra6;

		public int items_extra7;

		public int items_extra_points1;

		public int items_extra_points2;

		public int items_extra_points3;

		public int items_extra_points4;

		public int items_extra_points5;

		public int items_extra_points6;

		public int items_extra_points7;

		public int items_extra_type1;

		public int items_extra_type2;

		public int items_extra_type3;

		public int items_extra_type4;

		public int items_extra_type5;

		public int items_extra_type6;

		public int items_extra_type7;

		public int score_troops;

		public int troops_percent_lost;

		public int siege_that_score;

		public int siege_defenders_score;

		public int siege_attackers_score;

		public int difficulty_level;
	}

	public class ScoreData
	{
		public int score_weapons;

		public int score_weapons_points;

		public int score;

		public int levelPoints;

		public int score_months;

		public int score_months_points;

		public int items_count;

		public int[] items_extra;

		public int[] items_extra_points;

		public int[] items_extra_type;

		public int score_troops;

		public int troops_percent_lost;

		public int siege_that_score;

		public int siege_defenders_score;

		public int siege_attackers_score;

		public int difficulty_level;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct multiplayer_stats_export
	{
		[FieldOffset(0)]
		public unsafe fixed int valid[9];

		[FieldOffset(36)]
		public unsafe fixed int gold_acquired[9];

		[FieldOffset(72)]
		public unsafe fixed int max_population[9];

		[FieldOffset(108)]
		public unsafe fixed int fearfactor[9];

		[FieldOffset(144)]
		public unsafe fixed int time_deceased[9];

		[FieldOffset(180)]
		public unsafe fixed int who_killed_who[81];

		[FieldOffset(504)]
		public unsafe fixed int enemy_buildings_destroyed[9];

		[FieldOffset(540)]
		public unsafe fixed int food_produced[9];

		[FieldOffset(576)]
		public unsafe fixed int iron_produced[9];

		[FieldOffset(612)]
		public unsafe fixed int stone_produced[9];

		[FieldOffset(648)]
		public unsafe fixed int wood_produced[9];

		[FieldOffset(684)]
		public unsafe fixed int pitch_produced[9];

		[FieldOffset(720)]
		public unsafe fixed int minfearfactor[9];

		[FieldOffset(756)]
		public unsafe fixed int winners[9];

		[FieldOffset(792)]
		public unsafe fixed int troop_points_killed[9];

		[FieldOffset(828)]
		public unsafe fixed int enemy_buildings_razed_points[9];

		[FieldOffset(864)]
		public unsafe fixed int troops_produced[9];

		[FieldOffset(900)]
		public unsafe fixed int goods_received[9];

		[FieldOffset(936)]
		public unsafe fixed int goods_sent[9];

		[FieldOffset(972)]
		public unsafe fixed int notable_victories[9];

		[FieldOffset(1008)]
		public unsafe fixed int notable_defeats[9];

		[FieldOffset(1044)]
		public unsafe fixed int time_lord_killed[9];

		[FieldOffset(1080)]
		public unsafe fixed int blank2[9];

		[FieldOffset(1116)]
		public unsafe fixed int blank3[9];

		[FieldOffset(1152)]
		public unsafe fixed int blank4[9];

		[FieldOffset(1188)]
		public unsafe fixed int weapons_produced[9];

		[FieldOffset(1224)]
		public unsafe fixed int buildings_lost[9];

		[FieldOffset(1260)]
		public unsafe fixed int lords_killed[9];

		[FieldOffset(1296)]
		public unsafe fixed int team_shield[9];

		[FieldOffset(1332)]
		public unsafe fixed int computer_register[9];

		[FieldOffset(1368)]
		public int real_time;

		[FieldOffset(1372)]
		public int game_time;

		[FieldOffset(1376)]
		public int ranged_made;

		[FieldOffset(1380)]
		public int melee_made;

		[FieldOffset(1384)]
		public ulong unique;

		[FieldOffset(1392)]
		public unsafe fixed int teams[9];
	}

	[Serializable]
	public class MPScoreData
	{
		public int[] valid;

		public int[] gold_acquired;

		public int[] max_population;

		public int[] fearfactor;

		public int[] time_deceased;

		public int[] who_killed_who;

		public int[] enemy_buildings_destroyed;

		public int[] food_produced;

		public int[] iron_produced;

		public int[] stone_produced;

		public int[] wood_produced;

		public int[] pitch_produced;

		public int[] minfearfactor;

		public int[] winners;

		public int[] troop_points_killed;

		public int[] enemy_buildings_razed_points;

		public int[] troops_produced;

		public int[] goods_received;

		public int[] goods_sent;

		public int[] notable_victories;

		public int[] notable_defeats;

		public int[] time_lord_killed;

		public int[] blank2;

		public int[] blank3;

		public int[] blank4;

		public int[] weapons_produced;

		public int[] buildings_lost;

		public int[] lords_killed;

		public int[] team_shield;

		public int[] teams;

		public int[] computer_register;

		public int real_time;

		public int game_time;

		public int ranged_made;

		public int melee_made;

		public ulong unique;

		public int completedDate_Year;

		public int completedDate_Month;

		public int completedDate_Day;

		public int completedDate_Hour;

		public int completedDate_Minute;

		public int completedDate_Second;

		public int score;

		public int lord_type;

		public int numPlayers;

		public int trailLevel;

		public string mapName;

		public string[] playerName;

		public int[] colourMap1;

		public int[] colourMap2;

		public int version;

		public int index;

		public string trailName = "";

		public const int VERSION = 2;
	}

	public struct LogicDebugInfo
	{
		public int gfx_layer;

		public int gfx_layer_file;

		public int gfx_layer_id;

		public int alpha_gfx_layer;

		public int construction_gfx_layer;

		public int pillar_gfx_layer;

		public int pillar_gfx_layer_file;

		public int pillar_gfx_layer_id;

		public int wall_gfx_layer;

		public int wall_gfx_layer_file;

		public int wall_gfx_layer_id;

		public int floating_layer;

		public int random_layer;

		public int logic_layer;

		public int logic2_layer;

		public int changed_layer;

		public int organism_layer;

		public int structure_layer;

		public int structure_was_layer;

		public int chimp_layer;

		public int fly_layer;

		public int height_layer;

		public int default_height_layer;

		public int wall_owner_layer;

		public int luminesence_layer;

		public int show_hi_layer;

		public int misc_display_layer;

		public int damage_layer;

		public int macro_layer;

		public int path_connection_layer;

		public int path_linkage_layer;

		public int occupancy_layer;

		public int certain_path_layer;

		public int walk_layer;

		public int ai_zone_layer;

		public int ai_info_layer;

		public int ai_danger_layer;

		public int ai_proximity_layer;

		public int town_dz_spread_id;

		public int town_null_connects;

		public int town_dz_spread_count;

		public int town_stone_value;

		public int town_structure;

		public int town_oasis;

		public int town_farm;

		public int town_iron;

		public int problem_build;

		public int aiv_block_zone;

		public int delay_layer;

		public int aiv_block_layer;

		public int mapOfset;
	}

	public static object threadLock = new object();

	public const int CHIMPS_LIMIT_FAKE = 1;

	public const int CHIMPS_LIMIT = 10000;

	public static bool flattenedLandscape = false;

	public static int[] selectedChimps = new int[20000];

	public static bool FlattenedLandscape => flattenedLandscape;

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_PreInitMap_Editor(int mapSize, int mapType, bool siegeThat, bool multiplayerMap, byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_PreInitMap_Campaign(int difficulty);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_PreInitMap_EcoCampaign();

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_EcoCampaign_ChangeDifficulty(int difficulty);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_EcoCampaign_ChangeDifficulty_briefing(int difficulty, int* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_PreInitMap_SiegeThat(int difficulty, int playerID, int troop0, int troop1, int troop2, int troop3, int troop4, int troop5, int troop6, int troop7, int troop8, int troop9, int troop10, bool advancedMode);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_PreInitMap_Invasion(int difficulty, int restartInfoLength, byte* restartInfo);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_PreInitMap_EcoMap(int difficulty);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_PreInitMap_JustBuild(bool advancedFreebuild, int freebuild_GoldLevel, int freebuild_FoodLevel, int freebuild_ResourcesLevel, int freebuild_WeaponsLevel, int freebuild_RandomEvents, int freebuild_Invasions, int freebuild_InvasionDifficulty, int freebuild_Peacetime, int freebuild_Opponents, int restartInfoLength, bool removeHostileAnimals, bool freebuild_Extreme_Troops, bool freebuild_Extreme_Powers, bool freebuild_Defeat_On_Death, byte* restartInfo);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_PreInitMap_Multiplayer(byte* retData, bool skirmishGame, int restartInfoLength, byte* restartInfo, int coopTrailID, int coopMissionID, bool trailMakerTestMode, bool customTrail, bool customisedExtremeTrail);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_PreInitTutorial();

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_ApplyMultiplayerSetupData(byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetMultiplayerSetupData(byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_RegisterMultiplayerUser(int playerID, byte* name, int nameLength, int team, bool localPlayer, int lordType);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_RegisterSkirmishUser(int playerID, int AILord, int subType, int team);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_SetExtendedLordConfig(byte* retData, int playerID);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_StartMultiplayerGame(bool fromSave);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_SetMPRandSeed(int seed);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_StartMultiplayerGameSynced();

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_LoadSaveGame(byte* data, int length, byte* retData, bool loadingEditorMap);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_ReceiveChore(int playerID, byte* data, int length);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetMultiplayerChatInfo(int* players, int* teams);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_KickMPPlayer(int kickPlayerID, bool immediate);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_PromoteMPHost(int hostID);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_TriggerMPSave(byte* data, int length);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_TriggerMPLoad(byte* data, int length);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_RemapPlayers(int* newMappings, int newLocalPlayer);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_ConnectionPause(bool pause);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_SaveSaveGame(byte* data, int length, int screenCentreX, int screenCentreY, int realScreenCentreX, int realScreenCentreY, bool lockMap, bool tempLockOnly, bool mapSave);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_SetMPRadarColours(int* newMappings);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_LoadMapToPlay(int campaignMapID, byte* fileName, int length, byte* retData, bool dummy, byte* mapName, int maplength, bool multiplayerSave, int trailType, int trailID, bool allow_classic_bedouins);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_CreateTrailMission(byte* fileName, int length, byte* restartInfo, int restartInfoLength);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_CampaignLevel(byte* path, int length);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetColourMapping(int* retData, int remappedPlayer);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetTrailMissionLords(int* retData, int trail, int trailID);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetTrailMissionInfo(int* retData, byte* mapName, int trail, int trailID);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_RunTick(short* data, byte* radarMap, bool flattenedLandscape, int mouseOverX, int mouseOverY, bool shiftPressed, bool ctrlPressed, bool altPressed, byte* retData, bool paused, bool ambientSoundChannel1, bool ambientSoundChannel2, bool speechSoundChannel1, bool speechSoundChannel2, bool musicPlaying, bool musicAboutToLoop, bool binkPlaying, int screenCentrePosX, int screenCentrePosY, int screenTilesWide, int screenTilesHigh, int radarMapWidth, int radarMapHeight, int radarZoom, int screenZoom, bool SH1RtsControls, bool troopModeMode, int screenCentreTileX, int screenCentreTileY, byte* choreBuffer, int* selectedChimpsBuffer, bool mpFrameSkip, int buildingOverDepth, int troopOverdepth, int scrBoundsLeft, int scrBoundsRight, int scrBoundsTop, int scrBoundsBottom);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_ImportAIV(int AILord, int ID, short* data, int length, int custom);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_SetPath(byte* data, int length, byte* autoData, int autoLength, byte* saveFolderData, int saveFolderLength);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_Unpack(byte* source, byte* dest, int destBufferLength);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_Pack(byte* source, byte* dest, int sourceLength);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_GetunpackSize(byte* source);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_UnpackRadarToARGB(byte* source, byte* dest);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_CRC(byte* source, int size);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_CRCS(short* source, int size);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_GetSaveRadar(byte* dest, int* keeps);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_SetMapRotation(int rotation, int centreX, int centreY);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_StartMapAction(int action);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_MapAction(int action, int map_x, int map_y, int brushSize, int playerID, bool inGameNotMapEditor, bool constructingOnly, int mouseState);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_GameAction(int action, int structureID, int value, int value2);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_TroopSelection(int mouseState, bool rightDown, bool rightUp, int count, int* selectedChimps, bool selection_on, bool selection_established, int underCursorCount, int* underCursorChimps, int mousePosX, int mousePosY, bool overTopHalf, int onScreenCount, int* onScreenChimps);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_TroopSelectionChanged(int count, int* selectedChimps);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_GetMapperSize(int action);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_IsMapperAvailable(int mapper);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_GetMapperCoord(int mapper);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_SetAchValues(int food, int wood, int weapons);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_ImportTrailTimes(int trailType, int* times, bool handleExceptions);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_SetEditorPlayer(int playerID);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_SetUTF8MissionText(byte* text, int length);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_SetUTF8MapName(byte* text, int length, byte* text2, int length2);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetScenarioOverview(byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_CreateScenarioAction(int action);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern bool DLL_GetScenarioEvent(int eventID, byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern bool DLL_GetScenarioInvasion(int eventID, byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern bool DLL_ApplyScenarioEvent(int eventID, byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern bool DLL_ApplyScenarioInvasion(int eventID, byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_DeleteScenarioAction(int eventID);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_UpdateScenarioActionDate(int entryID, int year, int month);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_SetMapEditorParam(int SPMPMode, int gameType, int koth, int mapSize);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_SetAppMode(int app_mode, int app_sub_mode);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern void DLL_TutorialAction(int ID, int value);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetMeritData(int* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetScoreData(byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern void DLL_GetMPScoreData(byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public static extern int DLL_SetDebugMode(int action);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_GetLayerDebug(int x, int y, byte* retData);

	[DllImport("CrusaderDE")]
	[MockNativeDeclaration]
	public unsafe static extern int DLL_GetLayerData(int layedID, byte* retData);

	public unsafe static void ImportAIV(int AILord, int ID, short[] data, int custom)
	{
		lock (threadLock)
		{
			fixed (short* data2 = data)
			{
				DLL_ImportAIV(AILord, ID, data2, data.Length, custom);
			}
		}
	}

	public unsafe static void InitAIVLoading()
	{
		lock (threadLock)
		{
			DLL_ImportAIV(-1, -1, null, 0, 0);
		}
	}

	public unsafe static void sendPath(string gameDataPath, string multiplayerAutoSaveName, string saveFolder)
	{
		byte[] bytes = Encoding.Unicode.GetBytes(gameDataPath);
		byte[] bytes2 = Encoding.Unicode.GetBytes(multiplayerAutoSaveName);
		byte[] bytes3 = Encoding.Unicode.GetBytes(saveFolder);
		fixed (byte* data = bytes)
		{
			fixed (byte* autoData = bytes2)
			{
				fixed (byte* saveFolderData = bytes3)
				{
					DLL_SetPath(data, bytes.Length, autoData, bytes2.Length, saveFolderData, bytes3.Length);
				}
			}
		}
	}

	public unsafe static void TriggerMPLoad(string filename)
	{
		byte[] bytes = Encoding.Unicode.GetBytes(filename);
		fixed (byte* data = bytes)
		{
			DLL_TriggerMPLoad(data, bytes.Length);
		}
	}

	public unsafe static void TriggerMPSave(string savename)
	{
		lock (threadLock)
		{
			byte[] bytes = Encoding.Unicode.GetBytes(savename);
			fixed (byte* data = bytes)
			{
				DLL_TriggerMPSave(data, bytes.Length);
			}
		}
	}

	public static MultiplayerSetupData getMPSetup(MultiplayerSetupTransferData source)
	{
		MultiplayerSetupData obj = new MultiplayerSetupData
		{
			fairness = source.fairness,
			starting_gamespeed = source.starting_gamespeed,
			starting_goods_level = source.starting_goods_level,
			win_condition = source.win_condition,
			allow_autotrading = source.allow_autotrading,
			no_knockdown_walls = source.no_knockdown_walls,
			autosave = source.autosave,
			peacetime = source.peacetime,
			no_cows = source.no_cows,
			no_dogs = source.no_dogs
		};
		obj.start_keep_location_order[0] = source.start_keep_location_order0;
		obj.start_keep_location_order[1] = source.start_keep_location_order1;
		obj.start_keep_location_order[2] = source.start_keep_location_order2;
		obj.start_keep_location_order[3] = source.start_keep_location_order3;
		obj.start_keep_location_order[4] = source.start_keep_location_order4;
		obj.start_keep_location_order[5] = source.start_keep_location_order5;
		obj.start_keep_location_order[6] = source.start_keep_location_order6;
		obj.start_keep_location_order[7] = source.start_keep_location_order7;
		obj.extreme_troops = source.extreme_troops;
		obj.extreme_powers = source.extreme_powers;
		obj.extreme_powers_around_lord = source.extreme_powers_around_lord;
		obj.allow_outposts = source.allow_outposts;
		obj.advanced_options = source.advanced_options;
		obj.advanced_skirmish_options = source.advanced_skirmish_options;
		obj.advopt_pre_build = source.advopt_pre_build;
		obj.advopt_improved_arabswordsmen = source.advopt_improved_arabswordsmen;
		obj.advopt_improved_laddermen = source.advopt_improved_laddermen;
		obj.advopt_improved_spearmen = source.advopt_improved_spearmen;
		obj.advopt_rebalanced_horsearchers = source.advopt_rebalanced_horsearchers;
		obj.advopt_improved_fletchers = source.advopt_improved_fletchers;
		obj.advopt_uncapped_peasants = source.advopt_uncapped_peasants;
		obj.advopt_faster_peasants = source.advopt_faster_peasants;
		obj.advopt_enemy_hps = source.advopt_enemy_hps;
		obj.global_improved_sieging = source.global_improved_sieging;
		obj.global_improved_sieging2 = source.global_improved_sieging2;
		obj.advopt_healers = source.advopt_healers;
		obj.advopt_eunuchs = source.advopt_eunuchs;
		obj.advopt_nogold = source.advopt_nogold;
		obj.MP_BuildingsAvailable[0] = source.MP_BuildingsAvailable0;
		obj.MP_BuildingsAvailable[1] = source.MP_BuildingsAvailable1;
		obj.MP_BuildingsAvailable[2] = source.MP_BuildingsAvailable2;
		obj.MP_BuildingsAvailable[3] = source.MP_BuildingsAvailable3;
		obj.MP_BuildingsAvailable[4] = source.MP_BuildingsAvailable4;
		obj.MP_BuildingsAvailable[5] = source.MP_BuildingsAvailable5;
		obj.MP_BuildingsAvailable[6] = source.MP_BuildingsAvailable6;
		obj.MP_BuildingsAvailable[7] = source.MP_BuildingsAvailable7;
		obj.MP_BuildingsAvailable[8] = source.MP_BuildingsAvailable8;
		obj.MP_BuildingsAvailable[9] = source.MP_BuildingsAvailable9;
		obj.MP_BuildingsAvailable[10] = source.MP_BuildingsAvailable10;
		obj.MP_BuildingsAvailable[11] = source.MP_BuildingsAvailable11;
		obj.MP_BuildingsAvailable[12] = source.MP_BuildingsAvailable12;
		obj.MP_GoodsAvailable[0] = source.MP_GoodsAvailable0;
		obj.MP_GoodsAvailable[1] = source.MP_GoodsAvailable1;
		obj.MP_GoodsAvailable[2] = source.MP_GoodsAvailable2;
		obj.MP_GoodsAvailable[3] = source.MP_GoodsAvailable3;
		obj.MP_GoodsAvailable[4] = source.MP_GoodsAvailable4;
		obj.MP_GoodsAvailable[5] = source.MP_GoodsAvailable5;
		obj.MP_GoodsAvailable[6] = source.MP_GoodsAvailable6;
		obj.MP_GoodsAvailable[7] = source.MP_GoodsAvailable7;
		obj.MP_GoodsAvailable[8] = source.MP_GoodsAvailable8;
		obj.MP_GoodsAvailable[9] = source.MP_GoodsAvailable9;
		obj.MP_GoodsAvailable[10] = source.MP_GoodsAvailable10;
		obj.MP_GoodsAvailable[11] = source.MP_GoodsAvailable11;
		obj.MP_GoodsAvailable[12] = source.MP_GoodsAvailable12;
		obj.MP_GoodsAvailable[13] = source.MP_GoodsAvailable13;
		obj.MP_GoodsAvailable[14] = source.MP_GoodsAvailable14;
		obj.MP_GoodsAvailable[15] = source.MP_GoodsAvailable15;
		obj.MP_GoodsAvailable[16] = source.MP_GoodsAvailable16;
		obj.MP_GoodsAvailable[17] = source.MP_GoodsAvailable17;
		obj.MP_GoodsAvailable[18] = source.MP_GoodsAvailable18;
		obj.MP_GoodsAvailable[19] = source.MP_GoodsAvailable19;
		obj.MP_GoodsAvailable[20] = source.MP_GoodsAvailable20;
		obj.MP_GoodsAvailable[21] = source.MP_GoodsAvailable21;
		obj.MP_GoodsAvailable[22] = source.MP_GoodsAvailable22;
		obj.MP_GoodsAvailable[23] = source.MP_GoodsAvailable23;
		obj.MP_GoodsAvailable[24] = source.MP_GoodsAvailable24;
		obj.MP_TroopsAvailable[0] = source.MP_TroopsAvailable0;
		obj.MP_TroopsAvailable[1] = source.MP_TroopsAvailable1;
		obj.MP_TroopsAvailable[2] = source.MP_TroopsAvailable2;
		obj.MP_TroopsAvailable[3] = source.MP_TroopsAvailable3;
		obj.MP_TroopsAvailable[4] = source.MP_TroopsAvailable4;
		obj.MP_TroopsAvailable[5] = source.MP_TroopsAvailable5;
		obj.MP_TroopsAvailable[6] = source.MP_TroopsAvailable6;
		obj.MP_TroopsAvailable[7] = source.MP_TroopsAvailable7;
		obj.MP_TroopsAvailable[8] = source.MP_TroopsAvailable8;
		obj.MP_TroopsAvailable[9] = source.MP_TroopsAvailable9;
		obj.MP_TroopsAvailable[10] = source.MP_TroopsAvailable10;
		obj.MP_TroopsAvailable[11] = source.MP_TroopsAvailable11;
		obj.MP_TroopsAvailable[12] = source.MP_TroopsAvailable12;
		obj.MP_TroopsAvailable[13] = source.MP_TroopsAvailable13;
		obj.MP_TroopsAvailable[14] = source.MP_TroopsAvailable14;
		obj.MP_TroopsAvailable[15] = source.MP_TroopsAvailable15;
		obj.MP_TroopsAvailable[16] = source.MP_TroopsAvailable16;
		obj.MP_TroopsAvailable[17] = source.MP_TroopsAvailable17;
		obj.MP_TroopsAvailable[18] = source.MP_TroopsAvailable18;
		obj.MP_TroopsAvailable[19] = source.MP_TroopsAvailable19;
		obj.MP_TroopsAvailable[20] = source.MP_TroopsAvailable20;
		obj.MP_TroopsAvailable[21] = source.MP_TroopsAvailable21;
		obj.MP_TroopsAvailable[22] = source.MP_TroopsAvailable22;
		obj.MP_TroopsAvailable[23] = source.MP_TroopsAvailable23;
		obj.MP_TroopsAvailable[24] = source.MP_TroopsAvailable24;
		obj.MP_TroopsAvailable[25] = source.MP_TroopsAvailable25;
		obj.MP_TroopsAvailable[26] = source.MP_TroopsAvailable26;
		obj.MP_TroopsAvailable[27] = source.MP_TroopsAvailable27;
		obj.MP_TroopsAvailable[28] = source.MP_TroopsAvailable28;
		obj.MP_TroopsAvailable[29] = source.MP_TroopsAvailable29;
		obj.MP_TroopsAvailable[30] = source.MP_TroopsAvailable30;
		obj.MP_TroopsAvailable[31] = source.MP_TroopsAvailable31;
		obj.preferredAIVs[0] = source.preferredAIVs0;
		obj.preferredAIVs[1] = source.preferredAIVs1;
		obj.preferredAIVs[2] = source.preferredAIVs2;
		obj.preferredAIVs[3] = source.preferredAIVs3;
		obj.preferredAIVs[4] = source.preferredAIVs4;
		obj.preferredAIVs[5] = source.preferredAIVs5;
		obj.preferredAIVs[6] = source.preferredAIVs6;
		obj.preferredAIVs[7] = source.preferredAIVs7;
		return obj;
	}

	public static MultiplayerSetupTransferData setMPSetup(MultiplayerSetupData source)
	{
		return new MultiplayerSetupTransferData
		{
			fairness = source.fairness,
			starting_gamespeed = source.starting_gamespeed,
			starting_goods_level = source.starting_goods_level,
			win_condition = source.win_condition,
			allow_autotrading = source.allow_autotrading,
			no_knockdown_walls = source.no_knockdown_walls,
			autosave = source.autosave,
			peacetime = source.peacetime,
			no_cows = source.no_cows,
			no_dogs = source.no_dogs,
			start_keep_location_order0 = source.start_keep_location_order[0],
			start_keep_location_order1 = source.start_keep_location_order[1],
			start_keep_location_order2 = source.start_keep_location_order[2],
			start_keep_location_order3 = source.start_keep_location_order[3],
			start_keep_location_order4 = source.start_keep_location_order[4],
			start_keep_location_order5 = source.start_keep_location_order[5],
			start_keep_location_order6 = source.start_keep_location_order[6],
			start_keep_location_order7 = source.start_keep_location_order[7],
			extreme_troops = source.extreme_troops,
			extreme_powers = source.extreme_powers,
			extreme_powers_around_lord = source.extreme_powers_around_lord,
			allow_outposts = source.allow_outposts,
			advanced_options = source.advanced_options,
			advanced_skirmish_options = source.advanced_skirmish_options,
			advopt_pre_build = source.advopt_pre_build,
			advopt_improved_arabswordsmen = source.advopt_improved_arabswordsmen,
			advopt_improved_laddermen = source.advopt_improved_laddermen,
			advopt_improved_spearmen = source.advopt_improved_spearmen,
			advopt_rebalanced_horsearchers = source.advopt_rebalanced_horsearchers,
			advopt_improved_fletchers = source.advopt_improved_fletchers,
			advopt_uncapped_peasants = source.advopt_uncapped_peasants,
			advopt_faster_peasants = source.advopt_faster_peasants,
			advopt_enemy_hps = source.advopt_enemy_hps,
			global_improved_sieging = source.global_improved_sieging,
			global_improved_sieging2 = source.global_improved_sieging2,
			advopt_healers = source.advopt_healers,
			advopt_eunuchs = source.advopt_eunuchs,
			advopt_nogold = source.advopt_nogold,
			MP_BuildingsAvailable0 = source.MP_BuildingsAvailable[0],
			MP_BuildingsAvailable1 = source.MP_BuildingsAvailable[1],
			MP_BuildingsAvailable2 = source.MP_BuildingsAvailable[2],
			MP_BuildingsAvailable3 = source.MP_BuildingsAvailable[3],
			MP_BuildingsAvailable4 = source.MP_BuildingsAvailable[4],
			MP_BuildingsAvailable5 = source.MP_BuildingsAvailable[5],
			MP_BuildingsAvailable6 = source.MP_BuildingsAvailable[6],
			MP_BuildingsAvailable7 = source.MP_BuildingsAvailable[7],
			MP_BuildingsAvailable8 = source.MP_BuildingsAvailable[8],
			MP_BuildingsAvailable9 = source.MP_BuildingsAvailable[9],
			MP_BuildingsAvailable10 = source.MP_BuildingsAvailable[10],
			MP_BuildingsAvailable11 = source.MP_BuildingsAvailable[11],
			MP_BuildingsAvailable12 = source.MP_BuildingsAvailable[12],
			MP_GoodsAvailable0 = source.MP_GoodsAvailable[0],
			MP_GoodsAvailable1 = source.MP_GoodsAvailable[1],
			MP_GoodsAvailable2 = source.MP_GoodsAvailable[2],
			MP_GoodsAvailable3 = source.MP_GoodsAvailable[3],
			MP_GoodsAvailable4 = source.MP_GoodsAvailable[4],
			MP_GoodsAvailable5 = source.MP_GoodsAvailable[5],
			MP_GoodsAvailable6 = source.MP_GoodsAvailable[6],
			MP_GoodsAvailable7 = source.MP_GoodsAvailable[7],
			MP_GoodsAvailable8 = source.MP_GoodsAvailable[8],
			MP_GoodsAvailable9 = source.MP_GoodsAvailable[9],
			MP_GoodsAvailable10 = source.MP_GoodsAvailable[10],
			MP_GoodsAvailable11 = source.MP_GoodsAvailable[11],
			MP_GoodsAvailable12 = source.MP_GoodsAvailable[12],
			MP_GoodsAvailable13 = source.MP_GoodsAvailable[13],
			MP_GoodsAvailable14 = source.MP_GoodsAvailable[14],
			MP_GoodsAvailable15 = source.MP_GoodsAvailable[15],
			MP_GoodsAvailable16 = source.MP_GoodsAvailable[16],
			MP_GoodsAvailable17 = source.MP_GoodsAvailable[17],
			MP_GoodsAvailable18 = source.MP_GoodsAvailable[18],
			MP_GoodsAvailable19 = source.MP_GoodsAvailable[19],
			MP_GoodsAvailable20 = source.MP_GoodsAvailable[20],
			MP_GoodsAvailable21 = source.MP_GoodsAvailable[21],
			MP_GoodsAvailable22 = source.MP_GoodsAvailable[22],
			MP_GoodsAvailable23 = source.MP_GoodsAvailable[23],
			MP_GoodsAvailable24 = source.MP_GoodsAvailable[24],
			MP_TroopsAvailable0 = source.MP_TroopsAvailable[0],
			MP_TroopsAvailable1 = source.MP_TroopsAvailable[1],
			MP_TroopsAvailable2 = source.MP_TroopsAvailable[2],
			MP_TroopsAvailable3 = source.MP_TroopsAvailable[3],
			MP_TroopsAvailable4 = source.MP_TroopsAvailable[4],
			MP_TroopsAvailable5 = source.MP_TroopsAvailable[5],
			MP_TroopsAvailable6 = source.MP_TroopsAvailable[6],
			MP_TroopsAvailable7 = source.MP_TroopsAvailable[7],
			MP_TroopsAvailable8 = source.MP_TroopsAvailable[8],
			MP_TroopsAvailable9 = source.MP_TroopsAvailable[9],
			MP_TroopsAvailable10 = source.MP_TroopsAvailable[10],
			MP_TroopsAvailable11 = source.MP_TroopsAvailable[11],
			MP_TroopsAvailable12 = source.MP_TroopsAvailable[12],
			MP_TroopsAvailable13 = source.MP_TroopsAvailable[13],
			MP_TroopsAvailable14 = source.MP_TroopsAvailable[14],
			MP_TroopsAvailable15 = source.MP_TroopsAvailable[15],
			MP_TroopsAvailable16 = source.MP_TroopsAvailable[16],
			MP_TroopsAvailable17 = source.MP_TroopsAvailable[17],
			MP_TroopsAvailable18 = source.MP_TroopsAvailable[18],
			MP_TroopsAvailable19 = source.MP_TroopsAvailable[19],
			MP_TroopsAvailable20 = source.MP_TroopsAvailable[20],
			MP_TroopsAvailable21 = source.MP_TroopsAvailable[21],
			MP_TroopsAvailable22 = source.MP_TroopsAvailable[22],
			MP_TroopsAvailable23 = source.MP_TroopsAvailable[23],
			MP_TroopsAvailable24 = source.MP_TroopsAvailable[24],
			MP_TroopsAvailable25 = source.MP_TroopsAvailable[25],
			MP_TroopsAvailable26 = source.MP_TroopsAvailable[26],
			MP_TroopsAvailable27 = source.MP_TroopsAvailable[27],
			MP_TroopsAvailable28 = source.MP_TroopsAvailable[28],
			MP_TroopsAvailable29 = source.MP_TroopsAvailable[29],
			MP_TroopsAvailable30 = source.MP_TroopsAvailable[30],
			MP_TroopsAvailable31 = source.MP_TroopsAvailable[31],
			preferredAIVs0 = source.preferredAIVs[0],
			preferredAIVs1 = source.preferredAIVs[1],
			preferredAIVs2 = source.preferredAIVs[2],
			preferredAIVs3 = source.preferredAIVs[3],
			preferredAIVs4 = source.preferredAIVs[4],
			preferredAIVs5 = source.preferredAIVs[5],
			preferredAIVs6 = source.preferredAIVs[6],
			preferredAIVs7 = source.preferredAIVs[7]
		};
	}

	public static AILordConfigTransferData CreateAILordConfigData(CustomisationFileManager.NewAIC source)
	{
		return new AILordConfigTransferData
		{
			opponent_type = source.opponent_type,
			opponent_type_for_speech = source.opponent_type_for_speech,
			lord_gfx_type = source.lord_gfx_type,
			flag_type = source.flag_type,
			use_of_religion = source.use_of_religion,
			use_of_ale = source.use_of_ale,
			vlow_popularity = source.vlow_popularity,
			low_popularity = source.low_popularity,
			high_popularity = source.high_popularity,
			min_tax = source.min_tax,
			max_tax = source.max_tax,
			farm_types1 = source.farm_types[0],
			farm_types2 = source.farm_types[1],
			farm_types3 = source.farm_types[2],
			farm_types4 = source.farm_types[3],
			farm_types5 = source.farm_types[4],
			farm_types6 = source.farm_types[5],
			farm_types7 = source.farm_types[6],
			farm_types8 = source.farm_types[7],
			people_to_farm_ratio = source.people_to_farm_ratio,
			extract_wood_ratio = source.extract_wood_ratio,
			extract_stone_ratio = source.extract_stone_ratio,
			extract_iron_ratio = source.extract_iron_ratio,
			extract_pitch_ratio = source.extract_pitch_ratio,
			max_quarries = source.max_quarries,
			max_mines = source.max_mines,
			max_woodcutters = source.max_woodcutters,
			max_pitch_dugouts = source.max_pitch_dugouts,
			max_farms = source.max_farms,
			build_rate = source.build_rate,
			crushed_building_delay = source.crushed_building_delay,
			sell_food_at = source.sell_food_at,
			buy_apples_at = source.buy_apples_at,
			buy_cheese_at = source.buy_cheese_at,
			buy_bread_at = source.buy_bread_at,
			buy_wheat_at = source.buy_wheat_at,
			buy_hops_at = source.buy_hops_at,
			buy_food_amount = source.buy_food_amount,
			buy_weapons = source.buy_weapons,
			pester_for_goods_delay = source.pester_for_goods_delay,
			send_goods_margin = source.send_goods_margin,
			ration_boost = source.ration_boost,
			trade_wood_at = source.trade_wood_at,
			trade_stone_at = source.trade_stone_at,
			trade_resources_at = source.trade_resources_at,
			trade_flour_at = source.trade_flour_at,
			trade_weapons_at = source.trade_weapons_at,
			trade_ale_at = source.trade_ale_at,
			trade_pitch_at = source.trade_pitch_at,
			trade_minimum = source.trade_minimum,
			base_gold_reserves = source.base_gold_reserves,
			blacksmiths_make = source.blacksmiths_make,
			fletchers_make = source.fletchers_make,
			poleturners_make = source.poleturners_make,
			sell_all1 = source.sell_all[0],
			sell_all2 = source.sell_all[1],
			sell_all3 = source.sell_all[2],
			sell_all4 = source.sell_all[3],
			sell_all5 = source.sell_all[4],
			sell_all6 = source.sell_all[5],
			sell_all7 = source.sell_all[6],
			sell_all8 = source.sell_all[7],
			sell_all9 = source.sell_all[8],
			sell_all10 = source.sell_all[9],
			sell_all11 = source.sell_all[10],
			sell_all12 = source.sell_all[11],
			sell_all13 = source.sell_all[12],
			sell_all14 = source.sell_all[13],
			sell_all15 = source.sell_all[14],
			move_mobile_defenders = source.move_mobile_defenders,
			max_mobile_groups = source.max_mobile_groups,
			buy_defense_machines_at = source.buy_defense_machines_at,
			buy_defense_machines_delay = source.buy_defense_machines_delay,
			dog_release_timing = source.dog_release_timing,
			dog_points_count = source.dog_points_count,
			chance_of_defensive1 = source.chance_of_defensive[0],
			chance_of_defensive2 = source.chance_of_defensive[1],
			chance_of_defensive3 = source.chance_of_defensive[2],
			chance_of_harrasment1 = source.chance_of_harrasment[0],
			chance_of_harrasment2 = source.chance_of_harrasment[1],
			chance_of_harrasment3 = source.chance_of_harrasment[2],
			chance_of_seiging1 = source.chance_of_seiging[0],
			chance_of_seiging2 = source.chance_of_seiging[1],
			chance_of_seiging3 = source.chance_of_seiging[2],
			economy_protection_number = source.economy_protection_number,
			economy_protection_type = source.economy_protection_type,
			bodyguard_number = source.bodyguard_number,
			bodyguard_type = source.bodyguard_type,
			moat_diggers = source.moat_diggers,
			moat_digger_type = source.moat_digger_type,
			troop_production_rate1 = source.troop_production_rate[0],
			troop_production_rate2 = source.troop_production_rate[1],
			troop_production_rate3 = source.troop_production_rate[2],
			defense_patrol_trigger_level = source.defense_patrol_trigger_level,
			defense_patrols = source.defense_patrols,
			defense_patrol_style = source.defense_patrol_style,
			defense_patrol_delay = source.defense_patrol_delay,
			defensive_trigger_level = source.defensive_trigger_level,
			defensive_troops1 = source.defensive_troops[0],
			defensive_troops2 = source.defensive_troops[1],
			defensive_troops3 = source.defensive_troops[2],
			defensive_troops4 = source.defensive_troops[3],
			defensive_troops5 = source.defensive_troops[4],
			defensive_troops6 = source.defensive_troops[5],
			defensive_troops7 = source.defensive_troops[6],
			defensive_troops8 = source.defensive_troops[7],
			harrasment_trigger_level = source.harrasment_trigger_level,
			harrasment_trigger_variance = source.harrasment_trigger_variance,
			harrasment_troops1 = source.harrasment_troops[0],
			harrasment_troops2 = source.harrasment_troops[1],
			harrasment_troops3 = source.harrasment_troops[2],
			harrasment_troops4 = source.harrasment_troops[3],
			harrasment_troops5 = source.harrasment_troops[4],
			harrasment_troops6 = source.harrasment_troops[5],
			harrasment_troops7 = source.harrasment_troops[6],
			harrasment_troops8 = source.harrasment_troops[7],
			harrasment_machines1 = source.harrasment_machines[0],
			harrasment_machines2 = source.harrasment_machines[1],
			harrasment_machines3 = source.harrasment_machines[2],
			harrasment_machines4 = source.harrasment_machines[3],
			harrasment_machines5 = source.harrasment_machines[4],
			harrasment_machines6 = source.harrasment_machines[5],
			harrasment_machines7 = source.harrasment_machines[6],
			harrasment_machines8 = source.harrasment_machines[7],
			max_harrasment_machines = source.max_harrasment_machines,
			harrass_delay = source.harrass_delay,
			siege_trigger_level = source.siege_trigger_level,
			siege_trigger_variance = source.siege_trigger_variance,
			siege_troops_before_will_come_to_rescue = source.siege_troops_before_will_come_to_rescue,
			siege_troops_on_site_percent = source.siege_troops_on_site_percent,
			siege_troops_at_home_percent = source.siege_troops_at_home_percent,
			siege_soften_up_delay = source.siege_soften_up_delay,
			siege_victory_delay = source.siege_victory_delay,
			percent_chance_waiting_for_joint_attack = source.percent_chance_waiting_for_joint_attack,
			siege_machines1 = source.siege_machines[0],
			siege_machines2 = source.siege_machines[1],
			siege_machines3 = source.siege_machines[2],
			siege_machines4 = source.siege_machines[3],
			siege_machines5 = source.siege_machines[4],
			siege_machines6 = source.siege_machines[5],
			siege_machines7 = source.siege_machines[6],
			siege_machines8 = source.siege_machines[7],
			siege_cow_timer = source.siege_cow_timer,
			siege_eng_amount = source.siege_eng_amount,
			siege_moat_troop = source.siege_moat_troop,
			siege_moat_amount = source.siege_moat_amount,
			siege_herring_troop = source.siege_herring_troop,
			siege_herring_amount = source.siege_herring_amount,
			siege_assasin_amount = source.siege_assasin_amount,
			siege_ladder_amount = source.siege_ladder_amount,
			siege_tunnel_amount = source.siege_tunnel_amount,
			siege_storm_troop = source.siege_storm_troop,
			siege_storm_amount = source.siege_storm_amount,
			siege_storm_tribes = source.siege_storm_tribes,
			siege_cover_troop = source.siege_cover_troop,
			siege_cover_amount = source.siege_cover_amount,
			siege_cover_tribes = source.siege_cover_tribes,
			siege_shock_troop = source.siege_shock_troop,
			siege_shock_amount = source.siege_shock_amount,
			siege_reserve_troop = source.siege_reserve_troop,
			siege_reserve_amount = source.siege_reserve_amount,
			siege_reserve_tribes = source.siege_reserve_tribes,
			siege_wall_troops1 = source.siege_wall_troops[0],
			siege_wall_troops2 = source.siege_wall_troops[1],
			siege_wall_troops3 = source.siege_wall_troops[2],
			siege_wall_troops4 = source.siege_wall_troops[3],
			siege_wall_troops5 = source.siege_wall_troops[4],
			siege_wall_troops6 = source.siege_wall_troops[5],
			siege_wall_troops7 = source.siege_wall_troops[6],
			siege_wall_troops8 = source.siege_wall_troops[7],
			siege_wall_troops9 = source.siege_wall_troops[8],
			siege_wall_troops10 = source.siege_wall_troops[9],
			siege_wall_troops11 = source.siege_wall_troops[10],
			siege_wall_troops12 = source.siege_wall_troops[11],
			siege_wall_troops13 = source.siege_wall_troops[12],
			siege_wall_troops14 = source.siege_wall_troops[13],
			siege_wall_troops15 = source.siege_wall_troops[14],
			siege_wall_troops16 = source.siege_wall_troops[15],
			siege_wall_troops17 = source.siege_wall_troops[16],
			siege_wall_troops18 = source.siege_wall_troops[17],
			siege_wall_troops19 = source.siege_wall_troops[18],
			siege_wall_troops20 = source.siege_wall_troops[19],
			siege_wall_troops21 = source.siege_wall_troops[20],
			siege_wall_troops22 = source.siege_wall_troops[21],
			siege_wall_troops23 = source.siege_wall_troops[22],
			siege_wall_troops24 = source.siege_wall_troops[23],
			siege_wall_amount = source.siege_wall_amount,
			siege_wall_tribes = source.siege_wall_tribes,
			who_to_pick_on = source.who_to_pick_on,
			use_improved_sieging = source.use_improved_sieging,
			starting_troops_normal1 = source.starting_troops_normal[0],
			starting_troops_normal2 = source.starting_troops_normal[1],
			starting_troops_normal3 = source.starting_troops_normal[2],
			starting_troops_normal4 = source.starting_troops_normal[3],
			starting_troops_normal5 = source.starting_troops_normal[4],
			starting_troops_normal6 = source.starting_troops_normal[5],
			starting_troops_normal7 = source.starting_troops_normal[6],
			starting_troops_normal8 = source.starting_troops_normal[7],
			starting_troops_normal9 = source.starting_troops_normal[8],
			starting_troops_normal10 = source.starting_troops_normal[9],
			starting_troops_normal11 = source.starting_troops_normal[10],
			starting_troops_normal12 = source.starting_troops_normal[11],
			starting_troops_normal13 = source.starting_troops_normal[12],
			starting_troops_normal14 = source.starting_troops_normal[13],
			starting_troops_normal15 = source.starting_troops_normal[14],
			starting_troops_normal16 = source.starting_troops_normal[15],
			starting_troops_normal17 = source.starting_troops_normal[16],
			starting_troops_normal18 = source.starting_troops_normal[17],
			starting_troops_normal19 = source.starting_troops_normal[18],
			starting_troops_normal20 = source.starting_troops_normal[19],
			starting_troops_normal21 = source.starting_troops_normal[20],
			starting_troops_normal22 = source.starting_troops_normal[21],
			starting_troops_normal23 = source.starting_troops_normal[22],
			starting_troops_normal24 = source.starting_troops_normal[23],
			starting_troops_normal25 = source.starting_troops_normal[24],
			starting_troops_normal26 = source.starting_troops_normal[25],
			starting_troops_normal27 = source.starting_troops_normal[26],
			starting_troops_normal28 = source.starting_troops_normal[27],
			starting_troops_deathmatch1 = source.starting_troops_deathmatch[0],
			starting_troops_deathmatch2 = source.starting_troops_deathmatch[1],
			starting_troops_deathmatch3 = source.starting_troops_deathmatch[2],
			starting_troops_deathmatch4 = source.starting_troops_deathmatch[3],
			starting_troops_deathmatch5 = source.starting_troops_deathmatch[4],
			starting_troops_deathmatch6 = source.starting_troops_deathmatch[5],
			starting_troops_deathmatch7 = source.starting_troops_deathmatch[6],
			starting_troops_deathmatch8 = source.starting_troops_deathmatch[7],
			starting_troops_deathmatch9 = source.starting_troops_deathmatch[8],
			starting_troops_deathmatch10 = source.starting_troops_deathmatch[9],
			starting_troops_deathmatch11 = source.starting_troops_deathmatch[10],
			starting_troops_deathmatch12 = source.starting_troops_deathmatch[11],
			starting_troops_deathmatch13 = source.starting_troops_deathmatch[12],
			starting_troops_deathmatch14 = source.starting_troops_deathmatch[13],
			starting_troops_deathmatch15 = source.starting_troops_deathmatch[14],
			starting_troops_deathmatch16 = source.starting_troops_deathmatch[15],
			starting_troops_deathmatch17 = source.starting_troops_deathmatch[16],
			starting_troops_deathmatch18 = source.starting_troops_deathmatch[17],
			starting_troops_deathmatch19 = source.starting_troops_deathmatch[18],
			starting_troops_deathmatch20 = source.starting_troops_deathmatch[19],
			starting_troops_deathmatch21 = source.starting_troops_deathmatch[20],
			starting_troops_deathmatch22 = source.starting_troops_deathmatch[21],
			starting_troops_deathmatch23 = source.starting_troops_deathmatch[22],
			starting_troops_deathmatch24 = source.starting_troops_deathmatch[23],
			starting_troops_deathmatch25 = source.starting_troops_deathmatch[24],
			starting_troops_deathmatch26 = source.starting_troops_deathmatch[25],
			starting_troops_deathmatch27 = source.starting_troops_deathmatch[26],
			starting_troops_deathmatch28 = source.starting_troops_deathmatch[27],
			starting_troops_crusader1 = source.starting_troops_crusader[0],
			starting_troops_crusader2 = source.starting_troops_crusader[1],
			starting_troops_crusader3 = source.starting_troops_crusader[2],
			starting_troops_crusader4 = source.starting_troops_crusader[3],
			starting_troops_crusader5 = source.starting_troops_crusader[4],
			starting_troops_crusader6 = source.starting_troops_crusader[5],
			starting_troops_crusader7 = source.starting_troops_crusader[6],
			starting_troops_crusader8 = source.starting_troops_crusader[7],
			starting_troops_crusader9 = source.starting_troops_crusader[8],
			starting_troops_crusader10 = source.starting_troops_crusader[9],
			starting_troops_crusader11 = source.starting_troops_crusader[10],
			starting_troops_crusader12 = source.starting_troops_crusader[11],
			starting_troops_crusader13 = source.starting_troops_crusader[12],
			starting_troops_crusader14 = source.starting_troops_crusader[13],
			starting_troops_crusader15 = source.starting_troops_crusader[14],
			starting_troops_crusader16 = source.starting_troops_crusader[15],
			starting_troops_crusader17 = source.starting_troops_crusader[16],
			starting_troops_crusader18 = source.starting_troops_crusader[17],
			starting_troops_crusader19 = source.starting_troops_crusader[18],
			starting_troops_crusader20 = source.starting_troops_crusader[19],
			starting_troops_crusader21 = source.starting_troops_crusader[20],
			starting_troops_crusader22 = source.starting_troops_crusader[21],
			starting_troops_crusader23 = source.starting_troops_crusader[22],
			starting_troops_crusader24 = source.starting_troops_crusader[23],
			starting_troops_crusader25 = source.starting_troops_crusader[24],
			starting_troops_crusader26 = source.starting_troops_crusader[25],
			starting_troops_crusader27 = source.starting_troops_crusader[26],
			starting_troops_crusader28 = source.starting_troops_crusader[27],
			lord_power_display_level = source.lord_power_display_level,
			lord_hps_percent = source.lord_hps_percent,
			siege_max_troops = source.siege_max_troops,
			siege_normal_wave_multiplier = source.siege_normal_wave_multiplier,
			siege_high_gold_wave_multiplier = source.siege_high_gold_wave_multiplier
		};
	}

	public static byte[] EncodeLordConfig(ref AILordConfigTransferData data)
	{
		List<byte> list = new List<byte>();
		int value = 2;
		list.AddRange(BitConverter.GetBytes(value));
		list.AddRange(BitConverter.GetBytes(data.opponent_type));
		list.AddRange(BitConverter.GetBytes(data.opponent_type_for_speech));
		list.AddRange(BitConverter.GetBytes(data.lord_gfx_type));
		list.AddRange(BitConverter.GetBytes(data.flag_type));
		list.AddRange(BitConverter.GetBytes(data.use_of_religion));
		list.AddRange(BitConverter.GetBytes(data.use_of_ale));
		list.AddRange(BitConverter.GetBytes(data.vlow_popularity));
		list.AddRange(BitConverter.GetBytes(data.low_popularity));
		list.AddRange(BitConverter.GetBytes(data.high_popularity));
		list.AddRange(BitConverter.GetBytes(data.min_tax));
		list.AddRange(BitConverter.GetBytes(data.max_tax));
		list.AddRange(BitConverter.GetBytes(data.farm_types1));
		list.AddRange(BitConverter.GetBytes(data.farm_types2));
		list.AddRange(BitConverter.GetBytes(data.farm_types3));
		list.AddRange(BitConverter.GetBytes(data.farm_types4));
		list.AddRange(BitConverter.GetBytes(data.farm_types5));
		list.AddRange(BitConverter.GetBytes(data.farm_types6));
		list.AddRange(BitConverter.GetBytes(data.farm_types7));
		list.AddRange(BitConverter.GetBytes(data.farm_types8));
		list.AddRange(BitConverter.GetBytes(data.people_to_farm_ratio));
		list.AddRange(BitConverter.GetBytes(data.extract_wood_ratio));
		list.AddRange(BitConverter.GetBytes(data.extract_stone_ratio));
		list.AddRange(BitConverter.GetBytes(data.extract_iron_ratio));
		list.AddRange(BitConverter.GetBytes(data.extract_pitch_ratio));
		list.AddRange(BitConverter.GetBytes(data.max_quarries));
		list.AddRange(BitConverter.GetBytes(data.max_mines));
		list.AddRange(BitConverter.GetBytes(data.max_woodcutters));
		list.AddRange(BitConverter.GetBytes(data.max_pitch_dugouts));
		list.AddRange(BitConverter.GetBytes(data.max_farms));
		list.AddRange(BitConverter.GetBytes(data.build_rate));
		list.AddRange(BitConverter.GetBytes(data.crushed_building_delay));
		list.AddRange(BitConverter.GetBytes(data.sell_food_at));
		list.AddRange(BitConverter.GetBytes(data.buy_apples_at));
		list.AddRange(BitConverter.GetBytes(data.buy_cheese_at));
		list.AddRange(BitConverter.GetBytes(data.buy_bread_at));
		list.AddRange(BitConverter.GetBytes(data.buy_wheat_at));
		list.AddRange(BitConverter.GetBytes(data.buy_hops_at));
		list.AddRange(BitConverter.GetBytes(data.buy_food_amount));
		list.AddRange(BitConverter.GetBytes(data.buy_weapons));
		list.AddRange(BitConverter.GetBytes(data.pester_for_goods_delay));
		list.AddRange(BitConverter.GetBytes(data.send_goods_margin));
		list.AddRange(BitConverter.GetBytes(data.ration_boost));
		list.AddRange(BitConverter.GetBytes(data.trade_wood_at));
		list.AddRange(BitConverter.GetBytes(data.trade_stone_at));
		list.AddRange(BitConverter.GetBytes(data.trade_resources_at));
		list.AddRange(BitConverter.GetBytes(data.trade_flour_at));
		list.AddRange(BitConverter.GetBytes(data.trade_weapons_at));
		list.AddRange(BitConverter.GetBytes(data.trade_ale_at));
		list.AddRange(BitConverter.GetBytes(data.trade_pitch_at));
		list.AddRange(BitConverter.GetBytes(data.trade_minimum));
		list.AddRange(BitConverter.GetBytes(data.base_gold_reserves));
		list.AddRange(BitConverter.GetBytes(data.blacksmiths_make));
		list.AddRange(BitConverter.GetBytes(data.fletchers_make));
		list.AddRange(BitConverter.GetBytes(data.poleturners_make));
		list.AddRange(BitConverter.GetBytes(data.sell_all1));
		list.AddRange(BitConverter.GetBytes(data.sell_all2));
		list.AddRange(BitConverter.GetBytes(data.sell_all3));
		list.AddRange(BitConverter.GetBytes(data.sell_all4));
		list.AddRange(BitConverter.GetBytes(data.sell_all5));
		list.AddRange(BitConverter.GetBytes(data.sell_all6));
		list.AddRange(BitConverter.GetBytes(data.sell_all7));
		list.AddRange(BitConverter.GetBytes(data.sell_all8));
		list.AddRange(BitConverter.GetBytes(data.sell_all9));
		list.AddRange(BitConverter.GetBytes(data.sell_all10));
		list.AddRange(BitConverter.GetBytes(data.sell_all11));
		list.AddRange(BitConverter.GetBytes(data.sell_all12));
		list.AddRange(BitConverter.GetBytes(data.sell_all13));
		list.AddRange(BitConverter.GetBytes(data.sell_all14));
		list.AddRange(BitConverter.GetBytes(data.sell_all15));
		list.AddRange(BitConverter.GetBytes(data.move_mobile_defenders));
		list.AddRange(BitConverter.GetBytes(data.max_mobile_groups));
		list.AddRange(BitConverter.GetBytes(data.buy_defense_machines_at));
		list.AddRange(BitConverter.GetBytes(data.buy_defense_machines_delay));
		list.AddRange(BitConverter.GetBytes(data.dog_release_timing));
		list.AddRange(BitConverter.GetBytes(data.dog_points_count));
		list.AddRange(BitConverter.GetBytes(data.chance_of_defensive1));
		list.AddRange(BitConverter.GetBytes(data.chance_of_defensive2));
		list.AddRange(BitConverter.GetBytes(data.chance_of_defensive3));
		list.AddRange(BitConverter.GetBytes(data.chance_of_harrasment1));
		list.AddRange(BitConverter.GetBytes(data.chance_of_harrasment2));
		list.AddRange(BitConverter.GetBytes(data.chance_of_harrasment3));
		list.AddRange(BitConverter.GetBytes(data.chance_of_seiging1));
		list.AddRange(BitConverter.GetBytes(data.chance_of_seiging2));
		list.AddRange(BitConverter.GetBytes(data.chance_of_seiging3));
		list.AddRange(BitConverter.GetBytes(data.economy_protection_number));
		list.AddRange(BitConverter.GetBytes(data.economy_protection_type));
		list.AddRange(BitConverter.GetBytes(data.bodyguard_number));
		list.AddRange(BitConverter.GetBytes(data.bodyguard_type));
		list.AddRange(BitConverter.GetBytes(data.moat_diggers));
		list.AddRange(BitConverter.GetBytes(data.moat_digger_type));
		list.AddRange(BitConverter.GetBytes(data.troop_production_rate1));
		list.AddRange(BitConverter.GetBytes(data.troop_production_rate2));
		list.AddRange(BitConverter.GetBytes(data.troop_production_rate3));
		list.AddRange(BitConverter.GetBytes(data.defense_patrol_trigger_level));
		list.AddRange(BitConverter.GetBytes(data.defense_patrols));
		list.AddRange(BitConverter.GetBytes(data.defense_patrol_style));
		list.AddRange(BitConverter.GetBytes(data.defense_patrol_delay));
		list.AddRange(BitConverter.GetBytes(data.defensive_trigger_level));
		list.AddRange(BitConverter.GetBytes(data.defensive_troops1));
		list.AddRange(BitConverter.GetBytes(data.defensive_troops2));
		list.AddRange(BitConverter.GetBytes(data.defensive_troops3));
		list.AddRange(BitConverter.GetBytes(data.defensive_troops4));
		list.AddRange(BitConverter.GetBytes(data.defensive_troops5));
		list.AddRange(BitConverter.GetBytes(data.defensive_troops6));
		list.AddRange(BitConverter.GetBytes(data.defensive_troops7));
		list.AddRange(BitConverter.GetBytes(data.defensive_troops8));
		list.AddRange(BitConverter.GetBytes(data.harrasment_trigger_level));
		list.AddRange(BitConverter.GetBytes(data.harrasment_trigger_variance));
		list.AddRange(BitConverter.GetBytes(data.harrasment_troops1));
		list.AddRange(BitConverter.GetBytes(data.harrasment_troops2));
		list.AddRange(BitConverter.GetBytes(data.harrasment_troops3));
		list.AddRange(BitConverter.GetBytes(data.harrasment_troops4));
		list.AddRange(BitConverter.GetBytes(data.harrasment_troops5));
		list.AddRange(BitConverter.GetBytes(data.harrasment_troops6));
		list.AddRange(BitConverter.GetBytes(data.harrasment_troops7));
		list.AddRange(BitConverter.GetBytes(data.harrasment_troops8));
		list.AddRange(BitConverter.GetBytes(data.harrasment_machines1));
		list.AddRange(BitConverter.GetBytes(data.harrasment_machines2));
		list.AddRange(BitConverter.GetBytes(data.harrasment_machines3));
		list.AddRange(BitConverter.GetBytes(data.harrasment_machines4));
		list.AddRange(BitConverter.GetBytes(data.harrasment_machines5));
		list.AddRange(BitConverter.GetBytes(data.harrasment_machines6));
		list.AddRange(BitConverter.GetBytes(data.harrasment_machines7));
		list.AddRange(BitConverter.GetBytes(data.harrasment_machines8));
		list.AddRange(BitConverter.GetBytes(data.max_harrasment_machines));
		list.AddRange(BitConverter.GetBytes(data.harrass_delay));
		list.AddRange(BitConverter.GetBytes(data.siege_trigger_level));
		list.AddRange(BitConverter.GetBytes(data.siege_trigger_variance));
		list.AddRange(BitConverter.GetBytes(data.siege_troops_before_will_come_to_rescue));
		list.AddRange(BitConverter.GetBytes(data.siege_troops_on_site_percent));
		list.AddRange(BitConverter.GetBytes(data.siege_troops_at_home_percent));
		list.AddRange(BitConverter.GetBytes(data.siege_soften_up_delay));
		list.AddRange(BitConverter.GetBytes(data.siege_victory_delay));
		list.AddRange(BitConverter.GetBytes(data.percent_chance_waiting_for_joint_attack));
		list.AddRange(BitConverter.GetBytes(data.siege_machines1));
		list.AddRange(BitConverter.GetBytes(data.siege_machines2));
		list.AddRange(BitConverter.GetBytes(data.siege_machines3));
		list.AddRange(BitConverter.GetBytes(data.siege_machines4));
		list.AddRange(BitConverter.GetBytes(data.siege_machines5));
		list.AddRange(BitConverter.GetBytes(data.siege_machines6));
		list.AddRange(BitConverter.GetBytes(data.siege_machines7));
		list.AddRange(BitConverter.GetBytes(data.siege_machines8));
		list.AddRange(BitConverter.GetBytes(data.siege_cow_timer));
		list.AddRange(BitConverter.GetBytes(data.siege_eng_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_moat_troop));
		list.AddRange(BitConverter.GetBytes(data.siege_moat_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_herring_troop));
		list.AddRange(BitConverter.GetBytes(data.siege_herring_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_assasin_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_ladder_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_tunnel_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_storm_troop));
		list.AddRange(BitConverter.GetBytes(data.siege_storm_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_storm_tribes));
		list.AddRange(BitConverter.GetBytes(data.siege_cover_troop));
		list.AddRange(BitConverter.GetBytes(data.siege_cover_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_cover_tribes));
		list.AddRange(BitConverter.GetBytes(data.siege_shock_troop));
		list.AddRange(BitConverter.GetBytes(data.siege_shock_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_reserve_troop));
		list.AddRange(BitConverter.GetBytes(data.siege_reserve_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_reserve_tribes));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops1));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops2));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops3));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops4));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops5));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops6));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops7));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops8));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops9));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops10));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops11));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops12));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops13));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops14));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops15));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops16));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops17));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops18));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops19));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops20));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops21));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops22));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops23));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_troops24));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_amount));
		list.AddRange(BitConverter.GetBytes(data.siege_wall_tribes));
		list.AddRange(BitConverter.GetBytes(data.who_to_pick_on));
		list.AddRange(BitConverter.GetBytes(data.use_improved_sieging));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal1));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal2));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal3));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal4));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal5));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal6));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal7));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal8));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal9));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal10));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal11));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal12));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal13));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal14));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal15));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal16));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal17));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal18));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal19));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal20));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal21));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal22));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal23));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal24));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal25));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal26));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal27));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_normal28));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch1));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch2));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch3));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch4));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch5));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch6));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch7));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch8));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch9));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch10));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch11));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch12));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch13));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch14));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch15));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch16));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch17));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch18));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch19));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch20));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch21));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch22));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch23));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch24));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch25));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch26));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch27));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_deathmatch28));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader1));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader2));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader3));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader4));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader5));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader6));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader7));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader8));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader9));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader10));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader11));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader12));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader13));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader14));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader15));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader16));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader17));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader18));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader19));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader20));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader21));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader22));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader23));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader24));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader25));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader26));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader27));
		list.AddRange(BitConverter.GetBytes(data.starting_troops_crusader28));
		list.AddRange(BitConverter.GetBytes(data.lord_power_display_level));
		list.AddRange(BitConverter.GetBytes(data.lord_hps_percent));
		list.AddRange(BitConverter.GetBytes(data.siege_max_troops));
		list.AddRange(BitConverter.GetBytes(data.siege_normal_wave_multiplier));
		list.AddRange(BitConverter.GetBytes(data.siege_high_gold_wave_multiplier));
		return list.ToArray();
	}

	public static void DecodeLordConfig(ref AILordConfigTransferData data, byte[] source, int offset = 0)
	{
		int num = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.opponent_type = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.opponent_type_for_speech = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.lord_gfx_type = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.flag_type = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.use_of_religion = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.use_of_ale = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.vlow_popularity = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.low_popularity = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.high_popularity = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.min_tax = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.max_tax = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.farm_types1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.farm_types2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.farm_types3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.farm_types4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.farm_types5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.farm_types6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.farm_types7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.farm_types8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.people_to_farm_ratio = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.extract_wood_ratio = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.extract_stone_ratio = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.extract_iron_ratio = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.extract_pitch_ratio = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.max_quarries = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.max_mines = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.max_woodcutters = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.max_pitch_dugouts = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.max_farms = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.build_rate = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.crushed_building_delay = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_food_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_apples_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_cheese_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_bread_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_wheat_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_hops_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_food_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_weapons = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.pester_for_goods_delay = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.send_goods_margin = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.ration_boost = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.trade_wood_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.trade_stone_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.trade_resources_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.trade_flour_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.trade_weapons_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.trade_ale_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.trade_pitch_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.trade_minimum = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.base_gold_reserves = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.blacksmiths_make = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.fletchers_make = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.poleturners_make = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all9 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all10 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all11 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all12 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all13 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all14 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.sell_all15 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.move_mobile_defenders = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.max_mobile_groups = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_defense_machines_at = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.buy_defense_machines_delay = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.dog_release_timing = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.dog_points_count = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_defensive1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_defensive2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_defensive3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_harrasment1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_harrasment2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_harrasment3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_seiging1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_seiging2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.chance_of_seiging3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.economy_protection_number = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.economy_protection_type = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.bodyguard_number = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.bodyguard_type = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.moat_diggers = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.moat_digger_type = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.troop_production_rate1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.troop_production_rate2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.troop_production_rate3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defense_patrol_trigger_level = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defense_patrols = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defense_patrol_style = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defense_patrol_delay = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_trigger_level = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_troops1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_troops2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_troops3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_troops4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_troops5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_troops6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_troops7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.defensive_troops8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_trigger_level = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_trigger_variance = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_troops1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_troops2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_troops3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_troops4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_troops5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_troops6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_troops7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_troops8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_machines1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_machines2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_machines3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_machines4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_machines5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_machines6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_machines7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrasment_machines8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.max_harrasment_machines = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.harrass_delay = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_trigger_level = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_trigger_variance = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_troops_before_will_come_to_rescue = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_troops_on_site_percent = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_troops_at_home_percent = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_soften_up_delay = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_victory_delay = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.percent_chance_waiting_for_joint_attack = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_machines1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_machines2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_machines3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_machines4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_machines5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_machines6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_machines7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_machines8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_cow_timer = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_eng_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_moat_troop = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_moat_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_herring_troop = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_herring_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_assasin_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_ladder_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_tunnel_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_storm_troop = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_storm_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_storm_tribes = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_cover_troop = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_cover_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_cover_tribes = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_shock_troop = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_shock_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_reserve_troop = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_reserve_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_reserve_tribes = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops9 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops10 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops11 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops12 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops13 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops14 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops15 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops16 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops17 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops18 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops19 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops20 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops21 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops22 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops23 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_troops24 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_amount = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.siege_wall_tribes = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.who_to_pick_on = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.use_improved_sieging = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal9 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal10 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal11 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal12 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal13 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal14 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal15 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal16 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal17 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal18 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal19 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal20 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal21 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal22 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal23 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal24 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal25 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal26 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal27 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_normal28 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch9 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch10 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch11 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch12 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch13 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch14 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch15 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch16 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch17 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch18 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch19 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch20 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch21 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch22 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch23 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch24 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch25 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch26 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch27 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_deathmatch28 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader1 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader2 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader3 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader4 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader5 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader6 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader7 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader8 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader9 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader10 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader11 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader12 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader13 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader14 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader15 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader16 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader17 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader18 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader19 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader20 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader21 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader22 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader23 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader24 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader25 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader26 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader27 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.starting_troops_crusader28 = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.lord_power_display_level = BitConverter.ToInt32(source, offset);
		offset += 4;
		data.lord_hps_percent = BitConverter.ToInt32(source, offset);
		offset += 4;
		if (num >= 2)
		{
			data.siege_max_troops = BitConverter.ToInt32(source, offset);
			offset += 4;
			data.siege_normal_wave_multiplier = BitConverter.ToInt32(source, offset);
			offset += 4;
			data.siege_high_gold_wave_multiplier = BitConverter.ToInt32(source, offset);
			offset += 4;
		}
		else
		{
			data.siege_max_troops = 200;
			data.siege_normal_wave_multiplier = 5;
			data.siege_high_gold_wave_multiplier = 7;
		}
	}

	public unsafe static ScenarioOverview convertScenarioOverview(ScenarioOverviewReturnData source)
	{
		ScenarioOverview scenarioOverview = new ScenarioOverview();
		scenarioOverview.startMonth = source.startMonth;
		scenarioOverview.startYear = source.startYear;
		for (int i = 0; i < source.numEntries; i++)
		{
			ScenarioOverviewEntry item = new ScenarioOverviewEntry
			{
				month = source.month[i],
				year = source.year[i],
				entryType = source.entryType[i],
				data1 = source.data1[i],
				data2 = source.data2[i],
				message = source.message[i],
				repeatDuration = source.repeatDuration[i],
				repeatCount = source.repeatCount[i]
			};
			scenarioOverview.entries.Add(item);
		}
		for (int j = 0; j < 25; j++)
		{
			scenarioOverview.scenario_start_goods[j] = source.scenario_start_goods[j];
			scenarioOverview.scenario_trader_goods_available[j] = source.scenario_trader_goods_available[j];
		}
		for (int k = 0; k < 10; k++)
		{
			scenarioOverview.scenario_start_troops[k] = source.scenario_start_troops[k];
		}
		for (int l = 0; l < 100; l++)
		{
			scenarioOverview.scenario_buildings_available[l] = source.scenario_buildings_available[l];
		}
		for (int m = 0; m < 6; m++)
		{
			scenarioOverview.scenario_start_siege_equipment[m] = source.scenario_start_siege_equipment[m];
		}
		for (int n = 0; n < 7; n++)
		{
			scenarioOverview.sa_troop_availability[n] = source.sa_troop_availability[n];
			scenarioOverview.sa_merc_availability[n] = source.sa_merc_availability[n];
		}
		for (int num = 0; num < 8; num++)
		{
			scenarioOverview.sa_bed_availability[num] = source.sa_bed_availability[num];
		}
		scenarioOverview.sa_fletcher_bow = source.sa_fletcher_bow;
		scenarioOverview.sa_blacksmith_mace = source.sa_blacksmith_mace;
		scenarioOverview.sa_poleturner_pike = source.sa_poleturner_pike;
		scenarioOverview.sa_fletcher_xbow = source.sa_fletcher_xbow;
		scenarioOverview.sa_blacksmith_sword = source.sa_blacksmith_sword;
		scenarioOverview.sa_poleturner_spear = source.sa_poleturner_spear;
		scenarioOverview.special_start_gold = source.special_start_gold;
		scenarioOverview.special_start = source.special_start;
		scenarioOverview.special_start_rationing = source.special_start_rationing;
		scenarioOverview.special_start_tax_rate = source.special_start_tax_rate;
		scenarioOverview.fast_goods_feedin = source.fast_goods_feedin;
		scenarioOverview.scenario_start_popularity = source.scenario_start_popularity;
		scenarioOverview.scenario_buildings_count = source.scenario_buildings_count;
		return scenarioOverview;
	}

	public static ev convertTL_event(evF source)
	{
		return new ev
		{
			onoff = source.onoff,
			type = source.type,
			value = source.value
		};
	}

	public static tl_event convertTL_event(tl_eventF source)
	{
		tl_event obj = new tl_event
		{
			month = source.month,
			year = source.year,
			tl_type = source.tl_type,
			done = source.done,
			pre_done = source.pre_done,
			action_data = source.action_data,
			action = source.action,
			and_or = source.and_or,
			repeat = source.repeat,
			repeat_count = source.repeat_count
		};
		int num = 0;
		obj.event_value[num++] = convertTL_event(source.event_value1);
		obj.event_value[num++] = convertTL_event(source.event_value2);
		obj.event_value[num++] = convertTL_event(source.event_value3);
		obj.event_value[num++] = convertTL_event(source.event_value4);
		obj.event_value[num++] = convertTL_event(source.event_value5);
		obj.event_value[num++] = convertTL_event(source.event_value6);
		obj.event_value[num++] = convertTL_event(source.event_value7);
		obj.event_value[num++] = convertTL_event(source.event_value8);
		obj.event_value[num++] = convertTL_event(source.event_value9);
		obj.event_value[num++] = convertTL_event(source.event_value10);
		obj.event_value[num++] = convertTL_event(source.event_value11);
		obj.event_value[num++] = convertTL_event(source.event_value12);
		obj.event_value[num++] = convertTL_event(source.event_value13);
		obj.event_value[num++] = convertTL_event(source.event_value14);
		obj.event_value[num++] = convertTL_event(source.event_value15);
		obj.event_value[num++] = convertTL_event(source.event_value16);
		obj.event_value[num++] = convertTL_event(source.event_value17);
		obj.event_value[num++] = convertTL_event(source.event_value18);
		obj.event_value[num++] = convertTL_event(source.event_value19);
		obj.event_value[num++] = convertTL_event(source.event_value20);
		obj.event_value[num++] = convertTL_event(source.event_value21);
		obj.event_value[num++] = convertTL_event(source.event_value22);
		obj.event_value[num++] = convertTL_event(source.event_value23);
		obj.event_value[num++] = convertTL_event(source.event_value24);
		obj.event_value[num++] = convertTL_event(source.event_value25);
		obj.event_value[num++] = convertTL_event(source.event_value26);
		obj.event_value[num++] = convertTL_event(source.event_value27);
		obj.event_value[num++] = convertTL_event(source.event_value28);
		obj.event_value[num++] = convertTL_event(source.event_value29);
		obj.event_value[num++] = convertTL_event(source.event_value30);
		obj.event_value[num++] = convertTL_event(source.event_value31);
		obj.event_value[num++] = convertTL_event(source.event_value32);
		obj.event_value[num++] = convertTL_event(source.event_value33);
		obj.event_value[num++] = convertTL_event(source.event_value34);
		obj.event_value[num++] = convertTL_event(source.event_value35);
		obj.event_value[num++] = convertTL_event(source.event_value36);
		obj.event_value[num++] = convertTL_event(source.event_value37);
		obj.event_value[num++] = convertTL_event(source.event_value38);
		obj.event_value[num++] = convertTL_event(source.event_value39);
		obj.event_value[num++] = convertTL_event(source.event_value40);
		return obj;
	}

	public static evF convertTL_event(ev source)
	{
		return new evF
		{
			onoff = source.onoff,
			type = source.type,
			value = source.value
		};
	}

	public static tl_eventF convertTL_event(tl_event source)
	{
		tl_eventF result = new tl_eventF
		{
			month = source.month,
			year = source.year,
			tl_type = source.tl_type,
			done = source.done,
			pre_done = source.pre_done,
			action_data = source.action_data,
			action = source.action,
			and_or = source.and_or,
			repeat = source.repeat,
			repeat_count = source.repeat_count
		};
		int num = 0;
		result.event_value1 = convertTL_event(source.event_value[num++]);
		result.event_value2 = convertTL_event(source.event_value[num++]);
		result.event_value3 = convertTL_event(source.event_value[num++]);
		result.event_value4 = convertTL_event(source.event_value[num++]);
		result.event_value5 = convertTL_event(source.event_value[num++]);
		result.event_value6 = convertTL_event(source.event_value[num++]);
		result.event_value7 = convertTL_event(source.event_value[num++]);
		result.event_value8 = convertTL_event(source.event_value[num++]);
		result.event_value9 = convertTL_event(source.event_value[num++]);
		result.event_value10 = convertTL_event(source.event_value[num++]);
		result.event_value11 = convertTL_event(source.event_value[num++]);
		result.event_value12 = convertTL_event(source.event_value[num++]);
		result.event_value13 = convertTL_event(source.event_value[num++]);
		result.event_value14 = convertTL_event(source.event_value[num++]);
		result.event_value15 = convertTL_event(source.event_value[num++]);
		result.event_value16 = convertTL_event(source.event_value[num++]);
		result.event_value17 = convertTL_event(source.event_value[num++]);
		result.event_value18 = convertTL_event(source.event_value[num++]);
		result.event_value19 = convertTL_event(source.event_value[num++]);
		result.event_value20 = convertTL_event(source.event_value[num++]);
		result.event_value21 = convertTL_event(source.event_value[num++]);
		result.event_value22 = convertTL_event(source.event_value[num++]);
		result.event_value23 = convertTL_event(source.event_value[num++]);
		result.event_value24 = convertTL_event(source.event_value[num++]);
		result.event_value25 = convertTL_event(source.event_value[num++]);
		result.event_value26 = convertTL_event(source.event_value[num++]);
		result.event_value27 = convertTL_event(source.event_value[num++]);
		result.event_value28 = convertTL_event(source.event_value[num++]);
		result.event_value29 = convertTL_event(source.event_value[num++]);
		result.event_value30 = convertTL_event(source.event_value[num++]);
		result.event_value31 = convertTL_event(source.event_value[num++]);
		result.event_value32 = convertTL_event(source.event_value[num++]);
		result.event_value33 = convertTL_event(source.event_value[num++]);
		result.event_value34 = convertTL_event(source.event_value[num++]);
		result.event_value35 = convertTL_event(source.event_value[num++]);
		result.event_value36 = convertTL_event(source.event_value[num++]);
		result.event_value37 = convertTL_event(source.event_value[num++]);
		result.event_value38 = convertTL_event(source.event_value[num++]);
		result.event_value39 = convertTL_event(source.event_value[num++]);
		result.event_value40 = convertTL_event(source.event_value[num++]);
		return result;
	}

	public static tl_message convertTL_message(tl_messageF source)
	{
		return new tl_message
		{
			month = source.month,
			year = source.year,
			tl_type = source.tl_type,
			done = source.done,
			pre_done = source.pre_done,
			message_id = source.message_id,
			action = source.action
		};
	}

	public static tl_messageF convertTL_message(tl_message source)
	{
		return new tl_messageF
		{
			month = source.month,
			year = source.year,
			tl_type = source.tl_type,
			done = source.done,
			pre_done = source.pre_done,
			message_id = source.message_id,
			action = source.action
		};
	}

	public unsafe static tl_invasion convertTL_invasion(tl_invasionF source)
	{
		tl_invasion tl_invasion2 = new tl_invasion();
		tl_invasion2.month = source.month;
		tl_invasion2.year = source.year;
		tl_invasion2.tl_type = source.tl_type;
		tl_invasion2.done = source.done;
		tl_invasion2.pre_done = source.pre_done;
		tl_invasion2.total = source.total;
		for (int i = 0; i < 33; i++)
		{
			tl_invasion2._size[i] = source._size[i];
		}
		tl_invasion2.invasion_point = source.invasion_point;
		tl_invasion2.start_year = source.start_year;
		tl_invasion2.repeat = source.repeat;
		tl_invasion2.from = source.from;
		tl_invasion2.markerID = source.markerID;
		return tl_invasion2;
	}

	public unsafe static tl_invasionF convertTL_invasion(tl_invasion source)
	{
		tl_invasionF result = new tl_invasionF
		{
			month = source.month,
			year = source.year,
			tl_type = source.tl_type,
			done = source.done,
			pre_done = source.pre_done,
			total = source.total
		};
		for (int i = 0; i < 33; i++)
		{
			result._size[i] = source._size[i];
		}
		result.invasion_point = source.invasion_point;
		result.start_year = source.start_year;
		result.repeat = source.repeat;
		result.from = source.from;
		result.markerID = source.markerID;
		return result;
	}

	public unsafe static PlayState CopyPlayStateStruct(PlayStateReturnData source, int[] selectedChimps)
	{
		PlayState playState = new PlayState();
		for (int i = 0; i < 25; i++)
		{
			playState.resources[i] = source.resources[i];
			playState.keep_storage[i] = source.keep_storage[i];
		}
		playState.numSelectedChimps = source.numSelectedChimps;
		for (int j = 0; j < playState.numSelectedChimps; j++)
		{
			playState.selectedChimps[j] = selectedChimps[j * 2];
			playState.selectedChimpTypes[j] = selectedChimps[j * 2 + 1];
		}
		playState.popularity = source.popularity;
		playState.population = source.population;
		playState.gold = source.gold;
		playState.housing_cap = source.housing_cap;
		playState.upcoming_total_popularity = source.upcoming_total_popularity;
		playState.rationing_popularity = source.rationing_popularity;
		playState.foodsEaten_popularity = source.foodsEaten_popularity;
		playState.food_popularity = source.food_popularity;
		playState.tax_popularity = source.tax_popularity;
		playState.overcrowding_popularity = source.overcrowding_popularity;
		playState.fearFactor_popularity = source.fearFactor_popularity;
		playState.religion_popularity = source.religion_popularity;
		playState.fairs_popularity = source.fairs_popularity;
		playState.plague_popularity = source.plague_popularity;
		playState.wolves_popularity = source.wolves_popularity;
		playState.bandits_popularity = source.bandits_popularity;
		playState.fire_popularity = source.fire_popularity;
		playState.marriage_popularity = source.marriage_popularity;
		playState.jester_popularity = source.jester_popularity;
		playState.good_things = source.good_things;
		playState.bad_things = source.bad_things;
		playState.fear_factor = source.fear_factor;
		playState.fear_factor_next_level = source.fear_factor_next_level;
		playState.efficiency = source.efficiency;
		for (int k = 0; k < 300; k++)
		{
			playState.population_graph[k] = source.population_graph[k];
		}
		for (int l = 0; l < 4; l++)
		{
			playState.food_types_not_eatable[l] = source.food_types_not_eatable[l];
		}
		for (int m = 0; m < 34; m++)
		{
			playState.troop_counts[m] = source.troop_counts[m];
		}
		playState.num_priests = source.num_priests;
		playState.blessed_percent = source.blessed_percent;
		playState.blessed_next_level_at = source.blessed_next_level_at;
		playState.tax_rate = source.tax_rate;
		playState.tax_amount = source.tax_amount;
		playState.peasants_available_for_troops = source.peasants_available_for_troops;
		for (int n = 0; n < 8; n++)
		{
			playState.make_troop_state[n] = source.make_troop_state[n];
		}
		playState.rationing = source.rationing;
		playState.food_clock = source.food_clock;
		playState.total_food = source.total_food;
		playState.months_of_food = source.months_of_food;
		playState.food_types_eaten = source.food_types_eaten;
		playState.food_types_available = source.food_types_available;
		playState.app_mode = source.app_mode;
		playState.app_sub_mode = source.app_sub_mode;
		playState.debug_value1 = source.debug_value1;
		playState.game_time = source.game_time;
		playState.in_structure = source.in_structure;
		playState.in_structure_type = source.in_structure_type;
		playState.completeSelectionBox = source.completeSelectionBox;
		playState.in_chimp = source.in_chimp;
		playState.in_chimp_type = source.in_chimp_type;
		playState.inchimp_name1 = source.inchimp_name1;
		playState.inchimp_name2 = source.inchimp_name2;
		playState.dog_cage_state = source.dog_cage_state;
		playState.inchimp_n_text = source.inchimp_n_text;
		playState.in_chimp_goods = source.in_chimp_goods;
		playState.gatehouse_state = source.gatehouse_state;
		playState.repairs_allowed = source.repairs_allowed;
		playState.can_do_repairs = source.can_do_repairs;
		playState.building_hps_for_repair = source.building_hps_for_repair;
		playState.building_maxhps_for_repair = source.building_maxhps_for_repair;
		playState.sleep_allowed = source.sleep_allowed;
		playState.building_type_sleeping = source.building_type_sleeping;
		playState.have_building_stats = source.have_building_stats;
		playState.workers_have = source.workers_have;
		playState.job_vacancies = source.job_vacancies;
		playState.workers_needed = source.workers_needed;
		playState.got_keep_access = source.got_keep_access;
		playState.turned_off = source.turned_off;
		playState.working = source.working;
		playState.mill_message = source.mill_message;
		playState.pints_of_ale = source.pints_of_ale;
		playState.barrels_of_ale = source.barrels_of_ale;
		playState.working_inns = source.working_inns;
		playState.total_inns = source.total_inns;
		playState.inn_coverage_percent = source.inn_coverage_percent;
		playState.inn_coverage_popularity = source.inn_coverage_popularity;
		playState.inn_coverage_next = source.inn_coverage_next;
		playState.troops_show_disband = source.troops_show_disband;
		playState.troops_show_build_menu = source.troops_show_build_menu;
		playState.troops_show_make_catapult = source.troops_show_make_catapult;
		playState.troops_show_make_trebuchet = source.troops_show_make_trebuchet;
		playState.troops_show_make_siege_tower = source.troops_show_make_siege_tower;
		playState.troops_show_battering_ram = source.troops_show_battering_ram;
		playState.troops_show_portable_shield = source.troops_show_portable_shield;
		playState.troops_show_get_ammo = source.troops_show_get_ammo;
		playState.troops_show_launch_cow_and_num_cows = source.troops_show_launch_cow_and_num_cows;
		playState.troops_show_attack_here_and_type = source.troops_show_attack_here_and_type;
		playState.troops_show_attack_here_number_rocks = source.troops_show_attack_here_number_rocks;
		playState.troops_show_stance = source.troops_show_stance;
		playState.troops_show_patrol = source.troops_show_patrol;
		playState.troops_patrol_mode = source.troops_patrol_mode;
		playState.weapon_being_made_now = source.weapon_being_made_now;
		playState.game_type = source.game_type;
		playState.can_make_xbows = source.can_make_xbows;
		playState.can_make_sword = source.can_make_sword;
		playState.can_make_pike = source.can_make_pike;
		playState.weapon_being_made_next = source.weapon_being_made_next;
		playState.production_no_resources = source.production_no_resources;
		playState.playerdesc_message = source.playerdesc_message;
		playState.playerdesc_message2 = source.playerdesc_message2;
		playState.weapon_types_available = new byte[9];
		for (int num = 0; num < 9; num++)
		{
			playState.weapon_types_available[num] = source.weapon_types_available[num];
		}
		playState.troop_types_available = new byte[8];
		playState.merc_troop_types_available = new byte[8];
		playState.bed_troop_types_available = new byte[8];
		for (int num2 = 0; num2 < 8; num2++)
		{
			playState.troop_types_available[num2] = source.troop_types_available[num2];
			playState.merc_troop_types_available[num2] = source.merc_troop_types_available[num2];
			playState.bed_troop_types_available[num2] = source.bed_troop_types_available[num2];
		}
		playState.trade_buy_costs = new short[25];
		playState.trade_sell_costs = new short[25];
		playState.trade_buy_amounts = new short[25];
		playState.trade_sell_amounts = new short[25];
		playState.trade_sell_costs_fixed = new short[25];
		for (int num3 = 0; num3 < 25; num3++)
		{
			playState.trade_buy_costs[num3] = source.trade_buy_costs[num3];
			playState.trade_sell_costs[num3] = source.trade_sell_costs[num3];
			playState.trade_buy_amounts[num3] = source.trade_buy_amounts[num3];
			playState.trade_sell_amounts[num3] = source.trade_sell_amounts[num3];
			playState.trade_sell_costs_fixed[num3] = source.trade_sell_costs_fixed[num3];
		}
		playState.trading_current_goods = source.trading_current_goods;
		playState.trading_next_goods = source.trading_next_goods;
		playState.trading_prev_goods = source.trading_prev_goods;
		playState.marry_status = source.marry_status;
		playState.marry_male_type = source.marry_male_type;
		playState.marry_female_type = source.marry_female_type;
		playState.marry_text = source.marry_text;
		playState.marry_m_name1 = source.marry_m_name1;
		playState.marry_m_name2 = source.marry_m_name2;
		playState.marry_f_name1 = source.marry_f_name1;
		playState.marry_f_name2 = source.marry_f_name2;
		playState.blessed_popularity = source.blessed_popularity;
		playState.church_adjustment = (sbyte)source.church_adjustment;
		playState.church_missing = source.church_missing;
		playState.scribe_frame = source.scribe_frame;
		playState.total_horses_available = source.total_horses_available;
		playState.action_point_count = source.action_point_count;
		playState.action_points_x = new short[playState.action_point_count];
		playState.action_points_y = new short[playState.action_point_count];
		for (int num4 = 0; num4 < playState.action_point_count; num4++)
		{
			playState.action_points_x[num4] = source.action_points_x[num4];
			playState.action_points_y[num4] = source.action_points_y[num4];
		}
		playState.camera_target_x = source.camera_target_x;
		playState.camera_target_y = source.camera_target_y;
		playState.camera_target_z = source.camera_target_z;
		playState.rotateHappened = source.rotateHappened;
		playState.force_app_mode = source.force_app_mode;
		playState.month = source.month;
		playState.year = source.year;
		playState.pop_months = source.pop_months;
		playState.chimp_comments = source.chimp_comments;
		playState.camera_target_flat = source.camera_target_flat;
		playState.skirmish_map_num_keeps = source.skirmish_map_num_keeps;
		playState.inbuilding_help_id = source.inbuilding_help_id;
		playState.MP_Ahead_By = source.MP_Ahead_By;
		playState.MP_Behind_By = source.MP_Behind_By;
		playState.SkipFrame = source.SkipFrame;
		playState.undoAvailable = source.undoAvailable;
		playState.chimps_count = source.chimps_count;
		playState.chimps_limit = source.chimps_limit;
		playState.structs_count = source.structs_count;
		playState.structs_limit = source.structs_limit;
		playState.orgs_count = source.orgs_count;
		playState.orgs_limit = source.orgs_limit;
		playState.minerals_count = source.minerals_count;
		playState.minerals_limit = source.minerals_limit;
		playState.tribes_count = source.tribes_count;
		playState.tribes_limit = source.tribes_limit;
		playState.freeWoodcutter = source.freeWoodcutter;
		playState.freeGranary = source.freeGranary;
		playState.gotSignpost = source.gotSignpost;
		playState.repair_wood_needed = source.repair_wood_needed;
		playState.repair_stone_needed = source.repair_stone_needed;
		playState.panel_text_group = source.panel_text_group;
		playState.panel_text_text = source.panel_text_text;
		playState.free_buildingCheat = source.free_buildingCheat;
		playState.editor_time_paused = source.editor_time_paused;
		playState.bld_tiles_built = source.bld_tiles_built;
		playState.game_paused = source.game_paused;
		playState.numMPChatEntries = source.numMPChatEntries;
		playState.ai_clock = source.ai_clock;
		playState.lordOnlySelected = source.lordOnlySelected;
		playState.gotMarket = source.gotMarket;
		playState.keep_enclosed = source.keep_enclosed;
		playState.can_make_bows = source.can_make_bows;
		playState.can_make_mace = source.can_make_mace;
		playState.can_make_spear = source.can_make_spear;
		playState.messageFrom = source.messageFrom;
		playState.troops_show_make_arab_ballista = source.troops_show_make_arab_ballista;
		playState.starting_goods_level = source.starting_goods_level;
		playState.fairness = source.fairness;
		playState.elapsedTime = source.elapsedTime;
		playState.balanced = source.balanced;
		playState.extremeCount = source.extremeCount;
		playState.extremeEnabled = source.extremeEnabled;
		playState.mouse_selector_state = source.mouse_selector_state;
		playState.flattenedHappened = source.flattenedHappened;
		playState.skirmishInsultFrom = source.skirmishInsultFrom;
		playState.skirmishInsult = source.skirmishInsult;
		playState.lord_Type = source.lord_Type;
		playState.monk_available = source.monk_available;
		playState.engineer_available = source.engineer_available;
		playState.ladderman_available = source.ladderman_available;
		playState.resyncPercent = source.resyncPercent;
		playState.messageFromcharacter = source.messageFromcharacter;
		playState.debug_value2 = source.debug_value2;
		playState.laddermanCost = source.laddermanCost;
		playState.eunuchCost = source.eunuchCost;
		playState.spectatorMode = source.spectatorMode;
		playState.customisedExtremeTrail = source.customisedExtremeTrail;
		playState.starting_teams = new byte[9];
		for (int num5 = 0; num5 < 9; num5++)
		{
			playState.starting_teams[num5] = source.starting_teams[num5];
		}
		playState.koth_scores = new int[8];
		playState.pingtimes = new short[8];
		playState.mpkick = new byte[8];
		playState.computer_register = new short[9];
		playState.computer_names = new short[9];
		playState.teams = new short[9];
		playState.player_register = new short[9];
		playState.skirmish_needs_help = new short[9];
		playState.skirmish_player_requesting_type = new short[10];
		playState.skirmish_player_requesting_amount = new short[10];
		playState.skirmish_order = new short[9];
		playState.skirmish_order_player = new short[9];
		playState.skirmish_order_from_player = new short[9];
		playState.mp_stats_valid = new byte[9];
		playState.lord_alive = new byte[9];
		playState.team_shield = new byte[9];
		for (int num6 = 0; num6 < 8; num6++)
		{
			playState.koth_scores[num6] = source.koth_scores[num6];
			playState.pingtimes[num6] = source.pingtimes[num6];
			playState.mpkick[num6] = source.mpkick[num6];
			playState.computer_register[num6 + 1] = source.computer_register[num6];
			playState.computer_names[num6 + 1] = source.computer_names[num6];
			playState.teams[num6 + 1] = source.teams[num6];
			playState.player_register[num6 + 1] = source.player_register[num6];
			playState.skirmish_needs_help[num6 + 1] = source.skirmish_needs_help[num6];
			playState.skirmish_order[num6 + 1] = source.skirmish_order[num6];
			playState.skirmish_order_player[num6 + 1] = source.skirmish_order_player[num6];
			playState.skirmish_order_from_player[num6 + 1] = source.skirmish_order_from_player[num6];
			playState.mp_stats_valid[num6 + 1] = source.mp_stats_valid[num6];
			playState.lord_alive[num6 + 1] = source.lord_alive[num6];
		}
		playState.team_shield[1] = source.team_shield1;
		playState.team_shield[2] = source.team_shield2;
		playState.team_shield[3] = source.team_shield3;
		playState.team_shield[4] = source.team_shield4;
		playState.team_shield[5] = source.team_shield5;
		playState.team_shield[6] = source.team_shield6;
		playState.team_shield[7] = source.team_shield7;
		playState.team_shield[8] = source.team_shield8;
		for (int num7 = 0; num7 < 10; num7++)
		{
			playState.skirmish_player_requesting_type[num7] = source.skirmish_player_requesting_type[num7];
			playState.skirmish_player_requesting_amount[num7] = source.skirmish_player_requesting_amount[num7];
		}
		playState.chat_store_data = new short[10, 5];
		for (int num8 = 0; num8 < 50; num8++)
		{
			playState.chat_store_data[num8 % 10, num8 / 10] = source.chat_store_data[num8];
		}
		playState.autotrade_sell_amount = new short[25];
		playState.autotrade_buy_amount = new short[25];
		playState.autotrade_onoff = new byte[25];
		for (int num9 = 0; num9 < 25; num9++)
		{
			playState.autotrade_sell_amount[num9] = source.autotrade_sell_amount[num9];
			playState.autotrade_buy_amount[num9] = source.autotrade_buy_amount[num9];
			playState.autotrade_onoff[num9] = source.autotrade_onoff[num9];
		}
		if (source.control_groups_total[0] < 0)
		{
			playState.control_groups_total = new short[1];
			playState.control_groups_total[0] = -1;
		}
		else
		{
			playState.control_groups_match = new byte[10];
			playState.control_groups_total = new short[40];
			playState.control_groups_type = new byte[40];
			playState.control_groups_count = new short[40];
			for (int num10 = 0; num10 < 10; num10++)
			{
				playState.control_groups_match[num10] = source.control_groups_match[num10];
				playState.control_groups_total[num10] = source.control_groups_total[num10];
			}
			for (int num11 = 0; num11 < 40; num11++)
			{
				playState.control_groups_type[num11] = source.control_groups_type[num11];
				playState.control_groups_count[num11] = source.control_groups_count[num11];
			}
		}
		playState.markers_start_points = new int[10, 4];
		for (int num12 = 0; num12 < 40; num12++)
		{
			playState.markers_start_points[num12 % 10, num12 / 10] = source.markers_start_points[num12];
		}
		if (source.speechFileName[0] != 0)
		{
			int num13 = 0;
			for (int num14 = 0; num14 < 128; num14++)
			{
				if (source.speechFileName[num14] == 0)
				{
					num13 = num14;
					break;
				}
			}
			byte[] array = new byte[num13];
			for (int num15 = 0; num15 < num13; num15++)
			{
				array[num15] = source.speechFileName[num15];
			}
			playState.speechFileName = Encoding.ASCII.GetString(array);
		}
		else
		{
			playState.speechFileName = "";
		}
		if (source.musicFileName[0] != 0)
		{
			int num16 = 0;
			for (int num17 = 0; num17 < 128; num17++)
			{
				if (source.musicFileName[num17] == 0)
				{
					num16 = num17;
					break;
				}
			}
			byte[] array2 = new byte[num16];
			for (int num18 = 0; num18 < num16; num18++)
			{
				array2[num18] = source.musicFileName[num18];
			}
			playState.musicFileName = Encoding.ASCII.GetString(array2);
		}
		else
		{
			playState.musicFileName = "";
		}
		if (source.binkFileName[0] != 0)
		{
			int num19 = 0;
			for (int num20 = 0; num20 < 128; num20++)
			{
				if (source.binkFileName[num20] == 0)
				{
					num19 = num20;
					break;
				}
			}
			byte[] array3 = new byte[num19];
			for (int num21 = 0; num21 < num19; num21++)
			{
				array3[num21] = source.binkFileName[num21];
			}
			playState.binkFileName = Encoding.ASCII.GetString(array3);
		}
		else
		{
			playState.binkFileName = "";
		}
		return playState;
	}

	public static ScoreData convertScoreData(ScoreReturnData source)
	{
		ScoreData obj = new ScoreData
		{
			score_weapons = source.score_weapons,
			score_weapons_points = source.score_weapons_points,
			score = source.score,
			levelPoints = source.levelPoints,
			score_months = source.score_months,
			score_months_points = source.score_months_points,
			items_count = source.items_count,
			score_troops = source.score_troops,
			troops_percent_lost = source.troops_percent_lost,
			siege_that_score = source.siege_that_score,
			siege_defenders_score = source.siege_defenders_score,
			siege_attackers_score = source.siege_attackers_score,
			difficulty_level = source.difficulty_level,
			items_extra = new int[7]
		};
		obj.items_extra[0] = source.items_extra1;
		obj.items_extra[1] = source.items_extra2;
		obj.items_extra[2] = source.items_extra3;
		obj.items_extra[3] = source.items_extra4;
		obj.items_extra[4] = source.items_extra5;
		obj.items_extra[5] = source.items_extra6;
		obj.items_extra[6] = source.items_extra7;
		obj.items_extra_points = new int[7];
		obj.items_extra_points[0] = source.items_extra_points1;
		obj.items_extra_points[1] = source.items_extra_points2;
		obj.items_extra_points[2] = source.items_extra_points3;
		obj.items_extra_points[3] = source.items_extra_points4;
		obj.items_extra_points[4] = source.items_extra_points5;
		obj.items_extra_points[5] = source.items_extra_points6;
		obj.items_extra_points[6] = source.items_extra_points7;
		obj.items_extra_type = new int[7];
		obj.items_extra_type[0] = source.items_extra_type1;
		obj.items_extra_type[1] = source.items_extra_type2;
		obj.items_extra_type[2] = source.items_extra_type3;
		obj.items_extra_type[3] = source.items_extra_type4;
		obj.items_extra_type[4] = source.items_extra_type5;
		obj.items_extra_type[5] = source.items_extra_type6;
		obj.items_extra_type[6] = source.items_extra_type7;
		return obj;
	}

	public unsafe static MPScoreData convertMPStats(multiplayer_stats_export source)
	{
		MPScoreData mPScoreData = new MPScoreData();
		mPScoreData.version = 2;
		mPScoreData.valid = new int[9];
		mPScoreData.gold_acquired = new int[9];
		mPScoreData.max_population = new int[9];
		mPScoreData.fearfactor = new int[9];
		mPScoreData.time_deceased = new int[9];
		mPScoreData.who_killed_who = new int[81];
		mPScoreData.enemy_buildings_destroyed = new int[9];
		mPScoreData.food_produced = new int[9];
		mPScoreData.iron_produced = new int[9];
		mPScoreData.stone_produced = new int[9];
		mPScoreData.wood_produced = new int[9];
		mPScoreData.pitch_produced = new int[9];
		mPScoreData.minfearfactor = new int[9];
		mPScoreData.winners = new int[9];
		mPScoreData.troop_points_killed = new int[9];
		mPScoreData.enemy_buildings_razed_points = new int[9];
		mPScoreData.troops_produced = new int[9];
		mPScoreData.goods_received = new int[9];
		mPScoreData.goods_sent = new int[9];
		mPScoreData.notable_victories = new int[9];
		mPScoreData.notable_defeats = new int[9];
		mPScoreData.time_lord_killed = new int[9];
		mPScoreData.blank2 = new int[9];
		mPScoreData.blank3 = new int[9];
		mPScoreData.blank4 = new int[9];
		mPScoreData.weapons_produced = new int[9];
		mPScoreData.buildings_lost = new int[9];
		mPScoreData.lords_killed = new int[9];
		mPScoreData.team_shield = new int[9];
		mPScoreData.computer_register = new int[9];
		mPScoreData.teams = new int[9];
		for (int i = 0; i < 9; i++)
		{
			mPScoreData.valid[i] = source.valid[i];
			mPScoreData.gold_acquired[i] = source.gold_acquired[i];
			mPScoreData.max_population[i] = source.max_population[i];
			mPScoreData.fearfactor[i] = source.fearfactor[i];
			mPScoreData.time_deceased[i] = source.time_deceased[i];
			mPScoreData.enemy_buildings_destroyed[i] = source.enemy_buildings_destroyed[i];
			mPScoreData.food_produced[i] = source.food_produced[i];
			mPScoreData.iron_produced[i] = source.iron_produced[i];
			mPScoreData.stone_produced[i] = source.stone_produced[i];
			mPScoreData.wood_produced[i] = source.wood_produced[i];
			mPScoreData.pitch_produced[i] = source.pitch_produced[i];
			mPScoreData.minfearfactor[i] = source.minfearfactor[i];
			mPScoreData.lords_killed[i] = source.lords_killed[i];
			mPScoreData.winners[i] = source.winners[i];
			mPScoreData.troop_points_killed[i] = source.troop_points_killed[i];
			mPScoreData.enemy_buildings_razed_points[i] = source.enemy_buildings_razed_points[i];
			mPScoreData.troops_produced[i] = source.troops_produced[i];
			mPScoreData.goods_received[i] = source.goods_received[i];
			mPScoreData.goods_sent[i] = source.goods_sent[i];
			mPScoreData.notable_victories[i] = source.notable_victories[i];
			mPScoreData.notable_defeats[i] = source.notable_defeats[i];
			mPScoreData.time_lord_killed[i] = source.time_lord_killed[i];
			mPScoreData.blank2[i] = source.blank2[i];
			mPScoreData.blank3[i] = source.blank3[i];
			mPScoreData.blank4[i] = source.blank4[i];
			mPScoreData.weapons_produced[i] = source.weapons_produced[i];
			mPScoreData.buildings_lost[i] = source.buildings_lost[i];
			mPScoreData.team_shield[i] = source.team_shield[i];
			mPScoreData.computer_register[i] = source.computer_register[i];
			mPScoreData.teams[i] = source.teams[i];
		}
		for (int j = 0; j < 81; j++)
		{
			mPScoreData.who_killed_who[j] = source.who_killed_who[j];
		}
		mPScoreData.real_time = source.real_time;
		mPScoreData.game_time = source.game_time;
		mPScoreData.ranged_made = source.ranged_made;
		mPScoreData.melee_made = source.melee_made;
		mPScoreData.unique = source.unique;
		DateTime utcNow = DateTime.UtcNow;
		mPScoreData.completedDate_Year = utcNow.Year;
		mPScoreData.completedDate_Month = utcNow.Month;
		mPScoreData.completedDate_Day = utcNow.Day;
		mPScoreData.completedDate_Hour = utcNow.Hour;
		mPScoreData.completedDate_Minute = utcNow.Minute;
		mPScoreData.completedDate_Second = utcNow.Second;
		if (GameData.Instance.lastGameState != null)
		{
			mPScoreData.lord_type = GameData.Instance.lastGameState.lord_Type;
		}
		else
		{
			mPScoreData.lord_type = ConfigSettings.Settings_LordType;
		}
		return mPScoreData;
	}

	public static LoadMapReturnData loadTutorial()
	{
		lock (threadLock)
		{
			DLL_PreInitTutorial();
		}
		return loadMap(-1);
	}

	public static LoadMapReturnData loadCampaignMap(int campaignMapID, int difficulty = 1)
	{
		lock (threadLock)
		{
			DLL_PreInitMap_Campaign(difficulty);
		}
		return loadMap(campaignMapID);
	}

	public static LoadMapReturnData loadEcoCampaignMap(int campaignMapID, int difficulty = 1)
	{
		lock (threadLock)
		{
			DLL_PreInitMap_EcoCampaign();
			DLL_EcoCampaign_ChangeDifficulty(difficulty);
		}
		return loadMap(campaignMapID);
	}

	public unsafe static void setEcoCampaignDifficulty(Enums.GameDifficulty difficulty)
	{
		int[] array = new int[160];
		int num = 0;
		lock (threadLock)
		{
			fixed (int* retData = array)
			{
				num = DLL_EcoCampaign_ChangeDifficulty_briefing((int)difficulty, retData);
			}
		}
		for (int i = 0; i < num; i++)
		{
			GameData.scenario.updateEvent(array[i * 4], 1, 0, array[i * 4 + 1], array[i * 4 + 2], array[i * 4 + 3]);
		}
	}

	public unsafe static LoadMapReturnData loadSkirmishTrailMap(int trailType, int trailID, int difficulty)
	{
		lock (threadLock)
		{
			byte[] array = new byte[0];
			fixed (byte* restartInfo = array)
			{
				DLL_PreInitMap_Invasion(difficulty, array.Length, restartInfo);
			}
		}
		return loadMap(-1, "", dummy: false, multiplayerSave: false, trailType, trailID, ConfigSettings.Settings_Allow_Classic_Bedouin_Stockade);
	}

	public unsafe static LoadMapReturnData loadInvasionMap(string mapName, Enums.GameDifficulty difficulty, byte[] restartInfo)
	{
		lock (threadLock)
		{
			fixed (byte* restartInfo2 = restartInfo)
			{
				DLL_PreInitMap_Invasion((int)difficulty, restartInfo.Length, restartInfo2);
			}
		}
		return loadMap(-1, mapName);
	}

	public unsafe static LoadMapReturnData loadJustBuildMap(string mapName, bool advancedFreebuild, int freebuild_GoldLevel, int freebuild_FoodLevel, int freebuild_ResourcesLevel, int freebuild_WeaponsLevel, int freebuild_RandomEvents, int freebuild_Invasions, int freebuild_InvasionDifficulty, int freebuild_Peacetime, int freebuild_Opponents, bool removeHostileAnimals, bool freebuild_Extreme_Troops, bool freebuild_Extreme_Powers, bool freebuild_Defeat_On_Death, byte[] restartInfo)
	{
		lock (threadLock)
		{
			fixed (byte* restartInfo2 = restartInfo)
			{
				DLL_PreInitMap_JustBuild(advancedFreebuild, freebuild_GoldLevel, freebuild_FoodLevel, freebuild_ResourcesLevel, freebuild_WeaponsLevel, freebuild_RandomEvents, freebuild_Invasions, freebuild_InvasionDifficulty, freebuild_Peacetime, freebuild_Opponents, restartInfo.Length, removeHostileAnimals, freebuild_Extreme_Troops, freebuild_Extreme_Powers, freebuild_Defeat_On_Death, restartInfo2);
			}
		}
		return loadMap(-1, mapName);
	}

	public unsafe static LoadMapReturnData newMapEditor(int size, int gameType, bool siegeThat, bool multiplayerMap = false)
	{
		lock (threadLock)
		{
			byte[] array = new byte[Marshal.SizeOf(typeof(LoadMapReturnData))];
			fixed (byte* retData = array)
			{
				DLL_PreInitMap_Editor(size, gameType, siegeThat: false, multiplayerMap, retData);
			}
			int[] array2 = new int[9];
			fixed (int* retData2 = array2)
			{
				DLL_GetColourMapping(retData2, -1);
			}
			GameMap.instance.setColourMapping(array2);
			LoadMapReturnData result = Deserialize<LoadMapReturnData>(array);
			result.mapRotation++;
			return result;
		}
	}

	public unsafe static MultiplayerSetupData initMultiplayerGame(bool skirmishGame = false, byte[] restartInfo = null, int coopTrailID = 0, int coopMissionID = 0, bool trailMakerTestMode = false, bool customTrail = false, bool customisedExtremeTrail = false)
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(MultiplayerSetupTransferData))];
		lock (threadLock)
		{
			if (restartInfo == null)
			{
				restartInfo = new byte[0];
			}
			fixed (byte* retData = array)
			{
				fixed (byte* restartInfo2 = restartInfo)
				{
					DLL_PreInitMap_Multiplayer(retData, skirmishGame, restartInfo.Length, restartInfo2, coopTrailID, coopMissionID, trailMakerTestMode, customTrail, customisedExtremeTrail);
				}
			}
		}
		return getMPSetup(Deserialize<MultiplayerSetupTransferData>(array));
	}

	public unsafe static void setMultiplayerStartingData(MultiplayerSetupData setupData)
	{
		byte[] array = Serialize(setMPSetup(setupData));
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_ApplyMultiplayerSetupData(retData);
			}
		}
	}

	public static LoadMapReturnData loadMultiplayerMap(string mapName, bool multiplayerSave = false)
	{
		return loadMap(-1, mapName, dummy: false, multiplayerSave);
	}

	public unsafe static MultiplayerSetupData getMultiplayerStartingData()
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(MultiplayerSetupTransferData))];
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_GetMultiplayerSetupData(retData);
			}
		}
		return getMPSetup(Deserialize<MultiplayerSetupTransferData>(array));
	}

	public unsafe static void RegisterMPPlayer(int playerID, string name, int team, bool localPlayer, int lordType)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(name);
		lock (threadLock)
		{
			fixed (byte* name2 = bytes)
			{
				DLL_RegisterMultiplayerUser(playerID, name2, bytes.Length, team, localPlayer, lordType);
			}
		}
	}

	public static void RegisterSkirmishUser(int playerID, int AILord, int subType, int team)
	{
		lock (threadLock)
		{
			DLL_RegisterSkirmishUser(playerID, AILord, subType, team);
		}
	}

	public unsafe static void setCustomLordConfig(ref AILordConfigTransferData lordData, int playerID)
	{
		byte[] array = Serialize(lordData);
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_SetExtendedLordConfig(retData, playerID);
			}
		}
	}

	public unsafe static void SetMPRadarColours(int[] newMappings)
	{
		lock (threadLock)
		{
			fixed (int* newMappings2 = newMappings)
			{
				DLL_SetMPRadarColours(newMappings2);
			}
		}
	}

	public static int StartMultiplayerGame(bool fromSave)
	{
		lock (threadLock)
		{
			return DLL_StartMultiplayerGame(fromSave);
		}
	}

	public static void SetMPRandSeed(int seed)
	{
		lock (threadLock)
		{
			DLL_SetMPRandSeed(seed);
		}
	}

	public static int StartMultiplayerGameSynced()
	{
		lock (threadLock)
		{
			return DLL_StartMultiplayerGameSynced();
		}
	}

	public unsafe static void RemapPlayers(int[] newMappings, int newLocalPlayer)
	{
		lock (threadLock)
		{
			fixed (int* newMappings2 = newMappings)
			{
				DLL_RemapPlayers(newMappings2, newLocalPlayer);
			}
		}
	}

	public static void ConnectionPauseEngine(bool state)
	{
		lock (threadLock)
		{
			DLL_ConnectionPause(state);
		}
	}

	public unsafe static void ReceiveChore(int playerID, byte[] data, int dataLength)
	{
		lock (threadLock)
		{
			fixed (byte* data2 = data)
			{
				DLL_ReceiveChore(playerID, data2, dataLength);
			}
		}
	}

	public unsafe static void GetMultiplayerChatInfo(ref int[] players, ref int[] teams)
	{
		lock (threadLock)
		{
			int[] array = new int[9];
			int[] array2 = new int[9];
			fixed (int* players2 = array)
			{
				fixed (int* teams2 = array2)
				{
					DLL_GetMultiplayerChatInfo(players2, teams2);
				}
			}
			for (int i = 0; i < 9; i++)
			{
				players[i] = array[i];
				teams[i] = array2[i];
			}
		}
	}

	public static void KickMPPlayer(int playerID, bool kickImmediate)
	{
		lock (threadLock)
		{
			DLL_KickMPPlayer(playerID, kickImmediate);
		}
	}

	public static void PromoteMPHost(int playerID)
	{
		lock (threadLock)
		{
			DLL_PromoteMPHost(playerID);
		}
	}

	public static void SetAchData(int food, int wood, int weapons)
	{
		lock (threadLock)
		{
			DLL_SetAchValues(food, wood, weapons);
		}
	}

	public unsafe static LoadMapReturnData LoadSaveFile(string path)
	{
		lock (threadLock)
		{
			Director.instance.SetWaitCursor();
			byte[] bytes = Encoding.Unicode.GetBytes(path);
			byte[] array = new byte[Marshal.SizeOf(typeof(LoadMapReturnData))];
			int num;
			fixed (byte* data = bytes)
			{
				fixed (byte* retData = array)
				{
					num = DLL_LoadSaveGame(data, bytes.Length, retData, loadingEditorMap: false);
				}
			}
			if (num > 0)
			{
				int[] array2 = new int[9];
				fixed (int* retData2 = array2)
				{
					DLL_GetColourMapping(retData2, ConfigSettings.Settings_PlayerColour + 1);
				}
				GameMap.instance.setColourMapping(array2);
				LoadMapReturnData result = Deserialize<LoadMapReturnData>(array);
				result.mapRotation++;
				return result;
			}
			return default(LoadMapReturnData);
		}
	}

	public unsafe static LoadMapReturnData LoadMapFile(string path, bool editorMode)
	{
		lock (threadLock)
		{
			Director.instance.SetWaitCursor();
			fixed (byte* retData = new byte[Marshal.SizeOf(typeof(LoadMapReturnData))])
			{
				DLL_PreInitMap_Editor(160, 0, siegeThat: false, multiplayerMap: false, retData);
			}
			byte[] bytes = Encoding.Unicode.GetBytes(path);
			byte[] array = new byte[Marshal.SizeOf(typeof(LoadMapReturnData))];
			int num;
			fixed (byte* data = bytes)
			{
				fixed (byte* retData2 = array)
				{
					num = DLL_LoadSaveGame(data, bytes.Length, retData2, editorMode);
				}
			}
			if (num > 0)
			{
				int[] array2 = new int[9];
				fixed (int* retData3 = array2)
				{
					DLL_GetColourMapping(retData3, ConfigSettings.Settings_PlayerColour + 1);
				}
				GameMap.instance.setColourMapping(array2);
				LoadMapReturnData result = Deserialize<LoadMapReturnData>(array);
				result.mapRotation++;
				return result;
			}
			return default(LoadMapReturnData);
		}
	}

	public unsafe static bool SaveSaveGame(string path, int screenCentreX, int screenCentreY, int realScreenCentreX, int realScreenCentreY, bool lockMap = false, bool tempLockOnly = false, bool mapSave = false)
	{
		int num = 0;
		if (Director.instance.SafeToSave(wait: true))
		{
			Director.instance.SetWaitCursor();
			lock (threadLock)
			{
				byte[] bytes = Encoding.Unicode.GetBytes(path);
				fixed (byte* data = bytes)
				{
					num = DLL_SaveSaveGame(data, bytes.Length, screenCentreX, screenCentreY, realScreenCentreX, realScreenCentreY, lockMap, tempLockOnly, mapSave);
				}
			}
			Director.instance.FinishedSaving();
			Director.instance.ClearWaitCursor();
		}
		return num > 0;
	}

	public unsafe static bool CreateTrailMission(string path, byte[] restartInfo)
	{
		int num = 0;
		lock (threadLock)
		{
			byte[] bytes = Encoding.Unicode.GetBytes(path);
			fixed (byte* fileName = bytes)
			{
				fixed (byte* restartInfo2 = restartInfo)
				{
					num = DLL_CreateTrailMission(fileName, bytes.Length, restartInfo2, restartInfo.Length);
				}
			}
		}
		return num > 0;
	}

	public unsafe static LoadMapReturnData loadMap(int campaignMapID, string fileName = "", bool dummy = false, bool multiplayerSave = false, int trailType = -1, int trailID = 0, bool allow_classic_bedouins = false)
	{
		lock (threadLock)
		{
			flattenedLandscape = false;
			Director.instance.SetWaitCursor();
			string fileName2 = Path.GetFileName(fileName);
			byte[] bytes = Encoding.Unicode.GetBytes(fileName2);
			byte[] bytes2 = Encoding.Unicode.GetBytes(fileName);
			byte[] array = new byte[Marshal.SizeOf(typeof(LoadMapReturnData))];
			fixed (byte* fileName3 = bytes2)
			{
				fixed (byte* retData = array)
				{
					fixed (byte* mapName = bytes)
					{
						DLL_LoadMapToPlay(campaignMapID, fileName3, bytes2.Length, retData, dummy: false, mapName, bytes.Length, multiplayerSave, trailType, trailID, allow_classic_bedouins);
					}
				}
			}
			int[] array2 = new int[9];
			fixed (int* retData2 = array2)
			{
				DLL_GetColourMapping(retData2, ConfigSettings.Settings_PlayerColour + 1);
			}
			GameMap.instance.setColourMapping(array2);
			LoadMapReturnData result = Deserialize<LoadMapReturnData>(array);
			result.mapRotation++;
			EditorDirector.instance.clearMouseStateForEngine();
			return result;
		}
	}

	public unsafe static int GetCampaignLevel(string mapName)
	{
		lock (threadLock)
		{
			byte[] bytes = Encoding.Unicode.GetBytes(mapName);
			fixed (byte* path = bytes)
			{
				return DLL_CampaignLevel(path, bytes.Length);
			}
		}
	}

	public unsafe static int[] getTrailMissionLords(int trailType, int trailID)
	{
		lock (threadLock)
		{
			int[] array = new int[9];
			fixed (int* retData = array)
			{
				DLL_GetTrailMissionLords(retData, trailType, trailID);
			}
			return array;
		}
	}

	public unsafe static int[] getTrailMissionInfo(int trailType, int trailID, ref string mapName)
	{
		lock (threadLock)
		{
			int[] array = new int[38];
			byte[] array2 = new byte[1000];
			fixed (byte* mapName2 = array2)
			{
				fixed (int* retData = array)
				{
					DLL_GetTrailMissionInfo(retData, mapName2, trailType, trailID);
				}
			}
			mapName = Encoding.ASCII.GetString(array2).TrimEnd('\0');
			return array;
		}
	}

	public static void toggleFlattenedLandscapeMode()
	{
		flattenedLandscape = !flattenedLandscape;
		if (GameData.Instance.game_type == 4)
		{
			if (!flattenedLandscape)
			{
				TutorialAction(5);
			}
			else
			{
				TutorialAction(4);
			}
		}
		EditorDirector.instance.FlattenedLandscape();
	}

	public static void setFlattenedLandscapeMode(bool state)
	{
		flattenedLandscape = state;
	}

	public unsafe static int run(bool mpFrameSkip = false)
	{
		MemoryBuffers.MemBuffer freeBuffer = MemoryBuffers.instance.getFreeBuffer(writing: true);
		if (freeBuffer != null)
		{
			lock (threadLock)
			{
				int mouseOverX = -1;
				int mouseOverY = -1;
				EditorDirector.instance.preDLLCallActions(ref mouseOverX, ref mouseOverY);
				byte[] array = new byte[Marshal.SizeOf(typeof(PlayStateReturnData))];
				int num = 0;
				fixed (short* memory = freeBuffer.memory)
				{
					fixed (byte* radarMap = freeBuffer.radarMap)
					{
						fixed (byte* retData = array)
						{
							fixed (byte* mPChores = freeBuffer.MPChores)
							{
								fixed (int* selectedChimpsBuffer = selectedChimps)
								{
									num = DLL_RunTick(memory, radarMap, flattenedLandscape, mouseOverX, mouseOverY, EditorDirector.instance.shiftPressed, EditorDirector.instance.ctrlPressed, EditorDirector.instance.altPressed, retData, Director.instance.Paused, MyAudioManager.Instance.isAmbientPlaying(1), MyAudioManager.Instance.isAmbientPlaying(2), MyAudioManager.Instance.isSpeechPlaying(1), MyAudioManager.Instance.isSpeechPlaying(2), MyAudioManager.Instance.isMusicPlaying(), MyAudioManager.Instance.isMusicAboutToLoop(), SFXManager.instance.isBinkPlaying(), GameMap.instance.ScreenCentreTileScreenSpaceX, GameMap.instance.ScreenCentreTileScreenSpaceY, GameMap.instance.ScreenTilesWide, GameMap.instance.ScreenTilesHigh, GameMap.instance.RadarMapWidth, GameMap.instance.RadarMapHeight, GameMap.instance.RadarZoom, GameMap.instance.ScreenZoom, ConfigSettings.Settings_SH1RTSControls, ConfigSettings.Settings_TroopMoveMode, GameMap.instance.ScreenCentreTileX, GameMap.instance.ScreenCentreTileY, mPChores, selectedChimpsBuffer, mpFrameSkip, MainControls.instance.mouseTileClickDepth - 49, EditorDirector.instance.lastTroopOverDepth, GameMap.instance.cachedRenderBoundsLeft, GameMap.instance.cachedRenderBoundsRight, GameMap.instance.cachedRenderBoundsTop, GameMap.instance.cachedRenderBoundsBottom);
								}
							}
						}
					}
				}
				PlayStateReturnData source = Deserialize<PlayStateReturnData>(array);
				freeBuffer.gameState = CopyPlayStateStruct(source, selectedChimps);
				if (freeBuffer.gameState.SkipFrame > 0)
				{
					Director.instance.MPSkipFrame(freeBuffer.gameState.SkipFrame);
				}
				freeBuffer.numTiles = num;
				MemoryBuffers.instance.returnBuffer(freeBuffer);
				return num;
			}
		}
		return 0;
	}

	public unsafe static byte[] unpack(byte[] source)
	{
		if (source.Length <= 4)
		{
			return null;
		}
		lock (threadLock)
		{
			int num = 0;
			fixed (byte* source2 = source)
			{
				num = DLL_GetunpackSize(source2);
				if (num == 0 || num >= 10000000)
				{
					return null;
				}
				byte[] array = new byte[num];
				fixed (byte* dest = array)
				{
					DLL_Unpack(source2, dest, num);
					return array;
				}
			}
		}
	}

	public unsafe static byte[] pack(byte[] source)
	{
		lock (threadLock)
		{
			byte[] array = new byte[source.Length + 1000];
			int num = 0;
			fixed (byte* source2 = source)
			{
				fixed (byte* dest = array)
				{
					num = DLL_Pack(source2, dest, source.Length);
				}
			}
			if (num > 0)
			{
				byte[] array2 = new byte[num];
				Array.Copy(array, array2, num);
				return array2;
			}
			return null;
		}
	}

	public unsafe static byte[] unpackSavedRadar(byte[] source)
	{
		lock (threadLock)
		{
			byte[] array = new byte[160000];
			fixed (byte* source2 = source)
			{
				fixed (byte* dest = array)
				{
					if (DLL_UnpackRadarToARGB(source2, dest) > 0)
					{
						return array;
					}
					return null;
				}
			}
		}
	}

	public unsafe static byte[] getSaveRadar(ref int[] keep_locations, ref int world_size)
	{
		lock (threadLock)
		{
			keep_locations = new int[16];
			byte[] array = new byte[160000];
			fixed (int* keeps = keep_locations)
			{
				fixed (byte* dest = array)
				{
					world_size = DLL_GetSaveRadar(dest, keeps);
					if (world_size > 0)
					{
						return array;
					}
					return null;
				}
			}
		}
	}

	public unsafe static uint crc(byte[] source)
	{
		lock (threadLock)
		{
			fixed (byte* source2 = source)
			{
				return (uint)DLL_CRC(source2, source.Length);
			}
		}
	}

	public unsafe static uint crc(short[] source)
	{
		lock (threadLock)
		{
			fixed (short* source2 = source)
			{
				return (uint)DLL_CRCS(source2, source.Length);
			}
		}
	}

	public static void SetMapRotation(Enums.Dircs rotation, int centreX, int centreY)
	{
		lock (threadLock)
		{
			DLL_SetMapRotation((int)(rotation - 1), centreX, centreY);
			GameMap.instance.cameraMovedRecalcBounds(centreX, centreY, (int)rotation);
		}
	}

	public static int StartMapperItem(int item)
	{
		lock (threadLock)
		{
			return DLL_StartMapAction(item);
		}
	}

	public static void PlaceMapperItem(int item, int x, int y, int size, int player, bool inGameNotEditor, bool constructingOnly, int mouseState)
	{
		lock (threadLock)
		{
			int num = DLL_MapAction(item, x, y, size, player, inGameNotEditor, constructingOnly, mouseState);
			if (num >= 0)
			{
				if (MainViewModel.Instance.MEMode == 0 && Enum.IsDefined(typeof(Enums.eMappers), num))
				{
					EditorDirector.instance.mapEditorInteraction((Enums.eMappers)num);
				}
			}
			else if (num == -50)
			{
				EditorDirector.instance.CancelPlacement();
			}
		}
	}

	public static int GameAction(Enums.GameActionCommand command, int structureID, int state, int value2 = 0)
	{
		lock (threadLock)
		{
			int num = DLL_GameAction((int)command, structureID, state, value2);
			if (num > 0)
			{
				switch (command)
				{
				case Enums.GameActionCommand.CycleBookmarks:
				case Enums.GameActionCommand.RadarClicked:
				case Enums.GameActionCommand.CentreMarker:
				{
					int cameraX = num % 1000;
					int cameraY = num / 1000;
					GameMap.instance.cameraMovedRecalcBounds(cameraX, cameraY);
					break;
				}
				case Enums.GameActionCommand.MakeTroop:
				case Enums.GameActionCommand.BuyGoods:
				case Enums.GameActionCommand.SellGoods:
				case Enums.GameActionCommand.Ally_SendGoods:
				case Enums.GameActionCommand.Ally_RequestGoods:
					return num;
				case Enums.GameActionCommand.RotateBuilding:
					MainControls.instance.CurrentSubAction = num;
					break;
				default:
					if (Enum.IsDefined(typeof(Enums.eMappers), num))
					{
						EditorDirector.instance.placeBuilding((Enums.eMappers)num);
					}
					break;
				}
			}
			return 0;
		}
	}

	public static void GameAction(Enums.KeyFunctions command, int value1 = -1, int value2 = 0, int value3 = 0)
	{
		lock (threadLock)
		{
			int num = DLL_GameAction((int)command, value1, value2, value3);
			if (num <= 0)
			{
				return;
			}
			switch (command)
			{
			case Enums.KeyFunctions.HomeKeep:
			case Enums.KeyFunctions.Market:
			case Enums.KeyFunctions.Signpost:
			case Enums.KeyFunctions.Barracks:
			case Enums.KeyFunctions.Granary:
			case Enums.KeyFunctions.SelectClan0:
			case Enums.KeyFunctions.SelectClan1:
			case Enums.KeyFunctions.SelectClan2:
			case Enums.KeyFunctions.SelectClan3:
			case Enums.KeyFunctions.SelectClan4:
			case Enums.KeyFunctions.SelectClan5:
			case Enums.KeyFunctions.SelectClan6:
			case Enums.KeyFunctions.SelectClan7:
			case Enums.KeyFunctions.SelectClan8:
			case Enums.KeyFunctions.SelectClan9:
			case Enums.KeyFunctions.GotoBookmark0:
			case Enums.KeyFunctions.GotoBookmark1:
			case Enums.KeyFunctions.GotoBookmark2:
			case Enums.KeyFunctions.GotoBookmark3:
			case Enums.KeyFunctions.GotoBookmark4:
			case Enums.KeyFunctions.GotoBookmark5:
			case Enums.KeyFunctions.GotoBookmark6:
			case Enums.KeyFunctions.GotoBookmark7:
			case Enums.KeyFunctions.GotoBookmark8:
			case Enums.KeyFunctions.GotoBookmark9:
			case Enums.KeyFunctions.Lord:
			case Enums.KeyFunctions.CycleLord:
			case Enums.KeyFunctions.MercPost:
				if (num > 0)
				{
					int cameraX = num % 1000;
					int cameraY = num / 1000;
					GameMap.instance.cameraMovedRecalcBounds(cameraX, cameraY);
				}
				break;
			}
		}
	}

	public static void SetAutoTrade(int goods, bool on, int buyLevel, int sellLevel)
	{
		lock (threadLock)
		{
			DLL_GameAction(1052, goods, buyLevel, 0);
			DLL_GameAction(1053, goods, sellLevel, 0);
			if (on)
			{
				DLL_GameAction(1051, goods, 1, 0);
			}
			else
			{
				DLL_GameAction(1051, goods, 0, 0);
			}
		}
	}

	public unsafe static void TroopSelection(int mouseState, bool rightDown, bool rightUp, int[] selectedChimps, bool selection_on, bool selection_established, int[] underCursorChimps, int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps)
	{
		if (onScreenChimps == null)
		{
			onScreenChimps = new int[0];
		}
		if (underCursorChimps == null)
		{
			underCursorChimps = new int[0];
		}
		lock (threadLock)
		{
			if (selectedChimps != null)
			{
				fixed (int* ptr = selectedChimps)
				{
					fixed (int* underCursorChimps2 = underCursorChimps)
					{
						fixed (int* onScreenChimps2 = onScreenChimps)
						{
							DLL_TroopSelection(mouseState, rightDown, rightUp, selectedChimps.Length, ptr, selection_on, selection_established, underCursorChimps.Length, underCursorChimps2, mousePosX, mousePosY, overTopHalf, onScreenChimps.Length, onScreenChimps2);
						}
					}
				}
				return;
			}
			fixed (int* ptr2 = new int[0])
			{
				fixed (int* underCursorChimps3 = underCursorChimps)
				{
					fixed (int* onScreenChimps3 = onScreenChimps)
					{
						DLL_TroopSelection(mouseState, rightDown, rightUp, 0, ptr2, selection_on, selection_established, underCursorChimps.Length, underCursorChimps3, mousePosX, mousePosY, overTopHalf, onScreenChimps.Length, onScreenChimps3);
					}
				}
			}
		}
	}

	public unsafe static void TroopSelectionChanged(int[] selectedChimps)
	{
		lock (threadLock)
		{
			if (selectedChimps != null)
			{
				fixed (int* ptr = selectedChimps)
				{
					DLL_TroopSelectionChanged(selectedChimps.Length, ptr);
				}
			}
			else
			{
				fixed (int* ptr2 = new int[0])
				{
					DLL_TroopSelectionChanged(0, ptr2);
				}
			}
		}
	}

	public unsafe static void SetTrailTimes(int trailType, int[] times)
	{
		lock (threadLock)
		{
			fixed (int* times2 = times)
			{
				DLL_ImportTrailTimes(trailType, times2, handleExceptions: true);
			}
		}
	}

	public static void DeleteBuilding(int x, int y, int player = -1, bool inGameNotEditor = true, int mouseState = 0)
	{
		lock (threadLock)
		{
			DLL_MapAction(39, x, y, 0, player, inGameNotEditor, constructingOnly: false, mouseState);
		}
	}

	public static bool IsMapperAvailable(int mapper)
	{
		lock (threadLock)
		{
			return DLL_IsMapperAvailable(mapper) > 0;
		}
	}

	public static bool GetMapperCoords(int mapper, ref int x, ref int y)
	{
		lock (threadLock)
		{
			int num = DLL_GetMapperCoord(mapper);
			if (num == 1431655765)
			{
				return false;
			}
			x = (num >> 16) & 0xFFFF;
			y = num & 0xFFFF;
			return true;
		}
	}

	public static void SetEditorPlayer(int playerID)
	{
		lock (threadLock)
		{
			DLL_SetEditorPlayer(playerID);
		}
	}

	public unsafe static void SetUTF8MissionText(string missionText)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(missionText);
		lock (threadLock)
		{
			fixed (byte* text = bytes)
			{
				DLL_SetUTF8MissionText(text, bytes.Length);
			}
		}
	}

	public unsafe static void SetUTF8MapName(string mapName)
	{
		GameData.Instance.currentMapName = mapName;
		byte[] array = Encoding.ASCII.GetBytes(mapName);
		if (array.Length > 78)
		{
			byte[] array2 = new byte[78];
			for (int i = 0; i < 78; i++)
			{
				array2[i] = array[i];
			}
			array = array2;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(mapName);
		lock (threadLock)
		{
			fixed (byte* text = bytes)
			{
				fixed (byte* text2 = array)
				{
					DLL_SetUTF8MapName(text, bytes.Length, text2, array.Length);
				}
			}
		}
	}

	public static tl_event CreateNewScenarioEvent(ref int eventid)
	{
		eventid = DLL_CreateScenarioAction(3);
		if (eventid >= 0)
		{
			return GetScenarioEvent(eventid);
		}
		return null;
	}

	public static tl_invasion CreateNewScenarioInvasion(ref int eventid)
	{
		eventid = DLL_CreateScenarioAction(1);
		if (eventid >= 0)
		{
			return GetScenarioInvasion(eventid);
		}
		return null;
	}

	public static tl_message CreateNewScenarioMessage(ref int eventid)
	{
		eventid = DLL_CreateScenarioAction(2);
		if (eventid >= 0)
		{
			return GetScenarioMessage(eventid);
		}
		return null;
	}

	public unsafe static tl_event GetScenarioEvent(int eventID)
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(tl_eventF))];
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_GetScenarioEvent(eventID, retData);
			}
		}
		return convertTL_event(Deserialize<tl_eventF>(array));
	}

	public unsafe static tl_message GetScenarioMessage(int eventID)
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(tl_messageF))];
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_GetScenarioEvent(eventID, retData);
			}
		}
		return convertTL_message(Deserialize<tl_messageF>(array));
	}

	public unsafe static tl_invasion GetScenarioInvasion(int eventID)
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(tl_invasionF))];
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_GetScenarioInvasion(eventID, retData);
			}
		}
		return convertTL_invasion(Deserialize<tl_invasionF>(array));
	}

	public unsafe static ScenarioOverview GetScenarioOverview()
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(ScenarioOverviewReturnData))];
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_GetScenarioOverview(retData);
			}
		}
		return convertScenarioOverview(Deserialize<ScenarioOverviewReturnData>(array));
	}

	public unsafe static void ApplyScenarioEvent(int eventID, tl_event evnt)
	{
		byte[] array = Serialize(convertTL_event(evnt));
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_ApplyScenarioEvent(eventID, retData);
			}
		}
	}

	public unsafe static void ApplyScenarioInvasion(int eventID, tl_invasion inv)
	{
		byte[] array = Serialize(convertTL_invasion(inv));
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_ApplyScenarioInvasion(eventID, retData);
			}
		}
	}

	public static void DeleteScenarioEntry(int entryID)
	{
		lock (threadLock)
		{
			DLL_DeleteScenarioAction(entryID);
		}
	}

	public static void UpdateScenarioActionDate(int entryID, int year, int month)
	{
		lock (threadLock)
		{
			DLL_UpdateScenarioActionDate(entryID, year, month);
		}
	}

	public static int EditorChangeMap_Mode(bool changeToMP)
	{
		if (changeToMP)
		{
			return DLL_SetMapEditorParam(1, -1, -1, -1);
		}
		return DLL_SetMapEditorParam(0, -1, -1, -1);
	}

	public static int EditorChangeMap_GameType(Enums.GameModes mapType)
	{
		return DLL_SetMapEditorParam(-1, (int)mapType, -1, -1);
	}

	public static int EditorChangeMap_KotH(bool koth)
	{
		if (koth)
		{
			return DLL_SetMapEditorParam(-1, -1, 1, -1);
		}
		return DLL_SetMapEditorParam(-1, -1, 0, -1);
	}

	public static int EditorChangeMap_MapSize(int mapSize)
	{
		return DLL_SetMapEditorParam(-1, -1, -1, mapSize);
	}

	public static void SetAppMode(int app_mode, int app_sub_mode)
	{
		lock (threadLock)
		{
			DLL_SetAppMode(app_mode, app_sub_mode);
		}
	}

	public static void TutorialAction(int ID, int value = -1)
	{
		lock (threadLock)
		{
			DLL_TutorialAction(ID, value);
		}
	}

	public unsafe static int[,] GetMeritData()
	{
		int[] array = new int[45];
		lock (threadLock)
		{
			fixed (int* retData = array)
			{
				DLL_GetMeritData(retData);
			}
		}
		int[,] array2 = new int[9, 5];
		for (int i = 1; i < 9; i++)
		{
			array2[i, 0] = array[i * 5];
			array2[i, 1] = array[i * 5 + 1];
			array2[i, 2] = array[i * 5 + 2];
			array2[i, 3] = array[i * 5 + 3];
			array2[i, 4] = array[i * 5 + 4];
		}
		return array2;
	}

	public unsafe static ScoreData GetScoreData()
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(ScoreReturnData))];
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_GetScoreData(retData);
			}
		}
		return convertScoreData(Deserialize<ScoreReturnData>(array));
	}

	public unsafe static MPScoreData GetMPScoreData()
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(multiplayer_stats_export))];
		lock (threadLock)
		{
			fixed (byte* retData = array)
			{
				DLL_GetMPScoreData(retData);
			}
		}
		MPScoreData mPScoreData = convertMPStats(Deserialize<multiplayer_stats_export>(array));
		mPScoreData.playerName = new string[9];
		mPScoreData.colourMap1 = new int[9];
		mPScoreData.colourMap2 = new int[9];
		if (SpriteMapping.mpLoadRemapping == null)
		{
			for (int i = 0; i < 9; i++)
			{
				mPScoreData.colourMap1[i] = -1;
			}
		}
		else
		{
			for (int j = 0; j < 9; j++)
			{
				mPScoreData.colourMap1[j] = SpriteMapping.mpLoadRemapping[j];
			}
		}
		for (int k = 0; k < 9; k++)
		{
			mPScoreData.colourMap2[k] = SpriteMapping.remapColours[k];
		}
		for (int l = 0; l < 9; l++)
		{
			if (mPScoreData.valid[l] > 0)
			{
				mPScoreData.playerName[l] = Platform_Multiplayer.Instance.getSkirmishName(l);
			}
		}
		return mPScoreData;
	}

	public unsafe static LogicDebugInfo GetLayerDebug(int x, int y)
	{
		lock (threadLock)
		{
			byte[] array = new byte[Marshal.SizeOf(typeof(LogicDebugInfo))];
			fixed (byte* retData = array)
			{
				DLL_GetLayerDebug(x, y, retData);
			}
			return Deserialize<LogicDebugInfo>(array);
		}
	}

	public unsafe static byte[] GetLayerData(int layerID)
	{
		lock (threadLock)
		{
			byte[] array = new byte[640000];
			fixed (byte* retData = array)
			{
				DLL_GetLayerData(layerID, retData);
			}
			return array;
		}
	}

	public static void SetDebugMode(int action)
	{
		lock (threadLock)
		{
			DLL_SetDebugMode(action);
		}
	}

	public static byte[] Serialize<T>(T s) where T : struct
	{
		int num = Marshal.SizeOf(typeof(T));
		byte[] array = new byte[num];
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		Marshal.StructureToPtr(s, intPtr, fDeleteOld: true);
		Marshal.Copy(intPtr, array, 0, num);
		Marshal.FreeHGlobal(intPtr);
		return array;
	}

	public static T Deserialize<T>(byte[] array) where T : struct
	{
		int num = Marshal.SizeOf(typeof(T));
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		Marshal.Copy(array, 0, intPtr, num);
		T result = (T)Marshal.PtrToStructure(intPtr, typeof(T));
		Marshal.FreeHGlobal(intPtr);
		return result;
	}

	public static T DeserializeStr<T>(byte[] array) where T : class
	{
		int num = Marshal.SizeOf(typeof(T));
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		Marshal.Copy(array, 0, intPtr, num);
		T result = (T)Marshal.PtrToStructure(intPtr, typeof(T));
		Marshal.FreeHGlobal(intPtr);
		return result;
	}
}
