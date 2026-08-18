using System;
using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_MPChatMessages : UserControl
{
	public class IngameChatMessage
	{
		public DateTime expiry;

		public string fromName;

		public int fromPlayerID;

		public string message;
	}

	public Queue<IngameChatMessage> chatMessages = new Queue<IngameChatMessage>();

	public Queue<IngameChatMessage> chatMessagesCache = new Queue<IngameChatMessage>();

	public DateTime multiplayerChatMessageDisplayPostGameDecay = DateTime.MinValue;

	public static readonly int[] MP_orig_remap_colour_order = new int[9] { 0, 1, 3, 4, 2, 6, 5, 7, 8 };

	public static readonly Color[] MPTeamColours = (Color[])(object)new Color[9]
	{
		Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
		Color.FromArgb(byte.MaxValue, (byte)196, (byte)2, (byte)2),
		Color.FromArgb(byte.MaxValue, (byte)70, (byte)70, (byte)200),
		Color.FromArgb(byte.MaxValue, (byte)200, (byte)97, (byte)6),
		Color.FromArgb(byte.MaxValue, (byte)198, (byte)195, (byte)0),
		Color.FromArgb(byte.MaxValue, (byte)144, (byte)0, (byte)144),
		Color.FromArgb(byte.MaxValue, (byte)128, (byte)128, (byte)128),
		Color.FromArgb(byte.MaxValue, (byte)9, (byte)193, (byte)191),
		Color.FromArgb(byte.MaxValue, (byte)2, (byte)200, (byte)2)
	};

	public HUD_MPChatMessages()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDMPChatMessages = this;
	}

	public void recieveIngameChat(string fromName, int fromPlayerID, string message, int duration = 20)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Expected O, but got Unknown
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
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
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

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_MPChatMessages.xaml");
	}
}
