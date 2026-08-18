using System.ComponentModel;

namespace CrusaderDE;

public class HotKeyRow : INotifyPropertyChanged
{
	public int _HotKey_ColumnWidth1 = 220;

	public int _HotKey_ColumnWidth2 = 150;

	public string _text1;

	public string _text2;

	public string _dataValue;

	public int _iDataValue;

	public HUD_Options parent;

	public int HotKey_ColumnWidth1
	{
		get
		{
			return _HotKey_ColumnWidth1;
		}
		set
		{
			if (_HotKey_ColumnWidth1 != value)
			{
				_HotKey_ColumnWidth1 = value;
				NotifyPropertyChanged("HotKey_ColumnWidth1");
			}
		}
	}

	public int HotKey_ColumnWidth2
	{
		get
		{
			return _HotKey_ColumnWidth2;
		}
		set
		{
			if (_HotKey_ColumnWidth2 != value)
			{
				_HotKey_ColumnWidth2 = value;
				NotifyPropertyChanged("HotKey_ColumnWidth2");
			}
		}
	}

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

	public int iDataValue
	{
		get
		{
			return _iDataValue;
		}
		set
		{
			_iDataValue = value;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public HotKeyRow(HUD_Options _parent, int width)
	{
		parent = _parent;
		HotKey_ColumnWidth1 = width;
		HotKey_ColumnWidth2 = 370 - width;
	}
}
