using Noesis;

namespace CrusaderDE;

public class HUD_ControlGroups : UserControl
{
	public Button[] RefSelectButtons = (Button[])(object)new Button[10];

	public Button[] RefCreateButtons = (Button[])(object)new Button[10];

	public Button[] RefAddButtons = (Button[])(object)new Button[10];

	public Button[] RefDeleteButtons = (Button[])(object)new Button[10];

	public Image[,] RefTroopImages = new Image[10, 4];

	public TextBlock[,] RefTroopValues = new TextBlock[10, 4];

	public TextBlock[] RefTroopExtraValues = (TextBlock[])(object)new TextBlock[10];

	public TextBlock[] RefTroopRowID = (TextBlock[])(object)new TextBlock[10];

	public static SolidColorBrush RowIDColour_Black = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)0, (byte)0));

	public static SolidColorBrush RowIDColour_Light = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)239, (byte)243, (byte)198));

	public HUD_ControlGroups()
	{
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected O, but got Unknown
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Expected O, but got Unknown
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected O, but got Unknown
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Expected O, but got Unknown
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Expected O, but got Unknown
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Expected O, but got Unknown
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Expected O, but got Unknown
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Expected O, but got Unknown
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Expected O, but got Unknown
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Expected O, but got Unknown
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Expected O, but got Unknown
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Expected O, but got Unknown
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Expected O, but got Unknown
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Expected O, but got Unknown
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Expected O, but got Unknown
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Expected O, but got Unknown
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Expected O, but got Unknown
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Expected O, but got Unknown
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Expected O, but got Unknown
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Expected O, but got Unknown
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Expected O, but got Unknown
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Expected O, but got Unknown
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c3: Expected O, but got Unknown
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Expected O, but got Unknown
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Expected O, but got Unknown
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Expected O, but got Unknown
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0323: Expected O, but got Unknown
		//IL_0335: Unknown result type (might be due to invalid IL or missing references)
		//IL_033b: Expected O, but got Unknown
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0354: Expected O, but got Unknown
		//IL_0366: Unknown result type (might be due to invalid IL or missing references)
		//IL_036c: Expected O, but got Unknown
		//IL_037e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0384: Expected O, but got Unknown
		//IL_0396: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Expected O, but got Unknown
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b4: Expected O, but got Unknown
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cc: Expected O, but got Unknown
		//IL_03de: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e4: Expected O, but got Unknown
		//IL_03f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fc: Expected O, but got Unknown
		//IL_040e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Expected O, but got Unknown
		//IL_0426: Unknown result type (might be due to invalid IL or missing references)
		//IL_042c: Expected O, but got Unknown
		//IL_043f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0445: Expected O, but got Unknown
		//IL_0458: Unknown result type (might be due to invalid IL or missing references)
		//IL_0462: Expected O, but got Unknown
		//IL_0475: Unknown result type (might be due to invalid IL or missing references)
		//IL_047f: Expected O, but got Unknown
		//IL_0492: Unknown result type (might be due to invalid IL or missing references)
		//IL_049c: Expected O, but got Unknown
		//IL_04af: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b9: Expected O, but got Unknown
		//IL_04cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d6: Expected O, but got Unknown
		//IL_04e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f3: Expected O, but got Unknown
		//IL_0506: Unknown result type (might be due to invalid IL or missing references)
		//IL_0510: Expected O, but got Unknown
		//IL_0523: Unknown result type (might be due to invalid IL or missing references)
		//IL_052d: Expected O, but got Unknown
		//IL_0540: Unknown result type (might be due to invalid IL or missing references)
		//IL_054a: Expected O, but got Unknown
		//IL_055d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0567: Expected O, but got Unknown
		//IL_057a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0584: Expected O, but got Unknown
		//IL_0597: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a1: Expected O, but got Unknown
		//IL_05b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_05be: Expected O, but got Unknown
		//IL_05d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05db: Expected O, but got Unknown
		//IL_05ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f8: Expected O, but got Unknown
		//IL_060b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0615: Expected O, but got Unknown
		//IL_0628: Unknown result type (might be due to invalid IL or missing references)
		//IL_0632: Expected O, but got Unknown
		//IL_0645: Unknown result type (might be due to invalid IL or missing references)
		//IL_064f: Expected O, but got Unknown
		//IL_0662: Unknown result type (might be due to invalid IL or missing references)
		//IL_066c: Expected O, but got Unknown
		//IL_067f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0689: Expected O, but got Unknown
		//IL_069c: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a6: Expected O, but got Unknown
		//IL_06b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c3: Expected O, but got Unknown
		//IL_06d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e0: Expected O, but got Unknown
		//IL_06f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_06fd: Expected O, but got Unknown
		//IL_0710: Unknown result type (might be due to invalid IL or missing references)
		//IL_071a: Expected O, but got Unknown
		//IL_072d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0737: Expected O, but got Unknown
		//IL_074a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0754: Expected O, but got Unknown
		//IL_0767: Unknown result type (might be due to invalid IL or missing references)
		//IL_0771: Expected O, but got Unknown
		//IL_0784: Unknown result type (might be due to invalid IL or missing references)
		//IL_078e: Expected O, but got Unknown
		//IL_07a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ab: Expected O, but got Unknown
		//IL_07be: Unknown result type (might be due to invalid IL or missing references)
		//IL_07c8: Expected O, but got Unknown
		//IL_07db: Unknown result type (might be due to invalid IL or missing references)
		//IL_07e5: Expected O, but got Unknown
		//IL_07f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0802: Expected O, but got Unknown
		//IL_0815: Unknown result type (might be due to invalid IL or missing references)
		//IL_081f: Expected O, but got Unknown
		//IL_0832: Unknown result type (might be due to invalid IL or missing references)
		//IL_083c: Expected O, but got Unknown
		//IL_084f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0859: Expected O, but got Unknown
		//IL_086d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0877: Expected O, but got Unknown
		//IL_088b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0895: Expected O, but got Unknown
		//IL_08a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_08b3: Expected O, but got Unknown
		//IL_08c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d1: Expected O, but got Unknown
		//IL_08e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ee: Expected O, but got Unknown
		//IL_0901: Unknown result type (might be due to invalid IL or missing references)
		//IL_090b: Expected O, but got Unknown
		//IL_091e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0928: Expected O, but got Unknown
		//IL_093b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0945: Expected O, but got Unknown
		//IL_0958: Unknown result type (might be due to invalid IL or missing references)
		//IL_0962: Expected O, but got Unknown
		//IL_0975: Unknown result type (might be due to invalid IL or missing references)
		//IL_097f: Expected O, but got Unknown
		//IL_0992: Unknown result type (might be due to invalid IL or missing references)
		//IL_099c: Expected O, but got Unknown
		//IL_09af: Unknown result type (might be due to invalid IL or missing references)
		//IL_09b9: Expected O, but got Unknown
		//IL_09cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_09d6: Expected O, but got Unknown
		//IL_09e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_09f3: Expected O, but got Unknown
		//IL_0a06: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a10: Expected O, but got Unknown
		//IL_0a23: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a2d: Expected O, but got Unknown
		//IL_0a40: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a4a: Expected O, but got Unknown
		//IL_0a5d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a67: Expected O, but got Unknown
		//IL_0a7a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a84: Expected O, but got Unknown
		//IL_0a97: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aa1: Expected O, but got Unknown
		//IL_0ab4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0abe: Expected O, but got Unknown
		//IL_0ad1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0adb: Expected O, but got Unknown
		//IL_0aee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0af8: Expected O, but got Unknown
		//IL_0b0b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b15: Expected O, but got Unknown
		//IL_0b28: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b32: Expected O, but got Unknown
		//IL_0b45: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b4f: Expected O, but got Unknown
		//IL_0b62: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b6c: Expected O, but got Unknown
		//IL_0b7f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b89: Expected O, but got Unknown
		//IL_0b9c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ba6: Expected O, but got Unknown
		//IL_0bb9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bc3: Expected O, but got Unknown
		//IL_0bd6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0be0: Expected O, but got Unknown
		//IL_0bf3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bfd: Expected O, but got Unknown
		//IL_0c10: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c1a: Expected O, but got Unknown
		//IL_0c2d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c37: Expected O, but got Unknown
		//IL_0c4a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c54: Expected O, but got Unknown
		//IL_0c67: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c71: Expected O, but got Unknown
		//IL_0c84: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c8e: Expected O, but got Unknown
		//IL_0ca1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cab: Expected O, but got Unknown
		//IL_0cbe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cc8: Expected O, but got Unknown
		//IL_0cdb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ce5: Expected O, but got Unknown
		//IL_0cf9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d03: Expected O, but got Unknown
		//IL_0d17: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d21: Expected O, but got Unknown
		//IL_0d35: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d3f: Expected O, but got Unknown
		//IL_0d53: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d5d: Expected O, but got Unknown
		//IL_0d6f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d75: Expected O, but got Unknown
		//IL_0d87: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d8d: Expected O, but got Unknown
		//IL_0d9f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0da5: Expected O, but got Unknown
		//IL_0db7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dbd: Expected O, but got Unknown
		//IL_0dcf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dd5: Expected O, but got Unknown
		//IL_0de7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ded: Expected O, but got Unknown
		//IL_0dff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e05: Expected O, but got Unknown
		//IL_0e17: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e1d: Expected O, but got Unknown
		//IL_0e2f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e35: Expected O, but got Unknown
		//IL_0e48: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e4e: Expected O, but got Unknown
		//IL_0e60: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e66: Expected O, but got Unknown
		//IL_0e78: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e7e: Expected O, but got Unknown
		//IL_0e90: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e96: Expected O, but got Unknown
		//IL_0ea8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eae: Expected O, but got Unknown
		//IL_0ec0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ec6: Expected O, but got Unknown
		//IL_0ed8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ede: Expected O, but got Unknown
		//IL_0ef0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ef6: Expected O, but got Unknown
		//IL_0f08: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f0e: Expected O, but got Unknown
		//IL_0f20: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f26: Expected O, but got Unknown
		//IL_0f39: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f3f: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDControlGroups = this;
		RefSelectButtons[0] = (Button)((FrameworkElement)this).FindName("CG0_Select");
		RefSelectButtons[1] = (Button)((FrameworkElement)this).FindName("CG1_Select");
		RefSelectButtons[2] = (Button)((FrameworkElement)this).FindName("CG2_Select");
		RefSelectButtons[3] = (Button)((FrameworkElement)this).FindName("CG3_Select");
		RefSelectButtons[4] = (Button)((FrameworkElement)this).FindName("CG4_Select");
		RefSelectButtons[5] = (Button)((FrameworkElement)this).FindName("CG5_Select");
		RefSelectButtons[6] = (Button)((FrameworkElement)this).FindName("CG6_Select");
		RefSelectButtons[7] = (Button)((FrameworkElement)this).FindName("CG7_Select");
		RefSelectButtons[8] = (Button)((FrameworkElement)this).FindName("CG8_Select");
		RefSelectButtons[9] = (Button)((FrameworkElement)this).FindName("CG9_Select");
		RefCreateButtons[0] = (Button)((FrameworkElement)this).FindName("CG0_Create");
		RefCreateButtons[1] = (Button)((FrameworkElement)this).FindName("CG1_Create");
		RefCreateButtons[2] = (Button)((FrameworkElement)this).FindName("CG2_Create");
		RefCreateButtons[3] = (Button)((FrameworkElement)this).FindName("CG3_Create");
		RefCreateButtons[4] = (Button)((FrameworkElement)this).FindName("CG4_Create");
		RefCreateButtons[5] = (Button)((FrameworkElement)this).FindName("CG5_Create");
		RefCreateButtons[6] = (Button)((FrameworkElement)this).FindName("CG6_Create");
		RefCreateButtons[7] = (Button)((FrameworkElement)this).FindName("CG7_Create");
		RefCreateButtons[8] = (Button)((FrameworkElement)this).FindName("CG8_Create");
		RefCreateButtons[9] = (Button)((FrameworkElement)this).FindName("CG9_Create");
		RefAddButtons[0] = (Button)((FrameworkElement)this).FindName("CG0_Add");
		RefAddButtons[1] = (Button)((FrameworkElement)this).FindName("CG1_Add");
		RefAddButtons[2] = (Button)((FrameworkElement)this).FindName("CG2_Add");
		RefAddButtons[3] = (Button)((FrameworkElement)this).FindName("CG3_Add");
		RefAddButtons[4] = (Button)((FrameworkElement)this).FindName("CG4_Add");
		RefAddButtons[5] = (Button)((FrameworkElement)this).FindName("CG5_Add");
		RefAddButtons[6] = (Button)((FrameworkElement)this).FindName("CG6_Add");
		RefAddButtons[7] = (Button)((FrameworkElement)this).FindName("CG7_Add");
		RefAddButtons[8] = (Button)((FrameworkElement)this).FindName("CG8_Add");
		RefAddButtons[9] = (Button)((FrameworkElement)this).FindName("CG9_Add");
		RefDeleteButtons[0] = (Button)((FrameworkElement)this).FindName("CG0_Delete");
		RefDeleteButtons[1] = (Button)((FrameworkElement)this).FindName("CG1_Delete");
		RefDeleteButtons[2] = (Button)((FrameworkElement)this).FindName("CG2_Delete");
		RefDeleteButtons[3] = (Button)((FrameworkElement)this).FindName("CG3_Delete");
		RefDeleteButtons[4] = (Button)((FrameworkElement)this).FindName("CG4_Delete");
		RefDeleteButtons[5] = (Button)((FrameworkElement)this).FindName("CG5_Delete");
		RefDeleteButtons[6] = (Button)((FrameworkElement)this).FindName("CG6_Delete");
		RefDeleteButtons[7] = (Button)((FrameworkElement)this).FindName("CG7_Delete");
		RefDeleteButtons[8] = (Button)((FrameworkElement)this).FindName("CG8_Delete");
		RefDeleteButtons[9] = (Button)((FrameworkElement)this).FindName("CG9_Delete");
		RefTroopImages[0, 0] = (Image)((FrameworkElement)this).FindName("CG0_TroopImage1");
		RefTroopImages[0, 1] = (Image)((FrameworkElement)this).FindName("CG0_TroopImage2");
		RefTroopImages[0, 2] = (Image)((FrameworkElement)this).FindName("CG0_TroopImage3");
		RefTroopImages[0, 3] = (Image)((FrameworkElement)this).FindName("CG0_TroopImage4");
		RefTroopImages[1, 0] = (Image)((FrameworkElement)this).FindName("CG1_TroopImage1");
		RefTroopImages[1, 1] = (Image)((FrameworkElement)this).FindName("CG1_TroopImage2");
		RefTroopImages[1, 2] = (Image)((FrameworkElement)this).FindName("CG1_TroopImage3");
		RefTroopImages[1, 3] = (Image)((FrameworkElement)this).FindName("CG1_TroopImage4");
		RefTroopImages[2, 0] = (Image)((FrameworkElement)this).FindName("CG2_TroopImage1");
		RefTroopImages[2, 1] = (Image)((FrameworkElement)this).FindName("CG2_TroopImage2");
		RefTroopImages[2, 2] = (Image)((FrameworkElement)this).FindName("CG2_TroopImage3");
		RefTroopImages[2, 3] = (Image)((FrameworkElement)this).FindName("CG2_TroopImage4");
		RefTroopImages[3, 0] = (Image)((FrameworkElement)this).FindName("CG3_TroopImage1");
		RefTroopImages[3, 1] = (Image)((FrameworkElement)this).FindName("CG3_TroopImage2");
		RefTroopImages[3, 2] = (Image)((FrameworkElement)this).FindName("CG3_TroopImage3");
		RefTroopImages[3, 3] = (Image)((FrameworkElement)this).FindName("CG3_TroopImage4");
		RefTroopImages[4, 0] = (Image)((FrameworkElement)this).FindName("CG4_TroopImage1");
		RefTroopImages[4, 1] = (Image)((FrameworkElement)this).FindName("CG4_TroopImage2");
		RefTroopImages[4, 2] = (Image)((FrameworkElement)this).FindName("CG4_TroopImage3");
		RefTroopImages[4, 3] = (Image)((FrameworkElement)this).FindName("CG4_TroopImage4");
		RefTroopImages[5, 0] = (Image)((FrameworkElement)this).FindName("CG5_TroopImage1");
		RefTroopImages[5, 1] = (Image)((FrameworkElement)this).FindName("CG5_TroopImage2");
		RefTroopImages[5, 2] = (Image)((FrameworkElement)this).FindName("CG5_TroopImage3");
		RefTroopImages[5, 3] = (Image)((FrameworkElement)this).FindName("CG5_TroopImage4");
		RefTroopImages[6, 0] = (Image)((FrameworkElement)this).FindName("CG6_TroopImage1");
		RefTroopImages[6, 1] = (Image)((FrameworkElement)this).FindName("CG6_TroopImage2");
		RefTroopImages[6, 2] = (Image)((FrameworkElement)this).FindName("CG6_TroopImage3");
		RefTroopImages[6, 3] = (Image)((FrameworkElement)this).FindName("CG6_TroopImage4");
		RefTroopImages[7, 0] = (Image)((FrameworkElement)this).FindName("CG7_TroopImage1");
		RefTroopImages[7, 1] = (Image)((FrameworkElement)this).FindName("CG7_TroopImage2");
		RefTroopImages[7, 2] = (Image)((FrameworkElement)this).FindName("CG7_TroopImage3");
		RefTroopImages[7, 3] = (Image)((FrameworkElement)this).FindName("CG7_TroopImage4");
		RefTroopImages[8, 0] = (Image)((FrameworkElement)this).FindName("CG8_TroopImage1");
		RefTroopImages[8, 1] = (Image)((FrameworkElement)this).FindName("CG8_TroopImage2");
		RefTroopImages[8, 2] = (Image)((FrameworkElement)this).FindName("CG8_TroopImage3");
		RefTroopImages[8, 3] = (Image)((FrameworkElement)this).FindName("CG8_TroopImage4");
		RefTroopImages[9, 0] = (Image)((FrameworkElement)this).FindName("CG9_TroopImage1");
		RefTroopImages[9, 1] = (Image)((FrameworkElement)this).FindName("CG9_TroopImage2");
		RefTroopImages[9, 2] = (Image)((FrameworkElement)this).FindName("CG9_TroopImage3");
		RefTroopImages[9, 3] = (Image)((FrameworkElement)this).FindName("CG9_TroopImage4");
		RefTroopValues[0, 0] = (TextBlock)((FrameworkElement)this).FindName("CG0_TroopCount1");
		RefTroopValues[0, 1] = (TextBlock)((FrameworkElement)this).FindName("CG0_TroopCount2");
		RefTroopValues[0, 2] = (TextBlock)((FrameworkElement)this).FindName("CG0_TroopCount3");
		RefTroopValues[0, 3] = (TextBlock)((FrameworkElement)this).FindName("CG0_TroopCount4");
		RefTroopValues[1, 0] = (TextBlock)((FrameworkElement)this).FindName("CG1_TroopCount1");
		RefTroopValues[1, 1] = (TextBlock)((FrameworkElement)this).FindName("CG1_TroopCount2");
		RefTroopValues[1, 2] = (TextBlock)((FrameworkElement)this).FindName("CG1_TroopCount3");
		RefTroopValues[1, 3] = (TextBlock)((FrameworkElement)this).FindName("CG1_TroopCount4");
		RefTroopValues[2, 0] = (TextBlock)((FrameworkElement)this).FindName("CG2_TroopCount1");
		RefTroopValues[2, 1] = (TextBlock)((FrameworkElement)this).FindName("CG2_TroopCount2");
		RefTroopValues[2, 2] = (TextBlock)((FrameworkElement)this).FindName("CG2_TroopCount3");
		RefTroopValues[2, 3] = (TextBlock)((FrameworkElement)this).FindName("CG2_TroopCount4");
		RefTroopValues[3, 0] = (TextBlock)((FrameworkElement)this).FindName("CG3_TroopCount1");
		RefTroopValues[3, 1] = (TextBlock)((FrameworkElement)this).FindName("CG3_TroopCount2");
		RefTroopValues[3, 2] = (TextBlock)((FrameworkElement)this).FindName("CG3_TroopCount3");
		RefTroopValues[3, 3] = (TextBlock)((FrameworkElement)this).FindName("CG3_TroopCount4");
		RefTroopValues[4, 0] = (TextBlock)((FrameworkElement)this).FindName("CG4_TroopCount1");
		RefTroopValues[4, 1] = (TextBlock)((FrameworkElement)this).FindName("CG4_TroopCount2");
		RefTroopValues[4, 2] = (TextBlock)((FrameworkElement)this).FindName("CG4_TroopCount3");
		RefTroopValues[4, 3] = (TextBlock)((FrameworkElement)this).FindName("CG4_TroopCount4");
		RefTroopValues[5, 0] = (TextBlock)((FrameworkElement)this).FindName("CG5_TroopCount1");
		RefTroopValues[5, 1] = (TextBlock)((FrameworkElement)this).FindName("CG5_TroopCount2");
		RefTroopValues[5, 2] = (TextBlock)((FrameworkElement)this).FindName("CG5_TroopCount3");
		RefTroopValues[5, 3] = (TextBlock)((FrameworkElement)this).FindName("CG5_TroopCount4");
		RefTroopValues[6, 0] = (TextBlock)((FrameworkElement)this).FindName("CG6_TroopCount1");
		RefTroopValues[6, 1] = (TextBlock)((FrameworkElement)this).FindName("CG6_TroopCount2");
		RefTroopValues[6, 2] = (TextBlock)((FrameworkElement)this).FindName("CG6_TroopCount3");
		RefTroopValues[6, 3] = (TextBlock)((FrameworkElement)this).FindName("CG6_TroopCount4");
		RefTroopValues[7, 0] = (TextBlock)((FrameworkElement)this).FindName("CG7_TroopCount1");
		RefTroopValues[7, 1] = (TextBlock)((FrameworkElement)this).FindName("CG7_TroopCount2");
		RefTroopValues[7, 2] = (TextBlock)((FrameworkElement)this).FindName("CG7_TroopCount3");
		RefTroopValues[7, 3] = (TextBlock)((FrameworkElement)this).FindName("CG7_TroopCount4");
		RefTroopValues[8, 0] = (TextBlock)((FrameworkElement)this).FindName("CG8_TroopCount1");
		RefTroopValues[8, 1] = (TextBlock)((FrameworkElement)this).FindName("CG8_TroopCount2");
		RefTroopValues[8, 2] = (TextBlock)((FrameworkElement)this).FindName("CG8_TroopCount3");
		RefTroopValues[8, 3] = (TextBlock)((FrameworkElement)this).FindName("CG8_TroopCount4");
		RefTroopValues[9, 0] = (TextBlock)((FrameworkElement)this).FindName("CG9_TroopCount1");
		RefTroopValues[9, 1] = (TextBlock)((FrameworkElement)this).FindName("CG9_TroopCount2");
		RefTroopValues[9, 2] = (TextBlock)((FrameworkElement)this).FindName("CG9_TroopCount3");
		RefTroopValues[9, 3] = (TextBlock)((FrameworkElement)this).FindName("CG9_TroopCount4");
		RefTroopExtraValues[0] = (TextBlock)((FrameworkElement)this).FindName("CG0_TroopRemainder");
		RefTroopExtraValues[1] = (TextBlock)((FrameworkElement)this).FindName("CG1_TroopRemainder");
		RefTroopExtraValues[2] = (TextBlock)((FrameworkElement)this).FindName("CG2_TroopRemainder");
		RefTroopExtraValues[3] = (TextBlock)((FrameworkElement)this).FindName("CG3_TroopRemainder");
		RefTroopExtraValues[4] = (TextBlock)((FrameworkElement)this).FindName("CG4_TroopRemainder");
		RefTroopExtraValues[5] = (TextBlock)((FrameworkElement)this).FindName("CG5_TroopRemainder");
		RefTroopExtraValues[6] = (TextBlock)((FrameworkElement)this).FindName("CG6_TroopRemainder");
		RefTroopExtraValues[7] = (TextBlock)((FrameworkElement)this).FindName("CG7_TroopRemainder");
		RefTroopExtraValues[8] = (TextBlock)((FrameworkElement)this).FindName("CG8_TroopRemainder");
		RefTroopExtraValues[9] = (TextBlock)((FrameworkElement)this).FindName("CG9_TroopRemainder");
		RefTroopRowID[0] = (TextBlock)((FrameworkElement)this).FindName("CG0_Number");
		RefTroopRowID[1] = (TextBlock)((FrameworkElement)this).FindName("CG1_Number");
		RefTroopRowID[2] = (TextBlock)((FrameworkElement)this).FindName("CG2_Number");
		RefTroopRowID[3] = (TextBlock)((FrameworkElement)this).FindName("CG3_Number");
		RefTroopRowID[4] = (TextBlock)((FrameworkElement)this).FindName("CG4_Number");
		RefTroopRowID[5] = (TextBlock)((FrameworkElement)this).FindName("CG5_Number");
		RefTroopRowID[6] = (TextBlock)((FrameworkElement)this).FindName("CG6_Number");
		RefTroopRowID[7] = (TextBlock)((FrameworkElement)this).FindName("CG7_Number");
		RefTroopRowID[8] = (TextBlock)((FrameworkElement)this).FindName("CG8_Number");
		RefTroopRowID[9] = (TextBlock)((FrameworkElement)this).FindName("CG9_Number");
	}

	public static void ToggleMenu()
	{
		if (MainViewModel.Instance.Show_HUD_ControlGroups)
		{
			MainViewModel.Instance.Show_HUD_ControlGroups = false;
			return;
		}
		if (MainViewModel.Instance.Show_HUD_LoadSaveRequester)
		{
			MainViewModel.Instance.HUDLoadSaveRequester.CloseRequester();
		}
		if (MainViewModel.Instance.Show_HUD_Confirmation)
		{
			MainViewModel.Instance.HUDConfirmationPopup.ConfirmationClicked(2);
		}
		if (MainViewModel.Instance.Show_HUD_IngameMenu)
		{
			MainViewModel.Instance.HUDmain.InGameOptions(null, null);
		}
		MainViewModel.Instance.HUDControlGroups.Init();
	}

	public void Init()
	{
		MainViewModel.Instance.Show_HUD_ControlGroups = true;
		populate();
	}

	public void Update()
	{
		populate();
	}

	public ImageSource GetTroopSprite(int type)
	{
		return (ImageSource)(type switch
		{
			0 => MainViewModel.Instance.UIBuildingsO001, 
			1 => MainViewModel.Instance.UIBuildingsO009, 
			2 => MainViewModel.Instance.UIBuildingsO003, 
			3 => MainViewModel.Instance.UIBuildingsO005, 
			4 => MainViewModel.Instance.UIBuildingsO007, 
			5 => MainViewModel.Instance.UIBuildingsO011, 
			6 => MainViewModel.Instance.UIBuildingsO013, 
			7 => MainViewModel.Instance.UIBuildingsO015, 
			8 => MainViewModel.Instance.UIBuildingsO017, 
			9 => MainViewModel.Instance.UIBuildingsO021, 
			10 => MainViewModel.Instance.UIBuildingsO023, 
			11 => MainViewModel.Instance.UIBuildingsO025, 
			12 => MainViewModel.Instance.GameSprites[295], 
			13 => MainViewModel.Instance.UIBuildingsO029, 
			14 => MainViewModel.Instance.UIBuildingsO027, 
			15 => MainViewModel.Instance.UIBuildingsO031, 
			16 => MainViewModel.Instance.UIBuildingsM005, 
			17 => MainViewModel.Instance.UIBuildingsO035, 
			18 => MainViewModel.Instance.UIBuildingsO037, 
			19 => MainViewModel.Instance.UIBuildingsO039, 
			20 => MainViewModel.Instance.UIBuildingsO041, 
			21 => MainViewModel.Instance.UIBuildingsO043, 
			22 => MainViewModel.Instance.UIBuildingsO045, 
			23 => MainViewModel.Instance.UIBuildingsO047, 
			24 => MainViewModel.Instance.UIBuildingsO049, 
			25 => MainViewModel.Instance.UIBuildingsO051, 
			26 => MainViewModel.Instance.UIBuildingsO053, 
			27 => MainViewModel.Instance.UIBuildingsO055, 
			28 => MainViewModel.Instance.UIBuildingsO057, 
			29 => MainViewModel.Instance.UIBuildingsO059, 
			30 => MainViewModel.Instance.UIBuildingsO061, 
			31 => MainViewModel.Instance.UIBuildingsO063, 
			32 => MainViewModel.Instance.UIBuildingsO065, 
			33 => MainViewModel.Instance.UIBuildingsO033, 
			_ => null, 
		});
	}

	public void populate()
	{
		if (GameData.Instance.lastGameState == null)
		{
			return;
		}
		EngineInterface.PlayState lastGameState = GameData.Instance.lastGameState;
		if (lastGameState.control_groups_total.Length <= 1)
		{
			return;
		}
		for (int i = 0; i < 10; i++)
		{
			if (lastGameState.control_groups_total[i] > 0)
			{
				PropEx.SetButtonVisibility((UIElement)(object)RefDeleteButtons[i], (Visibility)2);
				PropEx.SetButtonVisibility((UIElement)(object)RefSelectButtons[i], (Visibility)2);
				RefTroopRowID[i].Foreground = (Brush)(object)RowIDColour_Black;
				int num = lastGameState.control_groups_count[i * 4] + lastGameState.control_groups_count[i * 4 + 1] + lastGameState.control_groups_count[i * 4 + 2] + lastGameState.control_groups_count[i * 4 + 3];
				if (num != lastGameState.control_groups_total[i])
				{
					RefTroopExtraValues[i].Text = "+" + (lastGameState.control_groups_total[i] - num);
					((UIElement)RefTroopExtraValues[i]).Visibility = (Visibility)2;
				}
				else
				{
					((UIElement)RefTroopExtraValues[i]).Visibility = (Visibility)1;
				}
				for (int j = 0; j < 4; j++)
				{
					if (lastGameState.control_groups_count[i * 4 + j] > 0)
					{
						((UIElement)RefTroopImages[i, j]).Visibility = (Visibility)2;
						RefTroopImages[i, j].Source = GetTroopSprite(lastGameState.control_groups_type[i * 4 + j]);
						RefTroopValues[i, j].Text = lastGameState.control_groups_count[i * 4 + j].ToString();
						((UIElement)RefTroopValues[i, j]).Visibility = (Visibility)2;
					}
					else
					{
						((UIElement)RefTroopImages[i, j]).Visibility = (Visibility)1;
						((UIElement)RefTroopValues[i, j]).Visibility = (Visibility)1;
					}
				}
			}
			else
			{
				((UIElement)RefTroopImages[i, 0]).Visibility = (Visibility)1;
				((UIElement)RefTroopImages[i, 1]).Visibility = (Visibility)1;
				((UIElement)RefTroopImages[i, 2]).Visibility = (Visibility)1;
				((UIElement)RefTroopImages[i, 3]).Visibility = (Visibility)1;
				((UIElement)RefTroopValues[i, 0]).Visibility = (Visibility)1;
				((UIElement)RefTroopValues[i, 1]).Visibility = (Visibility)1;
				((UIElement)RefTroopValues[i, 2]).Visibility = (Visibility)1;
				((UIElement)RefTroopValues[i, 3]).Visibility = (Visibility)1;
				PropEx.SetButtonVisibility((UIElement)(object)RefDeleteButtons[i], (Visibility)1);
				PropEx.SetButtonVisibility((UIElement)(object)RefSelectButtons[i], (Visibility)1);
				((UIElement)RefTroopExtraValues[i]).Visibility = (Visibility)1;
				RefTroopRowID[i].Foreground = (Brush)(object)RowIDColour_Light;
			}
		}
	}

	public void ButtonClicked(string command)
	{
		switch (command)
		{
		case "Select_1":
		case "Select_2":
		case "Select_3":
		case "Select_4":
		case "Select_5":
		case "Select_6":
		case "Select_7":
		case "Select_8":
		case "Select_9":
		case "Select_0":
			switch (command)
			{
			case "Select_1":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan1);
				break;
			case "Select_2":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan2);
				break;
			case "Select_3":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan3);
				break;
			case "Select_4":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan4);
				break;
			case "Select_5":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan5);
				break;
			case "Select_6":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan6);
				break;
			case "Select_7":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan7);
				break;
			case "Select_8":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan8);
				break;
			case "Select_9":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan8);
				break;
			case "Select_0":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan9);
				break;
			}
			break;
		case "Create_1":
		case "Create_2":
		case "Create_3":
		case "Create_4":
		case "Create_5":
		case "Create_6":
		case "Create_7":
		case "Create_8":
		case "Create_9":
		case "Create_0":
			switch (command)
			{
			case "Create_1":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops1);
				break;
			case "Create_2":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops2);
				break;
			case "Create_3":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops3);
				break;
			case "Create_4":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops4);
				break;
			case "Create_5":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops5);
				break;
			case "Create_6":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops6);
				break;
			case "Create_7":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops7);
				break;
			case "Create_8":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8);
				break;
			case "Create_9":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8);
				break;
			case "Create_0":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops9);
				break;
			}
			break;
		case "Add_1":
		case "Add_2":
		case "Add_3":
		case "Add_4":
		case "Add_5":
		case "Add_6":
		case "Add_7":
		case "Add_8":
		case "Add_9":
		case "Add_0":
			switch (command)
			{
			case "Add_1":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops1, 10);
				break;
			case "Add_2":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops2, 10);
				break;
			case "Add_3":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops3, 10);
				break;
			case "Add_4":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops4, 10);
				break;
			case "Add_5":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops5, 10);
				break;
			case "Add_6":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops6, 10);
				break;
			case "Add_7":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops7, 10);
				break;
			case "Add_8":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8, 10);
				break;
			case "Add_9":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8, 10);
				break;
			case "Add_0":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops9, 10);
				break;
			}
			break;
		case "Delete_1":
		case "Delete_2":
		case "Delete_3":
		case "Delete_4":
		case "Delete_5":
		case "Delete_6":
		case "Delete_7":
		case "Delete_8":
		case "Delete_9":
		case "Delete_0":
			switch (command)
			{
			case "Delete_1":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops1, 20);
				break;
			case "Delete_2":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops2, 20);
				break;
			case "Delete_3":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops3, 20);
				break;
			case "Delete_4":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops4, 20);
				break;
			case "Delete_5":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops5, 20);
				break;
			case "Delete_6":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops6, 20);
				break;
			case "Delete_7":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops7, 20);
				break;
			case "Delete_8":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8, 20);
				break;
			case "Delete_9":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8, 20);
				break;
			case "Create_0":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops9, 20);
				break;
			}
			break;
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_ControlGroups.xaml");
	}
}
