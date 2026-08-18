using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class Platform_Workshop
{
	public struct SteamWorkshopItem
	{
		public string ContentFolderPath;

		public string Description;

		public string PreviewImagePath;

		public string[] Tags;

		public string Title;

		public bool PublicVisible;
	}

	public static readonly Platform_Workshop instance;

	public const bool WorkshopSupport = true;

	public PublishedFileId_t publishedFileID;

	public SteamWorkshopItem currentSteamWorkshopItem;

	public Action successfulUploadAction;

	public Action failurefulUploadAction;

	public bool updatingExistingMap;

	public string existingMapName = "";

	public string existingDescription = "";

	public int existingDifficulty = 1;

	public bool existingBalanced;

	public static Platform_Workshop Instance => instance;

	static Platform_Workshop()
	{
		instance = new Platform_Workshop();
	}

	public List<string> GetListOfSubscribedItemsPaths()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		PublishedFileId_t[] array = new PublishedFileId_t[SteamUGC.GetNumSubscribedItems()];
		SteamUGC.GetSubscribedItems((PublishedFileId_t[])(object)array, (uint)array.Length);
		List<string> list = new List<string>();
		PublishedFileId_t[] array2 = (PublishedFileId_t[])(object)array;
		ulong num = default(ulong);
		string item = default(string);
		uint num2 = default(uint);
		for (int i = 0; i < array2.Length; i++)
		{
			SteamUGC.GetItemInstallInfo(array2[i], ref num, ref item, 1024u, ref num2);
			list.Add(item);
		}
		return list;
	}

	public void importMetaData(ulong publishID, string mapName, int difficulty, string description, bool balanced)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		updatingExistingMap = true;
		publishedFileID = new PublishedFileId_t(publishID);
		existingMapName = mapName;
		existingDescription = description;
		existingDifficulty = difficulty;
		existingBalanced = balanced;
	}

	public bool getMetaData(ref string mapName, ref int difficulty, ref string description, ref bool balanced)
	{
		if (updatingExistingMap)
		{
			mapName = existingMapName;
			difficulty = existingDifficulty;
			description = existingDescription;
			balanced = existingBalanced;
			return true;
		}
		return false;
	}

	public void clearMetaData()
	{
		updatingExistingMap = false;
	}

	public void UploadWorkshopMap(string nameMap, string mapTitle, string description, string[] tags, bool publicMap, string previewImage, Action successAction, Action failAction)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		currentSteamWorkshopItem = new SteamWorkshopItem
		{
			Title = mapTitle,
			Description = description,
			ContentFolderPath = nameMap,
			Tags = tags,
			PreviewImagePath = previewImage,
			PublicVisible = publicMap
		};
		successfulUploadAction = successAction;
		failurefulUploadAction = failAction;
		if (!updatingExistingMap)
		{
			SteamAPICall_t val = SteamUGC.CreateItem(SteamManager.AppID, (EWorkshopFileType)0);
			CallResult<CreateItemResult_t>.Create((APIDispatchDelegate<CreateItemResult_t>)null).Set(val, (APIDispatchDelegate<CreateItemResult_t>)CreateItemResult);
		}
		else
		{
			UpdateItem();
		}
	}

	public unsafe void CreateItemResult(CreateItemResult_t param, bool bIOFailure)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if ((int)param.m_eResult == 1)
		{
			publishedFileID = param.m_nPublishedFileId;
			UpdateItem();
			return;
		}
		Debug.Log((object)("Couldn't create a new item : " + ((object)(*(EResult*)(&param.m_eResult))/*cast due to constrained. prefix*/).ToString()));
		if (failurefulUploadAction != null)
		{
			failurefulUploadAction();
		}
	}

	public void UpdateItem()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		UGCUpdateHandle_t val = SteamUGC.StartItemUpdate(SteamManager.AppID, publishedFileID);
		SteamUGC.SetItemTitle(val, currentSteamWorkshopItem.Title);
		SteamUGC.SetItemDescription(val, currentSteamWorkshopItem.Description);
		SteamUGC.SetItemContent(val, currentSteamWorkshopItem.ContentFolderPath.Replace('\\', '/'));
		SteamUGC.SetItemTags(val, (IList<string>)currentSteamWorkshopItem.Tags, false);
		SteamUGC.SetItemPreview(val, currentSteamWorkshopItem.PreviewImagePath.Replace('\\', '/'));
		if (currentSteamWorkshopItem.PublicVisible)
		{
			SteamUGC.SetItemVisibility(val, (ERemoteStoragePublishedFileVisibility)0);
		}
		else
		{
			SteamUGC.SetItemVisibility(val, (ERemoteStoragePublishedFileVisibility)1);
		}
		SteamAPICall_t val2 = SteamUGC.SubmitItemUpdate(val, "");
		CallResult<SubmitItemUpdateResult_t>.Create((APIDispatchDelegate<SubmitItemUpdateResult_t>)null).Set(val2, (APIDispatchDelegate<SubmitItemUpdateResult_t>)UpdateItemResult);
	}

	public unsafe void UpdateItemResult(SubmitItemUpdateResult_t param, bool bIOFailure)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Invalid comparison between Unknown and I4
		if ((int)param.m_eResult == 1)
		{
			SFXManager.instance.playAdditionalSpeech("Workshop_Publish_1.wav");
			SteamFriends.ActivateGameOverlayToWebPage("steam://url/CommunityFilePage/" + param.m_nPublishedFileId.m_PublishedFileId, (EActivateGameOverlayToWebPageMode)0);
			Platform_Achievements.Instance.SetAchievementComplete(Enums.Achievements.Map_Uploaded_To_Workshop);
			if (successfulUploadAction != null)
			{
				successfulUploadAction();
			}
		}
		else if ((int)param.m_eResult == 9 && updatingExistingMap)
		{
			updatingExistingMap = false;
			UploadWorkshopMap(currentSteamWorkshopItem.ContentFolderPath, currentSteamWorkshopItem.Title, currentSteamWorkshopItem.Description, currentSteamWorkshopItem.Tags, currentSteamWorkshopItem.PublicVisible, currentSteamWorkshopItem.PreviewImagePath, successfulUploadAction, failurefulUploadAction);
		}
		else
		{
			Debug.Log((object)("Couldn't submit the item to Steam + " + ((object)(*(EResult*)(&param.m_eResult))/*cast due to constrained. prefix*/).ToString()));
			if (failurefulUploadAction != null)
			{
				failurefulUploadAction();
			}
		}
	}

	public ulong GetPublishID()
	{
		return publishedFileID.m_PublishedFileId;
	}
}
