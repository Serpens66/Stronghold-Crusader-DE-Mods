using System;
using UnityEngine;
using UnityEngine.UI;

namespace Vectrosity;

[Serializable]
public class VectorObject2D : RawImage, IVectorObject
{
	public bool m_updateVerts = true;

	public bool m_updateUVs = true;

	public bool m_updateColors = true;

	public bool m_updateNormals;

	public bool m_updateTangents;

	public bool m_updateTris = true;

	public Mesh m_mesh;

	public VectorLine vectorLine;

	public static VertexHelper vertexHelper;

	public void SetVectorLine(VectorLine vectorLine, Texture tex, Material mat, bool useCustomMaterial)
	{
		this.vectorLine = vectorLine;
		SetTexture(tex);
		SetMaterial(mat);
	}

	public void Destroy()
	{
		Object.Destroy((Object)(object)m_mesh);
	}

	public void DestroyNow()
	{
		Object.DestroyImmediate((Object)(object)m_mesh);
	}

	public void Enable(bool enable)
	{
		if (!((Object)(object)this == (Object)null))
		{
			((Behaviour)this).enabled = enable;
		}
	}

	public void SetTexture(Texture tex)
	{
		((RawImage)this).texture = tex;
	}

	public void SetMaterial(Material mat)
	{
		((Graphic)this).material = mat;
	}

	public override void UpdateGeometry()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)m_mesh == (Object)null)
		{
			SetupMesh();
		}
		if ((Object)(object)((Graphic)this).rectTransform != (Object)null)
		{
			Rect rect = ((Graphic)this).rectTransform.rect;
			if (((Rect)(ref rect)).width >= 0f)
			{
				rect = ((Graphic)this).rectTransform.rect;
				if (((Rect)(ref rect)).height >= 0f)
				{
					((Graphic)this).OnPopulateMesh(vertexHelper);
				}
			}
		}
		((Graphic)this).canvasRenderer.SetMesh(m_mesh);
	}

	public void SetupMesh()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		m_mesh = new Mesh();
		((Object)m_mesh).name = vectorLine.name;
		((Object)m_mesh).hideFlags = (HideFlags)61;
		SetMeshBounds();
	}

	public void SetMeshBounds()
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)m_mesh != (Object)null)
		{
			m_mesh.bounds = new Bounds(new Vector3((float)(Screen.width / 2), (float)(Screen.height / 2), 0f), new Vector3((float)Screen.width, (float)Screen.height, 0f));
		}
	}

	public override void OnPopulateMesh(VertexHelper vh)
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
		if (m_updateNormals && (Object)(object)m_mesh != (Object)null)
		{
			m_mesh.RecalculateNormals();
			m_updateNormals = false;
			((Graphic)this).UpdateGeometry();
		}
		if (m_updateTangents && (Object)(object)m_mesh != (Object)null)
		{
			m_mesh.tangents = vectorLine.CalculateTangents(m_mesh.normals);
			m_updateTangents = false;
		}
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
		((Graphic)this).SetVerticesDirty();
	}

	public void UpdateUVs()
	{
		m_updateUVs = true;
		((Graphic)this).SetVerticesDirty();
	}

	public void UpdateColors()
	{
		m_updateColors = true;
		((Graphic)this).SetVerticesDirty();
	}

	public void UpdateNormals()
	{
		m_updateNormals = true;
		((Graphic)this).SetVerticesDirty();
	}

	public void UpdateTangents()
	{
		m_updateTangents = true;
		((Graphic)this).SetVerticesDirty();
	}

	public void UpdateTris()
	{
		m_updateTris = true;
		((Graphic)this).SetVerticesDirty();
	}

	public void UpdateMeshAttributes()
	{
		if ((Object)(object)m_mesh != (Object)null)
		{
			m_mesh.Clear();
		}
		m_updateVerts = true;
		m_updateUVs = true;
		m_updateColors = true;
		m_updateTris = true;
		((Graphic)this).SetVerticesDirty();
		SetMeshBounds();
	}

	public void ClearMesh()
	{
		if (!((Object)(object)m_mesh == (Object)null))
		{
			m_mesh.Clear();
			((Graphic)this).UpdateGeometry();
		}
	}

	public int VertexCount()
	{
		return m_mesh.vertexCount;
	}
}
