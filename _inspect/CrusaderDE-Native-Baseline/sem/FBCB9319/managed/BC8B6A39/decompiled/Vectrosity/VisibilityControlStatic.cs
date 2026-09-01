using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vectrosity;

[AddComponentMenu("Vectrosity/VisibilityControlStatic")]
public class VisibilityControlStatic : MonoBehaviour
{
	private RefInt m_objectNumber;

	private VectorLine m_vectorLine;

	private bool m_destroyed;

	private bool m_dontDestroyLine;

	private Matrix4x4 m_originalMatrix;

	public RefInt objectNumber => m_objectNumber;

	public void Setup(VectorLine line, bool makeBounds)
	{
		if (makeBounds)
		{
			VectorManager.SetupBoundsMesh(base.gameObject, line);
		}
		m_originalMatrix = base.transform.localToWorldMatrix;
		List<Vector3> list = new List<Vector3>(line.points3);
		for (int i = 0; i < list.Count; i++)
		{
			list[i] = m_originalMatrix.MultiplyPoint3x4(list[i]);
		}
		line.points3 = list;
		m_vectorLine = line;
		VectorManager.VisibilityStaticSetup(line, out m_objectNumber);
		StartCoroutine(WaitCheck());
	}

	private IEnumerator WaitCheck()
	{
		VectorManager.DrawArrayLine(m_objectNumber.i);
		yield return null;
		yield return null;
		if (!GetComponent<Renderer>().isVisible)
		{
			m_vectorLine.active = false;
		}
	}

	private void OnBecameVisible()
	{
		m_vectorLine.active = true;
		VectorManager.DrawArrayLine(m_objectNumber.i);
	}

	private void OnBecameInvisible()
	{
		m_vectorLine.active = false;
	}

	private void OnDestroy()
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
		return m_originalMatrix;
	}
}
