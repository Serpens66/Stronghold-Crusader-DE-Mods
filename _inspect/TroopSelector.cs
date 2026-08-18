using System;
using System.Collections.Generic;
using UnityEngine;
using Vectrosity;

public class TroopSelector : MonoBehaviour
{
	public static TroopSelector instance;

	public VectorLine selectionLine;

	public Vector2 originalPos;

	public bool selection_on;

	public bool selection_established;

	public DateTime selectionClickTime = DateTime.MinValue;

	public void Start()
	{
		instance = this;
		selectionLine = new VectorLine("Selection", new List<Vector2>(5), 3f, LineType.Continuous);
		selectionLine.capLength = 1.5f;
		selectionLine.active = false;
	}

	public void startSelection(Vector2 startPos, Vector2 curPos)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		selectionLine.SetColor(Color32.op_Implicit(Color.gray));
		originalPos = startPos;
		selection_on = true;
		selection_established = false;
	}

	public void updateSelection(Vector2 curPos)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!selection_established && (Mathf.Abs(curPos.x - originalPos.x) > 16f || Mathf.Abs(curPos.y - originalPos.y) > 16f))
		{
			selection_established = true;
		}
		if (selection_established)
		{
			selectionLine.active = true;
			selectionLine.MakeRect(Vector2.op_Implicit(originalPos), Vector2.op_Implicit(curPos));
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
