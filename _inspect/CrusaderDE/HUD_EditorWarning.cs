using Noesis;

namespace CrusaderDE;

public class HUD_EditorWarning : UserControl
{
	public HUD_EditorWarning()
	{
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_EditorWarning.xaml");
	}
}
