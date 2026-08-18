using System;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class HUD_MeritPanel : UserControl
{
	private int sortBy;

	private bool sortReversed;

	private DateTime delayedSpeech = DateTime.MinValue;

	private DateTime lastPopulation = DateTime.MinValue;

	private int[] ranking = new int[9];

	public HUD_MeritPanel()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDMeritPanel = this;
	}

	public static void Open(bool state)
	{
		MainViewModel.Instance.AlliesPanelVisible = false;
		MainViewModel.Instance.MeritPanelVisible = state;
		if (state)
		{
			MainViewModel.Instance.HUDMeritPanel.Open();
		}
	}

	private void Open()
	{
		sortBy = 0;
		sortReversed = false;
		MainViewModel.Instance.MPChatVisible = false;
		populateList(force: true, initial: true);
		SFXManager.instance.playGenieSpeech(3, "GenieDE121.wav", 1f);
		delayedSpeech = DateTime.UtcNow.AddSeconds(10.0);
	}

	public void Update()
	{
		int[,] array = populateList();
		if (array != null && delayedSpeech != DateTime.MinValue && delayedSpeech < DateTime.UtcNow)
		{
			delayedSpeech = DateTime.MinValue;
			playGenieSpeech(array);
		}
	}

	private void playGenieSpeech(int[,] scores)
	{
		System.Random random = new System.Random();
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.game_time < 12000)
		{
			return;
		}
		int[] array = new int[9];
		int[,] array2 = new int[9, 5];
		for (int i = 0; i < 5; i++)
		{
			for (int j = 0; j < 9; j++)
			{
				array2[j, i] = scores[j, i];
			}
		}
		for (int k = 1; k < 9; k++)
		{
			int num = -2;
			int num2 = -2;
			int num3 = -2;
			for (int l = 0; l < 9; l++)
			{
				if (array2[l, 0] != 0 && (num == -1 || array2[l, 1] >= num2) && (num == -1 || array2[l, 1] != num2 || array2[l, 2] > num3))
				{
					num = l;
					num2 = array2[l, 1];
					num3 = array2[l, 2];
				}
			}
			if (num < 0)
			{
				break;
			}
			array[k - 1] = array2[num, 0];
			array2[num, 0] = 0;
		}
		int num4 = 0;
		for (int k = 0; k < 8; k++)
		{
			if (array[k] == EditorDirector.instance.ActivePlayerID)
			{
				num4 = k;
				break;
			}
		}
		switch (num4)
		{
		case 0:
			num4 = 0;
			break;
		case 1:
		case 2:
			num4 = 1;
			break;
		case 3:
		case 4:
			num4 = 2;
			break;
		case 5:
		case 6:
		case 7:
			num4 = 3;
			break;
		}
		int num5 = random.Next(4);
		switch (num4)
		{
		case 0:
			switch (num5)
			{
			case 0:
				SFXManager.instance.playGenieSpeech(3, "GenieDE129.wav", 1f);
				break;
			case 1:
				SFXManager.instance.playGenieSpeech(3, "GenieDE130.wav", 1f);
				break;
			case 2:
				SFXManager.instance.playGenieSpeech(3, "GenieDE131.wav", 1f);
				break;
			case 3:
				SFXManager.instance.playGenieSpeech(3, "GenieDE132.wav", 1f);
				break;
			}
			break;
		case 1:
			switch (num5)
			{
			case 0:
				SFXManager.instance.playGenieSpeech(3, "GenieDE133.wav", 1f);
				break;
			case 1:
				SFXManager.instance.playGenieSpeech(3, "GenieDE134.wav", 1f);
				break;
			case 2:
				SFXManager.instance.playGenieSpeech(3, "GenieDE135.wav", 1f);
				break;
			case 3:
				SFXManager.instance.playGenieSpeech(3, "GenieDE136.wav", 1f);
				break;
			}
			break;
		case 2:
			switch (num5)
			{
			case 0:
				SFXManager.instance.playGenieSpeech(3, "GenieDE137.wav", 1f);
				break;
			case 1:
				SFXManager.instance.playGenieSpeech(3, "GenieDE138.wav", 1f);
				break;
			case 2:
				SFXManager.instance.playGenieSpeech(3, "GenieDE139.wav", 1f);
				break;
			case 3:
				SFXManager.instance.playGenieSpeech(3, "GenieDE140.wav", 1f);
				break;
			}
			break;
		case 3:
			switch (num5)
			{
			case 0:
				SFXManager.instance.playGenieSpeech(3, "GenieDE141.wav", 1f);
				break;
			case 1:
				SFXManager.instance.playGenieSpeech(3, "GenieDE142.wav", 1f);
				break;
			case 2:
				SFXManager.instance.playGenieSpeech(3, "GenieDE143.wav", 1f);
				break;
			case 3:
				SFXManager.instance.playGenieSpeech(3, "GenieDE144.wav", 1f);
				break;
			}
			break;
		}
	}

	private int[,] populateList(bool force = false, bool initial = false)
	{
		if (!force && (DateTime.Now - lastPopulation).TotalSeconds < 1.0)
		{
			return null;
		}
		lastPopulation = DateTime.Now;
		int[,] meritData = EngineInterface.GetMeritData();
		for (int i = 0; i < 9; i++)
		{
			ranking[i] = 0;
		}
		switch (sortBy)
		{
		case 0:
		{
			MainViewModel.Instance.MeritSortText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MP_RANK, 1);
			for (int l = 1; l < 9; l++)
			{
				int num4 = -2;
				int num5 = -2;
				int num6 = -2;
				for (int m = 0; m < 9; m++)
				{
					if (meritData[m, 0] != 0 && (num4 == -1 || meritData[m, 1] >= num5) && (num4 == -1 || meritData[m, 1] != num5 || meritData[m, 2] > num6))
					{
						num4 = m;
						num5 = meritData[m, 1];
						num6 = meritData[m, 2];
					}
				}
				if (num4 < 0)
				{
					break;
				}
				ranking[l - 1] = meritData[num4, 0];
				meritData[num4, 0] = 0;
			}
			break;
		}
		case 1:
		{
			MainViewModel.Instance.MeritSortText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MP_RANK, 2);
			for (int n = 1; n < 9; n++)
			{
				int num7 = -1;
				int num8 = -1;
				int num9 = -1;
				for (int num10 = 0; num10 < 9; num10++)
				{
					if (meritData[num10, 0] != 0 && (num7 == -1 || meritData[num10, 2] >= num8) && (num7 == -1 || meritData[num10, 2] != num8 || meritData[num10, 3] > num9))
					{
						num7 = num10;
						num8 = meritData[num10, 2];
						num9 = meritData[num10, 3];
					}
				}
				if (num7 < 0)
				{
					break;
				}
				ranking[n - 1] = meritData[num7, 0];
				meritData[num7, 0] = 0;
			}
			break;
		}
		case 2:
		{
			MainViewModel.Instance.MeritSortText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MP_RANK, 3);
			for (int j = 1; j < 9; j++)
			{
				int num = -1;
				int num2 = -1;
				int num3 = -1;
				for (int k = 0; k < 9; k++)
				{
					if (meritData[k, 0] != 0 && (num == -1 || meritData[k, 3] >= num2) && (num == -1 || meritData[k, 3] != num2 || meritData[k, 2] > num3))
					{
						num = k;
						num2 = meritData[k, 3];
						num3 = meritData[k, 2];
					}
				}
				if (num < 0)
				{
					break;
				}
				ranking[j - 1] = meritData[num, 0];
				meritData[num, 0] = 0;
			}
			break;
		}
		}
		if (sortReversed)
		{
			int num11 = 0;
			for (int num12 = 0; num12 < 8; num12++)
			{
				if (ranking[num12] > 0)
				{
					num11++;
				}
			}
			if (num11 > 1)
			{
				int[] array = new int[num11];
				for (int num13 = 0; num13 < num11; num13++)
				{
					array[num11 - num13 - 1] = ranking[num13];
				}
				for (int num14 = 0; num14 < num11; num14++)
				{
					ranking[num14] = array[num14];
				}
			}
		}
		for (int num15 = 0; num15 < 8; num15++)
		{
			if (ranking[num15] <= 0)
			{
				MainViewModel.Instance.MeritLordName[num15 + 1] = "";
				MainViewModel.Instance.MeritGold[num15 + 1] = "";
				MainViewModel.Instance.MeritTroops[num15 + 1] = "";
				MainViewModel.Instance.MeritKeepVis[num15 + 1] = false;
				switch (num15)
				{
				case 0:
					MainViewModel.Instance.MeritAlly1 = null;
					break;
				case 1:
					MainViewModel.Instance.MeritAlly2 = null;
					break;
				case 2:
					MainViewModel.Instance.MeritAlly3 = null;
					break;
				case 3:
					MainViewModel.Instance.MeritAlly4 = null;
					break;
				case 4:
					MainViewModel.Instance.MeritAlly5 = null;
					break;
				case 5:
					MainViewModel.Instance.MeritAlly6 = null;
					break;
				case 6:
					MainViewModel.Instance.MeritAlly7 = null;
					break;
				case 7:
					MainViewModel.Instance.MeritAlly8 = null;
					break;
				}
				continue;
			}
			int num16 = ranking[num15];
			MainViewModel.Instance.MeritLordName[num15 + 1] = Platform_Multiplayer.Instance.getSkirmishName(num16);
			MainViewModel.Instance.MeritGold[num15 + 1] = meritData[num16, 2].ToString();
			MainViewModel.Instance.MeritTroops[num15 + 1] = meritData[num16, 3].ToString();
			MainViewModel.Instance.MeritKeepVis[num15 + 1] = meritData[num16, 1] >= 0;
			UnityEngine.Color color = GameMap.Lighten(OnScreenText.Instance.MPTeamColours[SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(num16)]], 0.3f);
			ImageSource imageSource = ((meritData[num16, 1] < 0) ? MainViewModel.Instance.GameSprites[692] : MainViewModel.Instance.getTeamAlliesShield(meritData[num16, 4], large: false));
			ImageSource teamKeepImage = MainViewModel.Instance.getTeamKeepImage(num16);
			ImageSource teamKeepOverImage = MainViewModel.Instance.getTeamKeepOverImage(num16);
			switch (num15)
			{
			case 0:
				MainViewModel.Instance.MeritAlly1 = imageSource;
				MainViewModel.Instance.MeritKeep1 = teamKeepImage;
				MainViewModel.Instance.MeritKeepOver1 = teamKeepOverImage;
				MainViewModel.Instance.MeritLordColour1 = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
				break;
			case 1:
				MainViewModel.Instance.MeritAlly2 = imageSource;
				MainViewModel.Instance.MeritKeep2 = teamKeepImage;
				MainViewModel.Instance.MeritKeepOver2 = teamKeepOverImage;
				MainViewModel.Instance.MeritLordColour2 = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
				break;
			case 2:
				MainViewModel.Instance.MeritAlly3 = imageSource;
				MainViewModel.Instance.MeritKeep3 = teamKeepImage;
				MainViewModel.Instance.MeritKeepOver3 = teamKeepOverImage;
				MainViewModel.Instance.MeritLordColour3 = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
				break;
			case 3:
				MainViewModel.Instance.MeritAlly4 = imageSource;
				MainViewModel.Instance.MeritKeep4 = teamKeepImage;
				MainViewModel.Instance.MeritKeepOver4 = teamKeepOverImage;
				MainViewModel.Instance.MeritLordColour4 = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
				break;
			case 4:
				MainViewModel.Instance.MeritAlly5 = imageSource;
				MainViewModel.Instance.MeritKeep5 = teamKeepImage;
				MainViewModel.Instance.MeritKeepOver5 = teamKeepOverImage;
				MainViewModel.Instance.MeritLordColour5 = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
				break;
			case 5:
				MainViewModel.Instance.MeritAlly6 = imageSource;
				MainViewModel.Instance.MeritKeep6 = teamKeepImage;
				MainViewModel.Instance.MeritKeepOver6 = teamKeepOverImage;
				MainViewModel.Instance.MeritLordColour6 = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
				break;
			case 6:
				MainViewModel.Instance.MeritAlly7 = imageSource;
				MainViewModel.Instance.MeritKeep7 = teamKeepImage;
				MainViewModel.Instance.MeritKeepOver7 = teamKeepOverImage;
				MainViewModel.Instance.MeritLordColour7 = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
				break;
			case 7:
				MainViewModel.Instance.MeritAlly8 = imageSource;
				MainViewModel.Instance.MeritKeep8 = teamKeepImage;
				MainViewModel.Instance.MeritKeepOver8 = teamKeepOverImage;
				MainViewModel.Instance.MeritLordColour8 = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
				break;
			}
		}
		return meritData;
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "Lord":
			if (sortBy != 0)
			{
				sortBy = 0;
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			populateList(force: true);
			break;
		case "Gold":
			if (sortBy != 1)
			{
				sortBy = 1;
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			populateList(force: true);
			break;
		case "Troops":
			if (sortBy != 2)
			{
				sortBy = 2;
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			populateList(force: true);
			break;
		case "Exit":
			Open(state: false);
			break;
		case "1":
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep, ranking[0]);
			break;
		case "2":
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep, ranking[1]);
			break;
		case "3":
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep, ranking[2]);
			break;
		case "4":
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep, ranking[3]);
			break;
		case "5":
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep, ranking[4]);
			break;
		case "6":
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep, ranking[5]);
			break;
		case "7":
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep, ranking[6]);
			break;
		case "8":
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep, ranking[7]);
			break;
		}
	}

	private void MouseEnterRolloverHandler(object sender, MouseEventArgs e)
	{
		if (e.Source is Button && ((Button)e.Source).CommandParameter != null && ((Button)e.Source).CommandParameter is string)
		{
			_ = (string)((Button)e.Source).CommandParameter == "Orders";
		}
	}

	private void MouseLeaveRolloverHandler(object sender, MouseEventArgs e)
	{
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_MeritPanel.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "MouseEnterRolloverHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MouseEnterRolloverHandler;
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveRolloverHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseLeave += MouseLeaveRolloverHandler;
			}
			return true;
		}
		return false;
	}
}
