using UnityEngine;

namespace Vectrosity;

public class VectorChar
{
	public const int numberOfCharacters = 256;

	public static Vector2[][] points;

	public static Vector2[][] data
	{
		get
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_012f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_0156: Unknown result type (might be due to invalid IL or missing references)
			//IL_015b: Unknown result type (might be due to invalid IL or missing references)
			//IL_016c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0171: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_0187: Unknown result type (might be due to invalid IL or missing references)
			//IL_0198: Unknown result type (might be due to invalid IL or missing references)
			//IL_019d: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0204: Unknown result type (might be due to invalid IL or missing references)
			//IL_0215: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Unknown result type (might be due to invalid IL or missing references)
			//IL_022b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0230: Unknown result type (might be due to invalid IL or missing references)
			//IL_0241: Unknown result type (might be due to invalid IL or missing references)
			//IL_0246: Unknown result type (might be due to invalid IL or missing references)
			//IL_0257: Unknown result type (might be due to invalid IL or missing references)
			//IL_025c: Unknown result type (might be due to invalid IL or missing references)
			//IL_026d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0272: Unknown result type (might be due to invalid IL or missing references)
			//IL_0284: Unknown result type (might be due to invalid IL or missing references)
			//IL_0289: Unknown result type (might be due to invalid IL or missing references)
			//IL_029b: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_030e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0313: Unknown result type (might be due to invalid IL or missing references)
			//IL_0325: Unknown result type (might be due to invalid IL or missing references)
			//IL_032a: Unknown result type (might be due to invalid IL or missing references)
			//IL_033c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0341: Unknown result type (might be due to invalid IL or missing references)
			//IL_0361: Unknown result type (might be due to invalid IL or missing references)
			//IL_0366: Unknown result type (might be due to invalid IL or missing references)
			//IL_0377: Unknown result type (might be due to invalid IL or missing references)
			//IL_037c: Unknown result type (might be due to invalid IL or missing references)
			//IL_038d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0392: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_03be: Unknown result type (might be due to invalid IL or missing references)
			//IL_03cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0400: Unknown result type (might be due to invalid IL or missing references)
			//IL_0411: Unknown result type (might be due to invalid IL or missing references)
			//IL_0416: Unknown result type (might be due to invalid IL or missing references)
			//IL_0428: Unknown result type (might be due to invalid IL or missing references)
			//IL_042d: Unknown result type (might be due to invalid IL or missing references)
			//IL_043f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0444: Unknown result type (might be due to invalid IL or missing references)
			//IL_0456: Unknown result type (might be due to invalid IL or missing references)
			//IL_045b: Unknown result type (might be due to invalid IL or missing references)
			//IL_046d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0472: Unknown result type (might be due to invalid IL or missing references)
			//IL_0484: Unknown result type (might be due to invalid IL or missing references)
			//IL_0489: Unknown result type (might be due to invalid IL or missing references)
			//IL_049b: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_04db: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0510: Unknown result type (might be due to invalid IL or missing references)
			//IL_0515: Unknown result type (might be due to invalid IL or missing references)
			//IL_0526: Unknown result type (might be due to invalid IL or missing references)
			//IL_052b: Unknown result type (might be due to invalid IL or missing references)
			//IL_053c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0541: Unknown result type (might be due to invalid IL or missing references)
			//IL_0552: Unknown result type (might be due to invalid IL or missing references)
			//IL_0557: Unknown result type (might be due to invalid IL or missing references)
			//IL_0568: Unknown result type (might be due to invalid IL or missing references)
			//IL_056d: Unknown result type (might be due to invalid IL or missing references)
			//IL_057e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0583: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_05b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_05bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_05d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_05fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0610: Unknown result type (might be due to invalid IL or missing references)
			//IL_0615: Unknown result type (might be due to invalid IL or missing references)
			//IL_0634: Unknown result type (might be due to invalid IL or missing references)
			//IL_0639: Unknown result type (might be due to invalid IL or missing references)
			//IL_064a: Unknown result type (might be due to invalid IL or missing references)
			//IL_064f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0660: Unknown result type (might be due to invalid IL or missing references)
			//IL_0665: Unknown result type (might be due to invalid IL or missing references)
			//IL_0676: Unknown result type (might be due to invalid IL or missing references)
			//IL_067b: Unknown result type (might be due to invalid IL or missing references)
			//IL_068c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0691: Unknown result type (might be due to invalid IL or missing references)
			//IL_06a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_06a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_06b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_06bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_06ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_06d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_06f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_06f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0708: Unknown result type (might be due to invalid IL or missing references)
			//IL_070d: Unknown result type (might be due to invalid IL or missing references)
			//IL_071e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0723: Unknown result type (might be due to invalid IL or missing references)
			//IL_0734: Unknown result type (might be due to invalid IL or missing references)
			//IL_0739: Unknown result type (might be due to invalid IL or missing references)
			//IL_0758: Unknown result type (might be due to invalid IL or missing references)
			//IL_075d: Unknown result type (might be due to invalid IL or missing references)
			//IL_076e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0773: Unknown result type (might be due to invalid IL or missing references)
			//IL_0792: Unknown result type (might be due to invalid IL or missing references)
			//IL_0797: Unknown result type (might be due to invalid IL or missing references)
			//IL_07a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_07ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_07cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_07d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_07e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_07e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0806: Unknown result type (might be due to invalid IL or missing references)
			//IL_080b: Unknown result type (might be due to invalid IL or missing references)
			//IL_081c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0821: Unknown result type (might be due to invalid IL or missing references)
			//IL_0840: Unknown result type (might be due to invalid IL or missing references)
			//IL_0845: Unknown result type (might be due to invalid IL or missing references)
			//IL_0856: Unknown result type (might be due to invalid IL or missing references)
			//IL_085b: Unknown result type (might be due to invalid IL or missing references)
			//IL_086c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0871: Unknown result type (might be due to invalid IL or missing references)
			//IL_0882: Unknown result type (might be due to invalid IL or missing references)
			//IL_0887: Unknown result type (might be due to invalid IL or missing references)
			//IL_0898: Unknown result type (might be due to invalid IL or missing references)
			//IL_089d: Unknown result type (might be due to invalid IL or missing references)
			//IL_08ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_08b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_08c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_08c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_08da: Unknown result type (might be due to invalid IL or missing references)
			//IL_08df: Unknown result type (might be due to invalid IL or missing references)
			//IL_08fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0903: Unknown result type (might be due to invalid IL or missing references)
			//IL_0914: Unknown result type (might be due to invalid IL or missing references)
			//IL_0919: Unknown result type (might be due to invalid IL or missing references)
			//IL_0939: Unknown result type (might be due to invalid IL or missing references)
			//IL_093e: Unknown result type (might be due to invalid IL or missing references)
			//IL_094f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0954: Unknown result type (might be due to invalid IL or missing references)
			//IL_0965: Unknown result type (might be due to invalid IL or missing references)
			//IL_096a: Unknown result type (might be due to invalid IL or missing references)
			//IL_097b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0980: Unknown result type (might be due to invalid IL or missing references)
			//IL_0991: Unknown result type (might be due to invalid IL or missing references)
			//IL_0996: Unknown result type (might be due to invalid IL or missing references)
			//IL_09a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_09ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_09bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_09c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_09d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_09d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_09e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_09ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a00: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a05: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a24: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a29: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a3a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a3f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a50: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a55: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a66: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a6b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a7c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a81: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a92: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a97: Unknown result type (might be due to invalid IL or missing references)
			//IL_0aa8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0aad: Unknown result type (might be due to invalid IL or missing references)
			//IL_0abe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ac3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ae2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ae7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0af8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0afd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b0e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b13: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b24: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b29: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b3a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b3f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b50: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b55: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b75: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b7a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b8b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b90: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ba1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ba6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bb7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bbc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bcd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bd2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0be3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0be8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bf9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bfe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c0f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c14: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c25: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c2a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c3c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c41: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c61: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c66: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c77: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c7c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c8d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c92: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ca3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ca8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cb9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cbe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ccf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cd4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ce5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cea: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cfb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d00: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d11: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d16: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d28: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d2d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d4c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d51: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d62: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d67: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d78: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d7d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d8e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0d93: Unknown result type (might be due to invalid IL or missing references)
			//IL_0db3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0db8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0dc9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0dce: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ddf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0de4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0df5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0dfa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e0b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e10: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e21: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e26: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e37: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e3c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e4d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e52: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e63: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e68: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e7a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e7f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0e9f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ea4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0eb5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0eba: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ecb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ed0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ee1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ee6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ef7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0efc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f0d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f12: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f23: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f28: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f39: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f3e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f4f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f54: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f66: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f6b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f8a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0f8f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0fa0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0fa5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0fb6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0fbb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0fcc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0fd1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ff0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ff5: Unknown result type (might be due to invalid IL or missing references)
			//IL_1006: Unknown result type (might be due to invalid IL or missing references)
			//IL_100b: Unknown result type (might be due to invalid IL or missing references)
			//IL_101c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1021: Unknown result type (might be due to invalid IL or missing references)
			//IL_1032: Unknown result type (might be due to invalid IL or missing references)
			//IL_1037: Unknown result type (might be due to invalid IL or missing references)
			//IL_1056: Unknown result type (might be due to invalid IL or missing references)
			//IL_105b: Unknown result type (might be due to invalid IL or missing references)
			//IL_106c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1071: Unknown result type (might be due to invalid IL or missing references)
			//IL_1082: Unknown result type (might be due to invalid IL or missing references)
			//IL_1087: Unknown result type (might be due to invalid IL or missing references)
			//IL_1098: Unknown result type (might be due to invalid IL or missing references)
			//IL_109d: Unknown result type (might be due to invalid IL or missing references)
			//IL_10bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_10c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_10d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_10d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_10e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_10ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_10fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_1103: Unknown result type (might be due to invalid IL or missing references)
			//IL_1122: Unknown result type (might be due to invalid IL or missing references)
			//IL_1127: Unknown result type (might be due to invalid IL or missing references)
			//IL_1138: Unknown result type (might be due to invalid IL or missing references)
			//IL_113d: Unknown result type (might be due to invalid IL or missing references)
			//IL_114e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1153: Unknown result type (might be due to invalid IL or missing references)
			//IL_1164: Unknown result type (might be due to invalid IL or missing references)
			//IL_1169: Unknown result type (might be due to invalid IL or missing references)
			//IL_1189: Unknown result type (might be due to invalid IL or missing references)
			//IL_118e: Unknown result type (might be due to invalid IL or missing references)
			//IL_119f: Unknown result type (might be due to invalid IL or missing references)
			//IL_11a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_11b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_11ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_11cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_11d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_11e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_11e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_11f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_11fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_120d: Unknown result type (might be due to invalid IL or missing references)
			//IL_1212: Unknown result type (might be due to invalid IL or missing references)
			//IL_1223: Unknown result type (might be due to invalid IL or missing references)
			//IL_1228: Unknown result type (might be due to invalid IL or missing references)
			//IL_1239: Unknown result type (might be due to invalid IL or missing references)
			//IL_123e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1250: Unknown result type (might be due to invalid IL or missing references)
			//IL_1255: Unknown result type (might be due to invalid IL or missing references)
			//IL_1275: Unknown result type (might be due to invalid IL or missing references)
			//IL_127a: Unknown result type (might be due to invalid IL or missing references)
			//IL_128b: Unknown result type (might be due to invalid IL or missing references)
			//IL_1290: Unknown result type (might be due to invalid IL or missing references)
			//IL_12a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_12a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_12b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_12bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_12cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_12d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_12e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_12e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_12f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_12fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_130f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1314: Unknown result type (might be due to invalid IL or missing references)
			//IL_1325: Unknown result type (might be due to invalid IL or missing references)
			//IL_132a: Unknown result type (might be due to invalid IL or missing references)
			//IL_133c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1341: Unknown result type (might be due to invalid IL or missing references)
			//IL_1361: Unknown result type (might be due to invalid IL or missing references)
			//IL_1366: Unknown result type (might be due to invalid IL or missing references)
			//IL_1377: Unknown result type (might be due to invalid IL or missing references)
			//IL_137c: Unknown result type (might be due to invalid IL or missing references)
			//IL_138d: Unknown result type (might be due to invalid IL or missing references)
			//IL_1392: Unknown result type (might be due to invalid IL or missing references)
			//IL_13a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_13a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_13b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_13be: Unknown result type (might be due to invalid IL or missing references)
			//IL_13cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_13d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_13e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_13ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_13fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_1400: Unknown result type (might be due to invalid IL or missing references)
			//IL_1411: Unknown result type (might be due to invalid IL or missing references)
			//IL_1416: Unknown result type (might be due to invalid IL or missing references)
			//IL_1428: Unknown result type (might be due to invalid IL or missing references)
			//IL_142d: Unknown result type (might be due to invalid IL or missing references)
			//IL_143f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1444: Unknown result type (might be due to invalid IL or missing references)
			//IL_1456: Unknown result type (might be due to invalid IL or missing references)
			//IL_145b: Unknown result type (might be due to invalid IL or missing references)
			//IL_146d: Unknown result type (might be due to invalid IL or missing references)
			//IL_1472: Unknown result type (might be due to invalid IL or missing references)
			//IL_1484: Unknown result type (might be due to invalid IL or missing references)
			//IL_1489: Unknown result type (might be due to invalid IL or missing references)
			//IL_149b: Unknown result type (might be due to invalid IL or missing references)
			//IL_14a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_14b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_14b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_14c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_14ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_14e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_14e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_14f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_14fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_150e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1513: Unknown result type (might be due to invalid IL or missing references)
			//IL_1532: Unknown result type (might be due to invalid IL or missing references)
			//IL_1537: Unknown result type (might be due to invalid IL or missing references)
			//IL_1548: Unknown result type (might be due to invalid IL or missing references)
			//IL_154d: Unknown result type (might be due to invalid IL or missing references)
			//IL_155e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1563: Unknown result type (might be due to invalid IL or missing references)
			//IL_1574: Unknown result type (might be due to invalid IL or missing references)
			//IL_1579: Unknown result type (might be due to invalid IL or missing references)
			//IL_158a: Unknown result type (might be due to invalid IL or missing references)
			//IL_158f: Unknown result type (might be due to invalid IL or missing references)
			//IL_15a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_15a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_15c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_15ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_15db: Unknown result type (might be due to invalid IL or missing references)
			//IL_15e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_15f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_15f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_1607: Unknown result type (might be due to invalid IL or missing references)
			//IL_160c: Unknown result type (might be due to invalid IL or missing references)
			//IL_161d: Unknown result type (might be due to invalid IL or missing references)
			//IL_1622: Unknown result type (might be due to invalid IL or missing references)
			//IL_1633: Unknown result type (might be due to invalid IL or missing references)
			//IL_1638: Unknown result type (might be due to invalid IL or missing references)
			//IL_1649: Unknown result type (might be due to invalid IL or missing references)
			//IL_164e: Unknown result type (might be due to invalid IL or missing references)
			//IL_165f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1664: Unknown result type (might be due to invalid IL or missing references)
			//IL_1675: Unknown result type (might be due to invalid IL or missing references)
			//IL_167a: Unknown result type (might be due to invalid IL or missing references)
			//IL_168c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1691: Unknown result type (might be due to invalid IL or missing references)
			//IL_16a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_16a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_16ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_16bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_16de: Unknown result type (might be due to invalid IL or missing references)
			//IL_16e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_16f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_16f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_170a: Unknown result type (might be due to invalid IL or missing references)
			//IL_170f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1720: Unknown result type (might be due to invalid IL or missing references)
			//IL_1725: Unknown result type (might be due to invalid IL or missing references)
			//IL_1736: Unknown result type (might be due to invalid IL or missing references)
			//IL_173b: Unknown result type (might be due to invalid IL or missing references)
			//IL_174c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1751: Unknown result type (might be due to invalid IL or missing references)
			//IL_1762: Unknown result type (might be due to invalid IL or missing references)
			//IL_1767: Unknown result type (might be due to invalid IL or missing references)
			//IL_1778: Unknown result type (might be due to invalid IL or missing references)
			//IL_177d: Unknown result type (might be due to invalid IL or missing references)
			//IL_179c: Unknown result type (might be due to invalid IL or missing references)
			//IL_17a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_17b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_17b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_17c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_17cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_17de: Unknown result type (might be due to invalid IL or missing references)
			//IL_17e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_17f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_17f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_180a: Unknown result type (might be due to invalid IL or missing references)
			//IL_180f: Unknown result type (might be due to invalid IL or missing references)
			//IL_182f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1834: Unknown result type (might be due to invalid IL or missing references)
			//IL_1845: Unknown result type (might be due to invalid IL or missing references)
			//IL_184a: Unknown result type (might be due to invalid IL or missing references)
			//IL_185b: Unknown result type (might be due to invalid IL or missing references)
			//IL_1860: Unknown result type (might be due to invalid IL or missing references)
			//IL_1871: Unknown result type (might be due to invalid IL or missing references)
			//IL_1876: Unknown result type (might be due to invalid IL or missing references)
			//IL_1887: Unknown result type (might be due to invalid IL or missing references)
			//IL_188c: Unknown result type (might be due to invalid IL or missing references)
			//IL_189d: Unknown result type (might be due to invalid IL or missing references)
			//IL_18a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_18b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_18b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_18c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_18ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_18df: Unknown result type (might be due to invalid IL or missing references)
			//IL_18e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_18f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_18fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_191a: Unknown result type (might be due to invalid IL or missing references)
			//IL_191f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1930: Unknown result type (might be due to invalid IL or missing references)
			//IL_1935: Unknown result type (might be due to invalid IL or missing references)
			//IL_1946: Unknown result type (might be due to invalid IL or missing references)
			//IL_194b: Unknown result type (might be due to invalid IL or missing references)
			//IL_195c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1961: Unknown result type (might be due to invalid IL or missing references)
			//IL_1972: Unknown result type (might be due to invalid IL or missing references)
			//IL_1977: Unknown result type (might be due to invalid IL or missing references)
			//IL_1988: Unknown result type (might be due to invalid IL or missing references)
			//IL_198d: Unknown result type (might be due to invalid IL or missing references)
			//IL_19ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_19b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_19c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_19c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_19d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_19dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_19ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_19f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a04: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a09: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a1a: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a1f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a3e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a43: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a54: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a59: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a6a: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a6f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a80: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a85: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a96: Unknown result type (might be due to invalid IL or missing references)
			//IL_1a9b: Unknown result type (might be due to invalid IL or missing references)
			//IL_1aac: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ab1: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ad0: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ad5: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ae6: Unknown result type (might be due to invalid IL or missing references)
			//IL_1aeb: Unknown result type (might be due to invalid IL or missing references)
			//IL_1afc: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b01: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b12: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b17: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b28: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b2d: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b3e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b43: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b62: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b67: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b78: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b7d: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b8e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1b93: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ba4: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ba9: Unknown result type (might be due to invalid IL or missing references)
			//IL_1bc8: Unknown result type (might be due to invalid IL or missing references)
			//IL_1bcd: Unknown result type (might be due to invalid IL or missing references)
			//IL_1bde: Unknown result type (might be due to invalid IL or missing references)
			//IL_1be3: Unknown result type (might be due to invalid IL or missing references)
			//IL_1bf4: Unknown result type (might be due to invalid IL or missing references)
			//IL_1bf9: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c0a: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c0f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c20: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c25: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c36: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c3b: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c4c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c51: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c62: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c67: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c86: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c8b: Unknown result type (might be due to invalid IL or missing references)
			//IL_1c9c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ca1: Unknown result type (might be due to invalid IL or missing references)
			//IL_1cb2: Unknown result type (might be due to invalid IL or missing references)
			//IL_1cb7: Unknown result type (might be due to invalid IL or missing references)
			//IL_1cc8: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ccd: Unknown result type (might be due to invalid IL or missing references)
			//IL_1cde: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ce3: Unknown result type (might be due to invalid IL or missing references)
			//IL_1cf4: Unknown result type (might be due to invalid IL or missing references)
			//IL_1cf9: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d18: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d1d: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d2e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d33: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d44: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d49: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d5a: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d5f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d70: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d75: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d86: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d8b: Unknown result type (might be due to invalid IL or missing references)
			//IL_1d9c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1da1: Unknown result type (might be due to invalid IL or missing references)
			//IL_1db2: Unknown result type (might be due to invalid IL or missing references)
			//IL_1db7: Unknown result type (might be due to invalid IL or missing references)
			//IL_1dd6: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ddb: Unknown result type (might be due to invalid IL or missing references)
			//IL_1dec: Unknown result type (might be due to invalid IL or missing references)
			//IL_1df1: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e02: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e07: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e18: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e1d: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e2e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e33: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e44: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e49: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e5a: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e5f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e70: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e75: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e95: Unknown result type (might be due to invalid IL or missing references)
			//IL_1e9a: Unknown result type (might be due to invalid IL or missing references)
			//IL_1eab: Unknown result type (might be due to invalid IL or missing references)
			//IL_1eb0: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ec1: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ec6: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ed7: Unknown result type (might be due to invalid IL or missing references)
			//IL_1edc: Unknown result type (might be due to invalid IL or missing references)
			//IL_1eed: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ef2: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f03: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f08: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f19: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f1e: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f2f: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f34: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f45: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f4a: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f5c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f61: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f81: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f86: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f97: Unknown result type (might be due to invalid IL or missing references)
			//IL_1f9c: Unknown result type (might be due to invalid IL or missing references)
			//IL_1fad: Unknown result type (might be due to invalid IL or missing references)
			//IL_1fb2: Unknown result type (might be due to invalid IL or missing references)
			//IL_1fc3: Unknown result type (might be due to invalid IL or missing references)
			//IL_1fc8: Unknown result type (might be due to invalid IL or missing references)
			//IL_1fd9: Unknown result type (might be due to invalid IL or missing references)
			//IL_1fde: Unknown result type (might be due to invalid IL or missing references)
			//IL_1fef: Unknown result type (might be due to invalid IL or missing references)
			//IL_1ff4: Unknown result type (might be due to invalid IL or missing references)
			//IL_2005: Unknown result type (might be due to invalid IL or missing references)
			//IL_200a: Unknown result type (might be due to invalid IL or missing references)
			//IL_201b: Unknown result type (might be due to invalid IL or missing references)
			//IL_2020: Unknown result type (might be due to invalid IL or missing references)
			//IL_2031: Unknown result type (might be due to invalid IL or missing references)
			//IL_2036: Unknown result type (might be due to invalid IL or missing references)
			//IL_2048: Unknown result type (might be due to invalid IL or missing references)
			//IL_204d: Unknown result type (might be due to invalid IL or missing references)
			//IL_206d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2072: Unknown result type (might be due to invalid IL or missing references)
			//IL_2083: Unknown result type (might be due to invalid IL or missing references)
			//IL_2088: Unknown result type (might be due to invalid IL or missing references)
			//IL_2099: Unknown result type (might be due to invalid IL or missing references)
			//IL_209e: Unknown result type (might be due to invalid IL or missing references)
			//IL_20af: Unknown result type (might be due to invalid IL or missing references)
			//IL_20b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_20c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_20ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_20db: Unknown result type (might be due to invalid IL or missing references)
			//IL_20e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_20f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_20f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_2107: Unknown result type (might be due to invalid IL or missing references)
			//IL_210c: Unknown result type (might be due to invalid IL or missing references)
			//IL_211d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2122: Unknown result type (might be due to invalid IL or missing references)
			//IL_2134: Unknown result type (might be due to invalid IL or missing references)
			//IL_2139: Unknown result type (might be due to invalid IL or missing references)
			//IL_2158: Unknown result type (might be due to invalid IL or missing references)
			//IL_215d: Unknown result type (might be due to invalid IL or missing references)
			//IL_216e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2173: Unknown result type (might be due to invalid IL or missing references)
			//IL_2184: Unknown result type (might be due to invalid IL or missing references)
			//IL_2189: Unknown result type (might be due to invalid IL or missing references)
			//IL_219a: Unknown result type (might be due to invalid IL or missing references)
			//IL_219f: Unknown result type (might be due to invalid IL or missing references)
			//IL_21be: Unknown result type (might be due to invalid IL or missing references)
			//IL_21c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_21d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_21d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_21ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_21ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_2200: Unknown result type (might be due to invalid IL or missing references)
			//IL_2205: Unknown result type (might be due to invalid IL or missing references)
			//IL_2216: Unknown result type (might be due to invalid IL or missing references)
			//IL_221b: Unknown result type (might be due to invalid IL or missing references)
			//IL_222c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2231: Unknown result type (might be due to invalid IL or missing references)
			//IL_2250: Unknown result type (might be due to invalid IL or missing references)
			//IL_2255: Unknown result type (might be due to invalid IL or missing references)
			//IL_2266: Unknown result type (might be due to invalid IL or missing references)
			//IL_226b: Unknown result type (might be due to invalid IL or missing references)
			//IL_227c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2281: Unknown result type (might be due to invalid IL or missing references)
			//IL_2292: Unknown result type (might be due to invalid IL or missing references)
			//IL_2297: Unknown result type (might be due to invalid IL or missing references)
			//IL_22b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_22bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_22cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_22d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_22e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_22e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_22f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_22fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_230e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2313: Unknown result type (might be due to invalid IL or missing references)
			//IL_2324: Unknown result type (might be due to invalid IL or missing references)
			//IL_2329: Unknown result type (might be due to invalid IL or missing references)
			//IL_233a: Unknown result type (might be due to invalid IL or missing references)
			//IL_233f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2350: Unknown result type (might be due to invalid IL or missing references)
			//IL_2355: Unknown result type (might be due to invalid IL or missing references)
			//IL_2374: Unknown result type (might be due to invalid IL or missing references)
			//IL_2379: Unknown result type (might be due to invalid IL or missing references)
			//IL_238a: Unknown result type (might be due to invalid IL or missing references)
			//IL_238f: Unknown result type (might be due to invalid IL or missing references)
			//IL_23a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_23a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_23b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_23bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_23da: Unknown result type (might be due to invalid IL or missing references)
			//IL_23df: Unknown result type (might be due to invalid IL or missing references)
			//IL_23f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_23f5: Unknown result type (might be due to invalid IL or missing references)
			//IL_2406: Unknown result type (might be due to invalid IL or missing references)
			//IL_240b: Unknown result type (might be due to invalid IL or missing references)
			//IL_241c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2421: Unknown result type (might be due to invalid IL or missing references)
			//IL_2432: Unknown result type (might be due to invalid IL or missing references)
			//IL_2437: Unknown result type (might be due to invalid IL or missing references)
			//IL_2448: Unknown result type (might be due to invalid IL or missing references)
			//IL_244d: Unknown result type (might be due to invalid IL or missing references)
			//IL_246c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2471: Unknown result type (might be due to invalid IL or missing references)
			//IL_2482: Unknown result type (might be due to invalid IL or missing references)
			//IL_2487: Unknown result type (might be due to invalid IL or missing references)
			//IL_2498: Unknown result type (might be due to invalid IL or missing references)
			//IL_249d: Unknown result type (might be due to invalid IL or missing references)
			//IL_24ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_24b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_24c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_24c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_24da: Unknown result type (might be due to invalid IL or missing references)
			//IL_24df: Unknown result type (might be due to invalid IL or missing references)
			//IL_24fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_2503: Unknown result type (might be due to invalid IL or missing references)
			//IL_2514: Unknown result type (might be due to invalid IL or missing references)
			//IL_2519: Unknown result type (might be due to invalid IL or missing references)
			//IL_252a: Unknown result type (might be due to invalid IL or missing references)
			//IL_252f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2540: Unknown result type (might be due to invalid IL or missing references)
			//IL_2545: Unknown result type (might be due to invalid IL or missing references)
			//IL_2556: Unknown result type (might be due to invalid IL or missing references)
			//IL_255b: Unknown result type (might be due to invalid IL or missing references)
			//IL_256c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2571: Unknown result type (might be due to invalid IL or missing references)
			//IL_2590: Unknown result type (might be due to invalid IL or missing references)
			//IL_2595: Unknown result type (might be due to invalid IL or missing references)
			//IL_25a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_25ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_25ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_25cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_25e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_25e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_25f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_25fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_260c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2611: Unknown result type (might be due to invalid IL or missing references)
			//IL_2622: Unknown result type (might be due to invalid IL or missing references)
			//IL_2627: Unknown result type (might be due to invalid IL or missing references)
			//IL_2638: Unknown result type (might be due to invalid IL or missing references)
			//IL_263d: Unknown result type (might be due to invalid IL or missing references)
			//IL_265c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2661: Unknown result type (might be due to invalid IL or missing references)
			//IL_2672: Unknown result type (might be due to invalid IL or missing references)
			//IL_2677: Unknown result type (might be due to invalid IL or missing references)
			//IL_2688: Unknown result type (might be due to invalid IL or missing references)
			//IL_268d: Unknown result type (might be due to invalid IL or missing references)
			//IL_269e: Unknown result type (might be due to invalid IL or missing references)
			//IL_26a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_26c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_26c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_26d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_26dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_26fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_2701: Unknown result type (might be due to invalid IL or missing references)
			//IL_2712: Unknown result type (might be due to invalid IL or missing references)
			//IL_2717: Unknown result type (might be due to invalid IL or missing references)
			//IL_2737: Unknown result type (might be due to invalid IL or missing references)
			//IL_273c: Unknown result type (might be due to invalid IL or missing references)
			//IL_274d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2752: Unknown result type (might be due to invalid IL or missing references)
			//IL_2763: Unknown result type (might be due to invalid IL or missing references)
			//IL_2768: Unknown result type (might be due to invalid IL or missing references)
			//IL_2779: Unknown result type (might be due to invalid IL or missing references)
			//IL_277e: Unknown result type (might be due to invalid IL or missing references)
			//IL_278f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2794: Unknown result type (might be due to invalid IL or missing references)
			//IL_27a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_27aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_27bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_27c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_27d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_27d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_27e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_27ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_27fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_2803: Unknown result type (might be due to invalid IL or missing references)
			//IL_2822: Unknown result type (might be due to invalid IL or missing references)
			//IL_2827: Unknown result type (might be due to invalid IL or missing references)
			//IL_2838: Unknown result type (might be due to invalid IL or missing references)
			//IL_283d: Unknown result type (might be due to invalid IL or missing references)
			//IL_284e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2853: Unknown result type (might be due to invalid IL or missing references)
			//IL_2864: Unknown result type (might be due to invalid IL or missing references)
			//IL_2869: Unknown result type (might be due to invalid IL or missing references)
			//IL_287a: Unknown result type (might be due to invalid IL or missing references)
			//IL_287f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2890: Unknown result type (might be due to invalid IL or missing references)
			//IL_2895: Unknown result type (might be due to invalid IL or missing references)
			//IL_28a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_28ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_28bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_28c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_28e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_28e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_28f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_28fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_290c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2911: Unknown result type (might be due to invalid IL or missing references)
			//IL_2922: Unknown result type (might be due to invalid IL or missing references)
			//IL_2927: Unknown result type (might be due to invalid IL or missing references)
			//IL_2938: Unknown result type (might be due to invalid IL or missing references)
			//IL_293d: Unknown result type (might be due to invalid IL or missing references)
			//IL_294e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2953: Unknown result type (might be due to invalid IL or missing references)
			//IL_2972: Unknown result type (might be due to invalid IL or missing references)
			//IL_2977: Unknown result type (might be due to invalid IL or missing references)
			//IL_2988: Unknown result type (might be due to invalid IL or missing references)
			//IL_298d: Unknown result type (might be due to invalid IL or missing references)
			//IL_299e: Unknown result type (might be due to invalid IL or missing references)
			//IL_29a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_29b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_29b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_29ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_29cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_29e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_29e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_29f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_29fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a0c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a11: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a31: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a36: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a47: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a4c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a5d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a62: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a73: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a78: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a89: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a8e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2a9f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2aa4: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ab5: Unknown result type (might be due to invalid IL or missing references)
			//IL_2aba: Unknown result type (might be due to invalid IL or missing references)
			//IL_2acb: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ad0: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ae1: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ae6: Unknown result type (might be due to invalid IL or missing references)
			//IL_2af8: Unknown result type (might be due to invalid IL or missing references)
			//IL_2afd: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b1c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b21: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b32: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b37: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b48: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b4d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b5e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b63: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b74: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b79: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b8a: Unknown result type (might be due to invalid IL or missing references)
			//IL_2b8f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ba0: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ba5: Unknown result type (might be due to invalid IL or missing references)
			//IL_2bb6: Unknown result type (might be due to invalid IL or missing references)
			//IL_2bbb: Unknown result type (might be due to invalid IL or missing references)
			//IL_2bdb: Unknown result type (might be due to invalid IL or missing references)
			//IL_2be0: Unknown result type (might be due to invalid IL or missing references)
			//IL_2bf1: Unknown result type (might be due to invalid IL or missing references)
			//IL_2bf6: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c07: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c0c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c1d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c22: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c33: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c38: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c49: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c4e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c5f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c64: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c75: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c7a: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c8b: Unknown result type (might be due to invalid IL or missing references)
			//IL_2c90: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ca2: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ca7: Unknown result type (might be due to invalid IL or missing references)
			//IL_2cc6: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ccb: Unknown result type (might be due to invalid IL or missing references)
			//IL_2cdc: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ce1: Unknown result type (might be due to invalid IL or missing references)
			//IL_2cf2: Unknown result type (might be due to invalid IL or missing references)
			//IL_2cf7: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d08: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d0d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d1e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d23: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d34: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d39: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d58: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d5d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d6e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d73: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d84: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d89: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d9a: Unknown result type (might be due to invalid IL or missing references)
			//IL_2d9f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2dbe: Unknown result type (might be due to invalid IL or missing references)
			//IL_2dc3: Unknown result type (might be due to invalid IL or missing references)
			//IL_2dd4: Unknown result type (might be due to invalid IL or missing references)
			//IL_2dd9: Unknown result type (might be due to invalid IL or missing references)
			//IL_2dea: Unknown result type (might be due to invalid IL or missing references)
			//IL_2def: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e00: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e05: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e16: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e1b: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e2c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e31: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e50: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e55: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e66: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e6b: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e7c: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e81: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e92: Unknown result type (might be due to invalid IL or missing references)
			//IL_2e97: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ea8: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ead: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ebe: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ec3: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ee2: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ee7: Unknown result type (might be due to invalid IL or missing references)
			//IL_2ef8: Unknown result type (might be due to invalid IL or missing references)
			//IL_2efd: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f1d: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f22: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f33: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f38: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f49: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f4e: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f5f: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f64: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f75: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f7a: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f8b: Unknown result type (might be due to invalid IL or missing references)
			//IL_2f90: Unknown result type (might be due to invalid IL or missing references)
			//IL_2fa1: Unknown result type (might be due to invalid IL or missing references)
			//IL_2fa6: Unknown result type (might be due to invalid IL or missing references)
			//IL_2fb7: Unknown result type (might be due to invalid IL or missing references)
			//IL_2fbc: Unknown result type (might be due to invalid IL or missing references)
			//IL_2fcd: Unknown result type (might be due to invalid IL or missing references)
			//IL_2fd2: Unknown result type (might be due to invalid IL or missing references)
			//IL_2fe4: Unknown result type (might be due to invalid IL or missing references)
			//IL_2fe9: Unknown result type (might be due to invalid IL or missing references)
			//IL_3008: Unknown result type (might be due to invalid IL or missing references)
			//IL_300d: Unknown result type (might be due to invalid IL or missing references)
			//IL_301e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3023: Unknown result type (might be due to invalid IL or missing references)
			//IL_3034: Unknown result type (might be due to invalid IL or missing references)
			//IL_3039: Unknown result type (might be due to invalid IL or missing references)
			//IL_304a: Unknown result type (might be due to invalid IL or missing references)
			//IL_304f: Unknown result type (might be due to invalid IL or missing references)
			//IL_3060: Unknown result type (might be due to invalid IL or missing references)
			//IL_3065: Unknown result type (might be due to invalid IL or missing references)
			//IL_3076: Unknown result type (might be due to invalid IL or missing references)
			//IL_307b: Unknown result type (might be due to invalid IL or missing references)
			//IL_308c: Unknown result type (might be due to invalid IL or missing references)
			//IL_3091: Unknown result type (might be due to invalid IL or missing references)
			//IL_30a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_30a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_30c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_30cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_30dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_30e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_30f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_30f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_3108: Unknown result type (might be due to invalid IL or missing references)
			//IL_310d: Unknown result type (might be due to invalid IL or missing references)
			//IL_311e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3123: Unknown result type (might be due to invalid IL or missing references)
			//IL_3134: Unknown result type (might be due to invalid IL or missing references)
			//IL_3139: Unknown result type (might be due to invalid IL or missing references)
			//IL_314a: Unknown result type (might be due to invalid IL or missing references)
			//IL_314f: Unknown result type (might be due to invalid IL or missing references)
			//IL_3160: Unknown result type (might be due to invalid IL or missing references)
			//IL_3165: Unknown result type (might be due to invalid IL or missing references)
			//IL_3184: Unknown result type (might be due to invalid IL or missing references)
			//IL_3189: Unknown result type (might be due to invalid IL or missing references)
			//IL_319a: Unknown result type (might be due to invalid IL or missing references)
			//IL_319f: Unknown result type (might be due to invalid IL or missing references)
			//IL_31b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_31b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_31c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_31cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_31dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_31e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_31f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_31f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_3208: Unknown result type (might be due to invalid IL or missing references)
			//IL_320d: Unknown result type (might be due to invalid IL or missing references)
			//IL_321e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3223: Unknown result type (might be due to invalid IL or missing references)
			//IL_3242: Unknown result type (might be due to invalid IL or missing references)
			//IL_3247: Unknown result type (might be due to invalid IL or missing references)
			//IL_3258: Unknown result type (might be due to invalid IL or missing references)
			//IL_325d: Unknown result type (might be due to invalid IL or missing references)
			//IL_326e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3273: Unknown result type (might be due to invalid IL or missing references)
			//IL_3284: Unknown result type (might be due to invalid IL or missing references)
			//IL_3289: Unknown result type (might be due to invalid IL or missing references)
			//IL_329a: Unknown result type (might be due to invalid IL or missing references)
			//IL_329f: Unknown result type (might be due to invalid IL or missing references)
			//IL_32b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_32b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_32c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_32cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_32dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_32e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_3300: Unknown result type (might be due to invalid IL or missing references)
			//IL_3305: Unknown result type (might be due to invalid IL or missing references)
			//IL_3316: Unknown result type (might be due to invalid IL or missing references)
			//IL_331b: Unknown result type (might be due to invalid IL or missing references)
			//IL_332c: Unknown result type (might be due to invalid IL or missing references)
			//IL_3331: Unknown result type (might be due to invalid IL or missing references)
			//IL_3342: Unknown result type (might be due to invalid IL or missing references)
			//IL_3347: Unknown result type (might be due to invalid IL or missing references)
			//IL_3358: Unknown result type (might be due to invalid IL or missing references)
			//IL_335d: Unknown result type (might be due to invalid IL or missing references)
			//IL_336e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3373: Unknown result type (might be due to invalid IL or missing references)
			//IL_3393: Unknown result type (might be due to invalid IL or missing references)
			//IL_3398: Unknown result type (might be due to invalid IL or missing references)
			//IL_33a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_33ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_33bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_33c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_33d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_33da: Unknown result type (might be due to invalid IL or missing references)
			//IL_33eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_33f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_3401: Unknown result type (might be due to invalid IL or missing references)
			//IL_3406: Unknown result type (might be due to invalid IL or missing references)
			//IL_3417: Unknown result type (might be due to invalid IL or missing references)
			//IL_341c: Unknown result type (might be due to invalid IL or missing references)
			//IL_342d: Unknown result type (might be due to invalid IL or missing references)
			//IL_3432: Unknown result type (might be due to invalid IL or missing references)
			//IL_3443: Unknown result type (might be due to invalid IL or missing references)
			//IL_3448: Unknown result type (might be due to invalid IL or missing references)
			//IL_345a: Unknown result type (might be due to invalid IL or missing references)
			//IL_345f: Unknown result type (might be due to invalid IL or missing references)
			//IL_347e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3483: Unknown result type (might be due to invalid IL or missing references)
			//IL_3494: Unknown result type (might be due to invalid IL or missing references)
			//IL_3499: Unknown result type (might be due to invalid IL or missing references)
			//IL_34aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_34af: Unknown result type (might be due to invalid IL or missing references)
			//IL_34c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_34c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_34d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_34db: Unknown result type (might be due to invalid IL or missing references)
			//IL_34ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_34f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_3510: Unknown result type (might be due to invalid IL or missing references)
			//IL_3515: Unknown result type (might be due to invalid IL or missing references)
			//IL_3526: Unknown result type (might be due to invalid IL or missing references)
			//IL_352b: Unknown result type (might be due to invalid IL or missing references)
			//IL_353c: Unknown result type (might be due to invalid IL or missing references)
			//IL_3541: Unknown result type (might be due to invalid IL or missing references)
			//IL_3552: Unknown result type (might be due to invalid IL or missing references)
			//IL_3557: Unknown result type (might be due to invalid IL or missing references)
			//IL_3568: Unknown result type (might be due to invalid IL or missing references)
			//IL_356d: Unknown result type (might be due to invalid IL or missing references)
			//IL_357e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3583: Unknown result type (might be due to invalid IL or missing references)
			//IL_35a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_35a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_35b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_35bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_35ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_35d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_35e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_35e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_3608: Unknown result type (might be due to invalid IL or missing references)
			//IL_360d: Unknown result type (might be due to invalid IL or missing references)
			//IL_361e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3623: Unknown result type (might be due to invalid IL or missing references)
			//IL_3634: Unknown result type (might be due to invalid IL or missing references)
			//IL_3639: Unknown result type (might be due to invalid IL or missing references)
			//IL_364a: Unknown result type (might be due to invalid IL or missing references)
			//IL_364f: Unknown result type (might be due to invalid IL or missing references)
			//IL_3660: Unknown result type (might be due to invalid IL or missing references)
			//IL_3665: Unknown result type (might be due to invalid IL or missing references)
			//IL_3676: Unknown result type (might be due to invalid IL or missing references)
			//IL_367b: Unknown result type (might be due to invalid IL or missing references)
			//IL_368c: Unknown result type (might be due to invalid IL or missing references)
			//IL_3691: Unknown result type (might be due to invalid IL or missing references)
			//IL_36a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_36a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_36c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_36cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_36dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_36e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_36f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_36f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_3708: Unknown result type (might be due to invalid IL or missing references)
			//IL_370d: Unknown result type (might be due to invalid IL or missing references)
			//IL_372c: Unknown result type (might be due to invalid IL or missing references)
			//IL_3731: Unknown result type (might be due to invalid IL or missing references)
			//IL_3742: Unknown result type (might be due to invalid IL or missing references)
			//IL_3747: Unknown result type (might be due to invalid IL or missing references)
			//IL_3758: Unknown result type (might be due to invalid IL or missing references)
			//IL_375d: Unknown result type (might be due to invalid IL or missing references)
			//IL_376e: Unknown result type (might be due to invalid IL or missing references)
			//IL_3773: Unknown result type (might be due to invalid IL or missing references)
			//IL_3792: Unknown result type (might be due to invalid IL or missing references)
			//IL_3797: Unknown result type (might be due to invalid IL or missing references)
			//IL_37a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_37ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_37be: Unknown result type (might be due to invalid IL or missing references)
			//IL_37c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_37d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_37d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_37ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_37ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_3800: Unknown result type (might be due to invalid IL or missing references)
			//IL_3805: Unknown result type (might be due to invalid IL or missing references)
			if (points == null)
			{
				points = new Vector2[256][];
				points[33] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, -0.9f),
					new Vector2(0f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0f, -0.75f)
				};
				points[34] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.15f, 0f),
					new Vector2(0.15f, -0.25f),
					new Vector2(0.45f, -0.25f),
					new Vector2(0.45f, 0f)
				};
				points[35] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.2f, 0f),
					new Vector2(0.2f, -1f),
					new Vector2(0f, -0.33f),
					new Vector2(0.6f, -0.33f),
					new Vector2(0.4f, 0f),
					new Vector2(0.4f, -1f),
					new Vector2(0f, -0.66f),
					new Vector2(0.6f, -0.66f)
				};
				points[37] = (Vector2[])(object)new Vector2[18]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -0.25f),
					new Vector2(0.15f, 0f),
					new Vector2(0.15f, -0.25f),
					new Vector2(0f, -0.25f),
					new Vector2(0.15f, -0.25f),
					new Vector2(0f, 0f),
					new Vector2(0.15f, 0f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.45f, -0.75f),
					new Vector2(0.6f, -1f),
					new Vector2(0.45f, -1f),
					new Vector2(0.45f, -1f),
					new Vector2(0.45f, -0.75f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f)
				};
				points[38] = (Vector2[])(object)new Vector2[16]
				{
					new Vector2(0.2f, -0.5f),
					new Vector2(0.2f, 0f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.2f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.7f),
					new Vector2(0.2f, 0f),
					new Vector2(0.5f, 0f),
					new Vector2(0.5f, -0.5f),
					new Vector2(0.5f, 0f),
					new Vector2(0f, -0.5f),
					new Vector2(0.5f, -0.5f)
				};
				points[39] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0.3f, -0.25f),
					new Vector2(0.45f, 0f)
				};
				points[40] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.45f, 0f),
					new Vector2(0.15f, -0.25f),
					new Vector2(0.15f, -0.25f),
					new Vector2(0.15f, -0.75f),
					new Vector2(0.45f, -1f),
					new Vector2(0.15f, -0.75f)
				};
				points[41] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.15f, 0f),
					new Vector2(0.45f, -0.25f),
					new Vector2(0.45f, -0.25f),
					new Vector2(0.45f, -0.75f),
					new Vector2(0.15f, -1f),
					new Vector2(0.45f, -0.75f)
				};
				points[42] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, 0f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.5f, -0.1f),
					new Vector2(0.1f, -0.9f),
					new Vector2(0.5f, -0.9f),
					new Vector2(0.1f, -0.1f)
				};
				points[43] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.3f, -0.9f),
					new Vector2(0.3f, -0.1f)
				};
				points[44] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0f, -1f),
					new Vector2(0.15f, -0.75f)
				};
				points[45] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f)
				};
				points[46] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0f, -0.9f),
					new Vector2(0f, -1f)
				};
				points[47] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, -1f)
				};
				points[48] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[49] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, 0f)
				};
				points[50] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[51] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f)
				};
				points[52] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, -0.5f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f)
				};
				points[53] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[54] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0f, -1f)
				};
				points[55] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, 0f)
				};
				points[56] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f)
				};
				points[57] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, 0f),
					new Vector2(0f, -0.5f)
				};
				points[58] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, -0.9f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.3f),
					new Vector2(0f, -0.4f)
				};
				points[59] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, -1f),
					new Vector2(0.15f, -0.75f),
					new Vector2(0.1f, -0.3f),
					new Vector2(0.1f, -0.4f)
				};
				points[60] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -0.5f)
				};
				points[61] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.6f, -0.25f),
					new Vector2(0f, -0.25f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0f, -0.75f)
				};
				points[62] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -0.5f)
				};
				points[63] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, -0.9f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.75f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.3f, 0f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0f, 0f),
					new Vector2(0.3f, 0f)
				};
				points[65] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, -1f),
					new Vector2(0f, -0.3f),
					new Vector2(0.6f, -0.3f),
					new Vector2(0.6f, -1f),
					new Vector2(0.3f, 0f),
					new Vector2(0f, -0.3f),
					new Vector2(0.3f, 0f),
					new Vector2(0.6f, -0.3f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f)
				};
				points[66] = (Vector2[])(object)new Vector2[20]
				{
					new Vector2(0f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0.447f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0.447f, 0f),
					new Vector2(0.6f, -0.155f),
					new Vector2(0.6f, -0.347f),
					new Vector2(0.6f, -0.155f),
					new Vector2(0.448f, -0.5f),
					new Vector2(0.6f, -0.347f),
					new Vector2(0.448f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.653f),
					new Vector2(0.448f, -0.5f),
					new Vector2(0.6f, -0.653f),
					new Vector2(0.6f, -0.845f),
					new Vector2(0.447f, -1f),
					new Vector2(0.6f, -0.845f),
					new Vector2(0f, -1f),
					new Vector2(0.447f, -1f)
				};
				points[67] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[68] = (Vector2[])(object)new Vector2[12]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.447f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0.447f, 0f),
					new Vector2(0.6f, -0.155f),
					new Vector2(0.6f, -0.845f),
					new Vector2(0.6f, -0.155f),
					new Vector2(0.6f, -0.845f),
					new Vector2(0.447f, -1f),
					new Vector2(0.447f, -1f),
					new Vector2(0f, -1f)
				};
				points[69] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.3f, -0.5f)
				};
				points[70] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.3f, -0.5f)
				};
				points[71] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.6f, -0.5f)
				};
				points[72] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f)
				};
				points[73] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, 0f)
				};
				points[74] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.725f)
				};
				points[75] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f)
				};
				points[76] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[77] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.6f, 0f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f)
				};
				points[78] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, 0f)
				};
				points[79] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f)
				};
				points[80] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -0.5f)
				};
				points[81] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0.3f, -0.5f)
				};
				points[82] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.15f, -0.5f),
					new Vector2(0.6f, -1f)
				};
				points[83] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[84] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, 0f)
				};
				points[85] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[86] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.3f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0.3f, -1f),
					new Vector2(0.6f, 0f)
				};
				points[87] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.6f, 0f),
					new Vector2(0.6f, -1f)
				};
				points[88] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.6f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, -1f)
				};
				points[89] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, 0f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.6f, 0f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, -0.5f)
				};
				points[90] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.6f, 0f),
					new Vector2(0f, 0f),
					new Vector2(0.6f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[91] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.4f, 0f),
					new Vector2(0.1f, 0f),
					new Vector2(0.1f, -1f),
					new Vector2(0.4f, -1f),
					new Vector2(0.1f, -1f),
					new Vector2(0.1f, 0f)
				};
				points[92] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0.6f, -1f),
					new Vector2(0f, 0f)
				};
				points[93] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.2f, 0f),
					new Vector2(0.5f, 0f),
					new Vector2(0.2f, -1f),
					new Vector2(0.5f, -1f),
					new Vector2(0.5f, 0f),
					new Vector2(0.5f, -1f)
				};
				points[94] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, -0.5f),
					new Vector2(0.3f, 0f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.3f, 0f)
				};
				points[95] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0f, -1f),
					new Vector2(0.8f, -1f)
				};
				points[96] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0.5f, -0.3f),
					new Vector2(0.3f, 0f)
				};
				points[97] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -0.75f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -0.75f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[98] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f)
				};
				points[99] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[100] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, 0f)
				};
				points[101] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0f, -0.75f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[102] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.15f, -1f),
					new Vector2(0.15f, -0.25f),
					new Vector2(0.45f, 0f),
					new Vector2(0.3f, 0f),
					new Vector2(0.15f, -0.25f),
					new Vector2(0.3f, 0f),
					new Vector2(0.45f, -0.5f),
					new Vector2(0.15f, -0.5f)
				};
				points[103] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -1.25f),
					new Vector2(0.6f, -1.25f),
					new Vector2(0.6f, -1.25f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f)
				};
				points[104] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, 0f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f)
				};
				points[105] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0.3f, -0.25f),
					new Vector2(0.3f, -0.15f)
				};
				points[106] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.3f, -0.25f),
					new Vector2(0.3f, -0.15f),
					new Vector2(0.3f, -1.25f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0f, -1.25f),
					new Vector2(0.3f, -1.25f)
				};
				points[107] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, -1f),
					new Vector2(0f, 0f),
					new Vector2(0f, -0.75f),
					new Vector2(0.3f, -0.5f),
					new Vector2(0f, -0.75f),
					new Vector2(0.6f, -1f)
				};
				points[108] = (Vector2[])(object)new Vector2[2]
				{
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, 0f)
				};
				points[109] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0.45f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.45f, -0.5f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, -0.5f)
				};
				points[110] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.45f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.45f, -0.5f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f)
				};
				points[111] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f)
				};
				points[112] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -1.25f),
					new Vector2(0f, -0.5f)
				};
				points[113] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1.25f),
					new Vector2(0.6f, -0.5f)
				};
				points[114] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.6f, -0.5f)
				};
				points[115] = (Vector2[])(object)new Vector2[10]
				{
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -0.75f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0f, -0.75f),
					new Vector2(0.6f, -0.75f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[116] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.3f, -1f),
					new Vector2(0.3f, -0.25f),
					new Vector2(0.45f, -0.5f),
					new Vector2(0.15f, -0.5f),
					new Vector2(0.3f, -1f),
					new Vector2(0.45f, -1f)
				};
				points[117] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
				points[118] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.3f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.3f, -1f),
					new Vector2(0.6f, -0.5f)
				};
				points[119] = (Vector2[])(object)new Vector2[8]
				{
					new Vector2(0.15f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0.3f, -0.75f),
					new Vector2(0.15f, -1f),
					new Vector2(0.3f, -0.75f),
					new Vector2(0.45f, -1f),
					new Vector2(0.45f, -1f),
					new Vector2(0.6f, -0.5f)
				};
				points[120] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0.6f, -1f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -0.5f)
				};
				points[121] = (Vector2[])(object)new Vector2[4]
				{
					new Vector2(0f, -1.25f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.3f, -0.875f),
					new Vector2(0f, -0.5f)
				};
				points[122] = (Vector2[])(object)new Vector2[6]
				{
					new Vector2(0.6f, -0.5f),
					new Vector2(0f, -0.5f),
					new Vector2(0f, -1f),
					new Vector2(0.6f, -0.5f),
					new Vector2(0.6f, -1f),
					new Vector2(0f, -1f)
				};
			}
			return points;
		}
	}
}
