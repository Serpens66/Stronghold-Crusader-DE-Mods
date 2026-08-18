using System;
using System.Collections.Generic;
using System.IO;
using Noesis;

namespace CrusaderDE;

public class Translate
{
	public static readonly DependencyProperty InstanceProperty = DependencyProperty.Register("Instance", typeof(Translate), typeof(Translate), new PropertyMetadata((object)Instance));

	public readonly string StrongholdTextFileNew = "Assets/Text/Crusader.txt";

	public const string locale_english = "enus";

	public const string locale_german = "dede";

	public const string locale_french = "frfr";

	public const string locale_spanish = "eses";

	public const string locale_italian = "itit";

	public const string locale_polish = "plpl";

	public const string locale_russian = "ruru";

	public const string locale_portuguese = "ptbr";

	public const string locale_japanese = "jajp";

	public const string locale_korean = "kokr";

	public const string locale_simplified_chinese = "zhcn";

	public const string locale_traditional_chinese = "zhhk";

	public const string locale_czech = "cscz";

	public const string locale_turkish = "trtr";

	public const string locale_hungarian = "huhu";

	public const string locale_thai = "thth";

	public const string locale_ukrainian = "ukua";

	public const string locale_greek = "elgr";

	public const string locale_arabic = "ar";

	public const string locale_dutch = "nlnl";

	public const string locale_swedish = "svse";

	public const int maxTextSections = 370;

	public const int MAX_ML_MESSAGES = 1000;

	public string[] ML_message_text = new string[1000];

	public bool ExtraText_1_Loaded;

	public bool ExtraText_2_Loaded;

	public bool ExtraText_3_Loaded;

	public bool ExtraText_4_Loaded;

	public bool ExtraText_5_Loaded;

