using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CrusaderCastleEditorUnity;
using UnityEngine;

public class JsonReadWrite : MonoBehaviour
{
	public class SaveData
	{
		public int pauseDelayAmount;

		public List<Frame> frames;

		public List<MiscItemDefinition> miscItems;

		public void GenerateSaveData()
		{
			pauseDelayAmount = FrameManager.Instance.pauseDelayAmount;
			frames = FrameManager.Instance.GetAllFramesForExport();
			miscItems = TilemapManager.instance.GetMiscItemDefinitions();
		}
	}

	public class LordRoot
	{
		public NewAIC lord = new NewAIC();
	}

	[Serializable]
	public class NewAIC
	{
		public int opponent_type;

		public int lord_gfx_type;

		public int flag_type;

		public int use_of_religion;

		public int use_of_ale;

		public int vlow_popularity;

		public int low_popularity;

		public int high_popularity;

		public int min_tax;

		public int max_tax;

		public int[] farm_types = new int[8];

		public int people_to_farm_ratio;

		public int extract_wood_ratio;

		public int extract_stone_ratio;

		public int extract_iron_ratio;

		public int extract_pitch_ratio;

		public int max_quarries;

		public int max_mines;

		public int max_woodcutters;

		public int max_pitch_dugouts;

		public int max_farms;

		public int build_rate;

		public int crushed_building_delay;

		public int sell_food_at;

		public int buy_apples_at;

		public int buy_cheese_at;

		public int buy_bread_at;

		public int buy_wheat_at;

		public int buy_hops_at;

		public int buy_food_amount;

		public int buy_weapons;

		public int pester_for_goods_delay;

		public int send_goods_margin;

		public int ration_boost;

		public int trade_wood_at;

		public int trade_stone_at;

		public int trade_resources_at;

		public int trade_flour_at;

		public int trade_weapons_at;

		public int trade_ale_at;

		public int trade_pitch_at;

		public int trade_minimum;

		public int base_gold_reserves;

		public int blacksmiths_make;

		public int fletchers_make;

		public int poleturners_make;

		public int[] sell_all = new int[15];

		public int move_mobile_defenders;

		public int max_mobile_groups;

		public int buy_defense_machines_at;

		public int buy_defense_machines_delay;

		public int dog_release_timing;

		public int dog_points_count;

		public int[] chance_of_defensive = new int[3];

		public int[] chance_of_harrasment = new int[3];

		public int[] chance_of_seiging = new int[3];

		public int economy_protection_number;

		public int economy_protection_type;

		public int bodyguard_number;

		public int bodyguard_type;

		public int moat_diggers;

		public int moat_digger_type;

		public int[] troop_production_rate = new int[3];

		public int defense_patrol_trigger_level;

		public int defense_patrols;

		public int defense_patrol_style;

		public int defense_patrol_delay;

		public int defensive_trigger_level;

		public int[] defensive_troops = new int[8];

		public int harrasment_trigger_level;

		public int harrasment_trigger_variance;

		public int[] harrasment_troops = new int[8];

		public int[] harrasment_machines = new int[8];

		public int max_harrasment_machines;

		public int harrass_delay;

		public int siege_trigger_level;

		public int siege_trigger_variance;

		public int siege_troops_before_will_come_to_rescue;

		public int siege_troops_on_site_percent;

		public int siege_troops_at_home_percent;

		public int siege_soften_up_delay;

		public int siege_victory_delay;

		public int percent_chance_waiting_for_joint_attack;

		public int[] siege_machines = new int[8];

		public int siege_cow_timer;

		public int siege_eng_amount;

		public int siege_moat_troop;

		public int siege_moat_amount;

		public int siege_herring_troop;

		public int siege_herring_amount;

		public int siege_assasin_amount;

		public int siege_ladder_amount;

		public int siege_tunnel_amount;

		public int siege_storm_troop;

		public int siege_storm_amount;

		public int siege_storm_tribes;

		public int siege_cover_troop;

		public int siege_cover_amount;

		public int siege_cover_tribes;

		public int siege_shock_troop;

		public int siege_shock_amount;

		public int siege_reserve_troop;

		public int siege_reserve_amount;

		public int siege_reserve_tribes;

		public int[] siege_wall_troops = new int[24];

		public int siege_wall_amount;

		public int siege_wall_tribes;

		public int who_to_pick_on;

		public int use_improved_sieging;

		public int[] starting_troops_normal = new int[28];

		public int[] starting_troops_deathmatch = new int[28];

		public int[] starting_troops_crusader = new int[28];

		public int lord_power_display_level;

		public int lord_hps_percent;

		public int siege_max_troops = 200;

		public int siege_normal_wave_multiplier = 5;

		public int siege_high_gold_wave_multiplier = 7;
	}

	public static JsonReadWrite instance;

	public void Awake()
	{
		instance = this;
	}

