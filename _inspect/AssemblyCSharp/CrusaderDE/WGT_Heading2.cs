using Noesis;

namespace CrusaderDE;

public class WGT_Heading2 : UserControl
{
	public Grid RefLayoutRoot;

	public TextBlock RefHeading2TextBlock;

	public static readonly DependencyProperty headingText2Property = DependencyProperty.Register("HeadingText2", typeof(string), typeof(WGT_Heading2));

	public static readonly DependencyProperty headingFontSize2Property = DependencyProperty.Register("HeadingFontSize", typeof(double), typeof(WGT_Heading2));

	public string HeadingText2
	{
		get
		{
			return (string)GetValue(headingText2Property);
		}
		set
		{
			SetValue(headingText2Property, value);
		}
	}

	public double HeadingFontSize
	{
		get
		{
			return (double)GetValue(headingFontSize2Property);
		}
		set
		{
			SetValue(headingFontSize2Property, value);
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

	public WGT_Heading2()
	{
		InitializeComponent();
		RefLayoutRoot = (Grid)FindName("LayoutRoot");
		RefLayoutRoot.DataContext = this;
		RefHeading2TextBlock = (TextBlock)FindName("HeadingTextBlock");
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/WGT_Heading2.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
