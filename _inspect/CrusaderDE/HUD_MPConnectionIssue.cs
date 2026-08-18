using Noesis;

namespace CrusaderDE;

public class HUD_MPConnectionIssue : UserControl
{
	public bool multiplayerConnectionErrorKick = true;

	public int multiplayerConnectionErrorPlayerID = -1;

	public HUD_MPConnectionIssue()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDMPConnectionIssue = this;
	}

	public void ShowMultiplayerConnectionError(string message, bool kickNotLeave, int playerID)
	{
		MainViewModel.Instance.Show_HUD_MPConnectionIssue = message != "";
		MainViewModel.Instance.MPConnectionIssueText = message;
		multiplayerConnectionErrorKick = kickNotLeave;
		multiplayerConnectionErrorPlayerID = playerID;
		if (multiplayerConnectionErrorKick)
		{
			MainViewModel.Instance.MPConnectionIssueButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 42);
		}
		else
		{
			MainViewModel.Instance.MPConnectionIssueButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 43);
		}
		MainViewModel.Instance.MPConectionIssueButtonVisible = false;
	}

	public void ButtonClicked()
	{
		if (multiplayerConnectionErrorKick)
		{
			Platform_Multiplayer.Instance.kickPlayerFromGame(multiplayerConnectionErrorPlayerID);
		}
		else
		{
			EditorDirector.instance.stopGameSim();
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_MPConnectionIssue.xaml");
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
