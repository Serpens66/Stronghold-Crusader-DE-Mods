using System;
using Noesis;

namespace CrusaderDE;

public class HUD_MPInviteWarning : UserControl
{
	public static bool PendingMPInvite;

	private DateTime PendingMPInviteTime = DateTime.MinValue;

	public HUD_MPInviteWarning()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDMPInviteWarning = this;
	}

	public void ShowInviteWarning()
	{
		MainViewModel.Instance.Show_HUD_MPInviteWarning = true;
		PendingMPInvite = true;
		PendingMPInviteTime = DateTime.UtcNow.AddSeconds(60.0);
	}

	public void ButtonClicked()
	{
		MainViewModel.Instance.Show_HUD_MPInviteWarning = false;
		PendingMPInvite = false;
	}

	public void Update()
	{
		if (DateTime.UtcNow > PendingMPInviteTime)
		{
			MainViewModel.Instance.Show_HUD_MPInviteWarning = false;
			PendingMPInvite = false;
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_MPInviteWarning.xaml");
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