	public string[] SectionNames = new string[370]
	{
		"TEXT_COPYRIGHT", "TEXT_MONTHS", "TEXT_GOODS", "TEXT_POPULARITY_EFFECTS", "TEXT_STARTUP", "TEXT_MAINOPTIONS", "TEXT_LANGUAGE", "TEXT_BUBBLE_HELP_SUBTEXT", "TEXT_BUBBLE_HELP_TEXT", "TEXT_BUBBLE_HELP_DATA",
		"TEXT_FEEDBACK", "TEXT_MAPEDIT", "TEXT_DEMOSCORE", "TEXT_FREE2_", "TEXT_REPORTS", "TEXT_FREE3_", "TEXT_FREE4_", "TEXT_FREE5_", "TEXT_IN_GENERAL_BUILDINGS", "TEXT_IN_KEEP",
		"TEXT_IN_INN", "TEXT_FREE6_", "TEXT_IN_BARRACKS", "TEXT_IN_GRANARY", "TEXT_IN_HOUSE", "TEXT_IN_WOODCUTTERS_HUT", "TEXT_IN_OXEN_BASE", "TEXT_IN_IRON_MINE", "TEXT_IN_PITCH_DIGGER", "TEXT_IN_HUNTERS_HUT",
		"TEXT_IN_GOODS_YARD", "TEXT_IN_ARMOURY", "TEXT_IN_FLETCHERS_WORKSHOP", "TEXT_IN_BLACKSMITHS_WORKSHOP", "TEXT_IN_POLETURNERS_WORKSHOP", "TEXT_IN_ARMOURERS_WORKSHOP", "TEXT_IN_TANNERS_WORKSHOP", "TEXT_IN_BAKERS_WORKSHOP", "TEXT_IN_BREWERS_WORKSHOP", "TEXT_IN_QUARRY",
		"TEXT_IN_QUARRYPILE", "TEXT_IN_HEALERS", "TEXT_IN_ENGINEERS_GUILD", "TEXT_IN_TUNNELLERS_GUILD", "TEXT_IN_TRADEPOST", "TEXT_IN_WELL", "TEXT_IN_OIL_SMELTER", "TEXT_IN_SIEGE_TENT", "TEXT_IN_WHEATFARM", "TEXT_IN_HOPSFARM",
		"TEXT_IN_APPLEFARM", "TEXT_IN_CATTLEFARM", "TEXT_IN_MILL", "TEXT_IN_STABLES", "TEXT_IN_CHURCH", "TEXT_IN_GATEHOUSE", "TEXT_IN_DRAWBRIDGE", "TEXT_IN_POSTERN_GATE", "TEXT_IN_TUNNEL_ENTERANCE", "TEXT_IN_CAMP_FIRE",
		"TEXT_IN_SIGNPOST", "TEXT_IN_KILLING_PIT", "TEXT_IN_CATAPULT", "TEXT_IN_TREBUCHET", "TEXT_IN_OUTPOST", "TEXT_IN_TOWER", "TEXT_IN_GALLOWS", "TEXT_IN_STOCKS", "TEXT_IN_WITCH_HOIST", "TEXT_IN_MAYPOLE",
		"TEXT_FREE7_", "TEXT_IN_TRAINING_GROUND", "TEXT_IN_GARDEN", "TEXT_FREE8_", "TEXT_GAME_OPTIONS", "TEXT_HELP", "TEXT_MULTIPLAYER_CONNECTION", "TEXT_PANEL_FEEDBACK", "TEXT_STRUCTURE_WAS", "TEXT_XPLAY_WAITING_ROOM",
		"TEXT_MISSION_BUTTONS", "TEXT_OBJECTIVES", "TEXT_REPORT_BUTTONS", "TEXT_PLAYER_DESC", "TEXT_PEASANT_NAMES", "TEXT_PEASANT_SURNAMES", "TEXT_UNIT_ACTIONS", "TEXT_MARRIAGE", "TEXT_CHIMP_NAMES", "TEXT_CHIMP_COMMENT",
		"TEXT_FREE9_", "TEXT_FREE10_", "TEXT_NEWMAP_TYPES_HELP", "TEXT_INSULTS", "TEXT_PREVIEW", "TEXT_TUTORIAL", "TEXT_TUTORIAL_BUTTONS", "TEXT_MAP_SCREEN", "TEXT_FREE11_", "TEXT_MISSION1_STORY",
		"TEXT_MISSION1_BRIEFING", "TEXT_MISSION1_OBJECTIVES", "TEXT_MISSION1_HINTS", "TEXT_MISSION2_STORY", "TEXT_MISSION2_BRIEFING", "TEXT_MISSION2_OBJECTIVES", "TEXT_MISSION2_HINTS", "TEXT_MISSION3_STORY", "TEXT_MISSION3_BRIEFING", "TEXT_MISSION3_OBJECTIVES",
		"TEXT_MISSION3_HINTS", "TEXT_MISSION4_STORY", "TEXT_MISSION4_BRIEFING", "TEXT_MISSION4_OBJECTIVES", "TEXT_MISSION4_HINTS", "TEXT_MISSION5_STORY", "TEXT_MISSION5_BRIEFING", "TEXT_MISSION5_OBJECTIVES", "TEXT_MISSION5_HINTS", "TEXT_MISSION6_STORY",
		"TEXT_MISSION6_BRIEFING", "TEXT_MISSION6_OBJECTIVE", "TEXT_MISSION6_HINTS", "TEXT_MISSION7_STORY", "TEXT_MISSION7_BRIEFING", "TEXT_MISSION7_OBJECTIVES", "TEXT_MISSION7_HINTS", "TEXT_MISSION8_STORY", "TEXT_MISSION8_BRIEFING", "TEXT_MISSION8_OBJECTIVES",
		"TEXT_MISSION8_HINTS", "TEXT_MISSION9_STORY", "TEXT_MISSION9_BRIEFING", "TEXT_MISSION9_OBJECTIVES", "TEXT_MISSION9_HINTS", "TEXT_MISSION10_STORY", "TEXT_MISSION10_BRIEFING", "TEXT_MISSION10_OBJECTIVES", "TEXT_MISSION10_HINTS", "TEXT_MISSION11_STORY",
		"TEXT_MISSION11_BRIEFING", "TEXT_MISSION11_OBJECTIVES", "TEXT_MISSION11_HINTS", "TEXT_MISSION12_STORY", "TEXT_MISSION12_BRIEFING", "TEXT_MISSION12_OBJECTIVES", "TEXT_MISSION12_HINTS", "TEXT_MISSION13_STORY", "TEXT_MISSION13_BRIEFING", "TEXT_MISSION13_OBJECTIVES",
		"TEXT_MISSION13_HINTS", "TEXT_MISSION14_STORY", "TEXT_MISSION14_BRIEFING", "TEXT_MISSION14_OBJECTIVES", "TEXT_MISSION14_HINTS", "TEXT_MISSION15_STORY", "TEXT_MISSION15_BRIEFING", "TEXT_MISSION15_OBJECTIVES", "TEXT_MISSION15_HINTS", "TEXT_MISSION16_STORY",
		"TEXT_MISSION16_BRIEFING", "TEXT_MISSION16_OBJECTIVES", "TEXT_MISSION16_HINTS", "TEXT_MISSION17_STORY", "TEXT_MISSION17_BRIEFING", "TEXT_MISSION17_OBJECTIVES", "TEXT_MISSION17_HINTS", "TEXT_MISSION18_STORY", "TEXT_MISSION18_BRIEFING", "TEXT_MISSION18_OBJECTIVES",
		"TEXT_MISSION18_HINTS", "TEXT_MISSION19_STORY", "TEXT_MISSION19_BRIEFING", "TEXT_MISSION19_OBJECTIVES", "TEXT_MISSION19_HINTS", "TEXT_MISSION20_STORY", "TEXT_MISSION20_BRIEFING", "TEXT_MISSION20_OBJECTIVES", "TEXT_MISSION20_HINTS", "TEXT_CAMPAIGN_INFO",
		"TEXT_FREE12c_", "TEXT_FREE12b_", "TEXT_FREE12a_", "TEXT_FREE12_", "TEXT_FREE_x1", "TEXT_FREE_x2", "TEXT_FREE13_", "TEXT_FREE14_", "TEXT_FREE15_", "TEXT_DEMO_BRIEFINGS",
		"TEXT_HINTS", "TEXT_ECO1_HINTS", "TEXT_ECO2_HINTS", "TEXT_ECO3_HINTS", "TEXT_ECO4_HINTS", "TEXT_ECO5_HINTS", "TEXT_ECO_MISSION_BRIEFINGS", "TEXT_MISSION_NAMES", "TEXT_PREATTACK", "TEXT_SCENARIO",
		"TEXT_TRADER_NAMES", "TEXT_ACTION", "TEXT_IN_CESS_PIT", "TEXT_IN_BURNING_STAKE", "TEXT_IN_GIBBET", "TEXT_IN_DUNGEON", "TEXT_IN_STRETCHING_RACK", "TEXT_IN_FLOGGING_RACK", "TEXT_IN_CHOPPING_BLOCK", "TEXT_IN_DUNKING_STOOL",
		"TEXT_IN_DOG_CAGE", "TEXT_IN_STATUE", "TEXT_IN_SHRINE", "TEXT_IN_BEEHIVE", "TEXT_IN_DANCING_BEAR", "TEXT_IN_POND", "TEXT_IN_BEAR_CAVE", "TEXT_IN_WATERPOT", "TEXT_IN_CATHEDRAL", "TEXT_MAP_NAMES",
		"TEXT_SCENARIO_OPP", "TEXT_SKIRMISH_SPEECH", "TEXT_FREE21_", "TEXT_CUSTOM_HOOKS", "TEXT_ALLIES", "TEXT_MP_RANK", "TEXT_NEW_PRE_ATTACK", "TEXT_CUSTOMISATION", "TEXT_NEW_CTEXT", "TEXT_MP_VERSION_CONTROL",
		"TEXT_NEW_TEXT", "TEXT_HOT_KEYS", "TEXT_NEW_DEMO", "TEXT_NEW_TEXT2", "TEXT_SANDS_OF_TIME", "TEXT_BUILDING_DESCRIPTIONS", "TEXT_CREDITS", "TEXT_SUBTITLES", "TEXT_ECOBRIEFINGS", "TEXT_OTHER",
		"TEXT_SKIRMISH_MASTERS", "TEXT_GAME_TYPE", "TEXT_SKIRMISH_CHOOSE", "TEXT_ROADMAP", "TEXT_FREE41_", "TEXT_TRAIL_NAMES_CRU", "TEXT_FREE42_", "TEXT_EXTREME_DEMO", "TEXT_EXTREME_POWERS", "TEXT_DEMO_GAMENAMES",
		"TEXT_SKIRMISH_CHOOSE2", "TEXT_SHC_STANDALONE", "TEXT_SKIRMISH_MISC", "TEXT_SKTRAIL_WIN", "TEXT_ALLIES2", "TEXT_SKMASTERS", "TEXT_CHEATS", "TEXT_MISC2", "TEXT_FREE52_", "TEXT_ISLAMIC",
		"TEXT_COOP", "TEXT_TROOP_HELP", "TEXT_AI_LORD_HELP", "TEXT_FREE_x6", "TEXT_FREE_x7", "TEXT_FREE_x8", "TEXT_FREE_x9", "TEXT_FREE_x0", "TEXT_FREE_x10", "TEXT_FREE_x11",
		"TEXT_MISSION21_STORY", "TEXT_MISSION21_BRIEFING", "TEXT_MISSION21_OBJECTIVES", "TEXT_MISSION21_HINTS", "TEXT_MISSION22_STORY", "TEXT_MISSION22_BRIEFING", "TEXT_MISSION22_OBJECTIVES", "TEXT_MISSION22_HINTS", "TEXT_MISSION23_STORY", "TEXT_MISSION23_BRIEFING",
		"TEXT_MISSION23_OBJECTIVES", "TEXT_MISSION23_HINTS", "TEXT_MISSION24_STORY", "TEXT_MISSION24_BRIEFING", "TEXT_MISSION24_OBJECTIVES", "TEXT_MISSION24_HINTS", "TEXT_MISSION25_STORY", "TEXT_MISSION25_BRIEFING", "TEXT_MISSION25_OBJECTIVES", "TEXT_MISSION25_HINTS",
		"TEXT_MISSION26_STORY", "TEXT_MISSION26_BRIEFING", "TEXT_MISSION26_OBJECTIVES", "TEXT_MISSION26_HINTS", "TEXT_MISSION27_STORY", "TEXT_MISSION27_BRIEFING", "TEXT_MISSION27_OBJECTIVES", "TEXT_MISSION27_HINTS", "TEXT_MISSION28_STORY", "TEXT_MISSION28_BRIEFING",
		"TEXT_MISSION28_OBJECTIVES", "TEXT_MISSION28_HINTS", "TEXT_MISSION29_STORY", "TEXT_MISSION29_BRIEFING", "TEXT_MISSION29_OBJECTIVES", "TEXT_MISSION29_HINTS", "TEXT_MISSION30_STORY", "TEXT_MISSION30_BRIEFING", "TEXT_MISSION30_OBJECTIVES", "TEXT_MISSION30_HINTS",
		"TEXT_MISSION31_STORY", "TEXT_MISSION31_BRIEFING", "TEXT_MISSION31_OBJECTIVES", "TEXT_MISSION31_HINTS", "TEXT_MISSION32_STORY", "TEXT_MISSION32_BRIEFING", "TEXT_MISSION32_OBJECTIVES", "TEXT_MISSION32_HINTS", "TEXT_MISSION33_STORY", "TEXT_MISSION33_BRIEFING",
		"TEXT_MISSION33_OBJECTIVES", "TEXT_MISSION33_HINTS", "TEXT_MISSION34_STORY", "TEXT_MISSION34_BRIEFING", "TEXT_MISSION34_OBJECTIVES", "TEXT_MISSION34_HINTS", "TEXT_MISSION35_STORY", "TEXT_MISSION35_BRIEFING", "TEXT_MISSION35_OBJECTIVES", "TEXT_MISSION35_HINTS",
		"TEXT_FREE_x12", "TEXT_FREE_x13", "TEXT_FREE_x14", "TEXT_FREE_x15", "TEXT_FREE_x16", "TEXT_FREE_x17", "TEXT_FREE_x18", "TEXT_FREE_x19", "TEXT_FREE_x20", "TEXT_FREE_x21",
		"TEXT_FREE_x22", "TEXT_FREE_x23", "TEXT_FREE_x24", "TEXT_FREE_x25", "TEXT_FREE_x26", "TEXT_FREE_x27", "TEXT_FREE_x28", "TEXT_FREE_x29", "TEXT_FREE_x30", "TEXT_FREE_x31",
		"TEXT_FREE_x32", "TEXT_FREE_x33", "TEXT_FREE_x34", "TEXT_FREE_x35", "TEXT_FREE_x36", "TEXT_FREE_x37", "TEXT_FREE_x38", "TEXT_FREE_x39", "TEXT_FREE_x40", "TEXT_FREE_x41",
		"TEXT_FREE_x42", "TEXT_FREE_x43", "TEXT_FREE_x44", "TEXT_FREE_x45", "TEXT_FREE_x46", "TEXT_FREE_x47", "TEXT_FREE_x48", "TEXT_FREE_x49", "TEXT_FREE_x50", "TEXT_FREE_x51"
	};

