using Noesis;

namespace CrusaderDE;

public class HUD_MPConnectionIssue : UserControl
{
	private bool multiplayerConnectionErrorKick = true;

	private int multiplayerConnectionErrorPlayerID = -1;

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

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_MPConnectionIssue.xaml");
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
