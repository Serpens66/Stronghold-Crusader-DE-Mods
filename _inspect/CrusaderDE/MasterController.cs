using Noesis;

namespace CrusaderDE;

public class MasterController : UserControl
{
	public MasterController()
	{
		((FrameworkElement)this).DataContext = MainViewModel.INIT();
		InitializeComponent();
		MainViewModel.Instance.GlobalUIRoot = this;
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAML/MasterController.xaml");
	}
}
