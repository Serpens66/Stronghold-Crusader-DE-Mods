using System;
using System.Collections.Generic;
using System.Linq;

namespace CrusaderCastleEditorUnity;

internal class FrameManager
{
	private const int pauseDelayMax = 250;

	private const int pauseDelayMin = 10;

	public Action uiUpdateAction;

	public Action tilemapUpdateAction;

	private int curFrameIndex = -1;

	public int pauseDelayAmount = 100;

	public List<Frame> allFrames = new List<Frame>();

	private static FrameManager instance;

	public int CurFrameIndex
	{
		get
		{
			return curFrameIndex;
		}
		set
		{
			curFrameIndex = value;
			if (curFrameIndex < 0)
			{
				curFrameIndex = 0;
			}
			if (curFrameIndex > allFrames.Count - 1)
			{
				curFrameIndex = allFrames.Count - 1;
			}
			Update();
		}
	}

	public static FrameManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = new FrameManager();
			}
			return instance;
		}
		set
		{
			instance = value;
		}
	}

	public void AddPauseToCurrentFrame()
	{
		allFrames[CurFrameIndex].shouldPause = true;
	}

	public void DeletePauseFromCurrentFrame()
	{
		allFrames[CurFrameIndex].shouldPause = false;
	}

	private void RefreshCurFrameIndex()
	{
		CurFrameIndex = curFrameIndex;
	}

	public List<Frame> GetAllFramesForExport()
	{
		List<Frame> list = new List<Frame>();
		foreach (Frame allFrame in allFrames)
		{
			list.Add(allFrame.GetFrameForExport());
		}
		return list;
	}

	public int GetPauseFrameOfCurrentFrame()
	{
		int num = 1;
		for (int i = 0; i < CurFrameIndex; i++)
		{
			if (allFrames[i].shouldPause)
			{
				num++;
			}
		}
		return num;
	}

	internal void AddPauseToFrame(int pFrame)
	{
		if (pFrame < allFrames.Count)
		{
			allFrames[pFrame].shouldPause = true;
		}
	}

	public void AddFrame(LowerLevelItems item = LowerLevelItems.none, List<int> tilePositionOfsets = null, bool shouldPause = false)
	{
		if (tilePositionOfsets == null || tilePositionOfsets.Count == 0)
		{
			return;
		}
		List<int> list = new List<int>();
		foreach (int tilePositionOfset in tilePositionOfsets)
		{
			list.Add(tilePositionOfset);
		}
		if (item == LowerLevelItems.WallStairs)
		{
			List<Frame> list2 = new List<Frame>();
			for (int i = 0; i < list.Count; i++)
			{
				list2.Add(new Frame((LowerLevelItems)(181 + (4 - i % 5)), list[i], shouldPause));
			}
			allFrames.InsertRange(curFrameIndex + 1, list2);
			CurFrameIndex += list2.Count;
		}
		else
		{
			allFrames.Insert(curFrameIndex + 1, new Frame(item, list, shouldPause));
			CurFrameIndex++;
		}
		Update();
	}

	private void Update()
	{
		uiUpdateAction?.Invoke();
		tilemapUpdateAction?.Invoke();
	}

	public void ClearAllFrames()
	{
		allFrames.Clear();
		Update();
		TimelineDisplayViewModel.instance.DisplayZero();
		CurFrameIndex = -1;
	}

	public void DisplayZerothFrameDueToNoKeep()
	{
		curFrameIndex = -1;
		TimelineDisplayViewModel.instance.DisplayZero();
		tilemapUpdateAction?.Invoke();
	}

	public void TryToDeleteTile(int tilePositionOfset)
	{
		foreach (Frame item in allFrames.ToList())
		{
			item.TryRemovePosition(tilePositionOfset);
		}
	}

	public void TryToDeleteBuilding(int tilePositionOfset)
	{
		foreach (Frame item in allFrames.ToList())
		{
			if (item.ContainsPositionOfset(tilePositionOfset))
			{
				DeleteFrame(item);
				break;
			}
		}
	}

	public void DeleteFrame(Frame frameToDelete)
	{
		if (frameToDelete != null)
		{
			allFrames.Remove(frameToDelete);
		}
		RefreshCurFrameIndex();
	}

	public void NextFrame()
	{
		if (CurFrameIndex < allFrames.Count - 1)
		{
			CurFrameIndex++;
		}
	}

	public void PreviousFrame()
	{
		if (CurFrameIndex > 0)
		{
			CurFrameIndex--;
		}
	}

	public void FirstFrame()
	{
		CurFrameIndex = -1;
	}

	public void LastFrame()
	{
		CurFrameIndex = allFrames.Count - 1;
	}

	public void SetFrame(int index)
	{
		CurFrameIndex = index;
	}

	public void IncreasePauseDelay()
	{
		if (pauseDelayAmount < 250)
		{
			pauseDelayAmount += 10;
		}
	}

	public void DecreasePauseDelay()
	{
		if (pauseDelayAmount > 10)
		{
			pauseDelayAmount -= 10;
		}
	}

	public List<double> GetPauseFrames()
	{
		List<double> list = new List<double>();
		for (int i = 0; i < allFrames.Count; i++)
		{
			if (allFrames[i].shouldPause)
			{
				list.Add(i + 1);
			}
		}
		return list;
	}

	public Frame GetFrame(int frameIndex)
	{
		if (frameIndex > -1 && frameIndex < allFrames.Count)
		{
			return allFrames[frameIndex];
		}
		return null;
	}

	public int GetNumFrames()
	{
		if (allFrames == null)
		{
			return 0;
		}
		return allFrames.Count;
	}

	public bool DoesCurrentFrameHavePause()
	{
		if (allFrames != null && curFrameIndex >= 0 && CurFrameIndex < allFrames.Count)
		{
			return allFrames[CurFrameIndex].shouldPause;
		}
		return false;
	}

	public void UpdateFrameInformation(int currentFrame, int totalFrames)
	{
		FrameInformation arg = new FrameInformation
		{
			CurrentFrame = currentFrame,
			TotalFrames = totalFrames,
			TickPositions = GetPauseFrames()
		};
		if (TimelineDisplayViewModel.instance != null)
		{
			TimelineDisplayViewModel.instance.updateEvent.Invoke(arg);
		}
	}

	public void UpdateFrameInformation()
	{
		UpdateFrameInformation(CurFrameIndex, GetNumFrames());
	}

	public Frame GetFrameAtPos(int xYOfset)
	{
		foreach (Frame allFrame in allFrames)
		{
			if (allFrame.ContainsPositionOfset(xYOfset))
			{
				return allFrame;
			}
		}
		return null;
	}

	public int GetFrameIndexAtPos(int xYOfset)
	{
		int num = 0;
		foreach (Frame allFrame in allFrames)
		{
			if (allFrame.ContainsPositionOfset(xYOfset))
			{
				return num;
			}
			num++;
		}
		return -1;
	}

	public int GetTileIndexAtPos(int xYOfset)
	{
		foreach (Frame allFrame in allFrames)
		{
			if (allFrame.ContainsPositionOfset(xYOfset))
			{
				return allFrame.GetIndexOfPositionOfset(xYOfset);
			}
		}
		return -1;
	}

	public bool IsBuildingAtMaximum(LowerLevelItems buildingType)
	{
		int numOfBuilding = GetNumOfBuilding(buildingType);
		int buildingWidth = Definitions.GetBuildingWidth(buildingType);
		int buildingHeight = Definitions.GetBuildingHeight(buildingType);
		switch (buildingType)
		{
		case LowerLevelItems.Keep:
		case LowerLevelItems.TradingPost:
		case LowerLevelItems.Stockade:
		case LowerLevelItems.MercenaryPost:
		case LowerLevelItems.Barracks:
			if (numOfBuilding == 1)
			{
				return true;
			}
			break;
		default:
			if (numOfBuilding == CalulateNumFitInGrid(buildingWidth, buildingHeight))
			{
				return true;
			}
			break;
		}
		return false;
	}

	public void InsertFrame(int selectedFrame)
	{
		int index = CurFrameIndex;
		Frame item = allFrames[selectedFrame];
		allFrames.RemoveAt(selectedFrame);
		allFrames.Insert(index, item);
		Update();
	}

	private int CalulateNumFitInGrid(int shapeWidth, int shapeHeight)
	{
		int num = 100 / shapeWidth;
		int num2 = 100 / shapeHeight;
		return num * num2;
	}

	public int GetNumOfBuilding(LowerLevelItems buildingType)
	{
		int num = 0;
		foreach (Frame allFrame in allFrames)
		{
			if (allFrame.itemType == buildingType)
			{
				num++;
			}
		}
		return num;
	}

	public bool HasPlacedKeep()
	{
		foreach (Frame allFrame in allFrames)
		{
			if (allFrame.itemType == LowerLevelItems.Keep)
			{
				return true;
			}
		}
		return false;
	}

	public void MoveAllRight()
	{
		foreach (Frame allFrame in allFrames)
		{
			for (int i = 0; i < allFrame.tilePositionOfsets.Count; i++)
			{
				allFrame.tilePositionOfsets[i]++;
			}
		}
	}

	public void MoveAllLeft()
	{
		foreach (Frame allFrame in allFrames)
		{
			for (int i = 0; i < allFrame.tilePositionOfsets.Count; i++)
			{
				allFrame.tilePositionOfsets[i]--;
			}
		}
	}

	public void MoveAllUp()
	{
		foreach (Frame allFrame in allFrames)
		{
			for (int i = 0; i < allFrame.tilePositionOfsets.Count; i++)
			{
				allFrame.tilePositionOfsets[i] += 100;
			}
		}
	}

	public void MoveAllDown()
	{
		foreach (Frame allFrame in allFrames)
		{
			for (int i = 0; i < allFrame.tilePositionOfsets.Count; i++)
			{
				allFrame.tilePositionOfsets[i] -= 100;
			}
		}
	}
}
