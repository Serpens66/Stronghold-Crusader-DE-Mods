using Noesis;

namespace CrusaderDE;

public class WGT_Popularity : UserControl
{
	public Grid RefLayoutRoot;

	public Image RefPopHead;

	public static readonly DependencyProperty popValueProperty = DependencyProperty.Register("PopValue", typeof(string), typeof(WGT_Popularity));

	public string PopValue
	{
		get
		{
			return (string)GetValue(popValueProperty);
		}
		set
		{
			SetValue(popValueProperty, value);
		}
	}

	public string GlobalTextFlowAL2R
	{
		get
		{
			return MainViewModel.Instance.GlobalTextFlowAL2R;
		}
		set
		{
		}
	}

	public WGT_Popularity()
	{
		InitializeComponent();
		RefPopHead = (Image)FindName("PopHead");
		RefLayoutRoot = (Grid)FindName("LayoutRoot");
		RefLayoutRoot.DataContext = this;
	}

	public void SetPopHead(int value, bool visible)
	{
		if (value < 0)
		{
			RefPopHead.Source = MainViewModel.Instance.GameSprites[2];
		}
		else if (value > 0)
		{
			RefPopHead.Source = MainViewModel.Instance.GameSprites[0];
		}
		else
		{
			RefPopHead.Source = MainViewModel.Instance.GameSprites[1];
		}
		if (visible)
		{
			RefPopHead.Visibility = Visibility.Visible;
		}
		else
		{
			RefPopHead.Visibility = Visibility.Hidden;
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/WGT_Popularity.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
