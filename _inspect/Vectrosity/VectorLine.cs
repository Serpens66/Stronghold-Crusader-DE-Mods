using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vectrosity;

[Serializable]
public class VectorLine
{
	public enum FunctionName
	{
		SetColors,
		SetWidths,
		MakeCurve,
		MakeSpline,
		MakeEllipse
	}

	[SerializeField]
	public Vector3[] m_lineVertices;

	[SerializeField]
	public Vector2[] m_lineUVs;

	[SerializeField]
	public Color32[] m_lineColors;

	[SerializeField]
	public List<int> m_lineTriangles;

	[SerializeField]
	public int m_vertexCount;

	[SerializeField]
	public GameObject m_go;

	[SerializeField]
	public RectTransform m_rectTransform;

	public IVectorObject m_vectorObject;

	[SerializeField]
	public Color32 m_color;

	[SerializeField]
	public CanvasState m_canvasState;

	[SerializeField]
	public bool m_is2D;

	[SerializeField]
	public List<Vector2> m_points2;

	[SerializeField]
	public List<Vector3> m_points3;

	[SerializeField]
	public int m_pointsCount;

	[SerializeField]
	public Vector3[] m_screenPoints;

	[SerializeField]
	public float[] m_lineWidths;

	[SerializeField]
	public float m_lineWidth;

	[SerializeField]
	public float m_maxWeldDistance;

	[SerializeField]
	public float[] m_distances;

	[SerializeField]
	public string m_name;

	[SerializeField]
	public Material m_material;

	[SerializeField]
	public Texture m_originalTexture;

	[SerializeField]
	public Texture m_texture;

	[SerializeField]
	public bool m_active = true;

	[SerializeField]
	public LineType m_lineType;

	[SerializeField]
	public float m_capLength;

	[SerializeField]
	public bool m_smoothWidth;

	[SerializeField]
	public bool m_smoothColor;

	[SerializeField]
	public Joins m_joins;

	[SerializeField]
	public bool m_isAutoDrawing;

	[SerializeField]
	public int m_drawStart;

	[SerializeField]
	public int m_drawEnd;

	[SerializeField]
	public int m_endPointsUpdate;

	[SerializeField]
	public bool m_useNormals;

	[SerializeField]
	public bool m_useTangents;

	[SerializeField]
	public bool m_normalsCalculated;

	[SerializeField]
	public bool m_tangentsCalculated;

	[SerializeField]
	public EndCap m_capType = EndCap.None;

	[SerializeField]
	public string m_endCap;

	[SerializeField]
	public bool m_useCapColors;

	[SerializeField]
	public Color32 m_frontColor;

	[SerializeField]
	public Color32 m_backColor;

	[SerializeField]
	public int m_frontEndCapIndex = -1;

	[SerializeField]
	public int m_backEndCapIndex = -1;

	[SerializeField]
	public float m_lineUVBottom;

	[SerializeField]
	public float m_lineUVTop;

	[SerializeField]
	public float m_frontCapUVBottom;

	[SerializeField]
	public float m_frontCapUVTop;

	[SerializeField]
	public float m_backCapUVBottom;

	[SerializeField]
	public float m_backCapUVTop;

	[SerializeField]
	public bool m_continuousTexture;

	[SerializeField]
	public Transform m_drawTransform;

	[SerializeField]
	public bool m_viewportDraw;

	[SerializeField]
	public float m_textureScale;

	[SerializeField]
	public bool m_useTextureScale;

	[SerializeField]
	public float m_textureOffset;

	[SerializeField]
	public bool m_useMatrix;

	[SerializeField]
	public Matrix4x4 m_matrix;

	[SerializeField]
	public bool m_collider;

	[SerializeField]
	public bool m_trigger;

	[SerializeField]
	public PhysicsMaterial2D m_physicsMaterial;

	[SerializeField]
	public bool m_alignOddWidthToPixels;

	public static Vector3 v3zero = Vector3.zero;

	public static Canvas m_canvas;

	public static Transform camTransform;

	public static Camera cam3D;

	public static Vector3 oldPosition;

	public static Vector3 oldRotation;

	public static bool lineManagerCreated = false;

	public static LineManager m_lineManager;

	public static Dictionary<string, CapInfo> capDictionary;

	public static int endianDiff1;

	public static int endianDiff2;

	public static byte[] byteBlock;

	public static string[] functionNames = new string[5] { "VectorLine.SetColors: Length of color", "VectorLine.SetWidths: Length of line widths", "MakeCurve", "MakeSpline", "MakeEllipse" };

	public Vector3[] lineVertices => m_lineVertices;

	public Vector2[] lineUVs => m_lineUVs;

	public Color32[] lineColors => m_lineColors;

	public List<int> lineTriangles => m_lineTriangles;

	public RectTransform rectTransform
	{
		get
		{
			if ((Object)(object)m_go != (Object)null)
			{
				return m_rectTransform;
			}
			return null;
		}
	}

