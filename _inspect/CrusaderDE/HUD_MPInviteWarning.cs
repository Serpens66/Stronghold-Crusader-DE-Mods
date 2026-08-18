using System;
using Noesis;

namespace CrusaderDE;

public class HUD_MPInviteWarning : UserControl
{
	public static bool PendingMPInvite;

	public DateTime PendingMPInviteTime = DateTime.MinValue;

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

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_MPInviteWarning.xaml");
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
