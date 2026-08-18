using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class Platform_Workshop
{
	internal struct SteamWorkshopItem
	{
		public string ContentFolderPath;

		public string Description;

		public string PreviewImagePath;

		public string[] Tags;

		public string Title;

		public bool PublicVisible;
	}

	private static readonly Platform_Workshop instance;

	public const bool WorkshopSupport = true;

	private PublishedFileId_t publishedFileID;

	private SteamWorkshopItem currentSteamWorkshopItem;

	private Action successfulUploadAction;

	private Action failurefulUploadAction;

	private bool updatingExistingMap;

	private string existingMapName = "";

	private string existingDescription = "";

	private int existingDifficulty = 1;

	private bool existingBalanced;

	public static Platform_Workshop Instance => instance;

	static Platform_Workshop()
	{
		instance = new Platform_Workshop();
	}

	private Platform_Workshop()
	{
	}

	public List<string> GetListOfSubscribedItemsPaths()
	{
		PublishedFileId_t[] array = new PublishedFileId_t[SteamUGC.GetNumSubscribedItems()];
		SteamUGC.GetSubscribedItems(array, (uint)array.Length);
		List<string> list = new List<string>();
		PublishedFileId_t[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			SteamUGC.GetItemInstallInfo(array2[i], out var _, out var pchFolder, 1024u, out var _);
			list.Add(pchFolder);
		}
		return list;
	}

	public void importMetaData(ulong publishID, string mapName, int difficulty, string description, bool balanced)
	{
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
			SteamAPICall_t hAPICall = SteamUGC.CreateItem(SteamManager.AppID, EWorkshopFileType.k_EWorkshopFileTypeFirst);
			CallResult<CreateItemResult_t>.Create().Set(hAPICall, CreateItemResult);
		}
		else
		{
			UpdateItem();
		}
	}

	private void CreateItemResult(CreateItemResult_t param, bool bIOFailure)
	{
		if (param.m_eResult == EResult.k_EResultOK)
		{
			publishedFileID = param.m_nPublishedFileId;
			UpdateItem();
			return;
		}
		Debug.Log("Couldn't create a new item : " + param.m_eResult);
		if (failurefulUploadAction != null)
		{
			failurefulUploadAction();
		}
	}

	private void UpdateItem()
	{
		UGCUpdateHandle_t uGCUpdateHandle_t = SteamUGC.StartItemUpdate(SteamManager.AppID, publishedFileID);
		SteamUGC.SetItemTitle(uGCUpdateHandle_t, currentSteamWorkshopItem.Title);
		SteamUGC.SetItemDescription(uGCUpdateHandle_t, currentSteamWorkshopItem.Description);
		SteamUGC.SetItemContent(uGCUpdateHandle_t, currentSteamWorkshopItem.ContentFolderPath.Replace('\\', '/'));
		SteamUGC.SetItemTags(uGCUpdateHandle_t, currentSteamWorkshopItem.Tags);
		SteamUGC.SetItemPreview(uGCUpdateHandle_t, currentSteamWorkshopItem.PreviewImagePath.Replace('\\', '/'));
		if (currentSteamWorkshopItem.PublicVisible)
		{
			SteamUGC.SetItemVisibility(uGCUpdateHandle_t, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic);
		}
		else
		{
			SteamUGC.SetItemVisibility(uGCUpdateHandle_t, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly);
		}
		SteamAPICall_t hAPICall = SteamUGC.SubmitItemUpdate(uGCUpdateHandle_t, "");
		CallResult<SubmitItemUpdateResult_t>.Create().Set(hAPICall, UpdateItemResult);
	}

	private void UpdateItemResult(SubmitItemUpdateResult_t param, bool bIOFailure)
	{
		if (param.m_eResult == EResult.k_EResultOK)
		{
			SFXManager.instance.playAdditionalSpeech("Workshop_Publish_1.wav");
			SteamFriends.ActivateGameOverlayToWebPage("steam://url/CommunityFilePage/" + param.m_nPublishedFileId.m_PublishedFileId);
			Platform_Achievements.Instance.SetAchievementComplete(Enums.Achievements.Map_Uploaded_To_Workshop);
			if (successfulUploadAction != null)
			{
				successfulUploadAction();
			}
		}
		else if (param.m_eResult == EResult.k_EResultFileNotFound && updatingExistingMap)
		{
			updatingExistingMap = false;
			UploadWorkshopMap(currentSteamWorkshopItem.ContentFolderPath, currentSteamWorkshopItem.Title, currentSteamWorkshopItem.Description, currentSteamWorkshopItem.Tags, currentSteamWorkshopItem.PublicVisible, currentSteamWorkshopItem.PreviewImagePath, successfulUploadAction, failurefulUploadAction);
		}
		else
		{
			Debug.Log("Couldn't submit the item to Steam + " + param.m_eResult);
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
