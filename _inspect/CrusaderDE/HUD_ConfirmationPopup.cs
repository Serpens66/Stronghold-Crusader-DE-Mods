using System;
using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_ConfirmationPopup : UserControl
{
	public WGT_Heading RefHeading;

	public Grid RefYesNo;

	public Grid RefOK;

	public CheckBox RefConfirmCheck;

	public static List<HUD_ConfirmationPopup> instances = new List<HUD_ConfirmationPopup>();

	public static HUD_ConfirmationPopup instance1 = null;

	public static HUD_ConfirmationPopup instance2 = null;

	public static HUD_ConfirmationPopup instance3 = null;

	public static HUD_ConfirmationPopup instance4 = null;

	public static HUD_ConfirmationPopup instance5 = null;

	public static HUD_ConfirmationPopup instance6 = null;

	public static HUD_ConfirmationPopup instance7 = null;

	public static HUD_ConfirmationPopup instance8 = null;

	public static HUD_ConfirmationPopup instance9 = null;

	public static HUD_ConfirmationPopup instance10 = null;

	public static HUD_ConfirmationPopup instance11 = null;

	public static HUD_ConfirmationPopup instance12 = null;

	public static HUD_ConfirmationPopup instance13 = null;

	public static HUD_ConfirmationPopup instance14 = null;

	public static HUD_ConfirmationPopup instance15 = null;

	public static HUD_ConfirmationPopup instance16 = null;

	public static HUD_ConfirmationPopup instance17 = null;

	public static HUD_ConfirmationPopup instance18 = null;

	public static HUD_ConfirmationPopup instance19 = null;

	public static HUD_ConfirmationPopup instance20 = null;

	public static HUD_ConfirmationPopup instance21 = null;

	public static HUD_ConfirmationPopup instance22 = null;

	public static int ConfirmationWidth = 450;

	public static int ConfirmationHeight = 170;

	public static bool panelActive = true;

	public Action yesAction;

	public Action noAction;

	public Action<bool> checkAction;

	public HUD_ConfirmationPopup()
	{
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Expected O, but got Unknown
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Expected O, but got Unknown
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Expected O, but got Unknown
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Expected O, but got Unknown
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Expected O, but got Unknown
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
		else if ((BaseComponent)(object)instance6 == (BaseComponent)null)
		{
			instance6 = this;
		}
		else if ((BaseComponent)(object)instance7 == (BaseComponent)null)
		{
			instance7 = this;
		}
		else if ((BaseComponent)(object)instance8 == (BaseComponent)null)
		{
			instance8 = this;
		}
		else if ((BaseComponent)(object)instance9 == (BaseComponent)null)
		{
			instance9 = this;
		}
		else if ((BaseComponent)(object)instance10 == (BaseComponent)null)
		{
			instance10 = this;
		}
		else if ((BaseComponent)(object)instance11 == (BaseComponent)null)
		{
			instance11 = this;
		}
		else if ((BaseComponent)(object)instance12 == (BaseComponent)null)
		{
			instance12 = this;
		}
		else if ((BaseComponent)(object)instance13 == (BaseComponent)null)
		{
			instance13 = this;
		}
		else if ((BaseComponent)(object)instance14 == (BaseComponent)null)
		{
			instance14 = this;
		}
		else if ((BaseComponent)(object)instance15 == (BaseComponent)null)
		{
			instance15 = this;
		}
		else if ((BaseComponent)(object)instance16 == (BaseComponent)null)
		{
			instance16 = this;
		}
		else if ((BaseComponent)(object)instance17 == (BaseComponent)null)
		{
			instance17 = this;
		}
		else if ((BaseComponent)(object)instance18 == (BaseComponent)null)
		{
			instance18 = this;
		}
		else if ((BaseComponent)(object)instance19 == (BaseComponent)null)
		{
			instance19 = this;
		}
		else if ((BaseComponent)(object)instance20 == (BaseComponent)null)
		{
			instance20 = this;
		}
		else if ((BaseComponent)(object)instance21 == (BaseComponent)null)
		{
			instance21 = this;
		}
		else if ((BaseComponent)(object)instance22 == (BaseComponent)null)
		{
			instance22 = this;
		}
		instances.Add(this);
		RefHeading = (WGT_Heading)((FrameworkElement)this).FindName("ConfirmationHeader");
		RefYesNo = (Grid)((FrameworkElement)this).FindName("YesNo");
		RefOK = (Grid)((FrameworkElement)this).FindName("OK");
		RefConfirmCheck = (CheckBox)((FrameworkElement)this).FindName("ConfirmCheck");
		((ToggleButton)RefConfirmCheck).Checked += new RoutedEventHandler(Check_ValueChanged);
		((ToggleButton)RefConfirmCheck).Unchecked += new RoutedEventHandler(Check_ValueChanged);
		if (FatControler.hungarian)
		{
			RefHeading.RefHeadingTextBlock.FontSize = 30f;
		}
		if (FatControler.polish)
		{
			RefHeading.RefHeadingTextBlock.FontSize = 30f;
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_ConfirmationPopup.xaml");
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

	public void Check_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive && checkAction != null)
		{
			checkAction(((ToggleButton)RefConfirmCheck).IsChecked.Value);
		}
	}

	public static void SetInstance()
	{
		if (((UIElement)instance1).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance1;
		}
		else if (((UIElement)instance2).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance2;
		}
		else if (((UIElement)instance3).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance3;
		}
		else if (((UIElement)instance4).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance4;
		}
		else if (((UIElement)instance5).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance5;
		}
		else if (((UIElement)instance6).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance6;
		}
		else if (((UIElement)instance7).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance7;
		}
		else if (((UIElement)instance8).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance8;
		}
		else if (((UIElement)instance9).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance9;
		}
		else if (((UIElement)instance10).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance10;
		}
		else if (((UIElement)instance11).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance11;
		}
		else if ((BaseComponent)(object)instance12 != (BaseComponent)null && ((UIElement)instance12).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance12;
		}
		else if ((BaseComponent)(object)instance13 != (BaseComponent)null && ((UIElement)instance13).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance13;
		}
		else if ((BaseComponent)(object)instance14 != (BaseComponent)null && ((UIElement)instance14).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance14;
		}
		else if ((BaseComponent)(object)instance15 != (BaseComponent)null && ((UIElement)instance15).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance15;
		}
		else if ((BaseComponent)(object)instance16 != (BaseComponent)null && ((UIElement)instance16).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance16;
		}
		else if ((BaseComponent)(object)instance17 != (BaseComponent)null && ((UIElement)instance17).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance17;
		}
		else if ((BaseComponent)(object)instance18 != (BaseComponent)null && ((UIElement)instance18).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance18;
		}
		else if ((BaseComponent)(object)instance19 != (BaseComponent)null && ((UIElement)instance19).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance19;
		}
		else if ((BaseComponent)(object)instance20 != (BaseComponent)null && ((UIElement)instance20).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance20;
		}
		else if ((BaseComponent)(object)instance21 != (BaseComponent)null && ((UIElement)instance21).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance21;
		}
		else if ((BaseComponent)(object)instance22 != (BaseComponent)null && ((UIElement)instance22).IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance22;
		}
	}

	public static void ShowConfirmation(string title, Action _yesAction, Action _noAction, bool MPConf = false, bool skirmishMasters = false)
	{
		if (skirmishMasters)
		{
			MainViewModel.Instance.Show_HUD_ConfirmationSM = true;
		}
		else if (!MPConf)
		{
			MainViewModel.Instance.Show_HUD_Confirmation = true;
		}
		else
		{
			MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
		}
		SetInstance();
		if (FatControler.polish)
		{
			MainViewModel.Instance.ConfirmationPanelHeight = "170";
			MainViewModel.Instance.ConfirmationPanelWidth = "550";
			MainViewModel.Instance.ConfirmationPanelWidth2 = "520";
			MainViewModel.Instance.ConfirmationMessage = "";
			ConfirmationHeight = 170;
			ConfirmationWidth = 550;
			MainViewModel.Instance.ConfirmationPanelWidthView = "695";
			MainViewModel.Instance.ConfirmationPanelHeightView = "206";
		}
		else
		{
			MainViewModel.Instance.ConfirmationPanelHeight = "170";
			MainViewModel.Instance.ConfirmationPanelWidth = "450";
			MainViewModel.Instance.ConfirmationPanelWidth2 = "420";
			MainViewModel.Instance.ConfirmationMessage = "";
			ConfirmationHeight = 170;
			ConfirmationWidth = 450;
			MainViewModel.Instance.ConfirmationPanelWidthView = "486";
			MainViewModel.Instance.ConfirmationPanelHeightView = "206";
		}
		foreach (HUD_ConfirmationPopup instance in instances)
		{
			((UIElement)instance.RefConfirmCheck).Visibility = (Visibility)1;
			((UIElement)instance.RefYesNo).Visibility = (Visibility)2;
			((UIElement)instance.RefOK).Visibility = (Visibility)1;
			instance.yesAction = _yesAction;
			instance.noAction = _noAction;
			instance.RefHeading.HeadingText = title;
		}
		MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
	}

	public static void ShowOK(string title, Action _yesAction, bool MPConf = false, bool Sands = false)
	{
		if (Sands)
		{
			MainViewModel.Instance.Show_HUD_ConfirmationSands = true;
		}
		else if (!MPConf)
		{
			MainViewModel.Instance.Show_HUD_Confirmation = true;
		}
		else
		{
			MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
		}
		SetInstance();
		MainViewModel.Instance.ConfirmationPanelHeight = "170";
		MainViewModel.Instance.ConfirmationPanelWidth = "450";
		MainViewModel.Instance.ConfirmationPanelWidth2 = "420";
		MainViewModel.Instance.ConfirmationMessage = "";
		ConfirmationWidth = 450;
		ConfirmationHeight = 170;
		MainViewModel.Instance.ConfirmationPanelWidthView = "486";
		MainViewModel.Instance.ConfirmationPanelHeightView = "206";
		foreach (HUD_ConfirmationPopup instance in instances)
		{
			((UIElement)instance.RefConfirmCheck).Visibility = (Visibility)1;
			((UIElement)instance.RefYesNo).Visibility = (Visibility)1;
			((UIElement)instance.RefOK).Visibility = (Visibility)2;
			instance.yesAction = _yesAction;
			instance.RefHeading.HeadingText = title;
		}
		MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
	}

	public static void ShowConfirmationOKMessage(string title, Action _yesAction, string message, bool Sands = false)
	{
		if (Sands)
		{
			MainViewModel.Instance.Show_HUD_ConfirmationSands = true;
		}
		else
		{
			MainViewModel.Instance.Show_HUD_Confirmation = true;
		}
		SetInstance();
		MainViewModel.Instance.ConfirmationPanelHeight = "270";
		MainViewModel.Instance.ConfirmationPanelWidth = "650";
		MainViewModel.Instance.ConfirmationPanelWidth2 = "620";
		MainViewModel.Instance.ConfirmationMessage = message;
		ConfirmationHeight = 270;
		ConfirmationWidth = 650;
		MainViewModel.Instance.ConfirmationPanelWidthView = "702";
		MainViewModel.Instance.ConfirmationPanelHeightView = "327";
		foreach (HUD_ConfirmationPopup instance in instances)
		{
			((UIElement)instance.RefConfirmCheck).Visibility = (Visibility)1;
			((UIElement)instance.RefYesNo).Visibility = (Visibility)1;
			((UIElement)instance.RefOK).Visibility = (Visibility)2;
			instance.yesAction = _yesAction;
			instance.RefHeading.HeadingText = title;
		}
		MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
	}

	public static void ShowConfirmationMessage(string title, Action _yesAction, Action _noAction, string message, bool MPConf = false, bool tall = false)
	{
		if (!MPConf)
		{
			MainViewModel.Instance.Show_HUD_Confirmation = true;
		}
		else
		{
			MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
		}
		SetInstance();
		if (!tall)
		{
			MainViewModel.Instance.ConfirmationPanelHeight = "270";
			MainViewModel.Instance.ConfirmationPanelWidth = "650";
			MainViewModel.Instance.ConfirmationPanelWidth2 = "620";
			MainViewModel.Instance.ConfirmationMessage = message;
			ConfirmationHeight = 270;
			ConfirmationWidth = 650;
			MainViewModel.Instance.ConfirmationPanelWidthView = "702";
			MainViewModel.Instance.ConfirmationPanelHeightView = "327";
		}
		else
		{
			MainViewModel.Instance.ConfirmationPanelHeight = "335";
			MainViewModel.Instance.ConfirmationPanelWidth = "650";
			MainViewModel.Instance.ConfirmationPanelWidth2 = "620";
			MainViewModel.Instance.ConfirmationMessage = message;
			ConfirmationHeight = 335;
			ConfirmationWidth = 650;
			MainViewModel.Instance.ConfirmationPanelWidthView = "702";
			MainViewModel.Instance.ConfirmationPanelHeightView = "408";
		}
		foreach (HUD_ConfirmationPopup instance in instances)
		{
			((UIElement)instance.RefConfirmCheck).Visibility = (Visibility)1;
			((UIElement)instance.RefYesNo).Visibility = (Visibility)2;
			((UIElement)instance.RefOK).Visibility = (Visibility)1;
			instance.yesAction = _yesAction;
			instance.noAction = _noAction;
			instance.RefHeading.HeadingText = title;
		}
		MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
	}

	public static void ShowConfirmationCheck(string title, Action _yesAction, Action _noAction, string checkMessage, bool initialCheckState, Action<bool> _checkChangeAction, bool MPConf = false)
	{
		if (!MPConf)
		{
			MainViewModel.Instance.Show_HUD_Confirmation = true;
		}
		else
		{
			MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
		}
		SetInstance();
		if (FatControler.polish)
		{
			MainViewModel.Instance.ConfirmationPanelHeight = "170";
			MainViewModel.Instance.ConfirmationPanelWidth = "550";
			MainViewModel.Instance.ConfirmationPanelWidth2 = "520";
			MainViewModel.Instance.ConfirmationMessage = "";
			ConfirmationHeight = 170;
			ConfirmationWidth = 550;
			MainViewModel.Instance.ConfirmationPanelWidthView = "695";
			MainViewModel.Instance.ConfirmationPanelHeightView = "206";
		}
		else
		{
			MainViewModel.Instance.ConfirmationPanelHeight = "170";
			MainViewModel.Instance.ConfirmationPanelWidth = "450";
			MainViewModel.Instance.ConfirmationPanelWidth2 = "420";
			MainViewModel.Instance.ConfirmationMessage = "";
			ConfirmationHeight = 170;
			ConfirmationWidth = 450;
			MainViewModel.Instance.ConfirmationPanelWidthView = "486";
			MainViewModel.Instance.ConfirmationPanelHeightView = "206";
		}
		panelActive = false;
		MainViewModel.Instance.ConfirmationCheckLabel = checkMessage;
		foreach (HUD_ConfirmationPopup instance in instances)
		{
			((UIElement)instance.RefConfirmCheck).Visibility = (Visibility)2;
			((UIElement)instance.RefYesNo).Visibility = (Visibility)2;
			((UIElement)instance.RefOK).Visibility = (Visibility)1;
			((ToggleButton)instance.RefConfirmCheck).IsChecked = initialCheckState;
			instance.checkAction = _checkChangeAction;
			instance.yesAction = _yesAction;
			instance.noAction = _noAction;
			instance.RefHeading.HeadingText = title;
			panelActive = true;
		}
		MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
	}

	public static void ShowConfirmationMessageCheck(string title, Action _yesAction, Action _noAction, string message, string checkMessage, bool initialCheckState, Action<bool> _checkChangeAction, bool MPConf = false, int trailCustomSpecial = -1)
	{
		if (!MPConf)
		{
			MainViewModel.Instance.Show_HUD_Confirmation = true;
		}
		else
		{
			MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
		}
		if (trailCustomSpecial == -1)
		{
			SetInstance();
			MainViewModel.Instance.ConfirmationPanelHeight = "270";
			MainViewModel.Instance.ConfirmationPanelWidth = "650";
			MainViewModel.Instance.ConfirmationPanelWidth2 = "620";
			MainViewModel.Instance.ConfirmationMessage = message;
			ConfirmationHeight = 270;
			ConfirmationWidth = 650;
			MainViewModel.Instance.ConfirmationPanelWidthView = "702";
			MainViewModel.Instance.ConfirmationPanelHeightView = "327";
		}
		else
		{
			string text = trailCustomSpecial.ToString();
			foreach (HUD_ConfirmationPopup instance in instances)
			{
				if (((FrameworkElement)instance).Tag != null && (string)((FrameworkElement)instance).Tag == text)
				{
					MainViewModel.Instance.HUDConfirmationPopup = instance;
					break;
				}
			}
			MainViewModel.Instance.ConfirmationPanelHeight = "405";
			MainViewModel.Instance.ConfirmationPanelWidth = "650";
			MainViewModel.Instance.ConfirmationPanelWidth2 = "620";
			MainViewModel.Instance.ConfirmationMessage = message;
			ConfirmationHeight = 405;
			ConfirmationWidth = 650;
			MainViewModel.Instance.ConfirmationPanelWidthView = "702";
			MainViewModel.Instance.ConfirmationPanelHeightView = "490";
		}
		panelActive = false;
		MainViewModel.Instance.ConfirmationCheckLabel = checkMessage;
		foreach (HUD_ConfirmationPopup instance2 in instances)
		{
			((UIElement)instance2.RefConfirmCheck).Visibility = (Visibility)2;
			((UIElement)instance2.RefYesNo).Visibility = (Visibility)2;
			((UIElement)instance2.RefOK).Visibility = (Visibility)1;
			((ToggleButton)instance2.RefConfirmCheck).IsChecked = initialCheckState;
			instance2.checkAction = _checkChangeAction;
			instance2.yesAction = _yesAction;
			instance2.noAction = _noAction;
			instance2.RefHeading.HeadingText = title;
		}
		panelActive = true;
		MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
	}

	public void ConfirmationClicked(int mode)
	{
		MainViewModel.Instance.Show_HUD_Confirmation = false;
		MainViewModel.Instance.Show_HUD_ConfirmationMP = false;
		MainViewModel.Instance.Show_HUD_ConfirmationSands = false;
		MainViewModel.Instance.Show_HUD_ConfirmationSM = false;
		switch (mode)
		{
		case -1:
		case 1:
			if (yesAction != null)
			{
				yesAction();
			}
			break;
		case 2:
			if (noAction != null)
			{
				noAction();
			}
			break;
		}
	}
}
