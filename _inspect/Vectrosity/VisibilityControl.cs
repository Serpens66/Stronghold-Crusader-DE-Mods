using System.Collections;
using UnityEngine;

namespace Vectrosity;

[AddComponentMenu("Vectrosity/VisibilityControl")]
public class VisibilityControl : MonoBehaviour
{
	public RefInt m_objectNumber;

	public VectorLine m_vectorLine;

	public bool m_destroyed;

	public bool m_dontDestroyLine;

	public RefInt objectNumber => m_objectNumber;

	public void Setup(VectorLine line, bool makeBounds)
	{
		if (makeBounds)
		{
			VectorManager.SetupBoundsMesh(((Component)this).gameObject, line);
		}
		VectorManager.VisibilitySetup(((Component)this).transform, line, out m_objectNumber);
		m_vectorLine = line;
		VectorManager.DrawArrayLine2(m_objectNumber.i);
		((MonoBehaviour)this).StartCoroutine(VisibilityTest());
	}

	public IEnumerator VisibilityTest()
	{
		yield return null;
		yield return null;
		if (!((Component)this).GetComponent<Renderer>().isVisible)
		{
			m_vectorLine.active = false;
		}
	}

	public IEnumerator OnBecameVisible()
	{
		yield return (object)new WaitForEndOfFrame();
		m_vectorLine.active = true;
	}

	public IEnumerator OnBecameInvisible()
	{
		yield return (object)new WaitForEndOfFrame();
		m_vectorLine.active = false;
	}

	public void OnDestroy()
	{
		if (!m_destroyed)
		{
			m_destroyed = true;
			VectorManager.VisibilityRemove(m_objectNumber.i);
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
}
