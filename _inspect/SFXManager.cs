using System;
using System.Collections.Generic;
using System.IO;
using CrusaderDE;
using UnityEngine;

public class SFXManager
{
	public class sh1_sound_effect
	{
		public int first_buffer_no;

		public int max_variants;

		public int variants_loaded;

		public int last_variant_played;
	}

	public class sh1_sound
	{
		public float volume;

		public int position;

		public int requests;

		public float real_volume;

		public string name;

		public AudioClip clip;
	}

	public class VolumeData
	{
		public string name;

		public float volume;
	}

	public static SFXManager instance;

	public const int NUM_SFX_VARIANTS = 10;

	public readonly string[,] stronghold_main_list = new string[329, 10]
	{
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\button4 22k", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\chop1 22k", "fx\\chop2 22k", "fx\\chop3 22k", "fx\\chop4 22k", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\sawpull1 22k", "fx\\sawpush1 22k", "fx\\sawpull2 22k", "fx\\sawpush2 22k", "fx\\sawpull3 22k", "fx\\sawpush3 22k", "Null", "Null", "Null", "Null" },
		{ "fx\\stocks1", "fx\\stocks2", "fx\\stocks5", "fx\\stocks7", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bowtwang 22k", "fx\\arrowswish1 22k", "fx\\arrowswish2 22k", "fx\\arrowshoot1 22k", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\arrowhit4 22k", "fx\\arrowhit4 22k", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\tableclick", "fx\\dragndrop", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ilplop_01", "fx\\lilplop_02", "fx\\lilplop_03", "fx\\lilplop_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\medplop_01", "fx\\medplop_02", "fx\\medplop_03", "fx\\medplop_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\drop_plank1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\mill", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\inn_01", "fx\\inn_02", "fx\\inn_03", "fx\\inn_04", "fx\\inn_05", "fx\\inn_06", "fx\\inn_07", "Null", "Null", "Null" },
		{ "fx\\mason_chip1", "fx\\mason_chip2", "fx\\mason_chip3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\mason_crumble1", "fx\\mason_crumble2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\puller_lower", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\puller_strain", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\puller_rock", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\puller_impact", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\puller_return", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\armycharge1", "fx\\armycharge2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\pryer_lever1", "fx\\pryer_lever2", "fx\\pryer_lever3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\drawbridge_lowering", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\drawbridge_lowered", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\drawbridge_raising", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\drawbridge_raised", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\drawbridge_control", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\iron_dump1", "fx\\iron_dump2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\iron_lildump1", "fx\\iron_lildump2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\iron_boil1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\iron_pour1", "fx\\iron_pour2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\iron_pull1", "fx\\iron_pull2", "fx\\iron_pull7", "fx\\iron_pull4", "fx\\iron_pull5", "fx\\iron_pull6", "fx\\iron_pull3", "Null", "Null", "Null" },
		{ "fx\\iron_straining1", "fx\\iron_straining2", "fx\\iron_straining3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stckfood1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stckale1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stckhops1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stckiron2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stckpitch2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stckstone1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stckweap2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stckwheat1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\plank1", "fx\\plank2", "fx\\plank3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bigtreefall1", "fx\\bigtreefall2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\liltreefall", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bs_anvil4", "fx\\bs_anvil2", "fx\\bs_anvil3", "fx\\bs_anvil1", "fx\\bs_anvil5", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bs_bellow1", "fx\\bs_bellow3", "fx\\bs_bellow4", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bs_cooling2", "fx\\bs_cooling3", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bs_pour3", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bs_open4", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bs_file10", "fx\\bs_file12", "fx\\bs_file13", "fx\\bs_file9", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bakebig1", "fx\\bakebig4", "fx\\bakebig5", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bakesmall2", "fx\\bakesmall3", "fx\\bakesmall4", "fx\\bakesmall5", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\mudbub1", "fx\\mudbub2", "fx\\mudbub3", "fx\\mudbub4", "fx\\mudbub5", "fx\\mudbub6", "fx\\mudbub7", "fx\\mudbub8", "Null", "Null" },
		{ "fx\\pit_waterlap1", "fx\\pit_waterlap2", "fx\\pit_waterlap3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\pit_scoop1", "fx\\pit_scoop2", "fx\\pit_scoop3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\pit_pour5", "fx\\pit_pour6", "fx\\pit_pour7", "fx\\pit_pour8", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\tan_cut4", "fx\\tan_lilcut7", "fx\\tan_cut5", "fx\\tan_lilcut8", "fx\\tan_cut6", "fx\\tan_lilcut9", "Null", "Null", "Null", "Null" },
		{ "fx\\tan_upbrush1", "fx\\tan_upbrush2", "fx\\tan_upbrush3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\tan_dnbrush1", "fx\\tan_dnbrush2", "fx\\tan_dnbrush3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\regbow_sand_01", "fx\\regbow_sand_02", "fx\\regbow_sand_03", "fx\\regbow_sand_04", "fx\\regbow_sand_05", "fx\\regbow_sand_06", "fx\\regbow_sand_07", "fx\\regbow_sand_08", "Null", "Null" },
		{ "fx\\ghost_01a", "fx\\ghost_02a", "fx\\ghost_03a", "fx\\ghost_04a", "fx\\ghost_05a", "fx\\ghost_06a", "fx\\ghost_07a", "fx\\ghost_08a", "fx\\ghost_09a", "fx\\ghost_10a" },
		{ "fx\\cauldron_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stir1", "fx\\stir2", "fx\\stir3", "fx\\stir4", "fx\\stir5", "fx\\stir6", "Null", "Null", "Null", "Null" },
		{ "fx\\fireloop1", "fx\\fireloop2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\arrbounce1", "fx\\arrbounce4", "fx\\arrbounce5", "fx\\arrbounce7", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\swclang1", "fx\\swclang2", "fx\\swclang3", "fx\\swcombi1", "fx\\swcombi4", "fx\\swhit1", "fx\\swhit11", "fx\\swhit15", "Null", "Null" },
		{ "fx\\swhit3", "fx\\swhit8", "fx\\swhit9", "fx\\swscrape1", "fx\\swclang4", "fx\\swclang5", "fx\\swcombi8", "fx\\swcombi9", "Null", "Null" },
		{ "fx\\pole_turn1", "fx\\pole_turn2", "fx\\pole_turn3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\pole_grind2", "fx\\pole_grind3", "fx\\pole_grind6", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\moatdig1", "fx\\moatdig2", "fx\\moatdig3", "fx\\moatdig4", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\cbow_01", "fx\\cbow_02", "fx\\cbow_03", "fx\\cbow_04", "fx\\cbow_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\cbowwind_01", "fx\\cbowwind_02", "fx\\cbowwind_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bearattack_1", "fx\\bearattack_2", "fx\\bearattack_3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\beardies_1", "fx\\beardies_2", "fx\\beardies_3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\cow_slaughter", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\milking_1", "fx\\milking_2", "fx\\milking_3", "fx\\milking_4", "fx\\milking_5", "fx\\milking_6", "fx\\milking_7", "Null", "Null", "Null" },
		{ "fx\\cowmoo_1", "fx\\cowmoo_2", "fx\\cowmoo_3", "fx\\cowmoo_4", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\milkpour", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dogdblbark_1", "fx\\dogdblbark_2", "fx\\dogtalk_1", "fx\\dogtalk_4", "fx\\dogsingbark_1", "fx\\dogsingbark_2", "fx\\dogsingbark_3", "fx\\dogtalk_5", "Null", "Null" },
		{ "fx\\dogdies_1", "fx\\dogdies_2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dogpant", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dogtalk_6", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\broom_1", "fx\\broom_2", "fx\\broom_3", "fx\\broom_4", "fx\\broom_5", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\sharpen_sht_1", "fx\\sharpen_med_1", "fx\\sharpen_lng_1", "fx\\sharpen_sht_2", "fx\\sharpen_med_2", "fx\\sharpen_lng_2", "Null", "Null", "Null", "Null" },
		{ "fx\\deerfall_1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\huntercut_01", "fx\\huntercut_02", "fx\\huntercut_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_sing_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_mult_01", "fx\\trot_mult_02", "fx\\trot_mult_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_mult_04", "fx\\trot_mult_05", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\whinny_s_02", "fx\\whinny_m_01", "fx\\whinny_s_03", "fx\\whinny_m_02", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\horsedie_01", "fx\\horsedie_02", "fx\\horsedie_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\cowhitsdust", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\wabbitdies_1", "fx\\wabbitdies_4", "fx\\wabbitdies_5", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\wolfdies_1", "fx\\wolfdies_2", "fx\\wolfdies_3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\wolfattack_1", "fx\\wolfattack_2", "fx\\wolfattack_3", "fx\\wolfattack_4", "fx\\wolfattack_5", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\liondie_01", "fx\\liondie_02", "fx\\liondie_03", "fx\\liondie_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\armourhit_01", "fx\\armourhit_02", "fx\\armourhit_03", "fx\\armourhit_04", "fx\\armourhit_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\burn9", "fx\\burn10", "fx\\burn3", "fx\\burn4", "fx\\burn5", "fx\\burn6", "fx\\burn7", "fx\\burn8", "Null", "Null" },
		{ "fx\\pot_flareup_1", "fx\\pot_flareup_2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\opencldrn_01", "fx\\opencldrn_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\burn1", "fx\\burn2", "fx\\burn3", "fx\\burn4", "fx\\burn5", "fx\\burn6", "fx\\burn7", "fx\\burn8", "Null", "Null" },
		{ "fx\\ignite_oil", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\oildump_1", "fx\\oildump_2", "fx\\oildump_3", "fx\\oildump_4", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\menusl_1", "fx\\menusl_2", "fx\\menusl_3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\siegeroll1", "fx\\siegeroll2", "fx\\siegeroll3", "fx\\siegeroll4", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ca_load1", "fx\\ca_load2", "fx\\ca_load3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ca_fire1", "fx\\ca_fire2", "fx\\ca_fire3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ma_load1", "fx\\ma_load2", "fx\\ma_load3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ma_fire1", "fx\\ma_fire2", "fx\\ma_fire3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\tr_load1", "fx\\tr_load2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\tr_fire1", "fx\\tr_fire2", "fx\\tr_fire3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trebdie_1", "fx\\trebdie_2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\siegedie_1", "fx\\siegedie_2", "fx\\siegedie_3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bighit_01", "fx\\bighit_02", "fx\\bighit_03", "fx\\bighit_04", "fx\\bighit_05", "fx\\bighit_06", "Null", "Null", "Null", "Null" },
		{ "fx\\miss_l_01", "fx\\miss_l_02", "fx\\miss_l_03", "fx\\miss_s_01", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\woodrattle_1", "fx\\woodrattle_2", "fx\\woodrattle_3", "fx\\woodrattle_4", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\clubdth_01", "fx\\clubdth_02", "fx\\clubdth_03", "fx\\clubdth_04", "fx\\clubdth_05", "fx\\clubdth_06", "fx\\clubdth_07", "fx\\clubdth_08", "Null", "Null" },
		{ "fx\\arrwdth_01", "fx\\arrwdth_02", "fx\\arrwdth_03", "fx\\arrwdth_04", "fx\\arrwdth_05", "fx\\arrwdth_06", "fx\\arrwdth_07", "fx\\arrwdth_08", "Null", "Null" },
		{ "fx\\speardth_01", "fx\\speardth_02", "fx\\speardth_03", "fx\\speardth_04", "fx\\speardth_05", "fx\\speardth_06", "fx\\speardth_07", "fx\\speardth_08", "Null", "Null" },
		{ "fx\\swdth_01", "fx\\swdth_02", "fx\\swdth_03", "fx\\swdth_04", "fx\\swdth_05", "fx\\swdth_06", "fx\\swdth_07", "fx\\swdth_08", "Null", "Null" },
		{ "fx\\hit_01", "fx\\hit_02", "fx\\hit_03", "fx\\hit_04", "fx\\hit_05", "fx\\hit_06", "fx\\hit_07", "fx\\hit_08", "Null", "Null" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ignite_pitch", "fx\\ignite_oil", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metpush7", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metpush12", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metpush13", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metpush15", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metpush5", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metpush1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metrollover3a", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metrollover13", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metrollover15", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metrollover2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metrollover4", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metrollover12", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\woodpush2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\woodrollover7", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\begauk", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\chicflap_01", "fx\\chicflap_02", "fx\\chicflap_03", "fx\\chicflap_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\clucking1", "fx\\clucking2", "fx\\clucking3", "fx\\clucking4", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\portdrop1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\portdrop1a", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\portlift1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\portlift1a", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\maypole_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\swish1", "fx\\swish4", "fx\\swish5", "fx\\swish7", "fx\\swish8", "fx\\swish9", "fx\\swish13", "fx\\swish14", "Null", "Null" },
		{ "fx\\shieldrollover", "fx\\shieldrollover", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\portdrop1b", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\arrowstab_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\snort_01", "fx\\snort_02", "fx\\snort_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\towersmash_01", "fx\\towersmash_02", "fx\\towersmash_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\clubdth_09", "fx\\clubdth_10", "fx\\clubdth_11", "fx\\clubdth_12", "fx\\clubdth_13", "fx\\clubdth_14", "fx\\clubdth_15", "fx\\clubdth_16", "Null", "Null" },
		{ "fx\\arrwdth_09", "fx\\arrwdth_10", "fx\\arrwdth_11", "fx\\arrwdth_12", "fx\\arrwdth_13", "fx\\arrwdth_14", "fx\\arrwdth_15", "fx\\arrwdth_16", "Null", "Null" },
		{ "fx\\speardth_09", "fx\\speardth_10", "fx\\speardth_11", "fx\\speardth_12", "fx\\speardth_13", "fx\\speardth_14", "fx\\speardth_15", "fx\\speardth_16", "Null", "Null" },
		{ "fx\\swdth_09", "fx\\swdth_10", "fx\\swdth_11", "fx\\swdth_12", "fx\\swdth_13", "fx\\swdth_14", "fx\\swdth_15", "fx\\swdth_16", "Null", "Null" },
		{ "fx\\hit_09", "fx\\hit_10", "fx\\hit_11", "fx\\hit_12", "fx\\hit_13", "fx\\hit_14", "fx\\hit_15", "fx\\hit_16", "Null", "Null" },
		{ "fx\\hit_17", "fx\\hit_18", "fx\\hit_19", "fx\\hit_20", "fx\\hit_21", "fx\\hit_22", "fx\\hit_23", "fx\\hit_24", "Null", "Null" },
		{ "fx\\hit_25", "fx\\hit_26", "fx\\hit_27", "fx\\hit_28", "fx\\hit_29", "fx\\hit_30", "fx\\hit_31", "fx\\hit_32", "Null", "Null" },
		{ "fx\\tunnel1", "fx\\tunnel2", "fx\\tunnel3", "fx\\tunnel4", "fx\\tunnel5", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\tunnel6", "fx\\tunnel7", "fx\\tunnel8", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\walldrop_01", "fx\\walldrop_02", "fx\\walldrop_03", "fx\\walldrop_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\droplog", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\babycoo_01", "fx\\babycry_01", "fx\\babycoo_02", "fx\\babycry_02", "fx\\babycoo_03", "fx\\babycry_03", "fx\\babyhappy_01", "Null", "Null", "Null" },
		{ "fx\\metrock_01", "fx\\metrock_02", "fx\\metrock_03", "fx\\metrock_04", "fx\\metrock_05", "fx\\metrock_06", "fx\\metrock_07", "fx\\metrock_08", "Null", "Null" },
		{ "fx\\woodhit_01", "fx\\woodhit_02", "fx\\woodhit_03", "fx\\woodhit_04", "fx\\woodhit_05", "fx\\woodhit_06", "fx\\woodhit_07", "fx\\woodhit_08", "Null", "Null" },
		{ "fx\\splatdeath_01", "fx\\splatdeath_02", "fx\\splatdeath_03", "fx\\splatdeath_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\cowsplat_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\deerrun", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\baliscrank_01", "fx\\baliscrank_02", "fx\\baliscrank_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ballistalaunch_01", "fx\\ballistalaunch_02", "fx\\ballistalaunch_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\buildingwreck_01", "fx\\buildingwreck_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\shielddie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\flamearrow_01", "fx\\flamearrow_02", "fx\\flamearrow_03", "fx\\flamearrow_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\oneswdsmanwalk_01", "fx\\oneswdsmanwalk_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\twoswdsmanwalk_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\multswdsmanwalk_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\rocksplash_01", "fx\\rocksplash_02", "fx\\rocksplash_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\swing_01", "fx\\swing_02", "fx\\swing_03", "fx\\swing_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ramhit_01", "fx\\ramhit_02", "fx\\ramhit_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\sheathin_01", "fx\\sheathin_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\sheathout_01", "fx\\sheathout_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\metpush15", "fx\\metpush15", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\girlydie_01", "fx\\girlydie_02", "fx\\girlydie_03", "fx\\girlydie_04", "fx\\girlydie_05", "fx\\girlydie_06", "fx\\girlydie_07", "fx\\girlydie_08", "Null", "Null" },
		{ "fx\\girlyscream_01", "fx\\girlyscream_02", "fx\\girlyscream_03", "fx\\girlyscream_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\arrowbasic_01", "fx\\arrowbasic_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\macebasic_01", "fx\\macebasic_02", "fx\\macebasic_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\pikebasic_01", "fx\\pikebasic_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\spearbasic_01", "fx\\spearbasic_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\swordbasic_01", "fx\\swordbasic_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\flies_01", "fx\\flies_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\harvest_01", "fx\\harvest_02", "fx\\harvest_03", "fx\\harvest_04", "fx\\harvest_05", "fx\\harvest_06", "Null", "Null", "Null", "Null" },
		{ "fx\\hoe_01", "fx\\hoe_02", "fx\\hoe_03", "fx\\hoe_04", "fx\\hoe_05", "fx\\hoe_06", "fx\\hoe_07", "Null", "Null", "Null" },
		{ "fx\\lonewolf_1", "fx\\multwolves_4", "fx\\lonewolf_2", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dogcage", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\oxdeath", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ladder_01", "fx\\ladder_02", "fx\\ladder_03", "fx\\ladder_04", "fx\\ladder_05", "fx\\ladder_06", "Null", "Null", "Null", "Null" },
		{ "fx\\ladderbreak_01", "fx\\ladderbreak_02", "fx\\ladderbreak_03", "fx\\ladderbreak_04", "fx\\ladderbreak_05", "fx\\ladderbreak_06", "Null", "Null", "Null", "Null" },
		{ "fx\\jesterdie_01", "fx\\jesterdie_02", "fx\\jesterdie_03", "fx\\jesterdie_04", "fx\\jesterdie_05", "fx\\jesterdie_06", "fx\\jesterdie_07", "fx\\jesterdie_08", "Null", "Null" },
		{ "fx\\lorddie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\pigdie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\crowmulti_01", "fx\\crowmulti_02", "fx\\crowsingular_01", "fx\\crowsingular_02", "fx\\crowsingular_03", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\gulls_01", "fx\\gulls_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ironrefill", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\flagsmall_01", "fx\\flagsmall_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\flaglarge_01", "fx\\flaglarge_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\snakedie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\wolfdie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\chapel", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\church_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\cathedral_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stretch", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\gallows", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dungeon1", "fx\\dungeon2", "fx\\dungeon3", "fx\\dungeon4", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\gulldive1", "fx\\gulldive2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\gullsurface1", "fx\\gullsurface2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\quickbreath1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\quickbreath2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\liftchair1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dunkchair1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\girlgrunt1", "fx\\girlgrunt2", "fx\\girlgrunt3", "fx\\girlgrunt4", "fx\\girlgrunt5", "fx\\girlgrunt6", "fx\\girlgrunt7", "fx\\girlgrunt8", "Null", "Null" },
		{ "fx\\fireout1", "fx\\fireout2", "fx\\fireout3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\firepop1", "fx\\firepop2", "fx\\firepop3", "fx\\firepop4", "fx\\firepop5", "fx\\firepop6", "fx\\firepop7", "fx\\firepop8", "Null", "Null" },
		{ "fx\\throwwater1", "fx\\throwwater2", "fx\\throwwater3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\burnwitch1", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\screamwitch2", "fx\\screamwitch3", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\lionat_01", "fx\\lionat_02", "fx\\lionat_03", "fx\\lionat_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ghswing_01", "fx\\ghswing_02", "fx\\ghswing_01", "fx\\ghswing_02", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ghcatch_01", "fx\\ghcatch_02", "fx\\ghcatch_03", "fx\\ghcatch_04", "fx\\ghcatch_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ropeslide_01", "fx\\ropeslide_02", "fx\\ropeslide_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\asnland_01", "fx\\asnland_02", "fx\\asnland_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\asnswish_01", "fx\\asnswish_02", "fx\\asnswish_03", "fx\\asnswish_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\slingthrow_01", "fx\\slingthrow_02", "fx\\slingthrow_03", "fx\\slingthrow_04", "fx\\slingthrow_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\slavefire_01", "fx\\slavefire_02", "fx\\slavefire_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\lordswing_01", "fx\\lordswing_02", "fx\\lordswing_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\harch__01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\harch__02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\harch__03", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\firethrow_01", "fx\\firethrow_02", "fx\\firethrow_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\slingdth_01", "fx\\slingdth_02", "fx\\slingdth_03", "fx\\slingdth_04", "fx\\slingdth_05", "fx\\slingdth_06", "fx\\slingdth_07", "fx\\slingdth_08", "Null", "Null" },
		{ "fx\\slinghit", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\axedeath_01", "fx\\axedeath_02", "fx\\axedeath_03", "fx\\axedeath_04", "fx\\axedeath_05", "fx\\axedeath_06", "fx\\axedeath_07", "fx\\axedeath_08", "Null", "Null" },
		{ "fx\\axedeath_03", "fx\\axedeath_04", "fx\\axedeath_05", "fx\\axedeath_06", "fx\\axedeath_07", "fx\\axedeath_08", "fx\\axedeath_09", "fx\\axedeath_10", "Null", "Null" },
		{ "fx\\axehit", "fx\\axehit", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\fireballista_01", "fx\\fireballista_02", "fx\\fireballista_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\decimate_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\camdie_01", "fx\\camdie_02", "fx\\camdie_03", "fx\\camdie_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\hit_33", "fx\\hit_34", "fx\\hit_35", "fx\\hit_36", "fx\\hit_37", "fx\\hit_38", "fx\\hit_39", "fx\\hit_40", "Null", "Null" },
		{ "fx\\hit_41", "fx\\hit_42", "fx\\hit_43", "fx\\hit_44", "fx\\hit_45", "fx\\hit_46", "fx\\hit_47", "fx\\hit_48", "Null", "Null" },
		{ "fx\\hit_49", "fx\\hit_50", "fx\\hit_51", "fx\\hit_52", "fx\\hit_53", "fx\\hit_54", "fx\\hit_55", "fx\\hit_56", "Null", "Null" },
		{ "fx\\hit_57", "fx\\hit_58", "fx\\hit_59", "fx\\hit_60", "fx\\hit_61", "fx\\hit_62", "fx\\hit_63", "fx\\hit_64", "Null", "Null" },
		{ "fx\\armyhorses", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\exitrollover", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dice", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\skirmish master", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\goldplus2k", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\goldplus4k", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\goldplus8k", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\key", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\chicflap_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\armycharge2", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\towersmash_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\building_placement", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\building_placement_small", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\apothecary_explosion", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\miller_short", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\miller_long", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\pick_apple1", "fx\\pick_apple2", "fx\\pick_apple3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\pick_hops1", "fx\\pick_hops2", "fx\\pick_hops3", "fx\\pick_hops4", "fx\\pick_hops5", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\gibbet_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ox_select_01", "fx\\ox_select_02", "fx\\ox_select_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ox_walk_01", "fx\\ox_walk_02", "fx\\ox_walk_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\marketplace_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\siegetower_dock1", "fx\\siegetower_dock2", "fx\\siegetower_dock3", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\xbow_sand_01", "fx\\xbow_sand_02", "fx\\xbow_sand_03", "fx\\xbow_sand_04", "fx\\xbow_sand_05", "fx\\xbow_sand_06", "fx\\xbow_sand_07", "fx\\xbow_sand_08", "Null", "Null" },
		{ "fx\\xbow_inspect_01", "fx\\xbow_inspect_02", "fx\\xbow_inspect_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\xbow_hammerpickup_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\xbow_hammer_tap_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\xbow_hammer_tap_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\xbow_putdown_01", "fx\\xbow_putdown_02", "fx\\xbow_putdown_03", "fx\\xbow_putdown_04", "fx\\xbow_putdown_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dungeon_whip_01", "fx\\dungeon_whip_02", "fx\\dungeon_whip_03", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stocks_click", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\woodwall_placement", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stonewall_placement", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\woodplatform_placement", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stonetower_placement", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\woodrollover3", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\stonebuilding_placement", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\battlehorn", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bed_skirmisher_throwspear_01", "fx\\bed_skirmisher_throwspear_02", "fx\\bed_skirmisher_throwspear_03", "fx\\bed_skirmisher_throwspear_04", "fx\\bed_skirmisher_throwspear_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_single_camlancer_01", "fx\\trot_single_camlancer_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_several_camlancer_01", "fx\\trot_several_camlancer_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_many_camlancer_01", "fx\\trot_many_camlancer_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_single_heavycamel_01", "fx\\trot_single_heavycamel_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_several_heavycamel_01", "fx\\trot_several_heavycamel_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\trot_many_heavycamel_01", "fx\\trot_many_heavycamel_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bed_demolisher_hammerwall_01", "fx\\bed_demolisher_hammerwall_02", "fx\\bed_demolisher_hammerwall_03", "fx\\bed_demolisher_hammerwall_04", "fx\\bed_demolisher_hammerwall_05", "fx\\bed_demolisher_hammerwall_06", "fx\\bed_demolisher_hammerwall_07", "Null", "Null", "Null" },
		{ "fx\\bed_demolisher_shieldstruck_01", "fx\\bed_demolisher_shieldstruck_02", "fx\\bed_demolisher_shieldstruck_03", "fx\\bed_demolisher_shieldstruck_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bed_demolisher_shielddestroyed_01", "fx\\bed_demolisher_shielddestroyed_02", "fx\\bed_demolisher_shielddestroyed_03", "fx\\bed_demolisher_shielddestroyed_04", "fx\\bed_demolisher_shielddestroyed_05", "fx\\bed_demolisher_shielddestroyed_06", "Null", "Null", "Null", "Null" },
		{ "fx\\bed_ambusher_throwpot_01", "fx\\bed_ambusher_throwpot_02", "fx\\bed_ambusher_throwpot_03", "fx\\bed_ambusher_throwpot_04", "fx\\bed_ambusher_throwpot_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bed_ambusher_potlands_01", "fx\\bed_ambusher_potlands_02", "fx\\bed_ambusher_potlands_03", "fx\\bed_ambusher_potlands_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bed_eunich_swordattack_01", "fx\\bed_eunich_swordattack_02", "fx\\bed_eunich_swordattack_03", "fx\\bed_eunich_swordattack_04", "fx\\bed_eunich_swordattack_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bed_eunich_attackwalls_01", "fx\\bed_eunich_attackwalls_02", "fx\\bed_eunich_attackwalls_03", "fx\\bed_eunich_attackwalls_04", "fx\\bed_eunich_attackwalls_05", "fx\\bed_eunich_attackwalls_06", "fx\\bed_eunich_attackwalls_07", "fx\\bed_eunich_attackwalls_08", "fx\\bed_eunich_attackwalls_09", "fx\\bed_eunich_attackwalls_10" },
		{ "fx\\bed_eunich_swordattack_forward_01", "fx\\bed_eunich_swordattack_forward_02", "fx\\bed_eunich_swordattack_forward_03", "fx\\bed_eunich_swordattack_forward_04", "fx\\bed_eunich_swordattack_forward_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\dancing_bear_01", "fx\\dancing_bear_02", "fx\\dancing_bear_03", "fx\\dancing_bear_04", "fx\\dancing_bear_05", "fx\\dancing_bear_06", "null", "Null", "Null", "Null" },
		{ "fx\\croc_click_attack_die_01", "fx\\croc_click_attack_die_02", "fx\\croc_click_attack_die_03", "fx\\croc_click_attack_die_04", "fx\\croc_click_attack_die_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\croc_killbite_01", "fx\\croc_killbite_02", "fx\\croc_killbite_03", "fx\\croc_killbite_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\hyena_idle_click_01", "fx\\hyena_idle_click_02", "fx\\hyena_idle_click_03", "fx\\hyena_idle_click_04", "fx\\hyena_idle_click_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\hyena_attack_01", "fx\\hyena_attack_02", "fx\\hyena_attack_03", "fx\\hyena_attack_04", "fx\\hyena_attack_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\hyena_die_01", "fx\\hyena_die_02", "fx\\hyena_die_03", "fx\\hyena_die_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\goat_idle_click_01", "fx\\goat_idle_click_02", "fx\\goat_idle_click_03", "fx\\goat_idle_click_04", "fx\\goat_idle_click_05", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\goat_die_01", "fx\\goat_die_02", "fx\\goat_die_03", "fx\\goat_die_04", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\mosque_small_01a", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\mosque_medium_01a", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\mosque_large_01a", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\MPJoin", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\MPLeaver", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\Victory_banner", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\Defeat_banner", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\femalelorddie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bessylorddie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\femaleenemylorddie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\bessyenemylorddie_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\extragold_01", "fx\\extragold_02", "fx\\extragold_03", "fx\\extragold_04", "fx\\extragold_05", "fx\\extragold_06", "fx\\extragold_07", "fx\\extragold_08", "fx\\extragold_09", "fx\\extragold_10" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "", "", "", "", "", "", "", "", "", "" }
	};

	public readonly string[,] stronghold_ambient_list = new string[11, 8]
	{
		{ "fx\\wind_short1", "fx\\wind_short2", "fx\\wind_short3", "fx\\wind_short4", "fx\\wind_short5", "Null", "Null", "Null" },
		{ "fx\\gust1 22k", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\ocean_short1", "fx\\ocean_short2", "fx\\ocean_short3", "fx\\ocean_short4", "fx\\ocean_short5", "fx\\ocean_short6", "Null", "Null" },
		{ "fx\\firelp_1", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\waterfalllp_01", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\streamlp_02", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "fx\\birdsloop_01", "fx\\birdsloop_02", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "Null", "Null", "Null", "Null", "Null", "Null", "Null", "Null" },
		{ "", "", "", "", "", "", "", "" }
	};

	public readonly string[] scribeSpeech = new string[434]
	{
		"Food_Double.wav", "Food_Extra.wav", "Food_Falling.wav", "Food_Growing.wav", "Food_Half.wav", "Food_None.wav", "Food_Normal.wav", "Food_Warning1.wav", "Food_Warning2.wav", "Food_Warning3.wav",
		"Food_Warning4.wav", "Food_Warning5.wav", "General_Fear1.wav", "General_Fear10.wav", "General_Fear2.wav", "General_Fear3.wav", "General_Fear4.wav", "General_Fear5.wav", "General_Fear6.wav", "General_Fear7.wav",
		"General_Fear8.wav", "General_Fear9.wav", "General_Loading.wav", "General_Message1.wav", "General_Message10.wav", "General_Message11.wav", "General_Message12.wav", "General_Message13.wav", "General_Message14.wav", "General_Message15.wav",
		"General_Message16.wav", "General_Message17.wav", "General_Message18.wav", "General_Message2.wav", "General_Message3.wav", "General_Message4.wav", "General_Message5.wav", "General_Message6.wav", "General_Message7.wav", "General_Message8.wav",
		"General_Message9.wav", "General_Quitgame.wav", "General_Saving.wav", "General_Startgame.wav", "General_Victory1.wav", "General_Victory2.wav", "General_Victory3.wav", "General_Victory4.wav", "General_Victory5.wav", "General_Victory6.wav",
		"General_Victory7.wav", "General_Victory8.wav", "General_Warning1.wav", "General_Warning10.wav", "General_Warning11.wav", "General_Warning12.wav", "General_Warning13.wav", "General_Warning14.wav", "General_Warning15.wav", "General_Warning16.wav",
		"General_Warning2.wav", "General_Warning3.wav", "General_Warning4.wav", "General_Warning5.wav", "General_Warning6.wav", "General_Warning7.wav", "General_Warning8.wav", "General_Warning9.wav", "Other_Warning1.wav", "Other_Warning10.wav",
		"Other_Warning11.wav", "Other_Warning12.wav", "Other_Warning2.wav", "Other_Warning3.wav", "Other_Warning4.wav", "Other_Warning5.wav", "Other_Warning6.wav", "Other_Warning7.wav", "Other_Warning8.wav", "Other_Warning9.wav",
		"Pig_Attack.wav", "Pig_Defeat.wav", "Placement_Warning1.wav", "Placement_Warning10.wav", "Placement_Warning11.wav", "Placement_Warning12.wav", "Placement_Warning15.wav", "Placement_Warning16.wav", "placement_warning18.wav", "placement_warning19.wav",
		"Placement_Warning2.wav", "placement_warning20.wav", "placement_warning21.wav", "Placement_Warning3.wav", "Placement_Warning4.wav", "Placement_Warning5.wav", "Placement_Warning6.wav", "Placement_Warning8.wav", "Placement_Warning9.wav", "Pop_Emigrate.wav",
		"Pop_Falling.wav", "Pop_Immigrate.wav", "Pop_Popularity1.wav", "Pop_Popularity2.wav", "Pop_Popularity3.wav", "Pop_Popularity4.wav", "Pop_Popularity5.wav", "Pop_Popularity6.wav", "Pop_Popularity7.wav", "Pop_Popularity8.wav",
		"Pop_Rising.wav", "Pop_Stable.wav", "Random_Events1.wav", "Random_Events10.wav", "Random_Events11.wav", "Random_Events12.wav", "Random_Events13.wav", "Random_Events2.wav", "Random_Events3.wav", "Random_Events4.wav",
		"Random_Events5.wav", "Random_Events6.wav", "Random_Events7.wav", "Random_Events9.wav", "Rat_Attack.wav", "Rat_Defeat.wav", "Resource_Need1.wav", "Resource_Need10.wav", "Resource_Need11.wav", "Resource_Need12.wav",
		"Resource_Need13.wav", "Resource_Need14.wav", "Resource_Need15.wav", "Resource_Need16.wav", "Resource_Need17.wav", "Resource_Need18.wav", "Resource_Need19.wav", "Resource_Need2.wav", "Resource_Need20.wav", "Resource_Need21.wav",
		"Resource_Need22.wav", "Resource_Need23.wav", "Resource_Need24.wav", "Resource_Need25.wav", "Resource_Need26.wav", "Resource_Need27.wav", "Resource_Need28.wav", "Resource_Need3.wav", "Resource_Need4.wav", "Resource_Need5.wav",
		"Resource_Need6.wav", "Resource_Need7.wav", "Resource_Need8.wav", "Resource_Need9.wav", "Snake_Attack.wav", "Snake_Defeat.wav", "Space_Warning1.wav", "Space_Warning2.wav", "Space_Warning3.wav", "Space_Warning4.wav",
		"Space_Warning5.wav", "Space_Warning6.wav", "Space_Warning7.wav", "Space_Warning8.wav", "Taxes_Constant.wav", "Taxes_Decrease1.wav", "Taxes_Decrease2.wav", "Taxes_Increase1.wav", "Taxes_Increase2.wav", "Taxes_Rate1.wav",
		"Taxes_Rate2.wav", "Taxes_Rate3.wav", "Taxes_Rate4.wav", "Taxes_Rate5.wav", "Taxes_Rate6.wav", "Taxes_Rate7.wav", "Taxes_Rate8.wav", "Units_Warning1.wav", "Units_Warning2.wav", "Units_Warning3.wav",
		"Wolf_Attack.wav", "Wolf_Defeat.wav", "General_Message119.wav", "General_Message120.wav", "General_Message121.wav", "General_Message122.wav", "General_Message123.wav", "General_Message124.wav", "General_Message125.wav", "General_Message126.wav",
		"General_Message127.wav", "General_Message128.wav", "General_Message129.wav", "General_Message130.wav", "MP_Victory_1.wav", "MP_Victory_2.wav", "MP_Victory_3.wav", "MP_Defeat_1.wav", "MP_Defeat_2.wav", "MP_Defeat_3.wav",
		"MP_Defeat_4.wav", "MP_Defeat_5.wav", "MP_Defeat_6.wav", "Freebuild_Playtime_1.wav", "Freebuild_Playtime_2.wav", "Freebuild_Playtime_3.wav", "Workshop_Publish_1.wav", "Arabian_Attack.wav", "Arabian_Defeat.wav", "Bedouin_Attack.wav",
		"Bedouin_Defeat.wav", "Game_Paused.wav", "Game_Running.wav", "General_Gatehouse.wav", "General_Loading.wav", "General_Message19.wav", "General_Message20.wav", "General_Message21.wav", "General_Message22.wav", "General_Message23.wav",
		"General_Message24.wav", "General_Message25.wav", "General_Message26.wav", "General_Message27.wav", "General_Message28.wav", "General_Message29.wav", "General_Message30.wav", "General_Message31.wav", "General_Message32.wav", "General_Message33.wav",
		"General_Message34.wav", "General_Message35.wav", "General_Message36.wav", "General_Message37.wav", "General_Message38.wav", "General_Message39.wav", "General_Message40.wav", "General_Message41.wav", "General_Message42.wav", "General_Message43.wav",
		"General_Message43.wav", "General_Message44.wav", "General_Message45.wav", "General_Message46.wav", "General_Message47.wav", "General_Message48.wav", "General_Message49.wav", "General_Message50.wav", "General_Message51.wav", "General_Message52.wav",
		"General_Quitgame.wav", "General_Saving.wav", "General_Startgame.wav", "General_Warning17.wav", "General_Warning18.wav", "General_Warning19.wav", "General_Warning20.wav", "General_Warning21.wav", "General_Warning22.wav", "General_Warning23.wav",
		"General_Warning24.wav", "General_Warning25.wav", "General_Warning26.wav", "Genie_01.wav", "Genie_02.wav", "Genie_03.wav", "Genie_04.wav", "Genie_05.wav", "Genie_06.wav", "Genie_07.wav",
		"Genie_08.wav", "Genie_09.wav", "Genie_10.wav", "Genie_11.wav", "Genie_12.wav", "Genie_13.wav", "Genie_14.wav", "Genie_15.wav", "Genie_16.wav", "Genie_17.wav",
		"Genie_18.wav", "Genie_19.wav", "Genie_20.wav", "Genie_21.wav", "Genie_22.wav", "Genie_23.wav", "Genie_24.wav", "Genie_25.wav", "genie_26.wav", "genie_27.wav",
		"genie_28.wav", "genie_29.wav", "genie_30.wav", "genie_31.wav", "genie_32.wav", "genie_33.wav", "genie_34.wav", "genie_35.wav", "genie_36.wav", "genie_37.wav",
		"genie_38.wav", "genie_39.wav", "genie_40.wav", "genie_41.wav", "genie_42.wav", "genie_43.wav", "genie_44.wav", "genie_45.wav", "infidel_Attack.wav", "infidel_Defeat.wav",
		"placement_warning18.wav", "Random_Events14.wav", "Units_warning4.wav", "GenieDE001.wav", "GenieDE002.wav", "GenieDE003.wav", "GenieDE004.wav", "GenieDE005.wav", "GenieDE006.wav", "GenieDE007.wav",
		"GenieDE008.wav", "GenieDE009.wav", "GenieDE010.wav", "GenieDE011.wav", "GenieDE012.wav", "GenieDE013.wav", "GenieDE014.wav", "GenieDE015.wav", "GenieDE016.wav", "GenieDE017.wav",
		"GenieDE018.wav", "GenieDE019.wav", "GenieDE020.wav", "GenieDE021.wav", "GenieDE022.wav", "GenieDE023.wav", "GenieDE024.wav", "GenieDE025.wav", "GenieDE026.wav", "GenieDE027.wav",
		"GenieDE028.wav", "GenieDE029.wav", "GenieDE030.wav", "GenieDE031.wav", "GenieDE032.wav", "GenieDE033.wav", "GenieDE034.wav", "GenieDE035.wav", "GenieDE036.wav", "GenieDE037.wav",
		"GenieDE038.wav", "GenieDE039.wav", "GenieDE040.wav", "GenieDE041.wav", "GenieDE042.wav", "GenieDE043.wav", "GenieDE044.wav", "GenieDE045.wav", "GenieDE046.wav", "GenieDE047.wav",
		"GenieDE048.wav", "GenieDE049.wav", "GenieDE050.wav", "GenieDE051.wav", "GenieDE052.wav", "GenieDE053.wav", "GenieDE054.wav", "GenieDE055.wav", "GenieDE056.wav", "GenieDE057.wav",
		"GenieDE058.wav", "GenieDE059.wav", "GenieDE060.wav", "GenieDE061.wav", "GenieDE062.wav", "GenieDE112.wav", "GenieDE113.wav", "GenieDE114.wav", "GenieDE115.wav", "GenieDE116.wav",
		"GenieDE117.wav", "GenieDE118.wav", "GenieDE119.wav", "GenieDE120.wav", "GenieDE121.wav", "GenieDE122.wav", "GenieDE123.wav", "GenieDE124.wav", "GenieDE125.wav", "GenieDE126.wav",
		"GenieDE127.wav", "GenieDE128.wav", "GenieDE129.wav", "GenieDE130.wav", "GenieDE131.wav", "GenieDE132.wav", "GenieDE133.wav", "GenieDE134.wav", "GenieDE135.wav", "GenieDE136.wav",
		"GenieDE137.wav", "GenieDE138.wav", "GenieDE139.wav", "GenieDE140.wav", "GenieDE141.wav", "GenieDE142.wav", "GenieDE143.wav", "GenieDE144.wav", "GenieDE145.wav", "GenieDE146.wav",
		"GenieDE147.wav", "GenieDE148.wav", "GenieDE149.wav", "GenieDE150.wav", "GenieDE151.wav", "GenieDE152.wav", "GenieDE153.wav", "ally_request01.wav", "ally_request02.wav", "ally_request03.wav",
		"ally_request04.wav", "ally_request05.wav", "ally_request06.wav", "ally_request07.wav", "ally_request08.wav", "ally_request09.wav", "ally_request10.wav", "ally_request11.wav", "ally_request12.wav", "ally_request13.wav",
		"ally_request14.wav", "ally_request15.wav", "ally_request16.wav", "ally_request17.wav"
	};

	public readonly string[] aiSpeech = new string[880]
	{
		"pg_anger_01.wav", "pg_anger_02.wav", "pg_anger_03.wav", "pg_anger_04.wav", "pg_plead_01.wav", "pg_plead_02.wav", "pg_plead_03.wav", "pg_plead_04.wav", "pg_taunt_01.wav", "pg_taunt_02.wav",
		"pg_taunt_03.wav", "pg_taunt_04.wav", "pg_taunt_05.wav", "pg_taunt_06.wav", "pg_taunt_07.wav", "pg_taunt_08.wav", "pg_vict_01.wav", "pg_vict_02.wav", "pg_vict_03.wav", "pg_vict_04.wav",
		"rt_anger_01.wav", "rt_anger_02.wav", "rt_anger_03.wav", "rt_anger_04.wav", "rt_plead_01.wav", "rt_plead_02.wav", "rt_plead_03.wav", "rt_plead_04.wav", "rt_taunt_01.wav", "rt_taunt_02.wav",
		"rt_taunt_03.wav", "rt_taunt_04.wav", "rt_taunt_05.wav", "rt_taunt_06.wav", "rt_taunt_07.wav", "rt_taunt_08.wav", "rt_vict_01.wav", "rt_vict_02.wav", "rt_vict_03.wav", "rt_vict_04.wav",
		"sn_anger_01.wav", "sn_anger_02.wav", "sn_anger_03.wav", "sn_anger_04.wav", "sn_plead_01.wav", "sn_plead_02.wav", "sn_plead_03.wav", "sn_plead_04.wav", "sn_taunt_01.wav", "sn_taunt_02.wav",
		"sn_taunt_03.wav", "sn_taunt_04.wav", "sn_taunt_05.wav", "sn_taunt_06.wav", "sn_taunt_07.wav", "sn_taunt_08.wav", "sn_vict_01.wav", "sn_vict_02.wav", "sn_vict_03.wav", "sn_vict_04.wav",
		"wf_anger_01.wav", "wf_anger_02.wav", "wf_anger_03.wav", "wf_anger_04.wav", "wf_plead_01.wav", "wf_plead_02.wav", "wf_plead_03.wav", "wf_plead_04.wav", "wf_taunt_01.wav", "wf_taunt_02.wav",
		"wf_taunt_03.wav", "wf_taunt_04.wav", "wf_taunt_05.wav", "wf_taunt_06.wav", "wf_taunt_07.wav", "wf_taunt_08.wav", "wf_vict_01.wav", "wf_vict_02.wav", "wf_vict_03.wav", "wf_vict_04.wav",
		"ab_add_player_01.wav", "ab_ally_death_01.wav", "ab_anger_01.wav", "ab_anger_02.wav", "ab_boast_01.wav", "ab_congrats_01.wav", "ab_extra_01.wav", "ab_helpsent_01.wav", "ab_help_01.wav", "ab_kick_player_01.wav",
		"ab_nervous_01.wav", "ab_nervous_02.wav", "ab_noattack_01.wav", "ab_noattack_02.wav", "ab_nohelp_01.wav", "ab_nohelp_02.wav", "ab_notsent_01.wav", "ab_plead_01.wav", "ab_req_01.wav", "ab_sent_01.wav",
		"ab_siege_01.wav", "ab_taunt_01.wav", "ab_taunt_02.wav", "ab_taunt_03.wav", "ab_taunt_04.wav", "ab_team_losing_01.wav", "ab_team_winning_01.wav", "ab_thanks_01.wav", "ab_vict_01.wav", "ab_vict_02.wav",
		"ab_vict_03.wav", "ab_vict_04.wav", "ab_willattack_01.wav", "ca_add_player_01.wav", "ca_ally_death_01.wav", "ca_anger_01.wav", "ca_anger_02.wav", "ca_boast_01.wav", "ca_congrats_01.wav", "ca_extra_01.wav",
		"ca_helpsent_01.wav", "ca_help_01.wav", "ca_kick_player_01.wav", "ca_nervous_01.wav", "ca_nervous_02.wav", "ca_noattack_01.wav", "ca_noattack_02.wav", "ca_nohelp_01.wav", "ca_nohelp_02.wav", "ca_notsent_01.wav",
		"ca_plead_01.wav", "ca_req_01.wav", "ca_sent_01.wav", "ca_siege_01.wav", "ca_taunt_01.wav", "ca_taunt_02.wav", "ca_taunt_03.wav", "ca_taunt_04.wav", "ca_team_losing_01.wav", "ca_team_winning_01.wav",
		"ca_thanks_01.wav", "ca_vict_01.wav", "ca_vict_02.wav", "ca_vict_03.wav", "ca_vict_04.wav", "ca_willattack_01.wav", "em_add_player_01.wav", "em_ally_death_01.wav", "em_anger_01.wav", "em_anger_02.wav",
		"em_boast_01.wav", "em_congrats_01.wav", "em_extra_01.wav", "em_helpsent_01.wav", "em_help_01.wav", "em_kick_player_01.wav", "em_nervous_01.wav", "em_nervous_02.wav", "em_noattack_01.wav", "em_noattack_02.wav",
		"em_nohelp_01.wav", "em_nohelp_02.wav", "em_notsent_01.wav", "em_plead_01.wav", "em_req_01.wav", "em_sent_01.wav", "em_siege_01.wav", "em_taunt_01.wav", "em_taunt_02.wav", "em_taunt_03.wav",
		"em_taunt_04.wav", "em_team_losing_01.wav", "em_team_winning_01.wav", "em_thanks_01.wav", "em_vict_01.wav", "em_vict_02.wav", "em_vict_03.wav", "em_vict_04.wav", "em_willattack_01.wav", "fr_add_player_01.wav",
		"fr_ally_death_01.wav", "fr_anger_01.wav", "fr_anger_02.wav", "fr_boast_01.wav", "fr_congrats_01.wav", "fr_extra_01.wav", "fr_helpsent_01.wav", "fr_help_01.wav", "fr_kick_player_01.wav", "fr_nervous_01.wav",
		"fr_nervous_02.wav", "fr_noattack_01.wav", "fr_noattack_02.wav", "fr_nohelp_01.wav", "fr_nohelp_02.wav", "fr_notsent_01.wav", "fr_plead_01.wav", "fr_req_01.wav", "fr_sent_01.wav", "fr_siege_01.wav",
		"fr_taunt_01.wav", "fr_taunt_02.wav", "fr_taunt_03.wav", "fr_taunt_04.wav", "fr_team_losing_01.wav", "fr_team_winning_01.wav", "fr_thanks_01.wav", "fr_vict_01.wav", "fr_vict_02.wav", "fr_vict_03.wav",
		"fr_vict_04.wav", "fr_willattack_01.wav", "ma_add_player_01.wav", "ma_ally_death_01.wav", "ma_anger_01.wav", "ma_anger_02.wav", "ma_boast_01.wav", "ma_congrats_01.wav", "ma_extra_01.wav", "ma_helpsent_01.wav",
		"ma_help_01.wav", "ma_kick_player_01.wav", "ma_nervous_01.wav", "ma_nervous_02.wav", "ma_noattack_01.wav", "ma_noattack_02.wav", "ma_nohelp_01.wav", "ma_nohelp_02.wav", "ma_notsent_01.wav", "ma_plead_01.wav",
		"ma_req_01.wav", "ma_sent_01.wav", "ma_siege_01.wav", "ma_taunt_01.wav", "ma_taunt_02.wav", "ma_taunt_03.wav", "ma_taunt_04.wav", "ma_team_losing_01.wav", "ma_team_winning_01.wav", "ma_thanks_01.wav",
		"ma_vict_01.wav", "ma_vict_02.wav", "ma_vict_03.wav", "ma_vict_04.wav", "ma_willattack_01.wav", "ni_add_player_01.wav", "ni_ally_death_01.wav", "ni_anger_01.wav", "ni_anger_02.wav", "ni_boast_01.wav",
		"ni_congrats_01.wav", "ni_extra_01.wav", "ni_helpsent_01.wav", "ni_help_01.wav", "ni_kick_player_01.wav", "ni_nervous_01.wav", "ni_nervous_02.wav", "ni_noattack_01.wav", "ni_noattack_02.wav", "ni_nohelp_01.wav",
		"ni_nohelp_02.wav", "ni_notsent_01.wav", "ni_plead_01.wav", "ni_req_01.wav", "ni_sent_01.wav", "ni_siege_01.wav", "ni_taunt_01.wav", "ni_taunt_02.wav", "ni_taunt_03.wav", "ni_taunt_04.wav",
		"ni_team_losing_01.wav", "ni_team_winning_01.wav", "ni_thanks_01.wav", "ni_vict_01.wav", "ni_vict_02.wav", "ni_vict_03.wav", "ni_vict_04.wav", "ni_willattack_01.wav", "pg_add_player.wav", "pg_kick_player.wav",
		"ph_add_player_01.wav", "ph_ally_death_01.wav", "ph_anger_01.wav", "ph_anger_02.wav", "ph_boast_01.wav", "ph_congrats_01.wav", "ph_extra_01.wav", "ph_helpsent_01.wav", "ph_help_01.wav", "ph_kick_player_01.wav",
		"ph_nervous_01.wav", "ph_nervous_02.wav", "ph_noattack_01.wav", "ph_noattack_02.wav", "ph_nohelp_01.wav", "ph_nohelp_02.wav", "ph_notsent_01.wav", "ph_plead_01.wav", "ph_req_01.wav", "ph_sent_01.wav",
		"ph_siege_01.wav", "ph_taunt_01.wav", "ph_taunt_02.wav", "ph_taunt_03.wav", "ph_taunt_04.wav", "ph_team_losing_01.wav", "ph_team_winning_01.wav", "ph_thanks_01.wav", "ph_vict_01.wav", "ph_vict_02.wav",
		"ph_vict_03.wav", "ph_vict_04.wav", "ph_willattack_01.wav", "ri_add_player_01.wav", "ri_ally_death_01.wav", "ri_anger_01.wav", "ri_anger_02.wav", "ri_boast_01.wav", "ri_congrats_01.wav", "ri_extra_01.wav",
		"ri_helpsent_01.wav", "ri_help_01.wav", "ri_kick_player_01.wav", "ri_nervous_01.wav", "ri_nervous_02.wav", "ri_noattack_01.wav", "ri_noattack_02.wav", "ri_nohelp_01.wav", "ri_nohelp_02.wav", "ri_notsent_01.wav",
		"ri_plead_01.wav", "ri_req_01.wav", "ri_sent_01.wav", "ri_siege_01.wav", "ri_taunt_01.wav", "ri_taunt_02.wav", "ri_taunt_03.wav", "ri_taunt_04.wav", "ri_team_losing_01.wav", "ri_team_winning_01.wav",
		"ri_thanks_01.wav", "ri_vict_01.wav", "ri_vict_02.wav", "ri_vict_03.wav", "ri_vict_04.wav", "ri_willattack_01.wav", "rt_add_player.wav", "rt_kick_player.wav", "sa_add_player_01.wav", "sa_ally_death_01.wav",
		"sa_anger_01.wav", "sa_anger_02.wav", "sa_boast_01.wav", "sa_congrats_01.wav", "sa_extra_01.wav", "sa_helpsent_01.wav", "sa_help_01.wav", "sa_kick_player_01.wav", "sa_nervous_01.wav", "sa_nervous_02.wav",
		"sa_noattack_01.wav", "sa_noattack_02.wav", "sa_nohelp_01.wav", "sa_nohelp_02.wav", "sa_notsent_01.wav", "sa_plead_01.wav", "sa_req_01.wav", "sa_sent_01.wav", "sa_siege_01.wav", "sa_taunt_01.wav",
		"sa_taunt_02.wav", "sa_taunt_03.wav", "sa_taunt_04.wav", "sa_team_losing_01.wav", "sa_team_winning_01.wav", "sa_thanks_01.wav", "sa_vict_01.wav", "sa_vict_02.wav", "sa_vict_03.wav", "sa_vict_04.wav",
		"sa_willattack_01.wav", "sh_add_player_01.wav", "sh_ally_death_01.wav", "sh_anger_01.wav", "sh_anger_02.wav", "sh_boast_01.wav", "sh_congrats_01.wav", "sh_extra_01.wav", "sh_helpsent_01.wav", "sh_help_01.wav",
		"sh_kick_player_01.wav", "sh_nervous_01.wav", "sh_nervous_02.wav", "sh_noattack_01.wav", "sh_noattack_02.wav", "sh_nohelp_01.wav", "sh_nohelp_02.wav", "sh_notsent_01.wav", "sh_plead_01.wav", "sh_req_01.wav",
		"sh_sent_01.wav", "sh_siege_01.wav", "sh_taunt_01.wav", "sh_taunt_02.wav", "sh_taunt_03.wav", "sh_taunt_04.wav", "sh_team_losing_01.wav", "sh_team_winning_01.wav", "sh_thanks_01.wav", "sh_vict_01.wav",
		"sh_vict_02.wav", "sh_vict_03.wav", "sh_vict_04.wav", "sh_willattack_01.wav", "sn_add_player.wav", "sn_kick_player.wav", "su_add_player_01.wav", "su_ally_death_01.wav", "su_anger_01.wav", "su_anger_02.wav",
		"su_boast_01.wav", "su_congrats_01.wav", "su_extra_01.wav", "su_helpsent_01.wav", "su_help_01.wav", "su_kick_player_01.wav", "su_nervous_01.wav", "su_nervous_02.wav", "su_noattack_01.wav", "su_noattack_02.wav",
		"su_nohelp_01.wav", "su_nohelp_02.wav", "su_notsent_01.wav", "su_plead_01.wav", "su_req_01.wav", "su_sent_01.wav", "su_siege_01.wav", "su_taunt_01.wav", "su_taunt_02.wav", "su_taunt_03.wav",
		"su_taunt_04.wav", "su_team_losing_01.wav", "su_team_winning_01.wav", "su_thanks_01.wav", "su_vict_01.wav", "su_vict_02.wav", "su_vict_03.wav", "su_vict_04.wav", "su_willattack_01.wav", "wa_add_player_01.wav",
		"wa_ally_death_01.wav", "wa_anger_01.wav", "wa_anger_02.wav", "wa_boast_01.wav", "wa_congrats_01.wav", "wa_extra_01.wav", "wa_helpsent_01.wav", "wa_help_01.wav", "wa_kick_player_01.wav", "wa_nervous_01.wav",
		"wa_nervous_02.wav", "wa_noattack_01.wav", "wa_noattack_02.wav", "wa_nohelp_01.wav", "wa_nohelp_02.wav", "wa_notsent_01.wav", "wa_plead_01.wav", "wa_req_01.wav", "wa_sent_01.wav", "wa_siege_01.wav",
		"wa_taunt_01.wav", "wa_taunt_02.wav", "wa_taunt_03.wav", "wa_taunt_04.wav", "wa_team_losing_01.wav", "wa_team_winning_01.wav", "wa_thanks_01.wav", "wa_vict_01.wav", "wa_vict_02.wav", "wa_vict_03.wav",
		"wa_vict_04.wav", "wa_willattack_01.wav", "wf_add_player.wav", "wf_kick_player.wav", "all_add_player_01.wav", "all_ally_death_01.wav", "all_anger_01.wav", "all_anger_02.wav", "all_boast_01.wav", "all_congrats_01.wav",
		"all_extra_01.wav", "all_helpsent_01.wav", "all_help_01.wav", "all_kick_player_01.wav", "all_nervous_01.wav", "all_nervous_02.wav", "all_noattack_01.wav", "all_noattack_02.wav", "all_nohelp_01.wav", "all_nohelp_02.wav",
		"all_notsent_01.wav", "all_plead_01.wav", "all_req_01.wav", "all_sent_01.wav", "all_siege_01.wav", "all_taunt_01.wav", "all_taunt_02.wav", "all_taunt_03.wav", "all_taunt_04.wav", "all_team_losing_01.wav",
		"all_team_winning_01.wav", "all_thanks_01.wav", "all_vict_01.wav", "all_vict_02.wav", "all_vict_03.wav", "all_vict_04.wav", "all_willattack_01.wav", "je_taunt_01.wav", "se_taunt_01.wav", "no_taunt_01.wav",
		"ka_taunt_01.wav", "cn_taunt_01.wav", "tr_taunt_01.wav", "sg_taunt_01.wav", "li_taunt_01.wav", "cr_taunt_01.wav", "je_taunt_02.wav", "se_taunt_02.wav", "no_taunt_02.wav", "ka_taunt_02.wav",
		"cn_taunt_02.wav", "tr_taunt_02.wav", "sg_taunt_02.wav", "li_taunt_02.wav", "cr_taunt_02.wav", "je_taunt_03.wav", "se_taunt_03.wav", "no_taunt_03.wav", "ka_taunt_03.wav", "cn_taunt_03.wav",
		"tr_taunt_03.wav", "sg_taunt_03.wav", "li_taunt_03.wav", "cr_taunt_03.wav", "je_taunt_04.wav", "se_taunt_04.wav", "no_taunt_04.wav", "ka_taunt_04.wav", "cn_taunt_04.wav", "tr_taunt_04.wav",
		"sg_taunt_04.wav", "li_taunt_04.wav", "cr_taunt_04.wav", "je_anger_01.wav", "se_anger_01.wav", "no_anger_01.wav", "ka_anger_01.wav", "cn_anger_01.wav", "tr_anger_01.wav", "sg_anger_01.wav",
		"li_anger_01.wav", "cr_anger_01.wav", "je_anger_02.wav", "se_anger_02.wav", "no_anger_02.wav", "ka_anger_02.wav", "cn_anger_02.wav", "tr_anger_02.wav", "sg_anger_02.wav", "li_anger_02.wav",
		"cr_anger_02.wav", "je_plead_01.wav", "se_plead_01.wav", "no_plead_01.wav", "ka_plead_01.wav", "cn_plead_01.wav", "tr_plead_01.wav", "sg_plead_01.wav", "li_plead_01.wav", "cr_plead_01.wav",
		"je_nervous_01.wav", "se_nervous_01.wav", "no_nervous_01.wav", "ka_nervous_01.wav", "cn_nervous_01.wav", "tr_nervous_01.wav", "sg_nervous_01.wav", "li_nervous_01.wav", "cr_nervous_01.wav", "je_nervous_02.wav",
		"se_nervous_02.wav", "no_nervous_02.wav", "ka_nervous_02.wav", "cn_nervous_02.wav", "tr_nervous_02.wav", "sg_nervous_02.wav", "li_nervous_02.wav", "cr_nervous_02.wav", "je_vict_01.wav", "se_vict_01.wav",
		"no_vict_01.wav", "ka_vict_01.wav", "cn_vict_01.wav", "tr_vict_01.wav", "sg_vict_01.wav", "li_vict_01.wav", "cr_vict_01.wav", "je_vict_02.wav", "se_vict_02.wav", "no_vict_02.wav",
		"ka_vict_02.wav", "cn_vict_02.wav", "tr_vict_02.wav", "sg_vict_02.wav", "li_vict_02.wav", "cr_vict_02.wav", "je_vict_03.wav", "se_vict_03.wav", "no_vict_03.wav", "ka_vict_03.wav",
		"cn_vict_03.wav", "tr_vict_03.wav", "sg_vict_03.wav", "li_vict_03.wav", "cr_vict_03.wav", "je_vict_04.wav", "se_vict_04.wav", "no_vict_04.wav", "ka_vict_04.wav", "cn_vict_04.wav",
		"tr_vict_04.wav", "sg_vict_04.wav", "li_vict_04.wav", "cr_vict_04.wav", "je_req_01.wav", "se_req_01.wav", "no_req_01.wav", "ka_req_01.wav", "cn_req_01.wav", "tr_req_01.wav",
		"sg_req_01.wav", "li_req_01.wav", "cr_req_01.wav", "je_thanks_01.wav", "se_thanks_01.wav", "no_thanks_01.wav", "ka_thanks_01.wav", "cn_thanks_01.wav", "tr_thanks_01.wav", "sg_thanks_01.wav",
		"li_thanks_01.wav", "cr_thanks_01.wav", "je_ally_death_01.wav", "se_ally_death_01.wav", "no_ally_death_01.wav", "ka_ally_death_01.wav", "cn_ally_death_01.wav", "tr_ally_death_01.wav", "sg_ally_death_01.wav", "li_ally_death_01.wav",
		"cr_ally_death_01.wav", "je_congrats_01.wav", "se_congrats_01.wav", "no_congrats_01.wav", "ka_congrats_01.wav", "cn_congrats_01.wav", "tr_congrats_01.wav", "sg_congrats_01.wav", "li_congrats_01.wav", "cr_congrats_01.wav",
		"je_boast_01.wav", "se_boast_01.wav", "no_boast_01.wav", "ka_boast_01.wav", "cn_boast_01.wav", "tr_boast_01.wav", "sg_boast_01.wav", "li_boast_01.wav", "cr_boast_01.wav", "je_help_01.wav",
		"se_help_01.wav", "no_help_01.wav", "ka_help_01.wav", "cn_help_01.wav", "tr_help_01.wav", "sg_help_01.wav", "li_help_01.wav", "cr_help_01.wav", "je_extra_01.wav", "se_extra_01.wav",
		"no_extra_01.wav", "ka_extra_01.wav", "cn_extra_01.wav", "tr_extra_01.wav", "sg_extra_01.wav", "li_extra_01.wav", "cr_extra_01.wav", "je_kick_player_01.wav", "se_kick_player_01.wav", "no_kick_player_01.wav",
		"ka_kick_player_01.wav", "cn_kick_player_01.wav", "tr_kick_player_01.wav", "sg_kick_player_01.wav", "li_kick_player_01.wav", "cr_kick_player_01.wav", "je_add_player_01.wav", "se_add_player_01.wav", "no_add_player_01.wav", "ka_add_player_01.wav",
		"cn_add_player_01.wav", "tr_add_player_01.wav", "sg_add_player_01.wav", "li_add_player_01.wav", "cr_add_player_01.wav", "je_siege_01.wav", "se_siege_01.wav", "no_siege_01.wav", "ka_siege_01.wav", "cn_siege_01.wav",
		"tr_siege_01.wav", "sg_siege_01.wav", "li_siege_01.wav", "cr_siege_01.wav", "je_noattack_01.wav", "se_noattack_01.wav", "no_noattack_01.wav", "ka_noattack_01.wav", "cn_noattack_01.wav", "tr_noattack_01.wav",
		"sg_noattack_01.wav", "li_noattack_01.wav", "cr_noattack_01.wav", "je_noattack_02.wav", "se_noattack_02.wav", "no_noattack_02.wav", "ka_noattack_02.wav", "cn_noattack_02.wav", "tr_noattack_02.wav", "sg_noattack_02.wav",
		"li_noattack_02.wav", "cr_noattack_02.wav", "je_nohelp_01.wav", "se_nohelp_01.wav", "no_nohelp_01.wav", "ka_nohelp_01.wav", "cn_nohelp_01.wav", "tr_nohelp_01.wav", "sg_nohelp_01.wav", "li_nohelp_01.wav",
		"cr_nohelp_01.wav", "je_nohelp_02.wav", "se_nohelp_02.wav", "no_nohelp_02.wav", "ka_nohelp_02.wav", "cn_nohelp_02.wav", "tr_nohelp_02.wav", "sg_nohelp_02.wav", "li_nohelp_02.wav", "cr_nohelp_02.wav",
		"je_notsent_01.wav", "se_notsent_01.wav", "no_notsent_01.wav", "ka_notsent_01.wav", "cn_notsent_01.wav", "tr_notsent_01.wav", "sg_notsent_01.wav", "li_notsent_01.wav", "cr_notsent_01.wav", "je_sent_01.wav",
		"se_sent_01.wav", "no_sent_01.wav", "ka_sent_01.wav", "cn_sent_01.wav", "tr_sent_01.wav", "sg_sent_01.wav", "li_sent_01.wav", "cr_sent_01.wav", "je_team_winning_01.wav", "se_team_winning_01.wav",
		"no_team_winning_01.wav", "ka_team_winning_01.wav", "cn_team_winning_01.wav", "tr_team_winning_01.wav", "sg_team_winning_01.wav", "li_team_winning_01.wav", "cr_team_winning_01.wav", "je_team_losing_01.wav", "se_team_losing_01.wav", "no_team_losing_01.wav",
		"ka_team_losing_01.wav", "cn_team_losing_01.wav", "tr_team_losing_01.wav", "sg_team_losing_01.wav", "li_team_losing_01.wav", "cr_team_losing_01.wav", "je_helpsent_01.wav", "se_helpsent_01.wav", "no_helpsent_01.wav", "ka_helpsent_01.wav",
		"cn_helpsent_01.wav", "tr_helpsent_01.wav", "sg_helpsent_01.wav", "li_helpsent_01.wav", "cr_helpsent_01.wav", "je_willattack_01.wav", "se_willattack_01.wav", "no_willattack_01.wav", "ka_willattack_01.wav", "cn_willattack_01.wav",
		"tr_willattack_01.wav", "sg_willattack_01.wav", "li_willattack_01.wav", "cr_willattack_01.wav", "ba_taunt_01.wav", "bu_taunt_01.wav", "ba_taunt_02.wav", "bu_taunt_02.wav", "ba_taunt_03.wav", "bu_taunt_03.wav",
		"ba_taunt_04.wav", "bu_taunt_04.wav", "ba_anger_01.wav", "bu_anger_01.wav", "ba_anger_02.wav", "bu_anger_02.wav", "ba_plead_01.wav", "bu_plead_01.wav", "ba_nervous_01.wav", "bu_nervous_01.wav",
		"ba_nervous_02.wav", "bu_nervous_02.wav", "ba_vict_01.wav", "bu_vict_01.wav", "ba_vict_02.wav", "bu_vict_02.wav", "ba_vict_03.wav", "bu_vict_03.wav", "ba_vict_04.wav", "bu_vict_04.wav",
		"ba_req_01.wav", "bu_req_01.wav", "ba_thanks_01.wav", "bu_thanks_01.wav", "ba_ally_death_01.wav", "bu_ally_death_01.wav", "ba_congrats_01.wav", "bu_congrats_01.wav", "ba_boast_01.wav", "bu_boast_01.wav",
		"ba_help_01.wav", "bu_help_01.wav", "ba_extra_01.wav", "bu_extra_01.wav", "ba_kick_player_01.wav", "bu_kick_player_01.wav", "ba_add_player_01.wav", "bu_add_player_01.wav", "ba_siege_01.wav", "bu_siege_01.wav",
		"ba_noattack_01.wav", "bu_noattack_01.wav", "ba_noattack_02.wav", "bu_noattack_02.wav", "ba_nohelp_01.wav", "bu_nohelp_01.wav", "ba_nohelp_02.wav", "bu_nohelp_02.wav", "ba_notsent_01.wav", "bu_notsent_01.wav",
		"ba_sent_01.wav", "bu_sent_01.wav", "ba_team_winning_01.wav", "bu_team_winning_01.wav", "ba_team_losing_01.wav", "bu_team_losing_01.wav", "ba_helpsent_01.wav", "bu_helpsent_01.wav", "ba_willattack_01.wav", "bu_willattack_01.wav"
	};

	public readonly string[] inMissionSpeech = new string[46]
	{
		"ap_milit21.wav", "ap_milit22.wav", "ap_milit23.wav", "ap_milit24.wav", "ap_milit25.wav", "ap_milit26.wav", "ap_milit27.wav", "ap_milit28.wav", "ap_milit29.wav", "ap_milit30.wav",
		"ap_milit31.wav", "ap_milit32.wav", "ap_milit33.wav", "ap_milit34.wav", "ap_milit35.wav", "ap_milit36.wav", "ap_milit37.wav", "ap_milit38.wav", "ap_milit39.wav", "ap_milit40.wav",
		"ap_milit41.wav", "ap_milit42.wav", "enemy_attack17.wav", "enemy_attack18.wav", "enemy_attack19.wav", "enemy_attack20.wav", "enemy_attack21.wav", "enemy_attack22.wav", "enemy_attack_23.wav", "enemy_attack_24.wav",
		"enemy_attack25.wav", "enemy_attack26.wav", "enemy_attack27.wav", "enemy_attack28.wav", "enemy_attack29.wav", "enemy_attack30.wav", "enemy_attack31.wav", "enemy_attack32.wav", "enemy_attack33.wav", "enemy_attack34.wav",
		"enemy_attack35.wav", "enemy_attack36.wav", "enemy_attack37.wav", "enemy_attack38.wav", "enemy_attack39.wav", "enemy_attack40.wav"
	};

	public readonly string[] insultSpeech = new string[20]
	{
		"Insult1.wav", "Insult2.wav", "Insult3.wav", "Insult4.wav", "Insult5.wav", "insult6.wav", "insult7.wav", "insult8.wav", "insult9.wav", "insult10.wav",
		"Insult11.wav", "Insult12.wav", "Insult13.wav", "Insult14.wav", "Insult15.wav", "Insult16.wav", "Insult17.wav", "Insult18.wav", "Insult19.wav", "Insult20.wav"
	};

	public readonly string[] stronghold_names_speech_list = new string[660]
	{
		"Allison", "fx\\speech\\Name1.wav", "Andrea", "fx\\speech\\Name3.wav", "Annabelle", "fx\\speech\\Name5.wav", "Anna", "fx\\speech\\Name4.wav", "Anne Marie", "fx\\speech\\Name7.wav",
		"Anne", "fx\\speech\\Name6.wav", "Beth", "fx\\speech\\Name9.wav", "Betty", "fx\\speech\\Name10.wav", "Bonnie", "fx\\speech\\Name12.wav", "Camille", "fx\\speech\\Name13.wav",
		"Cindy", "fx\\speech\\Name14.wav", "Collette", "fx\\speech\\Name15.wav", "Darlene", "fx\\speech\\Name16.wav", "Dianne", "fx\\speech\\Name17.wav", "Elizabeth", "fx\\speech\\Name19.wav",
		"Ellen", "fx\\speech\\Name20.wav", "Emma", "fx\\speech\\Name21.wav", "Gabriel", "fx\\speech\\Name22.wav", "Heather", "fx\\speech\\Name23.wav", "Heidi", "fx\\speech\\Name24.wav",
		"Helen", "fx\\speech\\Name25.wav", "Jennifer", "fx\\speech\\Name28.wav", "Jessica", "fx\\speech\\Name29.wav", "Julie", "fx\\speech\\Name30.wav", "Kate", "fx\\speech\\Name31.wav",
		"Kathleen", "fx\\speech\\Name32.wav", "Mckanzie", "fx\\speech\\Name33.wav", "Megan", "fx\\speech\\Name34.wav", "Mellissa", "fx\\speech\\Name35.wav", "Nicole", "fx\\speech\\Name36.wav",
		"Patricia", "fx\\speech\\Name38.wav", "Rachael", "fx\\speech\\Name39.wav", "Rhian", "fx\\speech\\Name40.wav", "Sally", "fx\\speech\\Name41.wav", "Sarah", "fx\\speech\\Name42.wav",
		"Susan", "fx\\speech\\Name43.wav", "Tricia", "fx\\speech\\Name44.wav", "Aaron", "fx\\speech\\Name45.wav", "Andrew", "fx\\speech\\Name47.wav", "Andy", "fx\\speech\\Name48.wav",
		"Anthony", "fx\\speech\\Name49.wav", "Bill", "fx\\speech\\Name51.wav", "Brian", "fx\\speech\\Name52.wav", "Bruce", "fx\\speech\\Name53.wav", "Casimir", "fx\\speech\\Name55.wav",
		"Charles", "fx\\speech\\Name56.wav", "Charlie", "fx\\speech\\Name57.wav", "Christoff", "fx\\speech\\Name59.wav", "Christoph", "fx\\speech\\Name60.wav", "Chris", "fx\\speech\\Name58.wav",
		"Claude", "fx\\speech\\Name61.wav", "Cliff", "fx\\speech\\Name62.wav", "Collin", "fx\\speech\\Name63.wav", "Darren", "fx\\speech\\Name64.wav", "Darrin", "fx\\speech\\Name65.wav",
		"Dave", "fx\\speech\\Name66.wav", "David", "fx\\speech\\Name67.wav", "Denby", "fx\\speech\\Name68.wav", "Dennis", "fx\\speech\\Name69.wav", "Doug", "fx\\speech\\Name71.wav",
		"Earl", "fx\\speech\\Name72.wav", "Emmanuel", "fx\\speech\\Name73.wav", "Eric", "fx\\speech\\Name74.wav", "Family", "fx\\speech\\Name75.wav", "FireFly", "fx\\speech\\Name76.wav",
		"Friendly", "fx\\speech\\Name77.wav", "Geoff", "fx\\speech\\Name78.wav", "Gerry", "fx\\speech\\Name79.wav", "Grady", "fx\\speech\\Name80.wav", "Grant", "fx\\speech\\Name81.wav",
		"Greg", "fx\\speech\\Name82.wav", "Harry", "fx\\speech\\Name83.wav", "Heiko", "fx\\speech\\Name84.wav", "Jack", "fx\\speech\\Name86.wav", "James", "fx\\speech\\Name87.wav",
		"Jamie", "fx\\speech\\Name88.wav", "Jason", "fx\\speech\\Name89.wav", "Jeff", "fx\\speech\\Name90.wav", "Jimmy", "fx\\speech\\Name91.wav", "Joanna", "fx\\speech\\Name92.wav",
		"John", "fx\\speech\\Name93.wav", "Joost", "fx\\speech\\Name95.wav", "Jorge", "fx\\speech\\Name96.wav", "Josh", "fx\\speech\\Name97.wav", "Julian", "fx\\speech\\Name98.wav",
		"Keith", "fx\\speech\\Name99.wav", "Kelly", "fx\\speech\\Name100.wav", "Kevin", "fx\\speech\\Name102.wav", "Louie", "fx\\speech\\Name106.wav", "Luke", "fx\\speech\\Name107.wav",
		"Marc", "fx\\speech\\Name108.wav", "Markus", "fx\\speech\\Name110.wav", "Mark", "fx\\speech\\Name109.wav", "Matthias", "fx\\speech\\Name112.wav", "Matt", "fx\\speech\\Name111.wav",
		"Maurizio", "fx\\speech\\Name113.wav", "Michael", "fx\\speech\\Name114.wav", "Mike", "fx\\speech\\Name115.wav", "Nathan", "fx\\speech\\Name116.wav", "Neal", "fx\\speech\\Name117.wav",
		"Neil", "fx\\speech\\Name118.wav", "Nick", "fx\\speech\\Name119.wav", "of the flies", "fx\\speech\\Name120.wav", "Paolo", "fx\\speech\\Name121.wav", "Patrick", "fx\\speech\\Name122.wav",
		"Paul", "fx\\speech\\Name123.wav", "Peter", "fx\\speech\\Name125.wav", "Pete", "fx\\speech\\Name124.wav", "Phil", "fx\\speech\\Name126.wav", "Richard", "fx\\speech\\Name128.wav",
		"Robb", "fx\\speech\\Name130.wav", "Robert", "fx\\speech\\Name131.wav", "Robin", "fx\\speech\\Name132.wav", "Roland", "fx\\speech\\Name133.wav", "Sajjad", "fx\\speech\\Name134.wav",
		"Scott", "fx\\speech\\Name135.wav", "Sean", "fx\\speech\\Name136.wav", "Seth", "fx\\speech\\Name137.wav", "Simon", "fx\\speech\\Name138.wav", "Smitty", "fx\\speech\\Name139.wav",
		"Stephane", "fx\\speech\\Name140.wav", "Steven", "fx\\speech\\Name142.wav", "Steve", "fx\\speech\\Name141.wav", "Stuart", "fx\\speech\\Name144.wav", "Terry", "fx\\speech\\Name145.wav",
		"Thierry", "fx\\speech\\Name146.wav", "Thomas", "fx\\speech\\Name147.wav", "Wayne", "fx\\speech\\Name150.wav", "Youenn", "fx\\speech\\Name151.wav", "Megadeath", "fx\\speech\\Name152.wav",
		"Megalord", "fx\\speech\\Name153.wav", "Super Noodle", "fx\\speech\\Name154.wav", "Hayden", "fx\\speech\\Name201.wav", "Adelin", "fx\\speech\\Name202.wav", "Alessio", "fx\\speech\\Name203.wav",
		"Andreas", "fx\\speech\\Name204.wav", "Cristian", "fx\\speech\\Name205.wav", "Esplendido", "fx\\speech\\Name206.wav", "LoFiHeart", "fx\\speech\\Name207.wav", "CaptSkubba", "fx\\speech\\Name209.wav",
		"Debbie", "fx\\speech\\Name210.wav", "neph", "fx\\speech\\Name211.wav", "Laurie", "fx\\speech\\Name212.wav", "Leo", "fx\\speech\\Name213.wav", "Mateusz", "fx\\speech\\Name214.wav",
		"Palanion", "fx\\speech\\Name215.wav", "Meredith", "fx\\speech\\Name216.wav", "sudouken", "fx\\speech\\Name217.wav", "Natasha", "fx\\speech\\Name218.wav", "Caroline", "fx\\speech\\Name219.wav",
		"Benzie", "fx\\speech\\Name220.wav", "FireFlyNick", "fx\\speech\\Name221.wav", "Nikolay", "fx\\speech\\Name222.wav", "Lordy McLordface", "fx\\speech\\Name223.wav", "Sam", "fx\\speech\\Name224.wav",
		"Sophie", "fx\\speech\\Name225.wav", "Gruber", "fx\\speech\\Name226.wav", "Stephen", "fx\\speech\\Name227.wav", "logarhythm", "fx\\speech\\Name228.wav", "GamerZakh", "fx\\speech\\Name229.wav",
		"Zade", "fx\\speech\\Name230.wav", "Lionheartx10", "fx\\speech\\Name231.wav", "Sergiu", "fx\\speech\\Name232.wav", "Raptor", "fx\\speech\\Name233.wav", "Pixelated Apollo", "fx\\speech\\Name234.wav",
		"Udwin", "fx\\speech\\Name235.wav", "Lutel", "fx\\speech\\Name236.wav", "RIMPAC", "fx\\speech\\Name237.wav", "RTS Kurga", "fx\\speech\\Name238.wav", "Jefflenious", "fx\\speech\\Name239.wav",
		"Nookrium", "fx\\speech\\Name240.wav", "RobDiesALot", "fx\\speech\\Name241.wav", "El Escoces gamer", "fx\\speech\\Name242.wav", "Koinsky", "fx\\speech\\Name243.wav", "hugothester", "fx\\speech\\Name244.wav",
		"DrProof", "fx\\speech\\Name245.wav", "HandOfBlood", "fx\\speech\\Name246.wav", "Dryante Zan", "fx\\speech\\Name247.wav", "Beasty", "fx\\speech\\Name248.wav", "LoafCat", "fx\\speech\\Name249.wav",
		"Lorrdy", "fx\\speech\\Name250.wav", "Lurker", "fx\\speech\\Name251.wav", "Kure", "fx\\speech\\Name252.wav", "Jack", "fx\\speech\\Name253.wav", "Sandrobandito", "fx\\speech\\Name255.wav",
		"Gregg", "fx\\speech\\Name256.wav", "Jay", "fx\\speech\\Name257.wav", "Christopher", "fx\\speech\\Name258.wav", "Charlotte", "fx\\speech\\Name259.wav", "Graeme", "fx\\speech\\Name260.wav",
		"Nigel", "fx\\speech\\Name261.wav", "Abby", "fx\\speech\\Name262.wav", "Hazel", "fx\\speech\\Name263.wav", "Robbie", "fx\\speech\\Name264.wav", "Daniel", "fx\\speech\\Name265.wav",
		"Ardiana", "fx\\speech\\Name266.wav", "Amulya", "fx\\speech\\Name267.wav", "Clara", "fx\\speech\\Name268.wav", "Vieko", "fx\\speech\\Name270.wav", "Pavandeep", "fx\\speech\\Name271.wav",
		"Juan", "fx\\speech\\Name272.wav", "Eli", "fx\\speech\\Name274.wav", "Adoné", "fx\\speech\\Name275.wav", "JT", "fx\\speech\\Name276.wav", "Bridie", "fx\\speech\\Name277.wav",
		"Dilip", "fx\\speech\\Name278.wav", "Viraj", "fx\\speech\\Name279.wav", "Reese", "fx\\speech\\Name280.wav", "Danis", "fx\\speech\\Name281.wav", "Barnaby", "fx\\speech\\Name282.wav",
		"Jess", "fx\\speech\\Name283.wav", "Kavan", "fx\\speech\\Name284.wav", "Oliver", "fx\\speech\\Name287.wav", "Amanda", "fx\\speech\\Name288.wav", "Jared", "fx\\speech\\Name289.wav",
		"George", "fx\\speech\\Name290.wav", "Evan", "fx\\speech\\Name291.wav", "Lincoln", "fx\\speech\\Name292.wav", "Tena", "fx\\speech\\Name293.wav", "Shona", "fx\\speech\\Name294.wav",
		"Zachary", "fx\\speech\\Name295.wav", "Marcus", "fx\\speech\\Name296.wav", "Rodrigo", "fx\\speech\\Name297.wav", "Shiva", "fx\\speech\\Name298.wav", "Frederic", "fx\\speech\\Name299.wav",
		"Pawel", "fx\\speech\\Name300.wav", "Josip", "fx\\speech\\Name301.wav", "Joshua", "fx\\speech\\Name302.wav", "Lilac", "fx\\speech\\Name303.wav", "Douglas", "fx\\speech\\Name304.wav",
		"Lynette", "fx\\speech\\Name306.wav", "Christer", "fx\\speech\\Name307.wav", "Janet", "fx\\speech\\Name308.wav", "Daz", "fx\\speech\\Name208.wav", "Leo", "fx\\speech\\Name305.wav",
		"Fee", "fx\\speech\\Name285.wav", "Tom", "fx\\speech\\Name148.wav", "Tim", "fx\\speech\\Name149.wav", "Stu", "fx\\speech\\Name143.wav", "Rob", "fx\\speech\\Name129.wav",
		"Ray", "fx\\speech\\Name127.wav", "Lou", "fx\\speech\\Name105.wav", "Lee", "fx\\speech\\Name104.wav", "Jon", "fx\\speech\\Name94.wav", "Kit", "fx\\speech\\Name103.wav",
		"Ken", "fx\\speech\\Name101.wav", "Ian", "fx\\speech\\Name85.wav", "Don", "fx\\speech\\Name70.wav", "Cas", "fx\\speech\\Name54.wav", "Ben", "fx\\speech\\Name50.wav",
		"Bev", "fx\\speech\\Name11.wav", "Ava", "fx\\speech\\Name8.wav", "Amy", "fx\\speech\\Name2.wav", "Dot", "fx\\speech\\Name18.wav", "Ivy", "fx\\speech\\Name26.wav",
		"Jen", "fx\\speech\\Name27.wav", "Pat", "fx\\speech\\Name37.wav", "Al", "fx\\speech\\Name46.wav", "Jo", "fx\\speech\\Name286.wav", "JR", "fx\\speech\\Name269.wav",
		"JM", "fx\\speech\\Name273.wav", "Alex", "fx\\speech\\Name155.wav", "Lewis", "fx\\speech\\Name156.wav", "Denise", "fx\\speech\\Name157.wav", "Don", "fx\\speech\\Name158.wav",
		"Ed", "fx\\speech\\Name159.wav", "Francis", "fx\\speech\\Name160.wav", "Sindre", "fx\\speech\\Name161.wav", "Tina", "fx\\speech\\Name162.wav", "Cheryl", "fx\\speech\\Name163.wav",
		"Ville", "fx\\speech\\Name164.wav", "Triblade", "fx\\speech\\Name165.wav", "Draco", "fx\\speech\\Name166.wav", "Zen", "fx\\speech\\Name167.wav", "Jayhawk", "fx\\speech\\Name168.wav",
		"Kenneth", "fx\\speech\\Name169.wav", "Matthew", "fx\\speech\\Name170.wav", "Vernon", "fx\\speech\\Name171.wav", "Tina2", "fx\\speech\\Name172.wav", "Maria", "fx\\speech\\Name173.wav",
		"Barbara", "fx\\speech\\Name174.wav", "Triface", "fx\\speech\\Name175.wav", "Stark", "fx\\speech\\Name176.wav", "Captain Random", "fx\\speech\\Name177.wav", "Isaac", "fx\\speech\\Name178.wav",
		"William", "fx\\speech\\Name179.wav", "Nathan", "fx\\speech\\Name180.wav", "Ryan", "fx\\speech\\Name181.wav", "Tigger", "fx\\speech\\Name182.wav", "Dwee", "fx\\speech\\Name183.wav",
		"Vader", "fx\\speech\\Name184.wav", "Tas", "fx\\speech\\Name185.wav", "Joel", "fx\\speech\\Name186.wav", "Wolverine", "fx\\speech\\Name187.wav", "Rocklar", "fx\\speech\\Name188.wav",
		"Fantasia", "fx\\speech\\Name189.wav", "id", "fx\\speech\\Name190.wav", "Hades", "fx\\speech\\Name191.wav", "Wibble", "fx\\speech\\Name192.wav", "Wraith", "fx\\speech\\Name193.wav",
		"Spiderman", "fx\\speech\\Name194.wav", "Vesper", "fx\\speech\\Name195.wav", "Ztolk", "fx\\speech\\Name196.wav", "Tris", "fx\\speech\\Name197.wav", "Ken", "fx\\speech\\Name198.wav",
		"deRusett", "fx\\speech\\Name199.wav", "Randall", "fx\\speech\\Name200.wav", "Carl", "fx\\speech\\Name401.wav", "Carlo", "fx\\speech\\Name402.wav", "SpineMan", "fx\\speech\\Name403.wav",
		"Thunder", "fx\\speech\\Name404.wav", "Tony", "fx\\speech\\Name405.wav", "Sandy", "fx\\speech\\Name406.wav", "Merepatra", "fx\\speech\\Name407.wav", "Gordon", "fx\\speech\\Name408.wav",
		"Bob", "fx\\speech\\Name409.wav", "Deacon", "fx\\speech\\Name410.wav", "thurdl01", "fx\\speech\\Name411.wav", "yoshi", "fx\\speech\\Name412.wav", "Gabriel", "fx\\speech\\Name413.wav",
		"Thurston", "fx\\speech\\Name414.wav", "Fatal Exception", "fx\\speech\\Name415.wav", "Flying Poo", "fx\\speech\\Name416.wav", "Computer Gaming World", "fx\\speech\\Name417.wav", "Gamestar", "fx\\speech\\Name418.wav",
		"PC Gamer", "fx\\speech\\Name419.wav", "Computer Games Magazine", "fx\\speech\\Name420.wav", "PC Zone", "fx\\speech\\Name421.wav", "Strategy Player", "fx\\speech\\Name422.wav", "PC Format", "fx\\speech\\Name423.wav"
	};

	public readonly string[] nameSpeech = new string[331]
	{
		"name1.wav", "name10.wav", "name100.wav", "name101.wav", "name102.wav", "name103.wav", "name104.wav", "name105.wav", "name106.wav", "name107.wav",
		"name108.wav", "name109.wav", "name11.wav", "name110.wav", "name111.wav", "name112.wav", "name113.wav", "name114.wav", "name115.wav", "name116.wav",
		"name117.wav", "name118.wav", "name119.wav", "name12.wav", "name120.wav", "name121.wav", "name122.wav", "name123.wav", "name124.wav", "name125.wav",
		"name126.wav", "name127.wav", "name128.wav", "name129.wav", "name13.wav", "name130.wav", "name131.wav", "name132.wav", "name133.wav", "name134.wav",
		"name135.wav", "name136.wav", "name137.wav", "name138.wav", "name139.wav", "name14.wav", "name140.wav", "name141.wav", "name142.wav", "name143.wav",
		"name144.wav", "name145.wav", "name146.wav", "name147.wav", "name148.wav", "name149.wav", "name15.wav", "name150.wav", "name151.wav", "name152.wav",
		"name153.wav", "name154.wav", "name16.wav", "name17.wav", "name18.wav", "name19.wav", "name2.wav", "name20.wav", "name21.wav", "name22.wav",
		"name23.wav", "name24.wav", "name25.wav", "name26.wav", "name27.wav", "name28.wav", "name29.wav", "name3.wav", "name30.wav", "name31.wav",
		"name32.wav", "name33.wav", "name34.wav", "name35.wav", "name36.wav", "name37.wav", "name38.wav", "name39.wav", "name4.wav", "name40.wav",
		"name41.wav", "name42.wav", "name43.wav", "name44.wav", "name45.wav", "name46.wav", "name47.wav", "name48.wav", "name49.wav", "name5.wav",
		"name50.wav", "name51.wav", "name52.wav", "name53.wav", "name54.wav", "name55.wav", "name56.wav", "name57.wav", "name58.wav", "name59.wav",
		"name6.wav", "name60.wav", "name61.wav", "name62.wav", "name63.wav", "name64.wav", "name65.wav", "name66.wav", "name67.wav", "name68.wav",
		"name69.wav", "name7.wav", "name70.wav", "name71.wav", "name72.wav", "name73.wav", "name74.wav", "name75.wav", "name76.wav", "name77.wav",
		"name78.wav", "name79.wav", "name8.wav", "name80.wav", "name81.wav", "name82.wav", "name83.wav", "name84.wav", "name85.wav", "name86.wav",
		"name87.wav", "name88.wav", "name89.wav", "name9.wav", "name90.wav", "name91.wav", "name92.wav", "name93.wav", "name94.wav", "name95.wav",
		"name96.wav", "name97.wav", "name98.wav", "name99.wav", "name201.wav", "name202.wav", "name203.wav", "name204.wav", "name205.wav", "name206.wav",
		"name207.wav", "name208.wav", "name209.wav", "name210.wav", "name211.wav", "name212.wav", "name213.wav", "name214.wav", "name215.wav", "name216.wav",
		"name217.wav", "name218.wav", "name219.wav", "name220.wav", "name221.wav", "name222.wav", "name223.wav", "name224.wav", "name225.wav", "name226.wav",
		"name227.wav", "name228.wav", "name229.wav", "name230.wav", "name231.wav", "name232.wav", "name233.wav", "name234.wav", "name235.wav", "name236.wav",
		"name237.wav", "name238.wav", "name239.wav", "name240.wav", "name241.wav", "name242.wav", "name243.wav", "name244.wav", "name245.wav", "name246.wav",
		"name247.wav", "name248.wav", "name249.wav", "name250.wav", "name251.wav", "name252.wav", "name253.wav", "name254.wav", "name255.wav", "name256.wav",
		"name257.wav", "name258.wav", "name259.wav", "name260.wav", "name261.wav", "name262.wav", "name263.wav", "name264.wav", "name265.wav", "name266.wav",
		"name267.wav", "name268.wav", "name269.wav", "name270.wav", "name271.wav", "name272.wav", "name273.wav", "name274.wav", "name275.wav", "name276.wav",
		"name277.wav", "name278.wav", "name279.wav", "name280.wav", "name281.wav", "name282.wav", "name283.wav", "name284.wav", "name285.wav", "name286.wav",
		"name287.wav", "name288.wav", "name289.wav", "name290.wav", "name291.wav", "name292.wav", "name293.wav", "name294.wav", "name295.wav", "name296.wav",
		"name297.wav", "name298.wav", "name299.wav", "name300.wav", "name301.wav", "name302.wav", "name303.wav", "name304.wav", "name305.wav", "name306.wav",
		"name307.wav", "name308.wav", "name155.wav", "name156.wav", "name157.wav", "name158.wav", "name159.wav", "name160.wav", "name161.wav", "name162.wav",
		"name163.wav", "name164.wav", "name165.wav", "name166.wav", "name167.wav", "name168.wav", "name169.wav", "name170.wav", "name171.wav", "name172.wav",
		"name173.wav", "name174.wav", "name175.wav", "name176.wav", "name177.wav", "name178.wav", "name179.wav", "name180.wav", "name181.wav", "name182.wav",
		"name183.wav", "name184.wav", "name185.wav", "name186.wav", "name187.wav", "name188.wav", "name189.wav", "name190.wav", "name191.wav", "name192.wav",
		"name193.wav", "name194.wav", "name195.wav", "name196.wav", "name197.wav", "name198.wav", "name199.wav", "name200.wav", "name401.wav", "name402.wav",
		"name403.wav", "name404.wav", "name405.wav", "name406.wav", "name407.wav", "name408.wav", "name409.wav", "name410.wav", "name411.wav", "name412.wav",
		"name413.wav", "name414.wav", "name415.wav", "name416.wav", "name417.wav", "name418.wav", "name419.wav", "name420.wav", "name421.wav", "name422.wav",
		"name423.wav"
	};

	public readonly string[] peasantSpeech = new string[240]
	{
		"Peasant_Female1.wav", "Peasant_Female10.wav", "Peasant_Female100.wav", "Peasant_Female101.wav", "Peasant_Female102.wav", "Peasant_Female103.wav", "Peasant_Female104.wav", "Peasant_Female105.wav", "Peasant_Female106.wav", "Peasant_Female107.wav",
		"Peasant_Female108.wav", "Peasant_Female109.wav", "Peasant_Female11.wav", "Peasant_Female110.wav", "Peasant_Female111.wav", "Peasant_Female112.wav", "Peasant_Female113.wav", "Peasant_Female114.wav", "Peasant_Female115.wav", "Peasant_Female116.wav",
		"Peasant_Female117.wav", "Peasant_Female118.wav", "Peasant_Female119.wav", "Peasant_Female12.wav", "Peasant_Female120.wav", "Peasant_Female13.wav", "Peasant_Female14.wav", "Peasant_Female15.wav", "Peasant_Female16.wav", "Peasant_Female17.wav",
		"Peasant_Female18.wav", "Peasant_Female19.wav", "Peasant_Female2.wav", "Peasant_Female20.wav", "Peasant_Female21.wav", "Peasant_Female22.wav", "Peasant_Female23.wav", "Peasant_Female24.wav", "Peasant_Female25.wav", "Peasant_Female26.wav",
		"Peasant_Female27.wav", "Peasant_Female28.wav", "Peasant_Female29.wav", "Peasant_Female3.wav", "Peasant_Female30.wav", "Peasant_Female31.wav", "Peasant_Female32.wav", "Peasant_Female33.wav", "Peasant_Female34.wav", "Peasant_Female35.wav",
		"Peasant_Female36.wav", "Peasant_Female37.wav", "Peasant_Female38.wav", "Peasant_Female39.wav", "Peasant_Female4.wav", "Peasant_Female40.wav", "Peasant_Female41.wav", "Peasant_Female42.wav", "Peasant_Female43.wav", "Peasant_Female44.wav",
		"Peasant_Female45.wav", "Peasant_Female46.wav", "Peasant_Female47.wav", "Peasant_Female48.wav", "Peasant_Female49.wav", "Peasant_Female5.wav", "Peasant_Female50.wav", "Peasant_Female51.wav", "Peasant_Female52.wav", "Peasant_Female53.wav",
		"Peasant_Female54.wav", "Peasant_Female55.wav", "Peasant_Female56.wav", "Peasant_Female57.wav", "Peasant_Female58.wav", "Peasant_Female59.wav", "Peasant_Female6.wav", "Peasant_Female60.wav", "Peasant_Female61.wav", "Peasant_Female62.wav",
		"Peasant_Female63.wav", "Peasant_Female64.wav", "Peasant_Female65.wav", "Peasant_Female66.wav", "Peasant_Female67.wav", "Peasant_Female68.wav", "Peasant_Female69.wav", "Peasant_Female7.wav", "Peasant_Female70.wav", "Peasant_Female71.wav",
		"Peasant_Female72.wav", "Peasant_Female73.wav", "Peasant_Female74.wav", "Peasant_Female75.wav", "Peasant_Female76.wav", "Peasant_Female77.wav", "Peasant_Female78.wav", "Peasant_Female79.wav", "Peasant_Female8.wav", "Peasant_Female80.wav",
		"Peasant_Female81.wav", "Peasant_Female82.wav", "Peasant_Female83.wav", "Peasant_Female84.wav", "Peasant_Female85.wav", "Peasant_Female86.wav", "Peasant_Female87.wav", "Peasant_Female88.wav", "Peasant_Female89.wav", "Peasant_Female9.wav",
		"Peasant_Female90.wav", "Peasant_Female91.wav", "Peasant_Female92.wav", "Peasant_Female93.wav", "Peasant_Female94.wav", "Peasant_Female95.wav", "Peasant_Female96.wav", "Peasant_Female97.wav", "Peasant_Female98.wav", "Peasant_Female99.wav",
		"Peasant_Male1.wav", "Peasant_Male10.wav", "Peasant_Male100.wav", "Peasant_Male101.wav", "Peasant_Male102.wav", "Peasant_Male103.wav", "Peasant_Male104.wav", "Peasant_Male105.wav", "Peasant_Male106.wav", "Peasant_Male107.wav",
		"Peasant_Male108.wav", "Peasant_Male109.wav", "Peasant_Male11.wav", "Peasant_Male110.wav", "Peasant_Male111.wav", "Peasant_Male112.wav", "Peasant_Male113.wav", "Peasant_Male114.wav", "Peasant_Male115.wav", "Peasant_Male116.wav",
		"Peasant_Male117.wav", "Peasant_Male118.wav", "Peasant_Male119.wav", "Peasant_Male12.wav", "Peasant_Male120.wav", "Peasant_Male13.wav", "Peasant_Male14.wav", "Peasant_Male15.wav", "Peasant_Male16.wav", "Peasant_Male17.wav",
		"Peasant_Male18.wav", "Peasant_Male19.wav", "Peasant_Male2.wav", "Peasant_Male20.wav", "Peasant_Male21.wav", "Peasant_Male22.wav", "Peasant_Male23.wav", "Peasant_Male24.wav", "Peasant_Male25.wav", "Peasant_Male26.wav",
		"Peasant_Male27.wav", "Peasant_Male28.wav", "Peasant_Male29.wav", "Peasant_Male3.wav", "Peasant_Male30.wav", "Peasant_Male31.wav", "Peasant_Male32.wav", "Peasant_Male33.wav", "Peasant_Male34.wav", "Peasant_Male35.wav",
		"Peasant_Male36.wav", "Peasant_Male37.wav", "Peasant_Male38.wav", "Peasant_Male39.wav", "Peasant_Male4.wav", "Peasant_Male40.wav", "Peasant_Male41.wav", "Peasant_Male42.wav", "Peasant_Male43.wav", "Peasant_Male44.wav",
		"Peasant_Male45.wav", "Peasant_Male46.wav", "Peasant_Male47.wav", "Peasant_Male48.wav", "Peasant_Male49.wav", "Peasant_Male5.wav", "Peasant_Male50.wav", "Peasant_Male51.wav", "Peasant_Male52.wav", "Peasant_Male53.wav",
		"Peasant_Male54.wav", "Peasant_Male55.wav", "Peasant_Male56.wav", "Peasant_Male57.wav", "Peasant_Male58.wav", "Peasant_Male59.wav", "Peasant_Male6.wav", "Peasant_Male60.wav", "Peasant_Male61.wav", "Peasant_Male62.wav",
		"Peasant_Male63.wav", "Peasant_Male64.wav", "Peasant_Male65.wav", "Peasant_Male66.wav", "Peasant_Male67.wav", "Peasant_Male68.wav", "Peasant_Male69.wav", "Peasant_Male7.wav", "Peasant_Male70.wav", "Peasant_Male71.wav",
		"Peasant_Male72.wav", "Peasant_Male73.wav", "Peasant_Male74.wav", "Peasant_Male75.wav", "Peasant_Male76.wav", "Peasant_Male77.wav", "Peasant_Male78.wav", "Peasant_Male79.wav", "Peasant_Male8.wav", "Peasant_Male80.wav",
		"Peasant_Male81.wav", "Peasant_Male82.wav", "Peasant_Male83.wav", "Peasant_Male84.wav", "Peasant_Male85.wav", "Peasant_Male86.wav", "Peasant_Male87.wav", "Peasant_Male88.wav", "Peasant_Male89.wav", "Peasant_Male9.wav",
		"Peasant_Male90.wav", "Peasant_Male91.wav", "Peasant_Male92.wav", "Peasant_Male93.wav", "Peasant_Male94.wav", "Peasant_Male95.wav", "Peasant_Male96.wav", "Peasant_Male97.wav", "Peasant_Male98.wav", "Peasant_Male99.wav"
	};

	public readonly string[] troopsSpeech = new string[659]
	{
		"Aassasin_ATKS1.wav", "Aassasin_ATKS2.wav", "Aassasin_ATKS3.wav", "Aassasin_ATKS4.wav", "Aassasin_ATKW1.wav", "Aassasin_ATKW2.wav", "Aassasin_ATKW3.wav", "Aassasin_ATKW4.wav", "Aassasin_Disband1.wav", "Aassasin_M1.wav",
		"Aassasin_M2.wav", "Aassasin_M3.wav", "Aassasin_M4.wav", "Aassasin_M5.wav", "Aassasin_Moat1.wav", "Aassasin_S1.wav", "Aassasin_S2.wav", "Aassasin_S3.wav", "Aassasin_S4.wav", "Aassasin_S5.wav",
		"Aassasin_S6.wav", "Abow_ATKA1.wav", "Abow_ATKH1.wav", "Abow_ATKM1.wav", "Abow_ATKM2.wav", "Abow_ATKM3.wav", "Abow_ATKM4.wav", "Abow_ATKNT.wav", "Abow_ATK_EQP1.wav", "Abow_Disband1.wav",
		"Abow_Light_Pitch1.wav", "Abow_M1.wav", "Abow_M2.wav", "Abow_M3.wav", "Abow_M4.wav", "Abow_M5.wav", "Abow_Moat1.wav", "Abow_S1.wav", "Abow_S2.wav", "Abow_S3.wav",
		"Abow_S4.wav", "Abow_S5.wav", "Abow_S6.wav", "AEngineer_ATKS1.wav", "AEngineer_Balis1.wav", "AEngineer_Build1.wav", "AEngineer_Catplt1.wav", "AEngineer_Disband1.wav", "AEngineer_Equip4.wav", "AEngineer_Exit1.wav",
		"AEngineer_Launchcow1.wav", "AEngineer_M1.wav", "AEngineer_M2.wav", "AEngineer_M3.wav", "AEngineer_Manequip1.wav", "AEngineer_Mang1.wav", "AEngineer_Manoil1.wav", "AEngineer_Mansmelter1.wav", "AEngineer_Mcatplt.wav", "AEngineer_Moat1.wav",
		"AEngineer_Mram.wav", "AEngineer_Mshield.wav", "AEngineer_Mtower.wav", "AEngineer_Pouroil1.wav", "AEngineer_Ram1.wav", "AEngineer_S1.wav", "AEngineer_S2.wav", "AEngineer_Sbalis.wav", "AEngineer_Scatplt.wav", "AEngineer_Sman.wav",
		"AEngineer_Sram.wav", "AEngineer_Ssheild.wav", "AEngineer_Stower.wav", "AEngineer_Streb.wav", "AEngineer_Treb1.wav", "Agrenadier_ATKH1.wav", "Agrenadier_ATKM1.wav", "Agrenadier_ATKM2.wav", "Agrenadier_ATKM3.wav", "Agrenadier_ATKM4.wav",
		"Agrenadier_Disband1.wav", "Agrenadier_M1.wav", "Agrenadier_M2.wav", "Agrenadier_M3.wav", "Agrenadier_M4.wav", "Agrenadier_M5.wav", "Agrenadier_Moat1.wav", "Agrenadier_S1.wav", "Agrenadier_S2.wav", "Agrenadier_S3.wav",
		"Agrenadier_S4.wav", "Agrenadier_S5.wav", "Agrenadier_S6.wav", "Ahorse_ATKA1.wav", "Ahorse_ATKH1.wav", "Ahorse_ATKM1.wav", "Ahorse_ATKM2.wav", "Ahorse_ATKM3.wav", "Ahorse_ATKM4.wav", "Ahorse_Disband1.wav",
		"Ahorse_M1.wav", "Ahorse_M2.wav", "Ahorse_M3.wav", "Ahorse_M4.wav", "Ahorse_M5.wav", "Ahorse_Moat1.wav", "Ahorse_S1.wav", "Ahorse_S2.wav", "Ahorse_S3.wav", "Ahorse_S4.wav",
		"Ahorse_S5.wav", "Ahorse_S6.wav", "Ambusher_ATKM1.wav", "Ambusher_ATKM2.wav", "Ambusher_ATKM3.wav", "Ambusher_ATKM4.wav", "Ambusher_Disband1.wav", "Ambusher_Hide1.wav", "Ambusher_Hide2.wav", "Ambusher_M1.wav",
		"Ambusher_M2.wav", "Ambusher_M3.wav", "Ambusher_M4.wav", "Ambusher_M5.wav", "Ambusher_Moat1.wav", "Ambusher_Moat2.wav", "Ambusher_Moat3.wav", "Ambusher_S1.wav", "Ambusher_S2.wav", "Ambusher_S3.wav",
		"Ambusher_S4.wav", "Ambusher_S5.wav", "Ambusher_S6.wav", "Ambusher_UnHide1.wav", "Ambusher_UnHide2.wav", "Arch_ATKA1.wav", "Arch_ATKH1.wav", "Arch_ATKM1.wav", "Arch_ATKM2.wav", "Arch_ATKM3.wav",
		"Arch_ATKM4.wav", "Arch_ATKNT.wav", "Arch_ATK_EQP1.wav", "Arch_Disband1.wav", "Arch_Light_Pitch1.wav", "Arch_m1.wav", "Arch_m2.wav", "Arch_m3.wav", "Arch_m4.wav", "Arch_m5.wav",
		"Arch_Moat1.wav", "Arch_Moat2.wav", "Arch_Moat3.wav", "Arch_s1.wav", "Arch_s2.wav", "Arch_s3.wav", "Arch_s4.wav", "Arch_s5.wav", "Arch_s6.wav", "Aslave_ATKS1.wav",
		"Aslave_ATKS2.wav", "Aslave_Disband1.wav", "Aslave_M1.wav", "Aslave_M2.wav", "Aslave_M3.wav", "Aslave_M4.wav", "Aslave_Moat1.wav", "Aslave_Moat2.wav", "Aslave_Moat3.wav", "Aslave_Moat4.wav",
		"Aslave_Moat5.wav", "Aslave_S1.wav", "Aslave_S2.wav", "Asling_ATKA1.wav", "Asling_ATKH1.wav", "Asling_ATKM1.wav", "Asling_ATKM2.wav", "Asling_ATKM3.wav", "Asling_ATKM4.wav", "Asling_ATKNT.wav",
		"Asling_Disband1.wav", "Asling_M1.wav", "Asling_M2.wav", "Asling_M3.wav", "Asling_M4.wav", "Asling_M5.wav", "Asling_Moat1.wav", "Asling_S1.wav", "Asling_S2.wav", "Asling_S3.wav",
		"Asling_S4.wav", "Asling_S5.wav", "Asling_S6.wav", "Asword_ATKW1.wav", "Asword_ATKW2.wav", "Asword_ATKW3.wav", "Asword_ATKW4.wav", "Asword_Disband1.wav", "Asword_M1.wav", "Asword_M2.wav",
		"Asword_M3.wav", "Asword_M4.wav", "Asword_M5.wav", "Asword_Moat1.wav", "Asword_Moat2.wav", "asword_Moat3.wav", "Asword_S1.wav", "Asword_S2.wav", "Asword_S3.wav", "Asword_S4.wav",
		"Asword_S5.wav", "Asword_S6.wav", "Bedouin_ATKS1.wav", "Bedouin_ATKS2.wav", "Bedouin_ATKS3.wav", "Bedouin_ATKS4.wav", "Bedouin_ATKW1.wav", "Bedouin_ATKW2.wav", "Bedouin_ATKW3.wav", "Bedouin_ATKW4.wav",
		"Bedouin_Disband1.wav", "Bedouin_M1.wav", "Bedouin_M2.wav", "Bedouin_M3.wav", "Bedouin_M4.wav", "Bedouin_M5.wav", "Bedouin_Moat1.wav", "Bedouin_Moat2.wav", "Bedouin_Moat3.wav", "Bedouin_S1.wav",
		"Bedouin_S2.wav", "Bedouin_S3.wav", "Bedouin_S4.wav", "Bedouin_S5.wav", "Bedouin_S6.wav", "CamelLancer_ATKS1.wav", "CamelLancer_ATKS2.wav", "CamelLancer_ATKS3.wav", "CamelLancer_ATKS4.wav", "CamelLancer_ATKW1.wav",
		"CamelLancer_ATKW2.wav", "CamelLancer_ATKW3.wav", "CamelLancer_ATKW4.wav", "CamelLancer_Disband1.wav", "CamelLancer_M1.wav", "CamelLancer_M2.wav", "CamelLancer_M3.wav", "CamelLancer_M4.wav", "CamelLancer_M5.wav", "CamelLancer_Moat1.wav",
		"CamelLancer_Moat2.wav", "CamelLancer_Moat3.wav", "CamelLancer_S1.wav", "CamelLancer_S2.wav", "CamelLancer_S3.wav", "CamelLancer_S4.wav", "CamelLancer_S5.wav", "CamelLancer_S6.wav", "Cross_ATKA1.wav", "Cross_ATKH1.wav",
		"Cross_ATKM1.wav", "Cross_ATKM2.wav", "Cross_ATKM3.wav", "Cross_ATKM4.wav", "Cross_ATKNT.wav", "Cross_Disband1.wav", "Cross_m1.wav", "Cross_m2.wav", "Cross_m3.wav", "Cross_m4.wav",
		"Cross_m5.wav", "Cross_Moat1.wav", "Cross_Moat2.wav", "Cross_Moat3.wav", "Cross_s1.wav", "Cross_s2.wav", "Cross_s3.wav", "Cross_s4.wav", "Cross_s5.wav", "Cross_s6.wav",
		"Demolisher_ATKS1.wav", "Demolisher_ATKS2.wav", "Demolisher_ATKS3.wav", "Demolisher_ATKS4.wav", "Demolisher_Disband1.wav", "Demolisher_M1.wav", "Demolisher_M2.wav", "Demolisher_M3.wav", "Demolisher_M4.wav", "Demolisher_M5.wav",
		"Demolisher_Moat1.wav", "Demolisher_Moat2.wav", "Demolisher_Moat3.wav", "Demolisher_S1.wav", "Demolisher_S2.wav", "Demolisher_S3.wav", "Demolisher_S4.wav", "Demolisher_S5.wav", "Demolisher_S6.wav", "Demolisher_Sap1.wav",
		"Demolisher_Sap2.wav", "Demolisher_Sap3.wav", "Demolisher_Sap4.wav", "Engineer_ATKS1.wav", "Engineer_ATKS2.wav", "Engineer_ATKS3.wav", "Engineer_ATKS4.wav", "Engineer_ATKW1.wav", "Engineer_Balis1.wav", "Engineer_Build1.wav",
		"Engineer_catplt1.wav", "Engineer_Disband1.wav", "Engineer_Equip1.wav", "Engineer_Equip2.wav", "Engineer_Equip3.wav", "Engineer_Equip4.wav", "Engineer_Exit1.wav", "Engineer_Launchcow1.wav", "Engineer_Launchcow2.wav", "Engineer_Launchcow3.wav",
		"Engineer_Launchcow4.wav", "Engineer_Launchcow5.wav", "Engineer_Launchcow6.wav", "Engineer_M1.wav", "Engineer_M2.wav", "Engineer_M3.wav", "Engineer_M4.wav", "Engineer_M5.wav", "Engineer_Manequip1.wav", "Engineer_Mang1.wav",
		"Engineer_Manoil1.wav", "Engineer_Mansmelter1.wav", "Engineer_MCatplt.wav", "Engineer_Moat1.wav", "Engineer_Moat2.wav", "Engineer_Moat3.wav", "Engineer_Mram.wav", "Engineer_Mshield.wav", "Engineer_Mtower.wav", "Engineer_Pouroil1.wav",
		"Engineer_Pouroil2.wav", "Engineer_Pouroil3.wav", "Engineer_Pouroil4.wav", "Engineer_Pouroil5.wav", "Engineer_Pouroil6.wav", "Engineer_Pouroil7.wav", "Engineer_Pouroil8.wav", "Engineer_Pouroil9.wav", "Engineer_Ram1.wav", "Engineer_s1.wav",
		"Engineer_s2.wav", "Engineer_s3.wav", "Engineer_s4.wav", "Engineer_s5.wav", "Engineer_s6.wav", "Engineer_Sbalis.wav", "Engineer_Scatplt.wav", "Engineer_Sman.wav", "Engineer_Sram.wav", "Engineer_Sshield.wav",
		"Engineer_Stower.wav", "Engineer_STreb.wav", "Engineer_Treb1.wav", "Eunuch_ATKS1.wav", "Eunuch_ATKS2.wav", "Eunuch_ATKS3.wav", "Eunuch_ATKS4.wav", "Eunuch_ATKW1.wav", "Eunuch_ATKW2.wav", "Eunuch_ATKW3.wav",
		"Eunuch_ATKW4.wav", "Eunuch_Disband1.wav", "Eunuch_M1.wav", "Eunuch_M2.wav", "Eunuch_M3.wav", "Eunuch_M4.wav", "Eunuch_M5.wav", "Eunuch_Moat1.wav", "Eunuch_Moat2.wav", "Eunuch_Moat3.wav",
		"Eunuch_S1.wav", "Eunuch_S2.wav", "Eunuch_S3.wav", "Eunuch_S4.wav", "Eunuch_S5.wav", "Eunuch_S6.wav", "Healer_ATKS1.wav", "Healer_ATKS2.wav", "Healer_ATKS3.wav", "Healer_ATKS4.wav",
		"Healer_Disband1.wav", "Healer_Heal1.wav", "Healer_Heal2.wav", "Healer_Heal3.wav", "Healer_Heal4.wav", "Healer_M1.wav", "Healer_M2.wav", "Healer_M3.wav", "Healer_M4.wav", "Healer_M5.wav",
		"Healer_Moat1.wav", "Healer_Moat2.wav", "Healer_Moat3.wav", "Healer_S1.wav", "Healer_S2.wav", "Healer_S3.wav", "Healer_S4.wav", "Healer_S5.wav", "Healer_S6.wav", "HeavyCamel_ATKM1.wav",
		"HeavyCamel_ATKM2.wav", "HeavyCamel_ATKM3.wav", "HeavyCamel_ATKM4.wav", "HeavyCamel_Disband1.wav", "HeavyCamel_M1.wav", "HeavyCamel_M2.wav", "HeavyCamel_M3.wav", "HeavyCamel_M4.wav", "HeavyCamel_M5.wav", "HeavyCamel_Moat1.wav",
		"HeavyCamel_Moat2.wav", "HeavyCamel_Moat3.wav", "HeavyCamel_S1.wav", "HeavyCamel_S2.wav", "HeavyCamel_S3.wav", "HeavyCamel_S4.wav", "HeavyCamel_S5.wav", "HeavyCamel_S6.wav", "Knight_ATKW1.wav", "Knight_ATKW2.wav",
		"Knight_ATKW3.wav", "Knight_ATKW4.wav", "Knight_Disband1.wav", "Knight_m1.wav", "Knight_m2.wav", "Knight_m3.wav", "Knight_m4.wav", "Knight_m5.wav", "Knight_Moat1.wav", "Knight_Moat2.wav",
		"Knight_Moat3.wav", "Knight_s1.wav", "Knight_s2.wav", "Knight_s3.wav", "Knight_s4.wav", "Knight_s5.wav", "Knight_s6.wav", "Ladder_ATKS1.wav", "Ladder_ATKS2.wav", "Ladder_ATKS3.wav",
		"Ladder_ATKS4.wav", "Ladder_Disband1.wav", "Ladder_m1.wav", "Ladder_m2.wav", "Ladder_m3.wav", "Ladder_m4.wav", "Ladder_m5.wav", "Ladder_Moat1.wav", "Ladder_Moat2.wav", "Ladder_Moat3.wav",
		"Ladder_Placeladder1.wav", "Ladder_Placeladder2.wav", "Ladder_Placeladder3.wav", "Ladder_s1.wav", "Ladder_s2.wav", "Ladder_s3.wav", "Ladder_s4.wav", "Ladder_s5.wav", "Ladder_s6.wav", "Mace_ATKS1.wav",
		"Mace_ATKS2.wav", "Mace_ATKS3.wav", "Mace_ATKS4.wav", "Mace_ATKW1.wav", "Mace_ATKW2.wav", "Mace_ATKW3.wav", "Mace_ATKW4.wav", "Mace_Disband1.wav", "Mace_m1.wav", "Mace_m2.wav",
		"Mace_m3.wav", "Mace_m4.wav", "Mace_m5.wav", "Mace_Moat1.wav", "Mace_Moat2.wav", "Mace_Moat3.wav", "Mace_s1.wav", "Mace_s2.wav", "Mace_s3.wav", "Mace_s4.wav",
		"Mace_s5.wav", "Mace_s6.wav", "Monk_ATKS1.wav", "Monk_ATKS2.wav", "Monk_ATKS3.wav", "Monk_ATKS4.wav", "Monk_ATKW1.wav", "Monk_ATKW2.wav", "Monk_ATKW3.wav", "Monk_ATKW4.wav",
		"Monk_Disband1.wav", "Monk_m1.wav", "Monk_m2.wav", "Monk_m3.wav", "Monk_m4.wav", "Monk_m5.wav", "Monk_Moat1.wav", "Monk_Moat2.wav", "Monk_Moat3.wav", "Monk_s1.wav",
		"Monk_s2.wav", "Monk_s3.wav", "Monk_s4.wav", "Monk_s5.wav", "Monk_s6.wav", "Pike_ATKS1.wav", "Pike_ATKS2.wav", "Pike_ATKS3.wav", "Pike_ATKS4.wav", "Pike_ATKW1.wav",
		"Pike_ATKW2.wav", "Pike_ATKW3.wav", "Pike_ATKW4.wav", "Pike_Disband1.wav", "Pike_Ladder1.wav", "Pike_Ladder2.wav", "Pike_Ladder3.wav", "Pike_M1.wav", "Pike_M2.wav", "Pike_M3.wav",
		"Pike_M4.wav", "Pike_M5.wav", "Pike_Moat1.wav", "Pike_Moat2.wav", "Pike_Moat3.wav", "Pike_S1.wav", "Pike_S2.wav", "Pike_S3.wav", "Pike_S4.wav", "Pike_S5.wav",
		"Pike_S6.wav", "Sapper_ATKS1.wav", "Sapper_ATKS2.wav", "Sapper_ATKS3.wav", "Sapper_ATKS4.wav", "Sapper_Disband1.wav", "Sapper_M1.wav", "Sapper_M2.wav", "Sapper_M3.wav", "Sapper_M4.wav",
		"Sapper_M5.wav", "Sapper_Moat1.wav", "Sapper_Moat2.wav", "Sapper_Moat3.wav", "Sapper_S1.wav", "Sapper_S2.wav", "Sapper_S3.wav", "Sapper_S4.wav", "Sapper_S5.wav", "Sapper_S6.wav",
		"Sapper_Sap1.wav", "Sapper_Sap2.wav", "Sapper_Sap3.wav", "Sapper_Sap4.wav", "Spear_ATKS1.wav", "Spear_ATKS2.wav", "Spear_ATKS3.wav", "Spear_ATKS4.wav", "Spear_ATKW1.wav", "Spear_ATKW2.wav",
		"Spear_ATKW3.wav", "Spear_ATKW4.wav", "Spear_Disband1.wav", "Spear_Ladder1.wav", "Spear_Ladder2.wav", "Spear_Ladder3.wav", "Spear_m1.wav", "Spear_m2.wav", "Spear_m3.wav", "Spear_m4.wav",
		"Spear_m5.wav", "Spear_Moat1.wav", "Spear_Moat2.wav", "Spear_Moat3.wav", "Spear_Moat4.wav", "Spear_Moat5.wav", "Spear_s1.wav", "Spear_s2.wav", "Spear_s3.wav", "Spear_s4.wav",
		"Spear_s5.wav", "Spear_s6.wav", "Sword_ATKW1.wav", "Sword_ATKW2.wav", "Sword_ATKW3.wav", "Sword_ATKW4.wav", "Sword_Disband1.wav", "Sword_m1.wav", "Sword_m2.wav", "Sword_m3.wav",
		"Sword_m4.wav", "Sword_m5.wav", "Sword_Moat1.wav", "Sword_Moat2.wav", "Sword_Moat3.wav", "Sword_s1.wav", "Sword_s2.wav", "Sword_s3.wav", "Sword_s4.wav", "Sword_s5.wav",
		"Sword_s6.wav", "Tunnel_ATKS1.wav", "Tunnel_ATKS2.wav", "Tunnel_ATKS3.wav", "Tunnel_ATKS4.wav", "Tunnel_ATKW1.wav", "Tunnel_ATKW2.wav", "Tunnel_ATKW3.wav", "Tunnel_ATKW4.wav", "Tunnel_Digtunnel1.wav",
		"Tunnel_Digtunnel2.wav", "Tunnel_Disband1.wav", "Tunnel_m1.wav", "Tunnel_m2.wav", "Tunnel_m3.wav", "Tunnel_m4.wav", "Tunnel_m5.wav", "Tunnel_Moat1.wav", "Tunnel_Moat2.wav", "Tunnel_Moat3.wav",
		"Tunnel_s1.wav", "Tunnel_s2.wav", "Tunnel_s3.wav", "Tunnel_s4.wav", "Tunnel_s5.wav", "Tunnel_s6.wav", "TempleGuard_s1.wav", "TempleGuard_s2.wav", "TempleGuard_s3.wav", "TempleGuard_s4.wav",
		"TempleGuard_s5.wav", "TempleGuard_s6.wav", "TempleGuard_m1.wav", "TempleGuard_m2.wav", "TempleGuard_m3.wav", "TempleGuard_m4.wav", "TempleGuard_m5.wav", "TempleGuard_moat1.wav", "TempleGuard_moat2.wav", "TempleGuard_moat3.wav",
		"TempleGuard_atks1.wav", "TempleGuard_atks2.wav", "TempleGuard_atks3.wav", "TempleGuard_atks4.wav", "TempleGuard_atkw1.wav", "TempleGuard_atkw2.wav", "TempleGuard_atkw3.wav", "TempleGuard_atkw4.wav", "TempleGuard_disband1.wav"
	};

	public readonly string[] tutorialSpeech = new string[77]
	{
		"Tutorial_1.wav", "Tutorial_10a.wav", "Tutorial_10b.wav", "Tutorial_11.wav", "Tutorial_12a.wav", "Tutorial_12b.wav", "Tutorial_13.wav", "Tutorial_14a.wav", "Tutorial_14b.wav", "Tutorial_15.wav",
		"Tutorial_16.wav", "Tutorial_17a.wav", "Tutorial_17a_alt.wav", "Tutorial_17b.wav", "Tutorial_18.wav", "Tutorial_19.wav", "Tutorial_2.wav", "Tutorial_2a.wav", "Tutorial_20.wav", "Tutorial_21.wav",
		"Tutorial_22.wav", "Tutorial_22a.wav", "Tutorial_22b.wav", "Tutorial_22c.wav", "Tutorial_22d.wav", "Tutorial_23.wav", "Tutorial_24.wav", "Tutorial_25.wav", "Tutorial_26.wav", "Tutorial_26a.wav",
		"Tutorial_26b.wav", "Tutorial_26c.wav", "Tutorial_26d.wav", "Tutorial_26e.wav", "Tutorial_26f.wav", "Tutorial_26g.wav", "Tutorial_26h.wav", "Tutorial_26i.wav", "Tutorial_26j.wav", "Tutorial_27.wav",
		"Tutorial_28.wav", "Tutorial_28a.wav", "Tutorial_29.wav", "Tutorial_29a.wav", "Tutorial_30.wav", "Tutorial_31.wav", "Tutorial_32.wav", "Tutorial_32a.wav", "Tutorial_33.wav", "Tutorial_34.wav",
		"Tutorial_35.wav", "Tutorial_36.wav", "Tutorial_37.wav", "Tutorial_38.wav", "Tutorial_39.wav", "Tutorial_3a.wav", "Tutorial_3b.wav", "Tutorial_3c.wav", "Tutorial_40.wav", "Tutorial_41.wav",
		"Tutorial_42.wav", "Tutorial_43.wav", "Tutorial_44.wav", "Tutorial_45.wav", "Tutorial_46.wav", "Tutorial_4a.wav", "Tutorial_4b.wav", "Tutorial_4c.wav", "Tutorial_5.wav", "Tutorial_6a.wav",
		"Tutorial_6b.wav", "Tutorial_7.wav", "Tutorial_8a.wav", "Tutorial_8b.wav", "Tutorial_9a.wav", "Tutorial_9b.wav", "Tutorial_9c.wav"
	};

	public readonly string[] campaignSpeech = new string[84]
	{
		"After_01.wav", "After_02.wav", "After_03.wav", "After_04.wav", "After_05.wav", "After_06.wav", "After_07.wav", "Intro_01.wav", "Intro_02.wav", "Intro_03.wav",
		"Intro_04.wav", "Intro_05.wav", "Intro_06.wav", "Intro_07.wav", "M10_Brief.wav", "M10_Hist.wav", "M11_Brief.wav", "M11_Hist.wav", "M12_Brief.wav", "M12_Hist.wav",
		"M13_Brief.wav", "M13_Hist.wav", "M14_Brief.wav", "M14_Hist.wav", "M15_Brief.wav", "M15_Hist.wav", "M16_Brief.wav", "M16_Hist.wav", "M17_Brief.wav", "M17_Hist.wav",
		"M18_Brief.wav", "M18_Hist.wav", "M19_Brief.wav", "M19_Hist.wav", "M1_Brief.wav", "M1_Hist.wav", "M20_Brief.wav", "M20_Hist.wav", "M21_Brief.wav", "M21_Hist.wav",
		"M22_Brief.wav", "M22_Hist.wav", "M23_Brief.wav", "M23_Hist.wav", "M24_Brief.wav", "M24_Hist.wav", "M25_Brief.wav", "M25_Hist.wav", "M26_Brief.wav", "M26_Hist.wav",
		"M27_Brief.wav", "M27_Hist.wav", "M28_Brief.wav", "M28_Hist.wav", "M29_Brief.wav", "M29_Hist.wav", "M2_Brief.wav", "M2_Hist.wav", "M30_Brief.wav", "M30_Hist.wav",
		"M31_Brief.wav", "M31_Hist.wav", "M32_Brief.wav", "M32_Hist.wav", "M33_Brief.wav", "M33_Hist.wav", "M34_Brief.wav", "M34_Hist.wav", "M35_Brief.wav", "M35_Hist.wav",
		"M3_Brief.wav", "M3_Hist.wav", "M4_Brief.wav", "M4_Hist.wav", "M5_Brief.wav", "M5_Hist.wav", "M6_Brief.wav", "M6_Hist.wav", "M7_Brief.wav", "M7_Hist.wav",
		"M8_Brief.wav", "M8_Hist.wav", "M9_Brief.wav", "M9_Hist.wav"
	};

	public Dictionary<string, string> customLordDefaultSpeech = new Dictionary<string, string>
	{
		["SK_TAUNT1"] = "fx\\speech\\all_taunt_01.wav",
		["SK_TAUNT2"] = "fx\\speech\\all_taunt_02.wav",
		["SK_TAUNT3"] = "fx\\speech\\all_taunt_03.wav",
		["SK_TAUNT4"] = "fx\\speech\\all_taunt_04.wav",
		["SK_ANGRY_SIEGE_LOST"] = "fx\\speech\\all_anger_01.wav",
		["SK_ANGRY_CASTLE_DAMAGED"] = "fx\\speech\\all_anger_02.wav",
		["SK_DEFEAT"] = "fx\\speech\\all_plead_01.wav",
		["SK_NERV_PRE_SIEGE"] = "fx\\speech\\all_plead_02.wav",
		["SK_NERV_WEAK"] = "fx\\speech\\all_plead_03.wav",
		["SK_VICTORY_GOOD"] = "fx\\speech\\all_vict_01.wav",
		["SK_VICTORY_HARASS"] = "fx\\speech\\all_vict_02.wav",
		["SK_KILL_PLAYER"] = "fx\\speech\\all_vict_03.wav",
		["SK_KILL_NPC"] = "fx\\speech\\all_vict_04.wav",
		["SK_REQUEST_GOODS"] = "fx\\speech\\all_req_01.wav",
		["SK_THANK_GOODS"] = "fx\\speech\\all_thanks_01.wav",
		["SK_DIE_ALLY"] = "fx\\speech\\all_ally_death_01.wav",
		["SK_CONGRATS_ON_KILL"] = "fx\\speech\\all_congrats_01.wav",
		["SK_BOAST_OF_KILL"] = "fx\\speech\\all_boast_01.wav",
		["SK_ALLY_NEED_HELP"] = "fx\\speech\\all_help_01.wav",
		["SK_KICK_PLAYER"] = "fx\\speech\\all_kick_player_01.wav",
		["SK_ADD_PLAYER"] = "fx\\speech\\all_add_player_01.wav",
		["SK_ABOUT2SIEGE"] = "fx\\speech\\all_siege_01.wav",
		["SK_CANT_ATTACK"] = "fx\\speech\\all_noattack_01.wav",
		["SK_WONT_ATTACK"] = "fx\\speech\\all_noattack_02.wav",
		["SK_CANT_HELP"] = "fx\\speech\\all_nohelp_01.wav",
		["SK_WONT_HELP"] = "fx\\speech\\all_nohelp_02.wav",
		["SK_NOT_SENDING_GOODS"] = "fx\\speech\\all_notsent_01.wav",
		["SK_SENT_GOODS"] = "fx\\speech\\all_sent_01.wav",
		["SK_TEAM_WINNING"] = "fx\\speech\\all_team_winning_01.wav",
		["SK_TEAM_LOSING"] = "fx\\speech\\all_team_losing_01.wav",
		["SK_WILL_SEND_TROOPS"] = "fx\\speech\\all_helpsent_01.wav",
		["SK_WILL_ATTACK_ENEMY"] = "fx\\speech\\all_willattack_01.wav"
	};

	public readonly string[] music_filenames = new string[132]
	{
		"", "fx\\music\\null.raw", "fx\\music\\sand wedgie.raw", "fx\\music\\astrongspice.raw", "fx\\music\\null.raw", "fx\\music\\null.raw", "fx\\music\\monks1.raw", "fx\\music\\stainedglass1.raw", "fx\\music\\apaneintheglass.raw", "fx\\music\\caravan_ambient.raw",
		"fx\\music\\trancefusion.raw", "fx\\music\\crusader_solo.raw", "fx\\music\\thelastdrop.raw", "fx\\music\\astrongspice.raw", "fx\\music\\caravan.raw", "fx\\music\\sandalmaker.raw", "fx\\music\\dar meshq.raw", "fx\\music\\thelastdrop.raw", "fx\\music\\trancefusion.raw", "fx\\music\\crusader_solo.raw",
		"fx\\music\\cameltoe.raw", "fx\\music\\end_music.raw", "fx\\music\\suspense1a.raw", "fx\\music\\suspense1b.raw", "fx\\music\\suspense2a.raw", "fx\\music\\suspense2b.raw", "fx\\music\\suspense2c.raw", "fx\\music\\drumloop1c.raw", "fx\\music\\percloop1.raw", "fx\\music\\honor_02.raw",
		"fx\\music\\honor_03.raw", "fx\\music\\honor_04.raw", "fx\\music\\honor_05.raw", "fx\\music\\glory_01.raw", "fx\\music\\glory_02.raw", "fx\\music\\glory_03.raw", "fx\\music\\glory_04.raw", "fx\\music\\glory_05.raw", "fx\\music\\glory_06.raw", "fx\\music\\percloop1.raw",
		"fx\\music\\drumloop1a.raw", "fx\\music\\drumloop1b.raw", "fx\\music\\bigwin1.raw", "fx\\music\\bigwin2.raw", "fx\\music\\bigwin3.raw", "fx\\music\\bigloss1.raw", "fx\\music\\bigloss2.raw", "fx\\music\\crusader.raw", "fx\\music\\crusader.raw", "fx\\music\\sand wedgie.raw",
		"fx\\music\\flt_04.raw", "fx\\music\\crusader.raw", "fx\\music\\thelastdrop.raw", "fx\\music\\caravan_ambient.raw", "fx\\music\\thelastdrop.raw", "fx\\music\\caravan_ambient.raw", "fx\\music\\thelastdrop.raw", "fx\\music\\crusader.raw", "fx\\music\\caravan_ambient.raw", "fx\\music\\caravan_ambient.raw",
		"fx\\music\\suspense1b.raw", "fx\\music\\caravan_ambient.raw", "fx\\music\\crusader.raw", "fx\\music\\caravan.raw", "fx\\music\\null.raw", "fx\\music\\sand wedgie.raw", "fx\\music\\suspense2b.raw", "fx\\music\\solovln_01.raw", "fx\\music\\flt_narr1.raw", "fx\\music\\flt_narr1.raw",
		"fx\\music\\flt_narr1.raw", "fx\\music\\flt_narr1.raw", "fx\\music\\flt_07.raw", "fx\\music\\oud_01.raw", "fx\\music\\oud_02.raw", "fx\\music\\oud_03.raw", "fx\\music\\oud_04.raw", "fx\\music\\oud_05.raw", "fx\\music\\oud_06.raw", "fx\\music\\oud_07.raw",
		"fx\\music\\oud_08.raw", "fx\\music\\oud_09.raw", "fx\\music\\oud_20.raw", "fx\\music\\oud_11.raw", "fx\\music\\oud_12.raw", "fx\\music\\oud_13.raw", "fx\\music\\oud_14.raw", "fx\\music\\oud_15.raw", "fx\\music\\oud_16.raw", "fx\\music\\oud_17.raw",
		"fx\\music\\oud_18.raw", "fx\\music\\oud_19.raw", "fx\\music\\oud_20.raw", "fx\\music\\oud_21.raw", "fx\\music\\oud_22.raw", "fx\\music\\oud_23.raw", "fx\\music\\oud_24.raw", "fx\\music\\flt_01.raw", "fx\\music\\flt_02.raw", "fx\\music\\flt_03.raw",
		"fx\\music\\flt_04.raw", "fx\\music\\flt_05.raw", "fx\\music\\flt_06.raw", "fx\\music\\flt_07.raw", "fx\\music\\flt_08.raw", "fx\\music\\flt_09.raw", "fx\\music\\flt_10.raw", "fx\\music\\flt_11.raw", "fx\\music\\flt_12.raw", "fx\\music\\flt_13.raw",
		"fx\\music\\flt_14.raw", "fx\\music\\flt_15.raw", "fx\\music\\flt_16.raw", "fx\\music\\flt_17.raw", "fx\\music\\flt_18.raw", "fx\\music\\flt_19.raw", "fx\\music\\campaign 6_01.raw", "fx\\music\\campaign 6_02.raw", "fx\\music\\campaign 6_03.raw", "fx\\music\\campaign 6_04.raw",
		"fx\\music\\campaign 6_05.raw", "fx\\music\\campaign 7_01.raw", "fx\\music\\campaign 7_02.raw", "fx\\music\\campaign 7_03.raw", "fx\\music\\campaign 7_04.raw", "fx\\music\\campaign 7_05.raw", "fx\\music\\campaign 8_01.raw", "fx\\music\\campaign 8_02.raw", "fx\\music\\campaign 8_03.raw", "fx\\music\\campaign 8_04.raw",
		"fx\\music\\campaign 8_05.raw", "fx\\music\\DeTrailer.raw"
	};

	public Dictionary<string, string> speechFolders = new Dictionary<string, string>();

	public List<sh1_sound_effect> fx_list = new List<sh1_sound_effect>();

	public List<sh1_sound_effect> ambient_list = new List<sh1_sound_effect>();

	public List<sh1_sound> play_list = new List<sh1_sound>();

	public Dictionary<string, VolumeData> volumeData = new Dictionary<string, VolumeData>();

	public bool soundsLoaded;

	public DateTime nextAllowableMoo = DateTime.MinValue;

	public DateTime nextAllowableInn = DateTime.MinValue;

	public DateTime nextAllowableDancingBear = DateTime.MinValue;

	public DateTime nextAllowableMaypole = DateTime.MinValue;

	public string lastMusic = "";

	public DateTime freeBuildVoicelinesStart = DateTime.MinValue;

	public int freeBuildVoicelinesStage;

	public static int last_win_tune;

	public static int last_lose_tune;

	public readonly string[] buildingBinks = new string[16]
	{
		"st03_woodcutters_hut.webm", "st07_hunters_hut.webm", "st12_fletchers_workshop.webm", "st13_blacksmiths_workshop.webm", "st14_poleturners_workshop.webm", "st15_armourers_workshop.webm", "st16_tanners_workshop.webm", "st17_bakers_workshop.webm", "st22_inn.webm", "st26_tradepost.webm",
		"st30_wheatfarm.webm", "st32_applefarm.webm", "st33_cattlefarm.webm", "st34_mill.webm", "st36_church1.webm", "st37_mosque1.webm"
	};

	public readonly string[] characterBinks = new string[37]
	{
		"pg_anger1.webm", "pg_plead1.webm", "pg_plead2.webm", "pg_taunt1.webm", "pg_taunt2.webm", "pg_vict1.webm", "pg_vict2.webm", "pg_vict3.webm", "rt_anger1.webm", "rt_plead1.webm",
		"rt_plead2.webm", "rt_plead3.webm", "rt_taunt1.webm", "rt_taunt2.webm", "rt_vict1.webm", "rt_vict2.webm", "sn_anger1.webm", "sn_plead1.webm", "sn_plead2.webm", "sn_taunt1.webm",
		"sn_taunt2.webm", "sn_vict1.webm", "sn_vict2.webm", "wf_anger1.webm", "wf_plead1.webm", "wf_plead2.webm", "wf_taunt1.webm", "wf_taunt2.webm", "wf_vict1.webm", "wf_vict2.webm",
		"sgt_angry_1.webm", "sgt_confident_1.webm", "sgt_nervous_1.webm", "sgt_neutral_1.webm", "sgt_taunt_1.webm", "sgt_confident_2.webm", "sgt_neutral_2.webm"
	};

	public readonly string[] eventBinks = new string[15]
	{
		"action_apples_die.webm", "action_archers.webm", "action_bandits.webm", "action_fair.webm", "action_fire.webm", "action_hops_die.webm", "action_jester.webm", "action_mad_cows.webm", "action_marriage.webm", "action_plague.webm",
		"action_rabbits.webm", "action_steal_bread.webm", "action_trees_die.webm", "action_wheat_die.webm", "message_default.webm"
	};

	public readonly string[] lordBinks = new string[96]
	{
		"abbot_angry.webm", "abbot_confident.webm", "abbot_natural.webm", "abbot_nervous.webm", "bad_arab_anger.webm", "bad_arab_natural.webm", "bad_arab_nervous.webm", "bad_arab_taunt.webm", "bad_soldier_nevous.webm", "bad_soldier_taunt.webm",
		"canary_angry.webm", "canary_happy.webm", "canary_neutral.webm", "canary_sad.webm", "crocodile_angry.webm", "crocodile_happy.webm", "crocodile_neutral.webm", "crocodile_sad.webm", "emir_angry.webm", "emir_natural.webm",
		"emir_nervous.webm", "emir_taunt.webm", "fred_anger.webm", "fred_natural.webm", "fred_nervous.webm", "fred_taunt.webm", "good_soldier_nervous.webm", "good_soldier_taunt.webm", "jewel_angry.webm", "jewel_happy.webm",
		"jewel_neutral.webm", "jewel_sad.webm", "kahin_angry.webm", "kahin_happy.webm", "kahin_neutral.webm", "kahin_sad.webm", "lioness_angry.webm", "lioness_happy.webm", "lioness_neutral.webm", "lioness_sad.webm",
		"ma_angry.webm", "ma_natural.webm", "ma_nervous.webm", "ma_taunt.webm", "nazir_angry.webm", "nazir_natural.webm", "nazir_nervous.webm", "nazir_taunt.webm", "nomad_angry.webm", "nomad_happy.webm",
		"nomad_neutral.webm", "nomad_sad.webm", "philip_anger.webm", "philip_natural.webm", "philip_nervous.webm", "philip_taunt.webm", "richard_anger.webm", "richard_natural.webm", "richard_nervous.webm", "richard_taunting.webm",
		"saladin_angry.webm", "saladin_natural.webm", "saladin_nervous.webm", "saladin_taunting.webm", "sentinel_angry.webm", "sentinel_happy.webm", "sentinel_neutral.webm", "sentinel_sad.webm", "sergeant_angry.webm", "sergeant_happy.webm",
		"sergeant_neutral.webm", "sergeant_sad.webm", "sheriff_anger.webm", "sheriff_natural.webm", "sheriff_nervous.webm", "sheriff_taunt.webm", "sultan_anger.webm", "sultan_natural.webm", "sultan_nervous.webm", "sultan_taunt.webm",
		"trader_angry.webm", "trader_happy.webm", "trader_neutral.webm", "trader_sad.webm", "vizir_angry.webm", "vizir_natural.webm", "vizir_nervous.webm", "vizir_taunt.webm", "baldwin_angry.webm", "baldwin_happy.webm",
		"baldwin_neutral.webm", "baldwin_sad.webm", "bullseye_angry.webm", "bullseye_happy.webm", "bullseye_neutral.webm", "bullseye_sad.webm"
	};

	public Dictionary<string, string> customLordDefaultBinks = new Dictionary<string, string>
	{
		["SK_TAUNT1"] = "bad_soldier_taunt.webm",
		["SK_TAUNT2"] = "bad_soldier_taunt.webm",
		["SK_TAUNT3"] = "bad_soldier_taunt.webm",
		["SK_TAUNT4"] = "bad_soldier_taunt.webm",
		["SK_ANGRY_SIEGE_LOST"] = "bad_soldier_taunt.webm",
		["SK_ANGRY_CASTLE_DAMAGED"] = "bad_soldier_taunt.webm",
		["SK_DEFEAT"] = "bad_soldier_taunt.webm",
		["SK_NERV_PRE_SIEGE"] = "bad_soldier_taunt.webm",
		["SK_NERV_WEAK"] = "bad_soldier_nevous.webm",
		["SK_VICTORY_GOOD"] = "bad_soldier_taunt.webm",
		["SK_VICTORY_HARASS"] = "bad_soldier_taunt.webm",
		["SK_KILL_PLAYER"] = "bad_soldier_taunt.webm",
		["SK_KILL_NPC"] = "bad_soldier_taunt.webm",
		["SK_REQUEST_GOODS"] = "bad_soldier_taunt.webm",
		["SK_THANK_GOODS"] = "bad_soldier_taunt.webm",
		["SK_DIE_ALLY"] = "bad_soldier_nevous.webm",
		["SK_CONGRATS_ON_KILL"] = "bad_soldier_taunt.webm",
		["SK_BOAST_OF_KILL"] = "bad_soldier_taunt.webm",
		["SK_ALLY_NEED_HELP"] = "bad_soldier_nevous.webm",
		["SK_ABOUT2SIEGE"] = "bad_soldier_taunt.webm",
		["SK_CANT_ATTACK"] = "bad_soldier_nevous.webm",
		["SK_WONT_ATTACK"] = "bad_soldier_taunt.webm",
		["SK_CANT_HELP"] = "bad_soldier_taunt.webm",
		["SK_WONT_HELP"] = "bad_soldier_taunt.webm",
		["SK_NOT_SENDING_GOODS"] = "bad_soldier_taunt.webm",
		["SK_SENT_GOODS"] = "bad_soldier_taunt.webm",
		["SK_TEAM_WINNING"] = "bad_soldier_taunt.webm",
		["SK_TEAM_LOSING"] = "bad_soldier_nevous.webm",
		["SK_WILL_SEND_TROOPS"] = "bad_soldier_taunt.webm",
		["SK_WILL_ATTACK_ENEMY"] = "bad_soldier_taunt.webm"
	};

	public Dictionary<string, string> customLordCustomBinks = new Dictionary<string, string>
	{
		["SK_TAUNT1"] = "angry.webm",
		["SK_TAUNT2"] = "angry.webm",
		["SK_TAUNT3"] = "neutral.webm",
		["SK_TAUNT4"] = "angry.webm",
		["SK_ANGRY_SIEGE_LOST"] = "angry.webm",
		["SK_ANGRY_CASTLE_DAMAGED"] = "sad.webm",
		["SK_DEFEAT"] = "sad.webm",
		["SK_NERV_PRE_SIEGE"] = "neutral.webm",
		["SK_NERV_WEAK"] = "neutral.webm",
		["SK_VICTORY_GOOD"] = "happy.webm",
		["SK_VICTORY_HARASS"] = "angry.webm",
		["SK_KILL_PLAYER"] = "angry.webm",
		["SK_KILL_NPC"] = "neutral.webm",
		["SK_REQUEST_GOODS"] = "neutral.webm",
		["SK_THANK_GOODS"] = "happy.webm",
		["SK_DIE_ALLY"] = "sad.webm",
		["SK_CONGRATS_ON_KILL"] = "happy.webm",
		["SK_BOAST_OF_KILL"] = "happy.webm",
		["SK_ALLY_NEED_HELP"] = "neutral.webm",
		["SK_ABOUT2SIEGE"] = "neutral.webm",
		["SK_CANT_ATTACK"] = "sad.webm",
		["SK_WONT_ATTACK"] = "sad.webm",
		["SK_CANT_HELP"] = "sad.webm",
		["SK_WONT_HELP"] = "angry.webm",
		["SK_NOT_SENDING_GOODS"] = "angry.webm",
		["SK_SENT_GOODS"] = "neutral.webm",
		["SK_TEAM_WINNING"] = "happy.webm",
		["SK_TEAM_LOSING"] = "sad.webm",
		["SK_WILL_SEND_TROOPS"] = "neutral.webm",
		["SK_WILL_ATTACK_ENEMY"] = "happy.webm"
	};

	public Dictionary<string, string> binkFolders = new Dictionary<string, string>();

	public bool binkStarted;

	public bool binkLooping;

	public float binkVolume = 1f;

	public int requestBinkPlayState;

	public Uri requestBinkPlaybackURI;

	public bool binkIsPlaying;

	public bool binkWaitForSpeech;

	public static void InitSoundFX()
	{
		if (instance == null)
		{
			instance = new SFXManager();
			instance.init();
		}
	}

	public void init()
	{
		Object obj = Resources.Load("fx/volume");
		string[] array = ((TextAsset)((obj is TextAsset) ? obj : null)).text.Replace("\r", "").Split('\n', StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].Length <= 5)
			{
				continue;
			}
			string text = array[i];
			if (!text.StartsWith("---"))
			{
				continue;
			}
			int num = 0;
			string[] array2 = text.Split('=', StringSplitOptions.None);
			if (array2.Length > 1)
			{
				string text2 = array2[1].Replace(" ", "");
				if (text2.StartsWith('-'))
				{
					text2 = "-" + text2.Replace("-", "");
					num = int.Parse(text2, Director.defaultCulture);
				}
				else
				{
					num = int.Parse(text2.Replace("-", ""), Director.defaultCulture);
				}
			}
			i++;
			while (i < array.Length)
			{
				text = array[i];
				if (text.StartsWith("---"))
				{
					i--;
					break;
				}
				if (array[i].Length < 5)
				{
					i++;
					continue;
				}
				string[] array3 = array[i].Split('"', StringSplitOptions.None);
				int num2 = 0;
				if (array3[0].Length == 0)
				{
					num2++;
				}
				string text3 = array3[num2];
				int num3 = int.Parse(array3[1 + num2].Replace(" ", "").Replace("\t", ""), Director.defaultCulture);
				VolumeData volumeData = new VolumeData();
				volumeData.name = text3;
				volumeData.volume = (float)(num3 + num) / 127f;
				if (volumeData.volume < 0f)
				{
					volumeData.volume = 0f;
				}
				else if (volumeData.volume > 1f)
				{
					volumeData.volume = 1f;
				}
				this.volumeData[text3.Replace(".wav", "").ToLowerInvariant()] = volumeData;
				i++;
			}
		}
	}

	public void init2()
	{
		if (soundsLoaded)
		{
			return;
		}
		soundsLoaded = true;
		for (int i = 0; stronghold_main_list[i, 0].Length != 0; i++)
		{
			sh1_sound_effect sh1_sound_effect2 = new sh1_sound_effect();
			fx_list.Add(sh1_sound_effect2);
			sh1_sound_effect2.first_buffer_no = -1;
			sh1_sound_effect2.max_variants = 0;
			sh1_sound_effect2.variants_loaded = 0;
			sh1_sound_effect2.last_variant_played = 0;
			for (int j = 0; j < 10 && !(stronghold_main_list[i, j].ToLowerInvariant() == "null"); j++)
			{
				sh1_sound_effect2.max_variants++;
			}
			for (int k = 0; k < 10 && !(stronghold_main_list[i, k].ToLowerInvariant() == "null"); k++)
			{
				AudioClip val = Resources.Load<AudioClip>(stronghold_main_list[i, k]);
				val.LoadAudioData();
				float num = 1f;
				string text = stronghold_main_list[i, k].ToLowerInvariant();
				if (volumeData.ContainsKey(text))
				{
					num = volumeData[text].volume;
				}
				else
				{
					Debug.Log((object)("Missing SFX Volume : " + text));
					num = 0f;
				}
				int count = play_list.Count;
				sh1_sound sh1_sound2 = new sh1_sound();
				sh1_sound2.volume = 1f;
				sh1_sound2.position = 64;
				sh1_sound2.clip = val;
				sh1_sound2.volume = (sh1_sound2.real_volume = num);
				play_list.Add(sh1_sound2);
				sh1_sound_effect2.variants_loaded++;
				if (sh1_sound_effect2.first_buffer_no == -1)
				{
					sh1_sound_effect2.first_buffer_no = count;
				}
			}
		}
		for (int i = 0; stronghold_ambient_list[i, 0].Length != 0; i++)
		{
			sh1_sound_effect sh1_sound_effect3 = new sh1_sound_effect();
			ambient_list.Add(sh1_sound_effect3);
			sh1_sound_effect3.first_buffer_no = -1;
			sh1_sound_effect3.max_variants = 0;
			sh1_sound_effect3.variants_loaded = 0;
			sh1_sound_effect3.last_variant_played = 0;
			for (int l = 0; l < 8 && !(stronghold_ambient_list[i, l].ToLowerInvariant() == "null"); l++)
			{
				sh1_sound_effect3.max_variants++;
			}
			for (int m = 0; m < 8 && !(stronghold_ambient_list[i, m].ToLowerInvariant() == "null"); m++)
			{
				AudioClip clip = Resources.Load<AudioClip>(stronghold_ambient_list[i, m]);
				float real_volume = 1f;
				string text2 = stronghold_ambient_list[i, m].ToLowerInvariant();
				if (volumeData.ContainsKey(text2))
				{
					real_volume = volumeData[text2].volume;
				}
				else
				{
					Debug.Log((object)("Missing Ambient Volume : " + text2));
				}
				int count2 = play_list.Count;
				sh1_sound sh1_sound3 = new sh1_sound();
				sh1_sound3.volume = 1f;
				sh1_sound3.position = 64;
				sh1_sound3.clip = clip;
				sh1_sound3.volume = (sh1_sound3.real_volume = real_volume);
				play_list.Add(sh1_sound3);
				sh1_sound_effect3.variants_loaded++;
				if (sh1_sound_effect3.first_buffer_no == -1)
				{
					sh1_sound_effect3.first_buffer_no = count2;
				}
			}
		}
		string[] array = scribeSpeech;
		foreach (string text3 in array)
		{
			speechFolders[text3.ToLowerInvariant()] = "scribe";
		}
		array = aiSpeech;
		foreach (string text4 in array)
		{
			speechFolders[text4.ToLowerInvariant()] = "ai";
		}
		array = inMissionSpeech;
		foreach (string text5 in array)
		{
			speechFolders[text5.ToLowerInvariant()] = "inmission";
		}
		array = insultSpeech;
		foreach (string text6 in array)
		{
			speechFolders[text6.ToLowerInvariant()] = "insults";
		}
		array = nameSpeech;
		foreach (string text7 in array)
		{
			speechFolders[text7.ToLowerInvariant()] = "names";
		}
		array = peasantSpeech;
		foreach (string text8 in array)
		{
			speechFolders[text8.ToLowerInvariant()] = "peasants";
		}
		array = troopsSpeech;
		foreach (string text9 in array)
		{
			speechFolders[text9.ToLowerInvariant()] = "troops";
		}
		array = tutorialSpeech;
		foreach (string text10 in array)
		{
			speechFolders[text10.ToLowerInvariant()] = "tutorial";
		}
		array = campaignSpeech;
		foreach (string text11 in array)
		{
			speechFolders[text11.ToLowerInvariant()] = "campaign";
		}
		initBinkFolders();
	}

	public void playUISound(int soundID, float setVolume = 1f)
	{
		if (ConfigSettings.Settings_PlayUISFX && Application.isFocused)
		{
			playSound(soundID, setVolume);
		}
	}

	public void playUISoundVariant(int soundID, int variant, float setVolume = 1f)
	{
		if (ConfigSettings.Settings_PlayUISFX && Application.isFocused)
		{
			playSoundVariant(soundID, variant, setVolume);
		}
	}

	public void playSound(int soundID, float volumeOfset = 1f, float pan = 0f, bool unstoppable = false)
	{
		bool force = false;
		if ((uint)(soundID - 198) <= 1u || (uint)(soundID - 323) <= 2u)
		{
			force = true;
		}
		if (throttleSFX(soundID))
		{
			return;
		}
		if (soundID > 10000)
		{
			soundID -= 10000;
		}
		if (soundID < 0 || soundID >= fx_list.Count)
		{
			return;
		}
		sh1_sound_effect sh1_sound_effect2 = fx_list[soundID];
		if (sh1_sound_effect2.first_buffer_no < 0)
		{
			return;
		}
		if (sh1_sound_effect2.max_variants <= 1)
		{
			sh1_sound_effect2.last_variant_played = 0;
		}
		else if (isSFXRandomVariant(soundID))
		{
			sh1_sound_effect2.last_variant_played = Random.Range(0, sh1_sound_effect2.max_variants);
		}
		else
		{
			sh1_sound_effect2.last_variant_played++;
			if (sh1_sound_effect2.last_variant_played >= sh1_sound_effect2.max_variants)
			{
				sh1_sound_effect2.last_variant_played = 0;
			}
		}
		int index = sh1_sound_effect2.first_buffer_no + sh1_sound_effect2.last_variant_played;
		MyAudioManager.Instance.playSFX(play_list[index].clip, play_list[index].volume * volumeOfset, pan, unstoppable, force);
	}

	public bool isSFXRandomVariant(int soundID)
	{
		if ((uint)(soundID - 304) <= 2u)
		{
			return true;
		}
		return false;
	}

	public void playSoundVariant(int soundID, int variant, float volumeOfset = 1f, float pan = 0f, bool unstoppable = false)
	{
		if (throttleSFX(soundID))
		{
			return;
		}
		if (soundID > 10000)
		{
			soundID -= 10000;
		}
		if (soundID >= 0 && soundID < fx_list.Count)
		{
			sh1_sound_effect sh1_sound_effect2 = fx_list[soundID];
			if (sh1_sound_effect2.first_buffer_no >= 0 && variant >= 0 && variant < sh1_sound_effect2.max_variants)
			{
				int index = sh1_sound_effect2.first_buffer_no + variant;
				MyAudioManager.Instance.playSFX(play_list[index].clip, play_list[index].volume * volumeOfset, pan, unstoppable);
			}
		}
	}

	public bool throttleSFX(int soundID)
	{
		switch (soundID)
		{
		case 76:
			if (DateTime.UtcNow > nextAllowableMoo)
			{
				nextAllowableMoo = DateTime.UtcNow.AddSeconds(Random.Range(3, 10));
				break;
			}
			return true;
		case 12:
			if (DateTime.UtcNow > nextAllowableInn)
			{
				nextAllowableInn = DateTime.UtcNow.AddSeconds(Random.Range(3, 10));
				break;
			}
			return true;
		case 307:
			if (DateTime.UtcNow > nextAllowableDancingBear)
			{
				nextAllowableDancingBear = DateTime.UtcNow.AddSeconds(Random.Range(3, 10));
				break;
			}
			return true;
		case 144:
			if (DateTime.UtcNow > nextAllowableMaypole)
			{
				nextAllowableMaypole = DateTime.UtcNow.AddSeconds(Random.Range(3, 7));
				break;
			}
			return true;
		}
		return false;
	}

	public void playAmbient(int channel, int soundID, float volumeOfset, bool loop)
	{
		if (soundID < 0 || soundID >= ambient_list.Count)
		{
			return;
		}
		sh1_sound_effect sh1_sound_effect2 = ambient_list[soundID];
		if (sh1_sound_effect2.first_buffer_no >= 0)
		{
			sh1_sound_effect2.last_variant_played++;
			if (sh1_sound_effect2.last_variant_played >= sh1_sound_effect2.max_variants)
			{
				sh1_sound_effect2.last_variant_played = 0;
			}
			int index = sh1_sound_effect2.first_buffer_no + sh1_sound_effect2.last_variant_played;
			MyAudioManager.Instance.playAmbient(channel, play_list[index].clip, play_list[index].volume, volumeOfset, loop);
		}
	}

	public void playGenieSpeech(int channel, string fullpath, float volume)
	{
		if (ConfigSettings.Settings_GenieSpeech)
		{
			playSpeech(channel, fullpath, volume);
		}
	}

	public void delayPlaySpeech(int channel, string fullpath, float volume, bool ignoreSpeechMuting = false)
	{
		MyAudioManager.Instance.delayPlaySpeech(channel, fullpath, volume, ignoreSpeechMuting);
	}

	public void playSpeech(int channel, string fullpath, float volume, bool ignoreSpeechMuting = false, bool ignorePauseState = false)
	{
		if (fullpath.Length > 0 && fullpath.Contains('*'))
		{
			string[] array = fullpath.Split('*', StringSplitOptions.None);
			if (array.Length != 3)
			{
				return;
			}
			int num = int.Parse(array[1]);
			if (CustomisationFileManager.CustomMediaExists && ((MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null && MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.aivs != null) || MainViewModel.Instance.HUDIngameMenu.restartMPInfo != null))
			{
				string text = Path.Combine(path2: (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo == null) ? MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartMPInfo.LordNames[num - 1]) : MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.aivs[num - 1].lordName), path1: ConfigSettings.GetUserCustomMediaPath());
				if (Directory.Exists(text))
				{
					string text2 = array[2].Substring(3, array[2].Length - 3);
					string text3 = Path.Combine(text, text2 + ".wav");
					if (File.Exists(text3))
					{
						MyAudioManager.Instance.PlaySpeech(channel, "*", text3, force: true, unitsSpeech: false, ignoreSpeechMuting, ignorePauseState);
						return;
					}
				}
				fullpath = customLordDefaultSpeech[array[2]];
			}
			else
			{
				fullpath = customLordDefaultSpeech[array[2]];
			}
		}
		fullpath = manageTutorialSpeechAndMissingSpeech(fullpath);
		if (fullpath == "")
		{
			return;
		}
		string text4 = fullpath.ToLowerInvariant();
		string[] array2 = text4.Split('\\', StringSplitOptions.None);
		if (array2.Length == 0)
		{
			return;
		}
		text4 = array2[array2.Length - 1];
		if (!text4.Contains("null.") && speechFolders.ContainsKey(text4))
		{
			bool unitsSpeech = false;
			string text5 = speechFolders[text4];
			if (text5 == "troops")
			{
				unitsSpeech = true;
			}
			MyAudioManager.Instance.PlaySpeech(channel, text5, text4, force: true, unitsSpeech, ignoreSpeechMuting, ignorePauseState);
		}
	}

	public string manageTutorialSpeechAndMissingSpeech(string fullpath)
	{
		string text = fullpath.ToLowerInvariant();
		if (text.Contains("tutorial"))
		{
			if (text.EndsWith("tutorial_2.wav"))
			{
				if (!ConfigSettings.Settings_PushMapScrolling)
				{
					return text.Replace("tutorial_2.wav", "tutorial_2a.wav");
				}
			}
			else if (text.EndsWith("tutorial_29.wav"))
			{
				if (!ConfigSettings.Settings_SH1MouseWheel)
				{
					return text.Replace("tutorial_29.wav", "tutorial_29a.wav");
				}
			}
			else
			{
				if (text.EndsWith("tutorial_31a.wav"))
				{
					return text.Replace("tutorial_31a.wav", "tutorial_31.wav");
				}
				if (text.EndsWith("tutorial_31b.wav"))
				{
					return "";
				}
			}
		}
		else
		{
			if (text.Contains("de_dlc4_m5_1"))
			{
				return text.Replace("de_dlc4_m5_1", "de_dlc0_0");
			}
			if (text.Contains("de_dlc4_m5_2"))
			{
				return text.Replace("de_dlc4_m5_2", "de_dlc0_0");
			}
			if (text.Contains("de_dlc4_m5_3"))
			{
				return text.Replace("de_dlc4_m5_3", "de_dlc0_0");
			}
		}
		return fullpath;
	}

	public void playMusic(string fullpath, float gameVolume, bool loop, bool followon, bool fadePrevious = false, bool restartOnSamePiece = true)
	{
		if (!restartOnSamePiece && lastMusic == fullpath)
		{
			return;
		}
		lastMusic = fullpath;
		string text = fullpath.ToLowerInvariant();
		string[] array = text.Split('\\', StringSplitOptions.None);
		if (array.Length == 0)
		{
			return;
		}
		text = array[array.Length - 1];
		if (text == "null.raw")
		{
			MyAudioManager.Instance.stopMusic();
			return;
		}
		float soundVolume = 1f;
		string text2 = fullpath.ToLowerInvariant();
		if (volumeData.ContainsKey(text2))
		{
			soundVolume = volumeData[text2].volume;
		}
		else
		{
			Debug.Log((object)("Missing Music Volume : " + text2));
		}
		MyAudioManager.Instance.PlayMusic(text, gameVolume, soundVolume, loop, followon, fadePrevious);
	}

	public void playMusic(int ID, bool fadePrevious = false, float volume = 1f, bool restartOnSamePiece = true)
	{
		if (ID > 0 && ID < music_filenames.Length && music_filenames[ID].Length > 0)
		{
			string fullpath = music_filenames[ID];
			playMusic(fullpath, volume, loop: true, followon: false, fadePrevious, restartOnSamePiece);
		}
	}

	public float GetMusicVolume(string fullpath)
	{
		string[] array = fullpath.ToLowerInvariant().Split('\\', StringSplitOptions.None);
		if (array.Length != 0)
		{
			if (array[array.Length - 1] == "null.raw")
			{
				MyAudioManager.Instance.stopMusic();
				return 0f;
			}
			string key = fullpath.ToLowerInvariant();
			if (volumeData.ContainsKey(key))
			{
				return volumeData[key].volume;
			}
		}
		return 0f;
	}

	public void playIntroSpeech(string playerName)
	{
		bool flag = false;
		int month = DateTime.Now.Month;
		int day = DateTime.Now.Day;
		if (month == 12 && day == 14)
		{
			flag = true;
			delayPlaySpeech(1, "General_message18.wav", 100f, ignoreSpeechMuting: true);
		}
		else if (month == 12 && day == 25)
		{
			flag = true;
			delayPlaySpeech(1, "General_message17.wav", 100f, ignoreSpeechMuting: true);
		}
		else if (supportsAdditionalVoicelines())
		{
			if (month == 10 && day == 19)
			{
				flag = true;
				delayPlaySpeech(1, "General_message130.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 9 && day == 25)
			{
				flag = true;
				delayPlaySpeech(1, "General_message119.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 4 && day == 18)
			{
				flag = true;
				delayPlaySpeech(1, "General_message120.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 10 && day == 13)
			{
				flag = true;
				delayPlaySpeech(1, "General_message121.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 10 && day == 25)
			{
				flag = true;
				delayPlaySpeech(1, "General_message122.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 9 && day == 22)
			{
				flag = true;
				delayPlaySpeech(1, "General_message123.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 3 && day == 9)
			{
				flag = true;
				delayPlaySpeech(1, "General_message124.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 11 && day == 4)
			{
				flag = true;
				delayPlaySpeech(1, "General_message125.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 1 && day == 1)
			{
				flag = true;
				delayPlaySpeech(1, "General_message126.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 6 && day == 21)
			{
				flag = true;
				delayPlaySpeech(1, "General_message127.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 12 && day == 21)
			{
				flag = true;
				delayPlaySpeech(1, "General_message128.wav", 100f, ignoreSpeechMuting: true);
			}
			else if (month == 11 && day == GetThanksgivingDate().Day)
			{
				flag = true;
				delayPlaySpeech(1, "General_message129.wav", 100f, ignoreSpeechMuting: true);
			}
		}
		if (ConfigSettings.Settings_CustomIntros && !flag)
		{
			string text = playerName.ToLowerInvariant();
			for (int i = 0; i < stronghold_names_speech_list.Length / 2; i++)
			{
				if (!text.Contains(stronghold_names_speech_list[i * 2].ToLowerInvariant()))
				{
					continue;
				}
				int num = text.IndexOf(stronghold_names_speech_list[i * 2].ToLowerInvariant());
				if (num > 0)
				{
					if (!text.EndsWith(stronghold_names_speech_list[i * 2].ToLowerInvariant()) || text[num - 1] != ' ')
					{
						continue;
					}
				}
				else if (text.Length != stronghold_names_speech_list[i * 2].Length && text[stronghold_names_speech_list[i * 2].Length] != ' ')
				{
					continue;
				}
				delayPlaySpeech(1, stronghold_names_speech_list[i * 2 + 1], 100f, ignoreSpeechMuting: true);
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			delayPlaySpeech(1, "general_startgame.wav", 100f, ignoreSpeechMuting: true);
		}
	}

	public DateTime GetThanksgivingDate()
	{
		DateTime result = DateTime.Now;
		for (int i = 22; i <= 30; i++)
		{
			result = new DateTime(DateTime.Now.Year, 11, i, 0, 0, 0);
			if (result.DayOfWeek == DayOfWeek.Thursday)
			{
				break;
			}
		}
		return result;
	}

	public bool supportsAdditionalVoicelines()
	{
		switch (FatControler.locale)
		{
		case "zhcn":
		case "zhhk":
		case "jajp":
		case "kokr":
		case "ukua":
		case "cscz":
		case "elgr":
		case "thth":
		case "trtr":
		case "enus":
		case "nlnl":
		case "svse":
			return true;
		default:
			return false;
		}
	}

	public void playAdditionalSpeech(string speech, bool ignorePauseState = false)
	{
		if (supportsAdditionalVoicelines())
		{
			playSpeech(1, speech, 1f, ignorePauseState);
		}
	}

	public void resetFreebuildMessages()
	{
		freeBuildVoicelinesStart = DateTime.MinValue;
	}

	public void startFreebuildMessages()
	{
		if (supportsAdditionalVoicelines())
		{
			freeBuildVoicelinesStart = DateTime.UtcNow;
			freeBuildVoicelinesStage = 0;
		}
		else
		{
			freeBuildVoicelinesStart = DateTime.MinValue;
		}
	}

	public void Update()
	{
		if (!(freeBuildVoicelinesStart != DateTime.MinValue))
		{
			return;
		}
		TimeSpan timeSpan = DateTime.UtcNow - freeBuildVoicelinesStart;
		switch (freeBuildVoicelinesStage)
		{
		case 0:
			if (timeSpan.TotalMinutes > 60.0)
			{
				playAdditionalSpeech("Freebuild_Playtime_1.wav");
				freeBuildVoicelinesStage = 1;
			}
			break;
		case 1:
			if (timeSpan.TotalMinutes > 120.0)
			{
				playAdditionalSpeech("Freebuild_Playtime_2.wav");
				freeBuildVoicelinesStage = 2;
			}
			break;
		case 2:
			if (timeSpan.TotalMinutes > 300.0)
			{
				playAdditionalSpeech("Freebuild_Playtime_3.wav");
				freeBuildVoicelinesStart = DateTime.MinValue;
			}
			break;
		}
	}

	public void playInsult(int insult)
	{
		insult--;
		if (insult >= 0 && insult < 20)
		{
			playSpeech(1, insultSpeech[insult], 100f);
		}
	}

	public void PlayWinTune()
	{
		if (last_win_tune == 0)
		{
			playMusic("fx\\music\\bigwin1.raw", 1f, loop: false, followon: false);
		}
		else if (last_win_tune == 1)
		{
			playMusic("fx\\music\\bigwin2.raw", 1f, loop: false, followon: false);
		}
		else
		{
			playMusic("fx\\music\\bigwin3.raw", 1f, loop: false, followon: false);
		}
		last_win_tune++;
		if (last_win_tune > 2)
		{
			last_win_tune = 0;
		}
	}

	public void PlayLoseTune()
	{
		if (last_lose_tune == 0)
		{
			playMusic("fx\\music\\bigloss1.raw", 1f, loop: false, followon: false);
		}
		else
		{
			playMusic("fx\\music\\bigloss2.raw", 1f, loop: false, followon: false);
		}
		last_lose_tune++;
		if (last_lose_tune > 1)
		{
			last_lose_tune = 0;
		}
	}

	public void initBinkFolders()
	{
		string[] array = buildingBinks;
		foreach (string text in array)
		{
			binkFolders[text.ToLowerInvariant()] = "Buildings";
		}
		array = characterBinks;
		foreach (string text2 in array)
		{
			binkFolders[text2.ToLowerInvariant()] = "Characters";
		}
		array = eventBinks;
		foreach (string text3 in array)
		{
			binkFolders[text3.ToLowerInvariant()] = "Events";
		}
		array = lordBinks;
		foreach (string text4 in array)
		{
			binkFolders[text4.ToLowerInvariant()] = "Lords";
		}
	}

	public void playBink(string binkName, bool loop, bool waitForSpeech)
	{
		string text = null;
		if (binkName.Length > 0 && binkName.Contains('*'))
		{
			string[] array = binkName.Split('*', StringSplitOptions.None);
			binkName = customLordDefaultBinks[array[2]];
			if (array.Length != 3)
			{
				return;
			}
			int num = int.Parse(array[1]);
			if (CustomisationFileManager.CustomMediaExists && ((MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null && MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.aivs != null) || MainViewModel.Instance.HUDIngameMenu.restartMPInfo != null))
			{
				string text2 = Path.Combine(path2: (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo == null) ? MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartMPInfo.LordNames[num - 1]) : MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.aivs[num - 1].lordName), path1: ConfigSettings.GetUserCustomMediaPath());
				if (Directory.Exists(text2))
				{
					string path = customLordCustomBinks[array[2]];
					text = Path.Combine(text2, path);
					if (!File.Exists(text))
					{
						text = null;
					}
				}
			}
		}
		bool flag = binkIsPlaying;
		float num2 = getBinkVolume(binkName, processVolume: false);
		binkStarted = true;
		binkLooping = loop;
		binkVolume = num2;
		if (binkName.ToLowerInvariant().Contains("action_marriage") && GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
		{
			binkName = "st37_mosque1.bik";
		}
		binkName = binkName.Replace(".bik", ".webm");
		if (waitForSpeech && MyAudioManager.Instance.isSpeechPlaying(1))
		{
			binkWaitForSpeech = true;
		}
		else
		{
			binkWaitForSpeech = false;
		}
		string text3 = "";
		if (binkFolders.ContainsKey(binkName.ToLowerInvariant()))
		{
			text3 = binkFolders[binkName];
		}
		string uriString = Path.Combine("Assets", "GUI", "Video", text3, binkName);
		if (text != null)
		{
			uriString = text;
			requestBinkPlaybackURI = new Uri(uriString, UriKind.Absolute);
		}
		else
		{
			requestBinkPlaybackURI = new Uri(uriString, UriKind.Relative);
		}
		if (loop)
		{
			requestBinkPlayState = 2;
		}
		else
		{
			requestBinkPlayState = 1;
		}
		if (flag)
		{
			requestBinkPlayState = -requestBinkPlayState;
		}
	}

	public void stopBink()
	{
		if (binkStarted)
		{
			MainViewModel.Instance.HUDRoot.RadarME_Ended();
			requestBinkPlayState = 0;
			binkStarted = false;
			binkIsPlaying = true;
			FatControler.instance.binkPlayWait = false;
		}
	}

	public bool isBinkPlaying()
	{
		if (binkStarted)
		{
			if (binkLooping)
			{
				return true;
			}
			return binkIsPlaying;
		}
		return false;
	}

	public float getBinkVolume(string path, bool processVolume = true)
	{
		float num = 1f;
		switch (Path.GetFileNameWithoutExtension(Path.GetFileName(path)).ToLower())
		{
		case "wf_vict1":
			num = 0.27559054f;
			break;
		case "wf_vict2":
			num = 0.35433072f;
			break;
		case "wf_taunt1":
			num = 0.23622048f;
			break;
		case "wf_taunt2":
			num = 0.27559054f;
			break;
		case "wf_plead1":
			num = 0.27559054f;
			break;
		case "wf_anger1":
			num = 0.23622048f;
			break;
		case "ap_milit6":
			num = 0.27559054f;
			break;
		case "ap_milit7":
			num = 0.27559054f;
			break;
		case "ap_milit4":
			num = 0.31496063f;
			break;
		case "ap_milit5":
			num = 0.31496063f;
			break;
		case "ap_milit9":
			num = 0.31496063f;
			break;
		case "ap_milit12":
			num = 0.31496063f;
			break;
		case "rt_taunt1":
			num = 0.31496063f;
			break;
		case "rt_vict1":
			num = 0.31496063f;
			break;
		case "ap_milit1":
			num = 0.31496063f;
			break;
		case "pig_vict1":
			num = 0.31496063f;
			break;
		case "pig_vict2":
			num = 0.31496063f;
			break;
		case "pig_vict3":
			num = 0.31496063f;
			break;
		case "rt_plead3":
			num = 0.5905512f;
			break;
		case "rt_plead2":
			num = 0.31496063f;
			break;
		case "rt_anger1":
			num = 0.31496063f;
			break;
		case "rt_vict2":
			num = 0.31496063f;
			break;
		case "rt_plead1":
			num = 0.31496063f;
			break;
		case "rt_taunt2":
			num = 0.31496063f;
			break;
		case "well_not_everybody":
			num = 0.62992126f;
			break;
		case "st17_bakers_workshop":
			num = 0.62992126f;
			break;
		case "st16_tanners_workshop":
			num = 0.511811f;
			break;
		case "st15_armourers_workshop":
			num = 0.39370078f;
			break;
		case "st14_poleturners_workshop":
			num = 0.43307087f;
			break;
		case "st12_fletchers_workshop":
			num = 1f;
			break;
		case "st07_hunters_hut":
			num = 0.62992126f;
			break;
		case "st03_woodcutters_hut":
			num = 0.62992126f;
			break;
		case "action_steal_bread":
			num = 0.62992126f;
			break;
		case "action_rabbits":
			num = 0.62992126f;
			break;
		case "action_plague":
			num = 0.62992126f;
			break;
		case "action_mad_cows":
			num = 0.62992126f;
			break;
		case "action_fire":
			num = 0.62992126f;
			break;
		case "st13_blacksmiths_workshop":
			num = 0.43307087f;
			break;
		case "action_apples_die":
			num = 0.62992126f;
			break;
		case "intro":
			num = 1f;
			break;
		}
		if (processVolume)
		{
			return ConfigSettings.Settings_SFXVolume * MyAudioManager.GetMasterVolume() * num;
		}
		return num;
	}
}
