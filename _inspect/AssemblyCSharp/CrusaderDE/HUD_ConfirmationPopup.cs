using System;
using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_ConfirmationPopup : UserControl
{
	private WGT_Heading RefHeading;

	private Grid RefYesNo;

	private Grid RefOK;

	public CheckBox RefConfirmCheck;

	private static List<HUD_ConfirmationPopup> instances = new List<HUD_ConfirmationPopup>();

	private static HUD_ConfirmationPopup instance1 = null;

	private static HUD_ConfirmationPopup instance2 = null;

	private static HUD_ConfirmationPopup instance3 = null;

	private static HUD_ConfirmationPopup instance4 = null;

	private static HUD_ConfirmationPopup instance5 = null;

	private static HUD_ConfirmationPopup instance6 = null;

	private static HUD_ConfirmationPopup instance7 = null;

	private static HUD_ConfirmationPopup instance8 = null;

	private static HUD_ConfirmationPopup instance9 = null;

	private static HUD_ConfirmationPopup instance10 = null;

	private static HUD_ConfirmationPopup instance11 = null;

	private static HUD_ConfirmationPopup instance12 = null;

	private static HUD_ConfirmationPopup instance13 = null;

	private static HUD_ConfirmationPopup instance14 = null;

	private static HUD_ConfirmationPopup instance15 = null;

	private static HUD_ConfirmationPopup instance16 = null;

	private static HUD_ConfirmationPopup instance17 = null;

	private static HUD_ConfirmationPopup instance18 = null;

	private static HUD_ConfirmationPopup instance19 = null;

	private static HUD_ConfirmationPopup instance20 = null;

	private static HUD_ConfirmationPopup instance21 = null;

	private static HUD_ConfirmationPopup instance22 = null;

	public static int ConfirmationWidth = 450;

	public static int ConfirmationHeight = 170;

	private static bool panelActive = true;

	private Action yesAction;

	private Action noAction;

	private Action<bool> checkAction;

	public HUD_ConfirmationPopup()
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
		else if (instance3 == null)
		{
			instance3 = this;
		}
		else if (instance4 == null)
		{
			instance4 = this;
		}
		else if (instance5 == null)
		{
			instance5 = this;
		}
		else if (instance6 == null)
		{
			instance6 = this;
		}
		else if (instance7 == null)
		{
			instance7 = this;
		}
		else if (instance8 == null)
		{
			instance8 = this;
		}
		else if (instance9 == null)
		{
			instance9 = this;
		}
		else if (instance10 == null)
		{
			instance10 = this;
		}
		else if (instance11 == null)
		{
			instance11 = this;
		}
		else if (instance12 == null)
		{
			instance12 = this;
		}
		else if (instance13 == null)
		{
			instance13 = this;
		}
		else if (instance14 == null)
		{
			instance14 = this;
		}
		else if (instance15 == null)
		{
			instance15 = this;
		}
		else if (instance16 == null)
		{
			instance16 = this;
		}
		else if (instance17 == null)
		{
			instance17 = this;
		}
		else if (instance18 == null)
		{
			instance18 = this;
		}
		else if (instance19 == null)
		{
			instance19 = this;
		}
		else if (instance20 == null)
		{
			instance20 = this;
		}
		else if (instance21 == null)
		{
			instance21 = this;
		}
		else if (instance22 == null)
		{
			instance22 = this;
		}
		instances.Add(this);
		RefHeading = (WGT_Heading)FindName("ConfirmationHeader");
		RefYesNo = (Grid)FindName("YesNo");
		RefOK = (Grid)FindName("OK");
		RefConfirmCheck = (CheckBox)FindName("ConfirmCheck");
		RefConfirmCheck.Checked += Check_ValueChanged;
		RefConfirmCheck.Unchecked += Check_ValueChanged;
		if (FatControler.hungarian)
		{
			RefHeading.RefHeadingTextBlock.FontSize = 30f;
		}
		if (FatControler.polish)
		{
			RefHeading.RefHeadingTextBlock.FontSize = 30f;
		}
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_ConfirmationPopup.xaml");
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

	private void Check_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive && checkAction != null)
		{
			checkAction(RefConfirmCheck.IsChecked.Value);
		}
	}

	private static void SetInstance()
	{
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance1;
		}
		else if (instance2.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance2;
		}
		else if (instance3.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance3;
		}
		else if (instance4.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance4;
		}
		else if (instance5.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance5;
		}
		else if (instance6.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance6;
		}
		else if (instance7.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance7;
		}
		else if (instance8.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance8;
		}
		else if (instance9.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance9;
		}
		else if (instance10.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance10;
		}
		else if (instance11.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance11;
		}
		else if (instance12 != null && instance12.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance12;
		}
		else if (instance13 != null && instance13.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance13;
		}
		else if (instance14 != null && instance14.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance14;
		}
		else if (instance15 != null && instance15.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance15;
		}
		else if (instance16 != null && instance16.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance16;
		}
		else if (instance17 != null && instance17.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance17;
		}
		else if (instance18 != null && instance18.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance18;
		}
		else if (instance19 != null && instance19.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance19;
		}
		else if (instance20 != null && instance20.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance20;
		}
		else if (instance21 != null && instance21.IsVisible)
		{
			MainViewModel.Instance.HUDConfirmationPopup = instance21;
		}
		else if (instance22 != null && instance22.IsVisible)
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
			instance.RefConfirmCheck.Visibility = Visibility.Hidden;
			instance.RefYesNo.Visibility = Visibility.Visible;
			instance.RefOK.Visibility = Visibility.Hidden;
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
			instance.RefConfirmCheck.Visibility = Visibility.Hidden;
			instance.RefYesNo.Visibility = Visibility.Hidden;
			instance.RefOK.Visibility = Visibility.Visible;
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
			instance.RefConfirmCheck.Visibility = Visibility.Hidden;
			instance.RefYesNo.Visibility = Visibility.Hidden;
			instance.RefOK.Visibility = Visibility.Visible;
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
			instance.RefConfirmCheck.Visibility = Visibility.Hidden;
			instance.RefYesNo.Visibility = Visibility.Visible;
			instance.RefOK.Visibility = Visibility.Hidden;
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
			instance.RefConfirmCheck.Visibility = Visibility.Visible;
			instance.RefYesNo.Visibility = Visibility.Visible;
			instance.RefOK.Visibility = Visibility.Hidden;
			instance.RefConfirmCheck.IsChecked = initialCheckState;
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
				if (instance.Tag != null && (string)instance.Tag == text)
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
			instance2.RefConfirmCheck.Visibility = Visibility.Visible;
			instance2.RefYesNo.Visibility = Visibility.Visible;
			instance2.RefOK.Visibility = Visibility.Hidden;
			instance2.RefConfirmCheck.IsChecked = initialCheckState;
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
