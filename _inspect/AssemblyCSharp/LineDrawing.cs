using System.Collections.Generic;
using UnityEngine;
using Vectrosity;

public class LineDrawing : MonoBehaviour
{
	public static LineDrawing instance;

	public Material BoxMaterial;

	public Material lineMaterial;

	public Texture hudLineTexture;

	public GameObject boxParent;

	public GameObject lineParent;

	public GameObject hudLineParent;

	private Vector3 lineVector;

	private debugBox[] debugBoxArray;

	private debugLine[] debugLineArray;

	private debugLine[] hudLineArray;

	private const int MAXDebugBoxes = 2000;

	private const int MAXDebugLines = 10000;

	private const int MAXHUDLines = 50;

	private float ssx1;

	private float ssx2;

	private float ssy1;

	private float ssy2;

	private void Awake()
	{
		instance = this;
		if (instance == null)
		{
			instance = this;
		}
		else if (instance != this)
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		debugBoxArray = new debugBox[2000];
		debugLineArray = new debugLine[10000];
		hudLineArray = new debugLine[50];
		setupDebugBoxes();
		setupDebugLines();
		setupHUDLines();
	}

	public void killAllLines()
	{
		killDebugBoxLines();
		killDebugLineLines();
		killHUDLines();
	}

	public void setupDebugBoxes()
	{
		killDebugBoxLines();
		for (int i = 0; i < 2000; i++)
		{
			debugBoxArray[i].active = 0;
		}
	}

	public void killDebugBoxLines()
	{
		for (int i = 0; i < 2000; i++)
		{
			if (debugBoxArray[i].active == 1)
			{
				VectorLine.Destroy(ref debugBoxArray[i].drawnLine);
			}
		}
	}

	public int getFreeDebugBox()
	{
		for (int i = 0; i < 2000; i++)
		{
			if (debugBoxArray[i].active == 0)
			{
				debugBoxArray[i].active = 1;
				return i;
			}
		}
		return -1;
	}

	public void addADebugBox(float x, float y, float w, float h)
	{
		int freeDebugBox = getFreeDebugBox();
		if (freeDebugBox > -1)
		{
			debugBoxArray[freeDebugBox].boxPosition = new Vector2(x, y);
			debugBoxArray[freeDebugBox].boxDimensions.x = w;
			debugBoxArray[freeDebugBox].boxDimensions.y = h;
			debugBoxArray[freeDebugBox].drawnLine = new VectorLine("Debug Box " + freeDebugBox, new List<Vector3>(), 1f, LineType.Continuous, Joins.Fill);
			debugBoxArray[freeDebugBox].drawnLine.Resize(5);
			debugBoxArray[freeDebugBox].drawnLine.material = BoxMaterial;
			debugBoxArray[freeDebugBox].drawnLine.color = Color.yellow;
			debugBoxArray[freeDebugBox].drawnLine.points3.Clear();
			createDebugBoxLine(freeDebugBox);
			debugBoxArray[freeDebugBox].drawnLine.rectTransform.SetParent(boxParent.transform);
		}
	}

	private void createDebugBoxLine(int thisDebugBox)
	{
		float x = debugBoxArray[thisDebugBox].boxPosition.x;
		float y = debugBoxArray[thisDebugBox].boxPosition.y;
		float x2 = debugBoxArray[thisDebugBox].boxDimensions.x;
		float y2 = debugBoxArray[thisDebugBox].boxDimensions.y;
		lineVector.z = -0.1f;
		lineVector.x = x;
		lineVector.y = y;
		debugBoxArray[thisDebugBox].drawnLine.points3.Add(lineVector);
		lineVector.x = x - 0.5f * y2;
		lineVector.y = y + 0.25f * y2;
		debugBoxArray[thisDebugBox].drawnLine.points3.Add(lineVector);
		lineVector.x = x + 0.5f * (x2 - y2);
		lineVector.y = y + 0.5f * ((x2 + y2) / 2f);
		debugBoxArray[thisDebugBox].drawnLine.points3.Add(lineVector);
		lineVector.x = x + 0.5f * x2;
		lineVector.y = y + 0.25f * x2;
		debugBoxArray[thisDebugBox].drawnLine.points3.Add(lineVector);
		lineVector.x = x;
		lineVector.y = y;
		debugBoxArray[thisDebugBox].drawnLine.points3.Add(lineVector);
		PerfectPixelWithZoom.instance.setCamaraToStartSize();
		debugBoxArray[thisDebugBox].drawnLine.Draw3D();
		PerfectPixelWithZoom.instance.returnCamaraToCurrentSize();
	}

	public void setupDebugLines()
	{
		killDebugLineLines();
		for (int i = 0; i < 10000; i++)
		{
			debugLineArray[i].active = 0;
		}
	}

	public void killDebugLineLines()
	{
		for (int i = 0; i < 10000; i++)
		{
			if (debugLineArray[i].active == 1)
			{
				VectorLine.Destroy(ref debugLineArray[i].drawnLine);
			}
		}
	}

	public int getFreeDebugLine()
	{
		for (int i = 0; i < 10000; i++)
		{
			if (debugLineArray[i].active == 0)
			{
				debugLineArray[i].active = 1;
				return i;
			}
		}
		return -1;
	}

