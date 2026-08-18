using UnityEngine;

namespace Vectrosity;

public struct Vector3Pair(Vector3 point1, Vector3 point2)
{
	public Vector3 p1 = point1;

	public Vector3 p2 = point2;
}
