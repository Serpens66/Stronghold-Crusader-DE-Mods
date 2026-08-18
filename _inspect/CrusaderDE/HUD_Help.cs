using System;
using System.IO;
using Noesis;
using Steamworks;
using UnityEngine;
using Vuplex.WebView;

namespace CrusaderDE;

public class HUD_Help : UserControl
{
	public static int from = 0;

	public static HUD_Help instance1 = null;

	public static HUD_Help instance2 = null;

	public Image RefMainHelpTexture;

	public Button RefButtonBack;

	public Button RefButtonHome;

	public IWebView webView;

	public bool webBrowserOpen;

	public bool webBrowserLoaded;

	public static bool browserThumbHeld = false;

	public static bool mouseIsUpStroke = false;

	public static bool mouseIsDownStroke = false;

	public static bool wasPaused = false;

	public string openingPageURL = "";

	public bool canGoBackValue;

	public static bool[] buildingHelpChecked = new bool[120];

	public static bool[] buildingHelpExists = new bool[120];

	public static string[] in_building_help = new string[109]
	{
		null, "st02_house.html", "st02_house.html", "st03_woodcutters_hut.html", "st04_oxen_base.html", "st05_iron_mine.html", "st06_pitch_digger.html", "st07_hunters_hut.html", "st08_mercenary_post.html", "st08_barracks.html",
		"st10_goods_yard.html", "st11_armoury.html", "st12_fletchers_workshop.html", "st13_blacksmiths_workshop.html", "st14_poleturners_workshop.html", "st15_armourers_workshop.html", "st16_tanners_workshop.html", "st17_bakers_workshop.html", "st18_brewers_workshop.html", "st19_granary.html",
		"st20_quarry.html", "st21_quarrypile.html", "st22_inn.html", "st23_healer.html", "st24_engineers_guild.html", "st25_tunnellers_guild.html", "st26_tradepost.html", "st27_well.html", "st28_oil_smelter.html", null,
		"st30_wheatfarm.html", "st31_hopsfarm.html", "st32_applefarm.html", "st33_cattlefarm.html", "st34_mill.html", "st35_stables.html", "st36_church.html", "st36_church.html", "st36_church.html", null,
		"st40_keep.html", "st40_keep.html", "st40_keep.html", "st40_keep.html", "st40_keep.html", "st60_gatehouse.html", "st60_gatehouse.html", "st60_gatehouse.html", "st60_gatehouse.html", "st49_drawbridge.html",
		"st50_tunnel_entrance.html", "st28_oil_smelter.html", "st52_signpost.html", "st24_engineers_guild.html", "st80_siege_tent.html", "st55_campground.html", "st09_barracks.html", "st09_barracks.html", "st09_barracks.html", "st25_tunnellers_guild.html",
		"st60_gatehouse.html", "st61_tower.html", "st62_bad_things.html", "st62_bad_things.html", "st62_bad_things.html", "st65_good_things.html", "st65_good_things.html", "st67_killing_pit.html", "st68_pitch_ditch.html", null,
		"st70_water_pot.html", "st71_keepdoor_left.html", "st72_keepdoor_right.html", "st73_keepdoor.html", "st74_tower1.html", "st75_tower2.html", "st76_tower3.html", "st77_tower4.html", "st78_tower5.html", null,
		"st80_siege_tent.html", "st80_siege_tent.html", "st80_siege_tent.html", "st80_siege_tent.html", "st80_siege_tent.html", "st50_tunnel_entrance.html", null, null, null, null,
		null, "st62_bad_things.html", "st62_bad_things.html", "st62_bad_things.html", "st62_bad_things.html", "st62_bad_things.html", "st62_bad_things.html", "st62_bad_things.html", "st62_bad_things.html", "st99_dog_cage.html",
		"st65_good_things.html", "st65_good_things.html", "st65_good_things.html", "st65_good_things.html", "st65_good_things.html", null, null, null, "st08_bedouin_stockade.html"
	};

