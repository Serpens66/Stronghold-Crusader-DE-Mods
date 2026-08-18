using System.ComponentModel;
using Noesis;

namespace CrusaderDE;

public class ScenarioEditorRow : INotifyPropertyChanged
{
	private string _text1;

	private string _text2;

	private string _text3;

	private string _dataValue;

	private string _text1HL;

	private string _text3HL;

	private Visibility _borderVisibility = Visibility.Visible;

	public int DataValue2;

	private HUD_Scenario parent;

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
			return _borderVisibility;
		}
		set
		{
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

	public DelegateCommand ButtonScenarioBuildingAvailToggleCommand { get; private set; }

	public DelegateCommand ButtonScenarioEventActionCommand { get; private set; }

	public DelegateCommand ButtonScenarioEventConditionCommand { get; private set; }

	public event PropertyChangedEventHandler PropertyChanged;

	protected void NotifyPropertyChanged(string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public ScenarioEditorRow(HUD_Scenario _parent)
	{
		parent = _parent;
		ButtonScenarioBuildingAvailToggleCommand = new DelegateCommand(ButtonScenarioBuildingAvailToggle);
		ButtonScenarioEventActionCommand = new DelegateCommand(ButtonScenarioEventActionFunc);
		ButtonScenarioEventConditionCommand = new DelegateCommand(ButtonScenarioEventConditionFunc);
	}

	private void ButtonScenarioBuildingAvailToggle(object parameter)
	{
		parent.ButtonScenarioBuildingAvailToggle(parameter);
	}

	private void ButtonScenarioEventActionFunc(object parameter)
	{
		parent.EventActionSelected(int.Parse((string)parameter, Director.defaultCulture));
	}

	private void ButtonScenarioEventConditionFunc(object parameter)
	{
		parent.EventConditionSelected(int.Parse((string)parameter, Director.defaultCulture));
	}
}
