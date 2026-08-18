using System;
using Noesis;

namespace CrusaderDE;

public class FRONT_Trail3 : UserControl
{
	private Point MousePosition;

	private Grid refmapgrid;

	public static Image refChicken;

	private static string rolloverMessage = "";

	private static string rolloverMissionName = "";

	private static DateTime rolloverTime = DateTime.MinValue;

	private static bool rolloverShow = false;

	private static Button rolloverButton = null;

	private UIElement SelectedObject { get; set; }

	public FRONT_Trail3()
	{
		InitializeComponent();
		refmapgrid = (Grid)FindName("mapgrid");
		refmapgrid.MouseLeftButtonDown += MapImage_MouseLeftClick;
		refChicken = (Image)FindName("Chicken");
	}

	public void MapImage_MouseLeftClick(object sender, MouseEventArgs e)
	{
		SelectedObject = e.Source as UIElement;
		if (SelectedObject.GetType() == typeof(Rectangle) || SelectedObject.GetType() == typeof(Image))
		{
			MousePosition = e.GetPosition(SelectedObject);
			MainViewModel.Instance.FrontEndMenu.TrailMapClicked((int)MousePosition.X, (int)MousePosition.Y);
		}
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_Trail3.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "ChickenCommandEnter")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MainViewModel.Instance.FrontEndMenu.ChickenCommandEnter;
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "ChickenCommandLeave")
		{
			if (source is Button)
			{
				((Button)source).MouseLeave += MainViewModel.Instance.FrontEndMenu.ChickenCommandLeave;
			}
			return true;
		}
		return false;
	}

	public static void Update()
	{
		if (rolloverMessage.Length > 0 && DateTime.UtcNow > rolloverTime)
		{
			rolloverTime = DateTime.UtcNow.AddSeconds(1.0);
			rolloverShow = !rolloverShow;
			if (rolloverShow)
			{
				PropEx.SetTextCentre(rolloverButton, rolloverMessage);
			}
			else
			{
				PropEx.SetTextCentre(rolloverButton, rolloverMissionName);
			}
		}
	}
}
