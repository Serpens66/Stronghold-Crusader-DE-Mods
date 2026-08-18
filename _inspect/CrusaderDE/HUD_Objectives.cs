using Noesis;

namespace CrusaderDE;

public class HUD_Objectives : UserControl
{
	public Grid RefRoot;

	public Grid RefObjectiveTimer;

	public StackPanel RefObjectiveStackPanel;

	public WGT_Objective[] RefWGTObjectives = new WGT_Objective[7];

	public HUD_Objectives()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		InitializeComponent();
		MainViewModel.Instance.HUDObjectives = this;
		RefRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		RefObjectiveTimer = (Grid)((FrameworkElement)this).FindName("ObjectiveTimer");
		RefObjectiveStackPanel = (StackPanel)((FrameworkElement)this).FindName("ObjectiveStackPanel");
		RefWGTObjectives[0] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective1");
		RefWGTObjectives[1] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective2");
		RefWGTObjectives[2] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective3");
		RefWGTObjectives[3] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective4");
		RefWGTObjectives[4] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective5");
		RefWGTObjectives[5] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective6");
		RefWGTObjectives[6] = (WGT_Objective)((FrameworkElement)this).FindName("WGT_Objective7");
		if (FatControler.arabic && ConfigSettings.Settings_ArabicL2R)
		{
			((FrameworkElement)RefObjectiveStackPanel).Margin = new Thickness(0f, 0f, 20f, 0f);
		}
	}

	public void SetSizeFromRows(int numRows)
	{
		((FrameworkElement)RefRoot).Height = 200 - (7 - numRows) * 25;
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Objectives.xaml");
	}
}