	public void addADebugLine(float x1, float y1, float x2, float y2)
	{
		int freeDebugLine = getFreeDebugLine();
		if (freeDebugLine > -1)
		{
			debugLineArray[freeDebugLine].point1 = new Vector2(x1, y1);
			debugLineArray[freeDebugLine].point2 = new Vector2(x2, y2);
			debugLineArray[freeDebugLine].drawnLine = new VectorLine("Debug Line " + freeDebugLine, new List<Vector3>(), 1f, LineType.Continuous, Joins.Fill);
			debugLineArray[freeDebugLine].drawnLine.Resize(2);
			debugLineArray[freeDebugLine].drawnLine.material = lineMaterial;
			debugLineArray[freeDebugLine].drawnLine.color = Color.cyan;
			debugLineArray[freeDebugLine].drawnLine.points3.Clear();
			createDebugLineLine(freeDebugLine);
			debugLineArray[freeDebugLine].drawnLine.rectTransform.SetParent(lineParent.transform);
		}
	}

	private void createDebugLineLine(int thisDebugLine)
	{
		float x = debugLineArray[thisDebugLine].point1.x;
		float y = debugLineArray[thisDebugLine].point1.y;
		float x2 = debugLineArray[thisDebugLine].point2.x;
		float y2 = debugLineArray[thisDebugLine].point2.y;
		lineVector.z = -0.1f;
		lineVector.x = x;
		lineVector.y = y;
		debugLineArray[thisDebugLine].drawnLine.points3.Add(lineVector);
		lineVector.x = x2;
		lineVector.y = y2;
		debugLineArray[thisDebugLine].drawnLine.points3.Add(lineVector);
		PerfectPixelWithZoom.instance.setCamaraToStartSize();
		debugLineArray[thisDebugLine].drawnLine.Draw3D();
		PerfectPixelWithZoom.instance.returnCamaraToCurrentSize();
	}

	public void setupHUDLines()
	{
		killHUDLines();
		for (int i = 0; i < 50; i++)
		{
			hudLineArray[i].active = 0;
		}
	}

	public void killHUDLines()
	{
		for (int i = 0; i < 50; i++)
		{
			killHUDLine(i);
		}
	}

	public void killHUDLine(int thisHUDLine)
	{
		if (hudLineArray[thisHUDLine].active != 0)
		{
			VectorLine.Destroy(ref hudLineArray[thisHUDLine].drawnLine);
			hudLineArray[thisHUDLine].active = 0;
			hudLineArray[thisHUDLine].owner = 0;
		}
	}

	public bool doesOwnerHaveHUDLine(int owner)
	{
		for (int i = 0; i < 50; i++)
		{
			if (hudLineArray[i].active != 0 && hudLineArray[i].owner == owner)
			{
				return true;
			}
		}
		return false;
	}

	public int addAHUDLine(int owner, int x1, int y1, int x2, int y2)
	{
		int num = 0;
		for (int i = 1; i < 50; i++)
		{
			if (hudLineArray[i].active < 1)
			{
				hudLineArray[i].active = 1;
				hudLineArray[i].owner = owner;
				num = i;
				break;
			}
		}
		if (num == 0)
		{
			return 0;
		}
		TilemapManager.instance.translateTileToScreenCoords(x1, y1, 1, ref ssx1, ref ssy1);
		TilemapManager.instance.translateTileToScreenCoords(x2, y2, 1, ref ssx2, ref ssy2);
		hudLineArray[num].point1.x = ssx1;
		hudLineArray[num].point1.y = ssy1;
		hudLineArray[num].point2.x = ssx2;
		hudLineArray[num].point2.y = ssy2;
		List<Vector3> list = new List<Vector3>();
		list.Add(new Vector3(ssx1, ssy1, -0.1f));
		list.Add(new Vector3(ssx2, ssy2, -0.1f));
		hudLineArray[num].drawnLine = new VectorLine("Hud Line " + num, list, hudLineTexture, 2f);
		hudLineArray[num].drawnLine.Resize(2);
		hudLineArray[num].drawnLine.textureScale = 1f;
		hudLineArray[num].drawnLine.points3.Clear();
		createHUDLine(num);
		return num;
	}

	public void repositionHUDLine(int thisHUDLine, int x1, int y1, int x2, int y2)
	{
		if (thisHUDLine > 0)
		{
			TilemapManager.instance.translateTileToScreenCoords(x1, y1, 1, ref ssx1, ref ssy1);
			TilemapManager.instance.translateTileToScreenCoords(x2, y2, 1, ref ssx2, ref ssy2);
			hudLineArray[thisHUDLine].point1.x = ssx1;
			hudLineArray[thisHUDLine].point1.y = ssy1;
			hudLineArray[thisHUDLine].point2.x = ssx2;
			hudLineArray[thisHUDLine].point2.y = ssy2;
			hudLineArray[thisHUDLine].drawnLine.points3[0] = new Vector3(ssx1, ssy1, -0.1f);
			hudLineArray[thisHUDLine].drawnLine.points3[1] = new Vector3(ssx2, ssy2, -0.1f);
			PerfectPixelWithZoom.instance.setCamaraToStartSize();
			hudLineArray[thisHUDLine].drawnLine.Draw3D();
			PerfectPixelWithZoom.instance.returnCamaraToCurrentSize();
			hudLineArray[thisHUDLine].drawnLine.rectTransform.SetParent(hudLineParent.transform);
		}
	}

	private void createHUDLine(int thisHUDLine)
	{
		float x = hudLineArray[thisHUDLine].point1.x;
		float y = hudLineArray[thisHUDLine].point1.y;
		float x2 = hudLineArray[thisHUDLine].point2.x;
		float y2 = hudLineArray[thisHUDLine].point2.y;
		lineVector.z = -0.1f;
		lineVector.x = x;
		lineVector.y = y;
		hudLineArray[thisHUDLine].drawnLine.points3.Add(lineVector);
		lineVector.x = x2;
		lineVector.y = y2;
		hudLineArray[thisHUDLine].drawnLine.points3.Add(lineVector);
	}
}
