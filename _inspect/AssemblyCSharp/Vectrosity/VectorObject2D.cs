using System;
using UnityEngine;
using UnityEngine.UI;

namespace Vectrosity;

[Serializable]
public class VectorObject2D : RawImage, IVectorObject
{
	private bool m_updateVerts = true;

	private bool m_updateUVs = true;

	private bool m_updateColors = true;

	private bool m_updateNormals;

	private bool m_updateTangents;

	private bool m_updateTris = true;

	private Mesh m_mesh;

	public VectorLine vectorLine;

	private static VertexHelper vertexHelper;

	public void SetVectorLine(VectorLine vectorLine, Texture tex, Material mat, bool useCustomMaterial)
	{
		this.vectorLine = vectorLine;
		SetTexture(tex);
		SetMaterial(mat);
	}

	public void Destroy()
	{
		UnityEngine.Object.Destroy(m_mesh);
	}

	public void DestroyNow()
	{
		UnityEngine.Object.DestroyImmediate(m_mesh);
	}

	public void Enable(bool enable)
	{
		if (!(this == null))
		{
			base.enabled = enable;
		}
	}

	public void SetTexture(Texture tex)
	{
		base.texture = tex;
	}

	public void SetMaterial(Material mat)
	{
		material = mat;
	}

	protected override void UpdateGeometry()
	{
		if (m_mesh == null)
		{
			SetupMesh();
		}
		if (base.rectTransform != null && base.rectTransform.rect.width >= 0f && base.rectTransform.rect.height >= 0f)
		{
			OnPopulateMesh(vertexHelper);
		}
		base.canvasRenderer.SetMesh(m_mesh);
	}

	private void SetupMesh()
	{
		m_mesh = new Mesh();
		m_mesh.name = vectorLine.name;
		m_mesh.hideFlags = HideFlags.HideAndDontSave;
		SetMeshBounds();
	}

	private void SetMeshBounds()
	{
		if (m_mesh != null)
		{
			m_mesh.bounds = new Bounds(new Vector3(Screen.width / 2, Screen.height / 2, 0f), new Vector3(Screen.width, Screen.height, 0f));
		}
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		if (m_updateVerts)
		{
			m_mesh.vertices = vectorLine.lineVertices;
			m_updateVerts = false;
		}
		if (m_updateUVs)
		{
			if (vectorLine.lineUVs.Length == m_mesh.vertexCount)
			{
				m_mesh.uv = vectorLine.lineUVs;
			}
			m_updateUVs = false;
		}
		if (m_updateColors)
		{
			if (vectorLine.lineColors.Length == m_mesh.vertexCount)
			{
				m_mesh.colors32 = vectorLine.lineColors;
			}
			m_updateColors = false;
		}
		if (m_updateTris)
		{
			m_mesh.SetTriangles(vectorLine.lineTriangles, 0);
			m_updateTris = false;
			SetMeshBounds();
		}
		if (m_updateNormals && m_mesh != null)
		{
			m_mesh.RecalculateNormals();
			m_updateNormals = false;
			UpdateGeometry();
		}
		if (m_updateTangents && m_mesh != null)
		{
			m_mesh.tangents = vectorLine.CalculateTangents(m_mesh.normals);
			m_updateTangents = false;
		}
	}

	public void SetName(string name)
	{
		if (!(m_mesh == null))
		{
			m_mesh.name = name;
		}
	}

	public void UpdateVerts()
	{
		m_updateVerts = true;
		SetVerticesDirty();
	}

	public void UpdateUVs()
	{
		m_updateUVs = true;
		SetVerticesDirty();
	}

	public void UpdateColors()
	{
		m_updateColors = true;
		SetVerticesDirty();
	}

	public void UpdateNormals()
	{
		m_updateNormals = true;
		SetVerticesDirty();
	}

	public void UpdateTangents()
	{
		m_updateTangents = true;
		SetVerticesDirty();
	}

	public void UpdateTris()
	{
		m_updateTris = true;
		SetVerticesDirty();
	}

	public void UpdateMeshAttributes()
	{
		if (m_mesh != null)
		{
			m_mesh.Clear();
		}
		m_updateVerts = true;
		m_updateUVs = true;
		m_updateColors = true;
		m_updateTris = true;
		SetVerticesDirty();
		SetMeshBounds();
	}

	public void ClearMesh()
	{
		if (!(m_mesh == null))
		{
			m_mesh.Clear();
			UpdateGeometry();
		}
	}

	public int VertexCount()
	{
		return m_mesh.vertexCount;
	}
}
