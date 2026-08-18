using Noesis;

namespace CrusaderDE;

public class HUD_StartingGoods : UserControl
{
	public HUD_StartingGoods()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_StartingGoods.xaml");
	}
}
