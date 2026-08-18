using Noesis;

namespace CrusaderDE;

public class WGT_SkirmishMastersRow : UserControl
{
	public WGT_SkirmishMastersRow()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/WGT_SkirmishMastersRow.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
