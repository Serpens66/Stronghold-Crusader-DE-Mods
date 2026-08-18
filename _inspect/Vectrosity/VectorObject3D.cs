using UnityEngine;

namespace Vectrosity;

public class VectorObject3D : MonoBehaviour, IVectorObject
{
	public bool m_updateVerts = true;

	public bool m_updateUVs = true;

	public bool m_updateColors = true;

	public bool m_updateNormals;

	public bool m_updateTangents;

	public bool m_updateTris = true;

	public Mesh m_mesh;

	public VectorLine m_vectorLine;

	public Material m_material;

	public bool m_useCustomMaterial;

	public void SetVectorLine(VectorLine vectorLine, Texture tex, Material mat, bool useCustomMaterial)
	{
		((Component)this).gameObject.AddComponent<MeshRenderer>();
		((Component)this).gameObject.AddComponent<MeshFilter>();
		m_vectorLine = vectorLine;
		m_material = mat;
		m_material.mainTexture = tex;
		((Renderer)((Component)this).GetComponent<MeshRenderer>()).sharedMaterial = m_material;
		m_useCustomMaterial = useCustomMaterial;
		SetupMesh();
	}

	public void Destroy()
	{
		Object.Destroy((Object)(object)m_mesh);
		if (!m_useCustomMaterial)
		{
			Object.Destroy((Object)(object)m_material);
		}
	}

	public void Enable(bool enable)
	{
		if (!((Object)(object)this == (Object)null))
		{
			((Renderer)((Component)this).GetComponent<MeshRenderer>()).enabled = enable;
		}
	}

	public void SetTexture(Texture tex)
	{
		((Renderer)((Component)this).GetComponent<MeshRenderer>()).sharedMaterial.mainTexture = tex;
	}

	public void SetMaterial(Material mat)
	{
		m_material = mat;
		m_useCustomMaterial = true;
		((Renderer)((Component)this).GetComponent<MeshRenderer>()).sharedMaterial = mat;
		if ((Object)(object)mat != (Object)null)
		{
			((Renderer)((Component)this).GetComponent<MeshRenderer>()).sharedMaterial.mainTexture = m_vectorLine.texture;
		}
	}

	public void SetupMesh()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		m_mesh = new Mesh();
		((Object)m_mesh).name = m_vectorLine.name;
		((Object)m_mesh).hideFlags = (HideFlags)61;
		((Component)this).GetComponent<MeshFilter>().mesh = m_mesh;
	}

	public void LateUpdate()
	{
		if (m_updateVerts)
		{
			SetVerts();
		}
		if (m_updateUVs)
		{
			if (m_vectorLine.lineUVs.Length == m_mesh.vertexCount)
			{
				m_mesh.uv = m_vectorLine.lineUVs;
			}
			m_updateUVs = false;
		}
		if (m_updateColors)
		{
			if (m_vectorLine.lineColors.Length == m_mesh.vertexCount)
			{
				m_mesh.colors32 = m_vectorLine.lineColors;
			}
			m_updateColors = false;
		}
		if (m_updateTris)
		{
			m_mesh.SetTriangles(m_vectorLine.lineTriangles, 0);
			m_updateTris = false;
		}
		if (m_updateNormals)
		{
			m_mesh.RecalculateNormals();
			m_updateNormals = false;
		}
		if (m_updateTangents)
		{
			m_mesh.tangents = m_vectorLine.CalculateTangents(m_mesh.normals);
			m_updateTangents = false;
		}
	}

	public void SetVerts()
	{
		m_mesh.vertices = m_vectorLine.lineVertices;
		m_updateVerts = false;
		m_mesh.RecalculateBounds();
	}

	public void SetName(string name)
	{
		if (!((Object)(object)m_mesh == (Object)null))
		{
			((Object)m_mesh).name = name;
		}
	}

	public void UpdateVerts()
	{
		m_updateVerts = true;
	}

	public void UpdateUVs()
	{
		m_updateUVs = true;
	}

	public void UpdateColors()
	{
		m_updateColors = true;
	}

	public void UpdateNormals()
	{
		m_updateNormals = true;
	}

	public void UpdateTangents()
	{
		m_updateTangents = true;
	}

	public void UpdateTris()
	{
		m_updateTris = true;
	}

	public void UpdateMeshAttributes()
	{
		m_mesh.Clear();
		m_updateVerts = true;
		m_updateUVs = true;
		m_updateColors = true;
		m_updateTris = true;
	}

	public void ClearMesh()
	{
		if (!((Object)(object)m_mesh == (Object)null))
		{
			m_mesh.Clear();
		}
	}

	public int VertexCount()
	{
		return m_mesh.vertexCount;
	}
}
