using System.Collections.ObjectModel;
using System.ComponentModel;
using Noesis;

namespace CrusaderDE;

public class SkirmishMastersRow : INotifyPropertyChanged
{
	public string _missionName;

	public string _score;

	public string _order;

	public string _date;

	public string _time;

	public string _trailName = "";

	public ImageSource _rowBackground;

	public ImageSource _alliesFace0;

	public ImageSource _alliesFace1;

	public ImageSource _alliesFace2;

	public ImageSource _alliesFace3;

	public ImageSource _alliesFace4;

	public ImageSource _alliesFace5;

	public ImageSource _alliesFace6;

	public ImageSource _alliesFace7;

	public ImageSource _alliesFaceBackground;

	public ImageSource _alliesFaceBackground0;

	public ImageSource _alliesFaceBackground1;

	public ImageSource _alliesFaceBackground2;

	public ImageSource _alliesFaceBackground3;

	public ImageSource _alliesFaceBackground4;

	public ImageSource _alliesFaceBackground5;

	public ImageSource _alliesFaceBackground6;

	public ImageSource _alliesFaceBackground7;

	public FRONT_SkirmishMasters parent;

	public EngineInterface.MPScoreData scoreData;

	public ObservableCollection<bool> SkirmishBriefingAlly { get; set; }

	public ObservableCollection<bool> SkirmishBriefingAllyFight { get; set; }

	public ObservableCollection<bool> SkirmishBriefingAllyDead { get; set; }

	public ObservableCollection<bool> AlliesHumanFaceVis { get; set; }

	public string GlobalTextFlow
	{
		get
		{
			return MainViewModel.Instance.GlobalTextFlow;
		}
		set
		{
		}
	}

	public string GlobalTextFlowAL2R
	{
		get
		{
			return MainViewModel.Instance.GlobalTextFlowAL2R;
		}
		set
		{
		}
	}

	public string MissionName
	{
		get
		{
			return _missionName;
		}
		set
		{
			_missionName = value;
			NotifyPropertyChanged("MissionName");
		}
	}

	public string Score
	{
		get
		{
			return _score;
		}
		set
		{
			_score = value;
			NotifyPropertyChanged("Score");
		}
	}

	public string Order
	{
		get
		{
			return _order;
		}
		set
		{
			_order = value;
			NotifyPropertyChanged("Order");
		}
	}

	public string Date
	{
		get
		{
			return _date;
		}
		set
		{
			_date = value;
			NotifyPropertyChanged("Date");
		}
	}

	public string Time
	{
		get
		{
			return _time;
		}
		set
		{
			_time = value;
			NotifyPropertyChanged("Time");
		}
	}

	public string TrailName
	{
		get
		{
			return _trailName;
		}
		set
		{
			_trailName = value;
			NotifyPropertyChanged("TrailName");
		}
	}

	public ImageSource RowBackground
	{
		get
		{
			return _rowBackground;
		}
		set
		{
			_rowBackground = value;
			NotifyPropertyChanged("RowBackground");
		}
	}

	public ImageSource AlliesFace0
	{
		get
		{
			return _alliesFace0;
		}
		set
		{
			if ((BaseComponent)(object)_alliesFace0 != (BaseComponent)(object)value)
			{
				_alliesFace0 = value;
				NotifyPropertyChanged("AlliesFace0");
			}
		}
	}

	public ImageSource AlliesFace1
	{
		get
		{
			return _alliesFace1;
		}
		set
		{
			if ((BaseComponent)(object)_alliesFace1 != (BaseComponent)(object)value)
			{
				_alliesFace1 = value;
				NotifyPropertyChanged("AlliesFace1");
			}
		}
	}

	public ImageSource AlliesFace2
	{
		get
		{
			return _alliesFace2;
		}
		set
		{
			if ((BaseComponent)(object)_alliesFace2 != (BaseComponent)(object)value)
			{
				_alliesFace2 = value;
				NotifyPropertyChanged("AlliesFace2");
			}
		}
	}

	public ImageSource AlliesFace3
	{
		get
		{
			return _alliesFace3;
		}
		set
		{
			if ((BaseComponent)(object)_alliesFace3 != (BaseComponent)(object)value)
			{
				_alliesFace3 = value;
				NotifyPropertyChanged("AlliesFace3");
			}
		}
	}

