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
			return (string)((DependencyObject)this).GetValue(typeTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(typeTextProperty, (object)value);
		}
	}

	public string AmountText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(amountTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(amountTextProperty, (object)value);
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
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Expected O, but got Unknown
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		InitializeComponent();
		RefCheckBoxOFF = (Image)((FrameworkElement)this).FindName("CheckBoxOFF");
		RefCheckBoxON = (Image)((FrameworkElement)this).FindName("CheckBoxON");
		RefLayoutRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		((FrameworkElement)RefLayoutRoot).DataContext = this;
		RefObjectiveTypeText = (TextBlock)((FrameworkElement)this).FindName("ObjectiveTypeText");
		RefObjectiveValueText = (TextBlock)((FrameworkElement)this).FindName("ObjectiveValueText");
		if (FatControler.russian)
		{
			RefObjectiveTypeText.FontSize = 16f;
			RefObjectiveValueText.FontSize = 16f;
			((FrameworkElement)RefObjectiveTypeText).Margin = new Thickness(0f, 0f, 0f, -2f);
			((FrameworkElement)RefObjectiveValueText).Margin = new Thickness(0f, 0f, 54f, -2f);
		}
		if (FatControler.hungarian)
		{
			RefObjectiveTypeText.FontSize = 16f;
			RefObjectiveValueText.FontSize = 16f;
			((FrameworkElement)RefObjectiveTypeText).Margin = new Thickness(0f, 0f, 0f, -1f);
			((FrameworkElement)RefObjectiveValueText).Margin = new Thickness(0f, 0f, 54f, -1f);
		}
	}

	public void SetObjective(bool isActive, string LText, string RText, bool complete)
	{
		TypeText = LText;
		AmountText = RText;
		if (!isActive)
		{
			((UIElement)RefCheckBoxOFF).Visibility = (Visibility)1;
			((UIElement)RefCheckBoxON).Visibility = (Visibility)1;
		}
		else if (complete)
		{
			((UIElement)RefCheckBoxOFF).Visibility = (Visibility)1;
			((UIElement)RefCheckBoxON).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefCheckBoxOFF).Visibility = (Visibility)2;
			((UIElement)RefCheckBoxON).Visibility = (Visibility)1;
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/WGT_Objective.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}
}
