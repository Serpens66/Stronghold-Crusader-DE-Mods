using System.Collections;
using System.Collections.Generic;
using CrusaderDE;
using UnityEngine;

public class spriteLoader : MonoBehaviour
{
	private Dictionary<string, Sprite> allSprites = new Dictionary<string, Sprite>();

	private Sprite[][] gmSprites = new Sprite[234][];

	private Sprite[][] gmAltSprites = new Sprite[234][];

	private Material[][] gmMaterials = new Material[234][];

	private Color[][] gmColors = new Color[234][];

	public static spriteLoader instance;

	public bool spritesLoaded;

	private readonly Color[] defaultColours = new Color[10]
	{
		new Color(0.69f, 0.59f, 0.17f),
		new Color(0.8f, 0.109f, 0.219f),
		new Color(0.058f, 0.572f, 0.831f),
		new Color(0.945f, 0.478f, 0.121f),
		new Color(1f, 0.823f, 0.137f),
		new Color(0.654f, 0.156f, 0.764f),
		new Color(0.435f, 0.435f, 0.435f),
		new Color(0.274f, 0.992f, 0.984f),
		new Color(0.301f, 0.831f, 0.129f),
		new Color(0.435f, 0.435f, 0.435f)
	};

	private readonly Color[] lordLadyColours = new Color[10]
	{
		new Color(0.058f, 0.572f, 0.831f),
		new Color(0.8f, 0.109f, 0.219f),
		new Color(0.058f, 0.572f, 0.831f),
		new Color(0.945f, 0.478f, 0.121f),
		new Color(1f, 0.823f, 0.137f),
		new Color(0.654f, 0.156f, 0.764f),
		new Color(0.435f, 0.435f, 0.435f),
		new Color(0.274f, 0.992f, 0.984f),
		new Color(0.301f, 0.831f, 0.129f),
		new Color(0.435f, 0.435f, 0.435f)
	};

	private readonly Color[] jesterColours = new Color[10]
	{
		new Color(0.8f, 0.109f, 0.219f),
		new Color(0.8f, 0.109f, 0.219f),
		new Color(0.058f, 0.572f, 0.831f),
		new Color(0.945f, 0.478f, 0.121f),
		new Color(0.45f, 0.898f, 0.317f),
		new Color(0.654f, 0.156f, 0.764f),
		new Color(0.435f, 0.435f, 0.435f),
		new Color(0.274f, 0.992f, 0.984f),
		new Color(0.301f, 0.831f, 0.129f),
		new Color(0.435f, 0.435f, 0.435f)
	};

	private readonly Color[] knightColours = new Color[10]
	{
		new Color(0.8962f, 0.5055f, 0.1817f),
		new Color(0.6698f, 0.6698f, 0.6698f),
		new Color(0.8867f, 0.8867f, 0.8867f),
		new Color(0.7264f, 0.3316f, 0.161f),
		new Color(1f, 0.964f, 0.964f),
		new Color(1f, 1f, 1f),
		new Color(0.3207f, 0.3207f, 0.3207f),
		new Color(0.89f, 0.607f, 0.423f),
		new Color(1f, 0.5063f, 0.2877f),
		new Color(0.3207f, 0.3207f, 0.3207f)
	};

