using Noesis;

namespace CrusaderDE;

public class WGT_Objective : UserControl
{
	public Grid RefLayoutRoot;

	public Image RefCheckBoxOFF;

	public Image RefCheckBoxON;

	public TextBlock RefObjectiveTypeText;

	public TextBlock RefObjectiveValueText;

	public static readonly DependencyProperty typeTextProperty = DependencyProperty.Register("TypeText", typeof(string), typeof(WGT_Objective));

	public static readonly DependencyProperty amountTextProperty = DependencyProperty.Register("AmountText", typeof(string), typeof(WGT_Objective));

	public string TypeText
	{
		get
		{
			return (string)GetValue(typeTextProperty);
		}
		set
		{
			SetValue(typeTextProperty, value);
		}
	}

	public string AmountText
	{
		get
		{
			return (string)GetValue(amountTextProperty);
		}
		set
		{
			SetValue(amountTextProperty, value);
		}
	}

	public string GlobalTextFlowAL2R
	{
		get
		{
			return MainViewModel.Instance.GlobalTextFlowAL2R;
		}
		set
		{
		}
	}

	public WGT_Objective()
	{
		InitializeComponent();
		RefCheckBoxOFF = (Image)FindName("CheckBoxOFF");
		RefCheckBoxON = (Image)FindName("CheckBoxON");
		RefLayoutRoot = (Grid)FindName("LayoutRoot");
		RefLayoutRoot.DataContext = this;
		RefObjectiveTypeText = (TextBlock)FindName("ObjectiveTypeText");
		RefObjectiveValueText = (TextBlock)FindName("ObjectiveValueText");
		if (FatControler.russian)
		{
			RefObjectiveTypeText.FontSize = 16f;
			RefObjectiveValueText.FontSize = 16f;
			RefObjectiveTypeText.Margin = new Thickness(0f, 0f, 0f, -2f);
			RefObjectiveValueText.Margin = new Thickness(0f, 0f, 54f, -2f);
		}
		if (FatControler.hungarian)
		{
			RefObjectiveTypeText.FontSize = 16f;
			RefObjectiveValueText.FontSize = 16f;
			RefObjectiveTypeText.Margin = new Thickness(0f, 0f, 0f, -1f);
			RefObjectiveValueText.Margin = new Thickness(0f, 0f, 54f, -1f);
		}
	}

	public void SetObjective(bool isActive, string LText, string RText, bool complete)
	{
		TypeText = LText;
		AmountText = RText;
		if (!isActive)
		{
			RefCheckBoxOFF.Visibility = Visibility.Hidden;
			RefCheckBoxON.Visibility = Visibility.Hidden;
		}
		else if (complete)
		{
			RefCheckBoxOFF.Visibility = Visibility.Hidden;
			RefCheckBoxON.Visibility = Visibility.Visible;
		}
		else
		{
			RefCheckBoxOFF.Visibility = Visibility.Visible;
			RefCheckBoxON.Visibility = Visibility.Hidden;
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/WGT_Objective.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
