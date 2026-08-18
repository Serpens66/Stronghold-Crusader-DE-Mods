using Noesis;

namespace CrusaderDE;

public class TestBed : UserControl
{
	public TestBed()
	{
		((FrameworkElement)this).DataContext = MainViewModel.Instance;
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAML/TestBed.xaml");
	}
}