	public void LoadAIVFromJson(string fileName)
	{
		SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(fileName));
		StartCoroutine(LoadCo(data));
	}

	private IEnumerator LoadCo(SaveData data)
	{
		float delayTime = 0.075f;
		MainViewModel.instance.ShouldShowLoadSaveVillagePanel(should: false);
		MainViewModel.instance.ShouldShowProgressPanel(should: true);
		PnlProgressViewModel.instance.DisplayAsLoading();
		PnlProgressViewModel.instance.UpdateProgress(25);
		yield return new WaitForSeconds(delayTime);
		SideUIViewModel.Instance.SetPauseDelay(data.pauseDelayAmount);
		PnlProgressViewModel.instance.UpdateProgress(50);
		yield return new WaitForSeconds(delayTime);
		foreach (Frame frame in data.frames)
		{
			TilemapManager.instance.PlaceFrameFromFile(frame);
		}
		PnlProgressViewModel.instance.UpdateProgress(75);
		yield return new WaitForSeconds(delayTime);
		foreach (MiscItemDefinition miscItem in data.miscItems)
		{
			TilemapManager.instance.PlaceMiscItemFromFile(miscItem);
		}
		PnlProgressViewModel.instance.UpdateProgress(100);
		yield return new WaitForSeconds(delayTime);
		FrameManager.Instance.UpdateFrameInformation();
		ItemPlacementManager.Instance.DeselectSideUIItem();
		TilemapManager.instance.ClearPreviewTilemap();
		MainViewModel.instance.ShouldShowProgressPanel(should: false);
		TilemapManager.instance.SetHasMadeChangesToFalse();
		MainViewModel.instance.HideMainMenuAndShowSideUI();
		SideUIViewModel.Instance.ShouldFrameButtonsBeInteractable(should: true);
		yield return null;
	}

	public void OverwriteFile()
	{
		if (PnlLoadSaveViewModel.instance.FileName != null)
		{
			StartCoroutine(SaveCo(PnlLoadSaveViewModel.instance.FileName));
		}
		PnlLoadSaveViewModel.instance.SetLoadedItemToItemWithCurrentFilename();
	}

	public void SaveFile(string fileName)
	{
		if (!string.IsNullOrEmpty(fileName))
		{
			StartCoroutine(SaveCo(fileName));
		}
	}

	private IEnumerator SaveCo(string fileName)
	{
		float delayTime = 0.075f;
		MainViewModel.instance.ShouldShowLoadSaveVillagePanel(should: false);
		MainViewModel.instance.ShouldShowProgressPanel(should: true);
		PnlProgressViewModel.instance.DisplayAsSaving();
		SaveData data = new SaveData();
		PnlProgressViewModel.instance.UpdateProgress(25);
		yield return new WaitForSeconds(delayTime);
		data.pauseDelayAmount = FrameManager.Instance.pauseDelayAmount;
		PnlProgressViewModel.instance.UpdateProgress(50);
		yield return new WaitForSeconds(delayTime);
		data.frames = FrameManager.Instance.GetAllFramesForExport();
		PnlProgressViewModel.instance.UpdateProgress(75);
		yield return new WaitForSeconds(delayTime);
		data.miscItems = TilemapManager.instance.GetMiscItemDefinitions();
		PnlProgressViewModel.instance.UpdateProgress(100);
		yield return new WaitForSeconds(delayTime);
		string contents = JsonUtility.ToJson(data, prettyPrint: false);
		File.WriteAllText(Path.Combine(Helpers.GetFolderPath(), fileName + ".aivjson"), contents);
		MainViewModel.instance.ShouldShowProgressPanel(should: false);
		TilemapManager.instance.SetHasMadeChangesToFalse();
		CameraController.instance.EnableCameraMovement();
		yield return null;
	}

	public SaveData createSaveData()
	{
		return new SaveData
		{
			pauseDelayAmount = FrameManager.Instance.pauseDelayAmount,
			frames = FrameManager.Instance.GetAllFramesForExport(),
			miscItems = TilemapManager.instance.GetMiscItemDefinitions()
		};
	}

	public void loadFromSaveData(SaveData data)
	{
		SideUIViewModel.Instance.SetPauseDelay(data.pauseDelayAmount);
		foreach (Frame frame in data.frames)
		{
			TilemapManager.instance.PlaceFrameFromFile(frame);
		}
		foreach (MiscItemDefinition miscItem in data.miscItems)
		{
			TilemapManager.instance.PlaceMiscItemFromFile(miscItem);
		}
		FrameManager.Instance.UpdateFrameInformation();
		ItemPlacementManager.Instance.DeselectSideUIItem();
		TilemapManager.instance.ClearPreviewTilemap();
		MainViewModel.instance.HideMainMenuAndShowSideUI();
		SideUIViewModel.Instance.ShouldFrameButtonsBeInteractable(should: true);
	}

	public LordRoot LoadLord(string path)
	{
		return JsonUtility.FromJson<LordRoot>(File.ReadAllText(path));
	}

	public void saveLordFile(string fileName)
	{
		string contents = JsonUtility.ToJson(Helpers.SelectedLordConfig, prettyPrint: false);
		File.WriteAllText(Path.Combine(Helpers.GetFolderPath(), fileName + ".lordjson"), contents);
	}

	public void ImportAICCommunityJson(string path)
	{
	}
}
