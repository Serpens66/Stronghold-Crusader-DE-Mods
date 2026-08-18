using System;
using Noesis;

namespace CrusaderDE;

public class FRONT_Extra4Campaign : UserControl
{
	public static Image RefBasemap;

	public static string rolloverMessage = "";

	public static string rolloverMissionName = "";

	public static DateTime rolloverTime = DateTime.MinValue;

	public static bool rolloverShow = false;

	public static Button rolloverButton = null;

	public FRONT_Extra4Campaign()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		InitializeComponent();
		RefBasemap = (Image)((FrameworkElement)this).FindName("Basemap");
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_Extra4Campaign.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "CampaignMenuCommandEnter")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(CampaignMenuCommandEnter);
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "CampaignMenuCommandLeave")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseLeave += new MouseEventHandler(CampaignMenuCommandLeave);
			}
			return true;
		}
		return false;
	}

	public void CampaignMenuCommandEnter(object sender, MouseEventArgs e)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Expected O, but got Unknown
		if (!(((RoutedEventArgs)e).Source is Button))
		{
			return;
		}
		string text = (string)((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter;
		switch (text)
		{
		case "41":
		case "42":
		case "43":
		case "44":
		case "45":
		{
			int num = int.Parse(text, Director.defaultCulture) - 30 + 5;
			int difficulty = 0;
			if (ConfigSettings.MapCompleted("mission" + num, ref difficulty))
			{
				rolloverMessage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				if (difficulty >= 0 && difficulty <= 3)
				{
					rolloverMessage += " : ";
					rolloverMessage += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 19 + difficulty);
				}
				rolloverShow = false;
				rolloverTime = DateTime.UtcNow.AddSeconds(1.0);
				rolloverButton = (Button)((RoutedEventArgs)e).Source;
				rolloverMissionName = Translate.Instance.lookUpText((Enums.eTextSections)(99 + (num - 1) * 4), 1);
			}
			break;
		}
		}
	}

	public void CampaignMenuCommandLeave(object sender, MouseEventArgs e)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		if (!(((RoutedEventArgs)e).Source is Button))
		{
			return;
		}
		switch ((string)((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter)
		{
		case "41":
		case "42":
		case "43":
		case "44":
		case "45":
			if (rolloverMessage.Length > 0)
			{
				PropEx.SetTextCentre((UIElement)(Button)((RoutedEventArgs)e).Source, rolloverMissionName);
				rolloverMessage = "";
			}
			break;
		}
	}

	public static void Update()
	{
		if (rolloverMessage.Length > 0 && DateTime.UtcNow > rolloverTime)
		{
			rolloverTime = DateTime.UtcNow.AddSeconds(1.0);
			rolloverShow = !rolloverShow;
			if (rolloverShow)
			{
				PropEx.SetTextCentre((UIElement)(object)rolloverButton, rolloverMessage);
			}
			else
			{
				PropEx.SetTextCentre((UIElement)(object)rolloverButton, rolloverMissionName);
			}
		}
	}
}
