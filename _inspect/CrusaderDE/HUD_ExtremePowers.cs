using Noesis;

namespace CrusaderDE;

public class HUD_ExtremePowers : UserControl
{
	public static Storyboard RefStory_ShowExtremeHelp;

	public HUD_ExtremePowers()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		InitializeComponent();
		RefStory_ShowExtremeHelp = (Storyboard)((FrameworkElement)this).TryFindResource((object)"Story_ShowExtremeHelp");
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_ExtremePowers.xaml");
	}
}
