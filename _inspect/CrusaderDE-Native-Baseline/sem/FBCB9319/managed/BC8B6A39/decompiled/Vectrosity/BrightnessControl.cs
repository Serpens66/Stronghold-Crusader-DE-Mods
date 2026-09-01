using UnityEngine;

namespace Vectrosity;

[AddComponentMenu("Vectrosity/BrightnessControl")]
public class BrightnessControl : MonoBehaviour
{
	private RefInt m_objectNumber;

	private VectorLine m_vectorLine;

	private bool m_useLine;

	private bool m_destroyed;

	public RefInt objectNumber => m_objectNumber;

	public void Setup(VectorLine line, bool m_useLine)
	{
		m_objectNumber = new RefInt(0);
		VectorManager.CheckDistanceSetup(base.transform, line, line.color, m_objectNumber);
		VectorManager.SetDistanceColor(m_objectNumber.i);
		if (m_useLine)
		{
			this.m_useLine = true;
			m_vectorLine = line;
		}
	}

	public void SetUseLine(bool useLine)
	{
		m_useLine = useLine;
	}

	private void OnBecameVisible()
	{
		VectorManager.SetOldDistance(m_objectNumber.i, -1);
		VectorManager.SetDistanceColor(m_objectNumber.i);
		if (m_useLine)
		{
			m_vectorLine.active = true;
		}
	}

	public void OnBecameInvisible()
	{
		if (m_useLine)
		{
			m_vectorLine.active = false;
		}
	}

	private void OnDestroy()
	{
		if (!m_destroyed)
		{
			m_destroyed = true;
			VectorManager.DistanceRemove(m_objectNumber.i);
			if (m_useLine)
			{
				VectorLine.Destroy(ref m_vectorLine);
			}
		}
	}
}
