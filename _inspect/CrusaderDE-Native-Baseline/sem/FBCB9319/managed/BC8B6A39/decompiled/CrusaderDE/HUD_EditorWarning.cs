using Noesis;

namespace CrusaderDE;

public class HUD_EditorWarning : UserControl
{
	public HUD_EditorWarning()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_EditorWarning.xaml");
	}
}
