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
			if (instance1.IsVisible)
			{
				return instance1;
			}
			return instance2;
		}
	}

	public FRONT_Multiplayer_Setup()
	{
		if (instance1 == null)
		{
			instance1 = this;
		}
		else if (instance2 == null)
		{
			instance2 = this;
		}
		InitializeComponent();
		RefFairness1 = (RadioButton)FindName("Fairness1");
		RefFairness2 = (RadioButton)FindName("Fairness2");
		RefFairness3 = (RadioButton)FindName("Fairness3");
		RefFairness4 = (RadioButton)FindName("Fairness4");
		RefFairness5 = (RadioButton)FindName("Fairness5");
		RefGameType1 = (RadioButton)FindName("GameType1");
		RefGameType2 = (RadioButton)FindName("GameType2");
		RefGameType3 = (RadioButton)FindName("GameType3");
		RefMP_UsePrevious = (Button)FindName("MP_UsePrevious");
		RefMP_UseDefault = (Button)FindName("MP_UseDefault");
		RefMP_UsePresets1 = (Button)FindName("MP_UsePresets1");
		RefMP_UsePresets2 = (Button)FindName("MP_UsePresets2");
		RefMP_SavePresets1 = (Button)FindName("MP_SavePresets1");
		RefMP_SavePresets2 = (Button)FindName("MP_SavePresets2");
		try
		{
			RefSetupMaxPlayersSlider = (Slider)FindName("SetupMaxPlayersSlider");
			RefSetupMaxPlayersSlider.ValueChanged += MainViewModel.Instance.FRONTMultiplayer.SetupMaxPlayersSlider_ValueChanged;
			RefMP_Settings_Peacetime_Slider = (Slider)FindName("MP_Settings_Peacetime_Slider");
			RefMP_Settings_Peacetime_Slider.ValueChanged += MainViewModel.Instance.FRONTMultiplayer.MP_Settings_Peacetime_Slider_ValueChanged;
			RefMP_Settings_GameSpeed_Slider = (Slider)FindName("MP_Settings_GameSpeed_Slider");
			RefMP_Settings_GameSpeed_Slider.ValueChanged += MainViewModel.Instance.FRONTMultiplayer.MP_Settings_GameSpeed_Slider_ValueChanged;
		}
		catch (Exception)
		{
			Debug.Log("MP Setup exception!");
		}
		if (FatControler.german)
		{
			PropEx.SetGlowButtonFontSize(RefMP_UsePrevious, 13);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets1, 14);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets2, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UsePresets1, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UsePresets2, 14);
		}
		if (FatControler.ukrainian)
		{
			PropEx.SetTextCentre(RefMP_UsePrevious, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 205));
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets1, 14);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets2, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UsePresets1, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UsePresets2, 14);
		}
		if (FatControler.french)
		{
			PropEx.SetGlowButtonFontSize(RefMP_UsePrevious, 13);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets1, 13);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets2, 13);
			PropEx.SetGlowButtonFontSize(RefMP_UseDefault, 13);
		}
		if (FatControler.swedish)
		{
			PropEx.SetGlowButtonFontSize(RefMP_UsePrevious, 13);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets1, 14);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets2, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UseDefault, 14);
		}
		if (FatControler.dutch)
		{
			PropEx.SetGlowButtonFontSize(RefMP_UsePrevious, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UseDefault, 14);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets1, 14);
			PropEx.SetGlowButtonFontSize(RefMP_SavePresets2, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UsePresets1, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UsePresets2, 14);
		}
		if (FatControler.arabic)
		{
			PropEx.SetGlowButtonFontSize(RefMP_UsePrevious, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UseDefault, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UsePresets1, 14);
			PropEx.SetGlowButtonFontSize(RefMP_UsePresets2, 14);
		}
	}

	public static void ResetMaxPlayers()
	{
		if (instance1 != null)
		{
			instance1.RefSetupMaxPlayersSlider.Value = 8f;
		}
		if (instance2 != null)
		{
			instance2.RefSetupMaxPlayersSlider.Value = 8f;
		}
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_Multiplayer_Setup.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			return true;
		}
		return false;
	}
}
