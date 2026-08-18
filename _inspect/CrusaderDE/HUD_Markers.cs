using Noesis;

namespace CrusaderDE;

public class HUD_Markers : UserControl
{
	public RadioButton RefMarkerInvisible;

	public RadioButton RefMarkerVisible;

	public RadioButton RefMarkerDisappearing;

	public bool DisableRadios;

	public HUD_Markers()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDMarkers = this;
		RefMarkerInvisible = (RadioButton)((FrameworkElement)this).FindName("MarkerInvisible");
		RefMarkerVisible = (RadioButton)((FrameworkElement)this).FindName("MarkerVisible");
		RefMarkerDisappearing = (RadioButton)((FrameworkElement)this).FindName("MarkerDisappearing");
		((ToggleButton)RefMarkerInvisible).Checked += new RoutedEventHandler(Include_ValueChanged);
		((ToggleButton)RefMarkerVisible).Checked += new RoutedEventHandler(Include_ValueChanged);
		((ToggleButton)RefMarkerDisappearing).Checked += new RoutedEventHandler(Include_ValueChanged);
	}

	public void Include_ValueChanged(object sender, RoutedEventArgs e)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (!DisableRadios && ((ToggleButton)(RadioButton)e.Source).IsChecked == true && MainControls.instance.CurrentAction == 3)
		{
			int structureID = MainControls.instance.CurrentSubAction - 380;
			int state = 1;
			if (((ToggleButton)RefMarkerVisible).IsChecked == true)
			{
				state = 2;
			}
			if (((ToggleButton)RefMarkerDisappearing).IsChecked == true)
			{
				state = 3;
			}
			EngineInterface.GameAction(Enums.GameActionCommand.SetMarkerState, structureID, state);
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Markers.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			return true;
		}
		return false;
	}
}
