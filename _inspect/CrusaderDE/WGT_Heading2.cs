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
			return (string)((DependencyObject)this).GetValue(headingText2Property);
		}
		set
		{
			((DependencyObject)this).SetValue(headingText2Property, (object)value);
		}
	}

	public double HeadingFontSize
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(headingFontSize2Property);
		}
		set
		{
			((DependencyObject)this).SetValue(headingFontSize2Property, (object)value);
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
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		InitializeComponent();
		RefLayoutRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		((FrameworkElement)RefLayoutRoot).DataContext = this;
		RefHeading2TextBlock = (TextBlock)((FrameworkElement)this).FindName("HeadingTextBlock");
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/WGT_Heading2.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
