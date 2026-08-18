using Noesis;

namespace CrusaderDE;

public class OST_StartingGoods : UserControl
{
	private TextBlock refTimeUntilLabel;

	public static Storyboard refCartStory;

	public static Image refCart;

	public OST_StartingGoods()
	{
		InitializeComponent();
		refTimeUntilLabel = (TextBlock)FindName("TimeUntilLabel");
		refCart = (Image)FindName("Cart");
		refCartStory = (Storyboard)TryFindResource("CartIntro");
		refCartStory.Completed += delegate
		{
			MainViewModel.Instance.OST_Cart_Vis = false;
		};
		if (FatControler.polish)
		{
			refTimeUntilLabel.FontSize = 14f;
		}
		if (FatControler.hungarian)
		{
			refTimeUntilLabel.FontSize = 16f;
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/OST_StartingGoods.xaml");
	}
}
