using Noesis;

namespace CrusaderDE;

public class OST_Koth : UserControl
{
	public OST_Koth()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/OST_Koth.xaml");
	}
}
