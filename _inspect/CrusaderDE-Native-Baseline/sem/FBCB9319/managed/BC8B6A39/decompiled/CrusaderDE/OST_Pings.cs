using Noesis;

namespace CrusaderDE;

public class OST_Pings : UserControl
{
	public OST_Pings()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/OST_Pings.xaml");
	}
}