	public Color32 color
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return m_color;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			m_color = value;
			SetColor(value);
		}
	}

	public bool is2D => m_is2D;

	public List<Vector2> points2
	{
		get
		{
			if (!m_is2D)
			{
				Debug.LogError((object)("Line \"" + name + "\" uses points3 rather than points2"));
				return null;
			}
			return m_points2;
		}
		set
		{
			if (value == null)
			{
				Debug.LogError((object)("List for Line \"" + name + "\" must not be null"));
			}
			else
			{
				m_points2 = value;
			}
		}
	}

	public List<Vector3> points3
	{
		get
		{
			if (m_is2D)
			{
				Debug.LogError((object)("Line \"" + name + "\" uses points2 rather than points3"));
				return null;
			}
			return m_points3;
		}
		set
		{
			if (value == null)
			{
				Debug.LogError((object)("List for Line \"" + name + "\" must not be null"));
			}
			else
			{
				m_points3 = value;
			}
		}
	}

	public int pointsCount
	{
		get
		{
			if (!m_is2D)
			{
				return m_points3.Count;
			}
			return m_points2.Count;
		}
	}

	public float lineWidth
	{
		get
		{
			return m_lineWidth;
		}
		set
		{
			m_lineWidth = value;
			float num = value * 0.5f;
			for (int i = 0; i < m_lineWidths.Length; i++)
			{
				m_lineWidths[i] = num;
			}
			m_maxWeldDistance = value * 2f * (value * 2f);
		}
	}

	public float maxWeldDistance
	{
		get
		{
			return Mathf.Sqrt(m_maxWeldDistance);
		}
		set
		{
			m_maxWeldDistance = value * value;
		}
	}

	public string name
	{
		get
		{
			return m_name;
		}
		set
		{
			m_name = value;
			if ((Object)(object)m_go != (Object)null)
			{
				((Object)m_go).name = value;
			}
			if (m_vectorObject != null)
			{
				m_vectorObject.SetName(value);
			}
		}
	}

	public Material material
	{
		get
		{
			return m_material;
		}
		set
		{
			if (m_vectorObject != null)
			{
				m_vectorObject.SetMaterial(value);
			}
			m_material = value;
		}
	}

	public Texture texture
	{
		get
		{
			return m_texture;
		}
		set
		{
			if (m_capType != EndCap.None)
			{
				m_originalTexture = value;
				return;
			}
			if (m_vectorObject != null)
			{
				m_vectorObject.SetTexture(value);
			}
			m_texture = value;
		}
	}

	public int layer
	{
		get
		{
			if ((Object)(object)m_go != (Object)null)
			{
				return m_go.layer;
			}
			return 0;
		}
		set
		{
			if ((Object)(object)m_go != (Object)null)
			{
				m_go.layer = Mathf.Clamp(value, 0, 31);
			}
		}
	}

	public bool active
	{
		get
		{
			return m_active;
		}
		set
		{
			m_active = value;
			if (m_vectorObject != null)
			{
				m_vectorObject.Enable(value);
			}
		}
	}

	public LineType lineType
	{
		get
		{
			return m_lineType;
		}
		set
		{
			if (value == m_lineType)
			{
				return;
			}
			m_lineType = value;
			if (value == LineType.Points || (value == LineType.Discrete && m_joins == Joins.Fill))
			{
				m_joins = Joins.None;
			}
			if (value == LineType.Discrete)
			{
				drawStart = m_drawStart;
				drawEnd = m_drawEnd;
			}
			if (value != LineType.Continuous && ((m_points2 != null && m_points2.Count > 16383) || (m_points3 != null && m_points3.Count > 16383)))
			{
				Resize(16383);
			}
			if (collider)
			{
				Collider2D component = m_go.GetComponent<Collider2D>();
				if ((Object)(object)component != (Object)null)
				{
					Object.DestroyImmediate((Object)(object)component);
				}
				AddColliderIfNeeded();
			}
			ResetLine();
		}
	}

	public float capLength
	{
		get
		{
			return m_capLength;
		}
		set
		{
			if (m_lineType == LineType.Points)
			{
				Debug.LogError((object)"LineType.Points can't use capLength");
			}
			else
			{
				m_capLength = value;
			}
		}
	}

	public bool smoothWidth
	{
		get
		{
			return m_smoothWidth;
		}
		set
		{
			m_smoothWidth = m_lineType != LineType.Points && value;
		}
	}

	public bool smoothColor
	{
		get
		{
			return m_smoothColor;
		}
		set
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			bool flag = m_smoothColor;
			m_smoothColor = m_lineType != LineType.Points && value;
			if (m_smoothColor != flag)
			{
				int segmentNumber = GetSegmentNumber();
				for (int i = 0; i < segmentNumber; i++)
				{
					SetColor(GetColor(i), i);
				}
			}
		}
	}

	public Joins joins
	{
		get
		{
			return m_joins;
		}
		set
		{
			if (m_lineType == LineType.Points || (m_lineType == LineType.Discrete && value == Joins.Fill))
			{
				return;
			}
			if ((m_joins == Joins.Fill && value != Joins.Fill) || (m_joins != Joins.Fill && value == Joins.Fill))
			{
				m_joins = value;
				ClearTriangles();
				SetupTriangles(0);
			}
			m_joins = value;
			if (m_joins == Joins.Weld)
			{
				if (m_canvasState == CanvasState.OnCanvas)
				{
					Draw();
				}
				else if (m_canvasState == CanvasState.OffCanvas)
				{
					Draw3D();
				}
			}
		}
	}

	public bool isAutoDrawing => m_isAutoDrawing;

	public int drawStart
	{
		get
		{
			return m_drawStart;
		}
		set
		{
			if (m_lineType == LineType.Discrete && (value & 1) != 0)
			{
				value++;
			}
			m_drawStart = Mathf.Clamp(value, 0, pointsCount - 1);
		}
	}

	public int drawEnd
	{
		get
		{
			return m_drawEnd;
		}
		set
		{
			if (m_lineType == LineType.Discrete && value != 0 && (value & 1) == 0)
			{
				value++;
			}
			m_drawEnd = Mathf.Clamp(value, 0, pointsCount - 1);
		}
	}

	public int endPointsUpdate
	{
		get
		{
			if (m_lineType != LineType.Discrete)
			{
				return m_endPointsUpdate;
			}
			if (m_endPointsUpdate != 0)
			{
				return m_endPointsUpdate + 1;
			}
			return 0;
		}
		set
		{
			if (m_lineType == LineType.Discrete && value > 1 && (value & 1) == 0)
			{
				value--;
			}
			m_endPointsUpdate = Mathf.Max(0, value);
		}
	}

	public string endCap
	{
		get
		{
			return m_endCap;
		}
		set
		{
			if (m_lineType == LineType.Points)
			{
				Debug.LogError((object)"LineType.Points can't use end caps");
			}
			else
			{
				if (m_endCap == value)
				{
					return;
				}
				if (value == null || value == "")
				{
					RemoveEndCap();
					return;
				}
				if (capDictionary == null || !capDictionary.ContainsKey(value))
				{
					Debug.LogError((object)("End cap \"" + value + "\" is not set up"));
					return;
				}
				if (m_capType != EndCap.None)
				{
					RemoveEndCap();
				}
				m_endCap = value;
				m_capType = capDictionary[value].capType;
				if (m_capType != EndCap.None)
				{
					SetupEndCap(capDictionary[value].uvHeights);
				}
			}
		}
	}

	public bool continuousTexture
	{
		get
		{
			return m_continuousTexture;
		}
		set
		{
			m_continuousTexture = value;
			if (!value)
			{
				ResetTextureScale();
			}
		}
	}

	public Transform drawTransform
	{
		get
		{
			return m_drawTransform;
		}
		set
		{
			m_drawTransform = value;
		}
	}

	public bool useViewportCoords
	{
		get
		{
			return m_viewportDraw;
		}
		set
		{
			if (m_is2D)
			{
				m_viewportDraw = value;
			}
			else
			{
				Debug.LogError((object)"Line must use Vector2 points in order to use viewport coords");
			}
		}
	}

	[SerializeField]
	public float textureScale
	{
		get
		{
			return m_textureScale;
		}
		set
		{
			m_textureScale = value;
			if (m_textureScale == 0f)
			{
				m_useTextureScale = false;
				ResetTextureScale();
			}
			else
			{
				m_useTextureScale = true;
			}
		}
	}

	public float textureOffset
	{
		get
		{
			return m_textureOffset;
		}
		set
		{
			m_textureOffset = value;
			SetTextureScale();
		}
	}

	public Matrix4x4 matrix
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return m_matrix;
		}
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			m_matrix = value;
			m_useMatrix = m_matrix != Matrix4x4.identity;
		}
	}

	public int drawDepth
	{
		get
		{
			if (m_canvasState == CanvasState.OffCanvas)
			{
				Debug.LogError((object)"VectorLine.drawDepth can't be used with lines made with Draw3D");
				return 0;
			}
			return m_go.transform.GetSiblingIndex();
		}
		set
		{
			if (m_canvasState == CanvasState.OffCanvas)
			{
				Debug.LogError((object)"VectorLine.drawDepth can't be used with lines made with Draw3D");
			}
			else
			{
				m_go.transform.SetSiblingIndex(value);
			}
		}
	}

	public bool collider
	{
		get
		{
			return m_collider;
		}
		set
		{
			m_collider = value;
			AddColliderIfNeeded();
			((Behaviour)m_go.GetComponent<Collider2D>()).enabled = value;
		}
	}

	public bool trigger
	{
		get
		{
			return m_trigger;
		}
		set
		{
			m_trigger = value;
			if ((Object)(object)m_go.GetComponent<Collider2D>() != (Object)null)
			{
				m_go.GetComponent<Collider2D>().isTrigger = value;
			}
		}
	}

	public PhysicsMaterial2D physicsMaterial
	{
		get
		{
			return m_physicsMaterial;
		}
		set
		{
			AddColliderIfNeeded();
			m_physicsMaterial = value;
			m_go.GetComponent<Collider2D>().sharedMaterial = value;
		}
	}

	public bool alignOddWidthToPixels
	{
		get
		{
			return m_alignOddWidthToPixels;
		}
		set
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			float num = (value ? 0.5f : 0f);
			m_rectTransform.anchoredPosition = new Vector2(num, num);
			m_alignOddWidthToPixels = value;
		}
	}

	public static Canvas canvas
	{
		get
		{
			if ((Object)(object)m_canvas == (Object)null)
			{
				SetupVectorCanvas();
			}
			return m_canvas;
		}
	}

	public static Vector3 camTransformPosition => camTransform.position;

	public static bool camTransformExists => (Object)(object)camTransform != (Object)null;

	public static LineManager lineManager
	{
		get
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			if (!lineManagerCreated)
			{
				lineManagerCreated = true;
				m_lineManager = new GameObject("LineManager").AddComponent<LineManager>();
				((Behaviour)m_lineManager).enabled = false;
				Object.DontDestroyOnLoad((Object)(object)m_lineManager);
			}
			return m_lineManager;
		}
	}

	public static string Version()
	{
		return "Vectrosity version 5.6";
	}

	public void AddColliderIfNeeded()
	{
		if ((Object)(object)m_go.GetComponent<Collider2D>() == (Object)null)
		{
			m_go.AddComponent((m_lineType == LineType.Continuous) ? typeof(EdgeCollider2D) : typeof(PolygonCollider2D));
			m_go.GetComponent<Collider2D>().isTrigger = m_trigger;
			m_go.GetComponent<Collider2D>().sharedMaterial = m_physicsMaterial;
		}
	}

	public VectorLine(string name, List<Vector3> points, float width)
	{
		m_points3 = points;
		SetupLine(name, null, width, LineType.Discrete, Joins.None, use2D: false);
	}

	public VectorLine(string name, List<Vector3> points, Texture texture, float width)
	{
		m_points3 = points;
		SetupLine(name, texture, width, LineType.Discrete, Joins.None, use2D: false);
	}

	public VectorLine(string name, List<Vector3> points, float width, LineType lineType)
	{
		m_points3 = points;
		SetupLine(name, null, width, lineType, Joins.None, use2D: false);
	}

	public VectorLine(string name, List<Vector3> points, Texture texture, float width, LineType lineType)
	{
		m_points3 = points;
		SetupLine(name, texture, width, lineType, Joins.None, use2D: false);
	}

	public VectorLine(string name, List<Vector3> points, float width, LineType lineType, Joins joins)
	{
		m_points3 = points;
		SetupLine(name, null, width, lineType, joins, use2D: false);
	}

	public VectorLine(string name, List<Vector3> points, Texture texture, float width, LineType lineType, Joins joins)
	{
		m_points3 = points;
		SetupLine(name, texture, width, lineType, joins, use2D: false);
	}

	public VectorLine(string name, List<Vector2> points, float width)
	{
		m_points2 = points;
		SetupLine(name, null, width, LineType.Discrete, Joins.None, use2D: true);
	}

	public VectorLine(string name, List<Vector2> points, Texture texture, float width)
	{
		m_points2 = points;
		SetupLine(name, texture, width, LineType.Discrete, Joins.None, use2D: true);
	}

	public VectorLine(string name, List<Vector2> points, float width, LineType lineType)
	{
		m_points2 = points;
		SetupLine(name, null, width, lineType, Joins.None, use2D: true);
	}

	public VectorLine(string name, List<Vector2> points, Texture texture, float width, LineType lineType)
	{
		m_points2 = points;
		SetupLine(name, texture, width, lineType, Joins.None, use2D: true);
	}

	public VectorLine(string name, List<Vector2> points, float width, LineType lineType, Joins joins)
	{
		m_points2 = points;
		SetupLine(name, null, width, lineType, joins, use2D: true);
	}

	public VectorLine(string name, List<Vector2> points, Texture texture, float width, LineType lineType, Joins joins)
	{
		m_points2 = points;
		SetupLine(name, texture, width, lineType, joins, use2D: true);
	}

	public void SetupLine(string lineName, Texture texture, float width, LineType lineType, Joins joins, bool use2D)
	{
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Expected O, but got Unknown
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		m_is2D = use2D;
		m_lineType = lineType;
		if (joins == Joins.Fill && m_lineType != LineType.Continuous)
		{
			Debug.LogError((object)("VectorLine: Must use LineType.Continuous if using Joins.Fill for \"" + lineName + "\""));
			return;
		}
		if (joins == Joins.Weld && m_lineType == LineType.Points)
		{
			Debug.LogError((object)("VectorLine: LineType.Points can't use Joins.Weld for \"" + lineName + "\""));
			return;
		}
		if ((m_is2D && m_points2 == null) || (!m_is2D && m_points3 == null))
		{
			Debug.LogError((object)("VectorLine: the points array is null for \"" + lineName + "\""));
			return;
		}
		if (m_is2D)
		{
			m_pointsCount = ((m_points2.Capacity > 0 && m_points2.Count == 0) ? m_points2.Capacity : m_points2.Count);
			int num = m_pointsCount - m_points2.Count;
			for (int i = 0; i < num; i++)
			{
				m_points2.Add(Vector2.zero);
			}
		}
		else
		{
			m_pointsCount = ((m_points3.Capacity > 0 && m_points3.Count == 0) ? m_points3.Capacity : m_points3.Count);
			int num2 = m_pointsCount - m_points3.Count;
			for (int j = 0; j < num2; j++)
			{
				m_points3.Add(Vector3.zero);
			}
		}
		name = lineName;
		if (SetVertexCount())
		{
			m_go = new GameObject(name);
			m_canvasState = CanvasState.None;
			layer = LayerMask.NameToLayer("UI");
			m_rectTransform = m_go.AddComponent<RectTransform>();
			SetupTransform(m_rectTransform);
			m_texture = texture;
			m_lineVertices = (Vector3[])(object)new Vector3[m_vertexCount];
			m_lineUVs = (Vector2[])(object)new Vector2[m_vertexCount];
			m_lineColors = (Color32[])(object)new Color32[m_vertexCount];
			m_lineUVBottom = 0f;
			m_lineUVTop = 1f;
			SetUVs(0, GetSegmentNumber());
			m_lineTriangles = new List<int>();
			color = Color32.op_Implicit(Color.white);
			m_maxWeldDistance = width * 2f * (width * 2f);
			m_joins = joins;
			m_lineWidths = new float[1];
			m_lineWidths[0] = width * 0.5f;
			m_lineWidth = width;
			if (!m_is2D)
			{
				m_screenPoints = (Vector3[])(object)new Vector3[m_vertexCount];
			}
			m_drawStart = 0;
			m_drawEnd = m_pointsCount - 1;
			SetupTriangles(0);
		}
	}

	public void SetupTriangles(int startVert)
	{
		int num = 0;
		int num2 = 0;
		if (pointsCount > 0)
		{
			if (m_lineType == LineType.Points)
			{
				num = pointsCount * 6;
				num2 = pointsCount * 4;
			}
			else if (m_lineType == LineType.Continuous)
			{
				num = ((m_joins == Joins.Fill) ? ((pointsCount - 1) * 12) : ((pointsCount - 1) * 6));
				num2 = (pointsCount - 1) * 4;
			}
			else
			{
				num = pointsCount / 2 * 6;
				num2 = pointsCount * 2;
			}
		}
		if (m_capType != EndCap.None)
		{
			num += 12;
		}
		if (m_lineTriangles.Count > num)
		{
			m_lineTriangles.RemoveRange(num, m_lineTriangles.Count - num);
			if (m_joins == Joins.Fill)
			{
				SetLastFillTriangles();
			}
			else if (m_vectorObject != null)
			{
				m_vectorObject.UpdateTris();
			}
			return;
		}
		if (m_joins == Joins.Fill)
		{
			if (startVert >= 4)
			{
				int num3 = m_lineTriangles.Count - 6;
				m_lineTriangles[num3] = startVert - 3;
				m_lineTriangles[num3 + 1] = startVert;
				m_lineTriangles[num3 + 2] = startVert + 3;
				m_lineTriangles[num3 + 3] = startVert - 2;
				m_lineTriangles[num3 + 4] = startVert;
				m_lineTriangles[num3 + 5] = startVert + 3;
			}
			for (int i = startVert; i < num2; i += 4)
			{
				m_lineTriangles.Add(i);
				m_lineTriangles.Add(i + 1);
				m_lineTriangles.Add(i + 3);
				m_lineTriangles.Add(i + 1);
				m_lineTriangles.Add(i + 2);
				m_lineTriangles.Add(i + 3);
				m_lineTriangles.Add(i + 1);
				m_lineTriangles.Add(i + 4);
				m_lineTriangles.Add(i + 7);
				m_lineTriangles.Add(i + 2);
				m_lineTriangles.Add(i + 4);
				m_lineTriangles.Add(i + 7);
			}
			SetLastFillTriangles();
		}
		else
		{
			for (int j = startVert; j < num2; j += 4)
			{
				m_lineTriangles.Add(j);
				m_lineTriangles.Add(j + 1);
				m_lineTriangles.Add(j + 3);
				m_lineTriangles.Add(j + 1);
				m_lineTriangles.Add(j + 2);
				m_lineTriangles.Add(j + 3);
			}
		}
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateTris();
		}
	}

	public void SetLastFillTriangles()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		if (pointsCount < 2)
		{
			return;
		}
		int num = (pointsCount - 1) * 12 + ((m_capType != EndCap.None) ? 12 : 0);
		bool flag = false;
		if ((m_is2D && m_points2[0] == m_points2[points2.Count - 1]) || (!m_is2D && m_points3[0] == m_points3[points3.Count - 1]))
		{
			if (m_lineTriangles[num - 4] != 3 && m_lineTriangles[num - 1] != 3)
			{
				flag = true;
			}
			m_lineTriangles[num - 6] = m_vertexCount - 3;
			m_lineTriangles[num - 5] = 0;
			m_lineTriangles[num - 4] = 3;
			m_lineTriangles[num - 3] = m_vertexCount - 2;
			m_lineTriangles[num - 2] = 0;
			m_lineTriangles[num - 1] = 3;
		}
		else
		{
			if (m_lineTriangles[num - 4] == 3 && m_lineTriangles[num - 1] == 3)
			{
				flag = true;
			}
			m_lineTriangles[num - 6] = 0;
			m_lineTriangles[num - 5] = 0;
			m_lineTriangles[num - 4] = 0;
			m_lineTriangles[num - 3] = 0;
			m_lineTriangles[num - 2] = 0;
			m_lineTriangles[num - 1] = 0;
		}
		if (flag && m_vectorObject != null)
		{
			m_vectorObject.UpdateTris();
		}
	}

	public void SetupEndCap(float[] uvHeights)
	{
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		int num = m_vertexCount + 8;
		if (num > 65534)
		{
			Debug.LogError((object)("VectorLine: exceeded maximum vertex count of 65534 for \"" + m_name + "\"...use fewer points"));
			return;
		}
		ResizeMeshArrays(num);
		int num2 = 0;
		if (m_joins == Joins.Fill)
		{
			for (int i = num - 8; i < num; i += 4)
			{
				m_lineTriangles.Insert(num2, i);
				m_lineTriangles.Insert(1 + num2, i + 1);
				m_lineTriangles.Insert(2 + num2, i + 3);
				m_lineTriangles.Insert(3 + num2, i + 1);
				m_lineTriangles.Insert(4 + num2, i + 2);
				m_lineTriangles.Insert(5 + num2, i + 3);
				num2 += 6;
			}
		}
		else
		{
			for (int j = num - 8; j < num; j += 4)
			{
				m_lineTriangles.Insert(num2, j);
				m_lineTriangles.Insert(1 + num2, j + 1);
				m_lineTriangles.Insert(2 + num2, j + 3);
				m_lineTriangles.Insert(3 + num2, j + 1);
				m_lineTriangles.Insert(4 + num2, j + 2);
				m_lineTriangles.Insert(5 + num2, j + 3);
				num2 += 6;
			}
		}
		int num3 = ((num >= 12) ? (num - 12) : 0);
		for (int k = num - 8; k < num - 4; k++)
		{
			m_lineColors[k] = m_lineColors[0];
			m_lineColors[k + 4] = m_lineColors[num3];
		}
		m_lineUVBottom = uvHeights[0];
		m_lineUVTop = uvHeights[1];
		m_backCapUVBottom = uvHeights[2];
		m_backCapUVTop = uvHeights[3];
		m_frontCapUVBottom = uvHeights[4];
		m_frontCapUVTop = uvHeights[5];
		SetUVs(0, GetSegmentNumber());
		SetEndCapUVs();
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateTris();
			m_vectorObject.UpdateUVs();
		}
		SetEndCapColors();
		m_originalTexture = m_texture;
		m_texture = capDictionary[m_endCap].texture;
		if (m_vectorObject != null)
		{
			m_vectorObject.SetTexture(m_texture);
		}
	}

	public void ResetLine()
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		SetVertexCount();
		m_lineVertices = (Vector3[])(object)new Vector3[m_vertexCount];
		m_lineUVs = (Vector2[])(object)new Vector2[m_vertexCount];
		m_lineColors = (Color32[])(object)new Color32[m_vertexCount];
		if (!m_is2D)
		{
			m_screenPoints = (Vector3[])(object)new Vector3[m_vertexCount];
		}
		SetUVs(0, GetSegmentNumber());
		SetColor(m_color);
		int segmentNumber = GetSegmentNumber();
		SetupWidths(segmentNumber);
		ClearTriangles();
		SetupTriangles(0);
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateMeshAttributes();
		}
		if (m_canvasState == CanvasState.OnCanvas)
		{
			Draw();
		}
		else if (m_canvasState == CanvasState.OffCanvas)
		{
			Draw3D();
		}
	}

	public void SetEndCapUVs()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		m_lineUVs[m_vertexCount + 3] = new Vector2(0f, m_frontCapUVTop);
		m_lineUVs[m_vertexCount] = new Vector2(1f, m_frontCapUVTop);
		m_lineUVs[m_vertexCount + 1] = new Vector2(1f, m_frontCapUVBottom);
		m_lineUVs[m_vertexCount + 2] = new Vector2(0f, m_frontCapUVBottom);
		if (capDictionary[m_endCap].capType == EndCap.Mirror)
		{
			m_lineUVs[m_vertexCount + 7] = new Vector2(0f, m_frontCapUVBottom);
			m_lineUVs[m_vertexCount + 4] = new Vector2(1f, m_frontCapUVBottom);
			m_lineUVs[m_vertexCount + 5] = new Vector2(1f, m_frontCapUVTop);
			m_lineUVs[m_vertexCount + 6] = new Vector2(0f, m_frontCapUVTop);
		}
		else
		{
			m_lineUVs[m_vertexCount + 7] = new Vector2(0f, m_backCapUVTop);
			m_lineUVs[m_vertexCount + 4] = new Vector2(1f, m_backCapUVTop);
			m_lineUVs[m_vertexCount + 5] = new Vector2(1f, m_backCapUVBottom);
			m_lineUVs[m_vertexCount + 6] = new Vector2(0f, m_backCapUVBottom);
		}
	}

	public void RemoveEndCap()
	{
		if (m_capType != EndCap.None)
		{
			m_endCap = null;
			m_capType = EndCap.None;
			ResizeMeshArrays(m_vertexCount);
			m_lineTriangles.RemoveRange(0, 12);
			m_lineUVBottom = 0f;
			m_lineUVTop = 1f;
			SetUVs(0, GetSegmentNumber());
			if (m_useTextureScale)
			{
				SetTextureScale();
			}
			texture = m_originalTexture;
			m_vectorObject.UpdateMeshAttributes();
			if (m_collider)
			{
				SetCollider(m_canvasState == CanvasState.OnCanvas);
			}
		}
	}

	public static void SetupTransform(RectTransform rectTransform)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.zero;
		rectTransform.pivot = Vector2.zero;
		rectTransform.anchoredPosition = Vector2.zero;
	}

	public void ResizeMeshArrays(int newCount)
	{
		Array.Resize(ref m_lineVertices, newCount);
		Array.Resize(ref m_lineUVs, newCount);
		Array.Resize(ref m_lineColors, newCount);
	}

	public void Resize(int newCount)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (newCount < 0)
		{
			Debug.LogError((object)"VectorLine.Resize: the new count must be >= 0");
		}
		else
		{
			if (newCount == pointsCount)
			{
				return;
			}
			if (m_is2D)
			{
				if (newCount > m_pointsCount)
				{
					for (int i = 0; i < newCount - m_pointsCount; i++)
					{
						m_points2.Add(Vector2.zero);
					}
				}
				else
				{
					m_points2.RemoveRange(newCount, m_pointsCount - newCount);
				}
			}
			else if (newCount > m_pointsCount)
			{
				for (int j = 0; j < newCount - m_pointsCount; j++)
				{
					m_points3.Add(v3zero);
				}
			}
			else
			{
				m_points3.RemoveRange(newCount, m_pointsCount - newCount);
			}
			Resize();
		}
	}

	public void Resize()
	{
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		int num = m_pointsCount;
		int num2 = m_pointsCount;
		if (m_lineType != LineType.Points)
		{
			num2 = ((m_lineType == LineType.Continuous) ? Mathf.Max(0, m_pointsCount - 1) : (m_pointsCount / 2));
		}
		bool flag = m_drawEnd == m_pointsCount - 1 || m_drawEnd < 1;
		if (!SetVertexCount())
		{
			return;
		}
		m_pointsCount = pointsCount;
		int num3 = m_lineVertices.Length - ((m_capType != EndCap.None) ? 8 : 0);
		if (num3 < m_vertexCount)
		{
			if (num3 == 0)
			{
				num3 = 4;
			}
			while (num3 < m_pointsCount)
			{
				num3 *= 2;
			}
			num3 = Mathf.Min(num3, MaxPoints());
			ResizeMeshArrays((m_capType == EndCap.None) ? (num3 * 4) : (num3 * 4 + 8));
			if (!m_is2D)
			{
				Array.Resize(ref m_screenPoints, num3 * 4);
			}
		}
		if (m_lineWidths.Length > 1)
		{
			if (m_lineType != LineType.Points)
			{
				num3 = ((m_lineType == LineType.Continuous) ? (num3 - 1) : (num3 / 2));
			}
			if (num3 > m_lineWidths.Length)
			{
				ResizeLineWidths(num3);
			}
		}
		if (flag)
		{
			m_drawEnd = m_pointsCount - 1;
		}
		m_drawStart = Mathf.Clamp(m_drawStart, 0, m_pointsCount - 1);
		m_drawEnd = Mathf.Clamp(m_drawEnd, 0, m_pointsCount - 1);
		if (m_pointsCount > num2)
		{
			SetColor(m_color, num2, GetSegmentNumber());
			SetUVs(num2, GetSegmentNumber());
		}
		if (m_pointsCount < num)
		{
			ZeroVertices(m_pointsCount, num);
		}
		if (m_capType != EndCap.None)
		{
			SetEndCapUVs();
			SetEndCapColors();
		}
		SetupTriangles(num2 * 4);
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateMeshAttributes();
		}
	}

	public void ResizeLineWidths(int newSize)
	{
		if (newSize > m_lineWidths.Length)
		{
			float[] array = new float[newSize];
			for (int i = 0; i < m_lineWidths.Length; i++)
			{
				array[i] = m_lineWidths[i];
			}
			for (int j = m_lineWidths.Length; j < newSize; j++)
			{
				array[j] = m_lineWidth * 0.5f;
			}
			m_lineWidths = array;
		}
	}

	public void SetUVs(int startIndex, int endIndex)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = default(Vector2);
		((Vector2)(ref val))._002Ector(0f, m_lineUVTop);
		Vector2 val2 = default(Vector2);
		((Vector2)(ref val2))._002Ector(1f, m_lineUVTop);
		Vector2 val3 = default(Vector2);
		((Vector2)(ref val3))._002Ector(1f, m_lineUVBottom);
		Vector2 val4 = default(Vector2);
		((Vector2)(ref val4))._002Ector(0f, m_lineUVBottom);
		int num = startIndex * 4;
		for (int i = startIndex; i < endIndex; i++)
		{
			m_lineUVs[num] = val;
			m_lineUVs[num + 1] = val2;
			m_lineUVs[num + 2] = val3;
			m_lineUVs[num + 3] = val4;
			num += 4;
		}
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateUVs();
		}
	}

	public bool SetVertexCount()
	{
		m_vertexCount = Mathf.Max(0, GetSegmentNumber() * 4);
		if (m_lineType == LineType.Discrete && (pointsCount & 1) != 0)
		{
			m_vertexCount += 4;
		}
		int num = 65534;
		if (m_capType != EndCap.None)
		{
			num -= 8;
		}
		if (m_vertexCount > num)
		{
			Debug.LogError((object)("VectorLine: exceeded maximum vertex count of 65534 for \"" + name + "\"...use fewer points (maximum is 16383 points for continuous lines and points, and 32767 points for discrete lines, minus two if end caps are used)"));
			return false;
		}
		return true;
	}

	public int MaxPoints()
	{
		if (m_capType != EndCap.None)
		{
			return 16381;
		}
		return 16383;
	}

	public void AddNormals()
	{
		m_useNormals = true;
		m_normalsCalculated = false;
	}

	public void AddTangents()
	{
		if (!m_useNormals)
		{
			m_useNormals = true;
			m_normalsCalculated = false;
		}
		m_useTangents = true;
		m_tangentsCalculated = false;
	}

	public Vector4[] CalculateTangents(Vector3[] normals)
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0301: Unknown result type (might be due to invalid IL or missing references)
		if (!m_useNormals)
		{
			m_vectorObject.UpdateNormals();
			m_useNormals = true;
			m_normalsCalculated = true;
		}
		int num = m_vectorObject.VertexCount();
		Vector3[] array = (Vector3[])(object)new Vector3[num];
		Vector3[] array2 = (Vector3[])(object)new Vector3[num];
		int count = m_lineTriangles.Count;
		Vector3 val7 = default(Vector3);
		Vector3 val8 = default(Vector3);
		for (int i = 0; i < count; i += 3)
		{
			int num2 = m_lineTriangles[i];
			int num3 = m_lineTriangles[i + 1];
			int num4 = m_lineTriangles[i + 2];
			Vector3 val = m_lineVertices[num2];
			Vector3 val2 = m_lineVertices[num3];
			Vector3 val3 = m_lineVertices[num4];
			Vector2 val4 = m_lineUVs[num2];
			Vector2 val5 = m_lineUVs[num3];
			Vector2 val6 = m_lineUVs[num4];
			float num5 = val2.x - val.x;
			float num6 = val3.x - val.x;
			float num7 = val2.y - val.y;
			float num8 = val3.y - val.y;
			float num9 = val2.z - val.z;
			float num10 = val3.z - val.z;
			float num11 = val5.x - val4.x;
			float num12 = val6.x - val4.x;
			float num13 = val5.y - val4.y;
			float num14 = val6.y - val4.y;
			float num15 = 1f / (num11 * num14 - num12 * num13);
			((Vector3)(ref val7))._002Ector((num14 * num5 - num13 * num6) * num15, (num14 * num7 - num13 * num8) * num15, (num14 * num9 - num13 * num10) * num15);
			((Vector3)(ref val8))._002Ector((num11 * num6 - num12 * num5) * num15, (num11 * num8 - num12 * num7) * num15, (num11 * num10 - num12 * num9) * num15);
			ref Vector3 reference = ref array[num2];
			reference += val7;
			ref Vector3 reference2 = ref array[num3];
			reference2 += val7;
			ref Vector3 reference3 = ref array[num4];
			reference3 += val7;
			ref Vector3 reference4 = ref array2[num2];
			reference4 += val8;
			ref Vector3 reference5 = ref array2[num3];
			reference5 += val8;
			ref Vector3 reference6 = ref array2[num4];
			reference6 += val8;
		}
		Vector4[] array3 = (Vector4[])(object)new Vector4[num];
		for (int j = 0; j < m_vertexCount; j++)
		{
			Vector3 val9 = normals[j];
			Vector3 val10 = array[j];
			int num16 = j;
			Vector3 val11 = val10 - val9 * Vector3.Dot(val9, val10);
			array3[num16] = Vector4.op_Implicit(((Vector3)(ref val11)).normalized);
			array3[j].w = ((Vector3.Dot(Vector3.Cross(val9, val10), array2[j]) < 0f) ? (-1f) : 1f);
		}
		return array3;
	}

	public static GameObject SetupVectorCanvas()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		GameObject val = GameObject.Find("VectorCanvas");
		Canvas val2;
		if ((Object)(object)val != (Object)null)
		{
			val2 = val.GetComponent<Canvas>();
		}
		else
		{
			val = new GameObject("VectorCanvas");
			val.layer = LayerMask.NameToLayer("UI");
			val2 = val.AddComponent<Canvas>();
		}
		val2.renderMode = (RenderMode)0;
		val2.sortingOrder = 1;
		m_canvas = val2;
		return val;
	}

	public static void SetCanvasCamera(Camera cam)
	{
		if ((Object)(object)m_canvas == (Object)null)
		{
			SetupVectorCanvas();
		}
		m_canvas.renderMode = (RenderMode)1;
		m_canvas.worldCamera = cam;
	}

	public void SetCanvas(GameObject canvasObject)
	{
		SetCanvas(canvasObject, worldPositionStays: true);
	}

	public void SetCanvas(GameObject canvasObject, bool worldPositionStays)
	{
		Canvas component = canvasObject.GetComponent<Canvas>();
		if ((Object)(object)component == (Object)null)
		{
			Debug.LogError((object)"VectorLine.SetCanvas: canvas object must have a Canvas component");
		}
		else
		{
			SetCanvas(component, worldPositionStays);
		}
	}

	public void SetCanvas(Canvas canvas)
	{
		SetCanvas(canvas, worldPositionStays: true);
	}

	public void SetCanvas(Canvas canvas, bool worldPositionStays)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		if (m_canvasState == CanvasState.OffCanvas)
		{
			Debug.LogError((object)"VectorLine.SetCanvas only works with lines made with Draw, not Draw3D.");
		}
		else if ((Object)(object)canvas == (Object)null)
		{
			Debug.LogError((object)"VectorLine.SetCanvas: canvas must not be null");
		}
		else if ((int)canvas.renderMode == 2)
		{
			Debug.LogError((object)"VectorLine.SetCanvas: canvas must be screen space overlay or screen space camera");
		}
		else
		{
			m_go.transform.SetParent(((Component)canvas).transform, worldPositionStays);
		}
	}

	public void SetMask(GameObject maskObject)
	{
		SetMask(maskObject, worldPositionStays: true);
	}

	public void SetMask(GameObject maskObject, bool worldPositionStays)
	{
		Mask component = maskObject.GetComponent<Mask>();
		if ((Object)(object)component == (Object)null)
		{
			Debug.LogError((object)"VectorLine.SetMask: mask object must have a Mask component");
		}
		else
		{
			SetMask(component, worldPositionStays);
		}
	}

	public void SetMask(Mask mask)
	{
		SetMask(mask, worldPositionStays: true);
	}

	public void SetMask(Mask mask, bool worldPositionStays)
	{
		if (m_canvasState == CanvasState.OffCanvas)
		{
			Debug.LogError((object)"VectorLine.SetMask only works with lines made with Draw, not Draw3D.");
		}
		else if ((Object)(object)mask == (Object)null)
		{
			Debug.LogError((object)"VectorLine.SetMask: mask must not be null");
		}
		else
		{
			m_go.transform.SetParent(((Component)mask).transform, worldPositionStays);
		}
	}

	public bool CheckCamera3D()
	{
		if (!m_is2D && !Object.op_Implicit((Object)(object)cam3D))
		{
			SetCamera3D();
			if (!Object.op_Implicit((Object)(object)cam3D))
			{
				Debug.LogError((object)"No camera available...use VectorLine.SetCamera3D to assign a camera");
				return false;
			}
		}
		return true;
	}

	public static void SetCamera3D()
	{
		if ((Object)(object)Camera.main == (Object)null)
		{
			Debug.LogError((object)"VectorLine.SetCamera3D: no camera tagged \"Main Camera\" found. Please call SetCamera3D with a specific camera instead.");
		}
		else
		{
			SetCamera3D(Camera.main);
		}
	}

	public static void SetCamera3D(GameObject cameraObject)
	{
		Camera component = cameraObject.GetComponent<Camera>();
		if ((Object)(object)component == (Object)null)
		{
			Debug.LogError((object)"VectorLine.SetCamera3D: camera object must have a Camera component");
		}
		else
		{
			SetCamera3D(component);
		}
	}

	public static void SetCamera3D(Camera camera)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		camTransform = ((Component)camera).transform;
		cam3D = camera;
		oldPosition = camTransform.position + Vector3.one;
		oldRotation = camTransform.eulerAngles + Vector3.one;
	}

	public static bool CameraHasMoved()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (!(oldPosition != camTransform.position))
		{
			return oldRotation != camTransform.eulerAngles;
		}
		return true;
	}

	public static void UpdateCameraInfo()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		oldPosition = camTransform.position;
		oldRotation = camTransform.eulerAngles;
	}

	public int GetSegmentNumber()
	{
		if (m_lineType == LineType.Points)
		{
			return pointsCount;
		}
		if (m_lineType == LineType.Continuous)
		{
			if (pointsCount != 0)
			{
				return pointsCount - 1;
			}
			return 0;
		}
		return pointsCount / 2;
	}

	public void SetEndCapColors()
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		if (m_lineVertices.Length < 4)
		{
			return;
		}
		if (m_capType <= EndCap.Mirror)
		{
			int num = ((m_lineType == LineType.Continuous) ? (m_drawStart * 4) : (m_drawStart * 2));
			for (int i = 0; i < 4; i++)
			{
				m_lineColors[i + m_vertexCount] = (m_useCapColors ? m_frontColor : m_lineColors[i + num]);
			}
		}
		if (m_capType >= EndCap.Both)
		{
			int num2 = m_drawEnd;
			if (m_lineType == LineType.Continuous)
			{
				if (m_drawEnd == pointsCount)
				{
					num2--;
				}
			}
			else if (num2 < pointsCount)
			{
				num2++;
			}
			int num3 = num2 * ((m_lineType == LineType.Continuous) ? 4 : 2) - 2;
			if (num3 < 0)
			{
				num3 = 0;
			}
			for (int j = 4; j < 8; j++)
			{
				m_lineColors[j + m_vertexCount] = (m_useCapColors ? m_backColor : m_lineColors[num3]);
			}
		}
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateColors();
		}
	}

	public void SetEndCapColor(Color32 color)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		SetEndCapColor(color, color);
	}

	public void SetEndCapColor(Color32 frontColor, Color32 backColor)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (m_capType == EndCap.None)
		{
			Debug.LogError((object)("VectorLine.SetEndCapColor: the line \"" + name + "\" does not have any end caps"));
			return;
		}
		m_useCapColors = true;
		m_frontColor = frontColor;
		m_backColor = backColor;
		SetEndCapColors();
	}

	public void SetEndCapIndex(EndCap endCap, int index)
	{
		if (m_capType == EndCap.None)
		{
			Debug.LogError((object)("VectorLine.SetEndCapIndex: the line \"" + name + "\" does not have any end caps"));
			return;
		}
		if (endCap != EndCap.Front && endCap != EndCap.Back)
		{
			Debug.LogError((object)"VectorLine.SetEndCapIndex: endCap must be EndCap.Front or EndCap.Back");
			return;
		}
		if (index < 0)
		{
			index = 0;
		}
		switch (endCap)
		{
		case EndCap.Front:
			m_frontEndCapIndex = index;
			break;
		case EndCap.Back:
			m_backEndCapIndex = index;
			break;
		}
	}

	public void SetColor(Color32 color)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		SetColor(color, 0, pointsCount);
	}

	public void SetColor(Color32 color, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		SetColor(color, index, index);
	}

	public void SetColor(Color32 color, int startIndex, int endIndex)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		int segmentNumber = GetSegmentNumber();
		startIndex = Mathf.Clamp(startIndex * 4, 0, segmentNumber * 4);
		endIndex = Mathf.Clamp((endIndex + 1) * 4, 0, segmentNumber * 4);
		if (!m_smoothColor)
		{
			for (int i = startIndex; i < endIndex; i++)
			{
				m_lineColors[i] = color;
			}
		}
		else
		{
			if (startIndex == 0)
			{
				m_lineColors[0] = color;
				m_lineColors[3] = color;
			}
			for (int j = startIndex; j < endIndex; j += 4)
			{
				m_lineColors[j + 1] = color;
				m_lineColors[j + 2] = color;
				if (j + 4 < m_vertexCount)
				{
					m_lineColors[j + 4] = color;
					m_lineColors[j + 7] = color;
				}
			}
		}
		if (m_capType != EndCap.None && (startIndex <= 0 || endIndex >= segmentNumber - 1))
		{
			SetEndCapColors();
		}
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateColors();
		}
	}

	public void SetColors(List<Color32> lineColors)
	{
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		if (lineColors == null)
		{
			Debug.LogError((object)"VectorLine.SetColors: lineColors list must not be null");
			return;
		}
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		if (m_lineType != LineType.Points)
		{
			if (WrongArrayLength(lineColors.Count, FunctionName.SetColors))
			{
				return;
			}
		}
		else if (lineColors.Count != pointsCount)
		{
			Debug.LogError((object)("VectorLine.SetColors: Length of lineColors list in \"" + name + "\" must be same length as points list"));
			return;
		}
		SetSegmentStartEnd(out var start, out var end);
		if (start == 0 && end == 0)
		{
			return;
		}
		int num = start * 4;
		if (m_lineType == LineType.Points)
		{
			end++;
		}
		if (smoothColor)
		{
			m_lineColors[num] = lineColors[start];
			m_lineColors[num + 3] = lineColors[start];
			m_lineColors[num + 2] = lineColors[start];
			m_lineColors[num + 1] = lineColors[start];
			num += 4;
			for (int i = start + 1; i < end; i++)
			{
				m_lineColors[num] = lineColors[i - 1];
				m_lineColors[num + 3] = lineColors[i - 1];
				m_lineColors[num + 2] = lineColors[i];
				m_lineColors[num + 1] = lineColors[i];
				num += 4;
			}
		}
		else
		{
			for (int j = start; j < end; j++)
			{
				m_lineColors[num] = lineColors[j];
				m_lineColors[num + 1] = lineColors[j];
				m_lineColors[num + 2] = lineColors[j];
				m_lineColors[num + 3] = lineColors[j];
				num += 4;
			}
		}
		if (m_capType != EndCap.None)
		{
			SetEndCapColors();
		}
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateColors();
		}
	}

	public void SetSegmentStartEnd(out int start, out int end)
	{
		start = ((m_lineType != LineType.Discrete) ? m_drawStart : (m_drawStart / 2));
		end = m_drawEnd;
		if (m_lineType == LineType.Discrete)
		{
			end = m_drawEnd / 2;
			if (m_drawEnd % 2 != 0)
			{
				end++;
			}
		}
	}

	public Color32 GetColor(int index)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		if (m_vertexCount == 0)
		{
			return m_color;
		}
		int num = index * 4 + 2;
		if (num < 0 || num >= m_vertexCount)
		{
			Debug.LogError((object)("VectorLine.GetColor: index " + index + " out of range"));
			return Color32.op_Implicit(Color.clear);
		}
		return m_lineColors[num];
	}

	public void SetupWidths(int max)
	{
		if ((max >= 2 && m_lineWidths.Length == 1) || (max >= 2 && m_lineWidths.Length != max))
		{
			ResizeLineWidths(max);
		}
	}

	public void SetWidth(float width)
	{
		m_lineWidth = width;
		SetWidth(width, 0, pointsCount);
	}

	public void SetWidth(float width, int index)
	{
		SetWidth(width, index, index);
	}

	public void SetWidth(float width, int startIndex, int endIndex)
	{
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		int segmentNumber = GetSegmentNumber();
		SetupWidths(segmentNumber);
		startIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(segmentNumber - 1, 0));
		endIndex = Mathf.Clamp(endIndex, 0, Mathf.Max(segmentNumber - 1, 0));
		for (int i = startIndex; i <= endIndex; i++)
		{
			m_lineWidths[i] = width * 0.5f;
		}
	}

	public void SetWidths(List<float> lineWidths)
	{
		SetWidths(lineWidths, null, lineWidths.Count, doFloat: true);
	}

	public void SetWidths(List<int> lineWidths)
	{
		SetWidths(null, lineWidths, lineWidths.Count, doFloat: false);
	}

	public void SetWidths(List<float> lineWidthsFloat, List<int> lineWidthsInt, int arrayLength, bool doFloat)
	{
		if ((doFloat && lineWidthsFloat == null) || (!doFloat && lineWidthsInt == null))
		{
			Debug.LogError((object)"VectorLine.SetWidths: line widths list must not be null");
			return;
		}
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		if (m_lineType == LineType.Points)
		{
			if (arrayLength != pointsCount)
			{
				Debug.LogError((object)("VectorLine.SetWidths: line widths list must be the same length as the points list for \"" + name + "\""));
				return;
			}
		}
		else if (WrongArrayLength(arrayLength, FunctionName.SetWidths))
		{
			return;
		}
		if (m_lineWidths.Length != arrayLength)
		{
			Array.Resize(ref m_lineWidths, arrayLength);
		}
		if (doFloat)
		{
			for (int i = 0; i < arrayLength; i++)
			{
				m_lineWidths[i] = lineWidthsFloat[i] * 0.5f;
			}
		}
		else
		{
			for (int j = 0; j < arrayLength; j++)
			{
				m_lineWidths[j] = (float)lineWidthsInt[j] * 0.5f;
			}
		}
	}

	public float GetWidth(int index)
	{
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		int segmentNumber = GetSegmentNumber();
		if (index < 0 || index >= segmentNumber)
		{
			Debug.LogError((object)("VectorLine.GetWidth: index " + index + " out of range...must be >= 0 and < " + segmentNumber));
			return 0f;
		}
		if (index >= m_lineWidths.Length)
		{
			return m_lineWidth;
		}
		return m_lineWidths[index] * 2f;
	}

	public static VectorLine SetLine(Color color, params Vector2[] points)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return SetLine(color, 0f, points);
	}

	public static VectorLine SetLine(Color color, float time, params Vector2[] points)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (points.Length < 2)
		{
			Debug.LogError((object)"VectorLine.SetLine needs at least two points");
			return null;
		}
		VectorLine vectorLine = new VectorLine("Line", new List<Vector2>(points), null, 1f, LineType.Continuous, Joins.None);
		vectorLine.color = Color32.op_Implicit(color);
		if (time > 0f)
		{
			lineManager.DisableLine(vectorLine, time);
		}
		vectorLine.Draw();
		return vectorLine;
	}

	public static VectorLine SetLine(Color color, params Vector3[] points)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return SetLine(color, 0f, points);
	}

	public static VectorLine SetLine(Color color, float time, params Vector3[] points)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (points.Length < 2)
		{
			Debug.LogError((object)"VectorLine.SetLine needs at least two points");
			return null;
		}
		VectorLine vectorLine = new VectorLine("SetLine", new List<Vector3>(points), null, 1f, LineType.Continuous, Joins.None);
		vectorLine.color = Color32.op_Implicit(color);
		if (time > 0f)
		{
			lineManager.DisableLine(vectorLine, time);
		}
		vectorLine.Draw();
		return vectorLine;
	}

	public static VectorLine SetLine3D(Color color, params Vector3[] points)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return SetLine3D(color, 0f, points);
	}

	public static VectorLine SetLine3D(Color color, float time, params Vector3[] points)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (points.Length < 2)
		{
			Debug.LogError((object)"VectorLine.SetLine3D needs at least two points");
			return null;
		}
		VectorLine vectorLine = new VectorLine("SetLine3D", new List<Vector3>(points), null, 1f, LineType.Continuous, Joins.None);
		vectorLine.color = Color32.op_Implicit(color);
		vectorLine.Draw3DAuto(time);
		return vectorLine;
	}

	public static VectorLine SetRay(Color color, Vector3 origin, Vector3 direction)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return SetRay(color, 0f, origin, direction);
	}

	public static VectorLine SetRay(Color color, float time, Vector3 origin, Vector3 direction)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] obj = new Vector3[2]
		{
			origin,
			default(Vector3)
		};
		Ray val = new Ray(origin, direction);
		obj[1] = ((Ray)(ref val)).GetPoint(((Vector3)(ref direction)).magnitude);
		VectorLine vectorLine = new VectorLine("SetRay", new List<Vector3>((IEnumerable<Vector3>)(object)obj), null, 1f, LineType.Continuous, Joins.None);
		vectorLine.color = Color32.op_Implicit(color);
		if (time > 0f)
		{
			lineManager.DisableLine(vectorLine, time);
		}
		vectorLine.Draw();
		return vectorLine;
	}

	public static VectorLine SetRay3D(Color color, Vector3 origin, Vector3 direction)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return SetRay3D(color, 0f, origin, direction);
	}

	public static VectorLine SetRay3D(Color color, float time, Vector3 origin, Vector3 direction)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] obj = new Vector3[2]
		{
			origin,
			default(Vector3)
		};
		Ray val = new Ray(origin, direction);
		obj[1] = ((Ray)(ref val)).GetPoint(((Vector3)(ref direction)).magnitude);
		VectorLine vectorLine = new VectorLine("SetRay3D", new List<Vector3>((IEnumerable<Vector3>)(object)obj), null, 1f, LineType.Continuous, Joins.None);
		vectorLine.color = Color32.op_Implicit(color);
		vectorLine.Draw3DAuto(time);
		return vectorLine;
	}

	public void CheckNormals()
	{
		if (m_useNormals && !m_normalsCalculated)
		{
			m_vectorObject.UpdateNormals();
			m_normalsCalculated = true;
		}
		if (m_useTangents && !m_tangentsCalculated)
		{
			m_vectorObject.UpdateTangents();
			m_tangentsCalculated = true;
		}
	}

	public void CheckLine(bool draw3D)
	{
		if (m_capType != EndCap.None)
		{
			DrawEndCap(draw3D);
		}
		if (m_continuousTexture)
		{
			SetContinuousTexture();
		}
		if (m_joins == Joins.Fill)
		{
			SetLastFillTriangles();
		}
	}

	public void DrawEndCap(bool draw3D)
	{
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		//IL_036c: Unknown result type (might be due to invalid IL or missing references)
		//IL_05cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05de: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0619: Unknown result type (might be due to invalid IL or missing references)
		//IL_061e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0620: Unknown result type (might be due to invalid IL or missing references)
		//IL_0637: Unknown result type (might be due to invalid IL or missing references)
		//IL_063c: Unknown result type (might be due to invalid IL or missing references)
		//IL_065a: Unknown result type (might be due to invalid IL or missing references)
		//IL_065f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0661: Unknown result type (might be due to invalid IL or missing references)
		//IL_0666: Unknown result type (might be due to invalid IL or missing references)
		//IL_0668: Unknown result type (might be due to invalid IL or missing references)
		//IL_066d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0672: Unknown result type (might be due to invalid IL or missing references)
		//IL_0693: Unknown result type (might be due to invalid IL or missing references)
		//IL_0698: Unknown result type (might be due to invalid IL or missing references)
		//IL_069a: Unknown result type (might be due to invalid IL or missing references)
		//IL_069f: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_06cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_06fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0700: Unknown result type (might be due to invalid IL or missing references)
		//IL_0705: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0501: Unknown result type (might be due to invalid IL or missing references)
		//IL_0518: Unknown result type (might be due to invalid IL or missing references)
		//IL_051d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0536: Unknown result type (might be due to invalid IL or missing references)
		//IL_053b: Unknown result type (might be due to invalid IL or missing references)
		//IL_053d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0542: Unknown result type (might be due to invalid IL or missing references)
		//IL_0544: Unknown result type (might be due to invalid IL or missing references)
		//IL_0549: Unknown result type (might be due to invalid IL or missing references)
		//IL_0565: Unknown result type (might be due to invalid IL or missing references)
		//IL_056a: Unknown result type (might be due to invalid IL or missing references)
		//IL_056c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0571: Unknown result type (might be due to invalid IL or missing references)
		//IL_0573: Unknown result type (might be due to invalid IL or missing references)
		//IL_0578: Unknown result type (might be due to invalid IL or missing references)
		//IL_058c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0591: Unknown result type (might be due to invalid IL or missing references)
		//IL_0593: Unknown result type (might be due to invalid IL or missing references)
		//IL_0598: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0721: Unknown result type (might be due to invalid IL or missing references)
		//IL_0726: Unknown result type (might be due to invalid IL or missing references)
		//IL_0742: Unknown result type (might be due to invalid IL or missing references)
		//IL_0747: Unknown result type (might be due to invalid IL or missing references)
		//IL_0794: Unknown result type (might be due to invalid IL or missing references)
		//IL_07a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_07b6: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val;
		if (m_capType <= EndCap.Mirror)
		{
			int num;
			if (m_frontEndCapIndex != -1)
			{
				num = m_frontEndCapIndex;
				if (m_lineType == LineType.Discrete && (num & 1) != 0)
				{
					num++;
				}
				num = Mathf.Clamp(num, drawStart, drawEnd) * 4;
			}
			else
			{
				num = m_drawStart * 4;
			}
			int num2 = ((m_lineWidths.Length > 1) ? m_drawStart : 0);
			if (m_lineType == LineType.Discrete)
			{
				num2 /= 2;
				num /= 2;
			}
			if (!draw3D)
			{
				val = m_lineVertices[num] - m_lineVertices[num + 1];
				Vector3 val2 = ((Vector3)(ref val)).normalized * m_lineWidths[num2] * 2f * capDictionary[m_endCap].ratio1;
				Vector3 val3 = val2 * capDictionary[m_endCap].offset1;
				m_lineVertices[m_vertexCount] = m_lineVertices[num] + val2 + val3;
				m_lineVertices[m_vertexCount + 3] = m_lineVertices[num + 3] + val2 + val3;
				ref Vector3 reference = ref m_lineVertices[num];
				reference += val3;
				ref Vector3 reference2 = ref m_lineVertices[num + 3];
				reference2 += val3;
			}
			else
			{
				val = m_screenPoints[num] - m_screenPoints[num + 1];
				Vector3 val4 = ((Vector3)(ref val)).normalized * m_lineWidths[num2] * 2f * capDictionary[m_endCap].ratio1;
				Vector3 val5 = val4 * capDictionary[m_endCap].offset1;
				m_lineVertices[m_vertexCount] = cam3D.ScreenToWorldPoint(m_screenPoints[num] + val4 + val5);
				m_lineVertices[m_vertexCount + 3] = cam3D.ScreenToWorldPoint(m_screenPoints[num + 3] + val4 + val5);
				m_lineVertices[num] = cam3D.ScreenToWorldPoint(m_screenPoints[num] + val5);
				m_lineVertices[num + 3] = cam3D.ScreenToWorldPoint(m_screenPoints[num + 3] + val5);
			}
			m_lineVertices[m_vertexCount + 2] = m_lineVertices[num + 3];
			m_lineVertices[m_vertexCount + 1] = m_lineVertices[num];
			if (capDictionary[m_endCap].scale1 != 1f)
			{
				ScaleCapVertices(m_vertexCount, capDictionary[m_endCap].scale1, (m_lineVertices[m_vertexCount + 1] + m_lineVertices[m_vertexCount + 2]) / 2f);
			}
			m_lineTriangles[0] = m_vertexCount;
			m_lineTriangles[1] = m_vertexCount + 1;
			m_lineTriangles[2] = m_vertexCount + 3;
			m_lineTriangles[3] = m_vertexCount + 1;
			m_lineTriangles[4] = m_vertexCount + 2;
			m_lineTriangles[5] = m_vertexCount + 3;
		}
		if (m_capType >= EndCap.Both)
		{
			int num3 = m_drawEnd;
			if (m_lineType == LineType.Continuous)
			{
				if (m_drawEnd == pointsCount)
				{
					num3--;
				}
			}
			else if (num3 < pointsCount)
			{
				num3++;
			}
			int num;
			if (m_backEndCapIndex != -1)
			{
				num = m_backEndCapIndex;
				if (m_lineType == LineType.Discrete && (num & 1) != 0)
				{
					num++;
				}
				num = Mathf.Clamp(num, drawStart, num3) * 4;
			}
			else
			{
				num = num3 * 4;
			}
			int num4 = ((m_lineWidths.Length > 1) ? (num3 - 1) : 0);
			if (num4 < 0)
			{
				num4 = 0;
			}
			if (m_lineType == LineType.Discrete)
			{
				num4 /= 2;
				num /= 2;
			}
			if (num < 4)
			{
				num = 4;
			}
			if (!draw3D)
			{
				val = m_lineVertices[num - 2] - m_lineVertices[num - 1];
				Vector3 val6 = ((Vector3)(ref val)).normalized * m_lineWidths[num4] * 2f * capDictionary[m_endCap].ratio2;
				Vector3 val7 = val6 * capDictionary[m_endCap].offset2;
				m_lineVertices[m_vertexCount + 6] = m_lineVertices[num - 2] + val6 + val7;
				m_lineVertices[m_vertexCount + 5] = m_lineVertices[num - 3] + val6 + val7;
				ref Vector3 reference3 = ref m_lineVertices[num - 3];
				reference3 += val7;
				ref Vector3 reference4 = ref m_lineVertices[num - 2];
				reference4 += val7;
			}
			else
			{
				val = m_screenPoints[num - 2] - m_screenPoints[num - 1];
				Vector3 val8 = ((Vector3)(ref val)).normalized * m_lineWidths[num4] * 2f * capDictionary[m_endCap].ratio2;
				Vector3 val9 = val8 * capDictionary[m_endCap].offset2;
				m_lineVertices[m_vertexCount + 6] = cam3D.ScreenToWorldPoint(m_screenPoints[num - 2] + val8 + val9);
				m_lineVertices[m_vertexCount + 5] = cam3D.ScreenToWorldPoint(m_screenPoints[num - 3] + val8 + val9);
				m_lineVertices[num - 3] = cam3D.ScreenToWorldPoint(m_screenPoints[num - 3] + val9);
				m_lineVertices[num - 2] = cam3D.ScreenToWorldPoint(m_screenPoints[num - 2] + val9);
			}
			m_lineVertices[m_vertexCount + 4] = m_lineVertices[num - 3];
			m_lineVertices[m_vertexCount + 7] = m_lineVertices[num - 2];
			if (capDictionary[m_endCap].scale2 != 1f)
			{
				ScaleCapVertices(m_vertexCount + 4, capDictionary[m_endCap].scale2, (m_lineVertices[m_vertexCount + 4] + m_lineVertices[m_vertexCount + 7]) / 2f);
			}
			m_lineTriangles[6] = m_vertexCount + 4;
			m_lineTriangles[7] = m_vertexCount + 5;
			m_lineTriangles[8] = m_vertexCount + 7;
			m_lineTriangles[9] = m_vertexCount + 5;
			m_lineTriangles[10] = m_vertexCount + 6;
			m_lineTriangles[11] = m_vertexCount + 7;
		}
		if (m_drawStart > 0 || m_drawEnd < pointsCount)
		{
			SetEndCapColors();
		}
	}

	public void ScaleCapVertices(int offset, float scale, Vector3 center)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		m_lineVertices[offset] = (m_lineVertices[offset] - center) * scale + center;
		m_lineVertices[offset + 1] = (m_lineVertices[offset + 1] - center) * scale + center;
		m_lineVertices[offset + 2] = (m_lineVertices[offset + 2] - center) * scale + center;
		m_lineVertices[offset + 3] = (m_lineVertices[offset + 3] - center) * scale + center;
	}

	public void SetContinuousTexture()
	{
		int num = 0;
		float x = 0f;
		SetDistances();
		int num2 = m_distances.Length - 1;
		float num3 = m_distances[num2];
		for (int i = 0; i < num2; i++)
		{
			m_lineUVs[num].x = x;
			m_lineUVs[num + 3].x = x;
			x = 1f / (num3 / m_distances[i + 1]);
			m_lineUVs[num + 1].x = x;
			m_lineUVs[num + 2].x = x;
			num += 4;
		}
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateUVs();
		}
	}

	public bool UseMatrix(out Matrix4x4 thisMatrix)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)m_drawTransform != (Object)null)
		{
			thisMatrix = m_drawTransform.localToWorldMatrix;
			return true;
		}
		if (m_useMatrix)
		{
			thisMatrix = m_matrix;
			return true;
		}
		thisMatrix = Matrix4x4.identity;
		return false;
	}

	public bool CheckPointCount()
	{
		if (pointsCount < ((m_lineType == LineType.Points) ? 1 : 2))
		{
			ClearTriangles();
			m_vectorObject.ClearMesh();
			m_pointsCount = pointsCount;
			m_drawEnd = 0;
			return false;
		}
		return true;
	}

	public void ClearTriangles()
	{
		if (m_capType == EndCap.None)
		{
			m_lineTriangles.Clear();
		}
		else
		{
			m_lineTriangles.RemoveRange(12, m_lineTriangles.Count - 12);
		}
	}

	public void SetupDrawStartEnd(out int start, out int end, bool clearVertices)
	{
		start = 0;
		end = m_pointsCount - 1;
		if (m_drawStart > 0)
		{
			start = m_drawStart;
			if (m_lineType == LineType.Discrete && start == pointsCount - 1)
			{
				start++;
			}
			if (clearVertices)
			{
				ZeroVertices(0, start);
			}
		}
		if (m_drawEnd < m_pointsCount - 1)
		{
			end = m_drawEnd;
			if (end < 0)
			{
				end = 0;
			}
			if (clearVertices)
			{
				ZeroVertices(end, m_pointsCount);
			}
		}
		if (m_endPointsUpdate > 0)
		{
			start = Mathf.Max(0, end - m_endPointsUpdate);
		}
	}

	public void ZeroVertices(int startIndex, int endIndex)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		if (m_lineType != LineType.Discrete)
		{
			startIndex *= 4;
			endIndex *= 4;
			if (endIndex > m_vertexCount)
			{
				endIndex -= 4;
			}
			for (int i = startIndex; i < endIndex; i += 4)
			{
				m_lineVertices[i] = v3zero;
				m_lineVertices[i + 1] = v3zero;
				m_lineVertices[i + 2] = v3zero;
				m_lineVertices[i + 3] = v3zero;
			}
		}
		else
		{
			startIndex *= 2;
			endIndex *= 2;
			for (int j = startIndex; j < endIndex; j += 2)
			{
				m_lineVertices[j] = v3zero;
				m_lineVertices[j + 1] = v3zero;
			}
		}
	}

	public void SetupCanvasState(CanvasState wantedState)
	{
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Expected O, but got Unknown
		if (wantedState == CanvasState.OnCanvas)
		{
			if ((Object)(object)m_go == (Object)null)
			{
				return;
			}
			Transform parent = m_go.transform.parent;
			bool flag = true;
			while ((Object)(object)parent != (Object)null)
			{
				if ((Object)(object)((Component)parent).GetComponent<Canvas>() != (Object)null)
				{
					flag = false;
					break;
				}
				parent = parent.parent;
			}
			if (flag)
			{
				if ((Object)(object)m_canvas == (Object)null)
				{
					SetupVectorCanvas();
				}
				m_go.transform.SetParent(((Component)m_canvas).transform, true);
			}
			m_canvasState = CanvasState.OnCanvas;
			if ((Object)(object)m_go.GetComponent<VectorObject3D>() != (Object)null)
			{
				Object.DestroyImmediate((Object)(object)m_go.GetComponent<VectorObject3D>());
				Object.DestroyImmediate((Object)(object)m_go.GetComponent<MeshFilter>());
				Object.DestroyImmediate((Object)(object)m_go.GetComponent<MeshRenderer>());
			}
			if ((Object)(object)m_go.GetComponent<VectorObject2D>() == (Object)null)
			{
				m_vectorObject = m_go.AddComponent<VectorObject2D>();
			}
			else
			{
				m_vectorObject = m_go.GetComponent<VectorObject2D>();
			}
			m_vectorObject.SetVectorLine(this, m_texture, m_material, useCustomMaterial: false);
		}
		else
		{
			if ((Object)(object)m_go == (Object)null)
			{
				return;
			}
			m_go.transform.SetParent((Transform)null);
			m_canvasState = CanvasState.OffCanvas;
			if ((Object)(object)m_go.GetComponent<VectorObject2D>() != (Object)null)
			{
				m_go.GetComponent<VectorObject2D>().DestroyNow();
				Object.DestroyImmediate((Object)(object)m_go.GetComponent<VectorObject2D>());
			}
			m_vectorObject = m_go.GetComponent<VectorObject3D>();
			if (m_vectorObject == null)
			{
				m_vectorObject = m_go.AddComponent<VectorObject3D>();
			}
			bool useCustomMaterial = true;
			if ((Object)(object)m_material == (Object)null)
			{
				Object obj = Resources.Load("DefaultLine3D");
				Material val = (Material)(object)((obj is Material) ? obj : null);
				if ((Object)(object)val == (Object)null)
				{
					Debug.LogError((object)"No DefaultLine3D material found in Resources");
					return;
				}
				m_material = new Material(val);
				useCustomMaterial = false;
			}
			m_vectorObject.SetVectorLine(this, m_texture, m_material, useCustomMaterial);
		}
	}

	public void Draw()
	{
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		if (!m_active)
		{
			return;
		}
		if (m_canvasState != CanvasState.OnCanvas)
		{
			SetupCanvasState(CanvasState.OnCanvas);
		}
		if (m_vectorObject == null)
		{
			m_vectorObject = m_go.GetComponent<VectorObject2D>();
		}
		if (!CheckPointCount() || m_lineWidths == null)
		{
			return;
		}
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		if (m_lineType == LineType.Points)
		{
			DrawPoints();
			return;
		}
		Matrix4x4 thisMatrix;
		bool useTransformMatrix = UseMatrix(out thisMatrix);
		int start = 0;
		int end = 0;
		SetupDrawStartEnd(out start, out end, clearVertices: true);
		if (m_is2D)
		{
			Line2D(start, end, thisMatrix, useTransformMatrix);
		}
		else
		{
			Line3D(start, end, thisMatrix, useTransformMatrix);
		}
		CheckNormals();
		CheckLine(draw3D: false);
		if (m_useTextureScale)
		{
			SetTextureScale();
		}
		m_vectorObject.UpdateVerts();
		if (m_collider)
		{
			SetCollider(convertToWorldSpace: true);
		}
	}

	public void Line2D(int start, int end, Matrix4x4 thisMatrix, bool useTransformMatrix)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0542: Unknown result type (might be due to invalid IL or missing references)
		//IL_0555: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_0507: Unknown result type (might be due to invalid IL or missing references)
		//IL_051a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0301: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_0321: Unknown result type (might be due to invalid IL or missing references)
		//IL_0327: Unknown result type (might be due to invalid IL or missing references)
		//IL_032d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_033a: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_034f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0350: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Unknown result type (might be due to invalid IL or missing references)
		//IL_0378: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_0391: Unknown result type (might be due to invalid IL or missing references)
		//IL_039b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_040f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0415: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0454: Unknown result type (might be due to invalid IL or missing references)
		//IL_046f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0475: Unknown result type (might be due to invalid IL or missing references)
		//IL_0490: Unknown result type (might be due to invalid IL or missing references)
		//IL_0496: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_042d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0439: Unknown result type (might be due to invalid IL or missing references)
		//IL_043e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = v3zero;
		Vector3 val2 = v3zero;
		Vector3 val3 = v3zero;
		Vector3 val4 = v3zero;
		Vector2 val5 = default(Vector2);
		((Vector2)(ref val5))._002Ector((float)Screen.width, (float)Screen.height);
		int num = 0;
		int num2 = 0;
		int widthIdx = 0;
		int widthIdxAdd = 0;
		if (m_lineWidths.Length > 1)
		{
			widthIdx = start;
			widthIdxAdd = 1;
		}
		if (m_lineType == LineType.Continuous)
		{
			num = 1;
			num2 = start * 4;
		}
		else
		{
			num = 2;
			widthIdx /= 2;
			num2 = start * 2;
		}
		float num3 = 0f;
		bool flag = smoothWidth && m_lineWidths.Length > 1;
		for (int i = start; i < end; i += num)
		{
			if (useTransformMatrix)
			{
				val = ((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(Vector2.op_Implicit(m_points2[i]));
				val2 = ((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(Vector2.op_Implicit(m_points2[i + 1]));
			}
			else
			{
				val.x = m_points2[i].x;
				val.y = m_points2[i].y;
				val2.x = m_points2[i + 1].x;
				val2.y = m_points2[i + 1].y;
			}
			if (m_viewportDraw)
			{
				val.x *= val5.x;
				val.y *= val5.y;
				val2.x *= val5.x;
				val2.y *= val5.y;
			}
			if (val.x == val2.x && val.y == val2.y)
			{
				SkipQuad(ref num2, ref widthIdx, ref widthIdxAdd);
				continue;
			}
			if (m_capLength == 0f)
			{
				val4.x = val2.y - val.y;
				val4.y = val.x - val2.x;
				num3 = 1f / (float)Math.Sqrt(val4.x * val4.x + val4.y * val4.y);
				val4 *= num3 * m_lineWidths[widthIdx];
				m_lineVertices[num2].x = val.x - val4.x;
				m_lineVertices[num2].y = val.y - val4.y;
				m_lineVertices[num2 + 3].x = val.x + val4.x;
				m_lineVertices[num2 + 3].y = val.y + val4.y;
				if (flag && i < end - num)
				{
					val4.x = val2.y - val.y;
					val4.y = val.x - val2.x;
					val4 *= num3 * m_lineWidths[widthIdx + 1];
				}
			}
			else
			{
				val4.x = val2.x - val.x;
				val4.y = val2.y - val.y;
				val4 *= 1f / (float)Math.Sqrt(val4.x * val4.x + val4.y * val4.y);
				val -= val4 * m_capLength;
				val2 += val4 * m_capLength;
				val3.x = val4.y;
				val3.y = 0f - val4.x;
				val4 = val3 * m_lineWidths[widthIdx];
				m_lineVertices[num2].x = val.x - val4.x;
				m_lineVertices[num2].y = val.y - val4.y;
				m_lineVertices[num2 + 3].x = val.x + val4.x;
				m_lineVertices[num2 + 3].y = val.y + val4.y;
				if (flag && i < end - num)
				{
					val4 = val3 * m_lineWidths[widthIdx + 1];
				}
			}
			m_lineVertices[num2 + 2].x = val2.x + val4.x;
			m_lineVertices[num2 + 2].y = val2.y + val4.y;
			m_lineVertices[num2 + 1].x = val2.x - val4.x;
			m_lineVertices[num2 + 1].y = val2.y - val4.y;
			num2 += 4;
			widthIdx += widthIdxAdd;
		}
		if (m_joins == Joins.Weld)
		{
			if (m_lineType == LineType.Continuous)
			{
				WeldJoins(start * 4 + ((start == 0) ? 4 : 0), end * 4, Approximately(m_points2[0], m_points2[m_pointsCount - 1]));
			}
			else
			{
				if ((end & 1) == 0)
				{
					end--;
				}
				WeldJoinsDiscrete(start + 1, end, Approximately(m_points2[0], m_points2[m_pointsCount - 1]));
			}
		}
		CheckDrawStartFill(start);
	}

	public void Line3D(int start, int end, Matrix4x4 thisMatrix, bool useTransformMatrix)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_031f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0344: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_035a: Unknown result type (might be due to invalid IL or missing references)
		//IL_035f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_0361: Unknown result type (might be due to invalid IL or missing references)
		//IL_0368: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0372: Unknown result type (might be due to invalid IL or missing references)
		//IL_0373: Unknown result type (might be due to invalid IL or missing references)
		//IL_0374: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_0388: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0404: Unknown result type (might be due to invalid IL or missing references)
		//IL_041f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0425: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_045e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0464: Unknown result type (might be due to invalid IL or missing references)
		//IL_047f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_057d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0590: Unknown result type (might be due to invalid IL or missing references)
		//IL_043d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0449: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0531: Unknown result type (might be due to invalid IL or missing references)
		//IL_0544: Unknown result type (might be due to invalid IL or missing references)
		if (!CheckCamera3D())
		{
			return;
		}
		Vector3 val = v3zero;
		Vector3 val2 = v3zero;
		Vector3 val3 = v3zero;
		Vector3 val4 = v3zero;
		Vector3 val5 = v3zero;
		Vector3 val6 = v3zero;
		float num = 0f;
		int widthIdx = 0;
		int widthIdxAdd = 0;
		if (m_lineWidths.Length > 1)
		{
			widthIdx = start;
			widthIdxAdd = 1;
		}
		int idx = start * 2;
		int num2 = 2;
		if (m_lineType == LineType.Continuous)
		{
			idx = start * 4;
			num2 = 1;
		}
		Plane cameraPlane = default(Plane);
		((Plane)(ref cameraPlane))._002Ector(camTransform.forward, camTransform.position + camTransform.forward * cam3D.nearClipPlane);
		Ray ray = default(Ray);
		((Ray)(ref ray))._002Ector(v3zero, v3zero);
		float screenHeight = Screen.height;
		bool flag = smoothWidth && m_lineWidths.Length > 1;
		for (int i = start; i < end; i += num2)
		{
			if (useTransformMatrix)
			{
				val5 = ((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(m_points3[i]);
				val6 = ((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(m_points3[i + 1]);
			}
			else
			{
				val5 = m_points3[i];
				val6 = m_points3[i + 1];
			}
			val = cam3D.WorldToScreenPoint(val5);
			val2 = cam3D.WorldToScreenPoint(val6);
			if ((val.x == val2.x && val.y == val2.y) || IntersectAndDoSkip(ref val, ref val2, ref val5, ref val6, ref screenHeight, ref ray, ref cameraPlane))
			{
				SkipQuad(ref idx, ref widthIdx, ref widthIdxAdd);
				continue;
			}
			if (m_capLength == 0f)
			{
				val4.x = val2.y - val.y;
				val4.y = val.x - val2.x;
				num = 1f / (float)Math.Sqrt(val4.x * val4.x + val4.y * val4.y);
				val4.x *= num * m_lineWidths[widthIdx];
				val4.y *= num * m_lineWidths[widthIdx];
				m_lineVertices[idx].x = val.x - val4.x;
				m_lineVertices[idx].y = val.y - val4.y;
				m_lineVertices[idx + 3].x = val.x + val4.x;
				m_lineVertices[idx + 3].y = val.y + val4.y;
				if (flag && i < end - num2)
				{
					val4.x = val2.y - val.y;
					val4.y = val.x - val2.x;
					val4.x *= num * m_lineWidths[widthIdx + 1];
					val4.y *= num * m_lineWidths[widthIdx + 1];
				}
			}
			else
			{
				val4.x = val2.x - val.x;
				val4.y = val2.y - val.y;
				val4 *= 1f / (float)Math.Sqrt(val4.x * val4.x + val4.y * val4.y);
				val -= val4 * m_capLength;
				val2 += val4 * m_capLength;
				val3.x = val4.y;
				val3.y = 0f - val4.x;
				val4 = val3 * m_lineWidths[widthIdx];
				m_lineVertices[idx].x = val.x - val4.x;
				m_lineVertices[idx].y = val.y - val4.y;
				m_lineVertices[idx + 3].x = val.x + val4.x;
				m_lineVertices[idx + 3].y = val.y + val4.y;
				if (flag && i < end - num2)
				{
					val4 = val3 * m_lineWidths[widthIdx + 1];
				}
			}
			m_lineVertices[idx + 2].x = val2.x + val4.x;
			m_lineVertices[idx + 2].y = val2.y + val4.y;
			m_lineVertices[idx + 1].x = val2.x - val4.x;
			m_lineVertices[idx + 1].y = val2.y - val4.y;
			idx += 4;
			widthIdx += widthIdxAdd;
		}
		if (m_joins == Joins.Weld && end - start > 1)
		{
			if (m_lineType == LineType.Continuous)
			{
				WeldJoins(start * 4 + ((start == 0) ? 4 : 0), end * 4, start == 0 && end == m_pointsCount - 1 && Approximately(m_points3[0], m_points3[m_pointsCount - 1]));
			}
			else
			{
				if ((end & 1) == 0)
				{
					end--;
				}
				WeldJoinsDiscrete(start + 1, end, start == 0 && end == m_pointsCount - 1 && Approximately(m_points3[0], m_points3[m_pointsCount - 1]));
			}
		}
		CheckDrawStartFill(start);
	}

	public void CheckDrawStartFill(int start)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		if (m_joins == Joins.Fill)
		{
			int num = start * 4;
			if (m_drawStart > 0 && m_lineVertices.Length > num && num - 3 >= 0)
			{
				m_lineVertices[num - 1] = m_lineVertices[num];
				m_lineVertices[num - 2] = m_lineVertices[num];
				m_lineVertices[num - 3] = m_lineVertices[num];
			}
		}
	}

	public void Draw3D()
	{
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02da: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0317: Unknown result type (might be due to invalid IL or missing references)
		//IL_031e: Unknown result type (might be due to invalid IL or missing references)
		//IL_033a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0390: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0409: Unknown result type (might be due to invalid IL or missing references)
		//IL_0410: Unknown result type (might be due to invalid IL or missing references)
		//IL_042c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0433: Unknown result type (might be due to invalid IL or missing references)
		//IL_044f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0456: Unknown result type (might be due to invalid IL or missing references)
		//IL_0472: Unknown result type (might be due to invalid IL or missing references)
		//IL_0479: Unknown result type (might be due to invalid IL or missing references)
		//IL_0495: Unknown result type (might be due to invalid IL or missing references)
		//IL_049c: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_050d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0512: Unknown result type (might be due to invalid IL or missing references)
		//IL_0517: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0577: Unknown result type (might be due to invalid IL or missing references)
		//IL_058a: Unknown result type (might be due to invalid IL or missing references)
		if (!m_active)
		{
			return;
		}
		if (m_is2D)
		{
			Debug.LogError((object)("VectorLine.Draw3D can only be used with a Vector3 array, which \"" + name + "\" doesn't have"));
			return;
		}
		if (m_canvasState != CanvasState.OffCanvas)
		{
			SetupCanvasState(CanvasState.OffCanvas);
		}
		if (!CheckPointCount() || m_lineWidths == null)
		{
			return;
		}
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		if (!CheckCamera3D())
		{
			return;
		}
		if (m_lineType == LineType.Points)
		{
			DrawPoints3D();
			return;
		}
		int start = 0;
		int end = 0;
		int num = 0;
		int widthIdx = 0;
		SetupDrawStartEnd(out start, out end, clearVertices: true);
		Matrix4x4 thisMatrix;
		bool flag = UseMatrix(out thisMatrix);
		bool flag2 = smoothWidth && m_lineWidths.Length > 1;
		int num2 = 0;
		int widthIdxAdd = 0;
		if (m_lineWidths.Length > 1)
		{
			widthIdx = start;
			widthIdxAdd = 1;
		}
		if (m_lineType == LineType.Continuous)
		{
			num = 1;
			num2 = start * 4;
		}
		else
		{
			widthIdx /= 2;
			num = 2;
			num2 = start * 2;
		}
		Vector3 val = v3zero;
		Vector3 val2 = v3zero;
		Vector3 val3 = v3zero;
		Vector3 val4 = v3zero;
		Vector3 val5 = v3zero;
		Vector3 val6 = v3zero;
		Plane cameraPlane = default(Plane);
		((Plane)(ref cameraPlane))._002Ector(camTransform.forward, camTransform.position + camTransform.forward * cam3D.nearClipPlane);
		Ray ray = default(Ray);
		((Ray)(ref ray))._002Ector(v3zero, v3zero);
		float screenHeight = Screen.height;
		for (int i = start; i < end; i += num)
		{
			if (flag)
			{
				val5 = ((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(m_points3[i]);
				val6 = ((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(m_points3[i + 1]);
			}
			else
			{
				val5 = m_points3[i];
				val6 = m_points3[i + 1];
			}
			val3 = cam3D.WorldToScreenPoint(val5);
			val4 = cam3D.WorldToScreenPoint(val6);
			if ((val3.x == val4.x && val3.y == val4.y) || IntersectAndDoSkip(ref val3, ref val4, ref val5, ref val6, ref screenHeight, ref ray, ref cameraPlane))
			{
				SkipQuad3D(ref num2, ref widthIdx, ref widthIdxAdd);
				continue;
			}
			val2.x = val4.y - val3.y;
			val2.y = val3.x - val4.x;
			val = val2 / (float)Math.Sqrt(val2.x * val2.x + val2.y * val2.y);
			val2.x = val.x * m_lineWidths[widthIdx];
			val2.y = val.y * m_lineWidths[widthIdx];
			m_screenPoints[num2].x = val3.x - val2.x;
			m_screenPoints[num2].y = val3.y - val2.y;
			m_screenPoints[num2].z = val3.z - val2.z;
			m_screenPoints[num2 + 3].x = val3.x + val2.x;
			m_screenPoints[num2 + 3].y = val3.y + val2.y;
			m_screenPoints[num2 + 3].z = val3.z + val2.z;
			m_lineVertices[num2] = cam3D.ScreenToWorldPoint(m_screenPoints[num2]);
			m_lineVertices[num2 + 3] = cam3D.ScreenToWorldPoint(m_screenPoints[num2 + 3]);
			if (flag2 && i < end - num)
			{
				val2.x = val.x * m_lineWidths[widthIdx + 1];
				val2.y = val.y * m_lineWidths[widthIdx + 1];
			}
			m_screenPoints[num2 + 2].x = val4.x + val2.x;
			m_screenPoints[num2 + 2].y = val4.y + val2.y;
			m_screenPoints[num2 + 2].z = val4.z + val2.z;
			m_screenPoints[num2 + 1].x = val4.x - val2.x;
			m_screenPoints[num2 + 1].y = val4.y - val2.y;
			m_screenPoints[num2 + 1].z = val4.z - val2.z;
			m_lineVertices[num2 + 2] = cam3D.ScreenToWorldPoint(m_screenPoints[num2 + 2]);
			m_lineVertices[num2 + 1] = cam3D.ScreenToWorldPoint(m_screenPoints[num2 + 1]);
			num2 += 4;
			widthIdx += widthIdxAdd;
		}
		if (m_joins == Joins.Weld && end - start > 1)
		{
			if (m_lineType == LineType.Continuous)
			{
				WeldJoins3D(start * 4 + ((start == 0) ? 4 : 0), end * 4, start == 0 && end == m_pointsCount - 1 && Approximately(m_points3[0], m_points3[m_pointsCount - 1]));
			}
			else
			{
				if ((end & 1) == 0)
				{
					end--;
				}
				WeldJoinsDiscrete3D(start + 1, end, start == 0 && end == m_pointsCount - 1 && Approximately(m_points3[0], m_points3[m_pointsCount - 1]));
			}
		}
		CheckDrawStartFill(start);
		CheckLine(draw3D: true);
		if (m_useTextureScale)
		{
			SetTextureScale();
		}
		m_vectorObject.UpdateVerts();
		CheckNormals();
		if (m_collider)
		{
			SetCollider(convertToWorldSpace: false);
		}
	}

	public bool IntersectAndDoSkip(ref Vector3 pos1, ref Vector3 pos2, ref Vector3 p1, ref Vector3 p2, ref float screenHeight, ref Ray ray, ref Plane cameraPlane)
	{
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		if (pos1.z < 0f)
		{
			if (pos2.z < 0f)
			{
				return true;
			}
			pos1 = cam3D.WorldToScreenPoint(PlaneIntersectionPoint(ref ray, ref cameraPlane, ref p2, ref p1));
			Vector3 val = camTransform.InverseTransformPoint(p1);
			if ((val.y < -1f && pos1.y > screenHeight) || (val.y > 1f && pos1.y < 0f))
			{
				return true;
			}
		}
		if (pos2.z < 0f)
		{
			pos2 = cam3D.WorldToScreenPoint(PlaneIntersectionPoint(ref ray, ref cameraPlane, ref p1, ref p2));
			Vector3 val2 = camTransform.InverseTransformPoint(p2);
			if ((val2.y < -1f && pos2.y > screenHeight) || (val2.y > 1f && pos2.y < 0f))
			{
				return true;
			}
		}
		return false;
	}

	public Vector3 PlaneIntersectionPoint(ref Ray ray, ref Plane plane, ref Vector3 p1, ref Vector3 p2)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		((Ray)(ref ray)).origin = p1;
		((Ray)(ref ray)).direction = p2 - p1;
		float num = 0f;
		((Plane)(ref plane)).Raycast(ray, ref num);
		return ((Ray)(ref ray)).GetPoint(num);
	}

	public void DrawPoints()
	{
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		//IL_035e: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_039d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0406: Unknown result type (might be due to invalid IL or missing references)
		//IL_040d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0429: Unknown result type (might be due to invalid IL or missing references)
		//IL_0430: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		if (!CheckCamera3D())
		{
			return;
		}
		Matrix4x4 thisMatrix;
		bool flag = UseMatrix(out thisMatrix);
		SetupDrawStartEnd(out var start, out var end, clearVertices: true);
		Vector2 val = default(Vector2);
		((Vector2)(ref val))._002Ector((float)Screen.width, (float)Screen.height);
		int idx = start * 4;
		int widthIdxAdd = ((m_lineWidths.Length > 1) ? 1 : 0);
		int widthIdx = start;
		Vector3 val2 = default(Vector3);
		((Vector3)(ref val2))._002Ector(m_lineWidths[0], m_lineWidths[0], 0f);
		Vector3 val3 = default(Vector3);
		((Vector3)(ref val3))._002Ector(0f - m_lineWidths[0], m_lineWidths[0], 0f);
		if (m_is2D)
		{
			Vector3 val4 = default(Vector3);
			for (int i = start; i <= end; i++)
			{
				if (flag)
				{
					val4 = ((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(Vector2.op_Implicit(m_points2[i]));
				}
				else
				{
					val4.x = m_points2[i].x;
					val4.y = m_points2[i].y;
				}
				if (m_viewportDraw)
				{
					val4.x *= val.x;
					val4.y *= val.y;
				}
				if (widthIdxAdd != 0)
				{
					val2.x = (val2.y = (val3.y = m_lineWidths[widthIdx]));
					val3.x = 0f - m_lineWidths[widthIdx];
					widthIdx++;
				}
				m_lineVertices[idx].x = val4.x + val3.x;
				m_lineVertices[idx].y = val4.y + val3.y;
				m_lineVertices[idx + 3].x = val4.x - val2.x;
				m_lineVertices[idx + 3].y = val4.y - val2.y;
				m_lineVertices[idx + 1].x = val4.x + val2.x;
				m_lineVertices[idx + 1].y = val4.y + val2.y;
				m_lineVertices[idx + 2].x = val4.x - val3.x;
				m_lineVertices[idx + 2].y = val4.y - val3.y;
				idx += 4;
			}
		}
		else
		{
			for (int j = start; j <= end; j++)
			{
				Vector3 val4 = (flag ? cam3D.WorldToScreenPoint(((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(m_points3[j])) : cam3D.WorldToScreenPoint(m_points3[j]));
				if (val4.z < 0f)
				{
					SkipQuad(ref idx, ref widthIdx, ref widthIdxAdd);
					continue;
				}
				if (widthIdxAdd != 0)
				{
					val2.x = (val2.y = (val3.y = m_lineWidths[widthIdx]));
					val3.x = 0f - m_lineWidths[widthIdx];
					widthIdx++;
				}
				m_lineVertices[idx].x = val4.x + val3.x;
				m_lineVertices[idx].y = val4.y + val3.y;
				m_lineVertices[idx + 3].x = val4.x - val2.x;
				m_lineVertices[idx + 3].y = val4.y - val2.y;
				m_lineVertices[idx + 1].x = val4.x + val2.x;
				m_lineVertices[idx + 1].y = val4.y + val2.y;
				m_lineVertices[idx + 2].x = val4.x - val3.x;
				m_lineVertices[idx + 2].y = val4.y - val3.y;
				idx += 4;
			}
		}
		CheckNormals();
		m_vectorObject.UpdateVerts();
	}

	public void DrawPoints3D()
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		if (!m_active)
		{
			return;
		}
		Matrix4x4 thisMatrix;
		bool flag = UseMatrix(out thisMatrix);
		int start = 0;
		int end = 0;
		int widthIdx = 0;
		SetupDrawStartEnd(out start, out end, clearVertices: true);
		int idx = start * 4;
		int widthIdxAdd = 0;
		if (m_lineWidths.Length > 1)
		{
			widthIdx = start;
			widthIdxAdd = 1;
		}
		Vector3 val = v3zero;
		Vector3 val2 = v3zero;
		Vector3 val3 = v3zero;
		for (int i = start; i <= end; i++)
		{
			val = (flag ? cam3D.WorldToScreenPoint(((Matrix4x4)(ref thisMatrix)).MultiplyPoint3x4(m_points3[i])) : cam3D.WorldToScreenPoint(m_points3[i]));
			if (val.z < 0f)
			{
				SkipQuad(ref idx, ref widthIdx, ref widthIdxAdd);
				continue;
			}
			val2.x = (val2.y = (val3.y = m_lineWidths[widthIdx]));
			val3.x = 0f - m_lineWidths[widthIdx];
			m_lineVertices[idx] = cam3D.ScreenToWorldPoint(val + val3);
			m_lineVertices[idx + 3] = cam3D.ScreenToWorldPoint(val - val2);
			m_lineVertices[idx + 1] = cam3D.ScreenToWorldPoint(val + val2);
			m_lineVertices[idx + 2] = cam3D.ScreenToWorldPoint(val - val3);
			idx += 4;
			widthIdx += widthIdxAdd;
		}
		CheckNormals();
		m_vectorObject.UpdateVerts();
	}

	public void SkipQuad(ref int idx, ref int widthIdx, ref int widthIdxAdd)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		m_lineVertices[idx] = v3zero;
		m_lineVertices[idx + 1] = v3zero;
		m_lineVertices[idx + 2] = v3zero;
		m_lineVertices[idx + 3] = v3zero;
		idx += 4;
		widthIdx += widthIdxAdd;
	}

	public void SkipQuad3D(ref int idx, ref int widthIdx, ref int widthIdxAdd)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		m_lineVertices[idx] = v3zero;
		m_lineVertices[idx + 1] = v3zero;
		m_lineVertices[idx + 2] = v3zero;
		m_lineVertices[idx + 3] = v3zero;
		m_screenPoints[idx] = v3zero;
		m_screenPoints[idx + 1] = v3zero;
		m_screenPoints[idx + 2] = v3zero;
		m_screenPoints[idx + 3] = v3zero;
		idx += 4;
		widthIdx += widthIdxAdd;
	}

	public void WeldJoins(int start, int end, bool connectFirstAndLast)
	{
		if (connectFirstAndLast)
		{
			SetIntersectionPoint(m_vertexCount - 4, m_vertexCount - 3, 0, 1);
			SetIntersectionPoint(m_vertexCount - 1, m_vertexCount - 2, 3, 2);
		}
		for (int i = start; i < end; i += 4)
		{
			SetIntersectionPoint(i - 4, i - 3, i, i + 1);
			SetIntersectionPoint(i - 1, i - 2, i + 3, i + 2);
		}
	}

	public void WeldJoinsDiscrete(int start, int end, bool connectFirstAndLast)
	{
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		if (connectFirstAndLast)
		{
			SetIntersectionPoint(m_vertexCount - 4, m_vertexCount - 3, 0, 1);
			SetIntersectionPoint(m_vertexCount - 1, m_vertexCount - 2, 3, 2);
		}
		int num = (start + 1) / 2 * 4;
		if (m_is2D)
		{
			for (int i = start; i < end; i += 2)
			{
				if (m_points2[i] == m_points2[i + 1])
				{
					SetIntersectionPoint(num - 4, num - 3, num, num + 1);
					SetIntersectionPoint(num - 1, num - 2, num + 3, num + 2);
				}
				num += 4;
			}
			return;
		}
		for (int j = start; j < end; j += 2)
		{
			if (m_points3[j] == m_points3[j + 1])
			{
				SetIntersectionPoint(num - 4, num - 3, num, num + 1);
				SetIntersectionPoint(num - 1, num - 2, num + 3, num + 2);
			}
			num += 4;
		}
	}

	public void SetIntersectionPoint(int p1, int p2, int p3, int p4)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = m_lineVertices[p1];
		Vector3 val2 = m_lineVertices[p2];
		Vector3 val3 = m_lineVertices[p3];
		Vector3 val4 = m_lineVertices[p4];
		if ((val.x == val2.x && val.y == val2.y) || (val3.x == val4.x && val3.y == val4.y))
		{
			return;
		}
		float num = (val4.y - val3.y) * (val2.x - val.x) - (val4.x - val3.x) * (val2.y - val.y);
		if (num > -0.005f && num < 0.005f)
		{
			if (Mathf.Abs(val2.x - val3.x) < 0.005f && Mathf.Abs(val2.y - val3.y) < 0.005f)
			{
				m_lineVertices[p2] = (val2 + val3) * 0.5f;
				m_lineVertices[p3] = m_lineVertices[p2];
			}
			return;
		}
		float num2 = ((val4.x - val3.x) * (val.y - val3.y) - (val4.y - val3.y) * (val.x - val3.x)) / num;
		Vector3 val5 = default(Vector3);
		((Vector3)(ref val5))._002Ector(val.x + num2 * (val2.x - val.x), val.y + num2 * (val2.y - val.y), val.z);
		Vector3 val6 = val5 - val2;
		if (!(((Vector3)(ref val6)).sqrMagnitude > m_maxWeldDistance))
		{
			m_lineVertices[p2] = val5;
			m_lineVertices[p3] = val5;
		}
	}

	public void WeldJoins3D(int start, int end, bool connectFirstAndLast)
	{
		if (connectFirstAndLast)
		{
			SetIntersectionPoint3D(m_vertexCount - 4, m_vertexCount - 3, 0, 1);
			SetIntersectionPoint3D(m_vertexCount - 1, m_vertexCount - 2, 3, 2);
		}
		if (m_drawStart > 0)
		{
			start += 4;
		}
		for (int i = start; i < end; i += 4)
		{
			SetIntersectionPoint3D(i - 4, i - 3, i, i + 1);
			SetIntersectionPoint3D(i - 1, i - 2, i + 3, i + 2);
		}
	}

	public void WeldJoinsDiscrete3D(int start, int end, bool connectFirstAndLast)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (connectFirstAndLast)
		{
			SetIntersectionPoint3D(m_vertexCount - 4, m_vertexCount - 3, 0, 1);
			SetIntersectionPoint3D(m_vertexCount - 1, m_vertexCount - 2, 3, 2);
		}
		int num = (start + 1) / 2 * 4;
		for (int i = start; i < end; i += 2)
		{
			if (m_points3[i] == m_points3[i + 1])
			{
				SetIntersectionPoint3D(num - 4, num - 3, num, num + 1);
				SetIntersectionPoint3D(num - 1, num - 2, num + 3, num + 2);
			}
			num += 4;
		}
	}

	public void SetIntersectionPoint3D(int p1, int p2, int p3, int p4)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = m_screenPoints[p1];
		Vector3 val2 = m_screenPoints[p2];
		Vector3 val3 = m_screenPoints[p3];
		Vector3 val4 = m_screenPoints[p4];
		if ((val.x == val2.x && val.y == val2.y) || (val3.x == val4.x && val3.y == val4.y))
		{
			return;
		}
		float num = (val4.y - val3.y) * (val2.x - val.x) - (val4.x - val3.x) * (val2.y - val.y);
		if (num > -0.005f && num < 0.005f)
		{
			if (Mathf.Abs(val2.x - val3.x) < 0.005f && Mathf.Abs(val2.y - val3.y) < 0.005f)
			{
				m_lineVertices[p2] = cam3D.ScreenToWorldPoint((val2 + val3) * 0.5f);
				m_lineVertices[p3] = m_lineVertices[p2];
			}
			return;
		}
		float num2 = ((val4.x - val3.x) * (val.y - val3.y) - (val4.y - val3.y) * (val.x - val3.x)) / num;
		Vector3 val5 = default(Vector3);
		((Vector3)(ref val5))._002Ector(val.x + num2 * (val2.x - val.x), val.y + num2 * (val2.y - val.y), val.z);
		Vector3 val6 = val5 - val2;
		if (!(((Vector3)(ref val6)).sqrMagnitude > m_maxWeldDistance))
		{
			m_lineVertices[p2] = cam3D.ScreenToWorldPoint(val5);
			m_lineVertices[p3] = m_lineVertices[p2];
		}
	}

	public static void LineManagerCheckDistance()
	{
		lineManager.StartCheckDistance();
	}

	public static void LineManagerDisable()
	{
		lineManager.DisableIfUnused();
	}

	public static void LineManagerEnable()
	{
		lineManager.EnableIfUsed();
	}

	public void Draw3DAuto()
	{
		Draw3DAuto(0f);
	}

	public void Draw3DAuto(float time)
	{
		if (time < 0f)
		{
			time = 0f;
		}
		lineManager.AddLine(this, m_drawTransform, time);
		m_isAutoDrawing = true;
		Draw3D();
	}

	public void StopDrawing3DAuto()
	{
		lineManager.RemoveLine(this);
		m_isAutoDrawing = false;
	}

	public void SetTextureScale()
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_040c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0411: Unknown result type (might be due to invalid IL or missing references)
		//IL_0416: Unknown result type (might be due to invalid IL or missing references)
		//IL_041b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_041f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Unknown result type (might be due to invalid IL or missing references)
		//IL_0435: Unknown result type (might be due to invalid IL or missing references)
		//IL_043c: Unknown result type (might be due to invalid IL or missing references)
		//IL_045a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0461: Unknown result type (might be due to invalid IL or missing references)
		//IL_0469: Unknown result type (might be due to invalid IL or missing references)
		//IL_0470: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		if (pointsCount != m_pointsCount)
		{
			Resize();
		}
		SetupDrawStartEnd(out var _, out var end, clearVertices: false);
		int num = ((m_lineType != LineType.Discrete) ? 1 : 2);
		int num2 = 0;
		int num3 = 0;
		int num4 = ((m_lineWidths.Length != 1) ? 1 : 0);
		float num5 = 1f / m_textureScale;
		bool flag = (Object)(object)m_drawTransform != (Object)null;
		Matrix4x4 val = (flag ? m_drawTransform.localToWorldMatrix : Matrix4x4.identity);
		Vector2 val2 = Vector2.zero;
		Vector2 val3 = Vector2.zero;
		Vector2 zero = Vector2.zero;
		float num6 = m_textureOffset;
		float num7 = m_capLength * 2f;
		if (m_is2D)
		{
			for (int i = 0; i < end; i += num)
			{
				if (!m_viewportDraw)
				{
					if (flag)
					{
						val2 = Vector2.op_Implicit(((Matrix4x4)(ref val)).MultiplyPoint3x4(Vector2.op_Implicit(m_points2[i])));
						val3 = Vector2.op_Implicit(((Matrix4x4)(ref val)).MultiplyPoint3x4(Vector2.op_Implicit(m_points2[i + 1])));
					}
					else
					{
						val2.x = m_points2[i].x;
						val2.y = m_points2[i].y;
						val3.x = m_points2[i + 1].x;
						val3.y = m_points2[i + 1].y;
					}
				}
				else if (flag)
				{
					val2 = Vector2.op_Implicit(((Matrix4x4)(ref val)).MultiplyPoint3x4(Vector2.op_Implicit(new Vector2(m_points2[i].x * (float)Screen.width, m_points2[i].y * (float)Screen.height))));
					val3 = Vector2.op_Implicit(((Matrix4x4)(ref val)).MultiplyPoint3x4(Vector2.op_Implicit(new Vector2(m_points2[i + 1].x * (float)Screen.width, m_points2[i + 1].y * (float)Screen.height))));
				}
				else
				{
					((Vector2)(ref val2))._002Ector(m_points2[i].x * (float)Screen.width, m_points2[i].y * (float)Screen.height);
					((Vector2)(ref val3))._002Ector(m_points2[i + 1].x * (float)Screen.width, m_points2[i + 1].y * (float)Screen.height);
				}
				zero.x = val3.x - val2.x;
				zero.y = val3.y - val2.y;
				float num8 = num5 / (m_lineWidths[num3] * 2f / ((float)Math.Sqrt(zero.x * zero.x + zero.y * zero.y) + num7));
				m_lineUVs[num2].x = num6;
				m_lineUVs[num2 + 3].x = num6;
				m_lineUVs[num2 + 2].x = num8 + num6;
				m_lineUVs[num2 + 1].x = num8 + num6;
				num2 += 4;
				num6 = (num6 + num8) % 1f;
				num3 += num4;
			}
		}
		else
		{
			if (!CheckCamera3D())
			{
				return;
			}
			for (int j = 0; j < end; j += num)
			{
				if (flag)
				{
					val2 = Vector2.op_Implicit(cam3D.WorldToScreenPoint(((Matrix4x4)(ref val)).MultiplyPoint3x4(m_points3[j])));
					val3 = Vector2.op_Implicit(cam3D.WorldToScreenPoint(((Matrix4x4)(ref val)).MultiplyPoint3x4(m_points3[j + 1])));
				}
				else
				{
					val2 = Vector2.op_Implicit(cam3D.WorldToScreenPoint(m_points3[j]));
					val3 = Vector2.op_Implicit(cam3D.WorldToScreenPoint(m_points3[j + 1]));
				}
				zero.x = val2.x - val3.x;
				zero.y = val2.y - val3.y;
				float num9 = num5 / (m_lineWidths[num3] * 2f / (float)Math.Sqrt(zero.x * zero.x + zero.y * zero.y));
				m_lineUVs[num2].x = num6;
				m_lineUVs[num2 + 3].x = num6;
				m_lineUVs[num2 + 2].x = num9 + num6;
				m_lineUVs[num2 + 1].x = num9 + num6;
				num2 += 4;
				num6 = (num6 + num9) % 1f;
				num3 += num4;
			}
		}
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateUVs();
		}
	}

	public void ResetTextureScale()
	{
		for (int i = 0; i < m_vertexCount; i += 4)
		{
			m_lineUVs[i].x = 0f;
			m_lineUVs[i + 3].x = 0f;
			m_lineUVs[i + 2].x = 1f;
			m_lineUVs[i + 1].x = 1f;
		}
		if (m_vectorObject != null)
		{
			m_vectorObject.UpdateUVs();
		}
	}

	public void SetCollider(bool convertToWorldSpace)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		if (!Object.op_Implicit((Object)(object)cam3D))
		{
			SetCamera3D();
			if (!Object.op_Implicit((Object)(object)cam3D))
			{
				Debug.LogError((object)"No camera available...use VectorLine.SetCamera3D to assign a camera");
				return;
			}
		}
		if (((Component)cam3D).transform.rotation != Quaternion.identity)
		{
			Debug.LogWarning((object)"The line collider will not be correct if the camera is rotated");
		}
		Vector3 v = default(Vector3);
		((Vector3)(ref v))._002Ector(0f, 0f, 0f - ((Component)cam3D).transform.position.z);
		int num = drawStart;
		int num2 = drawEnd;
		bool flag = m_capType != EndCap.None && m_capType <= EndCap.Mirror && drawStart == 0;
		bool flag2 = m_capType != EndCap.None && m_capType >= EndCap.Both && drawEnd == pointsCount - 1;
		int num3 = 0;
		if (m_lineType == LineType.Continuous)
		{
			Component component = m_go.GetComponent(typeof(EdgeCollider2D));
			EdgeCollider2D val = (EdgeCollider2D)(object)((component is EdgeCollider2D) ? component : null);
			int num4 = (num2 - num) * 4 + 1;
			if (flag)
			{
				num4 += 4;
			}
			if (flag2)
			{
				num4 += 4;
			}
			Vector2[] array = (Vector2[])(object)new Vector2[num4];
			int startIdx = 0;
			int endIdx = array.Length - 2;
			if (convertToWorldSpace)
			{
				if (flag)
				{
					num3 = m_vertexCount;
					SetPathWorldVerticesContinuous(ref num3, ref v, ref startIdx, ref endIdx, array);
				}
				for (num3 = num * 4; num3 < num2 * 4; num3 += 4)
				{
					SetPathWorldVerticesContinuous(ref num3, ref v, ref startIdx, ref endIdx, array);
				}
				if (flag2)
				{
					num3 = m_vertexCount + 4;
					SetPathWorldVerticesContinuous(ref num3, ref v, ref startIdx, ref endIdx, array);
				}
			}
			else
			{
				if (flag)
				{
					num3 = m_vertexCount;
					SetPathVerticesContinuous(ref num3, ref startIdx, ref endIdx, array);
				}
				for (num3 = num * 4; num3 < num2 * 4; num3 += 4)
				{
					SetPathVerticesContinuous(ref num3, ref startIdx, ref endIdx, array);
				}
				if (flag)
				{
					num3 = m_vertexCount + 4;
					SetPathVerticesContinuous(ref num3, ref startIdx, ref endIdx, array);
				}
			}
			array[array.Length - 1] = array[0];
			val.points = array;
			return;
		}
		Component component2 = m_go.GetComponent(typeof(PolygonCollider2D));
		PolygonCollider2D val2 = (PolygonCollider2D)(object)((component2 is PolygonCollider2D) ? component2 : null);
		Vector2[] path = (Vector2[])(object)new Vector2[4];
		int num5 = (num2 - num + 1) / 2;
		if (flag)
		{
			num5++;
		}
		if (flag2)
		{
			num5++;
		}
		val2.pathCount = num5;
		int num6 = (num2 + 1) / 2 * 4;
		int pIdx = 0;
		if (convertToWorldSpace)
		{
			if (flag)
			{
				num3 = m_vertexCount;
				SetPathWorldVerticesDiscrete(ref num3, ref v, ref pIdx, path, val2);
			}
			for (num3 = num / 2 * 4; num3 < num6; num3 += 4)
			{
				SetPathWorldVerticesDiscrete(ref num3, ref v, ref pIdx, path, val2);
			}
			if (flag2)
			{
				num3 = m_vertexCount + 4;
				SetPathWorldVerticesDiscrete(ref num3, ref v, ref pIdx, path, val2);
			}
		}
		else
		{
			if (flag)
			{
				num3 = m_vertexCount;
				SetPathVerticesDiscrete(ref num3, ref pIdx, path, val2);
			}
			for (num3 = num / 2 * 4; num3 < num6; num3 += 4)
			{
				SetPathVerticesDiscrete(ref num3, ref pIdx, path, val2);
			}
			if (flag2)
			{
				num3 = m_vertexCount + 4;
				SetPathVerticesDiscrete(ref num3, ref pIdx, path, val2);
			}
		}
	}

	public void SetPathVerticesContinuous(ref int i, ref int startIdx, ref int endIdx, Vector2[] path)
	{
		path[startIdx].x = m_lineVertices[i].x;
		path[startIdx].y = m_lineVertices[i].y;
		path[startIdx + 1].x = m_lineVertices[i + 1].x;
		path[startIdx + 1].y = m_lineVertices[i + 1].y;
		path[endIdx].x = m_lineVertices[i + 3].x;
		path[endIdx].y = m_lineVertices[i + 3].y;
		path[endIdx - 1].x = m_lineVertices[i + 2].x;
		path[endIdx - 1].y = m_lineVertices[i + 2].y;
		startIdx += 2;
		endIdx -= 2;
	}

	public void SetPathWorldVerticesContinuous(ref int i, ref Vector3 v3, ref int startIdx, ref int endIdx, Vector2[] path)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		v3.x = m_lineVertices[i].x;
		v3.y = m_lineVertices[i].y;
		path[startIdx] = Vector2.op_Implicit(cam3D.ScreenToWorldPoint(v3));
		v3.x = m_lineVertices[i + 1].x;
		v3.y = m_lineVertices[i + 1].y;
		path[startIdx + 1] = Vector2.op_Implicit(cam3D.ScreenToWorldPoint(v3));
		v3.x = m_lineVertices[i + 3].x;
		v3.y = m_lineVertices[i + 3].y;
		path[endIdx] = Vector2.op_Implicit(cam3D.ScreenToWorldPoint(v3));
		v3.x = m_lineVertices[i + 2].x;
		v3.y = m_lineVertices[i + 2].y;
		path[endIdx - 1] = Vector2.op_Implicit(cam3D.ScreenToWorldPoint(v3));
		startIdx += 2;
		endIdx -= 2;
	}

	public void SetPathVerticesDiscrete(ref int i, ref int pIdx, Vector2[] path, PolygonCollider2D collider)
	{
		path[0].x = m_lineVertices[i].x;
		path[0].y = m_lineVertices[i].y;
		path[1].x = m_lineVertices[i + 3].x;
		path[1].y = m_lineVertices[i + 3].y;
		path[2].x = m_lineVertices[i + 2].x;
		path[2].y = m_lineVertices[i + 2].y;
		path[3].x = m_lineVertices[i + 1].x;
		path[3].y = m_lineVertices[i + 1].y;
		collider.SetPath(pIdx++, path);
	}

	public void SetPathWorldVerticesDiscrete(ref int i, ref Vector3 v3, ref int pIdx, Vector2[] path, PolygonCollider2D collider)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		v3.x = m_lineVertices[i].x;
		v3.y = m_lineVertices[i].y;
		path[0] = Vector2.op_Implicit(cam3D.ScreenToWorldPoint(v3));
		v3.x = m_lineVertices[i + 3].x;
		v3.y = m_lineVertices[i + 3].y;
		path[1] = Vector2.op_Implicit(cam3D.ScreenToWorldPoint(v3));
		v3.x = m_lineVertices[i + 2].x;
		v3.y = m_lineVertices[i + 2].y;
		path[2] = Vector2.op_Implicit(cam3D.ScreenToWorldPoint(v3));
		v3.x = m_lineVertices[i + 1].x;
		v3.y = m_lineVertices[i + 1].y;
		path[3] = Vector2.op_Implicit(cam3D.ScreenToWorldPoint(v3));
		collider.SetPath(pIdx++, path);
	}

	public static List<Vector3> BytesToVector3List(byte[] lineBytes)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		if (lineBytes.Length % 12 != 0)
		{
			Debug.LogError((object)"VectorLine.BytesToVector3Array: Incorrect input byte length...must be a multiple of 12");
			return null;
		}
		SetupByteBlock();
		List<Vector3> list = new List<Vector3>(lineBytes.Length / 12);
		for (int i = 0; i < lineBytes.Length; i += 12)
		{
			list.Add(new Vector3(ConvertToFloat(lineBytes, i), ConvertToFloat(lineBytes, i + 4), ConvertToFloat(lineBytes, i + 8)));
		}
		return list;
	}

	public static List<Vector2> BytesToVector2List(byte[] lineBytes)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (lineBytes.Length % 8 != 0)
		{
			Debug.LogError((object)"VectorLine.BytesToVector2Array: Incorrect input byte length...must be a multiple of 8");
			return null;
		}
		SetupByteBlock();
		List<Vector2> list = new List<Vector2>(lineBytes.Length / 8);
		for (int i = 0; i < lineBytes.Length; i += 8)
		{
			list.Add(new Vector2(ConvertToFloat(lineBytes, i), ConvertToFloat(lineBytes, i + 4)));
		}
		return list;
	}

	public static void SetupByteBlock()
	{
		if (byteBlock == null)
		{
			byteBlock = new byte[4];
		}
		if (BitConverter.IsLittleEndian)
		{
			endianDiff1 = 0;
			endianDiff2 = 0;
		}
		else
		{
			endianDiff1 = 3;
			endianDiff2 = 1;
		}
	}

	public static float ConvertToFloat(byte[] bytes, int i)
	{
		byteBlock[endianDiff1] = bytes[i];
		byteBlock[1 + endianDiff2] = bytes[i + 1];
		byteBlock[2 - endianDiff2] = bytes[i + 2];
		byteBlock[3 - endianDiff1] = bytes[i + 3];
		return BitConverter.ToSingle(byteBlock, 0);
	}

	public static void Destroy(ref VectorLine line)
	{
		DestroyLine(ref line);
	}

	public static void Destroy(VectorLine[] lines)
	{
		for (int i = 0; i < lines.Length; i++)
		{
			DestroyLine(ref lines[i]);
		}
	}

	public static void Destroy(List<VectorLine> lines)
	{
		for (int i = 0; i < lines.Count; i++)
		{
			VectorLine line = lines[i];
			DestroyLine(ref line);
		}
	}

	public static void DestroyLine(ref VectorLine line)
	{
		if (line != null)
		{
			Object.Destroy((Object)(object)line.m_go);
			if (line.m_vectorObject != null)
			{
				line.m_vectorObject.Destroy();
			}
			if (line.isAutoDrawing)
			{
				line.StopDrawing3DAuto();
			}
			line = null;
		}
	}

	public static void Destroy(ref VectorLine line, GameObject go)
	{
		Destroy(ref line);
		if ((Object)(object)go != (Object)null)
		{
			Object.Destroy((Object)(object)go);
		}
	}

	public void SetDistances()
	{
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		if (m_lineType == LineType.Points)
		{
			return;
		}
		if (m_distances == null || m_distances.Length != ((m_lineType != LineType.Discrete) ? pointsCount : (pointsCount / 2 + 1)))
		{
			m_distances = new float[(m_lineType != LineType.Discrete) ? pointsCount : (pointsCount / 2 + 1)];
		}
		double num = 0.0;
		int num2 = pointsCount - 1;
		if (is2D)
		{
			if (m_lineType != LineType.Discrete)
			{
				for (int i = 0; i < num2; i++)
				{
					Vector2 val = m_points2[i] - m_points2[i + 1];
					num += Math.Sqrt(val.x * val.x + val.y * val.y);
					m_distances[i + 1] = (float)num;
				}
				return;
			}
			int num3 = 1;
			for (int j = 0; j < num2; j += 2)
			{
				Vector2 val2 = m_points2[j] - m_points2[j + 1];
				num += Math.Sqrt(val2.x * val2.x + val2.y * val2.y);
				m_distances[num3++] = (float)num;
			}
		}
		else if (m_lineType != LineType.Discrete)
		{
			for (int k = 0; k < num2; k++)
			{
				Vector3 val3 = m_points3[k] - m_points3[k + 1];
				num += Math.Sqrt(val3.x * val3.x + val3.y * val3.y + val3.z * val3.z);
				m_distances[k + 1] = (float)num;
			}
		}
		else
		{
			int num4 = 1;
			for (int l = 0; l < num2; l += 2)
			{
				Vector3 val4 = m_points3[l] - m_points3[l + 1];
				num += Math.Sqrt(val4.x * val4.x + val4.y * val4.y + val4.z * val4.z);
				m_distances[num4++] = (float)num;
			}
		}
	}

	public float GetLength()
	{
		if (m_distances == null || m_distances.Length != ((m_lineType != LineType.Discrete) ? pointsCount : (pointsCount / 2 + 1)))
		{
			SetDistances();
		}
		return m_distances[m_distances.Length - 1];
	}

	public Vector2 GetPoint01(float distance)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		int index;
		return GetPoint(Mathf.Lerp(0f, GetLength(), distance), out index);
	}

	public Vector2 GetPoint01(float distance, out int index)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		return GetPoint(Mathf.Lerp(0f, GetLength(), distance), out index);
	}

	public Vector2 GetPoint(float distance)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		int index;
		return GetPoint(distance, out index);
	}

	public Vector2 GetPoint(float distance, out int index)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		if (!m_is2D)
		{
			Debug.LogError((object)"VectorLine.GetPoint only works with Vector2 points");
			index = 0;
			return Vector2.zero;
		}
		SetDistanceIndex(out index, distance);
		Vector2 val = ((m_lineType == LineType.Discrete) ? Vector2.Lerp(m_points2[(index - 1) * 2], m_points2[(index - 1) * 2 + 1], Mathf.InverseLerp(m_distances[index - 1], m_distances[index], distance)) : Vector2.Lerp(m_points2[index - 1], m_points2[index], Mathf.InverseLerp(m_distances[index - 1], m_distances[index], distance)));
		if (Object.op_Implicit((Object)(object)m_drawTransform))
		{
			Matrix4x4 localToWorldMatrix = m_drawTransform.localToWorldMatrix;
			val = Vector2.op_Implicit(((Matrix4x4)(ref localToWorldMatrix)).MultiplyPoint3x4(Vector2.op_Implicit(val)));
		}
		index--;
		return val;
	}

	public Vector3 GetPoint3D01(float distance)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		int index;
		return GetPoint3D(Mathf.Lerp(0f, GetLength(), distance), out index);
	}

	public Vector3 GetPoint3D01(float distance, out int index)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		return GetPoint3D(Mathf.Lerp(0f, GetLength(), distance), out index);
	}

	public Vector3 GetPoint3D(float distance)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		int index;
		return GetPoint3D(distance, out index);
	}

	public Vector3 GetPoint3D(float distance, out int index)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		if (m_is2D)
		{
			Debug.LogError((object)"VectorLine.GetPoint3D only works with Vector3 points");
			index = 0;
			return Vector3.zero;
		}
		SetDistanceIndex(out index, distance);
		Vector3 val = ((m_lineType == LineType.Discrete) ? Vector3.Lerp(m_points3[(index - 1) * 2], m_points3[(index - 1) * 2 + 1], Mathf.InverseLerp(m_distances[index - 1], m_distances[index], distance)) : Vector3.Lerp(m_points3[index - 1], m_points3[index], Mathf.InverseLerp(m_distances[index - 1], m_distances[index], distance)));
		if (Object.op_Implicit((Object)(object)m_drawTransform))
		{
			Matrix4x4 localToWorldMatrix = m_drawTransform.localToWorldMatrix;
			val = ((Matrix4x4)(ref localToWorldMatrix)).MultiplyPoint3x4(val);
		}
		index--;
		return val;
	}

	public void SetDistanceIndex(out int i, float distance)
	{
		if (m_distances == null)
		{
			SetDistances();
		}
		i = m_drawStart + 1;
		if (m_lineType == LineType.Discrete)
		{
			i = (i + 1) / 2;
		}
		if (i >= m_distances.Length)
		{
			i = m_distances.Length - 1;
		}
		int num = m_drawEnd;
		if (m_lineType == LineType.Discrete)
		{
			num = (num + 1) / 2;
		}
		while (distance > m_distances[i] && i < num)
		{
			i++;
		}
	}

	public static void SetEndCap(string name, EndCap capType)
	{
		SetEndCap(name, capType, 0f, 0f, 1f, 1f, (Texture2D[])null);
	}

	public static void SetEndCap(string name, EndCap capType, params Texture2D[] textures)
	{
		SetEndCap(name, capType, 0f, 0f, 1f, 1f, textures);
	}

	public static void SetEndCap(string name, EndCap capType, float offset, params Texture2D[] textures)
	{
		SetEndCap(name, capType, offset, offset, 1f, 1f, textures);
	}

	public static void SetEndCap(string name, EndCap capType, float offsetFront, float offsetBack, params Texture2D[] textures)
	{
		SetEndCap(name, capType, offsetFront, offsetBack, 1f, 1f, textures);
	}

	public static void SetEndCap(string name, EndCap capType, float offsetFront, float offsetBack, float scaleFront, float scaleBack, params Texture2D[] textures)
	{
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0327: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Expected O, but got Unknown
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		if (capDictionary == null)
		{
			capDictionary = new Dictionary<string, CapInfo>();
		}
		if (name == null || name == "")
		{
			Debug.LogError((object)"VectorLine.SetEndCap: must supply a name");
			return;
		}
		if (capDictionary.ContainsKey(name) && capType != EndCap.None)
		{
			Debug.LogError((object)("VectorLine.SetEndCap: end cap \"" + name + "\" has already been set up"));
			return;
		}
		switch (capType)
		{
		case EndCap.None:
			RemoveEndCap(name);
			return;
		case EndCap.Front:
		case EndCap.Mirror:
		case EndCap.Back:
			if (textures.Length < 2)
			{
				Debug.LogError((object)("VectorLine.SetEndCap (\"" + name + "\"): must supply two textures when using SetEndCap with EndCap.Front, EndCap.Back, or EndCap.Mirror"));
				return;
			}
			break;
		}
		if ((Object)(object)textures[0] == (Object)null || (Object)(object)textures[1] == (Object)null)
		{
			Debug.LogError((object)("VectorLine.SetEndCap (\"" + name + "\"): end cap textures must not be null"));
			return;
		}
		if (((Texture)textures[0]).width != ((Texture)textures[0]).height)
		{
			Debug.LogError((object)("VectorLine.SetEndCap (\"" + name + "\"): the line texture must be square"));
			return;
		}
		if (((Texture)textures[1]).height != ((Texture)textures[0]).height)
		{
			Debug.LogError((object)("VectorLine.SetEndCap (\"" + name + "\"): all textures must be the same height"));
			return;
		}
		if (capType == EndCap.Both)
		{
			if (textures.Length < 3)
			{
				Debug.LogError((object)("VectorLine.SetEndCap (\"" + name + "\"): must supply three textures when using SetEndCap with EndCap.Both"));
				return;
			}
			if ((Object)(object)textures[2] == (Object)null)
			{
				Debug.LogError((object)("VectorLine.SetEndCap (\"" + name + "\"): end cap textures must not be null"));
				return;
			}
			if (((Texture)textures[2]).height != ((Texture)textures[0]).height)
			{
				Debug.LogError((object)("VectorLine.SetEndCap (\"" + name + "\"): all textures must be the same height"));
				return;
			}
		}
		Texture2D val = textures[0];
		Texture2D val2 = textures[1];
		Texture2D val3 = ((textures.Length == 3) ? textures[2] : null);
		int num = 4;
		int width = ((Texture)val).width;
		float num2 = 0f;
		float ratio = 0f;
		int num3 = 0;
		int num4 = 0;
		Color32[] array = null;
		Color32[] array2 = null;
		switch (capType)
		{
		case EndCap.Front:
			array = GetRotatedPixels(val2);
			num3 = ((Texture)val2).width;
			array2 = GetRowPixels(array, num, 0, width);
			num4 = num;
			num2 = (float)((Texture)val2).width / (float)((Texture)val2).height;
			break;
		case EndCap.Back:
			array2 = GetRotatedPixels(val2);
			num4 = ((Texture)val2).width;
			array = GetRowPixels(array2, num, num4 - 1, width);
			num3 = num;
			ratio = (float)((Texture)val2).width / (float)((Texture)val2).height;
			break;
		case EndCap.Both:
			array = GetRotatedPixels(val2);
			num3 = ((Texture)val2).width;
			array2 = GetRotatedPixels(val3);
			num4 = ((Texture)val3).width;
			num2 = (float)((Texture)val2).width / (float)((Texture)val2).height;
			ratio = (float)((Texture)val3).width / (float)((Texture)val3).height;
			break;
		case EndCap.Mirror:
			array = GetRotatedPixels(val2);
			num3 = ((Texture)val2).width;
			array2 = GetRowPixels(array, num, 0, width);
			num4 = num;
			num2 = (float)((Texture)val2).width / (float)((Texture)val2).height;
			ratio = num2;
			break;
		}
		int num5 = ((Texture)val).height + num3 + num4 + num * 4;
		Color32[] pixels = val.GetPixels32();
		Color32[] array3 = (Color32[])(object)new Color32[num * width];
		Color32 val4 = Color32.op_Implicit(Color.clear);
		for (int i = 0; i < num * width; i++)
		{
			array3[i] = val4;
		}
		Color32[] rowPixels = GetRowPixels(array2, num, num4 - 1, width);
		Color32[] rowPixels2 = GetRowPixels(array, num, 0, width);
		bool flag = ((Texture)val).mipmapCount > 1;
		Texture2D val5 = new Texture2D(width, num5, (TextureFormat)5, flag);
		((Object)val5).name = ((Object)val).name + " end cap";
		((Texture)val5).wrapMode = ((Texture)val).wrapMode;
		((Texture)val5).filterMode = ((Texture)val).filterMode;
		float num6 = 1f / (float)num5;
		float[] array4 = new float[6];
		int num7 = 0;
		val5.SetPixels32(0, 0, width, num, array3);
		num7 += num;
		array4[0] = num6 * (float)num7;
		val5.SetPixels32(0, num7, width, ((Texture)val).height, pixels);
		num7 += ((Texture)val).height;
		array4[1] = num6 * (float)num7;
		val5.SetPixels32(0, num7, width, num, array3);
		num7 += num;
		array4[2] = num6 * (float)num7;
		val5.SetPixels32(0, num7, width, num4, array2);
		num7 += num4;
		array4[3] = num6 * (float)num7;
		val5.SetPixels32(0, num7, width, num, rowPixels);
		num7 += num;
		val5.SetPixels32(0, num7, width, num, rowPixels2);
		num7 += num;
		array4[4] = num6 * (float)num7;
		val5.SetPixels32(0, num7, width, num3, array);
		array4[5] = num6 * (float)(num7 + num3);
		val5.Apply(flag, true);
		capDictionary.Add(name, new CapInfo(capType, (Texture)(object)val5, num2, ratio, offsetFront, offsetBack, scaleFront, scaleBack, array4));
	}

	public static Color32[] GetRowPixels(Color32[] texPixels, int numberOfRows, int row, int w)
	{
		Color32[] array = (Color32[])(object)new Color32[w * numberOfRows];
		for (int i = 0; i < numberOfRows; i++)
		{
			Array.Copy(texPixels, row * w, array, i * w, w);
		}
		return array;
	}

	public static Color32[] GetRotatedPixels(Texture2D tex)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		Color32[] pixels = tex.GetPixels32();
		Color32[] array = (Color32[])(object)new Color32[pixels.Length];
		int width = ((Texture)tex).width;
		int height = ((Texture)tex).height;
		int num = 0;
		for (int i = 0; i < height; i++)
		{
			int num2 = ((Texture)tex).width - 1;
			for (int j = 0; j < width; j++)
			{
				array[num2 * height + num] = pixels[i * width + j];
				num2--;
			}
			num++;
		}
		return array;
	}

	public static void RemoveEndCap(string name)
	{
		if (!capDictionary.ContainsKey(name))
		{
			Debug.LogError((object)("VectorLine: RemoveEndCap: \"" + name + "\" has not been set up"));
			return;
		}
		Object.Destroy((Object)(object)capDictionary[name].texture);
		capDictionary.Remove(name);
	}

	public bool Selected(Vector2 p)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		int index;
		return Selected(p, 0, 0, out index, cam3D);
	}

	public bool Selected(Vector2 p, out int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return Selected(p, 0, 0, out index, cam3D);
	}

	public bool Selected(Vector2 p, int extraDistance, out int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return Selected(p, extraDistance, 0, out index, cam3D);
	}

	public bool Selected(Vector2 p, int extraDistance, int extraLength, out int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return Selected(p, extraDistance, extraLength, out index, cam3D);
	}

	public bool Selected(Vector2 p, Camera cam)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		int index;
		return Selected(p, 0, 0, out index, cam);
	}

	public bool Selected(Vector2 p, out int index, Camera cam)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return Selected(p, 0, 0, out index, cam);
	}

	public bool Selected(Vector2 p, int extraDistance, out int index, Camera cam)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return Selected(p, extraDistance, 0, out index, cam);
	}

	public bool Selected(Vector2 p, int extraDistance, int extraLength, out int index, Camera cam)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0534: Unknown result type (might be due to invalid IL or missing references)
		//IL_0539: Unknown result type (might be due to invalid IL or missing references)
		//IL_053e: Unknown result type (might be due to invalid IL or missing references)
		//IL_054c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0551: Unknown result type (might be due to invalid IL or missing references)
		//IL_0556: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0502: Unknown result type (might be due to invalid IL or missing references)
		//IL_0507: Unknown result type (might be due to invalid IL or missing references)
		//IL_0517: Unknown result type (might be due to invalid IL or missing references)
		//IL_051c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0521: Unknown result type (might be due to invalid IL or missing references)
		//IL_0526: Unknown result type (might be due to invalid IL or missing references)
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_0307: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0558: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0569: Unknown result type (might be due to invalid IL or missing references)
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		//IL_0396: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_057c: Unknown result type (might be due to invalid IL or missing references)
		//IL_058c: Unknown result type (might be due to invalid IL or missing references)
		//IL_059c: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_042a: Unknown result type (might be due to invalid IL or missing references)
		//IL_042b: Unknown result type (might be due to invalid IL or missing references)
		//IL_042d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0432: Unknown result type (might be due to invalid IL or missing references)
		//IL_0434: Unknown result type (might be due to invalid IL or missing references)
		//IL_0436: Unknown result type (might be due to invalid IL or missing references)
		//IL_0440: Unknown result type (might be due to invalid IL or missing references)
		//IL_0442: Unknown result type (might be due to invalid IL or missing references)
		//IL_0444: Unknown result type (might be due to invalid IL or missing references)
		//IL_0449: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_040f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0644: Unknown result type (might be due to invalid IL or missing references)
		//IL_0645: Unknown result type (might be due to invalid IL or missing references)
		//IL_0647: Unknown result type (might be due to invalid IL or missing references)
		//IL_064c: Unknown result type (might be due to invalid IL or missing references)
		//IL_064e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0650: Unknown result type (might be due to invalid IL or missing references)
		//IL_065a: Unknown result type (might be due to invalid IL or missing references)
		//IL_065c: Unknown result type (might be due to invalid IL or missing references)
		//IL_065e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0663: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0605: Unknown result type (might be due to invalid IL or missing references)
		//IL_0617: Unknown result type (might be due to invalid IL or missing references)
		//IL_0629: Unknown result type (might be due to invalid IL or missing references)
		//IL_063b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0467: Unknown result type (might be due to invalid IL or missing references)
		//IL_0468: Unknown result type (might be due to invalid IL or missing references)
		//IL_046c: Unknown result type (might be due to invalid IL or missing references)
		//IL_046e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0470: Unknown result type (might be due to invalid IL or missing references)
		//IL_0475: Unknown result type (might be due to invalid IL or missing references)
		//IL_047a: Unknown result type (might be due to invalid IL or missing references)
		//IL_047f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0484: Unknown result type (might be due to invalid IL or missing references)
		//IL_0681: Unknown result type (might be due to invalid IL or missing references)
		//IL_0682: Unknown result type (might be due to invalid IL or missing references)
		//IL_0686: Unknown result type (might be due to invalid IL or missing references)
		//IL_0688: Unknown result type (might be due to invalid IL or missing references)
		//IL_068a: Unknown result type (might be due to invalid IL or missing references)
		//IL_068f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0694: Unknown result type (might be due to invalid IL or missing references)
		//IL_0699: Unknown result type (might be due to invalid IL or missing references)
		//IL_069e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)cam == (Object)null)
		{
			SetCamera3D();
			if (!Object.op_Implicit((Object)(object)cam3D))
			{
				Debug.LogError((object)"VectorLine.Selected: camera cannot be null. If there is no camera tagged \"MainCamera\", supply one manually");
				index = 0;
				return false;
			}
			cam = cam3D;
		}
		int num = ((m_lineWidths.Length != 1) ? 1 : 0);
		int num2 = ((m_lineType != LineType.Discrete) ? (m_drawStart - num) : (m_drawStart / 2 - num));
		if (m_lineWidths.Length == 1)
		{
			num = 0;
			num2 = 0;
		}
		else
		{
			num = 1;
		}
		int num3 = m_drawEnd;
		bool flag = (Object)(object)m_drawTransform != (Object)null;
		Matrix4x4 val = (flag ? m_drawTransform.localToWorldMatrix : Matrix4x4.identity);
		Vector2 val2 = default(Vector2);
		((Vector2)(ref val2))._002Ector((float)Screen.width, (float)Screen.height);
		if (m_lineType == LineType.Points)
		{
			if (num3 == pointsCount)
			{
				num3--;
			}
			if (m_is2D)
			{
				for (int i = m_drawStart; i <= num3; i++)
				{
					num2 += num;
					float num4 = m_lineWidths[num2] + (float)extraDistance;
					Vector2 val3 = (flag ? Vector2.op_Implicit(((Matrix4x4)(ref val)).MultiplyPoint3x4(Vector2.op_Implicit(m_points2[i]))) : m_points2[i]);
					if (m_viewportDraw)
					{
						val3.x *= val2.x;
						val3.y *= val2.y;
					}
					if (p.x >= val3.x - num4 && p.x <= val3.x + num4 && p.y >= val3.y - num4 && p.y <= val3.y + num4)
					{
						index = i;
						return true;
					}
				}
				index = -1;
				return false;
			}
			for (int j = m_drawStart; j <= num3; j++)
			{
				num2 += num;
				float num5 = m_lineWidths[num2] + (float)extraDistance;
				Vector2 val3 = Vector2.op_Implicit(flag ? cam.WorldToScreenPoint(((Matrix4x4)(ref val)).MultiplyPoint3x4(m_points3[j])) : cam.WorldToScreenPoint(m_points3[j]));
				if (p.x >= val3.x - num5 && p.x <= val3.x + num5 && p.y >= val3.y - num5 && p.y <= val3.y + num5)
				{
					index = j;
					return true;
				}
			}
			index = -1;
			return false;
		}
		float num6 = 0f;
		int num7 = ((m_lineType != LineType.Discrete) ? 1 : 2);
		Vector2 zero = Vector2.zero;
		if (m_lineType != LineType.Discrete && m_drawEnd == pointsCount)
		{
			num3--;
		}
		Vector2 val4 = default(Vector2);
		Vector2 val5 = default(Vector2);
		Vector2 val6;
		if (m_is2D)
		{
			for (int k = m_drawStart; k < num3; k += num7)
			{
				num2 += num;
				if (flag)
				{
					val4 = Vector2.op_Implicit(((Matrix4x4)(ref val)).MultiplyPoint3x4(Vector2.op_Implicit(m_points2[k])));
					val5 = Vector2.op_Implicit(((Matrix4x4)(ref val)).MultiplyPoint3x4(Vector2.op_Implicit(m_points2[k + 1])));
				}
				else
				{
					val4.x = m_points2[k].x;
					val4.y = m_points2[k].y;
					val5.x = m_points2[k + 1].x;
					val5.y = m_points2[k + 1].y;
				}
				if (m_viewportDraw)
				{
					val4.x *= val2.x;
					val4.y *= val2.y;
					val5.x *= val2.x;
					val5.y *= val2.y;
				}
				if (extraLength > 0)
				{
					val6 = val4 - val5;
					zero = ((Vector2)(ref val6)).normalized * (float)extraLength;
					val4.x += zero.x;
					val4.y += zero.y;
					val5.x -= zero.x;
					val5.y -= zero.y;
				}
				float num8 = Vector2.Dot(p - val4, val5 - val4);
				val6 = val5 - val4;
				num6 = num8 / ((Vector2)(ref val6)).sqrMagnitude;
				if (!(num6 < 0f) && !(num6 > 1f))
				{
					val6 = p - (val4 + num6 * (val5 - val4));
					if (((Vector2)(ref val6)).sqrMagnitude <= (m_lineWidths[num2] + (float)extraDistance) * (m_lineWidths[num2] + (float)extraDistance))
					{
						index = ((m_lineType != LineType.Discrete) ? k : (k / 2));
						return true;
					}
				}
			}
			index = -1;
			return false;
		}
		Vector3 val7 = v3zero;
		for (int l = m_drawStart; l < num3; l += num7)
		{
			num2 += num;
			Vector3 val8;
			if (flag)
			{
				val8 = cam.WorldToScreenPoint(((Matrix4x4)(ref val)).MultiplyPoint3x4(m_points3[l]));
				val7 = cam.WorldToScreenPoint(((Matrix4x4)(ref val)).MultiplyPoint3x4(m_points3[l + 1]));
			}
			else
			{
				val8 = cam.WorldToScreenPoint(m_points3[l]);
				val7 = cam.WorldToScreenPoint(m_points3[l + 1]);
			}
			if (val8.z < 0f || val7.z < 0f)
			{
				continue;
			}
			val4.x = (int)val8.x;
			val5.x = (int)val7.x;
			val4.y = (int)val8.y;
			val5.y = (int)val7.y;
			if (val4.x == val5.x && val4.y == val5.y)
			{
				continue;
			}
			if (extraLength > 0)
			{
				val6 = val4 - val5;
				zero = ((Vector2)(ref val6)).normalized * (float)extraLength;
				val4.x += zero.x;
				val4.y += zero.y;
				val5.x -= zero.x;
				val5.y -= zero.y;
			}
			float num9 = Vector2.Dot(p - val4, val5 - val4);
			val6 = val5 - val4;
			num6 = num9 / ((Vector2)(ref val6)).sqrMagnitude;
			if (!(num6 < 0f) && !(num6 > 1f))
			{
				val6 = p - (val4 + num6 * (val5 - val4));
				if (((Vector2)(ref val6)).sqrMagnitude <= (m_lineWidths[num2] + (float)extraDistance) * (m_lineWidths[num2] + (float)extraDistance))
				{
					index = ((m_lineType != LineType.Discrete) ? l : (l / 2));
					return true;
				}
			}
		}
		index = -1;
		return false;
	}

	public bool Approximately(Vector2 p1, Vector2 p2)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (Approximately(p1.x, p2.x))
		{
			return Approximately(p1.y, p2.y);
		}
		return false;
	}

	public bool Approximately(Vector3 p1, Vector3 p2)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (Approximately(p1.x, p2.x) && Approximately(p1.y, p2.y))
		{
			return Approximately(p1.z, p2.z);
		}
		return false;
	}

	public bool Approximately(float a, float b)
	{
		return Mathf.Round(a * 100f) / 100f == Mathf.Round(b * 100f) / 100f;
	}

	public bool WrongArrayLength(int arrayLength, FunctionName functionName)
	{
		if (m_lineType == LineType.Continuous)
		{
			if (arrayLength != pointsCount - 1)
			{
				Debug.LogError((object)(functionNames[(int)functionName] + " list for \"" + name + "\" must be length of points array minus one for a continuous line (one entry per line segment). Expected " + (pointsCount - 1) + ", got " + arrayLength));
				return true;
			}
		}
		else if (arrayLength != pointsCount / 2)
		{
			Debug.LogError((object)(functionNames[(int)functionName] + " list in \"" + name + "\" must be exactly half the length of points array for a discrete line (one entry per line segment). Expected " + pointsCount / 2 + ", got " + arrayLength));
			return true;
		}
		return false;
	}

	public bool CheckArrayLength(FunctionName functionName, int segments, int index)
	{
		if (segments < 1)
		{
			Debug.LogError((object)("VectorLine." + functionNames[(int)functionName] + " needs at least 1 segment"));
			return false;
		}
		if (index < 0)
		{
			Debug.LogError((object)("VectorLine." + functionNames[(int)functionName] + ": The index value for \"" + name + "\" must be >= 0"));
			return false;
		}
		if (m_lineType == LineType.Points)
		{
			if (index + segments > pointsCount)
			{
				if (index == 0)
				{
					Debug.LogError((object)("VectorLine." + functionNames[(int)functionName] + ": The number of segments cannot exceed the number of points in the array for \"" + name + "\""));
					return false;
				}
				Debug.LogError((object)("VectorLine: Calling " + functionNames[(int)functionName] + " with an index of " + index + " would exceed the length of the Vector array for \"" + name + "\""));
				return false;
			}
			return true;
		}
		if (m_lineType == LineType.Continuous)
		{
			if (index + (segments + 1) > pointsCount)
			{
				if (index == 0)
				{
					Debug.LogError((object)("VectorLine." + functionNames[(int)functionName] + ": The length of the array for continuous lines needs to be at least the number of segments plus one for \"" + name + "\""));
					return false;
				}
				Debug.LogError((object)("VectorLine: Calling " + functionNames[(int)functionName] + " with an index of " + index + " would exceed the length of the Vector array (" + pointsCount + ") for \"" + name + "\""));
				return false;
			}
		}
		else if (index + segments * 2 > pointsCount)
		{
			if (index == 0)
			{
				Debug.LogError((object)("VectorLine." + functionNames[(int)functionName] + ": The length of the array for discrete lines needs to be at least twice the number of segments for \"" + name + "\""));
				return false;
			}
			Debug.LogError((object)("VectorLine: Calling " + functionNames[(int)functionName] + " with an index of " + index + " would exceed the length of the Vector array (" + pointsCount + ") for \"" + name + "\""));
			return false;
		}
		return true;
	}

	public void MakeRect(Rect rect)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		MakeRect(Vector2.op_Implicit(new Vector2(((Rect)(ref rect)).x, ((Rect)(ref rect)).y)), Vector2.op_Implicit(new Vector2(((Rect)(ref rect)).x + ((Rect)(ref rect)).width, ((Rect)(ref rect)).y + ((Rect)(ref rect)).height)), 0);
	}

	public void MakeRect(Rect rect, int index)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		MakeRect(Vector2.op_Implicit(new Vector2(((Rect)(ref rect)).x, ((Rect)(ref rect)).y)), Vector2.op_Implicit(new Vector2(((Rect)(ref rect)).x + ((Rect)(ref rect)).width, ((Rect)(ref rect)).y + ((Rect)(ref rect)).height)), index);
	}

	public void MakeRect(Vector3 bottomLeft, Vector3 topRight)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeRect(bottomLeft, topRight, 0);
	}

	public void MakeRect(Vector3 bottomLeft, Vector3 topRight, int index)
	{
		//IL_033c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0361: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0373: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_038c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Unknown result type (might be due to invalid IL or missing references)
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0401: Unknown result type (might be due to invalid IL or missing references)
		//IL_0407: Unknown result type (might be due to invalid IL or missing references)
		//IL_041a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0420: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Unknown result type (might be due to invalid IL or missing references)
		//IL_042c: Unknown result type (might be due to invalid IL or missing references)
		//IL_043f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_044b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0451: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_026a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_028f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_031e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_032a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		if (m_lineType != LineType.Discrete)
		{
			if (index + 5 > pointsCount)
			{
				if (index == 0)
				{
					Debug.LogError((object)("VectorLine.MakeRect: The length of the array for continuous lines needs to be at least 5 for \"" + name + "\""));
					return;
				}
				Debug.LogError((object)("Calling VectorLine.MakeRect with an index of " + index + " would exceed the length of the Vector2 array for \"" + name + "\""));
			}
			else if (m_is2D)
			{
				m_points2[index] = new Vector2(bottomLeft.x, bottomLeft.y);
				m_points2[index + 1] = new Vector2(topRight.x, bottomLeft.y);
				m_points2[index + 2] = new Vector2(topRight.x, topRight.y);
				m_points2[index + 3] = new Vector2(bottomLeft.x, topRight.y);
				m_points2[index + 4] = new Vector2(bottomLeft.x, bottomLeft.y);
			}
			else
			{
				m_points3[index] = new Vector3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
				m_points3[index + 1] = new Vector3(topRight.x, bottomLeft.y, bottomLeft.z);
				m_points3[index + 2] = new Vector3(topRight.x, topRight.y, topRight.z);
				m_points3[index + 3] = new Vector3(bottomLeft.x, topRight.y, topRight.z);
				m_points3[index + 4] = new Vector3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
			}
		}
		else if (index + 8 > pointsCount)
		{
			if (index == 0)
			{
				Debug.LogError((object)("VectorLine.MakeRect: The length of the array for discrete lines needs to be at least 8 for \"" + name + "\""));
				return;
			}
			Debug.LogError((object)("Calling VectorLine.MakeRect with an index of " + index + " would exceed the length of the Vector2 array for \"" + name + "\""));
		}
		else if (m_is2D)
		{
			m_points2[index] = new Vector2(bottomLeft.x, bottomLeft.y);
			m_points2[index + 1] = new Vector2(topRight.x, bottomLeft.y);
			m_points2[index + 2] = new Vector2(topRight.x, bottomLeft.y);
			m_points2[index + 3] = new Vector2(topRight.x, topRight.y);
			m_points2[index + 4] = new Vector2(topRight.x, topRight.y);
			m_points2[index + 5] = new Vector2(bottomLeft.x, topRight.y);
			m_points2[index + 6] = new Vector2(bottomLeft.x, topRight.y);
			m_points2[index + 7] = new Vector2(bottomLeft.x, bottomLeft.y);
		}
		else
		{
			m_points3[index] = new Vector3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
			m_points3[index + 1] = new Vector3(topRight.x, bottomLeft.y, bottomLeft.z);
			m_points3[index + 2] = new Vector3(topRight.x, bottomLeft.y, bottomLeft.z);
			m_points3[index + 3] = new Vector3(topRight.x, topRight.y, topRight.z);
			m_points3[index + 4] = new Vector3(topRight.x, topRight.y, topRight.z);
			m_points3[index + 5] = new Vector3(bottomLeft.x, topRight.y, topRight.z);
			m_points3[index + 6] = new Vector3(bottomLeft.x, topRight.y, topRight.z);
			m_points3[index + 7] = new Vector3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
		}
	}

	public void MakeRoundedRect(Rect rect, float cornerRadius, int cornerSegments)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		MakeRoundedRect(Vector2.op_Implicit(new Vector2(((Rect)(ref rect)).x, ((Rect)(ref rect)).y)), Vector2.op_Implicit(new Vector2(((Rect)(ref rect)).x + ((Rect)(ref rect)).width, ((Rect)(ref rect)).y + ((Rect)(ref rect)).height)), cornerRadius, cornerSegments, 0);
	}

	public void MakeRoundedRect(Rect rect, float cornerRadius, int cornerSegments, int index)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		MakeRoundedRect(Vector2.op_Implicit(new Vector2(((Rect)(ref rect)).x, ((Rect)(ref rect)).y)), Vector2.op_Implicit(new Vector2(((Rect)(ref rect)).x + ((Rect)(ref rect)).width, ((Rect)(ref rect)).y + ((Rect)(ref rect)).height)), cornerRadius, cornerSegments, index);
	}

	public void MakeRoundedRect(Vector3 bottomLeft, Vector3 topRight, float cornerRadius, int cornerSegments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeRoundedRect(bottomLeft, topRight, cornerRadius, cornerSegments, 0);
	}

	public void MakeRoundedRect(Vector3 bottomLeft, Vector3 topRight, float cornerRadius, int cornerSegments, int index)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		if (cornerSegments < 1)
		{
			Debug.LogError((object)"VectorLine.MakeRoundedRect: cornerSegments value must be >= 1");
			return;
		}
		if (index < 0)
		{
			Debug.LogError((object)"VectorLine.MakeRoundedRect: index value must be >= 0");
			return;
		}
		if (!m_is2D && bottomLeft.z != topRight.z)
		{
			Debug.LogError((object)"VectorLine.MakeRoundedRect only works on the X/Y plane");
			return;
		}
		int num = ((m_lineType != LineType.Discrete) ? (cornerSegments * 4 + 5 + index) : (cornerSegments * 8 + 8 + index));
		if (pointsCount < num)
		{
			Resize(num);
		}
		if (bottomLeft.x > topRight.x)
		{
			Exchange(ref bottomLeft, ref topRight, 0);
		}
		if (bottomLeft.y > topRight.y)
		{
			Exchange(ref bottomLeft, ref topRight, 1);
		}
		bottomLeft += new Vector3(cornerRadius, cornerRadius);
		topRight -= new Vector3(cornerRadius, cornerRadius);
		MakeCircle(bottomLeft, cornerRadius, 4 * cornerSegments, index);
		int num2 = ((m_lineType != LineType.Discrete) ? (cornerSegments + 1) : (cornerSegments * 2));
		int originalCount = ((m_lineType != LineType.Discrete) ? cornerSegments : (cornerSegments * 2));
		if (m_is2D)
		{
			CopyAndAddPoints(num2, originalCount, 3, new Vector2(0f, topRight.y - bottomLeft.y), index);
			CopyAndAddPoints(num2, originalCount, 2, Vector2.zero, index);
			CopyAndAddPoints(num2, originalCount, 1, new Vector2(topRight.x - bottomLeft.x, 0f), index);
			CopyAndAddPoints(num2, originalCount, 0, new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y), index);
			if (m_lineType != LineType.Discrete)
			{
				m_points2[num2 * 4 + index] = m_points2[index];
				return;
			}
			m_points2[num2 * 4 + 7 + index] = m_points2[index];
			m_points2[num2 * 3 + 5 + index] = m_points2[num2 * 3 + 6 + index];
			m_points2[num2 * 2 + 3 + index] = m_points2[num2 * 2 + 4 + index];
			m_points2[num2 + 1 + index] = m_points2[num2 + 2 + index];
		}
		else
		{
			CopyAndAddPoints(num2, originalCount, 3, Vector2.zero, index);
			CopyAndAddPoints(num2, originalCount, 2, new Vector2(0f, topRight.y - bottomLeft.y), index);
			CopyAndAddPoints(num2, originalCount, 1, new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y), index);
			CopyAndAddPoints(num2, originalCount, 0, new Vector2(topRight.x - bottomLeft.x, 0f), index);
			if (m_lineType != LineType.Discrete)
			{
				m_points3[num2 * 4 + index] = m_points3[index];
				return;
			}
			m_points3[num2 * 4 + 7 + index] = m_points3[index];
			m_points3[num2 * 3 + 5 + index] = m_points3[num2 * 3 + 6 + index];
			m_points3[num2 * 2 + 3 + index] = m_points3[num2 * 2 + 4 + index];
			m_points3[num2 + 1 + index] = m_points3[num2 + 2 + index];
		}
	}

	public void CopyAndAddPoints(int cornerPointCount, int originalCount, int sectionNumber, Vector2 add, int index)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Vector2.op_Implicit(add);
		for (int num = cornerPointCount - 1; num >= 0; num--)
		{
			if (m_lineType != LineType.Discrete)
			{
				if (m_is2D)
				{
					m_points2[cornerPointCount * sectionNumber + num + index] = m_points2[originalCount * sectionNumber + num + index] + add;
				}
				else
				{
					m_points3[cornerPointCount * sectionNumber + num + index] = m_points3[originalCount * sectionNumber + num + index] + val;
				}
			}
			else if (m_is2D)
			{
				m_points2[cornerPointCount * sectionNumber + sectionNumber * 2 + num + index] = m_points2[originalCount * sectionNumber + num + index] + add;
			}
			else
			{
				m_points3[cornerPointCount * sectionNumber + sectionNumber * 2 + num + index] = m_points3[originalCount * sectionNumber + num + index] + val;
			}
		}
		if (m_lineType == LineType.Discrete)
		{
			int num2 = cornerPointCount * (sectionNumber + 1) + sectionNumber * 2 + index;
			if (m_is2D)
			{
				m_points2[num2] = m_points2[num2 - 1];
			}
			else
			{
				m_points3[num2] = m_points3[num2 - 1];
			}
		}
	}

	public void Exchange(ref Vector3 v1, ref Vector3 v2, int i)
	{
		float num = ((Vector3)(ref v1))[i];
		((Vector3)(ref v1))[i] = ((Vector3)(ref v2))[i];
		((Vector3)(ref v2))[i] = num;
	}

	public void MakeCircle(Vector3 origin, float radius)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, GetSegmentNumber(), 0f, 0);
	}

	public void MakeCircle(Vector3 origin, float radius, int segments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, segments, 0f, 0);
	}

	public void MakeCircle(Vector3 origin, float radius, int segments, float pointRotation)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, segments, pointRotation, 0);
	}

	public void MakeCircle(Vector3 origin, float radius, int segments, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, segments, 0f, index);
	}

	public void MakeCircle(Vector3 origin, float radius, int segments, float pointRotation, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, segments, pointRotation, index);
	}

	public void MakeCircle(Vector3 origin, Vector3 upVector, float radius)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, radius, radius, 0f, 0f, GetSegmentNumber(), 0f, 0);
	}

	public void MakeCircle(Vector3 origin, Vector3 upVector, float radius, int segments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, radius, radius, 0f, 0f, segments, 0f, 0);
	}

	public void MakeCircle(Vector3 origin, Vector3 upVector, float radius, int segments, float pointRotation)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, radius, radius, 0f, 0f, segments, pointRotation, 0);
	}

	public void MakeCircle(Vector3 origin, Vector3 upVector, float radius, int segments, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, radius, radius, 0f, 0f, segments, 0f, index);
	}

	public void MakeCircle(Vector3 origin, Vector3 upVector, float radius, int segments, float pointRotation, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, radius, radius, 0f, 0f, segments, pointRotation, index);
	}

	public void MakeEllipse(Vector3 origin, float xRadius, float yRadius)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, xRadius, yRadius, 0f, 0f, GetSegmentNumber(), 0f, 0);
	}

	public void MakeEllipse(Vector3 origin, float xRadius, float yRadius, int segments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, xRadius, yRadius, 0f, 0f, segments, 0f, 0);
	}

	public void MakeEllipse(Vector3 origin, float xRadius, float yRadius, int segments, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, xRadius, yRadius, 0f, 0f, segments, 0f, index);
	}

	public void MakeEllipse(Vector3 origin, float xRadius, float yRadius, int segments, float pointRotation)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, xRadius, yRadius, 0f, 0f, segments, pointRotation, 0);
	}

	public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, GetSegmentNumber(), 0f, 0);
	}

	public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, int segments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, segments, 0f, 0);
	}

	public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, int segments, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, segments, 0f, index);
	}

	public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, int segments, float pointRotation)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, segments, pointRotation, 0);
	}

	public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, int segments, float pointRotation, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, segments, pointRotation, index);
	}

	public void MakeArc(Vector3 origin, float xRadius, float yRadius, float startDegrees, float endDegrees)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, xRadius, yRadius, startDegrees, endDegrees, GetSegmentNumber(), 0f, 0);
	}

	public void MakeArc(Vector3 origin, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, xRadius, yRadius, startDegrees, endDegrees, segments, 0f, 0);
	}

	public void MakeArc(Vector3 origin, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, Vector3.forward, xRadius, yRadius, startDegrees, endDegrees, segments, 0f, index);
	}

	public void MakeArc(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, float startDegrees, float endDegrees)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, xRadius, yRadius, startDegrees, endDegrees, GetSegmentNumber(), 0f, 0);
	}

	public void MakeArc(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, xRadius, yRadius, startDegrees, endDegrees, segments, 0f, 0);
	}

	public void MakeArc(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments, int index)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeEllipse(origin, upVector, xRadius, yRadius, startDegrees, endDegrees, segments, 0f, index);
	}

	public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments, float pointRotation, int index)
	{
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_0307: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		if (segments < 3)
		{
			Debug.LogError((object)"VectorLine.MakeEllipse needs at least 3 segments");
		}
		else
		{
			if (!CheckArrayLength(FunctionName.MakeEllipse, segments, index))
			{
				return;
			}
			startDegrees = Mathf.Repeat(startDegrees, 360f);
			endDegrees = Mathf.Repeat(endDegrees, 360f);
			float num;
			float num2;
			if (startDegrees == endDegrees)
			{
				num = 360f;
				num2 = (0f - pointRotation) * ((float)Math.PI / 180f);
			}
			else
			{
				num = ((endDegrees > startDegrees) ? (endDegrees - startDegrees) : (360f - startDegrees + endDegrees));
				num2 = startDegrees * ((float)Math.PI / 180f);
			}
			float num3 = num / (float)segments * ((float)Math.PI / 180f);
			if (m_lineType != LineType.Discrete)
			{
				if (startDegrees != endDegrees)
				{
					segments++;
				}
				int num4 = 0;
				if (m_is2D)
				{
					Vector2 val = Vector2.op_Implicit(origin);
					for (num4 = 0; num4 < segments; num4++)
					{
						m_points2[index + num4] = val + new Vector2(0.5f + Mathf.Sin(num2) * xRadius, 0.5f + Mathf.Cos(num2) * yRadius);
						num2 += num3;
					}
					if (m_lineType != LineType.Points && startDegrees == endDegrees)
					{
						m_points2[index + num4] = m_points2[index + (num4 - segments)];
					}
				}
				else
				{
					Matrix4x4 val2 = Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(-upVector, upVector), Vector3.one);
					for (num4 = 0; num4 < segments; num4++)
					{
						m_points3[index + num4] = origin + ((Matrix4x4)(ref val2)).MultiplyPoint3x4(new Vector3(Mathf.Sin(num2) * xRadius, Mathf.Cos(num2) * yRadius, 0f));
						num2 += num3;
					}
					if (m_lineType != LineType.Points && startDegrees == endDegrees)
					{
						m_points3[index + num4] = m_points3[index + (num4 - segments)];
					}
				}
			}
			else if (m_is2D)
			{
				Vector2 val3 = Vector2.op_Implicit(origin);
				int num5;
				for (num5 = 0; num5 < segments * 2; num5++)
				{
					m_points2[index + num5] = val3 + new Vector2(0.5f + Mathf.Sin(num2) * xRadius, 0.5f + Mathf.Cos(num2) * yRadius);
					num2 += num3;
					num5++;
					m_points2[index + num5] = val3 + new Vector2(0.5f + Mathf.Sin(num2) * xRadius, 0.5f + Mathf.Cos(num2) * yRadius);
				}
			}
			else
			{
				Matrix4x4 val4 = Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(-upVector, upVector), Vector3.one);
				int num6;
				for (num6 = 0; num6 < segments * 2; num6++)
				{
					m_points3[index + num6] = origin + ((Matrix4x4)(ref val4)).MultiplyPoint3x4(new Vector3(Mathf.Sin(num2) * xRadius, Mathf.Cos(num2) * yRadius, 0f));
					num2 += num3;
					num6++;
					m_points3[index + num6] = origin + ((Matrix4x4)(ref val4)).MultiplyPoint3x4(new Vector3(Mathf.Sin(num2) * xRadius, Mathf.Cos(num2) * yRadius, 0f));
				}
			}
		}
	}

	public void MakeCurve(Vector2[] curvePoints)
	{
		MakeCurve(curvePoints, GetSegmentNumber(), 0);
	}

	public void MakeCurve(Vector2[] curvePoints, int segments)
	{
		MakeCurve(curvePoints, segments, 0);
	}

	public void MakeCurve(Vector2[] curvePoints, int segments, int index)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (curvePoints.Length != 4)
		{
			Debug.LogError((object)"VectorLine.MakeCurve needs exactly 4 points in the curve points array");
		}
		else
		{
			MakeCurve(Vector2.op_Implicit(curvePoints[0]), Vector2.op_Implicit(curvePoints[1]), Vector2.op_Implicit(curvePoints[2]), Vector2.op_Implicit(curvePoints[3]), segments, index);
		}
	}

	public void MakeCurve(Vector3[] curvePoints)
	{
		MakeCurve(curvePoints, GetSegmentNumber(), 0);
	}

	public void MakeCurve(Vector3[] curvePoints, int segments)
	{
		MakeCurve(curvePoints, segments, 0);
	}

	public void MakeCurve(Vector3[] curvePoints, int segments, int index)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (curvePoints.Length != 4)
		{
			Debug.LogError((object)"VectorLine.MakeCurve needs exactly 4 points in the curve points array");
		}
		else
		{
			MakeCurve(curvePoints[0], curvePoints[1], curvePoints[2], curvePoints[3], segments, index);
		}
	}

	public void MakeCurve(Vector3 anchor1, Vector3 control1, Vector3 anchor2, Vector3 control2)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		MakeCurve(anchor1, control1, anchor2, control2, GetSegmentNumber(), 0);
	}

	public void MakeCurve(Vector3 anchor1, Vector3 control1, Vector3 anchor2, Vector3 control2, int segments)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		MakeCurve(anchor1, control1, anchor2, control2, segments, 0);
	}

	public void MakeCurve(Vector3 anchor1, Vector3 control1, Vector3 anchor2, Vector3 control2, int segments, int index)
	{
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		if (!CheckArrayLength(FunctionName.MakeCurve, segments, index))
		{
			return;
		}
		if (m_lineType != LineType.Discrete)
		{
			int num = ((m_lineType == LineType.Points) ? segments : (segments + 1));
			if (m_is2D)
			{
				Vector2 anchor3 = Vector2.op_Implicit(anchor1);
				Vector2 anchor4 = Vector2.op_Implicit(anchor2);
				Vector2 control3 = Vector2.op_Implicit(control1);
				Vector2 control4 = Vector2.op_Implicit(control2);
				for (int i = 0; i < num; i++)
				{
					m_points2[index + i] = GetBezierPoint(ref anchor3, ref control3, ref anchor4, ref control4, (float)i / (float)segments);
				}
			}
			else
			{
				for (int j = 0; j < num; j++)
				{
					m_points3[index + j] = GetBezierPoint3D(ref anchor1, ref control1, ref anchor2, ref control2, (float)j / (float)segments);
				}
			}
			return;
		}
		int num2 = 0;
		if (m_is2D)
		{
			Vector2 anchor5 = Vector2.op_Implicit(anchor1);
			Vector2 anchor6 = Vector2.op_Implicit(anchor2);
			Vector2 control5 = Vector2.op_Implicit(control1);
			Vector2 control6 = Vector2.op_Implicit(control2);
			for (int k = 0; k < segments; k++)
			{
				m_points2[index + num2++] = GetBezierPoint(ref anchor5, ref control5, ref anchor6, ref control6, (float)k / (float)segments);
				m_points2[index + num2++] = GetBezierPoint(ref anchor5, ref control5, ref anchor6, ref control6, (float)(k + 1) / (float)segments);
			}
		}
		else
		{
			for (int l = 0; l < segments; l++)
			{
				m_points3[index + num2++] = GetBezierPoint3D(ref anchor1, ref control1, ref anchor2, ref control2, (float)l / (float)segments);
				m_points3[index + num2++] = GetBezierPoint3D(ref anchor1, ref control1, ref anchor2, ref control2, (float)(l + 1) / (float)segments);
			}
		}
	}

	public static Vector2 GetBezierPoint(ref Vector2 anchor1, ref Vector2 control1, ref Vector2 anchor2, ref Vector2 control2, float t)
	{
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		float num = 3f * (control1.x - anchor1.x);
		float num2 = 3f * (control2.x - control1.x) - num;
		float num3 = anchor2.x - anchor1.x - num - num2;
		float num4 = 3f * (control1.y - anchor1.y);
		float num5 = 3f * (control2.y - control1.y) - num4;
		float num6 = anchor2.y - anchor1.y - num4 - num5;
		return new Vector2(num3 * (t * t * t) + num2 * (t * t) + num * t + anchor1.x, num6 * (t * t * t) + num5 * (t * t) + num4 * t + anchor1.y);
	}

	public static Vector3 GetBezierPoint3D(ref Vector3 anchor1, ref Vector3 control1, ref Vector3 anchor2, ref Vector3 control2, float t)
	{
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		float num = 3f * (control1.x - anchor1.x);
		float num2 = 3f * (control2.x - control1.x) - num;
		float num3 = anchor2.x - anchor1.x - num - num2;
		float num4 = 3f * (control1.y - anchor1.y);
		float num5 = 3f * (control2.y - control1.y) - num4;
		float num6 = anchor2.y - anchor1.y - num4 - num5;
		float num7 = 3f * (control1.z - anchor1.z);
		float num8 = 3f * (control2.z - control1.z) - num7;
		float num9 = anchor2.z - anchor1.z - num7 - num8;
		return new Vector3(num3 * (t * t * t) + num2 * (t * t) + num * t + anchor1.x, num6 * (t * t * t) + num5 * (t * t) + num4 * t + anchor1.y, num9 * (t * t * t) + num8 * (t * t) + num7 * t + anchor1.z);
	}

	public void MakeSpline(Vector2[] splinePoints)
	{
		MakeSpline(splinePoints, null, GetSegmentNumber(), 0, loop: false);
	}

	public void MakeSpline(Vector2[] splinePoints, bool loop)
	{
		MakeSpline(splinePoints, null, GetSegmentNumber(), 0, loop);
	}

	public void MakeSpline(Vector2[] splinePoints, int segments)
	{
		MakeSpline(splinePoints, null, segments, 0, loop: false);
	}

	public void MakeSpline(Vector2[] splinePoints, int segments, bool loop)
	{
		MakeSpline(splinePoints, null, segments, 0, loop);
	}

	public void MakeSpline(Vector2[] splinePoints, int segments, int index)
	{
		MakeSpline(splinePoints, null, segments, index, loop: false);
	}

	public void MakeSpline(Vector2[] splinePoints, int segments, int index, bool loop)
	{
		MakeSpline(splinePoints, null, segments, index, loop);
	}

	public void MakeSpline(Vector3[] splinePoints)
	{
		MakeSpline(null, splinePoints, GetSegmentNumber(), 0, loop: false);
	}

	public void MakeSpline(Vector3[] splinePoints, bool loop)
	{
		MakeSpline(null, splinePoints, GetSegmentNumber(), 0, loop);
	}

	public void MakeSpline(Vector3[] splinePoints, int segments)
	{
		MakeSpline(null, splinePoints, segments, 0, loop: false);
	}

	public void MakeSpline(Vector3[] splinePoints, int segments, bool loop)
	{
		MakeSpline(null, splinePoints, segments, 0, loop);
	}

	public void MakeSpline(Vector3[] splinePoints, int segments, int index)
	{
		MakeSpline(null, splinePoints, segments, index, loop: false);
	}

	public void MakeSpline(Vector3[] splinePoints, int segments, int index, bool loop)
	{
		MakeSpline(null, splinePoints, segments, index, loop);
	}

	public void MakeSpline(Vector2[] splinePoints2, Vector3[] splinePoints3, int segments, int index, bool loop)
	{
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		int num = ((splinePoints2 != null) ? splinePoints2.Length : splinePoints3.Length);
		if (num < 2)
		{
			Debug.LogError((object)"VectorLine.MakeSpline needs at least 2 spline points");
		}
		else if (splinePoints2 != null && !m_is2D)
		{
			Debug.LogError((object)"VectorLine.MakeSpline was called with a Vector2 spline points array, but the line uses Vector3 points");
		}
		else if (splinePoints3 != null && m_is2D)
		{
			Debug.LogError((object)"VectorLine.MakeSpline was called with a Vector3 spline points array, but the line uses Vector2 points");
		}
		else
		{
			if (!CheckArrayLength(FunctionName.MakeSpline, segments, index))
			{
				return;
			}
			int num2 = index;
			int num3 = (loop ? num : (num - 1));
			float num4 = 1f / (float)segments * (float)num3;
			float num5 = 0f;
			int num6 = 0;
			int num7 = 0;
			int num8 = 0;
			int i;
			for (i = 0; i < num3; i++)
			{
				num6 = i - 1;
				num7 = i + 1;
				num8 = i + 2;
				if (num6 < 0)
				{
					num6 = (loop ? (num3 - 1) : 0);
				}
				if (loop && num7 > num3 - 1)
				{
					num7 -= num3;
				}
				if (num8 > num3 - 1)
				{
					num8 = (loop ? (num8 - num3) : num3);
				}
				float num9;
				if (m_lineType != LineType.Discrete)
				{
					if (m_is2D)
					{
						for (num9 = num5; num9 <= 1f; num9 += num4)
						{
							m_points2[num2++] = GetSplinePoint(ref splinePoints2[num6], ref splinePoints2[i], ref splinePoints2[num7], ref splinePoints2[num8], num9);
						}
					}
					else
					{
						for (num9 = num5; num9 <= 1f; num9 += num4)
						{
							m_points3[num2++] = GetSplinePoint3D(ref splinePoints3[num6], ref splinePoints3[i], ref splinePoints3[num7], ref splinePoints3[num8], num9);
						}
					}
				}
				else if (m_is2D)
				{
					for (num9 = num5; num9 <= 1f; num9 += num4)
					{
						m_points2[num2++] = GetSplinePoint(ref splinePoints2[num6], ref splinePoints2[i], ref splinePoints2[num7], ref splinePoints2[num8], num9);
						if (num2 > index + 1 && num2 < index + segments * 2)
						{
							m_points2[num2++] = m_points2[num2 - 2];
						}
					}
				}
				else
				{
					for (num9 = num5; num9 <= 1f; num9 += num4)
					{
						m_points3[num2++] = GetSplinePoint3D(ref splinePoints3[num6], ref splinePoints3[i], ref splinePoints3[num7], ref splinePoints3[num8], num9);
						if (num2 > index + 1 && num2 < index + segments * 2)
						{
							m_points3[num2++] = m_points3[num2 - 2];
						}
					}
				}
				num5 = num9 - 1f;
			}
			if ((m_lineType != LineType.Discrete && num2 < index + (segments + 1)) || (m_lineType == LineType.Discrete && num2 < index + segments * 2))
			{
				if (m_is2D)
				{
					m_points2[num2] = GetSplinePoint(ref splinePoints2[num6], ref splinePoints2[i - 1], ref splinePoints2[num7], ref splinePoints2[num8], 1f);
				}
				else
				{
					m_points3[num2] = GetSplinePoint3D(ref splinePoints3[num6], ref splinePoints3[i - 1], ref splinePoints3[num7], ref splinePoints3[num8], 1f);
				}
			}
		}
	}

	public static Vector2 GetSplinePoint(ref Vector2 p0, ref Vector2 p1, ref Vector2 p2, ref Vector2 p3, float t)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		Vector4 p4 = Vector4.zero;
		Vector4 p5 = Vector4.zero;
		float num = Mathf.Pow(VectorDistanceSquared(ref p0, ref p1), 0.25f);
		float num2 = Mathf.Pow(VectorDistanceSquared(ref p1, ref p2), 0.25f);
		float num3 = Mathf.Pow(VectorDistanceSquared(ref p2, ref p3), 0.25f);
		if (num2 < 0.0001f)
		{
			num2 = 1f;
		}
		if (num < 0.0001f)
		{
			num = num2;
		}
		if (num3 < 0.0001f)
		{
			num3 = num2;
		}
		InitNonuniformCatmullRom(p0.x, p1.x, p2.x, p3.x, num, num2, num3, ref p4);
		InitNonuniformCatmullRom(p0.y, p1.y, p2.y, p3.y, num, num2, num3, ref p5);
		return new Vector2(EvalCubicPoly(ref p4, t), EvalCubicPoly(ref p5, t));
	}

	public static Vector3 GetSplinePoint3D(ref Vector3 p0, ref Vector3 p1, ref Vector3 p2, ref Vector3 p3, float t)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		Vector4 p4 = Vector4.zero;
		Vector4 p5 = Vector4.zero;
		Vector4 p6 = Vector4.zero;
		float num = Mathf.Pow(VectorDistanceSquared(ref p0, ref p1), 0.25f);
		float num2 = Mathf.Pow(VectorDistanceSquared(ref p1, ref p2), 0.25f);
		float num3 = Mathf.Pow(VectorDistanceSquared(ref p2, ref p3), 0.25f);
		if (num2 < 0.0001f)
		{
			num2 = 1f;
		}
		if (num < 0.0001f)
		{
			num = num2;
		}
		if (num3 < 0.0001f)
		{
			num3 = num2;
		}
		InitNonuniformCatmullRom(p0.x, p1.x, p2.x, p3.x, num, num2, num3, ref p4);
		InitNonuniformCatmullRom(p0.y, p1.y, p2.y, p3.y, num, num2, num3, ref p5);
		InitNonuniformCatmullRom(p0.z, p1.z, p2.z, p3.z, num, num2, num3, ref p6);
		return new Vector3(EvalCubicPoly(ref p4, t), EvalCubicPoly(ref p5, t), EvalCubicPoly(ref p6, t));
	}

	public static float VectorDistanceSquared(ref Vector2 p, ref Vector2 q)
	{
		float num = q.x - p.x;
		float num2 = q.y - p.y;
		return num * num + num2 * num2;
	}

	public static float VectorDistanceSquared(ref Vector3 p, ref Vector3 q)
	{
		float num = q.x - p.x;
		float num2 = q.y - p.y;
		float num3 = q.z - p.z;
		return num * num + num2 * num2 + num3 * num3;
	}

	public static void InitNonuniformCatmullRom(float x0, float x1, float x2, float x3, float dt0, float dt1, float dt2, ref Vector4 p)
	{
		float num = ((x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1) * dt1;
		float num2 = ((x2 - x1) / dt1 - (x3 - x1) / (dt1 + dt2) + (x3 - x2) / dt2) * dt1;
		p.x = x1;
		p.y = num;
		p.z = -3f * x1 + 3f * x2 - 2f * num - num2;
		p.w = 2f * x1 - 2f * x2 + num + num2;
	}

	public static float EvalCubicPoly(ref Vector4 p, float t)
	{
		return p.x + p.y * t + p.z * (t * t) + p.w * (t * t * t);
	}

	public void MakeText(string text, Vector3 startPos, float size)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeText(text, startPos, size, 1f, 1.5f, uppercaseOnly: true);
	}

	public void MakeText(string text, Vector3 startPos, float size, bool uppercaseOnly)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeText(text, startPos, size, 1f, 1.5f, uppercaseOnly);
	}

	public void MakeText(string text, Vector3 startPos, float size, float charSpacing, float lineSpacing)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		MakeText(text, startPos, size, charSpacing, lineSpacing, uppercaseOnly: true);
	}

	public void MakeText(string text, Vector3 startPos, float size, float charSpacing, float lineSpacing, bool uppercaseOnly)
	{
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		if (m_lineType != LineType.Discrete)
		{
			Debug.LogError((object)"VectorLine.MakeText only works with a discrete line");
			return;
		}
		int num = 0;
		for (int i = 0; i < text.Length; i++)
		{
			int num2 = Convert.ToInt32(text[i]);
			if (num2 < 0 || num2 > 256)
			{
				Debug.LogError((object)("VectorLine.MakeText: Character '" + text[i] + "' is not valid"));
				return;
			}
			if (uppercaseOnly && num2 >= 97 && num2 <= 122)
			{
				num2 -= 32;
			}
			if (VectorChar.data[num2] != null)
			{
				num += VectorChar.data[num2].Length;
			}
		}
		if (num != pointsCount)
		{
			Resize(num);
		}
		float num3 = 0f;
		float num4 = 0f;
		int num5 = 0;
		Vector2 val = default(Vector2);
		((Vector2)(ref val))._002Ector(size, size);
		for (int j = 0; j < text.Length; j++)
		{
			int num6 = Convert.ToInt32(text[j]);
			switch (num6)
			{
			case 10:
				num4 -= lineSpacing;
				num3 = 0f;
				continue;
			case 32:
				num3 += charSpacing;
				continue;
			}
			if (uppercaseOnly && num6 >= 97 && num6 <= 122)
			{
				num6 -= 32;
			}
			int num7 = 0;
			if (VectorChar.data[num6] != null)
			{
				num7 = VectorChar.data[num6].Length;
				if (m_is2D)
				{
					for (int k = 0; k < num7; k++)
					{
						m_points2[num5++] = Vector2.Scale(VectorChar.data[num6][k] + new Vector2(num3, num4), val) + Vector2.op_Implicit(startPos);
					}
				}
				else
				{
					for (int l = 0; l < num7; l++)
					{
						m_points3[num5++] = Vector3.Scale(Vector2.op_Implicit(VectorChar.data[num6][l]) + new Vector3(num3, num4, 0f), Vector2.op_Implicit(val)) + startPos;
					}
				}
				num3 += charSpacing;
			}
			else
			{
				num3 += charSpacing;
			}
		}
	}

	public void MakeWireframe(Mesh mesh)
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		if (m_lineType != LineType.Discrete)
		{
			Debug.LogError((object)"VectorLine.MakeWireframe only works with a discrete line");
			return;
		}
		if (m_is2D)
		{
			Debug.LogError((object)("VectorLine.MakeWireframe can only be used with Vector3 points, which \"" + name + "\" doesn't have"));
			return;
		}
		if ((Object)(object)mesh == (Object)null)
		{
			Debug.LogError((object)"VectorLine.MakeWireframe can't use a null mesh");
			return;
		}
		Vector3[] vertices = mesh.vertices;
		Dictionary<Vector3Pair, bool> pairs = new Dictionary<Vector3Pair, bool>();
		List<Vector3> list = new List<Vector3>();
		for (int i = 0; i < mesh.subMeshCount; i++)
		{
			int[] indices = mesh.GetIndices(i);
			int num = (((int)mesh.GetTopology(i) == 0) ? 3 : 4);
			for (int j = 0; j < indices.Length; j += num)
			{
				for (int k = 0; k < num; k++)
				{
					CheckPairPoints(pairs, vertices[indices[j + k]], vertices[indices[j + (k + 1) % num]], list);
				}
			}
		}
		if (list.Count != m_pointsCount)
		{
			Resize(list.Count);
		}
		for (int l = 0; l < m_pointsCount; l++)
		{
			m_points3[l] = list[l];
		}
	}

	public static void CheckPairPoints(Dictionary<Vector3Pair, bool> pairs, Vector3 p1, Vector3 p2, List<Vector3> linePoints)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		Vector3Pair key = new Vector3Pair(p1, p2);
		Vector3Pair key2 = new Vector3Pair(p2, p1);
		if (!pairs.ContainsKey(key) && !pairs.ContainsKey(key2))
		{
			pairs[key] = true;
			pairs[key2] = true;
			linePoints.Add(p1);
			linePoints.Add(p2);
		}
	}

	public void MakeCube(Vector3 position, float xSize, float ySize, float zSize)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		MakeCube(position, xSize, ySize, zSize, 0);
	}

	public void MakeCube(Vector3 position, float xSize, float ySize, float zSize, int index)
	{
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0323: Unknown result type (might be due to invalid IL or missing references)
		//IL_0329: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0390: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b2: Unknown result type (might be due to invalid IL or missing references)
		if (m_lineType != LineType.Discrete)
		{
			Debug.LogError((object)"VectorLine.MakeCube only works with a discrete line");
			return;
		}
		if (m_is2D)
		{
			Debug.LogError((object)("VectorLine.MakeCube can only be used with Vector3 points, which \"" + name + "\" doesn't have"));
			return;
		}
		if (index + 24 > pointsCount)
		{
			if (index == 0)
			{
				Debug.LogError((object)("VectorLine.MakeCube: The number of Vector3 points needs to be at least 24 for \"" + name + "\""));
				return;
			}
			Debug.LogError((object)("Calling VectorLine.MakeCube with an index of " + index + " would exceed the length of the Vector3 points for \"" + name + "\""));
			return;
		}
		xSize /= 2f;
		ySize /= 2f;
		zSize /= 2f;
		m_points3[index] = position + new Vector3(0f - xSize, ySize, 0f - zSize);
		m_points3[index + 1] = position + new Vector3(xSize, ySize, 0f - zSize);
		m_points3[index + 2] = position + new Vector3(xSize, ySize, 0f - zSize);
		m_points3[index + 3] = position + new Vector3(xSize, ySize, zSize);
		m_points3[index + 4] = position + new Vector3(xSize, ySize, zSize);
		m_points3[index + 5] = position + new Vector3(0f - xSize, ySize, zSize);
		m_points3[index + 6] = position + new Vector3(0f - xSize, ySize, zSize);
		m_points3[index + 7] = position + new Vector3(0f - xSize, ySize, 0f - zSize);
		m_points3[index + 8] = position + new Vector3(0f - xSize, 0f - ySize, 0f - zSize);
		m_points3[index + 9] = position + new Vector3(0f - xSize, ySize, 0f - zSize);
		m_points3[index + 10] = position + new Vector3(xSize, 0f - ySize, 0f - zSize);
		m_points3[index + 11] = position + new Vector3(xSize, ySize, 0f - zSize);
		m_points3[index + 12] = position + new Vector3(0f - xSize, 0f - ySize, zSize);
		m_points3[index + 13] = position + new Vector3(0f - xSize, ySize, zSize);
		m_points3[index + 14] = position + new Vector3(xSize, 0f - ySize, zSize);
		m_points3[index + 15] = position + new Vector3(xSize, ySize, zSize);
		m_points3[index + 16] = position + new Vector3(0f - xSize, 0f - ySize, 0f - zSize);
		m_points3[index + 17] = position + new Vector3(xSize, 0f - ySize, 0f - zSize);
		m_points3[index + 18] = position + new Vector3(xSize, 0f - ySize, 0f - zSize);
		m_points3[index + 19] = position + new Vector3(xSize, 0f - ySize, zSize);
		m_points3[index + 20] = position + new Vector3(xSize, 0f - ySize, zSize);
		m_points3[index + 21] = position + new Vector3(0f - xSize, 0f - ySize, zSize);
		m_points3[index + 22] = position + new Vector3(0f - xSize, 0f - ySize, zSize);
		m_points3[index + 23] = position + new Vector3(0f - xSize, 0f - ySize, 0f - zSize);
	}
}
