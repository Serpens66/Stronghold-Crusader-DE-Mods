using Noesis;

namespace CrusaderDE;

public class OST_Koth : UserControl
{
	public OST_Koth()
	{
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/OST_Koth.xaml");
	}
}
