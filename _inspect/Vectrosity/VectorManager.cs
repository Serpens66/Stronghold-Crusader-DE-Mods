using System.Collections.Generic;
using UnityEngine;

namespace Vectrosity;

public class VectorManager
{
	public static float minBrightnessDistance = 500f;

	public static float maxBrightnessDistance = 250f;

	public static int brightnessLevels = 32;

	public static float distanceCheckFrequency = 0.2f;

	public static Color fogColor;

	public static bool useDraw3D = false;

	public static List<VectorLine> vectorLines;

	public static List<RefInt> objectNumbers;

	public static int _arrayCount = 0;

	public static List<VectorLine> vectorLines2;

	public static List<RefInt> objectNumbers2;

	public static int _arrayCount2 = 0;

	public static List<Transform> transforms3;

	public static List<VectorLine> vectorLines3;

	public static List<int> oldDistances;

	public static List<Color> colors;

	public static List<RefInt> objectNumbers3;

	public static int _arrayCount3 = 0;

	public static Dictionary<string, Mesh> meshTable;

	public static int arrayCount => _arrayCount;

	public static int arrayCount2 => _arrayCount2;

	public static void SetBrightnessParameters(float fadeOutDistance, float fullBrightDistance, int levels, float frequency, Color color)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		minBrightnessDistance = fadeOutDistance * fadeOutDistance;
		maxBrightnessDistance = fullBrightDistance * fullBrightDistance;
		brightnessLevels = levels;
		distanceCheckFrequency = frequency;
		fogColor = color;
	}

	public static float GetBrightnessValue(Vector3 pos)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (!VectorLine.camTransformExists)
		{
			VectorLine.SetCamera3D();
		}
		float num = minBrightnessDistance;
		float num2 = maxBrightnessDistance;
		Vector3 val = pos - VectorLine.camTransformPosition;
		return Mathf.InverseLerp(num, num2, ((Vector3)(ref val)).sqrMagnitude);
	}

	public static void ObjectSetup(GameObject go, VectorLine line, Visibility visibility, Brightness brightness)
	{
		ObjectSetup(go, line, visibility, brightness, makeBounds: true);
	}

	public static void ObjectSetup(GameObject go, VectorLine line, Visibility visibility, Brightness brightness, bool makeBounds)
	{
		VisibilityControl visibilityControl = go.GetComponent(typeof(VisibilityControl)) as VisibilityControl;
		VisibilityControlStatic visibilityControlStatic = go.GetComponent(typeof(VisibilityControlStatic)) as VisibilityControlStatic;
		VisibilityControlAlways visibilityControlAlways = go.GetComponent(typeof(VisibilityControlAlways)) as VisibilityControlAlways;
		BrightnessControl brightnessControl = go.GetComponent(typeof(BrightnessControl)) as BrightnessControl;
		Component component = go.GetComponent(typeof(MeshFilter));
		if ((Object)(object)((component is MeshFilter) ? component : null) == (Object)null)
		{
			go.AddComponent<MeshFilter>();
		}
		Component component2 = go.GetComponent(typeof(MeshRenderer));
		if ((Object)(object)((component2 is MeshRenderer) ? component2 : null) == (Object)null)
		{
			go.AddComponent<MeshRenderer>();
		}
		switch (visibility)
		{
		case Visibility.Dynamic:
			if (Object.op_Implicit((Object)(object)visibilityControlStatic))
			{
				visibilityControlStatic.DontDestroyLine();
				Object.Destroy((Object)(object)visibilityControlStatic);
				ResetLinePoints(visibilityControlStatic, line);
			}
			if (Object.op_Implicit((Object)(object)visibilityControlAlways))
			{
				visibilityControlAlways.DontDestroyLine();
				Object.Destroy((Object)(object)visibilityControlAlways);
			}
			if ((Object)(object)visibilityControl == (Object)null)
			{
				visibilityControl = go.AddComponent(typeof(VisibilityControl)) as VisibilityControl;
				visibilityControl.Setup(line, makeBounds);
				if ((Object)(object)brightnessControl != (Object)null)
				{
					brightnessControl.SetUseLine(useLine: false);
				}
			}
			break;
		case Visibility.Static:
			if (Object.op_Implicit((Object)(object)visibilityControl))
			{
				visibilityControl.DontDestroyLine();
				Object.Destroy((Object)(object)visibilityControl);
			}
			if (Object.op_Implicit((Object)(object)visibilityControlAlways))
			{
				visibilityControlAlways.DontDestroyLine();
				Object.Destroy((Object)(object)visibilityControlAlways);
			}
			if ((Object)(object)visibilityControlStatic == (Object)null)
			{
				visibilityControlStatic = go.AddComponent(typeof(VisibilityControlStatic)) as VisibilityControlStatic;
				visibilityControlStatic.Setup(line, makeBounds);
				if ((Object)(object)brightnessControl != (Object)null)
				{
					brightnessControl.SetUseLine(useLine: false);
				}
			}
			break;
		case Visibility.Always:
			if (Object.op_Implicit((Object)(object)visibilityControl))
			{
				visibilityControl.DontDestroyLine();
				Object.Destroy((Object)(object)visibilityControl);
			}
			if (Object.op_Implicit((Object)(object)visibilityControlStatic))
			{
				visibilityControlStatic.DontDestroyLine();
				Object.Destroy((Object)(object)visibilityControlStatic);
				ResetLinePoints(visibilityControlStatic, line);
			}
			if ((Object)(object)visibilityControlAlways == (Object)null)
			{
				visibilityControlAlways = go.AddComponent(typeof(VisibilityControlAlways)) as VisibilityControlAlways;
				visibilityControlAlways.Setup(line);
				if ((Object)(object)brightnessControl != (Object)null)
				{
					brightnessControl.SetUseLine(useLine: false);
				}
			}
			break;
		}
		if (brightness == Brightness.Fog)
		{
			if ((Object)(object)brightnessControl == (Object)null)
			{
				brightnessControl = go.AddComponent(typeof(BrightnessControl)) as BrightnessControl;
				if ((Object)(object)visibilityControl == (Object)null && (Object)(object)visibilityControlStatic == (Object)null && (Object)(object)visibilityControlAlways == (Object)null)
				{
					brightnessControl.Setup(line, m_useLine: true);
				}
				else
				{
					brightnessControl.Setup(line, m_useLine: false);
				}
			}
		}
		else if (Object.op_Implicit((Object)(object)brightnessControl))
		{
			Object.Destroy((Object)(object)brightnessControl);
		}
	}

	public static void ResetLinePoints(VisibilityControlStatic vcs, VectorLine line)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		Matrix4x4 matrix = vcs.GetMatrix();
		Matrix4x4 inverse = ((Matrix4x4)(ref matrix)).inverse;
		for (int i = 0; i < line.points3.Count; i++)
		{
			line.points3[i] = ((Matrix4x4)(ref inverse)).MultiplyPoint3x4(line.points3[i]);
		}
	}

	public static void VisibilityStaticSetup(VectorLine line, out RefInt objectNum)
	{
		if (vectorLines == null)
		{
			vectorLines = new List<VectorLine>();
			objectNumbers = new List<RefInt>();
		}
		line.drawTransform = null;
		vectorLines.Add(line);
		objectNum = new RefInt(_arrayCount++);
		objectNumbers.Add(objectNum);
		VectorLine.LineManagerEnable();
	}

	public static void VisibilityStaticRemove(int objectNumber)
	{
		if (objectNumber >= vectorLines.Count)
		{
			Debug.LogError((object)"VectorManager: object number exceeds array length in VisibilityStaticRemove");
			return;
		}
		for (int i = objectNumber + 1; i < _arrayCount; i++)
		{
			objectNumbers[i].i--;
		}
		vectorLines.RemoveAt(objectNumber);
		objectNumbers.RemoveAt(objectNumber);
		_arrayCount--;
		VectorLine.LineManagerDisable();
	}

	public static void VisibilitySetup(Transform thisTransform, VectorLine line, out RefInt objectNum)
	{
		if (vectorLines2 == null)
		{
			vectorLines2 = new List<VectorLine>();
			objectNumbers2 = new List<RefInt>();
		}
		line.drawTransform = thisTransform;
		vectorLines2.Add(line);
		objectNum = new RefInt(_arrayCount2++);
		objectNumbers2.Add(objectNum);
		VectorLine.LineManagerEnable();
	}

	public static void VisibilityRemove(int objectNumber)
	{
		if (objectNumber >= vectorLines2.Count)
		{
			Debug.LogError((object)"VectorManager: object number exceeds array length in VisibilityRemove");
			return;
		}
		for (int i = objectNumber + 1; i < _arrayCount2; i++)
		{
			objectNumbers2[i].i--;
		}
		vectorLines2.RemoveAt(objectNumber);
		objectNumbers2.RemoveAt(objectNumber);
		_arrayCount2--;
		VectorLine.LineManagerDisable();
	}

	public static void CheckDistanceSetup(Transform thisTransform, VectorLine line, Color color, RefInt objectNum)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		VectorLine.LineManagerEnable();
		if (vectorLines3 == null)
		{
			vectorLines3 = new List<VectorLine>();
			transforms3 = new List<Transform>();
			oldDistances = new List<int>();
			colors = new List<Color>();
			objectNumbers3 = new List<RefInt>();
			VectorLine.LineManagerCheckDistance();
		}
		transforms3.Add(thisTransform);
		vectorLines3.Add(line);
		oldDistances.Add(-1);
		colors.Add(color);
		objectNum.i = _arrayCount3++;
		objectNumbers3.Add(objectNum);
	}

	public static void DistanceRemove(int objectNumber)
	{
		if (objectNumber >= vectorLines3.Count)
		{
			Debug.LogError((object)"VectorManager: object number exceeds array length in DistanceRemove");
			return;
		}
		for (int i = objectNumber + 1; i < _arrayCount3; i++)
		{
			objectNumbers3[i].i--;
		}
		transforms3.RemoveAt(objectNumber);
		vectorLines3.RemoveAt(objectNumber);
		oldDistances.RemoveAt(objectNumber);
		colors.RemoveAt(objectNumber);
		objectNumbers3.RemoveAt(objectNumber);
		_arrayCount3--;
	}

	public static void CheckDistance()
	{
		for (int i = 0; i < _arrayCount3; i++)
		{
			SetDistanceColor(i);
		}
	}

	public static void SetOldDistance(int objectNumber, int val)
	{
		oldDistances[objectNumber] = val;
	}

	public static void SetDistanceColor(int i)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		if (vectorLines3[i].active)
		{
			float brightnessValue = GetBrightnessValue(transforms3[i].position);
			int num = (int)(brightnessValue * (float)brightnessLevels);
			if (num != oldDistances[i])
			{
				vectorLines3[i].SetColor(Color32.op_Implicit(Color.Lerp(fogColor, colors[i], brightnessValue)));
			}
			oldDistances[i] = num;
		}
	}

	public static void DrawArrayLine(int i)
	{
		if (useDraw3D)
		{
			vectorLines[i].Draw3D();
		}
		else
		{
			vectorLines[i].Draw();
		}
	}

	public static void DrawArrayLine2(int i)
	{
		if (useDraw3D)
		{
			vectorLines2[i].Draw3D();
		}
		else
		{
			vectorLines2[i].Draw();
		}
	}

	public static void DrawArrayLines()
	{
		if (useDraw3D)
		{
			for (int i = 0; i < _arrayCount; i++)
			{
				vectorLines[i].Draw3D();
			}
		}
		else
		{
			for (int j = 0; j < _arrayCount; j++)
			{
				vectorLines[j].Draw();
			}
		}
	}

	public static void DrawArrayLines2()
	{
		if (useDraw3D)
		{
			for (int i = 0; i < _arrayCount2; i++)
			{
				vectorLines2[i].Draw3D();
			}
		}
		else
		{
			for (int j = 0; j < _arrayCount2; j++)
			{
				vectorLines2[j].Draw();
			}
		}
	}

	public static Bounds GetBounds(VectorLine line)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		if (line.points3 == null)
		{
			Debug.LogError((object)"VectorManager: GetBounds can only be used with a Vector3 array");
			return default(Bounds);
		}
		return GetBounds(line.points3);
	}

	public static Bounds GetBounds(List<Vector3> points3)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		Bounds result = default(Bounds);
		Vector3 val = default(Vector3);
		((Vector3)(ref val))._002Ector(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 val2 = default(Vector3);
		((Vector3)(ref val2))._002Ector(float.MinValue, float.MinValue, float.MinValue);
		int count = points3.Count;
		for (int i = 0; i < count; i++)
		{
			if (points3[i].x < val.x)
			{
				val.x = points3[i].x;
			}
			else if (points3[i].x > val2.x)
			{
				val2.x = points3[i].x;
			}
			if (points3[i].y < val.y)
			{
				val.y = points3[i].y;
			}
			else if (points3[i].y > val2.y)
			{
				val2.y = points3[i].y;
			}
			if (points3[i].z < val.z)
			{
				val.z = points3[i].z;
			}
			else if (points3[i].z > val2.z)
			{
				val2.z = points3[i].z;
			}
		}
		((Bounds)(ref result)).min = val;
		((Bounds)(ref result)).max = val2;
		return result;
	}

	public static Mesh MakeBoundsMesh(Bounds bounds)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		Mesh val = new Mesh();
		val.vertices = (Vector3[])(object)new Vector3[8]
		{
			((Bounds)(ref bounds)).center + new Vector3(0f - ((Bounds)(ref bounds)).extents.x, ((Bounds)(ref bounds)).extents.y, ((Bounds)(ref bounds)).extents.z),
			((Bounds)(ref bounds)).center + new Vector3(((Bounds)(ref bounds)).extents.x, ((Bounds)(ref bounds)).extents.y, ((Bounds)(ref bounds)).extents.z),
			((Bounds)(ref bounds)).center + new Vector3(0f - ((Bounds)(ref bounds)).extents.x, ((Bounds)(ref bounds)).extents.y, 0f - ((Bounds)(ref bounds)).extents.z),
			((Bounds)(ref bounds)).center + new Vector3(((Bounds)(ref bounds)).extents.x, ((Bounds)(ref bounds)).extents.y, 0f - ((Bounds)(ref bounds)).extents.z),
			((Bounds)(ref bounds)).center + new Vector3(0f - ((Bounds)(ref bounds)).extents.x, 0f - ((Bounds)(ref bounds)).extents.y, ((Bounds)(ref bounds)).extents.z),
			((Bounds)(ref bounds)).center + new Vector3(((Bounds)(ref bounds)).extents.x, 0f - ((Bounds)(ref bounds)).extents.y, ((Bounds)(ref bounds)).extents.z),
			((Bounds)(ref bounds)).center + new Vector3(0f - ((Bounds)(ref bounds)).extents.x, 0f - ((Bounds)(ref bounds)).extents.y, 0f - ((Bounds)(ref bounds)).extents.z),
			((Bounds)(ref bounds)).center + new Vector3(((Bounds)(ref bounds)).extents.x, 0f - ((Bounds)(ref bounds)).extents.y, 0f - ((Bounds)(ref bounds)).extents.z)
		};
		return val;
	}

	public static void SetupBoundsMesh(GameObject go, VectorLine line)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		MeshFilter val = go.GetComponent<MeshFilter>();
		if ((Object)(object)val == (Object)null)
		{
			val = go.AddComponent<MeshFilter>();
		}
		MeshRenderer val2 = go.GetComponent<MeshRenderer>();
		if ((Object)(object)val2 == (Object)null)
		{
			val2 = go.AddComponent<MeshRenderer>();
		}
		((Renderer)val2).enabled = true;
		if (meshTable == null)
		{
			meshTable = new Dictionary<string, Mesh>();
		}
		if (!meshTable.ContainsKey(line.name))
		{
			meshTable.Add(line.name, MakeBoundsMesh(GetBounds(line)));
			((Object)meshTable[line.name]).name = line.name + " Bounds";
		}
		val.mesh = meshTable[line.name];
	}
}