	public Dictionary<string, int> builtInMapNameTranslations;

	public Dictionary<string, int> builtInMapDescExtremeTranslations;

	public static Translate Instance { get; } = new Translate();

	public Dictionary<string, string> GameTexts { get; set; }

	public Translate()
	{
		SetupGameText();
		LoadTextFileNew(StrongholdTextFileNew);
	}

	public void SetupGameText()
	{
		GameTexts = new Dictionary<string, string>();
	}

	public string lookUpText(string index)
	{
		if (index == "")
		{
			return index;
		}
		return GameTexts[index];
	}

	public string lookUpText(string sectionString, int index)
	{
		index++;
		string key = sectionString + "_" + index.ToString("D3");
		return GameTexts[key];
	}

	public string lookUpText(Enums.eTextSections section, Enums.eTextValues index)
	{
		return lookUpText(section, (int)index);
	}

	public string lookUpText(Enums.eTextSections section, int index)
	{
		index++;
		string key = section.ToString() + "_" + index.ToString("D3");
		return GameTexts[key];
	}

	public void CONVERT_FILENAME(string mapName, int textID, int descID = -1)
	{
		builtInMapNameTranslations[mapName.ToLowerInvariant()] = textID;
		builtInMapDescExtremeTranslations[mapName.ToLowerInvariant()] = descID;
	}