	public HUD_Help()
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Expected O, but got Unknown
		InitializeComponent();
		if ((BaseComponent)(object)instance1 == (BaseComponent)null)
		{
			instance1 = this;
		}
		else if ((BaseComponent)(object)instance2 == (BaseComponent)null)
		{
			instance2 = this;
		}
		RefMainHelpTexture = (Image)((FrameworkElement)this).FindName("MainHelpTexture");
		RefButtonBack = (Button)((FrameworkElement)this).FindName("ButtonBack");
		RefButtonHome = (Button)((FrameworkElement)this).FindName("ButtonHome");
	}

	public static void OpenHelp(bool fromMenu, string url = "")
	{
		if (ConfigSettings.Settings_UseSteamOverlayForHelp)
		{
			url = url.Replace('/', '\\');
			SteamFriends.ActivateGameOverlayToWebPage(url, (EActivateGameOverlayToWebPageMode)0);
			return;
		}
		MainViewModel.Instance.Show_HUD_Help = true;
		if (((UIElement)instance1).IsVisible)
		{
			MainViewModel.Instance.HUDHelp = instance1;
		}
		else if (((UIElement)instance2).IsVisible)
		{
			MainViewModel.Instance.HUDHelp = instance2;
		}
		if (fromMenu)
		{
			from = 0;
		}
		else
		{
			from = 1;
		}
		MainViewModel.Instance.HUDHelp.Init(url);
		if (fromMenu)
		{
			wasPaused = Director.instance.Paused;
			if (!wasPaused)
			{
				Director.instance.SetPausedState(state: true);
			}
		}
	}

	public async void Init(string url)
	{
		try
		{
			openingPageURL = url;
			((UIElement)RefButtonBack).Visibility = (Visibility)1;
			((UIElement)RefButtonHome).Visibility = (Visibility)1;
			webBrowserOpen = true;
			webView = Web.CreateWebView();
			int num = (int)(((FrameworkElement)RefMainHelpTexture).Width * 2f);
			int num2 = (int)(((FrameworkElement)RefMainHelpTexture).Height * 2f);
			await webView.Init(num, num2);
			webView.LoadUrl(url);
			mouseIsUpStroke = false;
			mouseIsDownStroke = false;
			webBrowserLoaded = true;
			browserThumbHeld = false;
		}
		catch (Exception ex)
		{
			Debug.Log((object)ex.Message);
		}
	}

	public void Update()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		if (!webBrowserLoaded)
		{
			return;
		}
		bool flag = FatControler.MouseIsDownStroke;
		bool flag2 = FatControler.MouseIsUpStroke;
		TextureSource mainHelpImage = new TextureSource(webView.Texture);
		MainViewModel.Instance.MainHelpImage = (ImageSource)(object)mainHelpImage;
		Point briefingHelpMousePoint = FatControler.instance.BriefingHelpMousePoint;
		if ((((Point)(ref briefingHelpMousePoint)).X >= 0f && ((Point)(ref briefingHelpMousePoint)).X < ((FrameworkElement)RefMainHelpTexture).Width && ((Point)(ref briefingHelpMousePoint)).Y >= 0f && ((Point)(ref briefingHelpMousePoint)).Y < ((FrameworkElement)RefMainHelpTexture).Height) || browserThumbHeld)
		{
			Vector2 val = default(Vector2);
			((Vector2)(ref val))._002Ector(((Point)(ref briefingHelpMousePoint)).X / ((FrameworkElement)RefMainHelpTexture).Width, 1f - ((Point)(ref briefingHelpMousePoint)).Y / ((FrameworkElement)RefMainHelpTexture).Height);
			if (val.x < 0f)
			{
				val.x = 0f;
			}
			if (val.y < 0f)
			{
				val.y = 0f;
			}
			if (val.x > 1f)
			{
				val.x = 1f;
			}
			if (val.y > 1f)
			{
				val.y = 1f;
			}
			IWebView obj = webView;
			IWithPointerDownAndUp val2 = (IWithPointerDownAndUp)(object)((obj is IWithPointerDownAndUp) ? obj : null);
			if (val2 != null && !webView.IsDisposed && webView.IsInitialized)
			{
				if (flag)
				{
					browserThumbHeld = true;
					val2.PointerDown(val);
				}
				if (flag2)
				{
					browserThumbHeld = false;
					val2.PointerUp(val);
				}
				IWebView obj2 = webView;
				((IWithMovablePointer)((obj2 is IWithMovablePointer) ? obj2 : null)).MovePointer(val, false);
			}
		}
		mouseIsUpStroke = false;
		mouseIsDownStroke = false;
		if (canGoBack())
		{
			Button refButtonHome = RefButtonHome;
			Button refButtonBack = RefButtonBack;
			Visibility visibility = (Visibility)2;
			((UIElement)refButtonBack).Visibility = (Visibility)2;
			((UIElement)refButtonHome).Visibility = visibility;
		}
		else
		{
			Button refButtonHome2 = RefButtonHome;
			Button refButtonBack2 = RefButtonBack;
			Visibility visibility = (Visibility)1;
			((UIElement)refButtonBack2).Visibility = (Visibility)1;
			((UIElement)refButtonHome2).Visibility = visibility;
		}
	}

	public void MouseWheelScrolled(float delta)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		Point briefingHelpMousePoint = FatControler.instance.BriefingHelpMousePoint;
		if (((Point)(ref briefingHelpMousePoint)).X >= 0f && ((Point)(ref briefingHelpMousePoint)).X < ((FrameworkElement)RefMainHelpTexture).Width && ((Point)(ref briefingHelpMousePoint)).Y >= 0f && ((Point)(ref briefingHelpMousePoint)).Y < ((FrameworkElement)RefMainHelpTexture).Height)
		{
			if (delta > 0f)
			{
				webView.Scroll(0, -60);
			}
			else
			{
				webView.Scroll(0, 60);
			}
		}
	}

	public bool canGoBack()
	{
		canGoBackInternal();
		return canGoBackValue;
	}

	public async void canGoBackInternal()
	{
		if (webBrowserLoaded && webView != null && !webView.IsDisposed && webView.IsInitialized)
		{
			canGoBackValue = await webView.CanGoBack();
		}
		else
		{
			canGoBackValue = false;
		}
	}

	public void goBack()
	{
		if (webBrowserLoaded && webView != null && !webView.IsDisposed && webView.IsInitialized)
		{
			webView.GoBack();
		}
	}

	public void goHome()
	{
		if (webBrowserLoaded && webView != null && !webView.IsDisposed && webView.IsInitialized)
		{
			webView.LoadUrl(openingPageURL);
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Help.xaml");
	}

	public void Close()
	{
		try
		{
			if (webBrowserOpen)
			{
				webView.LoadUrl("about:blank");
				webView.Dispose();
				webView = null;
				webBrowserOpen = false;
				webBrowserLoaded = false;
			}
		}
		catch (Exception)
		{
		}
		MainViewModel.Instance.MainHelpImage = null;
		mouseIsUpStroke = false;
		mouseIsDownStroke = false;
		browserThumbHeld = false;
		MainViewModel.Instance.Show_HUD_Help = false;
		if (from == 0)
		{
			if (!wasPaused)
			{
				Director.instance.SetPausedState(state: false);
			}
			MainViewModel.Instance.HUDmain.InGameOptions(null, null);
		}
	}

	public static void OpenHelpForCurrentBuildingOrChimmp()
	{
		if (GameData.Instance.lastGameState.app_mode == 16 && doesBuildingHelpExist(GameData.Instance.lastGameState.in_structure_type))
		{
			OpenHelp(fromMenu: false, getBuildingHelpURL(GameData.Instance.lastGameState.in_structure_type));
		}
	}

	public static string getBuildingHelpURL(int buildingType)
	{
		if (doesBuildingHelpExist(buildingType))
		{
			if ((buildingType == 36 || buildingType == 37 || buildingType == 38) && GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				return "file://" + Application.dataPath + "/StreamingAssets/Help/st100_mosque.html";
			}
			return "file://" + Application.dataPath + "/StreamingAssets/Help/" + in_building_help[buildingType];
		}
		return null;
	}

	public static bool doesBuildingHelpExist(int buildingType)
	{
		if (buildingType < 0 || buildingType > 120)
		{
			return false;
		}
		if (buildingHelpChecked[buildingType])
		{
			return buildingHelpExists[buildingType];
		}
		buildingHelpChecked[buildingType] = true;
		if (in_building_help[buildingType] == null)
		{
			buildingHelpExists[buildingType] = false;
		}
		string path = Application.dataPath + "/StreamingAssets/Help/" + in_building_help[buildingType];
		buildingHelpExists[buildingType] = File.Exists(path);
		return buildingHelpExists[buildingType];
	}
}
