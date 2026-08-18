using Noesis;

namespace CrusaderDE;

public class HUD_Goods : UserControl
{
	public HUD_Goods()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDGoods = this;
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Goods.xaml");
	}
}
