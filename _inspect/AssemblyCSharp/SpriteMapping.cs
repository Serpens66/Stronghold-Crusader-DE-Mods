using System;
using UnityEngine;

public class SpriteMapping
{
	public static int[] mpLoadRemapping = null;

	public static int[] remapColours = new int[9];

	public static int[] defaultRemapColours = new int[9];

	public static readonly int[] shcRemapColours = new int[9] { 0, 1, 3, 4, 2, 6, 5, 7, 8 };

	public static void setOpenTileGraphic(GameMapTile tile)
	{
		if (tile != null)
		{
			tile.tileImage = spriteLoader.instance.GetSprite("XXXSH1 Big Tile");
		}
	}

	public static void setGenericBuildingTileGraphic(GameMapTile tile, int file, int image, int light)
	{
		if (tile == null)
		{
			return;
		}
		tile.light = 0;
		bool flag = false;
		switch (file)
		{
		case 5:
			if (image < 64)
			{
				int num = 1;
				if (image >= 1 && image <= 32)
				{
					num = 1;
				}
				else if (image >= 33 && image <= 64)
				{
					num = 5;
				}
				int num2 = image - num;
				int imageID3 = num2 % 4 + num;
				tile.light = CalcLightValueForSea(num2 / 4);
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID3);
				flag = true;
			}
			else if (image < 150)
			{
				int imageID4 = 1;
				if (image >= 65 && image <= 72)
				{
					imageID4 = 9;
					tile.light = CalcLightValueForSea(image - 65);
				}
				else if (image >= 73 && image <= 80)
				{
					imageID4 = 10;
					tile.light = CalcLightValueForSea(image - 73);
				}
				else if (image >= 81 && image <= 88)
				{
					imageID4 = 11;
					tile.light = CalcLightValueForSea(image - 81);
				}
				else if (image >= 89 && image <= 96)
				{
					imageID4 = 12;
					tile.light = CalcLightValueForSea(image - 89);
				}
				else if (image >= 97 && image <= 104)
				{
					imageID4 = 13;
					tile.light = CalcLightValueForSea(image - 97);
				}
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID4);
				flag = true;
			}
			else if (image >= 205 && image <= 332)
			{
				int num3 = 205;
				if (image >= 205 && image <= 236)
				{
					num3 = 205;
				}
				else if (image >= 237 && image <= 268)
				{
					num3 = 237;
				}
				else if (image >= 269 && image <= 300)
				{
					num3 = 269;
				}
				else if (image >= 301 && image <= 332)
				{
					num3 = 301;
				}
				int num4 = image - num3;
				int imageID5 = num4 % 4 + num3;
				tile.light = CalcLightValueForSea(num4 / 4);
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID5);
				flag = true;
			}
			else if (image >= 333 && image <= 340)
			{
				tile.light = CalcLightValueForSea(image - 333);
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, 333);
				flag = true;
			}
			else if (image >= 533 && image <= 660)
			{
				int num5 = 533;
				if (image >= 533 && image <= 564)
				{
					num5 = 533;
				}
				else if (image >= 565 && image <= 596)
				{
					num5 = 565;
				}
				else if (image >= 597 && image <= 628)
				{
					num5 = 597;
				}
				else if (image >= 629 && image <= 660)
				{
					num5 = 629;
				}
				int num6 = image - num5;
				int imageID6 = num6 % 4 + num5;
				tile.light = CalcLightValueForSea(num6 / 4);
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID6);
				flag = true;
			}
			else if (image >= 661 && image <= 668)
			{
				tile.light = CalcLightValueForSea(image - 661);
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, 661);
				flag = true;
			}
			else if (image >= 669 && image <= 732)
			{
				int num7 = 669;
				int num8 = image - num7;
				int imageID7 = num8 % 8 + num7;
				tile.light = CalcLightValueForSea(num8 / 8);
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID7);
				flag = true;
			}
			else if (image >= 733 && image <= 764)
			{
				int num9 = 733;
				int num10 = image - num9;
				int imageID8 = num10 % 4 + num9;
				tile.light = CalcLightValueForSea(num10 / 4);
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID8);
				flag = true;
			}
			else if (image >= 1805)
			{
				int imageID9 = (image - 1805) % 60 + 1805;
				int origValue = (image - 1805) / 60;
				tile.light = CalcLightValueForSea(origValue);
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID9);
				flag = true;
			}
			else
			{
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
				flag = true;
			}
			break;
		case 6:
			if (image >= 3 && image <= 4)
			{
				image = 1;
			}
			if (image >= 7 && image <= 8)
			{
				image = 5;
			}
			if ((image >= 24 && image <= 197) || (image >= 297 && image <= 305))
			{
				tile.light = Math.Max(0, light - 2) + 10;
			}
			tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
			flag = true;
			break;
		case 38:
			switch (image)
			{
			case 320:
				tile.tileImage = spriteLoader.instance.getDebugLogicTileSprite(6);
				break;
			case 321:
				tile.tileImage = null;
				break;
			default:
				tile.tileImage = spriteLoader.instance.getDebugLogicTileSprite(1);
				break;
			}
			flag = true;
			break;
		case 15:
		case 55:
			if (image >= 5 && image <= 8)
			{
				tile.light = Math.Max(0, light - 2) + 10;
			}
			tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
			flag = true;
			break;
		case 2:
		case 7:
		case 8:
		case 12:
		case 14:
		case 49:
		case 52:
		case 56:
		case 60:
		case 157:
			tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
			flag = true;
			break;
		case 139:
		case 140:
		case 149:
			tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
			flag = true;
			break;
		case 166:
			if (image >= 1 && image < 1477)
			{
				int imageID = (image - 1) % 246 + 1;
				tile.light = (image - 1) / 246;
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID);
			}
			else if (image >= 2168 && image < 7064)
			{
				int imageID2 = (image - 2168) % 816 + 2168;
				tile.light = (image - 2168) / 816;
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, imageID2);
			}
			else
			{
				tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
			}
			flag = true;
			break;
		case 10:
			flag = true;
			tile.tileImage = spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
			break;
		default:
			setOpenTileGraphic(tile);
			tile.light = light;
			flag = true;
			break;
		}
		if (!flag)
		{
			tile.tileImage = spriteLoader.instance.GetSprite("Debug Logic 006");
			tile.light = light;
		}
	}

	private static int CalcLightValueForSea(int origValue)
	{
		if (origValue <= 2)
		{
			return 50;
		}
		return origValue - 3 + 51;
	}

	public static Sprite getChevronImage(GameMapTile tile, int file, int image)
	{
		switch (file)
		{
		case 4:
			return null;
		case 5:
			return null;
		case 3:
		case 9:
		case 10:
			return spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
		default:
			return null;
		}
	}

	public static void SetBodySprite(SpriteRenderer renderer, int file, int image, int colour, bool altFrame, int chopFeet, int transparency)
	{
		Sprite bodyImage = getBodyImage(file, image, altFrame);
		renderer.sprite = bodyImage;
		int colourOffset = colour;
		if ((uint)(file - 29) > 2u && (uint)(file - 70) > 2u && file != 97 && colour >= 0 && colour <= 8)
		{
			colourOffset = remapColours[colour];
		}
		renderer.sharedMaterial = spriteLoader.instance.GetGMMaterial((Enums.GM)file, colourOffset, chopFeet, out var outputColour, transparency);
		renderer.color = outputColour;
	}

	public static Sprite getBodyImage(int file, int image, bool altFrame = false)
	{
		switch (file)
		{
		case 17:
		case 18:
		case 19:
		case 20:
		case 21:
		case 23:
		case 25:
		case 26:
		case 27:
		case 32:
		case 33:
		case 34:
		case 35:
		case 36:
		case 37:
		case 39:
		case 40:
		case 41:
		case 42:
		case 43:
		case 44:
		case 47:
		case 51:
		case 53:
		case 58:
		case 59:
		case 61:
		case 62:
		case 63:
		case 64:
		case 65:
		case 66:
		case 67:
		case 68:
		case 69:
		case 73:
		case 74:
		case 75:
		case 76:
		case 77:
		case 78:
		case 81:
		case 82:
		case 83:
		case 84:
		case 86:
		case 88:
		case 89:
		case 90:
		case 91:
		case 92:
		case 93:
		case 94:
		case 95:
		case 96:
		case 98:
		case 99:
		case 100:
		case 101:
		case 102:
		case 103:
		case 104:
		case 106:
		case 107:
		case 109:
		case 113:
		case 114:
		case 115:
		case 118:
		case 119:
		case 120:
		case 121:
		case 122:
		case 123:
		case 124:
		case 125:
		case 126:
		case 127:
		case 128:
		case 129:
		case 130:
		case 131:
		case 132:
		case 133:
		case 134:
		case 135:
		case 137:
		case 138:
		case 141:
		case 144:
		case 145:
		case 147:
		case 154:
		case 155:
		case 159:
		case 160:
		case 161:
		case 162:
		case 163:
		case 168:
		case 169:
		case 170:
		case 171:
		case 172:
		case 173:
		case 175:
		case 176:
		case 178:
		case 179:
		case 180:
		case 181:
		case 182:
		case 185:
		case 186:
		case 187:
		case 188:
		case 189:
		case 190:
		case 191:
		case 192:
		case 193:
		case 194:
		case 195:
		case 196:
		case 197:
		case 199:
		case 201:
		case 202:
		case 203:
		case 205:
		case 208:
		case 209:
		case 210:
		case 211:
		case 212:
		case 213:
		case 214:
		case 215:
		case 219:
		case 220:
		case 221:
		case 222:
		case 224:
		case 225:
		case 226:
		case 227:
		case 228:
		case 229:
		case 230:
		case 231:
		case 232:
		case 233:
			return spriteLoader.instance.GetGMSprite((Enums.GM)file, image - 1, altFrame);
		case 85:
			if (image <= 101)
			{
				return spriteLoader.instance.GetGMSprite((Enums.GM)file, image - 1, altFrame);
			}
			return spriteLoader.instance.GetGMSprite(Enums.GM.GM_FLOAT_POP_CIRC_2, image - 202 - 1, altFrame);
		case 16:
		case 22:
		case 28:
		case 54:
		case 105:
		case 116:
		case 136:
		case 158:
		case 198:
			return spriteLoader.instance.GetGMSprite((Enums.GM)file, image, altFrame);
		case 167:
			if (image >= 145)
			{
				image = 29;
			}
			return spriteLoader.instance.GetGMSprite((Enums.GM)file, image - 1, altFrame);
		case 223:
			if (image >= 209)
			{
				image = 29;
			}
			return spriteLoader.instance.GetGMSprite((Enums.GM)file, image - 1, altFrame);
		case 29:
		case 30:
		case 31:
		case 70:
		case 71:
		case 72:
		case 97:
		case 200:
			return spriteLoader.instance.GetGMSprite((Enums.GM)file, image - 1, altFrame);
		case 207:
			switch (image)
			{
			case 1:
				return spriteLoader.instance.GetSprite("Marker  001");
			case 2:
				return spriteLoader.instance.GetSprite("Marker  002");
			case 3:
				return spriteLoader.instance.GetSprite("Marker  003");
			case 4:
				return spriteLoader.instance.GetSprite("Marker  004");
			case 5:
				return spriteLoader.instance.GetSprite("Marker  005");
			case 6:
				return spriteLoader.instance.GetSprite("Marker  006");
			case 7:
				return spriteLoader.instance.GetSprite("Marker  007");
			case 8:
				return spriteLoader.instance.GetSprite("Marker  008");
			case 9:
				return spriteLoader.instance.GetSprite("Marker  009");
			case 10:
				return spriteLoader.instance.GetSprite("Marker  010");
			}
			break;
		case 6:
		case 7:
		case 8:
		case 49:
		case 52:
			return spriteLoader.instance.GetGMSprite((Enums.GM)file, image);
		}
		return spriteLoader.instance.GetSprite("Marker  015");
	}

	public static void setDebugTileGraphic(GameMapTile tile, int value)
	{
		if (tile == null)
		{
			return;
		}
		if (value >= 0)
		{
			value++;
			if (value < 1)
			{
				tile.debugTileImage = spriteLoader.instance.GetSprite("Debug Logic 009");
				return;
			}
			if (value > 100)
			{
				value = 100;
			}
			string text;
			if (value < 10)
			{
				text = "00" + value;
			}
			else if (value == 100)
			{
				text = "100";
			}
			else
			{
				if (value >= 255)
				{
					tile.debugTileImage = spriteLoader.instance.GetSprite("Debug Logic 010");
					return;
				}
				text = "0" + value;
			}
			tile.debugTileImage = spriteLoader.instance.GetSprite("Debug Num " + text);
		}
		else
		{
			tile.debugTileImage = null;
		}
	}

	public static void BuildPlayerColourMapping(int playerColour, bool force = false)
	{
		if (GameData.Instance.game_type == 3 && !force)
		{
			playerColour = 1;
		}
		if (playerColour != 1)
		{
			int num = remapColours[1];
			remapColours[1] = remapColours[playerColour];
			remapColours[playerColour] = num;
		}
	}

	public static void BuildMultiPlayerColourMapping()
	{
		mpLoadRemapping = null;
		int[] array = new int[9];
		for (int i = 1; i < 9; i++)
		{
			Platform_Multiplayer.MPGameMember player = Platform_Multiplayer.Instance.getPlayer(i);
			if (player != null)
			{
				array[i] = remapColours[player.colourID];
			}
		}
		for (int j = 1; j < 9; j++)
		{
			remapColours[j] = array[j];
		}
		EngineInterface.SetMPRadarColours(remapColours);
	}

	public static void BuildMultiPlayerColourMapping(int[] loadRemapping)
	{
		mpLoadRemapping = loadRemapping;
		int[] array = new int[9];
		for (int i = 1; i < 9; i++)
		{
			Platform_Multiplayer.MPGameMember player = Platform_Multiplayer.Instance.getPlayer(loadRemapping[i]);
			if (player != null)
			{
				array[i] = defaultRemapColours[player.colourID];
			}
		}
		for (int j = 1; j < 9; j++)
		{
			remapColours[j] = array[j];
		}
		EngineInterface.SetMPRadarColours(remapColours);
	}

	public static void BuildSkirmishLoadColourMapping(int[] loadRemapping, int playerColour)
	{
		mpLoadRemapping = null;
		for (int i = 1; i < 9; i++)
		{
			remapColours[i] = loadRemapping[i];
		}
		EngineInterface.SetMPRadarColours(remapColours);
		Platform_Multiplayer.Instance.createLoadedSkirmishMembers(remapColours);
		int num = shcRemapColours[playerColour];
		if (remapColours[1] == num)
		{
			return;
		}
		for (int j = 2; j < 9; j++)
		{
			if (remapColours[j] == num)
			{
				remapColours[j] = remapColours[1];
				break;
			}
		}
		remapColours[1] = num;
	}

	public static void SetRemapColours(int[] remap)
	{
		for (int i = 0; i < 9; i++)
		{
			remapColours[i] = remap[i];
			defaultRemapColours[i] = remap[i];
		}
	}

	public static int RemapMPLoadedColour(int playerID)
	{
		if (mpLoadRemapping == null || playerID < 0 || playerID >= mpLoadRemapping.Length)
		{
			return playerID;
		}
		return mpLoadRemapping[playerID];
	}
}
