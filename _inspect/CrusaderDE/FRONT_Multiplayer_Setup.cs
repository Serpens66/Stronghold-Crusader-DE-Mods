using System;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_Multiplayer_Setup : UserControl
{
	public Slider RefSetupMaxPlayersSlider;

	public Slider RefMP_Settings_Peacetime_Slider;

	public Slider RefMP_Settings_GameSpeed_Slider;

	public RadioButton RefFairness1;

	public RadioButton RefFairness2;

	public RadioButton RefFairness3;

	public RadioButton RefFairness4;

	public RadioButton RefFairness5;

	public RadioButton RefGameType1;

	public RadioButton RefGameType2;

	public RadioButton RefGameType3;

	public Button RefMP_UsePrevious;

	public Button RefMP_UseDefault;

	public Button RefMP_UsePresets1;

	public Button RefMP_UsePresets2;

	public Button RefMP_SavePresets1;

	public Button RefMP_SavePresets2;

	public static FRONT_Multiplayer_Setup instance1;

	public static FRONT_Multiplayer_Setup instance2;

	public static FRONT_Multiplayer_Setup Instance
	{
		get
		{
			if (((UIElement)instance1).IsVisible)
			{
				return instance1;
			}
			return instance2;
		}
	}

	public FRONT_Multiplayer_Setup()
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Expected O, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Expected O, but got Unknown
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Expected O, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected O, but got Unknown
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Expected O, but got Unknown
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Expected O, but got Unknown
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Expected O, but got Unknown
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Expected O, but got Unknown
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Expected O, but got Unknown
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Expected O, but got Unknown
		if ((BaseComponent)(object)instance1 == (BaseComponent)null)
		{
			instance1 = this;
		}
		else if ((BaseComponent)(object)instance2 == (BaseComponent)null)
		{
			instance2 = this;
		}
		InitializeComponent();
		RefFairness1 = (RadioButton)((FrameworkElement)this).FindName("Fairness1");
		RefFairness2 = (RadioButton)((FrameworkElement)this).FindName("Fairness2");
		RefFairness3 = (RadioButton)((FrameworkElement)this).FindName("Fairness3");
		RefFairness4 = (RadioButton)((FrameworkElement)this).FindName("Fairness4");
		RefFairness5 = (RadioButton)((FrameworkElement)this).FindName("Fairness5");
		RefGameType1 = (RadioButton)((FrameworkElement)this).FindName("GameType1");
		RefGameType2 = (RadioButton)((FrameworkElement)this).FindName("GameType2");
		RefGameType3 = (RadioButton)((FrameworkElement)this).FindName("GameType3");
		RefMP_UsePrevious = (Button)((FrameworkElement)this).FindName("MP_UsePrevious");
		RefMP_UseDefault = (Button)((FrameworkElement)this).FindName("MP_UseDefault");
		RefMP_UsePresets1 = (Button)((FrameworkElement)this).FindName("MP_UsePresets1");
		RefMP_UsePresets2 = (Button)((FrameworkElement)this).FindName("MP_UsePresets2");
		RefMP_SavePresets1 = (Button)((FrameworkElement)this).FindName("MP_SavePresets1");
		RefMP_SavePresets2 = (Button)((FrameworkElement)this).FindName("MP_SavePresets2");
		try
		{
			RefSetupMaxPlayersSlider = (Slider)((FrameworkElement)this).FindName("SetupMaxPlayersSlider");
			((RangeBase)RefSetupMaxPlayersSlider).ValueChanged += MainViewModel.Instance.FRONTMultiplayer.SetupMaxPlayersSlider_ValueChanged;
			RefMP_Settings_Peacetime_Slider = (Slider)((FrameworkElement)this).FindName("MP_Settings_Peacetime_Slider");
			((RangeBase)RefMP_Settings_Peacetime_Slider).ValueChanged += MainViewModel.Instance.FRONTMultiplayer.MP_Settings_Peacetime_Slider_ValueChanged;
			RefMP_Settings_GameSpeed_Slider = (Slider)((FrameworkElement)this).FindName("MP_Settings_GameSpeed_Slider");
			((RangeBase)RefMP_Settings_GameSpeed_Slider).ValueChanged += MainViewModel.Instance.FRONTMultiplayer.MP_Settings_GameSpeed_Slider_ValueChanged;
		}
		catch (Exception)
		{
			Debug.Log((object)"MP Setup exception!");
		}
		if (FatControler.german)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePrevious, 13);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets1, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets2, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePresets1, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePresets2, 14);
		}
		if (FatControler.ukrainian)
		{
			PropEx.SetTextCentre((UIElement)(object)RefMP_UsePrevious, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 205));
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets1, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets2, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePresets1, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePresets2, 14);
		}
		if (FatControler.french)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePrevious, 13);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets1, 13);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets2, 13);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UseDefault, 13);
		}
		if (FatControler.swedish)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePrevious, 13);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets1, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets2, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UseDefault, 14);
		}
		if (FatControler.dutch)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePrevious, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UseDefault, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets1, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_SavePresets2, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePresets1, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePresets2, 14);
		}
		if (FatControler.arabic)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePrevious, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UseDefault, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePresets1, 14);
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMP_UsePresets2, 14);
		}
	}

	public static void ResetMaxPlayers()
	{
		if ((BaseComponent)(object)instance1 != (BaseComponent)null)
		{
			((RangeBase)instance1.RefSetupMaxPlayersSlider).Value = 8f;
		}
		if ((BaseComponent)(object)instance2 != (BaseComponent)null)
		{
			((RangeBase)instance2.RefSetupMaxPlayersSlider).Value = 8f;
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_Multiplayer_Setup.xaml");
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
}
