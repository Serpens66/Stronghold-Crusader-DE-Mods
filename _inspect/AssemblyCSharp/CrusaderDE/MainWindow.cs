using Noesis;

namespace CrusaderDE;

public class MainWindow : UserControl
{
	public MainWindow()
	{
		base.DataContext = MainViewModel.Instance;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAML/MainWindow.xaml");
	}
}
