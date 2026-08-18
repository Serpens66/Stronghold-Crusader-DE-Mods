using System;
using System.Collections.Generic;
using UnityEngine;
using Vectrosity;

public class TroopSelector : MonoBehaviour
{
	public static TroopSelector instance;

	private VectorLine selectionLine;

	private Vector2 originalPos;

	public bool selection_on;

	public bool selection_established;

	private DateTime selectionClickTime = DateTime.MinValue;

	private void Start()
	{
		instance = this;
		selectionLine = new VectorLine("Selection", new List<Vector2>(5), 3f, LineType.Continuous);
		selectionLine.capLength = 1.5f;
		selectionLine.active = false;
	}

	public void startSelection(Vector2 startPos, Vector2 curPos)
	{
		selectionLine.SetColor(Color.gray);
		originalPos = startPos;
		selection_on = true;
		selection_established = false;
	}

	public void updateSelection(Vector2 curPos)
	{
		if (!selection_established && (Mathf.Abs(curPos.x - originalPos.x) > 16f || Mathf.Abs(curPos.y - originalPos.y) > 16f))
		{
			selection_established = true;
		}
		if (selection_established)
		{
			selectionLine.active = true;
			selectionLine.MakeRect(originalPos, curPos);
			selectionLine.Draw();
		}
	}

	public void endSelection()
	{
		selectionLine.active = false;
		selection_on = false;
		selection_established = false;
	}
}
