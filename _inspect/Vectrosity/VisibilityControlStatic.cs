using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vectrosity;

[AddComponentMenu("Vectrosity/VisibilityControlStatic")]
public class VisibilityControlStatic : MonoBehaviour
{
	public RefInt m_objectNumber;

	public VectorLine m_vectorLine;

	public bool m_destroyed;

	public bool m_dontDestroyLine;

	public Matrix4x4 m_originalMatrix;

	public RefInt objectNumber => m_objectNumber;

	public void Setup(VectorLine line, bool makeBounds)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		if (makeBounds)
		{
			VectorManager.SetupBoundsMesh(((Component)this).gameObject, line);
		}
		m_originalMatrix = ((Component)this).transform.localToWorldMatrix;
		List<Vector3> list = new List<Vector3>(line.points3);
		for (int i = 0; i < list.Count; i++)
		{
			list[i] = ((Matrix4x4)(ref m_originalMatrix)).MultiplyPoint3x4(list[i]);
		}
		line.points3 = list;
		m_vectorLine = line;
		VectorManager.VisibilityStaticSetup(line, out m_objectNumber);
		((MonoBehaviour)this).StartCoroutine(WaitCheck());
	}

	public IEnumerator WaitCheck()
	{
		VectorManager.DrawArrayLine(m_objectNumber.i);
		yield return null;
		yield return null;
		if (!((Component)this).GetComponent<Renderer>().isVisible)
		{
			m_vectorLine.active = false;
		}
	}

	public void OnBecameVisible()
	{
		m_vectorLine.active = true;
		VectorManager.DrawArrayLine(m_objectNumber.i);
	}

	public void OnBecameInvisible()
	{
		m_vectorLine.active = false;
	}

	public void OnDestroy()
	{
		if (!m_destroyed)
		{
			m_destroyed = true;
			VectorManager.VisibilityStaticRemove(m_objectNumber.i);
			if (!m_dontDestroyLine)
			{
				VectorLine.Destroy(ref m_vectorLine);
			}
		}
	}

	public void DontDestroyLine()
	{
		m_dontDestroyLine = true;
	}

	public Matrix4x4 GetMatrix()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return m_originalMatrix;
	}
}
