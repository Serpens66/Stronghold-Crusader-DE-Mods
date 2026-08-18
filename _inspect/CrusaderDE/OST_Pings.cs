using Noesis;

namespace CrusaderDE;

public class OST_Pings : UserControl
{
	public OST_Pings()
	{
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/OST_Pings.xaml");
	}
}
