using System;
using CrusaderDE;

public class FileHeader
{
	public DateTime created;

	public DateTime written;

	public uint crc;

	public int length;

	public int headerID;

	public int radarMapCompressedSize;

	public int missionTextType;

	public int missionTextNumber;

	public string ansiMissionText;

	public string unicodeMissionText;

	public string utf8MissionText;

	public bool showAlternateMissionTextForBriefing;

	public int xPlaySaveTime;

	public int xPlaySaveChecksum;

	public int xPlayAutoSave;

	public int mapType;

	public int[] mapKeeps;

	public int maxPlayers;

	public int scnMissionType;

	public int scnMissionSiegeOrInvasion;

	public int missionLockType;

	public string standAlone_filename = "";

	public string display_filename;

	public string mission_description = "";

	public string fileName;

	public string filePath;

	public string sortFileName;

	public int isKingOfTheHill;

	public bool skirmishMap;

	public bool missionMap;

	public int mission_level;

	public int inv_or_eco;

	public bool balanced;

	public bool classicSave = true;

	public int world_size = -1;

	public int chimps_limit = 3000;

	public int flies_limit = 3000;

	public bool extreme_powers_available;

	public bool hasOutposts;

	public int hostileAnimals;

	public int coopTrailID;

	public int coopMissionID;

	public int[,] keep_locations = new int[8, 2];

	public bool userMap;

	public bool workshopMap;

	public bool builtinMap;

	public bool customTrailMap;

	public int trail;

	public int trailID;

	public int achFood;

	public int achWood;

	public int achWeapons;

	public static bool AllowLockedEditing;

	public HUD_IngameMenu.RestartMapInfo restartInfo;

	public bool hasRestartSkirmishInfo;

	public HUD_IngameMenu.RestartSkirmishMapInfo restartSkirmishInfo;

	public bool hasRestartMPInfo;

	public HUD_IngameMenu.RestartMPInfo restartMPInfo;

	public string typeString = "";

	public int retrieveCRCChecks;

	public bool rowVisible;

	public bool isMapEditable()
	{
		if (AllowLockedEditing)
		{
			return true;
		}
		if (!missionMap)
		{
			return missionLockType == 0;
		}
		return false;
	}

	public string getDateString()
	{
		if (FatControler.arabic)
		{
			return written.ToString("yyyy/MM/dd") + " " + written.ToShortTimeString();
		}
		return written.ToShortDateString() + " " + written.ToShortTimeString();
	}

	public string getGameTypeString()
	{
		return typeString;
	}

	public void setGameTypeString()
	{
		if (classicSave)
		{
			typeString = "?";
		}
		else if (coopTrailID > 0)
		{
			typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, 0) + " " + coopTrailID + " : " + (coopMissionID + 1);
		}
		else if (mission_level >= 0)
		{
			if (mission_level <= 35)
			{
				int num = (mission_level - 1) / 5;
				int num2 = mission_level - num * 5;
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 13) + " " + (num + 1) + " : " + num2;
			}
			else
			{
				int num3 = mission_level / 10 - 3;
				int num4 = mission_level % 10;
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 30) + " " + num3 + "-" + num4;
			}
		}
		else if (mapType == 1)
		{
			if (!skirmishMap)
			{
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 9);
				return;
			}
			switch (trail)
			{
			case 0:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 194);
				break;
			case 1:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 195) + " " + trailID;
				break;
			case 2:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 196) + " " + trailID + " (" + (trailID + 50) + ")";
				break;
			case 3:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 215) + " " + trailID;
				break;
			case 12:
			case 13:
			case 14:
			case 15:
			case 16:
			case 17:
			case 18:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 0) + " " + (trail - 11) + " : " + trailID;
				break;
			default:
				typeString = trail.ToString();
				break;
			}
		}
		else
		{
			switch (scnMissionSiegeOrInvasion)
			{
			case 3:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 4);
				break;
			case 2:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 5);
				break;
			case 1:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 542);
				break;
			case 0:
				typeString = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 7);
				break;
			}
		}
	}

	public FileHeader()
	{
		mapKeeps = new int[5];
		headerID = 0;
	}
}
