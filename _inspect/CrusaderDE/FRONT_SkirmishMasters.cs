using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_SkirmishMasters : UserControl
{
	public Grid RefMainPanel;

	public ListView RefList;

	public CheckBox RefIncludeTrail;

	public CheckBox RefIncludeCustom;

	public CheckBox RefBestScoreOnly;

	public bool includeTrails = true;

	public bool includeCustom = true;

	public bool bestScoreOnly;

	public int sortMode;

	public bool panelActive;

	public string prevHelp = "";

	public FRONT_SkirmishMasters()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected O, but got Unknown
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Expected O, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Expected O, but got Unknown
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Expected O, but got Unknown
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Expected O, but got Unknown
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Expected O, but got Unknown
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.FRONTSkirmishMasters = this;
		RefMainPanel = (Grid)((FrameworkElement)this).FindName("MainPanel");
		RefIncludeTrail = (CheckBox)((FrameworkElement)this).FindName("IncludeTrail");
		((ToggleButton)RefIncludeTrail).Checked += new RoutedEventHandler(Check_ValueChanged);
		((ToggleButton)RefIncludeTrail).Unchecked += new RoutedEventHandler(TrailUnCheck_ValueChanged);
		RefIncludeCustom = (CheckBox)((FrameworkElement)this).FindName("IncludeCustom");
		((ToggleButton)RefIncludeCustom).Checked += new RoutedEventHandler(Check_ValueChanged);
		((ToggleButton)RefIncludeCustom).Unchecked += new RoutedEventHandler(CustomUnCheck_ValueChanged);
		RefBestScoreOnly = (CheckBox)((FrameworkElement)this).FindName("BestScoreOnly");
		((ToggleButton)RefBestScoreOnly).Checked += new RoutedEventHandler(Check_ValueChanged);
		((ToggleButton)RefBestScoreOnly).Unchecked += new RoutedEventHandler(Check_ValueChanged);
		RefList = (ListView)((FrameworkElement)this).FindName("SkimrishMastersList");
		((Selector)RefList).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefList).SelectedItem != null)
			{
				if (Input.GetMouseButtonDown(1) || Input.GetMouseButton(1))
				{
					SkirmishMastersRow record = (SkirmishMastersRow)((Selector)RefList).SelectedItem;
					HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 13), delegate
					{
						ConfigSettings.DeleteSkirmishMastersGame(record.scoreData);
						CreateList();
					}, delegate
					{
					}, MPConf: false, skirmishMasters: true);
				}
				else
				{
					HUD_MissionOver.ShowSkirmishMasters(Enums.VictoryScreens.banquet, ((SkirmishMastersRow)((Selector)RefList).SelectedItem).scoreData);
				}
				((Selector)RefList).SelectedItem = null;
			}
		};
	}

	public static void Open()
	{
		MainViewModel.Instance.Show_SkirmishMasters = true;
		MainViewModel.Instance.FRONTSkirmishMasters.doOpen();
		SFXManager.instance.playUISound(253);
	}

	public void doOpen()
	{
		panelActive = true;
		RadioButton val = ((IEnumerable)((Panel)RefMainPanel).Children).OfType<RadioButton>().FirstOrDefault((RadioButton r) => ((ToggleButton)r).IsChecked.HasValue && ((ToggleButton)r).IsChecked.Value);
		if ((BaseComponent)(object)val != (BaseComponent)null)
		{
			ButtonClicked((string)((ButtonBase)val).CommandParameter);
		}
		else
		{
			CreateList();
		}
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "SortScore":
		{
			sortMode = 0;
			string text = (MainViewModel.Instance.SkirmishMasters_Sorted = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 18));
			prevHelp = text;
			CreateList();
			break;
		}
		case "SortName":
		{
			sortMode = 1;
			string text = (MainViewModel.Instance.SkirmishMasters_Sorted = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 19));
			prevHelp = text;
			CreateList();
			break;
		}
		case "SortTime":
		{
			sortMode = 2;
			string text = (MainViewModel.Instance.SkirmishMasters_Sorted = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 21));
			prevHelp = text;
			CreateList();
			break;
		}
		case "SortDate":
		{
			sortMode = 3;
			string text = (MainViewModel.Instance.SkirmishMasters_Sorted = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 20));
			prevHelp = text;
			CreateList();
			break;
		}
		case "Back":
			MainViewModel.Instance.Show_SkirmishMasters = false;
			break;
		}
	}

	public void ButtonEnter(string param)
	{
		switch (param)
		{
		default:
			_ = param == "Back";
			break;
		case "SortScore":
			prevHelp = MainViewModel.Instance.SkirmishMasters_Sorted;
			MainViewModel.Instance.SkirmishMasters_Sorted = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 14);
			break;
		case "SortName":
			prevHelp = MainViewModel.Instance.SkirmishMasters_Sorted;
			MainViewModel.Instance.SkirmishMasters_Sorted = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 15);
			break;
		case "SortTime":
			prevHelp = MainViewModel.Instance.SkirmishMasters_Sorted;
			MainViewModel.Instance.SkirmishMasters_Sorted = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 17);
			break;
		case "SortDate":
			prevHelp = MainViewModel.Instance.SkirmishMasters_Sorted;
			MainViewModel.Instance.SkirmishMasters_Sorted = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKMASTERS, 16);
			break;
		}
	}

	public void ButtonLeave(string param)
	{
		MainViewModel.Instance.SkirmishMasters_Sorted = prevHelp;
	}

	public void CreateList()
	{
		includeTrails = ((ToggleButton)RefIncludeTrail).IsChecked.Value;
		includeCustom = ((ToggleButton)RefIncludeCustom).IsChecked.Value;
		bestScoreOnly = ((ToggleButton)RefBestScoreOnly).IsChecked.Value;
		List<SkirmishMastersRow> list = new List<SkirmishMastersRow>();
		List<EngineInterface.MPScoreData> skirmishMastersData = ConfigSettings.GetSkirmishMastersData();
		List<EngineInterface.MPScoreData> list2 = new List<EngineInterface.MPScoreData>();
		Dictionary<ulong, EngineInterface.MPScoreData> dictionary = new Dictionary<ulong, EngineInterface.MPScoreData>();
		foreach (EngineInterface.MPScoreData item in skirmishMastersData)
		{
			if ((item.trailLevel >= 0 || !includeCustom) && (item.trailLevel < 0 || !includeTrails))
			{
				continue;
			}
			if (!bestScoreOnly)
			{
				list2.Add(item);
				continue;
			}
			if (item.unique == 0L && item.trailLevel < 0)
			{
				list2.Add(item);
				continue;
			}
			ulong key = ((item.trailLevel >= 0) ? ((ulong)item.trailLevel) : item.unique);
			if (dictionary.ContainsKey(key))
			{
				EngineInterface.MPScoreData mPScoreData = dictionary[key];
				if (mPScoreData.score < item.score)
				{
					dictionary[key] = item;
				}
				else if (mPScoreData.score == item.score && mPScoreData.real_time > item.real_time)
				{
					dictionary[key] = item;
				}
			}
			else
			{
				dictionary[key] = item;
			}
		}
		if (bestScoreOnly)
		{
			foreach (KeyValuePair<ulong, EngineInterface.MPScoreData> item2 in dictionary)
			{
				list2.Add(item2.Value);
			}
		}
		switch (sortMode)
		{
		case 0:
			list2.Sort(delegate(EngineInterface.MPScoreData x, EngineInterface.MPScoreData y)
			{
				int num2 = y.score.CompareTo(x.score);
				if (num2 == 0)
				{
					num2 = x.real_time.CompareTo(y.real_time);
				}
				return num2;
			});
			break;
		case 1:
			list2.Sort(delegate(EngineInterface.MPScoreData x, EngineInterface.MPScoreData y)
			{
				string trailName = "";
				return GetTrailMapName(x, ref trailName).CompareTo(GetTrailMapName(y, ref trailName));
			});
			break;
		case 2:
			list2.Sort(delegate(EngineInterface.MPScoreData x, EngineInterface.MPScoreData y)
			{
				int num2 = x.real_time.CompareTo(y.real_time);
				if (num2 == 0)
				{
					num2 = y.score.CompareTo(x.score);
				}
				return num2;
			});
			break;
		case 3:
			list2.Sort(delegate(EngineInterface.MPScoreData x, EngineInterface.MPScoreData y)
			{
				DateTime value = new DateTime(x.completedDate_Year, x.completedDate_Month, x.completedDate_Day, x.completedDate_Hour, x.completedDate_Minute, x.completedDate_Second);
				return new DateTime(y.completedDate_Year, y.completedDate_Month, y.completedDate_Day, y.completedDate_Hour, y.completedDate_Minute, y.completedDate_Second).CompareTo(value);
			});
			break;
		}
		int num = 1;
		foreach (EngineInterface.MPScoreData item3 in list2)
		{
			list.Add(new SkirmishMastersRow(this, num, item3));
			num++;
		}
		((ItemsControl)RefList).ItemsSource = list;
	}

	public void UpdateButtons()
	{
	}

	public void TrailUnCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			if (!((ToggleButton)RefIncludeCustom).IsChecked.Value)
			{
				((ToggleButton)RefIncludeCustom).IsChecked = true;
			}
			else
			{
				CreateList();
			}
		}
	}

	public void CustomUnCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			if (!((ToggleButton)RefIncludeTrail).IsChecked.Value)
			{
				((ToggleButton)RefIncludeTrail).IsChecked = true;
			}
			else
			{
				CreateList();
			}
		}
	}

	public void Check_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			CreateList();
		}
	}

	public static string GetTrailMapName(EngineInterface.MPScoreData score, ref string trailName)
	{
		string result = "";
		trailName = "";
		if (score.trailLevel < 0)
		{
			result = score.mapName;
		}
		else
		{
			int num = 0;
			int num2 = 0;
			if (score.trailLevel < 50)
			{
				num = 0;
				num2 = score.trailLevel;
			}
			else if (score.trailLevel < 80)
			{
				num = 1;
				num2 = score.trailLevel - 50;
			}
			else if (score.trailLevel < 100)
			{
				num = 2;
				num2 = score.trailLevel - 80;
			}
			else if (score.trailLevel >= 20000)
			{
				num = ((score.trailLevel < 21000) ? 100 : 101);
				num2 = score.trailLevel % 1000;
			}
			else
			{
				num = score.trailLevel / 1000;
				num2 = score.trailLevel % 1000;
			}
			if (score.trailName != null && score.trailName.Length > 0)
			{
				result = score.trailLevel + ". " + score.trailName;
				trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 55) + " " + score.trailName;
			}
			else
			{
				switch (num)
				{
				case 0:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, 1 + num2);
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_CHOOSE2, 6);
					break;
				case 1:
					result = num2 + 51 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, 51 + num2);
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_CHOOSE2, 21);
					break;
				case 2:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, 81 + num2);
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_CHOOSE2, 23);
					break;
				case 11:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(7 + num2));
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 1);
					break;
				case 12:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(12 + num2));
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 2);
					break;
				case 13:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(19 + num2));
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 3);
					break;
				case 14:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(28 + num2));
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 4);
					break;
				case 15:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(39 + num2));
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 5);
					break;
				case 16:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(49 + num2));
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 6);
					break;
				case 17:
					result = num2 + 1 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, (Enums.eTextValues)(82 + num2));
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 80);
					break;
				case 100:
					result = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, 1 + num2);
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, 22);
					break;
				case 101:
					result = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, 1 + num2 + 10);
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, 23);
					break;
				case 102:
					result = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, 1 + num2 + 25);
					trailName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, 24);
					break;
				}
			}
		}
		return result;
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_SkirmishMasters.xaml");
	}
}
