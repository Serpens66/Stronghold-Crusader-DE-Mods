using System.ComponentModel;
using Noesis;

namespace CrusaderDE;

public class ScenarioEditorRow : INotifyPropertyChanged
{
	public string _text1;

	public string _text2;

	public string _text3;

	public string _dataValue;

	public string _text1HL;

	public string _text3HL;

	public Visibility _borderVisibility = (Visibility)2;

	public int DataValue2;

	public HUD_Scenario parent;

	public string Text1
	{
		get
		{
			return _text1;
		}
		set
		{
			_text1 = value;
			NotifyPropertyChanged("Text1");
		}
	}

	public string Text2
	{
		get
		{
			return _text2;
		}
		set
		{
			_text2 = value;
			NotifyPropertyChanged("Text2");
		}
	}

	public string Text3
	{
		get
		{
			return _text3;
		}
		set
		{
			_text3 = value;
			NotifyPropertyChanged("Text3");
		}
	}

	public string DataValue
	{
		get
		{
			return _dataValue;
		}
		set
		{
			_dataValue = value;
			NotifyPropertyChanged("DataValue");
		}
	}

	public string Text1HL
	{
		get
		{
			return _text1HL;
		}
		set
		{
			_text1HL = value;
			NotifyPropertyChanged("Text1HL");
		}
	}

	public string Text3HL
	{
		get
		{
			return _text3HL;
		}
		set
		{
			_text3HL = value;
			NotifyPropertyChanged("Text3HL");
		}
	}

	public Visibility BorderVisibility
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _borderVisibility;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_borderVisibility = value;
			NotifyPropertyChanged("BorderVisibility");
		}
	}

	public string GlobalTextFlow
	{
		get
		{
			return MainViewModel.Instance.GlobalTextFlow;
		}
		set
		{
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

	public DelegateCommand ButtonScenarioBuildingAvailToggleCommand { get; set; }

	public DelegateCommand ButtonScenarioEventActionCommand { get; set; }

	public DelegateCommand ButtonScenarioEventConditionCommand { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public ScenarioEditorRow(HUD_Scenario _parent)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		parent = _parent;
		ButtonScenarioBuildingAvailToggleCommand = new DelegateCommand(ButtonScenarioBuildingAvailToggle);
		ButtonScenarioEventActionCommand = new DelegateCommand(ButtonScenarioEventActionFunc);
		ButtonScenarioEventConditionCommand = new DelegateCommand(ButtonScenarioEventConditionFunc);
	}

	public void ButtonScenarioBuildingAvailToggle(object parameter)
	{
		parent.ButtonScenarioBuildingAvailToggle(parameter);
	}

	public void ButtonScenarioEventActionFunc(object parameter)
	{
		parent.EventActionSelected(int.Parse((string)parameter, Director.defaultCulture));
	}

	public void ButtonScenarioEventConditionFunc(object parameter)
	{
		parent.EventConditionSelected(int.Parse((string)parameter, Director.defaultCulture));
	}
}
