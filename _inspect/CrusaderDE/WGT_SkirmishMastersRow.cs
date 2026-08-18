using Noesis;

namespace CrusaderDE;

public class WGT_SkirmishMastersRow : UserControl
{
	public WGT_SkirmishMastersRow()
	{
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/WGT_SkirmishMastersRow.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
