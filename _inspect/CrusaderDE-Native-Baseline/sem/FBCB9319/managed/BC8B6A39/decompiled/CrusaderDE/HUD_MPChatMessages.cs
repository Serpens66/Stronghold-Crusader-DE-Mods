using System;
using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_MPChatMessages : UserControl
{
	private class IngameChatMessage
	{
		public DateTime expiry;

		public string fromName;

		public int fromPlayerID;

		public string message;
	}

	private Queue<IngameChatMessage> chatMessages = new Queue<IngameChatMessage>();

	private Queue<IngameChatMessage> chatMessagesCache = new Queue<IngameChatMessage>();

	private DateTime multiplayerChatMessageDisplayPostGameDecay = DateTime.MinValue;

	public static readonly int[] MP_orig_remap_colour_order = new int[9] { 0, 1, 3, 4, 2, 6, 5, 7, 8 };

	public static readonly Color[] MPTeamColours = new Color[9]
	{
		Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
		Color.FromArgb(byte.MaxValue, 196, 2, 2),
		Color.FromArgb(byte.MaxValue, 70, 70, 200),
		Color.FromArgb(byte.MaxValue, 200, 97, 6),
		Color.FromArgb(byte.MaxValue, 198, 195, 0),
		Color.FromArgb(byte.MaxValue, 144, 0, 144),
		Color.FromArgb(byte.MaxValue, 128, 128, 128),
		Color.FromArgb(byte.MaxValue, 9, 193, 191),
		Color.FromArgb(byte.MaxValue, 2, 200, 2)
	};

	public HUD_MPChatMessages()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDMPChatMessages = this;
	}

	public void recieveIngameChat(string fromName, int fromPlayerID, string message, int duration = 20)
	{
		if (chatMessages.Count > 4)
		{
			chatMessages.Dequeue();
		}
		IngameChatMessage ingameChatMessage = new IngameChatMessage();
		ingameChatMessage.fromName = fromName;
		ingameChatMessage.fromPlayerID = fromPlayerID;
		ingameChatMessage.message = message;
		ingameChatMessage.expiry = DateTime.UtcNow.AddSeconds(duration);
		chatMessages.Enqueue(ingameChatMessage);
		if (chatMessagesCache.Count > 4)
		{
			chatMessagesCache.Dequeue();
		}
		chatMessagesCache.Enqueue(ingameChatMessage);
		int num = 5 - chatMessages.Count;
		foreach (IngameChatMessage chatMessage in chatMessages)
		{
			int num2 = SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(chatMessage.fromPlayerID)];
			SolidColorBrush value = new SolidColorBrush(MPTeamColours[num2]);
			MainViewModel.Instance.MPChat_Colours[num] = value;
			MainViewModel.Instance.MPChat_Names[num] = chatMessage.fromName;
			MainViewModel.Instance.MPChat_Text[num] = chatMessage.message;
			MainViewModel.Instance.MPChat_Rows[num] = true;
			num++;
		}
		MainViewModel.Instance.MPChat_Size = chatMessages.Count * 27;
		MainViewModel.Instance.Show_HUD_MPChatMessages = true;
	}

	public void OpenChatPanel()
	{
		if (chatMessagesCache.Count <= 0)
		{
			return;
		}
		chatMessages.Clear();
		chatMessages = new Queue<IngameChatMessage>(chatMessagesCache);
		int num = 5 - chatMessages.Count;
		foreach (IngameChatMessage chatMessage in chatMessages)
		{
			int num2 = SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(chatMessage.fromPlayerID)];
			SolidColorBrush value = new SolidColorBrush(MPTeamColours[num2]);
			MainViewModel.Instance.MPChat_Colours[num] = value;
			MainViewModel.Instance.MPChat_Names[num] = chatMessage.fromName;
			MainViewModel.Instance.MPChat_Text[num] = chatMessage.message;
			MainViewModel.Instance.MPChat_Rows[num] = true;
			num++;
		}
		MainViewModel.Instance.MPChat_Size = chatMessages.Count * 27;
		MainViewModel.Instance.Show_HUD_MPChatMessages = true;
	}

	public void Update()
	{
		if (chatMessages.Count > 0 && chatMessages.Peek().expiry < DateTime.UtcNow && !MainViewModel.Instance.MPChatVisible)
		{
			chatMessages.Dequeue();
			if (chatMessages.Count == 0)
			{
				ClearMPChat(clearCache: false);
				return;
			}
			int index = 4 - chatMessages.Count;
			MainViewModel.Instance.MPChat_Rows[index] = false;
			MainViewModel.Instance.MPChat_Names[index] = "";
			MainViewModel.Instance.MPChat_Text[index] = "";
			MainViewModel.Instance.MPChat_Size = chatMessages.Count * 27;
		}
	}

	public void ClearMPChat(bool clearCache = true)
	{
		MainViewModel.Instance.Show_HUD_MPChatMessages = false;
		for (int i = 0; i < 5; i++)
		{
			MainViewModel.Instance.MPChat_Rows[i] = false;
		}
		if (clearCache)
		{
			chatMessages.Clear();
			chatMessagesCache.Clear();
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_MPChatMessages.xaml");
	}
}
