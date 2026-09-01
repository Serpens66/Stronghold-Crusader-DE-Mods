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
			return (string)GetValue(headingTextProperty);
		}
		set
		{
			SetValue(headingTextProperty, value);
		}
	}

	public ImageSource Divider
	{
		get
		{
			return (ImageSource)GetValue(dividerProperty);
		}
		set
		{
			SetValue(dividerProperty, value);
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
		InitializeComponent();
		RefLayoutRoot = (Grid)FindName("LayoutRoot");
		RefLayoutRoot.DataContext = this;
		RefHeadingTextBlock = (TextBlock)FindName("HeadingTextBlock");
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/WGT_Heading.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