	private readonly Color[] animalColours = new Color[10]
	{
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f)
	};

	private readonly Color[] rabbitColours = new Color[10]
	{
		new Color(1f, 1f, 1f),
		new Color(0.941f, 0.682f, 0.494f),
		new Color(0.737f, 0.509f, 0.356f),
		new Color(0.584f, 0.38f, 0.247f),
		new Color(1f, 0.964f, 0.964f),
		new Color(0.713f, 0.756f, 0.831f),
		new Color(0.862f, 0.862f, 0.862f),
		new Color(0.89f, 0.607f, 0.423f),
		new Color(0.823f, 0.8f, 0.827f),
		new Color(0.862f, 0.862f, 0.862f)
	};

	private readonly Color[] foliageColours = new Color[10]
	{
		new Color(0.8f, 0.109f, 0.219f),
		new Color(0.058f, 0.572f, 0.831f),
		new Color(0.8f, 0.109f, 0.219f),
		new Color(0.945f, 0.478f, 0.121f),
		new Color(0.45f, 0.898f, 0.317f),
		new Color(0.654f, 0.156f, 0.764f),
		new Color(0.435f, 0.435f, 0.435f),
		new Color(0.274f, 0.992f, 0.984f),
		new Color(0.301f, 0.831f, 0.129f),
		new Color(0.435f, 0.435f, 0.435f)
	};

	private readonly Color[] pineFoliageColours = new Color[10]
	{
		new Color(0.51f, 0.525f, 0.506f),
		new Color(0.502f, 0.525f, 0.498f),
		new Color(0.51f, 0.525f, 0.49f),
		new Color(0.51f, 0.447f, 0.506f),
		new Color(0.51f, 0.569f, 0.494f),
		new Color(0.498f, 0.525f, 0.506f),
		new Color(0.51f, 0.553f, 0.533f),
		new Color(0.51f, 0.518f, 0.475f),
		new Color(0.506f, 0.482f, 0.506f),
		new Color(0.51f, 0.455f, 0.506f)
	};

	private readonly Color[] oakFoliageColours = new Color[10]
	{
		new Color(0.518f, 0.478f, 0.49f),
		new Color(0.518f, 0.478f, 0.475f),
		new Color(0.518f, 0.431f, 0.49f),
		new Color(0.506f, 0.478f, 0.49f),
		new Color(0.518f, 0.494f, 0.51f),
		new Color(0.506f, 0.537f, 0.482f),
		new Color(0.525f, 0.451f, 0.49f),
		new Color(0.51f, 0.486f, 0.506f),
		new Color(0.518f, 0.447f, 0.545f),
		new Color(0.518f, 0.561f, 0.498f)
	};

	private readonly Color[] shrub1FoliageColours = new Color[10]
	{
		new Color(0.5f, 0.539f, 0.617f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(0.404f, 0.546f, 0.681f),
		new Color(0.454f, 0.517f, 0.681f),
		new Color(1f, 1f, 1f),
		new Color(1f, 1f, 1f),
		new Color(0.426f, 0.596f, 0.716f),
		new Color(0.56f, 0.333f, 0.645f),
		new Color(0.5f, 0.5f, 0.5f)
	};

	private readonly Color[] shrub2FoliageColours = new Color[10]
	{
		new Color(0.496f, 0.564f, 0.472f),
		new Color(0.475f, 0.592f, 0.451f),
		new Color(0.475f, 0.546f, 0.482f),
		new Color(0.475f, 0.56f, 0.461f),
		new Color(0.461f, 0.539f, 0.454f),
		new Color(0.496f, 0.538f, 0.475f),
		new Color(0.496f, 0.592f, 0.451f),
		new Color(0.504f, 0.574f, 0.475f),
		new Color(0.489f, 0.649f, 0.461f),
		new Color(0.496f, 0.592f, 0.451f)
	};

	private readonly Color[] appleFoliageColours = new Color[10]
	{
		new Color(0.5f, 0.5f, 0.55f),
		new Color(0.525f, 0.511f, 0.5f),
		new Color(0.507f, 0.5f, 0.521f),
		new Color(0.514f, 0.472f, 0.521f),
		new Color(0.493f, 0.539f, 0.571f),
		new Color(0.507f, 0.461f, 0.507f),
		new Color(0.5f, 0.5f, 0.5f),
		new Color(0.5f, 0.529f, 0.521f),
		new Color(0.5f, 0.532f, 0.532f),
		new Color(0.5f, 0.5f, 0.5f)
	};

	private readonly Color[] birchFoliageColours = new Color[10]
	{
		new Color(0.514f, 0.608f, 0.494f),
		new Color(0.514f, 0.51f, 0.502f),
		new Color(0.506f, 0.565f, 0.494f),
		new Color(0.514f, 0.584f, 0.494f),
		new Color(0.514f, 0.608f, 0.478f),
		new Color(0.514f, 0.529f, 0.506f),
		new Color(0.494f, 0.576f, 0.494f),
		new Color(0.494f, 0.576f, 0.494f),
		new Color(0.506f, 0.561f, 0.51f),
		new Color(0.506f, 0.561f, 0.51f)
	};

	private readonly Color[] chestnutFoliageColours = new Color[10]
	{
		new Color(0.498f, 0.553f, 0.514f),
		new Color(0.51f, 0.498f, 0.518f),
		new Color(0.498f, 0.537f, 0.518f),
		new Color(0.498f, 0.467f, 0.537f),
		new Color(0.49f, 0.525f, 0.553f),
		new Color(0.498f, 0.58f, 0.545f),
		new Color(0.498f, 0.525f, 0.533f),
		new Color(0.498f, 0.525f, 0.518f),
		new Color(0.498f, 0.525f, 0.525f),
		new Color(0.498f, 0.525f, 0.525f)
	};

	private Dictionary<string, string> spriteToFileMatch = new Dictionary<string, string>();

	private Dictionary<string, Material[]> pathToMaterialArray = new Dictionary<string, Material[]>();

	public Material[] plainMaterials = new Material[7];

	public Material defaultMaterial;

	public Sprite[] treeAnim;

	private void Awake()
	{
		instance = this;
		if (instance == null)
		{
			instance = this;
		}
		else if (instance != this)
		{
			Object.Destroy(base.gameObject);
		}
	}

	public void loadSprites()
	{
		StartCoroutine(SpriteLoad());
	}

	private IEnumerator SpriteLoad()
	{
		initPlainMaterial();
		yield return loadSprites("Sprites/AllTiles/AllTileSprites");
		yield return loadSprites("Sprites/treeSprites", makeMask: true, foliage: true);
		yield return loadSprites("Sprites/body1Sprites", makeMask: true);
		yield return loadSprites("Sprites/body2Sprites", makeMask: true);
		yield return loadSprites("Sprites/body4Sprites", makeMask: true);
		yield return loadSprites("Sprites/body5Sprites", makeMask: true);
		yield return loadSprites("Sprites/body6Sprites", makeMask: true);
		yield return loadSprites("Sprites/body7Sprites", makeMask: true);
		yield return loadSprites("Sprites/body8Sprites", makeMask: true);
		yield return loadSprites("Sprites/body12Sprites", makeMask: true);
		yield return loadSprites("Sprites/body13Sprites", makeMask: true);
		yield return loadSprites("Sprites/body14Sprites", makeMask: true);
		yield return loadSprites("Sprites/body15Sprites", makeMask: true);
		yield return loadSprites("Sprites/body16Sprites", makeMask: true);
		yield return loadSprites("Sprites/anims1Sprites", makeMask: true);
		addGMFile(512, "tile_land8", Enums.GM.GM_LAND, dashFormat: false, plainShader: true);
		addGMFile(1864, "tile_sea8", Enums.GM.GM_SEA, dashFormat: false, plainShader: true);
		addGMFile(1752, "tile_buildings1", Enums.GM.GM_BUILDINGS1, dashFormat: false, plainShader: true);
		addGMFile(1359, "tile_buildings2", Enums.GM.GM_BUILDINGS2, dashFormat: false, plainShader: true);
		addGMFile(2016, "tile_workshops", Enums.GM.GM_WORKSHOPS, dashFormat: false, plainShader: true);
		addGMFile(315, "tile_land3", Enums.GM.GM_MISC_LAND, dashFormat: false, plainShader: true);
		addGMFile(62, "tile_farmland", Enums.GM.GM_FARMLAND, dashFormat: false, plainShader: true);
		addGMFile(1032, "tile_goods", Enums.GM.GM_GOODS, dashFormat: false, plainShader: true);
		addGMFile(741, "tile_churches", Enums.GM.GM_CHURCHS, dashFormat: false, plainShader: true);
		addGMFile(1596, "tile_castle", Enums.GM.GM_CASTLES, dashFormat: false, plainShader: true);
		addGMFile(1648, "tile_land_macros", Enums.GM.GM_MACRO_LAND, dashFormat: false, plainShader: true);
		addGMFile(740, "tile_rocks8", Enums.GM.GM_ROCKS, dashFormat: false, plainShader: true);
		addGMFile(512, "tile_land_and_stones", Enums.GM.GM_LAND_AND_STONES, dashFormat: false, plainShader: true);
		addGMFile(16, "killing_pits", Enums.GM.GM_KILLING_PITS, dashFormat: false, plainShader: true);
		addGMFile(8, "pitch_ditches", Enums.GM.GM_PITCH_DITCHES, dashFormat: false, plainShader: true);
		addGMFile(638, "tile_ruins", Enums.GM.GM_TILE_RUINS, dashFormat: false, plainShader: true);
		addGMFile(2167, "tile_sea_new_01", Enums.GM.GM_NEW_SEA, dashFormat: false, plainShader: true, null, 0, 816);
		addGMFile(816, "tile_sea_shore", Enums.GM.GM_NEW_SEA, dashFormat: false, plainShader: true, null, 2167, -1);
		addGMFile(46, "tile_flatties", Enums.GM.GM_TILE_FLATTIES, dashFormat: false, plainShader: true);
		addGMFile(512, "tile_chevrons", Enums.GM.GM_PILLARS, dashFormat: false, plainShader: true);
		addGMFile(66, "tile_cliffs", Enums.GM.GM_CLIFFS, dashFormat: false, plainShader: true);
		addGMFile(80, "tile_walls", Enums.GM.GM_WALLS, dashFormat: false, plainShader: true);
		addGMFile(147, "Tree_Oak", Enums.GM.GM_TREE_OAK, dashFormat: true, plainShader: false, oakFoliageColours);
		addGMFile(147, "tree_pine", Enums.GM.GM_TREE_PINE, dashFormat: true, plainShader: false, pineFoliageColours);
		addGMFile(24, "tree_shrub1", Enums.GM.GM_TREE_SHRUB1, dashFormat: true, plainShader: false, shrub1FoliageColours);
		addGMFile(24, "tree_shrub2", Enums.GM.GM_TREE_SHRUB2, dashFormat: true, plainShader: false, shrub2FoliageColours);
		addGMFile(100, "tree_apple", Enums.GM.GM_TREE_APPLE, dashFormat: true, plainShader: false, appleFoliageColours);
		addGMFile(147, "tree_birch", Enums.GM.GM_TREE_BIRCH, dashFormat: true, plainShader: false, birchFoliageColours);
		addGMFile(147, "Tree_Chestnut", Enums.GM.GM_TREE_CHESTNUT, dashFormat: true, plainShader: false, chestnutFoliageColours);
		addGMFile(991, "body_fighting_monk", Enums.GM.GM_BODY_FIGHTING_MONK, dashFormat: true);
		addGMFile(991, "body_temple_guard", Enums.GM.GM_BODY_TEMPLE_GUARD, dashFormat: true);
		addGMFile(1823, "body_knight", Enums.GM.GM_BODY_KNIGHT, dashFormat: true);
		addGMFile(358, "body_ladder_bearer", Enums.GM.GM_BODY_LADDERMAN, dashFormat: true);
		addGMFile(1177, "body_pikeman", Enums.GM.GM_BODY_PIKEMAN, dashFormat: true);
		addGMFile(551, "body_pitch_worker", Enums.GM.GM_BODY_PITCHWORKER, dashFormat: true);
		addGMFile(1159, "body_tunnelor", Enums.GM.GM_BODY_TUNNELER, dashFormat: true);
		addGMFile(783, "body_armourer", Enums.GM.GM_BODY_ARMOURER, dashFormat: true);
		addGMFile(639, "body_healer", Enums.GM.GM_BODY_HEALER, dashFormat: true);
		addGMFile(351, "body_ballista", Enums.GM.GM_BODY_BALLISTA, dashFormat: true);
		addGMFile(304, "body_battering_ram", Enums.GM.GM_BODY_BATTERING_RAM, dashFormat: true);
		addGMFile(751, "body_catapult", Enums.GM.GM_BODY_CATAPULT, dashFormat: true);
		addGMFile(487, "body_mangonel", Enums.GM.GM_BODY_MANGONEL, dashFormat: true);
		addGMFile(152, "body_siege_tower", Enums.GM.GM_BODY_SIEGE_TOWER, dashFormat: true, plainShader: true);
		addGMFile(503, "body_trebutchet", Enums.GM.GM_BODY_TREBUCHET, dashFormat: true);
		addGMFile(943, "body_lion", Enums.GM.GM_BODY_LION, dashFormat: true, plainShader: true);
		addGMFile(767, "body_rabbit", Enums.GM.GM_BODY_RABBIT, dashFormat: true, plainShader: false, rabbitColours);
		addGMFile(979, "body_fireman", Enums.GM.GM_BODY_FIREMAN, dashFormat: true);
		addGMFile(20, "body_animal_burning_big", Enums.GM.GM_BODY_ANIMAL_BURNING_BIG, dashFormat: true, plainShader: true);
		addGMFile(16, "body_animal_burning_small", Enums.GM.GM_BODY_ANIMAL_BURNING_SMALL, dashFormat: true, plainShader: true);
		addGMFile(1751, "body_archer", Enums.GM.GM_BODY_ARCHER, dashFormat: true);
		addGMFile(835, "body_baker", Enums.GM.GM_BODY_BAKER, dashFormat: true);
		addGMFile(1263, "body_blacksmith", Enums.GM.GM_BODY_BLACKSMITH, dashFormat: true);
		addGMFile(655, "body_boy", Enums.GM.GM_BODY_BOY, dashFormat: true);
		addGMFile(783, "body_brewer", Enums.GM.GM_BODY_BREWER, dashFormat: true);
		addGMFile(751, "body_catapult", Enums.GM.GM_BODY_CATAPULT, dashFormat: true);
		addGMFile(669, "body_chicken", Enums.GM.GM_BODY_CHICKEN, dashFormat: true, plainShader: false, animalColours);
		addGMFile(669, "body_chicken_brown", Enums.GM.GM_BODY_CHICKEN_BROWN, dashFormat: true, plainShader: false, animalColours);
		addGMFile(1308, "body_crossbowman", Enums.GM.GM_BODY_CROSSBOWMAN, dashFormat: true);
		addGMFile(479, "body_cow", Enums.GM.GM_BODY_COW, dashFormat: true, plainShader: false, animalColours);
		addGMFile(2103, "body_deer", Enums.GM.GM_BODY_DEER, dashFormat: true, plainShader: false, animalColours);
		addGMFile(703, "body_dog", Enums.GM.GM_BODY_DOG, dashFormat: true, plainShader: false, animalColours);
		addGMFile(271, "body_drunkard", Enums.GM.GM_BODY_DRUNKARD, dashFormat: true);
		addGMFile(1775, "body_farmer", Enums.GM.GM_BODY_FARMER, dashFormat: true);
		addGMFile(496, "body_fire_eater", Enums.GM.GM_BODY_FIREEATER, dashFormat: true);
		addGMFile(911, "body_fletcher", Enums.GM.GM_BODY_FLETCHER, dashFormat: true);
		addGMFile(127, "body_ghost", Enums.GM.GM_BODY_GHOST, dashFormat: true);
		addGMFile(655, "body_girl", Enums.GM.GM_BODY_GIRL, dashFormat: true);
		addGMFile(127, "body_horse_trader", Enums.GM.GM_BODY_TRADER_HORSE, dashFormat: true, plainShader: false, animalColours);
		addGMFile(1801, "body_hunter", Enums.GM.GM_BODY_HUNTER, dashFormat: true);
		addGMFile(813, "body_innkeeper", Enums.GM.GM_BODY_INNKEEPER, dashFormat: true);
		addGMFile(639, "body_iron_miner", Enums.GM.GM_BODY_IRONMINER, dashFormat: true);
		addGMFile(943, "body_jester", Enums.GM.GM_BODY_JESTER, dashFormat: true, plainShader: false, jesterColours);
		addGMFile(579, "body_juggler", Enums.GM.GM_BODY_JUGGLER, dashFormat: true);
		addGMFile(639, "body_lady", Enums.GM.GM_BODY_LADY, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(1679, "body_lord", Enums.GM.GM_BODY_LORD, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(1527, "body_maceman", Enums.GM.GM_BODY_MACEMAN, dashFormat: true);
		addGMFile(311, "body_man_burning", Enums.GM.GM_BODY_BURNING_MAN, dashFormat: true);
		addGMFile(779, "body_miller", Enums.GM.GM_BODY_MILLER, dashFormat: true);
		addGMFile(287, "body_mother", Enums.GM.GM_BODY_MOTHER, dashFormat: true);
		addGMFile(511, "body_ox", Enums.GM.GM_BODY_OXCART, dashFormat: true, plainShader: false, animalColours);
		addGMFile(3007, "body_peasant", Enums.GM.GM_BODY_PEASANT, dashFormat: true);
		addGMFile(1231, "body_poleturner", Enums.GM.GM_BODY_POLETURNER, dashFormat: true);
		addGMFile(655, "body_priest", Enums.GM.GM_BODY_PRIEST, dashFormat: true);
		addGMFile(136, "body_shield", Enums.GM.GM_BODY_SHIELD, dashFormat: true);
		addGMFile(1403, "body_siege_engineer", Enums.GM.GM_BODY_SIEGE_ENGINEER, dashFormat: true);
		addGMFile(1719, "body_spearman", Enums.GM.GM_BODY_SPEARMAN, dashFormat: true);
		addGMFile(823, "body_stonemason", Enums.GM.GM_BODY_STONEMASON, dashFormat: true);
		addGMFile(1087, "body_swordsman", Enums.GM.GM_BODY_SWORDSMAN, dashFormat: true);
		addGMFile(1111, "body_tanner", Enums.GM.GM_BODY_TANNER, dashFormat: true);
		addGMFile(255, "body_trader", Enums.GM.GM_BODY_TRADER, dashFormat: true);
		addGMFile(1367, "body_woodcutter", Enums.GM.GM_BODY_WOODCUTTER, dashFormat: true);
		addGMFile(1736, "body_arab_shortbow", Enums.GM.GM_BODY_ARAB_BOW, dashFormat: true);
		addGMFile(1467, "body_arab_assasin", Enums.GM.GM_BODY_ARAB_ASSASIN, dashFormat: true);
		addGMFile(1455, "body_horse_archer", Enums.GM.GM_BODY_ARAB_HORSE, dashFormat: true, plainShader: false, knightColours);
		addGMFile(1455, "body_horse_archer_top", Enums.GM.GM_BODY_ARAB_HORSEMAN, dashFormat: true);
		addGMFile(1255, "body_arab_grenadier", Enums.GM.GM_BODY_ARAB_GRENADIER, dashFormat: true);
		addGMFile(1463, "body_sapper", Enums.GM.GM_BODY_BEDOUIN_SAPPER, dashFormat: true);
		addGMFile(1599, "body_bedouin_healer", Enums.GM.GM_BODY_BEDOUIN_HEALER, dashFormat: true);
		addGMFile(1591, "body_eunuch", Enums.GM.GM_BODY_BEDOUIN_EUNUCH, dashFormat: true);
		addGMFile(1471, "body_ambusher", Enums.GM.GM_BODY_BEDOUIN_AMBUSHER, dashFormat: true);
		addGMFile(1337, "body_arab_slave", Enums.GM.GM_BODY_ARAB_SLAVE, dashFormat: true);
		addGMFile(1360, "body_arab_slinger", Enums.GM.GM_BODY_ARAB_SLINGER, dashFormat: true);
		addGMFile(911, "body_arab_swordsman", Enums.GM.GM_BODY_ARAB_SWORDSMAN, dashFormat: true);
		addGMFile(343, "body_arab_ballista", Enums.GM.GM_BODY_ARAB_BALLISTA, dashFormat: true);
		addGMFile(7, "assasin_rope", Enums.GM.GM_ASSASIN_ROPE, dashFormat: false, plainShader: true);
		addGMFile(16, "tree_cactii", Enums.GM.GM_TREE_CACTII, dashFormat: true, plainShader: true);
		addGMFile(15, "anim_crusader_flag", Enums.GM.GM_ANIM_CRUSADER_FLAG, dashFormat: true);
		addGMFile(15, "anim_arab_flag", Enums.GM.GM_ANIM_ARAB_FLAG, dashFormat: true);
		addGMFile(188, "body_info", Enums.GM.GM_BODY_INFO, dashFormat: true, plainShader: true);
		addGMFile(1055, "body_wolf", Enums.GM.GM_BODY_WOLF, dashFormat: true, plainShader: true);
		addGMFile(1679, "body_saladin", Enums.GM.GM_BODY_ARABIC_LORD, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(815, "body_camel", Enums.GM.GM_BODY_CAMEL, dashFormat: true, plainShader: false, animalColours);
		addGMFile(1343, "body_camel_lancer", Enums.GM.GM_BODY_BEDOUIN_CAMEL_LANCER, dashFormat: true);
		addGMFile(31, "body_missile_firepot", Enums.GM.GM_BODY_MISSILE_FIREPOT, dashFormat: true, plainShader: true);
		addGMFile(2427, "body_skirmisher", Enums.GM.GM_BODY_BEDOUIN_SKIRMISHER, dashFormat: true);
		addGMFile(1215, "body_heavy_camel", Enums.GM.GM_BODY_BEDOUIN_HEAVY_CAMEL, dashFormat: true);
		addGMFile(2103, "body_demolisher", Enums.GM.GM_BODY_BEDOUIN_DEMOLISHER, dashFormat: true);
		addGMFile(143, "body_javelin", Enums.GM.GM_BODY_JAVELIN, dashFormat: true, plainShader: true);
		addGMFile(1551, "body_goat", Enums.GM.GM_BODY_GOAT, dashFormat: true, plainShader: false, animalColours);
		addGMFile(943, "body_hyena", Enums.GM.GM_BODY_HYENA, dashFormat: true, plainShader: false, animalColours);
		addGMFile(767, "body_crocodile", Enums.GM.GM_BODY_CROCODILE, dashFormat: true, plainShader: false, animalColours);
		addGMFile(207, "body_condor", Enums.GM.GM_BODY_CONDOR, dashFormat: true, plainShader: true);
		addGMFile(1679, "body_bedouin_lord", Enums.GM.GM_BODY_BEDOUIN_LORD, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(655, "body_imam", Enums.GM.GM_BODY_IMAM, dashFormat: true);
		addGMFile(1679, "body_scribe_lord", Enums.GM.GM_BODY_SCRIBE_LORD, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(1679, "body_lord_female", Enums.GM.GM_BODY_LORD_FEMALE, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(1679, "body_bessie", Enums.GM.GM_BODY_LORD_BESSY, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(1679, "body_arab_lord_female", Enums.GM.GM_BODY_ARABIC_LORD_FEMALE, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(1679, "body_bedouin_lord_female", Enums.GM.GM_BODY_BEDOUIN_LORD_FEMALE, dashFormat: true, plainShader: false, lordLadyColours);
		addGMFile(7, "body_brazier", Enums.GM.GM_BODY_BRAZIER, dashFormat: true, plainShader: true);
		addGMFile(31, "body_disease", Enums.GM.GM_BODY_DISEASE, dashFormat: true, plainShader: true);
		addGMFile(731, "body_fire", Enums.GM.GM_BODY_FIRE, dashFormat: true, plainShader: true);
		addGMFile(293, "body_fire2", Enums.GM.GM_BODY_FIRE2, dashFormat: true, plainShader: true);
		addGMFile(207, "body_crow", Enums.GM.GM_BODY_CROW, dashFormat: true, plainShader: true);
		addGMFile(183, "body_missile", Enums.GM.GM_BODY_MISSILE, dashFormat: true, plainShader: true);
		addGMFile(143, "body_missile_2", Enums.GM.GM_BODY_MISSILE_2, dashFormat: true, plainShader: true);
		addGMFile(28, "body_missile_cow", Enums.GM.GM_BODY_MISSILE_COW, dashFormat: true, plainShader: true);
		addGMFile(143, "body_missile_fire", Enums.GM.GM_ANIM_MISSILE_FIRE, dashFormat: true, plainShader: true);
		addGMFile(143, "body_seagull", Enums.GM.GM_BODY_SEAGULL, dashFormat: true, plainShader: true);
		addGMFile(23, "body_splash", Enums.GM.GM_BODY_SPLASH, dashFormat: true, plainShader: true);
		addGMFile(15, "body_steam", Enums.GM.GM_BODY_STEAM, dashFormat: true, plainShader: true);
		addGMFile(47, "body_gate", Enums.GM.GM_BODY_GATE, dashFormat: true, plainShader: true);
		addGMFile(2, "body_tent", Enums.GM.GM_BODY_TENT, dashFormat: true);
		addGMFile(32, "rock_chips", Enums.GM.GM_ROCK_CHIPS, dashFormat: false, plainShader: true);
		addGMFile(32, "oil_dropped", Enums.GM.GM_BODY_OIL, dashFormat: false, plainShader: true);
		addGMFile(17, "cracks", Enums.GM.GM_CRACKS, dashFormat: false, plainShader: true);
		addGMFile(15, "puff of smoke", Enums.GM.GM_PUFF_OF_SMOKE, dashFormat: true, plainShader: true);
		addGMFile(72, "blast3", Enums.GM.GM_BLAST, dashFormat: true, plainShader: true);
		addGMFile(31, "smoke-30x30", Enums.GM.GM_SMOKE_ANIMS, dashFormat: true, plainShader: true);
		addGMFile(15, "anim_armourer", Enums.GM.GM_ARMOURER_ANIMS, dashFormat: true);
		addGMFile(67, "anim_baker", Enums.GM.GM_WORKSHOP_BAKER_ANIMS, dashFormat: true);
		addGMFile(198, "anim_blacksmith", Enums.GM.GM_WORKSHOP_SMITH_ANIMS, dashFormat: true);
		addGMFile(137, "anim_boiled_oil", Enums.GM.GM_OIL_ANIMS, dashFormat: true, plainShader: true);
		addGMFile(127, "anim_brewer", Enums.GM.GM_WORKSHOP_BREW_ANIMS, dashFormat: true);
		addGMFile(134, "anim_buildings2", Enums.GM.GM_BUILDING_ANIMS2, dashFormat: false, plainShader: true);
		addGMFile(138, "anim_castle", Enums.GM.GM_CASTLE_ANIMS, dashFormat: false, plainShader: true);
		addGMFile(20, "anim_chopping_block", Enums.GM.GM_ANIM_CHOPPING_BLOCK, dashFormat: true);
		addGMFile(54, "anim_dancing_bear", Enums.GM.GM_ANIM_DANCING_BEAR, dashFormat: true, plainShader: true);
		addGMFile(41, "anim_dog_cage", Enums.GM.GM_ANIM_DOG_CAGE, dashFormat: true);
		addGMFile(129, "anim_drawbridge", Enums.GM.GM_DRAWBRIDGE_ANIMS, dashFormat: true, plainShader: true);
		addGMFile(9, "anim_ducking_stool", Enums.GM.GM_ANIM_DUNKING_STOOL, dashFormat: true);
		addGMFile(12, "anim_dungeon", Enums.GM.GM_ANIM_DUNGEON, dashFormat: true);
		addGMFile(78, "anim_farmer", Enums.GM.GM_FARMER_ANIMS, dashFormat: true);
		addGMFile(130, "anim_flags", Enums.GM.GM_FLAG_ANIMS, dashFormat: true);
		addGMFile(31, "anim_flag_small", Enums.GM.GM_ANIM_FLAG_SMALL, dashFormat: true);
		addGMFile(71, "anim_fletcher", Enums.GM.GM_FLETCHER_ANIMS, dashFormat: true);
		addGMFile(19, "anim_gallows", Enums.GM.GM_GALLOWS_ANIMS, dashFormat: true, plainShader: true);
		addGMFile(16, "anim_gibbet", Enums.GM.GM_ANIM_GIBBET, dashFormat: true);
		addGMFile(192, "anim_goods", Enums.GM.GM_GOODS_ANIMS, dashFormat: false, plainShader: true);
		addGMFile(6, "anim_heads", Enums.GM.GM_ANIM_HEADS, dashFormat: true, plainShader: true);
		addGMFile(41, "anim_healer", Enums.GM.GM_ANIM_HEALER, dashFormat: true, plainShader: true);
		addGMFile(24, "anim_hunter", Enums.GM.GM_HUNTER_ANIMS, dashFormat: true);
		addGMFile(86, "anim_inn", Enums.GM.GM_ANIM_INN, dashFormat: true);
		addGMFile(169, "anim_iron_miner", Enums.GM.GM_MINE_ANIMS, dashFormat: true);
		addGMFile(31, "anim_killing_pits", Enums.GM.GM_ANIM_KILLING_PITS, dashFormat: true, plainShader: true);
		addGMFile(17, "anim_market", Enums.GM.GM_ANIM_MARKET, dashFormat: true);
		addGMFile(63, "anim_maypole", Enums.GM.GM_MAYPOLE_ANIMS, dashFormat: true);
		addGMFile(47, "anim_pitch_dugout", Enums.GM.GM_PITCH_ANIMS, dashFormat: true);
		addGMFile(126, "anim_poleturner", Enums.GM.GM_WORKSHOP_POLE_ANIMS, dashFormat: true);
		addGMFile(322, "anim_quarry", Enums.GM.GM_QUARRY_ANIMS, dashFormat: true);
		addGMFile(20, "anim_rack", Enums.GM.GM_ANIM_RACK, dashFormat: true);
		addGMFile(15, "anim_shields", Enums.GM.GM_SHEILD_ANIMS, dashFormat: true);
		addGMFile(41, "anim_stables", Enums.GM.GM_STABLE_ANIMS, dashFormat: true);
		addGMFile(19, "anim_stake", Enums.GM.GM_ANIM_STAKE, dashFormat: true);
		addGMFile(6, "anim_stocks", Enums.GM.GM_ANIM_STOCKS, dashFormat: true);
		addGMFile(75, "anim_tanner", Enums.GM.GM_WORKSHOP_TANNER_ANIMS, dashFormat: true);
		addGMFile(6, "anim_tunnelors_guild", Enums.GM.GM_ANIM_TUNNELERS_GUILD, dashFormat: false, plainShader: true);
		addGMFile(31, "anim_tunnels", Enums.GM.GM_ANIM_TUNNELS, dashFormat: true);
		addGMFile(211, "anim_whitecaps_wave", Enums.GM.GM_ANIM_WHITECAPS, dashFormat: true, plainShader: true);
		addGMFile(74, "anim_windmill", Enums.GM.GM_WINDMILL_ANIMS, dashFormat: true);
		addGMFile(24, "anim_woodcutter", Enums.GM.GM_WOODCUTTER_ANIMS, dashFormat: true);
		addGMFile(100, "float_pop_circ-1", Enums.GM.GM_FLOAT_POP_CIRC, dashFormat: true, plainShader: true);
		addGMFile(7, "float_pop_circ-2", Enums.GM.GM_FLOAT_POP_CIRC_2, dashFormat: true, plainShader: true);
		addGMFile(348, "floats", Enums.GM.GM_FLOATS, dashFormat: false, plainShader: true);
		addGMFile(331, "floats_new", Enums.GM.GM_FLOATS_NEW, dashFormat: true, plainShader: true);
		addGMFile(104, "cursors", Enums.GM.GM_CURSORS, dashFormat: true, plainShader: true);
		spritesLoaded = true;
		IntroSequence.loadingProgress++;
		yield return null;
	}

	private IEnumerator loadSprites(string path, bool makeMask = false, bool foliage = false)
	{
		Sprite[] array = Resources.LoadAll<Sprite>(path);
		foreach (Sprite sprite in array)
		{
			allSprites[sprite.name] = sprite;
			spriteToFileMatch[sprite.name] = path;
		}
		if (makeMask)
		{
			ResourceRequest resourceRequest = Resources.LoadAsync<Texture>(path + "_m");
			yield return resourceRequest;
			Texture texture = resourceRequest.asset as Texture;
			if (texture != null)
			{
				Material material = null;
				if (foliage)
				{
					material = new Material(Shader.Find("Unlit/Foliage"));
					material.SetTexture("_TeamMask", texture);
				}
				Material[] array2 = new Material[7];
				for (int j = 0; j < 7; j++)
				{
					if (!foliage)
					{
						Material material2 = new Material(Shader.Find("Unlit/TeamColour"));
						material2.SetTexture("_TeamMask", texture);
						if (j == 0)
						{
							material2.SetFloat("_SpriteCutoff", (float)j / 2f / 10f);
						}
						else
						{
							material2.SetFloat("_SpriteCutoff", ((float)j + 4f) / 2f / 10f);
						}
						array2[j] = material2;
					}
					else
					{
						array2[j] = material;
					}
				}
				pathToMaterialArray[path] = array2;
			}
		}
		IntroSequence.loadingProgress++;
		yield return null;
	}

	private void initPlainMaterial()
	{
		Material material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
		for (int i = 0; i < 7; i++)
		{
			plainMaterials[i] = material;
		}
	}

	public void setDefaultMaterial(GameObject obj)
	{
		if (defaultMaterial == null)
		{
			SpriteRenderer component = obj.GetComponent<SpriteRenderer>();
			defaultMaterial = component.material;
		}
	}

	private void addGMFile(int expectImageCount, string fileName, Enums.GM fileID, bool dashFormat = false, bool plainShader = false, Color[] colours = null, int ID_Offset = 0, int additionalStorage = 0)
	{
		if (colours == null)
		{
			colours = defaultColours;
		}
		int num = -1;
		for (int i = 0; i < expectImageCount + 2; i++)
		{
			string key = getKey(fileName, i, dashFormat);
			if (allSprites.ContainsKey(key))
			{
				num = i;
			}
		}
		if (num >= 0)
		{
			if (additionalStorage >= 0)
			{
				gmSprites[(int)fileID] = new Sprite[num + 1 + additionalStorage];
				gmAltSprites[(int)fileID] = new Sprite[num + 1 + additionalStorage];
				gmColors[(int)fileID] = colours;
			}
			bool flag = true;
			for (int j = 0; j <= num; j++)
			{
				string key2 = getKey(fileName, j, dashFormat);
				if (!allSprites.ContainsKey(key2))
				{
					continue;
				}
				gmSprites[(int)fileID][j + ID_Offset] = allSprites[key2];
				if (flag)
				{
					flag = false;
					if (plainShader)
					{
						gmMaterials[(int)fileID] = plainMaterials;
					}
					else
					{
						string key3 = spriteToFileMatch[key2];
						if (pathToMaterialArray.ContainsKey(key3))
						{
							gmMaterials[(int)fileID] = pathToMaterialArray[key3];
						}
					}
				}
				if (dashFormat)
				{
					key2 = getKey(fileName, j + ID_Offset, dashFormat, altFrame: true);
					if (allSprites.ContainsKey(key2))
					{
						gmAltSprites[(int)fileID][j + ID_Offset] = allSprites[key2];
					}
				}
			}
		}
		else
		{
			Debug.Log("No Sprites for " + fileName);
		}
	}

	private string getKey(string fileName, int ID, bool dashFormat, bool altFrame = false)
	{
		if (!dashFormat)
		{
			return fileName + " " + ID.ToString("000");
		}
		if (!altFrame)
		{
			return fileName + "-" + ID.ToString("");
		}
		return fileName + "-" + ID.ToString("") + "x";
	}

	private void createAnimArrays()
	{
	}

	public Sprite GetSprite(string name)
	{
		Sprite value = null;
		allSprites.TryGetValue(name, out value);
		return value;
	}

	public Sprite GetGMSprite(Enums.GM gmFile, int imageID, bool altFrame = false)
	{
		if (altFrame && gmAltSprites[(int)gmFile] != null && imageID >= 0 && imageID < gmAltSprites[(int)gmFile].Length)
		{
			Sprite sprite = gmAltSprites[(int)gmFile][imageID];
			if (sprite != null)
			{
				return sprite;
			}
		}
		if (gmSprites[(int)gmFile] != null && imageID >= 0 && imageID < gmSprites[(int)gmFile].Length)
		{
			return gmSprites[(int)gmFile][imageID];
		}
		Debug.Log("Missing Sprite " + gmFile.ToString() + " : " + imageID);
		return null;
	}

	public Material GetGMMaterial(Enums.GM gmFile, int colourOffset, int chop_feet, out Color outputColour, int transparency)
	{
		if (gmMaterials[(int)gmFile] != null)
		{
			if (gmMaterials[(int)gmFile] == plainMaterials)
			{
				outputColour = Color.white;
			}
			else
			{
				Color[] array = gmColors[(int)gmFile];
				outputColour = array[colourOffset];
			}
			outputColour.a = (float)(32 - transparency) / 32f;
			int num = 0;
			if (chop_feet > 0)
			{
				num = chop_feet / 4;
				if (num > 6)
				{
					num = 6;
				}
			}
			return gmMaterials[(int)gmFile][num];
		}
		outputColour = Color.white;
		outputColour.a = (float)(32 - transparency) / 32f;
		return defaultMaterial;
	}

	public Sprite getDebugLogicTileSprite(int frame)
	{
		return GetSprite("Debug Logic " + frame.ToString("000"));
	}
}
