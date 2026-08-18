using System;
using Noesis;

namespace CrusaderDE;

public class HUD_CoatOfArms : UserControl
{
	public static bool OrdinaryTabSelected = true;

	public static bool MainColourSelected = true;

	public static bool ChargeColourLockedSelected = true;

	public static readonly DependencyProperty COA_UseSteamAvatarProperty = DependencyProperty.Register("COA_UseSteamAvatar", typeof(bool), typeof(HUD_CoatOfArms), new PropertyMetadata((PropertyChangedCallback)null));

	public static HUD_CoatOfArms instance1 = null;

	public static HUD_CoatOfArms instance2 = null;

	public static HUD_CoatOfArms instance3 = null;

	public static HUD_CoatOfArms instance4 = null;

	public static HUD_CoatOfArms instance5 = null;

	public static Enums.AvatarItems[] OrdinariesDisplayOrder = new Enums.AvatarItems[91]
	{
		Enums.AvatarItems.BackMask_001,
		Enums.AvatarItems.BackMask_011,
		Enums.AvatarItems.BackMask_007,
		Enums.AvatarItems.BackMask_009,
		Enums.AvatarItems.BackMask_008,
		Enums.AvatarItems.BackMask_010,
		Enums.AvatarItems.BackMask_014,
		Enums.AvatarItems.BackMask_002,
		Enums.AvatarItems.BackMask_003,
		Enums.AvatarItems.BackMask_004,
		Enums.AvatarItems.BackMask_012,
		Enums.AvatarItems.BackMask_013,
		Enums.AvatarItems.BackMask_015,
		Enums.AvatarItems.BackMask_053,
		Enums.AvatarItems.BackMask_054,
		Enums.AvatarItems.BackMask_029,
		Enums.AvatarItems.BackMask_023,
		Enums.AvatarItems.BackMask_024,
		Enums.AvatarItems.BackMask_030,
		Enums.AvatarItems.BackMask_031,
		Enums.AvatarItems.BackMask_018,
		Enums.AvatarItems.BackMask_034,
		Enums.AvatarItems.BackMask_021,
		Enums.AvatarItems.BackMask_074,
		Enums.AvatarItems.BackMask_076,
		Enums.AvatarItems.BackMask_075,
		Enums.AvatarItems.BackMask_086,
		Enums.AvatarItems.BackMask_084,
		Enums.AvatarItems.BackMask_066,
		Enums.AvatarItems.BackMask_082,
		Enums.AvatarItems.BackMask_055,
		Enums.AvatarItems.BackMask_056,
		Enums.AvatarItems.BackMask_025,
		Enums.AvatarItems.BackMask_045,
		Enums.AvatarItems.BackMask_046,
		Enums.AvatarItems.BackMask_032,
		Enums.AvatarItems.BackMask_037,
		Enums.AvatarItems.BackMask_038,
		Enums.AvatarItems.BackMask_048,
		Enums.AvatarItems.BackMask_049,
		Enums.AvatarItems.BackMask_050,
		Enums.AvatarItems.BackMask_047,
		Enums.AvatarItems.BackMask_051,
		Enums.AvatarItems.BackMask_039,
		Enums.AvatarItems.BackMask_040,
		Enums.AvatarItems.BackMask_081,
		Enums.AvatarItems.BackMask_078,
		Enums.AvatarItems.BackMask_052,
		Enums.AvatarItems.BackMask_016,
		Enums.AvatarItems.BackMask_017,
		Enums.AvatarItems.BackMask_005,
		Enums.AvatarItems.BackMask_006,
		Enums.AvatarItems.BackMask_058,
		Enums.AvatarItems.BackMask_089,
		Enums.AvatarItems.BackMask_060,
		Enums.AvatarItems.BackMask_061,
		Enums.AvatarItems.BackMask_062,
		Enums.AvatarItems.BackMask_063,
		Enums.AvatarItems.BackMask_059,
		Enums.AvatarItems.BackMask_077,
		Enums.AvatarItems.BackMask_083,
		Enums.AvatarItems.BackMask_088,
		Enums.AvatarItems.BackMask_026,
		Enums.AvatarItems.BackMask_090,
		Enums.AvatarItems.BackMask_067,
		Enums.AvatarItems.BackMask_072,
		Enums.AvatarItems.BackMask_057,
		Enums.AvatarItems.BackMask_065,
		Enums.AvatarItems.BackMask_064,
		Enums.AvatarItems.BackMask_085,
		Enums.AvatarItems.BackMask_070,
		Enums.AvatarItems.BackMask_069,
		Enums.AvatarItems.BackMask_068,
		Enums.AvatarItems.BackMask_087,
		Enums.AvatarItems.BackMask_036,
		Enums.AvatarItems.BackMask_035,
		Enums.AvatarItems.BackMask_022,
		Enums.AvatarItems.BackMask_080,
		Enums.AvatarItems.BackMask_027,
		Enums.AvatarItems.BackMask_028,
		Enums.AvatarItems.BackMask_079,
		Enums.AvatarItems.BackMask_073,
		Enums.AvatarItems.BackMask_033,
		Enums.AvatarItems.BackMask_071,
		Enums.AvatarItems.BackMask_044,
		Enums.AvatarItems.BackMask_043,
		Enums.AvatarItems.BackMask_042,
		Enums.AvatarItems.BackMask_041,
		Enums.AvatarItems.BackMask_020,
		Enums.AvatarItems.BackMask_019,
		Enums.AvatarItems.BackMask_091
	};

