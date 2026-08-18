using Noesis;

namespace CrusaderDE;

public class WGT_Heading : UserControl
{
	public Grid RefLayoutRoot;

	public TextBlock RefHeadingTextBlock;

	public static readonly DependencyProperty headingTextProperty = DependencyProperty.Register("HeadingText", typeof(string), typeof(WGT_Heading));

	public static readonly DependencyProperty dividerProperty = DependencyProperty.Register("Divider", typeof(ImageSource), typeof(WGT_Heading));

	public string HeadingText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(headingTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(headingTextProperty, (object)value);
		}
	}

	public ImageSource Divider
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Expected O, but got Unknown
			return (ImageSource)((DependencyObject)this).GetValue(dividerProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(dividerProperty, (object)value);
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

	public WGT_Heading()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		InitializeComponent();
		RefLayoutRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		((FrameworkElement)RefLayoutRoot).DataContext = this;
		RefHeadingTextBlock = (TextBlock)((FrameworkElement)this).FindName("HeadingTextBlock");
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/WGT_Heading.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
