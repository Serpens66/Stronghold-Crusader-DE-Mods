using System;
using System.Collections.Generic;
using System.IO;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class Avatars
{
	public class AvatarDesign
	{
		public Enums.AvatarItems background = Enums.AvatarItems.BackMask_002;

		public Enums.AvatarItems background_colour1 = Enums.AvatarItems.Colour_RED;

		public Enums.AvatarItems background_colour2 = Enums.AvatarItems.Colour_GREEN;

		public Enums.AvatarItems item = Enums.AvatarItems.Item_SCRIBE;

		public Enums.AvatarItems item_colour1 = Enums.AvatarItems.Colour_GOLD;

		public Enums.AvatarItems item_colour2 = Enums.AvatarItems.Colour_Alt_GOLD;

		public override string ToString()
		{
			string[] obj = new string[6]
			{
				((int)(background - 1000)).ToString("D2"),
				null,
				null,
				null,
				null,
				null
			};
			int num = (int)background_colour1;
			obj[1] = num.ToString("D2");
			num = (int)background_colour2;
			obj[2] = num.ToString("D2");
			obj[3] = ((int)(item - 2000)).ToString("D3");
			num = (int)item_colour1;
			obj[4] = num.ToString("D2");
			obj[5] = ((int)(item_colour2 - 100)).ToString("D2");
			return string.Concat(obj);
		}

		public void FromString(string str)
		{
			if (str.Length == 13)
			{
				string s = str.Substring(0, 2);
				string s2 = str.Substring(2, 2);
				string s3 = str.Substring(4, 2);
				string s4 = str.Substring(6, 3);
				string s5 = str.Substring(9, 2);
				string s6 = str.Substring(11, 2);
				try
				{
					background = (Enums.AvatarItems)(int.Parse(s) + 1000);
					background_colour1 = (Enums.AvatarItems)int.Parse(s2);
					background_colour2 = (Enums.AvatarItems)int.Parse(s3);
					item = (Enums.AvatarItems)(int.Parse(s4) + 2000);
					item_colour1 = (Enums.AvatarItems)int.Parse(s5);
					item_colour2 = (Enums.AvatarItems)(int.Parse(s6) + 100);
				}
				catch (Exception)
				{
					background = Enums.AvatarItems.BackMask_001;
					background_colour1 = Enums.AvatarItems.Colour_RED;
					background_colour2 = Enums.AvatarItems.Colour_GREEN;
					item = Enums.AvatarItems.Item_SCRIBE;
					item_colour1 = Enums.AvatarItems.Colour_GOLD;
					item_colour2 = Enums.AvatarItems.Colour_Alt_GOLD;
				}
			}
		}
	}

	private class AvatarItemDefinition
	{
		public Enums.AvatarItems type;

		public string fileName1;

		public string fileName2;

		public string fileName3;

		public string fileName4;

		public AvatarItemDefinition(Enums.AvatarItems t, string f)
		{
			type = t;
			fileName1 = f;
		}

		public AvatarItemDefinition(Enums.AvatarItems t, string f1, string f2, string f3, string f4 = "")
		{
			type = t;
			fileName1 = f1;
			fileName2 = f2;
			fileName3 = f3;
			fileName4 = f4;
		}
	}

	private class AvatarData
	{
		public Enums.AvatarItems type;

		public int id;

		public byte[] dataSolid;

		public byte[] dataUIMask1;

		public byte[] dataMask1;

		public byte[] dataMask2;

		public byte[] dataOverlay;

		public ImageSource iconTexture;
	}

	public static Avatars Instance;

	public static TextureSource TESTAVATAR;

	public const int SIZE = 260;

	private Dictionary<Enums.AvatarItems, AvatarData> avatarItems = new Dictionary<Enums.AvatarItems, AvatarData>();

	private AvatarItemDefinition[] colourLoader = new AvatarItemDefinition[50]
	{
		new AvatarItemDefinition(Enums.AvatarItems.Colour_BLACK, "BLACK_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_BLUE, "BLUE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_BROWN, "BROWN_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_DARKBLUE, "DARKBLUE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_DARKBROWN, "DARKBROWN_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_DARKGREEN, "DARKGREEN_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_DARKPURPLE, "DARKPURPLE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_DARKRED, "DARKRED_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_FLESH, "FLESH_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_GOLD, "GOLD_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_GREEN, "GREEN_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_GREY, "GREY_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_LIGHTBLUE, "LIGHTBLUE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_LIGHTGREEN, "LIGHTGREEN_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_LIGHTORANGE, "LIGHTORANGE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_LIGHTPINK, "LIGHTPINK_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_LIGHTPURPLE, "LIGHTPURPLE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_LIGHTYELLOW, "LIGHTYELLOW_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_ORANGE, "ORANGE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_PINK, "PINK_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_PURPLE, "PURPLE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_RED, "RED_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_TEAL, "TEAL_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_TURQUOISE, "TURQUOISE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_WHITE, "WHITE_Base"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_BLACK, "BLACK_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_BLUE, "BLUE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_BROWN, "BROWN_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_DARKBLUE, "DARKBLUE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_DARKBROWN, "DARKBROWN_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_DARKGREEN, "DARKGREEN_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_DARKPURPLE, "DARKPURPLE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_DARKRED, "DARKRED_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_FLESH, "FLESH_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_GOLD, "GOLD_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_GREEN, "GREEN_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_GREY, "GREY_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_LIGHTBLUE, "LIGHTBLUE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_LIGHTGREEN, "LIGHTGREEN_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_LIGHTORANGE, "LIGHTORANGE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_LIGHTPINK, "LIGHTPINK_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_LIGHTPURPLE, "LIGHTPURPLE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_LIGHTYELLOW, "LIGHTYELLOW_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_ORANGE, "ORANGE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_PINK, "PINK_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_PURPLE, "PURPLE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_RED, "RED_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_TEAL, "TEAL_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_TURQUOISE, "TURQUOISE_Base_Darker"),
		new AvatarItemDefinition(Enums.AvatarItems.Colour_Alt_WHITE, "WHITE_Base_Darker")
	};

	private AvatarItemDefinition[] maskLoader = new AvatarItemDefinition[91]
	{
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_001, "Base_Mask_1"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_002, "Base_Mask_2"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_003, "Base_Mask_3"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_004, "Base_Mask_4"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_005, "Base_Mask_5"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_006, "Base_Mask_6"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_007, "Base_Mask_7"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_008, "Base_Mask_8"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_009, "Base_Mask_9"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_010, "Base_Mask_10"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_011, "Base_Mask_11"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_012, "Base_Mask_12"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_013, "Base_Mask_13"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_014, "Base_Mask_14"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_015, "Base_Mask_15"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_016, "Base_Mask_16"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_017, "Base_Mask_17"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_018, "Base_Mask_18"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_019, "Base_Mask_19"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_020, "Base_Mask_20"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_021, "Base_Mask_21"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_022, "Base_Mask_22"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_023, "Base_Mask_23"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_024, "Base_Mask_24"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_025, "Base_Mask_25"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_026, "Base_Mask_26"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_027, "Base_Mask_27"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_028, "Base_Mask_28"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_029, "Base_Mask_29"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_030, "Base_Mask_30"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_031, "Base_Mask_31"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_032, "Base_Mask_32"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_033, "Base_Mask_33"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_034, "Base_Mask_34"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_035, "Base_Mask_35"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_036, "Base_Mask_36"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_037, "Base_Mask_37"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_038, "Base_Mask_38"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_039, "Base_Mask_39"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_040, "Base_Mask_40"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_041, "Base_Mask_41"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_042, "Base_Mask_42"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_043, "Base_Mask_43"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_044, "Base_Mask_44"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_045, "Base_Mask_45"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_046, "Base_Mask_46"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_047, "Base_Mask_47"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_048, "Base_Mask_48"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_049, "Base_Mask_49"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_050, "Base_Mask_50"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_051, "Base_Mask_51"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_052, "Base_Mask_52"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_053, "Base_Mask_53"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_054, "Base_Mask_54"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_055, "Base_Mask_55"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_056, "Base_Mask_56"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_057, "Base_Mask_57"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_058, "Base_Mask_58"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_059, "Base_Mask_59"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_060, "Base_Mask_60"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_061, "Base_Mask_61"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_062, "Base_Mask_62"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_063, "Base_Mask_63"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_064, "Base_Mask_64"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_065, "Base_Mask_65"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_066, "Base_Mask_66"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_067, "Base_Mask_67"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_068, "Base_Mask_68"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_069, "Base_Mask_69"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_070, "Base_Mask_70"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_071, "Base_Mask_71"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_072, "Base_Mask_72"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_073, "Base_Mask_73"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_074, "Base_Mask_74"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_075, "Base_Mask_75"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_076, "Base_Mask_76"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_077, "Base_Mask_77"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_078, "Base_Mask_78"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_079, "Base_Mask_79"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_080, "Base_Mask_80"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_081, "Base_Mask_81"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_082, "Base_Mask_82"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_083, "Base_Mask_83"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_084, "Base_Mask_84"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_085, "Base_Mask_85"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_086, "Base_Mask_86"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_087, "Base_Mask_87"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_088, "Base_Mask_88"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_089, "Base_Mask_89"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_090, "Base_Mask_90"),
		new AvatarItemDefinition(Enums.AvatarItems.BackMask_091, "Base_Mask_91")
	};

	private AvatarItemDefinition[] itemLoader = new AvatarItemDefinition[90]
	{
		new AvatarItemDefinition(Enums.AvatarItems.Item_BLANK, "", "", "", "UI_MASK_BLANK"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_AF, "Emblem_Mask_AF", "", "Emblem_Outline_AF"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_AM, "Emblem_Mask_AM", "", "Emblem_Outline_AM"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_BESSY, "Emblem_Mask_BESSY", "", "Emblem_Outline_BESSY"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_BF, "Emblem_Mask_BF", "", "Emblem_Outline_BF"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_BM, "Emblem_Mask_BM", "", "Emblem_Outline_BM"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_COTL, "Emblem_Mask_COTL", "Emblem_Colour2_MASK_COTL", "Emblem_Outline_COTL"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_EF, "Emblem_Mask_EF", "", "Emblem_Outline_EF"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_EM, "Emblem_Mask_EM", "Emblem_Colour2_Mask_EM", "Emblem_Outline_EM"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_GOING, "Emblem_Mask_GOING", "Emblem_Colour2_MASK_GOING", "Emblem_Outline_GOING"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_GUNGEON, "Emblem_Mask_GUNGEON", "Emblem_Colour2_MASK_GUNGEON", "Emblem_Outline_GUNGEON"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_INK, "Emblem_Mask_INK", "Emblem_Colour2_MASK_INK", "Emblem_Outline_INK"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_INSCRYPTION, "Emblem_Mask_INSCRYPTION", "Emblem_Colour2_MASK_INSCRYPTION", "Emblem_Outline_INSCRYPTION"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_KZERO, "Emblem_Mask_KZERO", "Emblem_Colour2_MASK_KZERO", "Emblem_Outline_KZERO"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_NEVA, "Emblem_Mask_NEVA", "Emblem_Colour2_MASK_NEVA", "Emblem_Outline_NEVA"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_NORTH, "Emblem_Mask_NORTH", "Emblem_Colour2_MASK_NORTH", "Emblem_Outline_NORTH"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_REIGNS, "Emblem_Mask_REIGNS", "Emblem_Colour2_MASK_REIGNS", "Emblem_Outline_REIGNS"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SCRIBE, "Emblem_Mask_SCRIBE", "", "Emblem_Outline_SCRIBE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_STYX, "Emblem_Mask_STYX", "Emblem_Colour2_MASK_STYX", "Emblem_Outline_STYX"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_TALOS, "Emblem_Mask_TALOS", "Emblem_Colour2_MASK_TALOS", "Emblem_Outline_TALOS"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_THRONE, "Emblem_Mask_THRONE", "Emblem_Colour2_MASK_THRONE", "Emblem_Outline_THRONE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_VOLVY, "Emblem_Mask_VOLVY", "Emblem_Colour2_MASK_VOLVY", "Emblem_Outline_VOLVY"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_KTC, "Emblem_Mask_KTC", "Emblem_Colour2_MASK_KTC", "Emblem_Outline_KTC"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SABERS, "Emblem_Mask_SABERS", "", "Emblem_Outline_SABERS"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SARROW, "Emblem_Mask_SARROW", "Emblem_Colour2_MASK_SARROW", "Emblem_Outline_SARROW"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SCHECKER, "Emblem_Mask_SCHECKER", "Emblem_Colour2_MASK_SCHECKER", "Emblem_Outline_SCHECKER"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SCROSS, "Emblem_Mask_SCROSS", "Emblem_Colour2_MASK_SCROSS", "Emblem_Outline_SCROSS"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SDRAGON, "Emblem_Mask_SDRAGON", "Emblem_Colour2_MASK_SDRAGON", "Emblem_Outline_SDRAGON"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SFDL, "Emblem_Mask_SFDL", "Emblem_Colour2_MASK_SFDL", "Emblem_Outline_SFDL"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SKULL, "Emblem_Mask_SKULL", "", "Emblem_Outline_SKULL"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SKULL2, "Emblem_Mask_SKULL2", "", "Emblem_Outline_SKULL2"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SLION, "Emblem_Mask_SLION", "Emblem_Colour2_MASK_SLION", "Emblem_Outline_SLION"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SMOON, "Emblem_Mask_SMOON", "Emblem_Colour2_MASK_SMOON", "Emblem_Outline_SMOON"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SPHX, "Emblem_Mask_SPHX", "Emblem_Colour2_MASK_SPHX", "Emblem_Outline_SPHX"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SSTRIPE, "Emblem_Mask_SSTRIPE", "Emblem_Colour2_MASK_SSTRIPE", "Emblem_Outline_SSTRIPE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SSUN, "Emblem_Mask_SSUN", "Emblem_Colour2_MASK_SSUN", "Emblem_Outline_SSUN"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SUNICORN, "Emblem_Mask_SUNICORN", "Emblem_Colour2_MASK_SUNICORN", "Emblem_Outline_SUNICORN"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SWORDS, "Emblem_Mask_SWORDS", "", "Emblem_Outline_SWORDS"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_ABBOT, "Emblem_Mask_ABBOT", "Emblem_Colour2_MASK_ABBOT", "Emblem_Outline_ABBOT"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CALIPH, "Emblem_Mask_CALIPH", "Emblem_Colour2_MASK_CALIPH", "Emblem_Outline_CALIPH"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CANARY, "Emblem_Mask_CANARY", "Emblem_Colour2_MASK_CANARY", "Emblem_Outline_CANARY"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CROCODILE, "Emblem_Mask_CROCODILE", "Emblem_Colour2_MASK_CROCODILE", "Emblem_Outline_CROCODILE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_EMIR, "Emblem_Mask_EMIR", "Emblem_Colour2_MASK_EMIR", "Emblem_Outline_EMIR"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_FREDERICK, "Emblem_Mask_FREDERICK", "Emblem_Colour2_MASK_FREDERICK", "Emblem_Outline_FREDERICK"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_JEWEL, "Emblem_Mask_JEWEL", "Emblem_Colour2_MASK_JEWEL", "Emblem_Outline_JEWEL"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_KAHINAH, "Emblem_Mask_KAHINAH", "Emblem_Colour2_MASK_KAHINAH", "Emblem_Outline_KAHINAH"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_MARSHAL, "Emblem_Mask_MARSHAL", "Emblem_Colour2_MASK_MARSHAL", "Emblem_Outline_MARSHAL"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_NIZAR, "Emblem_Mask_NIZAR", "Emblem_Colour2_MASK_NIZAR", "Emblem_Outline_NIZAR"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_NOMAD, "Emblem_Mask_NOMAD", "Emblem_Colour2_MASK_NOMAD", "Emblem_Outline_NOMAD"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_PHILIP, "Emblem_Mask_PHILIP", "Emblem_Colour2_MASK_PHILIP", "Emblem_Outline_PHILIP"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_PIG, "Emblem_Mask_PIG", "Emblem_Colour2_MASK_PIG", "Emblem_Outline_PIG"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_RAT, "Emblem_Mask_RAT", "Emblem_Colour2_MASK_RAT", "Emblem_Outline_RAT"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_RICHARD, "Emblem_Mask_RICHARD", "Emblem_Colour2_MASK_RICHARD", "Emblem_Outline_RICHARD"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SALADIN, "Emblem_Mask_SALADIN", "Emblem_Colour2_MASK_SALADIN", "Emblem_Outline_SALADIN"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SHERIFF, "Emblem_Mask_SHERIFF", "Emblem_Colour2_MASK_SHERIFF", "Emblem_Outline_SHERIFF"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SNAKE, "Emblem_Mask_SNAKE", "Emblem_Colour2_MASK_SNAKE", "Emblem_Outline_SNAKE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SULTAN, "Emblem_Mask_SULTAN", "Emblem_Colour2_MASK_SULTAN", "Emblem_Outline_SULTAN"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_TRADER, "Emblem_Mask_TRADER", "Emblem_Colour2_MASK_TRADER", "Emblem_Outline_TRADER"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_WAZIR, "Emblem_Mask_WAZIR", "Emblem_Colour2_MASK_WAZIR", "Emblem_Outline_WAZIR"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_WOLF, "Emblem_Mask_WOLF", "Emblem_Colour2_MASK_WOLF", "Emblem_Outline_WOLF"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SENTINEL, "Emblem_Mask_SENTINEL", "Emblem_Colour2_MASK_SENTINEL", "Emblem_Outline_SENTINEL"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_GOAT, "Emblem_Mask_GOAT", "Emblem_Colour2_MASK_GOAT", "Emblem_Outline_GOAT"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_JACKAL, "Emblem_Mask_JACKAL", "Emblem_Colour2_MASK_JACKAL", "Emblem_Outline_JACKAL"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_LAMB, "Emblem_Mask_LAMB", "Emblem_Colour2_MASK_LAMB", "Emblem_Outline_LAMB"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_LEOPARD, "Emblem_Mask_LEOPARD", "Emblem_Colour2_MASK_LEOPARD", "Emblem_Outline_LEOPARD"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_FALCON, "Emblem_Mask_FALCON", "Emblem_Colour2_MASK_FALCON", "Emblem_Outline_FALCON"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_LIONESS, "Emblem_Mask_LIONESS", "Emblem_Colour2_MASK_LIONESS", "Emblem_Outline_LIONESS"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SERGEANT, "Emblem_Mask_SERGEANT", "Emblem_Colour2_MASK_SERGEANT", "Emblem_Outline_SERGEANT"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_COBRA, "Emblem_Mask_COBRA", "Emblem_Colour2_MASK_COBRA", "Emblem_Outline_COBRA"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_FISH, "Emblem_Mask_FISH", "Emblem_Colour2_MASK_FISH", "Emblem_Outline_FISH"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CROWN, "Emblem_Mask_CROWN", "Emblem_Colour2_MASK_CROWN", "Emblem_Outline_CROWN"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_ROSE, "Emblem_Mask_ROSE", "Emblem_Colour2_MASK_ROSE", "Emblem_Outline_ROSE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_FLOWERS, "Emblem_Mask_FLOWERS", "Emblem_Colour2_MASK_FLOWERS", "Emblem_Outline_FLOWERS"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CAT, "Emblem_Mask_CAT", "Emblem_Colour2_MASK_CAT", "Emblem_Outline_CAT"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_PAW, "Emblem_Mask_PAW", "Emblem_Colour2_MASK_PAW", "Emblem_Outline_PAW"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SUN, "Emblem_Mask_SUN", "Emblem_Colour2_MASK_SUN", "Emblem_Outline_SUN"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_ANKH, "Emblem_Mask_ANKH", "Emblem_Colour2_MASK_ANKH", "Emblem_Outline_ANKH"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CROSSA, "Emblem_Mask_CROSSA", "Emblem_Colour2_MASK_CROSSA", "Emblem_Outline_CROSSA"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CROSSB, "Emblem_Mask_CROSSB", "Emblem_Colour2_MASK_CROSSB", "Emblem_Outline_CROSSB"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CROSSC, "Emblem_Mask_CROSSC", "Emblem_Colour2_MASK_CROSSC", "Emblem_Outline_CROSSC"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CROSSD, "Emblem_Mask_CROSSD", "Emblem_Colour2_MASK_CROSSD", "Emblem_Outline_CROSSD"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CIRCLE, "Emblem_Mask_CIRCLE", "Emblem_Colour2_MASK_CIRCLE", "Emblem_Outline_CIRCLE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CIRCLEB, "Emblem_Mask_CIRCLEB", "Emblem_Colour2_MASK_CIRCLEB", "Emblem_Outline_CIRCLEB"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_CIRCLEC, "Emblem_Mask_CIRCLEC", "Emblem_Colour2_MASK_CIRCLEC", "Emblem_Outline_CIRCLEC"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_BALDWIN, "Emblem_Mask_BALDWIN", "Emblem_Colour2_MASK_BALDWIN", "Emblem_Outline_BALDWIN"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_BULLSEYE, "Emblem_Mask_BULLSEYE", "Emblem_Colour2_MASK_BULLSEYE", "Emblem_Outline_BULLSEYE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_SURGEON, "Emblem_Mask_SURGEON", "Emblem_Colour2_MASK_SURGEON", "Emblem_Outline_SURGEON"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_BAIBARS, "Emblem_Mask_BAIBARS", "Emblem_Colour2_MASK_BAIBARS", "Emblem_Outline_BAIBARS"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_VULTURE, "Emblem_Mask_VULTURE", "Emblem_Colour2_MASK_VULTURE", "Emblem_Outline_VULTURE"),
		new AvatarItemDefinition(Enums.AvatarItems.Item_FOOLKING, "Emblem_Mask_FOOL", "Emblem_Colour2_MASK_FOOL", "Emblem_Outline_FOOL")
	};

	private bool avatarsInitialised;

	public static void InitAvatars()
	{
		if (Instance == null)
		{
			Instance = new Avatars();
			Instance.init();
		}
	}

	private void init()
	{
		if (avatarsInitialised)
		{
			return;
		}
		avatarsInitialised = true;
		AvatarItemDefinition[] array = colourLoader;
		foreach (AvatarItemDefinition avatarItemDefinition in array)
		{
			AvatarData avatarData = new AvatarData();
			avatarData.type = avatarItemDefinition.type;
			avatarData.dataSolid = loadSolid(avatarItemDefinition.fileName1);
			avatarItems[avatarItemDefinition.type] = avatarData;
		}
		array = maskLoader;
		foreach (AvatarItemDefinition avatarItemDefinition2 in array)
		{
			AvatarData avatarData2 = new AvatarData();
			avatarData2.type = avatarItemDefinition2.type;
			avatarData2.dataMask1 = loadMask(avatarItemDefinition2.fileName1);
			avatarItems[avatarItemDefinition2.type] = avatarData2;
		}
		array = itemLoader;
		foreach (AvatarItemDefinition avatarItemDefinition3 in array)
		{
			AvatarData avatarData3 = new AvatarData();
			avatarData3.type = avatarItemDefinition3.type;
			if (avatarItemDefinition3.fileName1 != null && avatarItemDefinition3.fileName1.Length > 0)
			{
				avatarData3.dataMask1 = loadMask(avatarItemDefinition3.fileName1);
				avatarData3.iconTexture = loadIcon(avatarItemDefinition3.fileName1.Replace("Emblem_", "UI_"));
			}
			else if (avatarItemDefinition3.fileName4.Length > 0)
			{
				avatarData3.iconTexture = loadIcon(avatarItemDefinition3.fileName4);
			}
			if (avatarItemDefinition3.fileName2 != null && avatarItemDefinition3.fileName2.Length > 0)
			{
				avatarData3.dataMask2 = loadMask(avatarItemDefinition3.fileName2);
			}
			if (avatarItemDefinition3.fileName3 != null && avatarItemDefinition3.fileName3.Length > 0)
			{
				avatarData3.dataOverlay = loadOverlay(avatarItemDefinition3.fileName3);
			}
			avatarItems[avatarItemDefinition3.type] = avatarData3;
		}
		HUD_CoatOfArms.UpdateChargesAndOrdinaries();
	}

	private byte[] loadSolid(string fileName)
	{
		byte[] array = new byte[202800];
		UnityEngine.Color[] array2 = loadImage(fileName);
		if (array2 == null)
		{
			return null;
		}
		int num = 0;
		UnityEngine.Color[] array3 = array2;
		for (int i = 0; i < array3.Length; i++)
		{
			UnityEngine.Color color = array3[i];
			array[num] = (byte)(color.r * 255f);
			array[num + 1] = (byte)(color.g * 255f);
			array[num + 2] = (byte)(color.b * 255f);
			num += 3;
		}
		return array;
	}

	private byte[] loadMask(string fileName)
	{
		byte[] array = new byte[67600];
		UnityEngine.Color[] array2 = loadImage(fileName);
		if (array2 == null)
		{
			return null;
		}
		int num = 0;
		UnityEngine.Color[] array3 = array2;
		for (int i = 0; i < array3.Length; i++)
		{
			UnityEngine.Color color = array3[i];
			array[num] = (byte)(color.r * 255f);
			num++;
		}
		return array;
	}

	private byte[] loadOverlay(string fileName)
	{
		byte[] array = new byte[270400];
		UnityEngine.Color[] array2 = loadImage(fileName);
		if (array2 == null)
		{
			return null;
		}
		int num = 0;
		UnityEngine.Color[] array3 = array2;
		for (int i = 0; i < array3.Length; i++)
		{
			UnityEngine.Color color = array3[i];
			array[num] = (byte)(color.r * 255f);
			array[num + 1] = (byte)(color.g * 255f);
			array[num + 2] = (byte)(color.b * 255f);
			array[num + 3] = (byte)(color.a * 255f);
			num += 4;
		}
		return array;
	}

	private ImageSource loadIcon(string fileName)
	{
		if (fileName.Length > 0)
		{
			try
			{
				byte[] data = File.ReadAllBytes("Assets/GUI/Avatars/" + fileName + ".png");
				Texture2D texture2D = new Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, mipChain: false, linear: true);
				texture2D.LoadImage(data);
				TextureSource result = new TextureSource(texture2D);
				UnityEngine.Object.DestroyImmediate(texture2D);
				return result;
			}
			catch (Exception)
			{
			}
		}
		return null;
	}

	private UnityEngine.Color[] loadImage(string fileName)
	{
		try
		{
			byte[] data = File.ReadAllBytes("Assets/GUI/Avatars/" + fileName + ".png");
			Texture2D texture2D = new Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, mipChain: false, linear: true);
			texture2D.LoadImage(data);
			UnityEngine.Color[] pixels = texture2D.GetPixels();
			UnityEngine.Object.DestroyImmediate(texture2D);
			return pixels;
		}
		catch (Exception)
		{
		}
		return null;
	}

	private AvatarData getAvatarData(Enums.AvatarItems item)
	{
		if (avatarItems.TryGetValue(item, out var value))
		{
			return value;
		}
		return null;
	}

	public TextureSource GetAvatarTexture(AvatarDesign design, bool featherEdge = true, bool UIListVariant = false, bool saveAvatar = false)
	{
		if (UIListVariant)
		{
			return (TextureSource)getAvatarData(design.item).iconTexture;
		}
		UnityEngine.Color[] array = new UnityEngine.Color[67600];
		Texture2D tempTex = new Texture2D(260, 260, UnityEngine.TextureFormat.RGBA32, mipChain: false, linear: true);
		AvatarData avatarData = getAvatarData(design.background_colour1);
		if (avatarData != null && avatarData.dataSolid != null)
		{
			if (design.background == Enums.AvatarItems.BackMask_091)
			{
				UnityEngine.Color color = new UnityEngine.Color(1f, 1f, 1f, 0f);
				for (int i = 0; i < 67600; i++)
				{
					array[i] = color;
				}
			}
			else
			{
				for (int j = 0; j < 67600; j++)
				{
					UnityEngine.Color color2 = new UnityEngine.Color((float)(int)avatarData.dataSolid[j * 3] / 255f, (float)(int)avatarData.dataSolid[j * 3 + 1] / 255f, (float)(int)avatarData.dataSolid[j * 3 + 2] / 255f, 1f);
					array[j] = color2;
				}
			}
			if (design.item == Enums.AvatarItems.None && design.background == Enums.AvatarItems.BackMask_091)
			{
				UnityEngine.Color color3 = new UnityEngine.Color(0f, 0f, 0f, 1f);
				for (uint num = 0u; num < 260; num++)
				{
					for (uint num2 = 0u; num2 < 260; num2++)
					{
						array[num2 + num * 260].a = 1f;
						array[num2 + num * 260].r = color3.r;
						array[num2 + num * 260].g = color3.g;
						array[num2 + num * 260].b = color3.b;
					}
				}
			}
			AvatarData avatarData2 = getAvatarData(design.background);
			AvatarData avatarData3 = getAvatarData(design.background_colour2);
			if (avatarData2 != null && avatarData2.dataMask1 != null && avatarData3 != null && avatarData3.dataSolid != null && design.background != Enums.AvatarItems.BackMask_091)
			{
				for (int k = 0; k < 67600; k++)
				{
					float num3 = (float)(int)avatarData2.dataMask1[k] / 255f;
					if (num3 != 0f)
					{
						if (num3 == 1f)
						{
							array[k] = new UnityEngine.Color((float)(int)avatarData3.dataSolid[k * 3] / 255f, (float)(int)avatarData3.dataSolid[k * 3 + 1] / 255f, (float)(int)avatarData3.dataSolid[k * 3 + 2] / 255f);
							continue;
						}
						array[k].r *= 1f - num3;
						array[k].g *= 1f - num3;
						array[k].b *= 1f - num3;
						array[k].r += (float)(int)avatarData3.dataSolid[k * 3] / 255f * num3;
						array[k].g += (float)(int)avatarData3.dataSolid[k * 3 + 1] / 255f * num3;
						array[k].b += (float)(int)avatarData3.dataSolid[k * 3 + 2] / 255f * num3;
					}
				}
			}
		}
		else
		{
			UnityEngine.Color color4 = new UnityEngine.Color(0f, 0f, 0f, 0.5f);
			for (int l = 0; l < 67600; l++)
			{
				array[l] = color4;
			}
		}
		AvatarData avatarData4 = getAvatarData(design.item);
		AvatarData avatarData5 = getAvatarData(design.item_colour1);
		AvatarData avatarData6 = getAvatarData(design.item_colour2);
		if (avatarData4 != null && avatarData4.dataOverlay != null)
		{
			if (avatarData4.dataMask1 != null && avatarData5 != null && avatarData5.dataSolid != null)
			{
				for (int m = 0; m < 67600; m++)
				{
					float num4 = (float)(int)avatarData4.dataMask1[m] / 255f;
					if (num4 != 0f)
					{
						if (num4 == 1f)
						{
							array[m] = new UnityEngine.Color((float)(int)avatarData5.dataSolid[m * 3] / 255f, (float)(int)avatarData5.dataSolid[m * 3 + 1] / 255f, (float)(int)avatarData5.dataSolid[m * 3 + 2] / 255f);
						}
						else if (design.background == Enums.AvatarItems.BackMask_091)
						{
							array[m].r = (float)(int)avatarData5.dataSolid[m * 3] / 255f;
							array[m].g = (float)(int)avatarData5.dataSolid[m * 3 + 1] / 255f;
							array[m].b = (float)(int)avatarData5.dataSolid[m * 3 + 2] / 255f;
							array[m].a = num4;
						}
						else
						{
							array[m].r *= 1f - num4;
							array[m].g *= 1f - num4;
							array[m].b *= 1f - num4;
							array[m].r += (float)(int)avatarData5.dataSolid[m * 3] / 255f * num4;
							array[m].g += (float)(int)avatarData5.dataSolid[m * 3 + 1] / 255f * num4;
							array[m].b += (float)(int)avatarData5.dataSolid[m * 3 + 2] / 255f * num4;
						}
					}
				}
			}
			if (avatarData4.dataMask2 != null && avatarData6 != null && avatarData6.dataSolid != null)
			{
				for (int n = 0; n < 67600; n++)
				{
					float num5 = (float)(int)avatarData4.dataMask2[n] / 255f;
					if (num5 == 0f)
					{
						continue;
					}
					if (num5 == 1f)
					{
						array[n] = new UnityEngine.Color((float)(int)avatarData6.dataSolid[n * 3] / 255f, (float)(int)avatarData6.dataSolid[n * 3 + 1] / 255f, (float)(int)avatarData6.dataSolid[n * 3 + 2] / 255f);
						continue;
					}
					array[n].r *= 1f - num5;
					array[n].g *= 1f - num5;
					array[n].b *= 1f - num5;
					array[n].r += (float)(int)avatarData6.dataSolid[n * 3] / 255f * num5;
					array[n].g += (float)(int)avatarData6.dataSolid[n * 3 + 1] / 255f * num5;
					array[n].b += (float)(int)avatarData6.dataSolid[n * 3 + 2] / 255f * num5;
					if (num5 > array[n].a)
					{
						array[n].a = num5;
					}
				}
			}
			for (int num6 = 0; num6 < 67600; num6++)
			{
				float num7 = (float)(int)avatarData4.dataOverlay[num6 * 4 + 3] / 255f;
				if (num7 == 0f)
				{
					continue;
				}
				if (num7 == 1f)
				{
					array[num6] = new UnityEngine.Color((float)(int)avatarData4.dataOverlay[num6 * 4] / 255f, (float)(int)avatarData4.dataOverlay[num6 * 4 + 1] / 255f, (float)(int)avatarData4.dataOverlay[num6 * 4 + 2] / 255f);
					continue;
				}
				array[num6].r *= 1f - num7;
				array[num6].g *= 1f - num7;
				array[num6].b *= 1f - num7;
				array[num6].r += (float)(int)avatarData4.dataOverlay[num6 * 4] / 255f * num7;
				array[num6].g += (float)(int)avatarData4.dataOverlay[num6 * 4 + 1] / 255f * num7;
				array[num6].b += (float)(int)avatarData4.dataOverlay[num6 * 4 + 2] / 255f * num7;
				if (num7 > array[num6].a)
				{
					array[num6].a = num7;
				}
			}
		}
		if (design.background == Enums.AvatarItems.BackMask_091)
		{
			for (int num8 = 0; num8 < 67600; num8++)
			{
				float a = array[num8].a;
				array[num8].r *= a;
				array[num8].g *= a;
				array[num8].b *= a;
			}
		}
		else if (featherEdge)
		{
			for (uint num9 = 0u; num9 < 260; num9++)
			{
				for (uint num10 = 0u; num10 < 260; num10++)
				{
					uint num11 = num10;
					if (num11 > 130)
					{
						num11 = 260 - num11 - 1;
					}
					uint num12 = num9;
					if (num12 > 130)
					{
						num12 = 260 - num12 - 1;
					}
					if (num11 < 30 || num12 < 30)
					{
						num11 = ((num11 >= 15) ? (num11 - 15) : 0u);
						num12 = ((num12 >= 15) ? (num12 - 15) : 0u);
						float num13 = (float)Math.Min(num11, num12) / 15f;
						array[num10 + num9 * 260].a = num13;
						array[num10 + num9 * 260].r *= num13;
						array[num10 + num9 * 260].g *= num13;
						array[num10 + num9 * 260].b *= num13;
					}
				}
			}
		}
		tempTex.SetPixels(array);
		tempTex.Apply();
		if (saveAvatar)
		{
			string path = Application.persistentDataPath + "\\Coat Of Arms.png";
			if (File.Exists(path))
			{
				HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 30), delegate
				{
					OutputCoAPNG(path, tempTex);
				}, delegate
				{
					UnityEngine.Object.DestroyImmediate(tempTex);
				});
			}
			else
			{
				OutputCoAPNG(path, tempTex);
			}
			return null;
		}
		TextureSource result = new TextureSource(tempTex);
		UnityEngine.Object.DestroyImmediate(tempTex);
		return result;
	}

	private void OutputCoAPNG(string path, Texture2D tempTex)
	{
		byte[] bytes = tempTex.EncodeToPNG();
		File.WriteAllBytes(path, bytes);
		try
		{
			string persistentDataPath = Application.persistentDataPath;
			Application.OpenURL("file://" + persistentDataPath);
		}
		catch (Exception)
		{
		}
		UnityEngine.Object.DestroyImmediate(tempTex);
	}

	public void CreateLocalUserAvatar()
	{
		Platform_Multiplayer.Instance.SetCoatOfArms(Instance.GetAvatarTexture(ConfigSettings.getAvatar()));
	}
}
