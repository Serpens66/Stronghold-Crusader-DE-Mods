using Noesis;

namespace CrusaderDE;

public class HUD_StartingGoods : UserControl
{
	public HUD_StartingGoods()
	{
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_StartingGoods.xaml");
	}
}
