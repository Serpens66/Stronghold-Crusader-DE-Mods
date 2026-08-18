using Noesis;

namespace CrusaderDE;

public class FRONT_SandsTrail1 : UserControl
{
	public Point MousePosition;

	public Grid refmapgrid;

	public static Image refChicken;

	public UIElement SelectedObject { get; set; }

	public FRONT_SandsTrail1()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		InitializeComponent();
		refmapgrid = (Grid)((FrameworkElement)this).FindName("mapgrid");
		((UIElement)refmapgrid).MouseLeftButtonDown += new MouseButtonEventHandler(MapImage_MouseLeftClick);
		refChicken = (Image)((FrameworkElement)this).FindName("Chicken");
	}

	public void MapImage_MouseLeftClick(object sender, MouseEventArgs e)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		object source = ((RoutedEventArgs)e).Source;
		SelectedObject = (UIElement)((source is UIElement) ? source : null);
		if (((object)SelectedObject).GetType() == typeof(Rectangle) || ((object)SelectedObject).GetType() == typeof(Image))
		{
			MousePosition = e.GetPosition(SelectedObject);
			MainViewModel.Instance.FrontEndMenu.TrailMapClicked((int)((Point)(ref MousePosition)).X, (int)((Point)(ref MousePosition)).Y);
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_SandsTrail1.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "ChickenCommandEnter")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.FrontEndMenu.ChickenCommandEnter);
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "ChickenCommandLeave")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseLeave += new MouseEventHandler(MainViewModel.Instance.FrontEndMenu.ChickenCommandLeave);
			}
			return true;
		}
		return false;
	}
}
