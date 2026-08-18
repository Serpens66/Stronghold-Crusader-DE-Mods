using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;

namespace LorrdyAISharesGold.UI;

public class LobbySettingsViewModel : LobbyModSettingsBaseViewModel
{
	private bool _modEnabled = true;

	private bool _showMessage = false;

	private int _minGoldToShare = 20000;

	private int _maxGoldToGet = 1000;

	private int _goldAmountToShare = 2000;

	[SyncHostOnly]
	public bool ModEnabled
	{
		get
		{
			return _modEnabled;
		}
		set
		{
			_modEnabled = value;
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("ModEnabled");
		}
	}

	[SyncHostOnly]
	public bool ShowMessage
	{
		get
		{
			return _showMessage;
		}
		set
		{
			_showMessage = value;
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("ShowMessage");
		}
	}

	[SyncHostOnly]
	public int MinGoldToShare
	{
		get
		{
			return _minGoldToShare;
		}
		set
		{
			_minGoldToShare = value;
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("MinGoldToShare");
		}
	}

	[SyncHostOnly]
	public int MaxGoldToGet
	{
		get
		{
			return _maxGoldToGet;
		}
		set
		{
			_maxGoldToGet = value;
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("MaxGoldToGet");
		}
	}

	[SyncHostOnly]
	public int GoldAmountToShare
	{
		get
		{
			return _goldAmountToShare;
		}
		set
		{
			_goldAmountToShare = value;
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("GoldAmountToShare");
		}
	}
}
