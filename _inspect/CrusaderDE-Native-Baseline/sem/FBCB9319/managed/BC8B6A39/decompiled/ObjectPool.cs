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

	private class PoolRow
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

	private const int CHUNKS_IN_ROW = 1;

	private const int CHUNK_ROW_SIZE = 10000;

	public int loadedprefabs;

	private ObjPoolEntry[] Entries = new ObjPoolEntry[2];

	protected GameObject ContainerAllFreeObjs;

	protected GameObject ContainerAllUsedObjs;

	protected GameObject[] ContainerFreeObjs = new GameObject[2];

	protected GameObject[] ContainerUsedObjs = new GameObject[2];

	private PoolRow[] ContainerRows = new PoolRow[205];

	private prefabEntry[] prefabList = new prefabEntry[2];

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

	private void Awake()
	{
		instance = this;
		if (instance == null)
		{
			instance = this;
		}
		else if (instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void setupGeneralPrefabList()
	{
		prefabList[0].name = "Prefabs/simpleSprite";
		prefabList[0].max = 30000;
	}

	private void setupOrgPrefabList()
	{
		prefabList[1].name = "Prefabs/simpleSprite";
		prefabList[1].max = 4000;
	}

	private void setupPoolRows()
	{
		int num = ContainerRows.Length / 1;
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < 1; j++)
			{
				ContainerRows[i + j] = new PoolRow();
				ContainerRows[i + j].parentObj = new GameObject();
				ContainerRows[i + j].parentObj.name = "Pool Row " + i + " Chunk " + j;
				ContainerRows[i + j].parentObj.transform.parent = ContainerAllUsedObjs.transform;
				ContainerRows[i + j].rowID = i;
				ContainerRows[i + j].columnStartID = j * 10000;
				ContainerRows[i + j].columnEndID = (j + 1) * 10000 - 1;
			}
		}
	}

	private void Start()
	{
		setupGeneralPrefabList();
		setupOrgPrefabList();
		loadPrefabListIntoObjectPoolStructures();
		createObjectPools();
		StartCoroutine(fillObjectPools());
		groupContainersUnderOneMaster();
		setupPoolRows();
		poolsCreated = true;
	}

	private void groupContainersUnderOneMaster()
	{
		for (int i = 0; i < 2; i++)
		{
			ContainerUsedObjs[i].transform.parent = ContainerAllUsedObjs.transform;
			ContainerFreeObjs[i].transform.parent = ContainerAllFreeObjs.transform;
		}
	}

	private void loadPrefabListIntoObjectPoolStructures()
	{
		loadedprefabs = 0;
		for (int i = 0; i < 2; i++)
		{
			Entries[i].objectsInPool = 0;
			Entries[i].Count = prefabList[i].max;
			if (prefabList[i].max != 0)
			{
				UnityEngine.Object obj = Resources.Load(prefabList[i].name);
				if (!(obj == null))
				{
					Entries[i].Prefab = obj;
					loadedprefabs++;
				}
			}
		}
	}

	private void createObjectPools()
	{
		ContainerAllFreeObjs = new GameObject("ObjectPool");
		ContainerAllFreeObjs.SetActive(value: false);
		ContainerAllUsedObjs = new GameObject("ObjectPoolActives");
		ContainerFreeObjs[0] = new GameObject("GeneralPool");
		ContainerUsedObjs[0] = new GameObject("GeneralPool");
		ContainerFreeObjs[1] = new GameObject("OrgPool");
		ContainerUsedObjs[1] = new GameObject("OrgPool");
	}

	private IEnumerator fillObjectPools()
	{
		DateTime limit = DateTime.UtcNow.AddMilliseconds(160.0);
		for (int a = 0; a < 2; a++)
		{
			ObjPoolEntry objectPrefab = Entries[a];
			if (objectPrefab.Count == 0)
			{
				break;
			}
			Entries[a].pool = new GameObject[objectPrefab.Count];
			for (int n = 0; n < objectPrefab.Count; n++)
			{
				if (!(objectPrefab.Prefab == null))
				{
					GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(objectPrefab.Prefab);
					gameObject.name = objectPrefab.Prefab.name;
					PoolObject(a, gameObject);
					spriteLoader.instance.setDefaultMaterial(gameObject);
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
		if (Entries[poolNo].objectsInPool > 0)
		{
			GameObject gameObject = Entries[poolNo].pool[--Entries[poolNo].objectsInPool];
			if (containerID < 0 || objID == -1)
			{
				gameObject.transform.parent = ContainerUsedObjs[poolNo].transform;
			}
			else if (containerID < ContainerRows.Length)
			{
				ContainerRows[containerID].AddObj(objID, gameObject);
			}
			else
			{
				Debug.Log("Illegal container " + containerID);
			}
			if (!gameObject.activeSelf)
			{
				gameObject.SetActive(value: true);
			}
			gameObject.transform.localPosition = new Vector3(0f, 0f, 0f);
			gameObject.GetComponent<SpriteRenderer>().material = spriteLoader.instance.defaultMaterial;
			return gameObject;
		}
		Debug.Log("Pool Empty");
		return null;
	}

	public int getPoolCount(int poolNo, bool visibleOnly)
	{
		int num = 0;
		if (ContainerUsedObjs != null && ContainerUsedObjs.Length > poolNo && ContainerUsedObjs[poolNo] != null)
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
		if (row >= 0)
		{
			ContainerRows[row].removeObj(objID, obj);
		}
		obj.transform.localScale = Vector3.one;
		obj.transform.parent = ContainerFreeObjs[poolNo].transform;
		Entries[poolNo].pool[Entries[poolNo].objectsInPool++] = obj;
	}
}