	public string translateMapNames(string userMapName, ref string description)
	{
		description = "";
		if (builtInMapNameTranslations == null)
		{
			builtInMapNameTranslations = new Dictionary<string, int>();
			builtInMapDescExtremeTranslations = new Dictionary<string, int>();
			CONVERT_FILENAME("A resourceful divide", 1);
			CONVERT_FILENAME("A New Land", 111);
			CONVERT_FILENAME("A Friend Indeed", 61);
			CONVERT_FILENAME("Antioch", 87);
			CONVERT_FILENAME("A Mighty Oasis", 93);
			CONVERT_FILENAME("Armenia", 83);
			CONVERT_FILENAME("Arnon River", 123);
			CONVERT_FILENAME("Border Patrol", 79);
			CONVERT_FILENAME("Bow Ridge", 11);
			CONVERT_FILENAME("Broken Dune", 125);
			CONVERT_FILENAME("Centre of the Oasis", 13);
			CONVERT_FILENAME("Close Encounters", 31);
			CONVERT_FILENAME("Crete Peninsula", 133);
			CONVERT_FILENAME("Cactus Valley", 95);
			CONVERT_FILENAME("Canyons", 97);
			CONVERT_FILENAME("Crusader Demo", 109);
			CONVERT_FILENAME("Cyclades", 135);
			CONVERT_FILENAME("Caesarea Swampland", 127);
			CONVERT_FILENAME("Coconut Twist", 129);
			CONVERT_FILENAME("Craggy Cliffs", 131);
			CONVERT_FILENAME("Desert Island Blues", 17);
			CONVERT_FILENAME("Drawn and Quartered", 137);
			CONVERT_FILENAME("Dunes of Nicaea", 139);
			CONVERT_FILENAME("Empty Handed", 63);
			CONVERT_FILENAME("Edessa", 85);
			CONVERT_FILENAME("Flood Plains of Jordan", 141);
			CONVERT_FILENAME("Green Belt", 27);
			CONVERT_FILENAME("Green Haven", 39);
			CONVERT_FILENAME("Great Euphrates", 143);
			CONVERT_FILENAME("Happy Land", 29);
			CONVERT_FILENAME("Height Advantage", 9);
			CONVERT_FILENAME("Hell On The Hill", 73);
			CONVERT_FILENAME("Hilltop Hideout", 71);
			CONVERT_FILENAME("Halys River", 145);
			CONVERT_FILENAME("Hills of Antioch", 149);
			CONVERT_FILENAME("Hidden Crater", 147);
			CONVERT_FILENAME("Inches Apart", 53);
			CONVERT_FILENAME("Island Hoppin", 37);
			CONVERT_FILENAME("Its a jungle out there", 69);
			CONVERT_FILENAME("In The canyons", 97);
			CONVERT_FILENAME("In The Shadow", 113);
			CONVERT_FILENAME("Its just not fair", 121);
			CONVERT_FILENAME("Love Thy Neighbour", 57);
			CONVERT_FILENAME("Large Barren Desert", 99);
			CONVERT_FILENAME("Large Island", 101);
			CONVERT_FILENAME("Land of the Cactus", 119);
			CONVERT_FILENAME("Lacus Magnus", 151);
			CONVERT_FILENAME("Lakes of Konya", 153);
			CONVERT_FILENAME("Lake Qaddas", 155);
			CONVERT_FILENAME("Litani and Jordan", 157);
			CONVERT_FILENAME("No Escape", 33);
			CONVERT_FILENAME("North vs South", 41);
			CONVERT_FILENAME("Marshy Mayhem", 65);
			CONVERT_FILENAME("Melos", 159);
			CONVERT_FILENAME("Oasis Struggle", 3);
			CONVERT_FILENAME("Oasis by the Sea", 107);
			CONVERT_FILENAME("Piggy in the middle", 23);
			CONVERT_FILENAME("Pig in a Poke", 161);
			CONVERT_FILENAME("Province of Bodrum", 163);
			CONVERT_FILENAME("Riverside Rampage", 67);
			CONVERT_FILENAME("Rocky Oasis", 103);
			CONVERT_FILENAME("Reed Sea", 165);
			CONVERT_FILENAME("Region of Corinth", 167);
			CONVERT_FILENAME("Rock Face", 169);
			CONVERT_FILENAME("Sleeping With The Enemy", 55);
			CONVERT_FILENAME("Strati", 171);
			CONVERT_FILENAME("Small Barren Desert", 81);
			CONVERT_FILENAME("Small Island", 105);
			CONVERT_FILENAME("Tyre", 91);
			CONVERT_FILENAME("Tripoli", 89);
			CONVERT_FILENAME("Thasos", 173);
			CONVERT_FILENAME("The Valley", 77);
			CONVERT_FILENAME("The Bulls Eye", 21);
			CONVERT_FILENAME("Too Close For Comfort", 5);
			CONVERT_FILENAME("Target Zone", 15);
			CONVERT_FILENAME("Trapesac Island", 177);
			CONVERT_FILENAME("The ford across the river", 7);
			CONVERT_FILENAME("The Forest Oasis", 51);
			CONVERT_FILENAME("The Great lake", 25);
			CONVERT_FILENAME("The Guardians", 47);
			CONVERT_FILENAME("The Killing Plains", 35);
			CONVERT_FILENAME("The Last Stand", 49);
			CONVERT_FILENAME("The River", 19);
			CONVERT_FILENAME("The Trench", 59);
			CONVERT_FILENAME("The Wet Lands", 75);
			CONVERT_FILENAME("The Dunes", 117);
			CONVERT_FILENAME("Tilos", 175);
			CONVERT_FILENAME("Two in a bed", 179);
			CONVERT_FILENAME("Upwards Alliance", 43);
			CONVERT_FILENAME("West Coast", 45);
			CONVERT_FILENAME("Watering Holes", 115);
			CONVERT_FILENAME("Wall of Iron", 181);
			CONVERT_FILENAME("The Great River", 183);
			CONVERT_FILENAME("The Spine", 185);
			CONVERT_FILENAME("Two Rivers", 187);
			CONVERT_FILENAME("TheGreatProvider", 189);
			CONVERT_FILENAME("StarfishBay", 191);
			CONVERT_FILENAME("Archipelago", 195);
			CONVERT_FILENAME("Two Rivers", 197);
			CONVERT_FILENAME("IsleOfTears", 199);
			CONVERT_FILENAME("Outcrop", 201);
			CONVERT_FILENAME("A Layered Approach", 203);
			CONVERT_FILENAME("Headland", 205);
			CONVERT_FILENAME("GrainBasket", 207);
			CONVERT_FILENAME("TheFavourite", 209);
			CONVERT_FILENAME("HopeSprings", 211);
			CONVERT_FILENAME("Crater Lake", 213);
			CONVERT_FILENAME("Rocky Falls", 215);
			CONVERT_FILENAME("Convergence", 217);
			CONVERT_FILENAME("CrowdedIsland", 219);
			CONVERT_FILENAME("WorldsEnd", 221);
			CONVERT_FILENAME("TheCrateredCoast", 223);
			CONVERT_FILENAME("Coastal Warfare", 225);
			CONVERT_FILENAME("Marshy Crossroads", 227);
			CONVERT_FILENAME("TippedScales", 229);
			CONVERT_FILENAME("Protected", 231);
			CONVERT_FILENAME("CrossedStreams", 233);
			CONVERT_FILENAME("HighRoad", 235);
			CONVERT_FILENAME("Serpent's Gorge", 237);
			CONVERT_FILENAME("TheDivide", 239);
			CONVERT_FILENAME("Cramped Crossing", 241);
			CONVERT_FILENAME("Snakehead", 243);
			CONVERT_FILENAME("Shattered Peninsula", 245);
			CONVERT_FILENAME("Verdant Border", 247);
			CONVERT_FILENAME("RiftValley", 249);
			CONVERT_FILENAME("TheGreatDivide", 251);
			CONVERT_FILENAME("Encircled", 253);
			CONVERT_FILENAME("Splintered Cradle", 255);
			CONVERT_FILENAME("Province of Bodrum OP", 257);
			CONVERT_FILENAME("A Mightier Oasis", 289);
			CONVERT_FILENAME("Back to Back", 291);
			CONVERT_FILENAME("Custom Scenario", 293);
			CONVERT_FILENAME("Distant Encounters", 295);
			CONVERT_FILENAME("Castaways", 297);
			CONVERT_FILENAME("Temple Island", 299);
			CONVERT_FILENAME("The Thirsty Trail", 301);
			CONVERT_FILENAME("CanalNetwork", 303);
			CONVERT_FILENAME("EndlessSands", 305);
			CONVERT_FILENAME("HeadOfTheValley", 307);
			CONVERT_FILENAME("IslandsInTheStream", 309);
			CONVERT_FILENAME("TheTrident", 311);
			CONVERT_FILENAME("Outreach", 313);
			CONVERT_FILENAME("The Impasse", 315);
			CONVERT_FILENAME("TheJordanValley", 317);
			CONVERT_FILENAME("Conquest", 319);
			CONVERT_FILENAME("BigBend", 321);
			CONVERT_FILENAME("CanyonRidge", 323);
			CONVERT_FILENAME("DividedConquerors", 325);
			CONVERT_FILENAME("EmeraldCliff", 327);
			CONVERT_FILENAME("FerricFlow", 329);
			CONVERT_FILENAME("OldWounds", 331);
			CONVERT_FILENAME("RidgeRoad", 333);
			CONVERT_FILENAME("Iron Rush", 335);
			CONVERT_FILENAME("Mind Games", 337);
			CONVERT_FILENAME("Approaching Seas", 339);
			CONVERT_FILENAME("Oasis Crossing", 341);
			CONVERT_FILENAME("Ancient River", 343);
			CONVERT_FILENAME("The Rivers Edge", 345);
			CONVERT_FILENAME("Fractured Frontier", 347);
			CONVERT_FILENAME("Oasis by the River", 349);
			CONVERT_FILENAME("The Lush Strip", 351);
			CONVERT_FILENAME("AbandonedIsland", 353);
			CONVERT_FILENAME("OasisScramble", 355);
			CONVERT_FILENAME("Possession", 357);
			CONVERT_FILENAME("TallOrder", 359);
			CONVERT_FILENAME("TheTreeOfLife", 361);
			CONVERT_FILENAME("CenterOfPower", 363);
			CONVERT_FILENAME("Fortification", 365);
			CONVERT_FILENAME("OpenOcean", 367);
			CONVERT_FILENAME("PathToVictory", 369);
			CONVERT_FILENAME("SeparationAnxiety", 371);
			CONVERT_FILENAME("TheRunways", 373);
			CONVERT_FILENAME("TheSinkhole", 375);
			CONVERT_FILENAME("TriRiverIsland", 377);
			CONVERT_FILENAME("Crocodiles nest", 379);
			CONVERT_FILENAME("Best_Friends", -93, 259);
			CONVERT_FILENAME("Bird_In_Flight", -82, 260);
			CONVERT_FILENAME("Coastal_Trap", -87, 261);
			CONVERT_FILENAME("Crossroads", -99, 262);
			CONVERT_FILENAME("Divided", -111, 263);
			CONVERT_FILENAME("Enclosure", -90, 264);
			CONVERT_FILENAME("Fury_Bay", -83, 265);
			CONVERT_FILENAME("Jealous_Neighbours", -96, 266);
			CONVERT_FILENAME("Lionheart", -98, 267);
			CONVERT_FILENAME("Look_Out", -81, 268);
			CONVERT_FILENAME("MP-Divided", -101, 269);
			CONVERT_FILENAME("MP-Downhill Scrum", -102, 270);
			CONVERT_FILENAME("MP-No Mans Land", -103, 271);
			CONVERT_FILENAME("MP-No Where to Hide", -104, 272);
			CONVERT_FILENAME("MP-Slopes of Doom", -105, 273);
			CONVERT_FILENAME("MP-Snake River", -106, 274);
			CONVERT_FILENAME("MP-Surrounded", -107, 275);
			CONVERT_FILENAME("MP-The Rocky Divide", -108, 276);
			CONVERT_FILENAME("MP-Two Falls", -109, 277);
			CONVERT_FILENAME("MP-Valley of the Lords", -110, 278);
			CONVERT_FILENAME("Phoenix", -86, 279);
			CONVERT_FILENAME("Rivers_Fork", -89, 280);
			CONVERT_FILENAME("Snake_River", -84, 281);
			CONVERT_FILENAME("Spider_Island", -94, 282);
			CONVERT_FILENAME("Swampy_Island", -85, 283);
			CONVERT_FILENAME("The_Host", -92, 284);
			CONVERT_FILENAME("Three_Little_Pigs", -97, 285);
			CONVERT_FILENAME("Ultimate_Victory", -100, 286);
			CONVERT_FILENAME("Wazirs_Fortress", -88, 287);
			CONVERT_FILENAME("Wide_Open_Plain", -91, 288);
		}
		if (builtInMapNameTranslations.TryGetValue(userMapName.ToLowerInvariant(), out var value))
		{
			if (value < 0)
			{
				if (builtInMapDescExtremeTranslations.TryGetValue(userMapName.ToLowerInvariant(), out var value2))
				{
					description = lookUpText(Enums.eTextSections.TEXT_MAP_NAMES, value2);
				}
				else
				{
					description = "";
				}
				return lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, -value);
			}
			description = lookUpText(Enums.eTextSections.TEXT_MAP_NAMES, value + 1);
			return lookUpText(Enums.eTextSections.TEXT_MAP_NAMES, value);
		}
		return userMapName;
	}

	public string getMessageLibraryText(int index)
	{
		if (index >= 0 && index < 1000)
		{
			return ML_message_text[index];
		}
		return "";
	}

	public bool LoadTextFileNew(string filePath)
	{
		try
		{
			string[] array = File.ReadAllLines(filePath);
			int num = 0;
			int i = 0;
			int num2 = 0;
			string text = "";
			for (; i < array.Length; i++)
			{
				if (array[i].ToLowerInvariant() == ">>textstart")
				{
					i++;
					break;
				}
			}
			while (i < array.Length)
			{
				if (array[i] == "")
				{
					i++;
					continue;
				}
				if (array[i].Length >= 3 && array[i][0] == '>' && array[i][1] == '>')
				{
					num++;
					if (num > 370)
					{
						break;
					}
					i++;
					num2 = 0;
					text = SectionNames[num - 1];
					continue;
				}
				string text2 = array[i];
				text2 = text2.Replace("\\n", "\n");
				string text3 = "D3";
				if (num2 + 1 >= 1000)
				{
					text3 = "D4";
				}
				string text4 = text + "_" + (num2 + 1).ToString(text3);
				string value = text2;
				GameTexts.Add(text4, value);
				switch (text4.ToLower())
				{
				case "text_new_text_024":
				{
					string key13 = "TEXT_SCENARIO_195";
					GameTexts[key13] = text2;
					key13 = "TEXT_SCENARIO_197";
					GameTexts[key13] = text2;
					key13 = "TEXT_SCENARIO_198";
					GameTexts[key13] = text2;
					key13 = "TEXT_SCENARIO_199";
					GameTexts[key13] = text2;
					key13 = "TEXT_SCENARIO_201";
					GameTexts[key13] = text2;
					break;
				}
				case "text_new_text_026":
				{
					string key12 = "TEXT_SCENARIO_181";
					GameTexts[key12] = text2;
					break;
				}
				case "text_new_text_027":
				{
					string key11 = "TEXT_SCENARIO_182";
					GameTexts[key11] = text2;
					break;
				}
				case "text_new_text2_010":
				{
					string key10 = "TEXT_SCENARIO_183";
					GameTexts[key10] = text2;
					break;
				}
				case "text_new_text2_046":
				{
					string key9 = "TEXT_SCENARIO_184";
					GameTexts[key9] = text2;
					break;
				}
				case "text_new_text2_047":
				{
					string key8 = "TEXT_SCENARIO_185";
					GameTexts[key8] = text2;
					break;
				}
				case "text_new_text_090":
				{
					string key7 = "TEXT_SCENARIO_196";
					GameTexts[key7] = text2;
					break;
				}
				case "text_new_text2_007":
				{
					string key6 = "TEXT_SCENARIO_200";
					GameTexts[key6] = text2;
					break;
				}
				case "text_new_text2_148":
				{
					string key5 = "TEXT_SCENARIO_203";
					GameTexts[key5] = text2;
					break;
				}
				case "text_new_text2_149":
				{
					string key4 = "TEXT_SCENARIO_204";
					GameTexts[key4] = text2;
					break;
				}
				case "text_bubble_help_text_288":
				{
					string key3 = "TEXT_HOT_KEYS_135";
					GameTexts[key3] = text2;
					break;
				}
				case "text_bubble_help_text_289":
				{
					string key2 = "TEXT_HOT_KEYS_136";
					GameTexts[key2] = text2;
					break;
				}
				case "text_bubble_help_text_290":
				{
					string key = "TEXT_HOT_KEYS_137";
					GameTexts[key] = text2;
					break;
				}
				}
				num2++;
				i++;
			}
			GameTexts["TEXT_SCENARIO_202"] = GameTexts["TEXT_SCENARIO_115"];
			MainViewModel.Instance.SetLocalisedLayout(GameTexts["TEXT_LANGUAGE_002"]);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool LoadExtraText(string filePath, string tag)
	{
		try
		{
			string[] array = File.ReadAllLines(filePath);
			int num = 0;
			while (num < array.Length)
			{
				string key = tag + "_" + (num + 1).ToString("D3");
				string text = array[num];
				text = text.Replace("\\n", "\n");
				GameTexts.Add(key, text);
				num++;
				if (num == 70)
				{
					break;
				}
			}
			num = 70;
			string key2 = tag + "_" + (num + 1).ToString("D3");
			string value = StripMission(array[5]);
			GameTexts.Add(key2, value);
			num++;
			key2 = tag + "_" + (num + 1).ToString("D3");
			value = StripMission(array[8]);
			GameTexts.Add(key2, value);
			num++;
			key2 = tag + "_" + (num + 1).ToString("D3");
			value = StripMission(array[11]);
			GameTexts.Add(key2, value);
			num++;
			key2 = tag + "_" + (num + 1).ToString("D3");
			value = StripMission(array[14]);
			GameTexts.Add(key2, value);
			num++;
			key2 = tag + "_" + (num + 1).ToString("D3");
			value = StripMission(array[17]);
			GameTexts.Add(key2, value);
			num++;
			key2 = tag + "_" + (num + 1).ToString("D3");
			value = StripMission(array[20]);
			GameTexts.Add(key2, value);
			num++;
			key2 = tag + "_" + (num + 1).ToString("D3");
			value = StripMission(array[23]);
			GameTexts.Add(key2, value);
			num++;
			return true;
		}
		catch (Exception)
		{
			string value2 = " ";
			for (int i = 0; i < 80; i++)
			{
				string key3 = tag + "_" + (i + 1).ToString("D3");
				GameTexts.Add(key3, value2);
			}
			return false;
		}
	}

	public string StripMission(string str)
	{
		int num = str.IndexOf(':');
		if (num > 0)
		{
			string text = str.Substring(num + 1).TrimStart();
			if (text.Length > 2 && (FatControler.french || FatControler.spanish || FatControler.italian))
			{
				object arg = char.ToUpper(text[0]);
				int length = text.Length;
				int num2 = 1;
				int length2 = length - num2;
				return $"{arg}{text.Substring(num2, length2)}";
			}
			return text;
		}
		num = str.IndexOf('：');
		if (num > 0)
		{
			return str.Substring(num + 1).TrimStart();
		}
		return str;
	}

	public string GetLordName(int lordType)
	{
		if (lordType >= 25)
		{
			return Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 453 + 17 * (lordType - 25));
		}
		if (lordType >= 16)
		{
			return Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 88 + 9 * (lordType - 16));
		}
		return Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 239 + 9 * lordType);
	}
}
