using System;
using Noesis;

namespace CrusaderDE;

public class HUD_ScenarioSpecial : UserControl
{
	public static bool PendingMPInvite;

	private DateTime PendingMPInviteTime = DateTime.MinValue;

	public HUD_ScenarioSpecial()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDScenarioSpecial = this;
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_ScenarioSpecial.xaml");
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