	public ImageSource AlliesFace4
	{
		get
		{
			return _alliesFace4;
		}
		set
		{
			if ((BaseComponent)(object)_alliesFace4 != (BaseComponent)(object)value)
			{
				_alliesFace4 = value;
				NotifyPropertyChanged("AlliesFace4");
			}
		}
	}

	public ImageSource AlliesFace5
	{
		get
		{
			return _alliesFace5;
		}
		set
		{
			if ((BaseComponent)(object)_alliesFace5 != (BaseComponent)(object)value)
			{
				_alliesFace5 = value;
				NotifyPropertyChanged("AlliesFace5");
			}
		}
	}

	public ImageSource AlliesFace6
	{
		get
		{
			return _alliesFace6;
		}
		set
		{
			if ((BaseComponent)(object)_alliesFace6 != (BaseComponent)(object)value)
			{
				_alliesFace6 = value;
				NotifyPropertyChanged("AlliesFace6");
			}
		}
	}

	public ImageSource AlliesFace7
	{
		get
		{
			return _alliesFace7;
		}
		set
		{
			if ((BaseComponent)(object)_alliesFace7 != (BaseComponent)(object)value)
			{
				_alliesFace7 = value;
				NotifyPropertyChanged("AlliesFace7");
			}
		}
	}

	public ImageSource AlliesFaceBackground
	{
		get
		{
			return _alliesFaceBackground;
		}
		set
		{
			_alliesFaceBackground = value;
			NotifyPropertyChanged("AlliesFaceBackground");
		}
	}

	public ImageSource AlliesFaceBackground0
	{
		get
		{
			return _alliesFaceBackground0;
		}
		set
		{
			_alliesFaceBackground0 = value;
			NotifyPropertyChanged("AlliesFaceBackground0");
		}
	}

	public ImageSource AlliesFaceBackground1
	{
		get
		{
			return _alliesFaceBackground1;
		}
		set
		{
			_alliesFaceBackground1 = value;
			NotifyPropertyChanged("AlliesFaceBackground1");
		}
	}

	public ImageSource AlliesFaceBackground2
	{
		get
		{
			return _alliesFaceBackground2;
		}
		set
		{
			_alliesFaceBackground2 = value;
			NotifyPropertyChanged("AlliesFaceBackground2");
		}
	}

	public ImageSource AlliesFaceBackground3
	{
		get
		{
			return _alliesFaceBackground3;
		}
		set
		{
			_alliesFaceBackground3 = value;
			NotifyPropertyChanged("AlliesFaceBackground3");
		}
	}

	public ImageSource AlliesFaceBackground4
	{
		get
		{
			return _alliesFaceBackground4;
		}
		set
		{
			_alliesFaceBackground4 = value;
			NotifyPropertyChanged("AlliesFaceBackground4");
		}
	}

	public ImageSource AlliesFaceBackground5
	{
		get
		{
			return _alliesFaceBackground5;
		}
		set
		{
			_alliesFaceBackground5 = value;
			NotifyPropertyChanged("AlliesFaceBackground5");
		}
	}

	public ImageSource AlliesFaceBackground6
	{
		get
		{
			return _alliesFaceBackground6;
		}
		set
		{
			_alliesFaceBackground6 = value;
			NotifyPropertyChanged("AlliesFaceBackground6");
		}
	}

