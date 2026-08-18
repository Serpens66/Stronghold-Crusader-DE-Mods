using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vectrosity;

[AddComponentMenu("Vectrosity/LineManager")]
public class LineManager : MonoBehaviour
{
	public static List<VectorLine> lines;

	public static List<Transform> transforms;

	public static int lineCount;

	public bool destroyed;

	public void Awake()
	{
		Initialize();
	}

	public void Initialize()
	{
		lines = new List<VectorLine>();
		transforms = new List<Transform>();
		lineCount = 0;
		((Behaviour)this).enabled = false;
	}

	public void AddLine(VectorLine vectorLine, Transform thisTransform, float time)
	{
		if (time > 0f)
		{
			((MonoBehaviour)this).StartCoroutine(DisableLine(vectorLine, time, remove: false));
		}
		for (int i = 0; i < lineCount; i++)
		{
			if (vectorLine == lines[i])
			{
				return;
			}
		}
		lines.Add(vectorLine);
		transforms.Add(thisTransform);
		if (++lineCount == 1)
		{
			((Behaviour)this).enabled = true;
		}
	}

	public void DisableLine(VectorLine vectorLine, float time)
	{
		((MonoBehaviour)this).StartCoroutine(DisableLine(vectorLine, time, remove: false));
	}

	public IEnumerator DisableLine(VectorLine vectorLine, float time, bool remove)
	{
		yield return (object)new WaitForSeconds(time);
		if (remove)
		{
			RemoveLine(vectorLine);
		}
		else
		{
			RemoveLine(vectorLine);
			VectorLine.Destroy(ref vectorLine);
		}
		vectorLine = null;
	}

	public void LateUpdate()
	{
		if (!VectorLine.camTransformExists)
		{
			return;
		}
		for (int i = 0; i < lineCount; i++)
		{
			if ((Object)(object)lines[i].rectTransform != (Object)null)
			{
				lines[i].Draw3D();
			}
			else
			{
				RemoveLine(i--);
			}
		}
		if (VectorLine.CameraHasMoved())
		{
			VectorManager.DrawArrayLines();
		}
		VectorLine.UpdateCameraInfo();
		VectorManager.DrawArrayLines2();
	}

	public void RemoveLine(int i)
	{
		lines.RemoveAt(i);
		transforms.RemoveAt(i);
		lineCount--;
		DisableIfUnused();
	}

	public void RemoveLine(VectorLine vectorLine)
	{
		for (int i = 0; i < lineCount; i++)
		{
			if (vectorLine == lines[i])
			{
				RemoveLine(i);
				break;
			}
		}
	}

	public void DisableIfUnused()
	{
		if (!destroyed && lineCount == 0 && VectorManager.arrayCount == 0 && VectorManager.arrayCount2 == 0)
		{
			((Behaviour)this).enabled = false;
		}
	}

	public void EnableIfUsed()
	{
		if (VectorManager.arrayCount == 1 || VectorManager.arrayCount2 == 1)
		{
			((Behaviour)this).enabled = true;
		}
	}

	public void StartCheckDistance()
	{
		((MonoBehaviour)this).InvokeRepeating("CheckDistance", 0.01f, VectorManager.distanceCheckFrequency);
	}

	public void CheckDistance()
	{
		VectorManager.CheckDistance();
	}

	public void OnDestroy()
	{
		destroyed = true;
	}
}
