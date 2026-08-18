using Noesis;

namespace CrusaderDE;

public class TestBed : UserControl
{
	public TestBed()
	{
		base.DataContext = MainViewModel.Instance;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAML/TestBed.xaml");
	}
}
