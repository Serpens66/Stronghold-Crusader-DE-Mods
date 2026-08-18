using UnityEngine;

namespace Vectrosity;

[AddComponentMenu("Vectrosity/BrightnessControl")]
public class BrightnessControl : MonoBehaviour
{
	public RefInt m_objectNumber;

	public VectorLine m_vectorLine;

	public bool m_useLine;

	public bool m_destroyed;

	public RefInt objectNumber => m_objectNumber;

	public void Setup(VectorLine line, bool m_useLine)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		m_objectNumber = new RefInt(0);
		VectorManager.CheckDistanceSetup(((Component)this).transform, line, Color32.op_Implicit(line.color), m_objectNumber);
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

	public void OnBecameVisible()
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

	public void OnDestroy()
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
