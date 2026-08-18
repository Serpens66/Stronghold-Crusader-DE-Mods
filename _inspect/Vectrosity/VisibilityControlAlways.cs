using UnityEngine;

namespace Vectrosity;

[AddComponentMenu("Vectrosity/VisibilityControlAlways")]
public class VisibilityControlAlways : MonoBehaviour
{
	public RefInt m_objectNumber;

	public VectorLine m_vectorLine;

	public bool m_destroyed;

	public bool m_dontDestroyLine;

	public RefInt objectNumber => m_objectNumber;

	public void Setup(VectorLine line)
	{
		VectorManager.VisibilitySetup(((Component)this).transform, line, out m_objectNumber);
		VectorManager.DrawArrayLine2(m_objectNumber.i);
		m_vectorLine = line;
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