	public static Enums.AvatarItems[] ChargesDisplayOrder = new Enums.AvatarItems[86]
	{
		Enums.AvatarItems.Item_BLANK,
		Enums.AvatarItems.Item_RAT,
		Enums.AvatarItems.Item_SNAKE,
		Enums.AvatarItems.Item_PIG,
		Enums.AvatarItems.Item_WOLF,
		Enums.AvatarItems.Item_SALADIN,
		Enums.AvatarItems.Item_CALIPH,
		Enums.AvatarItems.Item_SULTAN,
		Enums.AvatarItems.Item_RICHARD,
		Enums.AvatarItems.Item_FREDERICK,
		Enums.AvatarItems.Item_PHILIP,
		Enums.AvatarItems.Item_WAZIR,
		Enums.AvatarItems.Item_EMIR,
		Enums.AvatarItems.Item_NIZAR,
		Enums.AvatarItems.Item_SHERIFF,
		Enums.AvatarItems.Item_MARSHAL,
		Enums.AvatarItems.Item_ABBOT,
		Enums.AvatarItems.Item_JEWEL,
		Enums.AvatarItems.Item_SENTINEL,
		Enums.AvatarItems.Item_NOMAD,
		Enums.AvatarItems.Item_KAHINAH,
		Enums.AvatarItems.Item_CROCODILE,
		Enums.AvatarItems.Item_CANARY,
		Enums.AvatarItems.Item_TRADER,
		Enums.AvatarItems.Item_LIONESS,
		Enums.AvatarItems.Item_SERGEANT,
		Enums.AvatarItems.Item_BALDWIN,
		Enums.AvatarItems.Item_BULLSEYE,
		Enums.AvatarItems.Item_LAMB,
		Enums.AvatarItems.Item_GOAT,
		Enums.AvatarItems.Item_FALCON,
		Enums.AvatarItems.Item_LEOPARD,
		Enums.AvatarItems.Item_JACKAL,
		Enums.AvatarItems.Item_COBRA,
		Enums.AvatarItems.Item_FISH,
		Enums.AvatarItems.Item_CROWN,
		Enums.AvatarItems.Item_ROSE,
		Enums.AvatarItems.Item_FLOWERS,
		Enums.AvatarItems.Item_CAT,
		Enums.AvatarItems.Item_PAW,
		Enums.AvatarItems.Item_SUN,
		Enums.AvatarItems.Item_ANKH,
		Enums.AvatarItems.Item_CROSSA,
		Enums.AvatarItems.Item_CROSSB,
		Enums.AvatarItems.Item_CROSSC,
		Enums.AvatarItems.Item_CROSSD,
		Enums.AvatarItems.Item_CIRCLE,
		Enums.AvatarItems.Item_CIRCLEB,
		Enums.AvatarItems.Item_CIRCLEC,
		Enums.AvatarItems.Item_COTL,
		Enums.AvatarItems.Item_GUNGEON,
		Enums.AvatarItems.Item_INSCRYPTION,
		Enums.AvatarItems.Item_KZERO,
		Enums.AvatarItems.Item_REIGNS,
		Enums.AvatarItems.Item_TALOS,
		Enums.AvatarItems.Item_VOLVY,
		Enums.AvatarItems.Item_NEVA,
		Enums.AvatarItems.Item_KTC,
		Enums.AvatarItems.Item_NORTH,
		Enums.AvatarItems.Item_INK,
		Enums.AvatarItems.Item_STYX,
		Enums.AvatarItems.Item_GOING,
		Enums.AvatarItems.Item_THRONE,
		Enums.AvatarItems.Item_EM,
		Enums.AvatarItems.Item_EF,
		Enums.AvatarItems.Item_AF,
		Enums.AvatarItems.Item_AM,
		Enums.AvatarItems.Item_BM,
		Enums.AvatarItems.Item_BF,
		Enums.AvatarItems.Item_SCRIBE,
		Enums.AvatarItems.Item_BESSY,
		Enums.AvatarItems.Item_SSTRIPE,
		Enums.AvatarItems.Item_SARROW,
		Enums.AvatarItems.Item_SCHECKER,
		Enums.AvatarItems.Item_SCROSS,
		Enums.AvatarItems.Item_SDRAGON,
		Enums.AvatarItems.Item_SFDL,
		Enums.AvatarItems.Item_SLION,
		Enums.AvatarItems.Item_SMOON,
		Enums.AvatarItems.Item_SPHX,
		Enums.AvatarItems.Item_SSUN,
		Enums.AvatarItems.Item_SUNICORN,
		Enums.AvatarItems.Item_SWORDS,
		Enums.AvatarItems.Item_SKULL,
		Enums.AvatarItems.Item_SKULL2,
		Enums.AvatarItems.Item_SABERS
	};

	public static string[] chargeRolloverIDs = new string[66]
	{
		"", "", "", "", "", "", "Cult of the Lamb", "", "", "Going Medieval",
		"Enter the Gungeon", "Inkulinati", "Inscryption", "Katana ZERO", "Neva", "Northgard", "", "Reigns", "", "Songs of Syx",
		"The Talos Principle 2", "Thronefall", "Volvy - Devolver Digital", "Kingdom Two Crowns", "", "", "", "", "", "",
		"", "", "", "", "", "", "", "", "", "",
		"", "", "", "", "", "", "", "", "", "",
		"", "", "", "", "", "", "", "", "", "",
		"", "", "", "", "", ""
	};

	public static Enums.AvatarItems[] ColourDisplayOrder = new Enums.AvatarItems[25]
	{
		Enums.AvatarItems.Colour_LIGHTPINK,
		Enums.AvatarItems.Colour_LIGHTYELLOW,
		Enums.AvatarItems.Colour_GOLD,
		Enums.AvatarItems.Colour_LIGHTGREEN,
		Enums.AvatarItems.Colour_WHITE,
		Enums.AvatarItems.Colour_LIGHTPURPLE,
		Enums.AvatarItems.Colour_FLESH,
		Enums.AvatarItems.Colour_LIGHTORANGE,
		Enums.AvatarItems.Colour_GREEN,
		Enums.AvatarItems.Colour_GREY,
		Enums.AvatarItems.Colour_PURPLE,
		Enums.AvatarItems.Colour_PINK,
		Enums.AvatarItems.Colour_ORANGE,
		Enums.AvatarItems.Colour_DARKGREEN,
		Enums.AvatarItems.Colour_LIGHTBLUE,
		Enums.AvatarItems.Colour_DARKPURPLE,
		Enums.AvatarItems.Colour_RED,
		Enums.AvatarItems.Colour_BROWN,
		Enums.AvatarItems.Colour_TURQUOISE,
		Enums.AvatarItems.Colour_BLUE,
		Enums.AvatarItems.Colour_BLACK,
		Enums.AvatarItems.Colour_DARKRED,
		Enums.AvatarItems.Colour_DARKBROWN,
		Enums.AvatarItems.Colour_TEAL,
		Enums.AvatarItems.Colour_DARKBLUE
	};

