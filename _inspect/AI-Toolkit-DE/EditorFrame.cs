using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrusaderCastleEditorUnity;

[Serializable]
public class Frame
{
	public LowerLevelItems itemType;

	public List<int> tilePositionOfsets;

	public bool shouldPause;

	public Frame GetFrameForExport()
	{
		if (Definitions.IsMapperPitchWallMoatCrenel(itemType))
		{
			return this;
		}
		return new Frame(itemType, tilePositionOfsets[0], shouldPause);
	}

	public Frame(LowerLevelItems _item, List<int> _tilePosOfsets, bool _shouldPause = false)
	{
		itemType = _item;
		tilePositionOfsets = _tilePosOfsets;
		shouldPause = _shouldPause;
	}

	public Frame(LowerLevelItems _item, int _tilePosOfset, bool _shouldPause = false)
	{
		itemType = _item;
		tilePositionOfsets = new List<int> { _tilePosOfset };
		shouldPause = _shouldPause;
	}

	public void TryRemovePosition(int positionOfset)
	{
		if (tilePositionOfsets != null && tilePositionOfsets.Count > 0 && ContainsPositionOfset(positionOfset))
		{
			tilePositionOfsets.Remove(positionOfset);
		}
		TryToRemoveFrameFromList();
	}

	public bool ContainsPositionOfset(int positionOfset)
	{
		return tilePositionOfsets.Contains(positionOfset);
	}

	public int GetIndexOfPositionOfset(int positionOfset)
	{
		int num = 0;
		foreach (int tilePositionOfset in tilePositionOfsets)
		{
			if (tilePositionOfset == positionOfset)
			{
				return num;
			}
			num++;
		}
		return -1;
	}

	public void TryToRemoveFrameFromList()
	{
		if (tilePositionOfsets != null && tilePositionOfsets.Count == 0)
		{
			FrameManager.Instance.DeleteFrame(this);
		}
	}

	public Vector3Int GetFirstPosAsVec3Int()
	{
		if (tilePositionOfsets != null && tilePositionOfsets.Count > 0)
		{
			return Helpers.OfsetToVec3(tilePositionOfsets[0]);
		}
		return Vector3Int.zero;
	}

	public int GetFirstPosOfset()
	{
		if (tilePositionOfsets != null && tilePositionOfsets.Count > 0)
		{
			return tilePositionOfsets[0];
		}
		return 0;
	}

	public List<Vector3Int> GetTilePosListAsVec3Int()
	{
		List<Vector3Int> list = new List<Vector3Int>();
		foreach (int tilePositionOfset in tilePositionOfsets)
		{
			list.Add(Helpers.OfsetToVec3(tilePositionOfset));
		}
		return list;
	}
}
