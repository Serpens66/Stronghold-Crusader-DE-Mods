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
			return (string)((DependencyObject)this).GetValue(popValueProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(popValueProperty, (object)value);
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
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		InitializeComponent();
		RefPopHead = (Image)((FrameworkElement)this).FindName("PopHead");
		RefLayoutRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		((FrameworkElement)RefLayoutRoot).DataContext = this;
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
			((UIElement)RefPopHead).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefPopHead).Visibility = (Visibility)1;
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/WGT_Popularity.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
