using System.Collections;
using UnityEngine;

namespace Vectrosity;

[AddComponentMenu("Vectrosity/VisibilityControl")]
public class VisibilityControl : MonoBehaviour
{
	private RefInt m_objectNumber;

	private VectorLine m_vectorLine;

	private bool m_destroyed;

	private bool m_dontDestroyLine;

	public RefInt objectNumber => m_objectNumber;

	public void Setup(VectorLine line, bool makeBounds)
	{
		if (makeBounds)
		{
			VectorManager.SetupBoundsMesh(base.gameObject, line);
		}
		VectorManager.VisibilitySetup(base.transform, line, out m_objectNumber);
		m_vectorLine = line;
		VectorManager.DrawArrayLine2(m_objectNumber.i);
		StartCoroutine(VisibilityTest());
	}

	private IEnumerator VisibilityTest()
	{
		yield return null;
		yield return null;
		if (!GetComponent<Renderer>().isVisible)
		{
			m_vectorLine.active = false;
		}
	}

	private IEnumerator OnBecameVisible()
	{
		yield return new WaitForEndOfFrame();
		m_vectorLine.active = true;
	}

	private IEnumerator OnBecameInvisible()
	{
		yield return new WaitForEndOfFrame();
		m_vectorLine.active = false;
	}

	private void OnDestroy()
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
