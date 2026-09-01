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
		InitializeComponent();
		MainViewModel.Instance.HUDMarkers = this;
		RefMarkerInvisible = (RadioButton)FindName("MarkerInvisible");
		RefMarkerVisible = (RadioButton)FindName("MarkerVisible");
		RefMarkerDisappearing = (RadioButton)FindName("MarkerDisappearing");
		RefMarkerInvisible.Checked += Include_ValueChanged;
		RefMarkerVisible.Checked += Include_ValueChanged;
		RefMarkerDisappearing.Checked += Include_ValueChanged;
	}

	private void Include_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (!DisableRadios && ((RadioButton)e.Source).IsChecked == true && MainControls.instance.CurrentAction == 3)
		{
			int structureID = MainControls.instance.CurrentSubAction - 380;
			int state = 1;
			if (RefMarkerVisible.IsChecked == true)
			{
				state = 2;
			}
			if (RefMarkerDisappearing.IsChecked == true)
			{
				state = 3;
			}
			EngineInterface.GameAction(Enums.GameActionCommand.SetMarkerState, structureID, state);
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Markers.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			return true;
		}
		return false;
	}
}
