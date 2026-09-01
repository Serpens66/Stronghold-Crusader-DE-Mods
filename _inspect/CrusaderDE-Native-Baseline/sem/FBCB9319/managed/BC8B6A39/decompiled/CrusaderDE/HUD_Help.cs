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

	private static HUD_Help instance1 = null;

	private static HUD_Help instance2 = null;

	public Image RefMainHelpTexture;

	public Button RefButtonBack;

	public Button RefButtonHome;

	private IWebView webView;

	private bool webBrowserOpen;

	public bool webBrowserLoaded;

	public static bool browserThumbHeld = false;

	public static bool mouseIsUpStroke = false;

	public static bool mouseIsDownStroke = false;

	private static bool wasPaused = false;

	private string openingPageURL = "";

	private bool canGoBackValue;

	private static bool[] buildingHelpChecked = new bool[120];

	private static bool[] buildingHelpExists = new bool[120];

	private static string[] in_building_help = new string[109]
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
		InitializeComponent();
		if (instance1 == null)
		{
			instance1 = this;
		}
		else if (instance2 == null)
		{
			instance2 = this;
		}
		RefMainHelpTexture = (Image)FindName("MainHelpTexture");
		RefButtonBack = (Button)FindName("ButtonBack");
		RefButtonHome = (Button)FindName("ButtonHome");
	}

	public static void OpenHelp(bool fromMenu, string url = "")
	{
		if (ConfigSettings.Settings_UseSteamOverlayForHelp)
		{
			url = url.Replace('/', '\\');
			SteamFriends.ActivateGameOverlayToWebPage(url);
			return;
		}
		MainViewModel.Instance.Show_HUD_Help = true;
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDHelp = instance1;
		}
		else if (instance2.IsVisible)
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

	private async void Init(string url)
	{
		try
		{
			openingPageURL = url;
			RefButtonBack.Visibility = Visibility.Hidden;
			RefButtonHome.Visibility = Visibility.Hidden;
			webBrowserOpen = true;
			webView = Web.CreateWebView();
			int width = (int)(RefMainHelpTexture.Width * 2f);
			int height = (int)(RefMainHelpTexture.Height * 2f);
			await webView.Init(width, height);
			webView.LoadUrl(url);
			mouseIsUpStroke = false;
			mouseIsDownStroke = false;
			webBrowserLoaded = true;
			browserThumbHeld = false;
		}
		catch (Exception ex)
		{
			Debug.Log(ex.Message);
		}
	}

	public void Update()
	{
		if (!webBrowserLoaded)
		{
			return;
		}
		bool flag = FatControler.MouseIsDownStroke;
		bool flag2 = FatControler.MouseIsUpStroke;
		TextureSource mainHelpImage = new TextureSource(webView.Texture);
		MainViewModel.Instance.MainHelpImage = mainHelpImage;
		Point briefingHelpMousePoint = FatControler.instance.BriefingHelpMousePoint;
		if ((briefingHelpMousePoint.X >= 0f && briefingHelpMousePoint.X < RefMainHelpTexture.Width && briefingHelpMousePoint.Y >= 0f && briefingHelpMousePoint.Y < RefMainHelpTexture.Height) || browserThumbHeld)
		{
			Vector2 normalizedPoint = new Vector2(briefingHelpMousePoint.X / RefMainHelpTexture.Width, 1f - briefingHelpMousePoint.Y / RefMainHelpTexture.Height);
			if (normalizedPoint.x < 0f)
			{
				normalizedPoint.x = 0f;
			}
			if (normalizedPoint.y < 0f)
			{
				normalizedPoint.y = 0f;
			}
			if (normalizedPoint.x > 1f)
			{
				normalizedPoint.x = 1f;
			}
			if (normalizedPoint.y > 1f)
			{
				normalizedPoint.y = 1f;
			}
			if (webView is IWithPointerDownAndUp withPointerDownAndUp && !webView.IsDisposed && webView.IsInitialized)
			{
				if (flag)
				{
					browserThumbHeld = true;
					withPointerDownAndUp.PointerDown(normalizedPoint);
				}
				if (flag2)
				{
					browserThumbHeld = false;
					withPointerDownAndUp.PointerUp(normalizedPoint);
				}
				(webView as IWithMovablePointer).MovePointer(normalizedPoint);
			}
		}
		mouseIsUpStroke = false;
		mouseIsDownStroke = false;
		if (canGoBack())
		{
			Button refButtonHome = RefButtonHome;
			Visibility visibility = (RefButtonBack.Visibility = Visibility.Visible);
			refButtonHome.Visibility = visibility;
		}
		else
		{
			Button refButtonHome2 = RefButtonHome;
			Visibility visibility = (RefButtonBack.Visibility = Visibility.Hidden);
			refButtonHome2.Visibility = visibility;
		}
	}

	public void MouseWheelScrolled(float delta)
	{
		Point briefingHelpMousePoint = FatControler.instance.BriefingHelpMousePoint;
		if (briefingHelpMousePoint.X >= 0f && briefingHelpMousePoint.X < RefMainHelpTexture.Width && briefingHelpMousePoint.Y >= 0f && briefingHelpMousePoint.Y < RefMainHelpTexture.Height)
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

	private async void canGoBackInternal()
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

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Help.xaml");
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