	public static SolidColorBrush[] ColourSwabs = (SolidColorBrush[])(object)new SolidColorBrush[25]
	{
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)19, (byte)19, (byte)19)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)31, (byte)62, (byte)171)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)109, (byte)54, (byte)5)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)22, (byte)22, (byte)91)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)51, (byte)28, (byte)11)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)100, (byte)0)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)43, (byte)20, (byte)60)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)75, (byte)9, (byte)9)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)192, (byte)97, (byte)97)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)231, (byte)180, (byte)72)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)100, (byte)141, (byte)13)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)93, (byte)93, (byte)93)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)72, (byte)150, (byte)215)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)143, (byte)195, (byte)72)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, (byte)141, (byte)0)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)212, (byte)155, (byte)209)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)114, (byte)89, (byte)164)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)239, (byte)189, (byte)73)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)178, (byte)87, (byte)24)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)229, (byte)74, (byte)160)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)130, (byte)22, (byte)130)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)195, (byte)43, (byte)43)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)96, (byte)96)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)37, (byte)177, (byte)140)),
		new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)181, (byte)181, (byte)181))
	};

	public static bool imagesCreate = false;

	public bool COA_UseSteamAvatar
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(COA_UseSteamAvatarProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(COA_UseSteamAvatarProperty, (object)value);
		}
	}

	public HUD_CoatOfArms()
	{
		InitializeComponent();
		if ((BaseComponent)(object)instance1 == (BaseComponent)null)
		{
			instance1 = this;
		}
		else if ((BaseComponent)(object)instance2 == (BaseComponent)null)
		{
			instance2 = this;
		}
		else if ((BaseComponent)(object)instance3 == (BaseComponent)null)
		{
			instance3 = this;
		}
		else if ((BaseComponent)(object)instance4 == (BaseComponent)null)
		{
			instance4 = this;
		}
		else if ((BaseComponent)(object)instance5 == (BaseComponent)null)
		{
			instance5 = this;
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_CoatOfArms.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			return true;
		}
		return false;
	}

	public static void Init(Avatars.AvatarDesign ad, bool intro = false)
	{
		MainViewModel.Instance.CoA_Main_Image = (ImageSource)(object)Avatars.Instance.GetAvatarTexture(ad, featherEdge: false);
		MainViewModel.Instance.CoA_OptionsBorder = !intro;
		MainViewModel.Instance.CoA_IntroBorder = intro;
		UpdateChargesAndOrdinaries();
		OrdinaryTabSelected = true;
		MainColourSelected = true;
		if (ad.item_colour1 == ad.item_colour2 - 100)
		{
			ChargeColourLockedSelected = true;
		}
		else
		{
			ChargeColourLockedSelected = false;
		}
		MainViewModel.Instance.Show_CoA_BorderMain = true;
		MainViewModel.Instance.Show_CoA_BorderAlt = false;
		if ((BaseComponent)(object)instance1 != (BaseComponent)null)
		{
			instance1.COA_UseSteamAvatar = ConfigSettings.Settings_UseSteamAvatar;
		}
		if ((BaseComponent)(object)instance2 != (BaseComponent)null)
		{
			instance2.COA_UseSteamAvatar = ConfigSettings.Settings_UseSteamAvatar;
		}
		if ((BaseComponent)(object)instance3 != (BaseComponent)null)
		{
			instance3.COA_UseSteamAvatar = ConfigSettings.Settings_UseSteamAvatar;
		}
		if ((BaseComponent)(object)instance4 != (BaseComponent)null)
		{
			instance4.COA_UseSteamAvatar = ConfigSettings.Settings_UseSteamAvatar;
		}
		if ((BaseComponent)(object)instance5 != (BaseComponent)null)
		{
			instance5.COA_UseSteamAvatar = ConfigSettings.Settings_UseSteamAvatar;
		}
		SetColourSwabs();
		SetTabs();
		SetLock();
		InitBackground();
		GC.Collect();
	}

	public static void InitBackground()
	{
		MainViewModel.Instance.CoA_Main_Image_background = MainViewModel.Instance.getAIFaceBackground(ConfigSettings.Settings_PlayerColour + 1, setupRemap: true);
	}

	public static void SaveIfChanged()
	{
		bool flag = false;
		if ((BaseComponent)(object)instance1 != (BaseComponent)null && !flag && instance1.COA_UseSteamAvatar != ConfigSettings.Settings_UseSteamAvatar)
		{
			ConfigSettings.Settings_UseSteamAvatar = instance1.COA_UseSteamAvatar;
			flag = true;
		}
		if ((BaseComponent)(object)instance2 != (BaseComponent)null && !flag && instance2.COA_UseSteamAvatar != ConfigSettings.Settings_UseSteamAvatar)
		{
			ConfigSettings.Settings_UseSteamAvatar = instance2.COA_UseSteamAvatar;
			flag = true;
		}
		if ((BaseComponent)(object)instance3 != (BaseComponent)null && !flag && instance3.COA_UseSteamAvatar != ConfigSettings.Settings_UseSteamAvatar)
		{
			ConfigSettings.Settings_UseSteamAvatar = instance3.COA_UseSteamAvatar;
			flag = true;
		}
		if ((BaseComponent)(object)instance4 != (BaseComponent)null && !flag && instance4.COA_UseSteamAvatar != ConfigSettings.Settings_UseSteamAvatar)
		{
			ConfigSettings.Settings_UseSteamAvatar = instance4.COA_UseSteamAvatar;
			flag = true;
		}
		if ((BaseComponent)(object)instance5 != (BaseComponent)null && !flag && instance5.COA_UseSteamAvatar != ConfigSettings.Settings_UseSteamAvatar)
		{
			ConfigSettings.Settings_UseSteamAvatar = instance5.COA_UseSteamAvatar;
			flag = true;
		}
		if (flag)
		{
			ConfigSettings.SaveSettings();
		}
		Avatars.Instance.CreateLocalUserAvatar();
	}

	public static void UpdateChargesAndOrdinaries()
	{
		if (!imagesCreate)
		{
			imagesCreate = true;
			Avatars.AvatarDesign avatarDesign = new Avatars.AvatarDesign();
			avatarDesign.background = Enums.AvatarItems.None;
			avatarDesign.background_colour1 = Enums.AvatarItems.None;
			avatarDesign.background_colour2 = Enums.AvatarItems.None;
			int num = 0;
			Enums.AvatarItems[] chargesDisplayOrder = ChargesDisplayOrder;
			foreach (Enums.AvatarItems item in chargesDisplayOrder)
			{
				avatarDesign.item = item;
				setCoACharge(num, (ImageSource)(object)Avatars.Instance.GetAvatarTexture(avatarDesign, featherEdge: false, UIListVariant: true));
				num++;
			}
			avatarDesign.item = Enums.AvatarItems.None;
			avatarDesign.background_colour1 = Enums.AvatarItems.Colour_WHITE;
			avatarDesign.background_colour2 = Enums.AvatarItems.Colour_BLACK;
			num = 0;
			chargesDisplayOrder = OrdinariesDisplayOrder;
			foreach (Enums.AvatarItems background in chargesDisplayOrder)
			{
				avatarDesign.background = background;
				setCoAOrdinary(num, (ImageSource)(object)Avatars.Instance.GetAvatarTexture(avatarDesign, featherEdge: false));
				num++;
			}
		}
	}

	public static void SetTabs()
	{
		if (OrdinaryTabSelected)
		{
			MainViewModel.Instance.BtnOrdinaryColour = MainViewModel.AvatarButtonColour_Selected;
			MainViewModel.Instance.BtnChargeColour = MainViewModel.AvatarButtonColour_NotSelected;
		}
		else
		{
			MainViewModel.Instance.BtnOrdinaryColour = MainViewModel.AvatarButtonColour_NotSelected;
			MainViewModel.Instance.BtnChargeColour = MainViewModel.AvatarButtonColour_Selected;
		}
		MainViewModel.Instance.Show_CoA_Charges = !OrdinaryTabSelected;
		MainViewModel.Instance.Show_CoA_Ordinaries = OrdinaryTabSelected;
	}

	public static void SetLock()
	{
		if (ChargeColourLockedSelected)
		{
			MainViewModel.Instance.CoALock = MainViewModel.Instance.GameSprites[720];
			MainViewModel.Instance.CoALock_Over = MainViewModel.Instance.GameSprites[721];
		}
		else
		{
			MainViewModel.Instance.CoALock = MainViewModel.Instance.GameSprites[722];
			MainViewModel.Instance.CoALock_Over = MainViewModel.Instance.GameSprites[723];
		}
	}

	public static void ButtonClicked(string function)
	{
		switch (function)
		{
		case "Charge0":
		case "Charge1":
		case "Charge2":
		case "Charge3":
		case "Charge4":
		case "Charge5":
		case "Charge6":
		case "Charge7":
		case "Charge8":
		case "Charge9":
		case "Charge10":
		case "Charge11":
		case "Charge12":
		case "Charge13":
		case "Charge14":
		case "Charge15":
		case "Charge16":
		case "Charge17":
		case "Charge18":
		case "Charge19":
		case "Charge20":
		case "Charge21":
		case "Charge22":
		case "Charge23":
		case "Charge24":
		case "Charge25":
		case "Charge26":
		case "Charge27":
		case "Charge28":
		case "Charge29":
		case "Charge30":
		case "Charge31":
		case "Charge32":
		case "Charge33":
		case "Charge34":
		case "Charge35":
		case "Charge36":
		case "Charge37":
		case "Charge38":
		case "Charge39":
		case "Charge40":
		case "Charge41":
		case "Charge42":
		case "Charge43":
		case "Charge44":
		case "Charge45":
		case "Charge46":
		case "Charge47":
		case "Charge48":
		case "Charge49":
		case "Charge50":
		case "Charge51":
		case "Charge52":
		case "Charge53":
		case "Charge54":
		case "Charge55":
		case "Charge56":
		case "Charge57":
		case "Charge58":
		case "Charge59":
		case "Charge60":
		case "Charge61":
		case "Charge62":
		case "Charge63":
		case "Charge64":
		case "Charge65":
		case "Charge66":
		case "Charge67":
		case "Charge68":
		case "Charge69":
		case "Charge70":
		case "Charge71":
		case "Charge72":
		case "Charge73":
		case "Charge74":
		case "Charge75":
		case "Charge76":
		case "Charge77":
		case "Charge78":
		case "Charge79":
		case "Charge80":
		case "Charge81":
		case "Charge82":
		case "Charge83":
		case "Charge84":
		{
			int num4 = int.Parse(function.Replace("Charge", ""));
			if (num4 < ChargesDisplayOrder.Length)
			{
				num4 = (int)ChargesDisplayOrder[num4];
				if (num4 != ConfigSettings.Settings_AvatarCharge)
				{
					ConfigSettings.Settings_AvatarCharge = num4;
					MainViewModel.Instance.CoA_Main_Image = (ImageSource)(object)Avatars.Instance.GetAvatarTexture(ConfigSettings.getAvatar(), featherEdge: false);
					ConfigSettings.SaveSettings();
				}
			}
			break;
		}
		case "Charge0_Enter":
		case "Charge1_Enter":
		case "Charge2_Enter":
		case "Charge3_Enter":
		case "Charge4_Enter":
		case "Charge5_Enter":
		case "Charge6_Enter":
		case "Charge7_Enter":
		case "Charge8_Enter":
		case "Charge9_Enter":
		case "Charge10_Enter":
		case "Charge11_Enter":
		case "Charge12_Enter":
		case "Charge13_Enter":
		case "Charge14_Enter":
		case "Charge15_Enter":
		case "Charge16_Enter":
		case "Charge17_Enter":
		case "Charge18_Enter":
		case "Charge19_Enter":
		case "Charge20_Enter":
		case "Charge21_Enter":
		case "Charge22_Enter":
		case "Charge23_Enter":
		case "Charge24_Enter":
		case "Charge25_Enter":
		case "Charge26_Enter":
		case "Charge27_Enter":
		case "Charge28_Enter":
		case "Charge29_Enter":
		case "Charge30_Enter":
		case "Charge31_Enter":
		case "Charge32_Enter":
		case "Charge33_Enter":
		case "Charge34_Enter":
		case "Charge35_Enter":
		case "Charge36_Enter":
		case "Charge37_Enter":
		case "Charge38_Enter":
		case "Charge39_Enter":
		case "Charge40_Enter":
		case "Charge41_Enter":
		case "Charge42_Enter":
		case "Charge43_Enter":
		case "Charge44_Enter":
		case "Charge45_Enter":
		case "Charge46_Enter":
		case "Charge47_Enter":
		case "Charge48_Enter":
		case "Charge49_Enter":
		case "Charge50_Enter":
		case "Charge51_Enter":
		case "Charge52_Enter":
		case "Charge53_Enter":
		case "Charge54_Enter":
		case "Charge55_Enter":
		case "Charge56_Enter":
		case "Charge57_Enter":
		case "Charge58_Enter":
		case "Charge59_Enter":
		case "Charge60_Enter":
		case "Charge61_Enter":
		case "Charge62_Enter":
		case "Charge63_Enter":
		case "Charge64_Enter":
		case "Charge65_Enter":
		case "Charge66_Enter":
		case "Charge67_Enter":
		case "Charge68_Enter":
		case "Charge69_Enter":
		case "Charge70_Enter":
		case "Charge71_Enter":
		case "Charge72_Enter":
		case "Charge73_Enter":
		case "Charge74_Enter":
		case "Charge75_Enter":
		case "Charge76_Enter":
		case "Charge77_Enter":
		case "Charge78_Enter":
		case "Charge79_Enter":
		case "Charge80_Enter":
		case "Charge81_Enter":
		case "Charge82_Enter":
		case "Charge83_Enter":
		case "Charge84_Enter":
		{
			int num3 = int.Parse(function.Replace("Charge", "").Replace("_Enter", ""));
			if (num3 < ChargesDisplayOrder.Length)
			{
				num3 = (int)ChargesDisplayOrder[num3];
				if (num3 - 2001 < chargeRolloverIDs.Length)
				{
					MainViewModel.Instance.CoARollover = chargeRolloverIDs[num3 - 2001];
				}
				else
				{
					MainViewModel.Instance.CoARollover = "";
				}
			}
			break;
		}
		case "leave":
			MainViewModel.Instance.CoARollover = "";
			break;
		case "Charges":
			OrdinaryTabSelected = false;
			SetTabs();
			SetColourSwabs();
			break;
		case "Ordinaries":
			OrdinaryTabSelected = true;
			SetTabs();
			SetColourSwabs();
			break;
		case "Ordinary0":
		case "Ordinary1":
		case "Ordinary2":
		case "Ordinary3":
		case "Ordinary4":
		case "Ordinary5":
		case "Ordinary6":
		case "Ordinary7":
		case "Ordinary8":
		case "Ordinary9":
		case "Ordinary10":
		case "Ordinary11":
		case "Ordinary12":
		case "Ordinary13":
		case "Ordinary14":
		case "Ordinary15":
		case "Ordinary16":
		case "Ordinary17":
		case "Ordinary18":
		case "Ordinary19":
		case "Ordinary20":
		case "Ordinary21":
		case "Ordinary22":
		case "Ordinary23":
		case "Ordinary24":
		case "Ordinary25":
		case "Ordinary26":
		case "Ordinary27":
		case "Ordinary28":
		case "Ordinary29":
		case "Ordinary30":
		case "Ordinary31":
		case "Ordinary32":
		case "Ordinary33":
		case "Ordinary34":
		case "Ordinary35":
		case "Ordinary36":
		case "Ordinary37":
		case "Ordinary38":
		case "Ordinary39":
		case "Ordinary40":
		case "Ordinary41":
		case "Ordinary42":
		case "Ordinary43":
		case "Ordinary44":
		case "Ordinary45":
		case "Ordinary46":
		case "Ordinary47":
		case "Ordinary48":
		case "Ordinary49":
		case "Ordinary50":
		case "Ordinary51":
		case "Ordinary52":
		case "Ordinary53":
		case "Ordinary54":
		case "Ordinary55":
		case "Ordinary56":
		case "Ordinary57":
		case "Ordinary58":
		case "Ordinary59":
		case "Ordinary60":
		case "Ordinary61":
		case "Ordinary62":
		case "Ordinary63":
		case "Ordinary64":
		case "Ordinary65":
		case "Ordinary66":
		case "Ordinary67":
		case "Ordinary68":
		case "Ordinary69":
		case "Ordinary70":
		case "Ordinary71":
		case "Ordinary72":
		case "Ordinary73":
		case "Ordinary74":
		case "Ordinary75":
		case "Ordinary76":
		case "Ordinary77":
		case "Ordinary78":
		case "Ordinary79":
		case "Ordinary80":
		case "Ordinary81":
		case "Ordinary82":
		case "Ordinary83":
		case "Ordinary84":
		case "Ordinary85":
		case "Ordinary86":
		case "Ordinary87":
		case "Ordinary88":
		case "Ordinary89":
		case "Ordinary90":
		{
			int num2 = int.Parse(function.Replace("Ordinary", ""));
			if (num2 < OrdinariesDisplayOrder.Length)
			{
				num2 = (int)OrdinariesDisplayOrder[num2];
				if (num2 != ConfigSettings.Settings_AvatarOrdinary)
				{
					ConfigSettings.Settings_AvatarOrdinary = num2;
					MainViewModel.Instance.CoA_Main_Image = (ImageSource)(object)Avatars.Instance.GetAvatarTexture(ConfigSettings.getAvatar(), featherEdge: false);
					ConfigSettings.SaveSettings();
				}
			}
			break;
		}
		case "Colour0":
		case "Colour1":
		case "Colour2":
		case "Colour3":
		case "Colour4":
		case "Colour5":
		case "Colour6":
		case "Colour7":
		case "Colour8":
		case "Colour9":
		case "Colour10":
		case "Colour11":
		case "Colour12":
		case "Colour13":
		case "Colour14":
		case "Colour15":
		case "Colour16":
		case "Colour17":
		case "Colour18":
		case "Colour19":
		case "Colour20":
		case "Colour21":
		case "Colour22":
		case "Colour23":
		case "Colour24":
		{
			int num = int.Parse(function.Replace("Colour", ""));
			num = (int)ColourDisplayOrder[num];
			if (OrdinaryTabSelected)
			{
				if (MainColourSelected)
				{
					if (num != ConfigSettings.Settings_AvatarOrdinaryColour1)
					{
						ConfigSettings.Settings_AvatarOrdinaryColour1 = num;
						MainViewModel.Instance.CoA_Main_Image = (ImageSource)(object)Avatars.Instance.GetAvatarTexture(ConfigSettings.getAvatar(), featherEdge: false);
						ConfigSettings.SaveSettings();
					}
				}
				else if (num != ConfigSettings.Settings_AvatarOrdinaryColour2)
				{
					ConfigSettings.Settings_AvatarOrdinaryColour2 = num;
					MainViewModel.Instance.CoA_Main_Image = (ImageSource)(object)Avatars.Instance.GetAvatarTexture(ConfigSettings.getAvatar(), featherEdge: false);
					ConfigSettings.SaveSettings();
				}
			}
			else
			{
				if ((MainColourSelected || ChargeColourLockedSelected) && num != ConfigSettings.Settings_AvatarChargeColour1)
				{
					ConfigSettings.Settings_AvatarChargeColour1 = num;
					MainViewModel.Instance.CoA_Main_Image = (ImageSource)(object)Avatars.Instance.GetAvatarTexture(ConfigSettings.getAvatar(), featherEdge: false);
					ConfigSettings.SaveSettings();
				}
				if ((!MainColourSelected || ChargeColourLockedSelected) && num + 100 != ConfigSettings.Settings_AvatarChargeColour2)
				{
					ConfigSettings.Settings_AvatarChargeColour2 = num + 100;
					MainViewModel.Instance.CoA_Main_Image = (ImageSource)(object)Avatars.Instance.GetAvatarTexture(ConfigSettings.getAvatar(), featherEdge: false);
					ConfigSettings.SaveSettings();
				}
			}
			SetColourSwabs();
			break;
		}
		case "MainColour":
			MainColourSelected = true;
			MainViewModel.Instance.Show_CoA_BorderMain = true;
			MainViewModel.Instance.Show_CoA_BorderAlt = false;
			SetColourSwabs();
			break;
		case "AltColour":
			MainColourSelected = false;
			MainViewModel.Instance.Show_CoA_BorderMain = false;
			MainViewModel.Instance.Show_CoA_BorderAlt = true;
			SetColourSwabs();
			break;
		case "ToggleLock":
			ChargeColourLockedSelected = !ChargeColourLockedSelected;
			SetLock();
			break;
		case "Export":
			Avatars.Instance.GetAvatarTexture(ConfigSettings.getAvatar(), featherEdge: false, UIListVariant: false, saveAvatar: true);
			break;
		}
	}

	public static void SetColourSwabs()
	{
		for (int i = 0; i < 25; i++)
		{
			MainViewModel.Instance.CoA_Colour_Selected[i] = false;
		}
		Enums.AvatarItems[] colourDisplayOrder;
		if (OrdinaryTabSelected)
		{
			MainViewModel.Instance.CoAMainColour = ColourSwabs[ConfigSettings.Settings_AvatarOrdinaryColour1 - 1];
			MainViewModel.Instance.CoAAltColour = ColourSwabs[ConfigSettings.Settings_AvatarOrdinaryColour2 - 1];
			if (MainColourSelected)
			{
				int num = 0;
				colourDisplayOrder = ColourDisplayOrder;
				for (int j = 0; j < colourDisplayOrder.Length; j++)
				{
					if (colourDisplayOrder[j] == (Enums.AvatarItems)ConfigSettings.Settings_AvatarOrdinaryColour1)
					{
						MainViewModel.Instance.CoA_Colour_Selected[num] = true;
						break;
					}
					num++;
				}
				return;
			}
			int num2 = 0;
			colourDisplayOrder = ColourDisplayOrder;
			for (int j = 0; j < colourDisplayOrder.Length; j++)
			{
				if (colourDisplayOrder[j] == (Enums.AvatarItems)ConfigSettings.Settings_AvatarOrdinaryColour2)
				{
					MainViewModel.Instance.CoA_Colour_Selected[num2] = true;
					break;
				}
				num2++;
			}
			return;
		}
		MainViewModel.Instance.CoAMainColour = ColourSwabs[ConfigSettings.Settings_AvatarChargeColour1 - 1];
		MainViewModel.Instance.CoAAltColour = ColourSwabs[ConfigSettings.Settings_AvatarChargeColour2 - 101];
		if (MainColourSelected)
		{
			int num3 = 0;
			colourDisplayOrder = ColourDisplayOrder;
			for (int j = 0; j < colourDisplayOrder.Length; j++)
			{
				if (colourDisplayOrder[j] == (Enums.AvatarItems)ConfigSettings.Settings_AvatarChargeColour1)
				{
					MainViewModel.Instance.CoA_Colour_Selected[num3] = true;
					break;
				}
				num3++;
			}
			return;
		}
		int num4 = 0;
		colourDisplayOrder = ColourDisplayOrder;
		for (int j = 0; j < colourDisplayOrder.Length; j++)
		{
			if (colourDisplayOrder[j] == (Enums.AvatarItems)(ConfigSettings.Settings_AvatarChargeColour2 - 100))
			{
				MainViewModel.Instance.CoA_Colour_Selected[num4] = true;
				break;
			}
			num4++;
		}
	}

	public static void setCoACharge(int index, ImageSource tex)
	{
		switch (index)
		{
		case 0:
			MainViewModel.Instance.CoACharges0 = tex;
			break;
		case 1:
			MainViewModel.Instance.CoACharges1 = tex;
			break;
		case 2:
			MainViewModel.Instance.CoACharges2 = tex;
			break;
		case 3:
			MainViewModel.Instance.CoACharges3 = tex;
			break;
		case 4:
			MainViewModel.Instance.CoACharges4 = tex;
			break;
		case 5:
			MainViewModel.Instance.CoACharges5 = tex;
			break;
		case 6:
			MainViewModel.Instance.CoACharges6 = tex;
			break;
		case 7:
			MainViewModel.Instance.CoACharges7 = tex;
			break;
		case 8:
			MainViewModel.Instance.CoACharges8 = tex;
			break;
		case 9:
			MainViewModel.Instance.CoACharges9 = tex;
			break;
		case 10:
			MainViewModel.Instance.CoACharges10 = tex;
			break;
		case 11:
			MainViewModel.Instance.CoACharges11 = tex;
			break;
		case 12:
			MainViewModel.Instance.CoACharges12 = tex;
			break;
		case 13:
			MainViewModel.Instance.CoACharges13 = tex;
			break;
		case 14:
			MainViewModel.Instance.CoACharges14 = tex;
			break;
		case 15:
			MainViewModel.Instance.CoACharges15 = tex;
			break;
		case 16:
			MainViewModel.Instance.CoACharges16 = tex;
			break;
		case 17:
			MainViewModel.Instance.CoACharges17 = tex;
			break;
		case 18:
			MainViewModel.Instance.CoACharges18 = tex;
			break;
		case 19:
			MainViewModel.Instance.CoACharges19 = tex;
			break;
		case 20:
			MainViewModel.Instance.CoACharges20 = tex;
			break;
		case 21:
			MainViewModel.Instance.CoACharges21 = tex;
			break;
		case 22:
			MainViewModel.Instance.CoACharges22 = tex;
			break;
		case 23:
			MainViewModel.Instance.CoACharges23 = tex;
			break;
		case 24:
			MainViewModel.Instance.CoACharges24 = tex;
			break;
		case 25:
			MainViewModel.Instance.CoACharges25 = tex;
			break;
		case 26:
			MainViewModel.Instance.CoACharges26 = tex;
			break;
		case 27:
			MainViewModel.Instance.CoACharges27 = tex;
			break;
		case 28:
			MainViewModel.Instance.CoACharges28 = tex;
			break;
		case 29:
			MainViewModel.Instance.CoACharges29 = tex;
			break;
		case 30:
			MainViewModel.Instance.CoACharges30 = tex;
			break;
		case 31:
			MainViewModel.Instance.CoACharges31 = tex;
			break;
		case 32:
			MainViewModel.Instance.CoACharges32 = tex;
			break;
		case 33:
			MainViewModel.Instance.CoACharges33 = tex;
			break;
		case 34:
			MainViewModel.Instance.CoACharges34 = tex;
			break;
		case 35:
			MainViewModel.Instance.CoACharges35 = tex;
			break;
		case 36:
			MainViewModel.Instance.CoACharges36 = tex;
			break;
		case 37:
			MainViewModel.Instance.CoACharges37 = tex;
			break;
		case 38:
			MainViewModel.Instance.CoACharges38 = tex;
			break;
		case 39:
			MainViewModel.Instance.CoACharges39 = tex;
			break;
		case 40:
			MainViewModel.Instance.CoACharges40 = tex;
			break;
		case 41:
			MainViewModel.Instance.CoACharges41 = tex;
			break;
		case 42:
			MainViewModel.Instance.CoACharges42 = tex;
			break;
		case 43:
			MainViewModel.Instance.CoACharges43 = tex;
			break;
		case 44:
			MainViewModel.Instance.CoACharges44 = tex;
			break;
		case 45:
			MainViewModel.Instance.CoACharges45 = tex;
			break;
		case 46:
			MainViewModel.Instance.CoACharges46 = tex;
			break;
		case 47:
			MainViewModel.Instance.CoACharges47 = tex;
			break;
		case 48:
			MainViewModel.Instance.CoACharges48 = tex;
			break;
		case 49:
			MainViewModel.Instance.CoACharges49 = tex;
			break;
		case 50:
			MainViewModel.Instance.CoACharges50 = tex;
			break;
		case 51:
			MainViewModel.Instance.CoACharges51 = tex;
			break;
		case 52:
			MainViewModel.Instance.CoACharges52 = tex;
			break;
		case 53:
			MainViewModel.Instance.CoACharges53 = tex;
			break;
		case 54:
			MainViewModel.Instance.CoACharges54 = tex;
			break;
		case 55:
			MainViewModel.Instance.CoACharges55 = tex;
			break;
		case 56:
			MainViewModel.Instance.CoACharges56 = tex;
			break;
		case 57:
			MainViewModel.Instance.CoACharges57 = tex;
			break;
		case 58:
			MainViewModel.Instance.CoACharges58 = tex;
			break;
		case 59:
			MainViewModel.Instance.CoACharges59 = tex;
			break;
		case 60:
			MainViewModel.Instance.CoACharges60 = tex;
			break;
		case 61:
			MainViewModel.Instance.CoACharges61 = tex;
			break;
		case 62:
			MainViewModel.Instance.CoACharges62 = tex;
			break;
		case 63:
			MainViewModel.Instance.CoACharges63 = tex;
			break;
		case 64:
			MainViewModel.Instance.CoACharges64 = tex;
			break;
		case 65:
			MainViewModel.Instance.CoACharges65 = tex;
			break;
		case 66:
			MainViewModel.Instance.CoACharges66 = tex;
			break;
		case 67:
			MainViewModel.Instance.CoACharges67 = tex;
			break;
		case 68:
			MainViewModel.Instance.CoACharges68 = tex;
			break;
		case 69:
			MainViewModel.Instance.CoACharges69 = tex;
			break;
		case 70:
			MainViewModel.Instance.CoACharges70 = tex;
			break;
		case 71:
			MainViewModel.Instance.CoACharges71 = tex;
			break;
		case 72:
			MainViewModel.Instance.CoACharges72 = tex;
			break;
		case 73:
			MainViewModel.Instance.CoACharges73 = tex;
			break;
		case 74:
			MainViewModel.Instance.CoACharges74 = tex;
			break;
		case 75:
			MainViewModel.Instance.CoACharges75 = tex;
			break;
		case 76:
			MainViewModel.Instance.CoACharges76 = tex;
			break;
		case 77:
			MainViewModel.Instance.CoACharges77 = tex;
			break;
		case 78:
			MainViewModel.Instance.CoACharges78 = tex;
			break;
		case 79:
			MainViewModel.Instance.CoACharges79 = tex;
			break;
		case 80:
			MainViewModel.Instance.CoACharges80 = tex;
			break;
		case 81:
			MainViewModel.Instance.CoACharges81 = tex;
			break;
		case 82:
			MainViewModel.Instance.CoACharges82 = tex;
			break;
		case 83:
			MainViewModel.Instance.CoACharges83 = tex;
			break;
		case 84:
			MainViewModel.Instance.CoACharges84 = tex;
			break;
		case 85:
			MainViewModel.Instance.CoACharges85 = tex;
			break;
		case 86:
			MainViewModel.Instance.CoACharges86 = tex;
			break;
		}
	}

	public static void setCoAOrdinary(int index, ImageSource tex)
	{
		switch (index)
		{
		case 0:
			MainViewModel.Instance.CoAOrdinaries0 = tex;
			break;
		case 1:
			MainViewModel.Instance.CoAOrdinaries1 = tex;
			break;
		case 2:
			MainViewModel.Instance.CoAOrdinaries2 = tex;
			break;
		case 3:
			MainViewModel.Instance.CoAOrdinaries3 = tex;
			break;
		case 4:
			MainViewModel.Instance.CoAOrdinaries4 = tex;
			break;
		case 5:
			MainViewModel.Instance.CoAOrdinaries5 = tex;
			break;
		case 6:
			MainViewModel.Instance.CoAOrdinaries6 = tex;
			break;
		case 7:
			MainViewModel.Instance.CoAOrdinaries7 = tex;
			break;
		case 8:
			MainViewModel.Instance.CoAOrdinaries8 = tex;
			break;
		case 9:
			MainViewModel.Instance.CoAOrdinaries9 = tex;
			break;
		case 10:
			MainViewModel.Instance.CoAOrdinaries10 = tex;
			break;
		case 11:
			MainViewModel.Instance.CoAOrdinaries11 = tex;
			break;
		case 12:
			MainViewModel.Instance.CoAOrdinaries12 = tex;
			break;
		case 13:
			MainViewModel.Instance.CoAOrdinaries13 = tex;
			break;
		case 14:
			MainViewModel.Instance.CoAOrdinaries14 = tex;
			break;
		case 15:
			MainViewModel.Instance.CoAOrdinaries15 = tex;
			break;
		case 16:
			MainViewModel.Instance.CoAOrdinaries16 = tex;
			break;
		case 17:
			MainViewModel.Instance.CoAOrdinaries17 = tex;
			break;
		case 18:
			MainViewModel.Instance.CoAOrdinaries18 = tex;
			break;
		case 19:
			MainViewModel.Instance.CoAOrdinaries19 = tex;
			break;
		case 20:
			MainViewModel.Instance.CoAOrdinaries20 = tex;
			break;
		case 21:
			MainViewModel.Instance.CoAOrdinaries21 = tex;
			break;
		case 22:
			MainViewModel.Instance.CoAOrdinaries22 = tex;
			break;
		case 23:
			MainViewModel.Instance.CoAOrdinaries23 = tex;
			break;
		case 24:
			MainViewModel.Instance.CoAOrdinaries24 = tex;
			break;
		case 25:
			MainViewModel.Instance.CoAOrdinaries25 = tex;
			break;
		case 26:
			MainViewModel.Instance.CoAOrdinaries26 = tex;
			break;
		case 27:
			MainViewModel.Instance.CoAOrdinaries27 = tex;
			break;
		case 28:
			MainViewModel.Instance.CoAOrdinaries28 = tex;
			break;
		case 29:
			MainViewModel.Instance.CoAOrdinaries29 = tex;
			break;
		case 30:
			MainViewModel.Instance.CoAOrdinaries30 = tex;
			break;
		case 31:
			MainViewModel.Instance.CoAOrdinaries31 = tex;
			break;
		case 32:
			MainViewModel.Instance.CoAOrdinaries32 = tex;
			break;
		case 33:
			MainViewModel.Instance.CoAOrdinaries33 = tex;
			break;
		case 34:
			MainViewModel.Instance.CoAOrdinaries34 = tex;
			break;
		case 35:
			MainViewModel.Instance.CoAOrdinaries35 = tex;
			break;
		case 36:
			MainViewModel.Instance.CoAOrdinaries36 = tex;
			break;
		case 37:
			MainViewModel.Instance.CoAOrdinaries37 = tex;
			break;
		case 38:
			MainViewModel.Instance.CoAOrdinaries38 = tex;
			break;
		case 39:
			MainViewModel.Instance.CoAOrdinaries39 = tex;
			break;
		case 40:
			MainViewModel.Instance.CoAOrdinaries40 = tex;
			break;
		case 41:
			MainViewModel.Instance.CoAOrdinaries41 = tex;
			break;
		case 42:
			MainViewModel.Instance.CoAOrdinaries42 = tex;
			break;
		case 43:
			MainViewModel.Instance.CoAOrdinaries43 = tex;
			break;
		case 44:
			MainViewModel.Instance.CoAOrdinaries44 = tex;
			break;
		case 45:
			MainViewModel.Instance.CoAOrdinaries45 = tex;
			break;
		case 46:
			MainViewModel.Instance.CoAOrdinaries46 = tex;
			break;
		case 47:
			MainViewModel.Instance.CoAOrdinaries47 = tex;
			break;
		case 48:
			MainViewModel.Instance.CoAOrdinaries48 = tex;
			break;
		case 49:
			MainViewModel.Instance.CoAOrdinaries49 = tex;
			break;
		case 50:
			MainViewModel.Instance.CoAOrdinaries50 = tex;
			break;
		case 51:
			MainViewModel.Instance.CoAOrdinaries51 = tex;
			break;
		case 52:
			MainViewModel.Instance.CoAOrdinaries52 = tex;
			break;
		case 53:
			MainViewModel.Instance.CoAOrdinaries53 = tex;
			break;
		case 54:
			MainViewModel.Instance.CoAOrdinaries54 = tex;
			break;
		case 55:
			MainViewModel.Instance.CoAOrdinaries55 = tex;
			break;
		case 56:
			MainViewModel.Instance.CoAOrdinaries56 = tex;
			break;
		case 57:
			MainViewModel.Instance.CoAOrdinaries57 = tex;
			break;
		case 58:
			MainViewModel.Instance.CoAOrdinaries58 = tex;
			break;
		case 59:
			MainViewModel.Instance.CoAOrdinaries59 = tex;
			break;
		case 60:
			MainViewModel.Instance.CoAOrdinaries60 = tex;
			break;
		case 61:
			MainViewModel.Instance.CoAOrdinaries61 = tex;
			break;
		case 62:
			MainViewModel.Instance.CoAOrdinaries62 = tex;
			break;
		case 63:
			MainViewModel.Instance.CoAOrdinaries63 = tex;
			break;
		case 64:
			MainViewModel.Instance.CoAOrdinaries64 = tex;
			break;
		case 65:
			MainViewModel.Instance.CoAOrdinaries65 = tex;
			break;
		case 66:
			MainViewModel.Instance.CoAOrdinaries66 = tex;
			break;
		case 67:
			MainViewModel.Instance.CoAOrdinaries67 = tex;
			break;
		case 68:
			MainViewModel.Instance.CoAOrdinaries68 = tex;
			break;
		case 69:
			MainViewModel.Instance.CoAOrdinaries69 = tex;
			break;
		case 70:
			MainViewModel.Instance.CoAOrdinaries70 = tex;
			break;
		case 71:
			MainViewModel.Instance.CoAOrdinaries71 = tex;
			break;
		case 72:
			MainViewModel.Instance.CoAOrdinaries72 = tex;
			break;
		case 73:
			MainViewModel.Instance.CoAOrdinaries73 = tex;
			break;
		case 74:
			MainViewModel.Instance.CoAOrdinaries74 = tex;
			break;
		case 75:
			MainViewModel.Instance.CoAOrdinaries75 = tex;
			break;
		case 76:
			MainViewModel.Instance.CoAOrdinaries76 = tex;
			break;
		case 77:
			MainViewModel.Instance.CoAOrdinaries77 = tex;
			break;
		case 78:
			MainViewModel.Instance.CoAOrdinaries78 = tex;
			break;
		case 79:
			MainViewModel.Instance.CoAOrdinaries79 = tex;
			break;
		case 80:
			MainViewModel.Instance.CoAOrdinaries80 = tex;
			break;
		case 81:
			MainViewModel.Instance.CoAOrdinaries81 = tex;
			break;
		case 82:
			MainViewModel.Instance.CoAOrdinaries82 = tex;
			break;
		case 83:
			MainViewModel.Instance.CoAOrdinaries83 = tex;
			break;
		case 84:
			MainViewModel.Instance.CoAOrdinaries84 = tex;
			break;
		case 85:
			MainViewModel.Instance.CoAOrdinaries85 = tex;
			break;
		case 86:
			MainViewModel.Instance.CoAOrdinaries86 = tex;
			break;
		case 87:
			MainViewModel.Instance.CoAOrdinaries87 = tex;
			break;
		case 88:
			MainViewModel.Instance.CoAOrdinaries88 = tex;
			break;
		case 89:
			MainViewModel.Instance.CoAOrdinaries89 = tex;
			break;
		case 90:
			MainViewModel.Instance.CoAOrdinaries90 = tex;
			break;
		}
	}
}
