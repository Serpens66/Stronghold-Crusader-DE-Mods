using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
	[Serializable]
	public class ObjectPoolEntry
	{
		[SerializeField]
		public GameObject Prefab;

		[SerializeField]
		public int Count;

		[HideInInspector]
		public GameObject[] pool;

		[HideInInspector]
		public int objectsInPool;
	}

	public class PoolRow
	{
		public int rowID = -1;

		public int columnStartID = -1;

		public int columnEndID = -1;

		public Dictionary<int, GameObject> rowDict = new Dictionary<int, GameObject>();

		public GameObject parentObj;

		public bool isActive = true;

		public void SetActive(bool state)
		{
			isActive = state;
			parentObj.SetActive(state);
		}

		public void AddObj(int id, GameObject obj)
		{
			try
			{
				rowDict[id] = obj;
				obj.transform.parent = parentObj.transform;
			}
			catch (Exception)
			{
			}
		}

		public void removeObj(int id, GameObject obj)
		{
			rowDict.Remove(id);
		}
	}

	public static ObjectPool instance;

	public const int CHUNKS_IN_ROW = 1;

	public const int CHUNK_ROW_SIZE = 10000;

	public int loadedprefabs;

	public ObjPoolEntry[] Entries = new ObjPoolEntry[2];

	public GameObject ContainerAllFreeObjs;

	public GameObject ContainerAllUsedObjs;

	public GameObject[] ContainerFreeObjs = (GameObject[])(object)new GameObject[2];

	public GameObject[] ContainerUsedObjs = (GameObject[])(object)new GameObject[2];

	public PoolRow[] ContainerRows = new PoolRow[205];

	public prefabEntry[] prefabList = new prefabEntry[2];

	public static bool poolsCreated;

	public static int getContainerID(int row, int column)
	{
		return row / 4;
	}

	public bool IsContainerActive(int row, int column)
	{
		int containerID = getContainerID(row, column);
		return ContainerRows[containerID].isActive;
	}

	public void Awake()
	{
		instance = this;
		if ((Object)(object)instance == (Object)null)
		{
			instance = this;
		}
		else if ((Object)(object)instance != (Object)(object)this)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
	}

	public void setupGeneralPrefabList()
	{
		prefabList[0].name = "Prefabs/simpleSprite";
		prefabList[0].max = 30000;
	}

	public void setupOrgPrefabList()
	{
		prefabList[1].name = "Prefabs/simpleSprite";
		prefabList[1].max = 4000;
	}

	public void setupPoolRows()
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		int num = ContainerRows.Length / 1;
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < 1; j++)
			{
				ContainerRows[i + j] = new PoolRow();
				ContainerRows[i + j].parentObj = new GameObject();
				((Object)ContainerRows[i + j].parentObj).name = "Pool Row " + i + " Chunk " + j;
				ContainerRows[i + j].parentObj.transform.parent = ContainerAllUsedObjs.transform;
				ContainerRows[i + j].rowID = i;
				ContainerRows[i + j].columnStartID = j * 10000;
				ContainerRows[i + j].columnEndID = (j + 1) * 10000 - 1;
			}
		}
	}

	public void Start()
	{
		setupGeneralPrefabList();
		setupOrgPrefabList();
		loadPrefabListIntoObjectPoolStructures();
		createObjectPools();
		((MonoBehaviour)this).StartCoroutine(fillObjectPools());
		groupContainersUnderOneMaster();
		setupPoolRows();
		poolsCreated = true;
	}

	public void groupContainersUnderOneMaster()
	{
		for (int i = 0; i < 2; i++)
		{
			ContainerUsedObjs[i].transform.parent = ContainerAllUsedObjs.transform;
			ContainerFreeObjs[i].transform.parent = ContainerAllFreeObjs.transform;
		}
	}

	public void loadPrefabListIntoObjectPoolStructures()
	{
		loadedprefabs = 0;
		for (int i = 0; i < 2; i++)
		{
			Entries[i].objectsInPool = 0;
			Entries[i].Count = prefabList[i].max;
			if (prefabList[i].max != 0)
			{
				Object val = Resources.Load(prefabList[i].name);
				if (!(val == (Object)null))
				{
					Entries[i].Prefab = val;
					loadedprefabs++;
				}
			}
		}
	}

	public void createObjectPools()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected O, but got Unknown
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		ContainerAllFreeObjs = new GameObject("ObjectPool");
		ContainerAllFreeObjs.SetActive(false);
		ContainerAllUsedObjs = new GameObject("ObjectPoolActives");
		ContainerFreeObjs[0] = new GameObject("GeneralPool");
		ContainerUsedObjs[0] = new GameObject("GeneralPool");
		ContainerFreeObjs[1] = new GameObject("OrgPool");
		ContainerUsedObjs[1] = new GameObject("OrgPool");
	}

	public IEnumerator fillObjectPools()
	{
		DateTime limit = DateTime.UtcNow.AddMilliseconds(160.0);
		for (int a = 0; a < 2; a++)
		{
			ObjPoolEntry objectPrefab = Entries[a];
			if (objectPrefab.Count == 0)
			{
				break;
			}
			Entries[a].pool = (GameObject[])(object)new GameObject[objectPrefab.Count];
			for (int n = 0; n < objectPrefab.Count; n++)
			{
				if (!(objectPrefab.Prefab == (Object)null))
				{
					GameObject val = (GameObject)Object.Instantiate(objectPrefab.Prefab);
					((Object)val).name = objectPrefab.Prefab.name;
					PoolObject(a, val);
					spriteLoader.instance.setDefaultMaterial(val);
					if (n % 10 == 0 && DateTime.UtcNow > limit)
					{
						limit = DateTime.UtcNow.AddMilliseconds(160.0);
						yield return null;
					}
				}
			}
		}
		yield return null;
	}

	public GameObject GetObjectForType(int poolNo, string objectType, int containerID = -1, int objID = -1)
	{
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		if (Entries[poolNo].objectsInPool > 0)
		{
			GameObject val = Entries[poolNo].pool[--Entries[poolNo].objectsInPool];
			if (containerID < 0 || objID == -1)
			{
				val.transform.parent = ContainerUsedObjs[poolNo].transform;
			}
			else if (containerID < ContainerRows.Length)
			{
				ContainerRows[containerID].AddObj(objID, val);
			}
			else
			{
				Debug.Log((object)("Illegal container " + containerID));
			}
			if (!val.activeSelf)
			{
				val.SetActive(true);
			}
			val.transform.localPosition = new Vector3(0f, 0f, 0f);
			((Renderer)val.GetComponent<SpriteRenderer>()).material = spriteLoader.instance.defaultMaterial;
			return val;
		}
		Debug.Log((object)"Pool Empty");
		return null;
	}

	public int getPoolCount(int poolNo, bool visibleOnly)
	{
		int num = 0;
		if (ContainerUsedObjs != null && ContainerUsedObjs.Length > poolNo && (Object)(object)ContainerUsedObjs[poolNo] != (Object)null)
		{
			num += ContainerUsedObjs[poolNo].transform.childCount;
		}
		if (ContainerRows != null)
		{
			PoolRow[] containerRows = ContainerRows;
			foreach (PoolRow poolRow in containerRows)
			{
				if (poolRow != null && (!visibleOnly || poolRow.parentObj.activeSelf))
				{
					num += poolRow.rowDict.Count;
				}
			}
		}
		return num;
	}

	public void moveRow(int poolNo, GameObject obj, int oldContainerID, int newContainerID, int objID)
	{
		if (oldContainerID != newContainerID && oldContainerID >= 0)
		{
			ContainerRows[oldContainerID].removeObj(objID, obj);
			ContainerRows[newContainerID].AddObj(objID, obj);
		}
	}

	public void filterRows(int topRow, int bottowRow, int leftColumn, int rightColumn)
	{
		topRow /= 4;
		bottowRow = (bottowRow + 3) / 4;
		for (int i = 1; i < GameMap.tilemapSize / 4 + 1; i++)
		{
			if (i < topRow || i > bottowRow)
			{
				if (ContainerRows[i].isActive)
				{
					ContainerRows[i].SetActive(state: false);
				}
				continue;
			}
			int num = i;
			if (!ContainerRows[num].isActive)
			{
				ContainerRows[num].SetActive(state: true);
			}
		}
	}

	public void PoolObject(int poolNo, GameObject obj, int row = -1, int objID = -1)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		if (row >= 0)
		{
			ContainerRows[row].removeObj(objID, obj);
		}
		obj.transform.localScale = Vector3.one;
		obj.transform.parent = ContainerFreeObjs[poolNo].transform;
		Entries[poolNo].pool[Entries[poolNo].objectsInPool++] = obj;
	}
}
