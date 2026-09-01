using Noesis;

namespace CrusaderDE;

public class HUD_ExtremePowers : UserControl
{
	public static Storyboard RefStory_ShowExtremeHelp;

	public HUD_ExtremePowers()
	{
		InitializeComponent();
		RefStory_ShowExtremeHelp = (Storyboard)TryFindResource("Story_ShowExtremeHelp");
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_ExtremePowers.xaml");
	}
}
