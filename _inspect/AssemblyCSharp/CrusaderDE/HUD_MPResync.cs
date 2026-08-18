using Noesis;

namespace CrusaderDE;

public class HUD_MPResync : UserControl
{
	public HUD_MPResync()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDMPResync = this;
	}

	public void ShowResyncing(string message, int progress)
	{
		MainViewModel.Instance.Show_HUD_MPResync = message != "";
		MainViewModel.Instance.MP_ResyncInfo = message;
		MainViewModel.Instance.MP_ResyncProgress = (progress * 4).ToString();
	}

	public void Update()
	{
		if (Platform_Multiplayer.Instance.resyncing)
		{
			MainViewModel.Instance.Show_HUD_MPResync = true;
			string mP_ResyncInfo = "";
			MainViewModel.Instance.MP_ResyncInfo = mP_ResyncInfo;
			MainViewModel.Instance.MP_ResyncProgress = (GameData.Instance.lastGameState.resyncPercent * 4).ToString();
		}
		else
		{
			MainViewModel.Instance.Show_HUD_MPResync = false;
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_MPResync.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			return true;
		}
		return false;
	}
}
