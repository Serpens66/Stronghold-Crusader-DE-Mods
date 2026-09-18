struct CrusaderDE_PlayerManagerSharedStateA
{
  uint8_t unknown_000000[808];
  uint32_t current_month;
  uint32_t current_year;
  uint8_t unknown_000330[20];
  uint8_t no_knockdown_walls_raw[8];
  uint8_t unknown_00034C[2608];
  uint8_t allowed_european_units_raw[16];
  uint8_t unknown_000D8C[48];
  uint8_t teams_list_raw[8];
  uint8_t unknown_000DC4[6692];
  uint8_t extreme_powers_enabled_raw[8];
  uint8_t unknown_0027F0[12];
  uint32_t local_player_state;
  uint8_t unknown_002800[252];
  uint8_t allowed_arabian_units_raw[16];
  uint8_t unknown_00290C[16];
  uint32_t signpost_building_ids[8];
  uint8_t unknown_00293C[16];
  uint32_t ai_siege_rally_point_x_by_player;
  uint32_t ai_siege_rally_point_y_by_player;
  uint8_t unknown_002954[8];
  CrusaderDE_SignpostPathAnchor signpost_path_anchors[16];
  uint8_t signpost_path_cache_a_raw[6400];
  uint8_t signpost_path_cache_b_raw[2560];
  uint8_t unknown_004D5C[242808];
  uint32_t instanced_skirmish_unit_spawn_queue_head;
  uint8_t unknown_0401D8[271004];
};

struct CrusaderDE_PlayerManagerSharedStateB
{
  CrusaderDE_PathMapOrigin path_map_origins[10];
  uint8_t unknown_0000A0[174692];
  uint8_t allowed_bedouin_units_raw[16];
  uint8_t unknown_02AB14[1530144];
};

struct CrusaderDE_PlayerManager
{
  uint8_t unknown_pre_tracking_state[440000];
  CrusaderDE_TrackedUnitHandle tracked_units_by_player[10][10000];
  PlayerResources players[9];
  uint8_t unknown_post_player_resources[133524];
  CrusaderDE_PlayerManagerSharedStateA shared_state_a;
  CrusaderDE_PlayerManagerSharedStateB shared_state_b;
  uint32_t global_tick_timer;
  CrusaderDE_PlayerBuildingIndexCache building_index_cache;
};

struct CrusaderDE_PlayerBuildingIndexCache
{
  uint16_t building_ids_by_player[9][500];
  uint32_t building_count_by_player[9];
};

struct CrusaderDE_SignpostPathAnchor
{
  int32_t tile_x;
  int32_t tile_y;
  int32_t tile_id;
  uint32_t unknown;
};

struct CrusaderDE_PathMapOrigin
{
  int32_t tile_x;
  int32_t tile_y;
  uint32_t unknown_08;
  uint32_t unknown_0C;
};

struct CrusaderDE_PathComponentPair
{
  int32_t component_a;
  int32_t component_b;
};

struct CrusaderDE_PathApproachCandidate
{
  int32_t tile_id;
  int32_t associated_tile_or_flags;
  int32_t unknown_08;
};

struct CrusaderDE_PathConnectionRecord
{
  uint32_t in_use;
  uint32_t kind;
  uint32_t global_id;
  uint32_t building_id;
  uint32_t unit_id;
  uint32_t object_global_id;
  uint32_t unknown_18;
  int32_t endpoint_a_x;
  int32_t endpoint_a_y;
  int32_t endpoint_a_tile_id;
  int32_t endpoint_b_x;
  int32_t endpoint_b_y;
  int32_t endpoint_b_tile_id;
  int32_t endpoint_a_component_id;
  int32_t endpoint_b_component_id;
  uint32_t registered_unit_count;
  int32_t registered_unit_ids[50];
  int32_t registered_unit_global_ids[50];
  int32_t kind_specific_value;
  uint8_t unknown_1D4[16];
  int32_t owner_player_id;
  int32_t extra_component_or_state;
  uint8_t unknown_1EC[24];
};

struct CrusaderDE_PathSearchState
{
  uint32_t deferred_reciprocal_edge_count;
  uint32_t current_distance_or_generation;
  uint32_t queue_head;
  uint32_t unknown_0C;
  uint32_t queue_tail;
  uint8_t unknown_14[36];
};

struct CrusaderDE_PathfindingHeader
{
  uint32_t connection_record_limit_or_count;
  uint32_t walk_generation;
  uint8_t unknown_008[16];
  int32_t result_tile_x;
  int32_t result_tile_y;
  uint8_t unknown_020[36];
  int32_t random_tile_x;
  int32_t random_tile_y;
  uint8_t unknown_04C[32];
  uint32_t rebuild_requested;
  uint32_t unknown_070;
  uint32_t rebuild_serial;
  uint8_t unknown_078[24];
  uint32_t component_pair_cache_initialized;
  uint8_t unknown_094[44];
  uint32_t connection_unit_tracking_enabled;
  uint32_t component_graph_search_generation;
  uint32_t unknown_C8;
  uint32_t component_count;
  uint8_t unknown_D0[8];
  uint32_t connection_table_state;
  uint32_t unknown_DC;
};

struct CrusaderDE_PathfindingContext
{
  CrusaderDE_PathfindingHeader header;
  uint32_t component_tile_counts[1000];
  uint32_t total_labelled_tiles;
  uint32_t component_visit_generation[1000];
  CrusaderDE_PathConnectionRecord connection_records[200];
  CrusaderDE_PathApproachCandidate approach_candidates[500];
  uint32_t deferred_reciprocal_edge_tile_ids[320800];
  CrusaderDE_PathSearchState search_state;
  int32_t bfs_tile_ids[320800];
  int16_t bfs_tile_y[320800];
  int16_t bfs_tile_x[320800];
  int32_t candidate_tile_ids[40100];
  int16_t candidate_tile_y[40100];
  int16_t candidate_tile_x[40100];
  CrusaderDE_PathComponentPair reachable_component_pairs[10];
  CrusaderDE_PathComponentPair unreachable_component_pairs[10];
};