	public ImageSource AlliesFaceBackground7
	{
		get
		{
			return _alliesFaceBackground7;
		}
		set
		{
			_alliesFaceBackground7 = value;
			NotifyPropertyChanged("AlliesFaceBackground7");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public SkirmishMastersRow(FRONT_SkirmishMasters _parent, int row, EngineInterface.MPScoreData score)
	{
		scoreData = score;
		if (score.colourMap1[0] < 0)
		{
			SpriteMapping.mpLoadRemapping = null;
		}
		else
		{
			SpriteMapping.mpLoadRemapping = new int[9];
			for (int i = 0; i < 9; i++)
			{
				SpriteMapping.mpLoadRemapping[i] = score.colourMap1[i];
			}
		}
		for (int j = 0; j < 9; j++)
		{
			SpriteMapping.remapColours[j] = score.colourMap2[j];
		}
		int[] array = new int[10];
		int[] array2 = new int[10];
		int[,] array3 = new int[10, 3];
		int num = 0;
		int[] array4 = new int[10];
		SkirmishBriefingAlly = new ObservableCollection<bool>();
		SkirmishBriefingAllyFight = new ObservableCollection<bool>();
		SkirmishBriefingAllyDead = new ObservableCollection<bool>();
		AlliesHumanFaceVis = new ObservableCollection<bool>();
		for (int k = 0; k < 8; k++)
		{
			SkirmishBriefingAlly.Add(item: false);
			SkirmishBriefingAllyFight.Add(item: false);
			SkirmishBriefingAllyDead.Add(item: false);
			AlliesHumanFaceVis.Add(item: false);
		}
		parent = _parent;
		Order = row.ToString();
		if ((row & 1) != 0)
		{
			RowBackground = MainViewModel.Instance.GameSprites[586];
		}
		else
		{
			RowBackground = MainViewModel.Instance.GameSprites[587];
		}
		string trailName = "";
		MissionName = FRONT_SkirmishMasters.GetTrailMapName(score, ref trailName);
		TrailName = trailName;
		Score = score.score.ToString();
		Date = score.completedDate_Day + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, score.completedDate_Month - 1) + " " + score.completedDate_Year;
		int seconds = score.real_time / 1000;
		Time = GameData.GetTimeString(seconds);
		for (int k = 0; k < 10; k++)
		{
			array3[k, 0] = 0;
		}
		int num2 = 0;
		for (int k = 1; k < 9; k++)
		{
			int num3 = k;
			if (score.valid[num3] != 0)
			{
				array3[num2, 0] = k;
				array4[num2] = score.teams[k];
				if (score.time_lord_killed[k] != 0)
				{
					array2[k] = 1;
					array3[num2, 1] = 0;
					array3[num2++, 2] = 0;
				}
				else
				{
					num2++;
				}
			}
		}
		int num4 = 9;
		int l;
		for (int k = 0; k < num2; k++)
		{
			int num5 = -1;
			for (l = 0; l < num2; l++)
			{
				if (array3[l, 0] != 0 && (num5 == -1 || array4[l] < num))
				{
					num5 = l;
					num = array4[l];
				}
			}
			if (num5 < 0)
			{
				break;
			}
			array[k + 1] = array3[num5, 0];
			array3[num5, 0] = 0;
			if (array4[k] < num4)
			{
				num4 = array4[k];
			}
		}
		l = num4;
		for (num2 = 1; num2 < 9; num2++)
		{
			int num3 = array[num2];
			if (num3 >= 1)
			{
				if (score.teams[num3] > l)
				{
					l = score.teams[num3];
					SkirmishBriefingAllyFight[num2 - 2] = true;
				}
				SkirmishBriefingAlly[num2 - 1] = true;
				if (score.computer_register[num3] == 0)
				{
					AlliesHumanFaceVis[num2 - 1] = true;
					setAlliesFace(num2 - 1, Platform_Multiplayer.Instance.GetLocalAvatar(), MainViewModel.Instance.getAIFaceBackground(num3));
				}
				else
				{
					setAlliesFace(num2 - 1, MainViewModel.Instance.getAIFace(score.computer_register[num3]), MainViewModel.Instance.getAIFaceBackground(num3));
				}
				if (array2[num3] > 0)
				{
					SkirmishBriefingAllyDead[num2 - 1] = true;
				}
				continue;
			}
			break;
		}
	}

	public void setAlliesFace(int ally, ImageSource face, ImageSource background)
	{
		switch (ally)
		{
		case 0:
			AlliesFace0 = face;
			AlliesFaceBackground0 = background;
			break;
		case 1:
			AlliesFace1 = face;
			AlliesFaceBackground1 = background;
			break;
		case 2:
			AlliesFace2 = face;
			AlliesFaceBackground2 = background;
			break;
		case 3:
			AlliesFace3 = face;
			AlliesFaceBackground3 = background;
			break;
		case 4:
			AlliesFace4 = face;
			AlliesFaceBackground4 = background;
			break;
		case 5:
			AlliesFace5 = face;
			AlliesFaceBackground5 = background;
			break;
		case 6:
			AlliesFace6 = face;
			AlliesFaceBackground6 = background;
			break;
		case 7:
			AlliesFace7 = face;
			AlliesFaceBackground7 = background;
			break;
		}
	}
}
