using System.ComponentModel;
using Noesis;

namespace CrusaderDE;

public class FileRow : INotifyPropertyChanged
{
	public string _text1;

	public string _text2;

	public string _text3;

	public string _text4 = "";

	public ImageSource _typeImage;

	public ImageSource _balancedImage;

	public FileHeader fileHeader;

	public Platform_Multiplayer.MPLobby lobby;

	public CustomisationFileManager.CustomLord lord;

	public CustomisationFileManager.CustomAIV aiv;

	public CustomisationFileManager.CustomLordConfig lordConfig;

	public MapFileManager.CustomTrailInfo trail;

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

	public string Text4
	{
		get
		{
			return _text4;
		}
		set
		{
			_text4 = value;
			NotifyPropertyChanged("Text4");
		}
	}

	public ImageSource TypeImage
	{
		get
		{
			return _typeImage;
		}
		set
		{
			_typeImage = value;
			NotifyPropertyChanged("TypeImage");
		}
	}

	public ImageSource BalancedImage
	{
		get
		{
			return _balancedImage;
		}
		set
		{
			_balancedImage = value;
			NotifyPropertyChanged("BalancedImage");
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

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
