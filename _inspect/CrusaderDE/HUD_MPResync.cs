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

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_MPResync.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			return true;
		}
		return false;
	}
}
