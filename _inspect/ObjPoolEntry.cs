using UnityEngine;

public struct ObjPoolEntry
{
	public Object Prefab;

	public int Count;

	public GameObject[] pool;

	public int objectsInPool;
}
