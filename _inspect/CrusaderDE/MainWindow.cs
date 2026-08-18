using Noesis;

namespace CrusaderDE;

public class MainWindow : UserControl
{
	public MainWindow()
	{
		((FrameworkElement)this).DataContext = MainViewModel.Instance;
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAML/MainWindow.xaml");
	}
}
