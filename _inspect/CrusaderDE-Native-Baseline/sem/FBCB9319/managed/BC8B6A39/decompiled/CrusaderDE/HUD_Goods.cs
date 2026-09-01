using Noesis;

namespace CrusaderDE;

public class HUD_Goods : UserControl
{
	public HUD_Goods()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDGoods = this;
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Goods.xaml");
	}
}
