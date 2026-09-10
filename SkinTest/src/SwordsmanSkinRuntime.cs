using APIShared;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using CrusaderDE;
using ImageSource = Noesis.ImageSource;
using UIElement = Noesis.UIElement;
using ExtenderGM = SHCDESE.Interop.GM;
using GameGM = Enums.GM;

namespace SkinTest
{
    internal sealed class SwordsmanSkinRuntime : IDisposable
    {
        private const string AtlasPath = "Assets/CrusaderSwordsman/atlas.png";
        private const string MaskPath = "Assets/CrusaderSwordsman/atlas_m.png";
        private const string ManifestPath = "Assets/CrusaderSwordsman/atlas.json";
        private const string CastleAtlasPath = "Assets/CrusaderRoundTower/atlas.png";
        private const string CastleManifestPath = "Assets/CrusaderRoundTower/atlas.json";
        private const string CastleAnimAtlasPath = "Assets/CrusaderRoundTowerAnimations/atlas.png";
        private const string CastleAnimManifestPath = "Assets/CrusaderRoundTowerAnimations/atlas.json";
        private const string UiAssetRoot = "Assets/CrusaderUI/";
        private delegate void SetBodySpriteDelegate(SpriteRenderer renderer, int file, int image, int colour,
            bool alternateFrame, int chopFeet, int transparency);
        private delegate void SetBuildingTileSpriteDelegate(GameMapTile tile, int file, int image, int light);
        private delegate void AddUpdateBuildingAnimDelegate(GameMap gameMap, int objectId, int x, int y,
            int tileX, int tileY, int animLayer, int file, int image, int colour, int transparency,
            int layerDelay, bool hasSubSpecial, int halfPixelX, int halfPixelY);
        private delegate void AddUpdateWallFillinDelegate(GameMap gameMap, int objectId, int x, int y,
            float heightAboveGround, int image, int xOffset);

        private readonly ManualLogSource log;
        private readonly Dictionary<SpriteRenderer, int> unitByRenderer =
            new Dictionary<SpriteRenderer, int>(ReferenceComparer<SpriteRenderer>.Instance);
        private readonly Dictionary<int, CachedCulture> cultureByPlayer = new Dictionary<int, CachedCulture>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);
        private Sprite[] normalSprites;
        private Sprite[] alternateSprites;
        private Material[] materials;
        private Texture2D colourTexture;
        private Texture2D maskTexture;
        private Texture2D castleTexture;
        private Texture2D castleAnimTexture;
        private Hook hook;
        private Hook buildingHook;
        private Hook buildingAnimHook;
        private Hook wallFillinHook;
        private SetBodySpriteDelegate trampoline;
        private SetBuildingTileSpriteDelegate buildingTrampoline;
        private AddUpdateBuildingAnimDelegate buildingAnimTrampoline;
        private AddUpdateWallFillinDelegate wallFillinTrampoline;
        private Sprite[] castleSprites;
        private Sprite[] castleAnimSprites;
        private readonly Stack<CastleAnimContext> castleAnimContexts = new Stack<CastleAnimContext>();
        private readonly byte[,][] troopHudBytes = new byte[8, 4][];
        private readonly ImageSource[,] troopHudSources = new ImageSource[8, 4];
        private readonly byte[][] towerHudBytes = new byte[2][];
        private readonly ImageSource[] towerHudSources = new ImageSource[2];
        private IUnitHudPresentationCapability troopHudCapability;
        private UIElement towerButton;
        private object vanillaTowerSprite1;
        private object vanillaTowerSprite2;
        private bool activeMap;
        private bool troopHudRegistrationAttempted;
        private bool disposed;

        public SwordsmanSkinRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Initialize()
        {
            try
            {
                LoadAssets();
                InstallHook();
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualSpawn.Observable.Subscribe(OnUnitVisualSpawn));
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualInterpolate.Observable.Subscribe(OnUnitVisualInterpolate));
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualRemove.Observable.Subscribe(OnUnitVisualRemove));
                subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => OnMapStarted()));
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => ClearBindings()));
                LogInfo($"Validated private atlases: swordsman normal={normalSprites.Length}, alternate={alternateSprites.Length}, mask=yes; castle sparse=1467; castle animations=122, mask=no.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void LoadAssets()
        {
            GameAssetManagerAPI assets = GameAssetManagerAPI.Instance;
            if (!assets.GetModFileBinaryContent(SkinTestPlugin.PluginGuid, AtlasPath, out byte[] colourBytes) || colourBytes == null)
                throw new InvalidOperationException($"Missing indexed mod asset: {AtlasPath}");
            if (!assets.GetModFileBinaryContent(SkinTestPlugin.PluginGuid, MaskPath, out byte[] maskBytes) || maskBytes == null)
                throw new InvalidOperationException($"Missing indexed mod asset: {MaskPath}");
            if (!assets.GetModFileTextContent(SkinTestPlugin.PluginGuid, ManifestPath, out string json))
                throw new InvalidOperationException($"Missing indexed mod asset: {ManifestPath}");

            colourTexture = LoadTexture(colourBytes, "SkinTest_SH1DE_Swordsman_Colour");
            maskTexture = LoadTexture(maskBytes, "SkinTest_SH1DE_Swordsman_Mask");
            if (colourTexture.width != maskTexture.width || colourTexture.height != maskTexture.height)
                throw new InvalidOperationException("Colour and team-mask atlas dimensions differ.");

            AtlasManifest manifest = AtlasManifest.ParseAndValidate(json, colourTexture.width, colourTexture.height);
            normalSprites = new Sprite[AtlasManifest.NormalFrameCount];
            alternateSprites = new Sprite[AtlasManifest.AlternateFrameCount];
            foreach (AtlasFrame frame in manifest.Frames)
            {
                var rect = new Rect(frame.X, frame.Y, frame.Width, frame.Height);
                var pivot = new Vector2(frame.PivotX, frame.PivotY);
                Sprite sprite = Sprite.Create(colourTexture, rect, pivot, frame.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = "SkinTest_" + frame.Name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(sprite);
                (frame.Alternate ? alternateSprites : normalSprites)[frame.Index] = sprite;
            }

            materials = new Material[7];
            Shader shader = Shader.Find("Unlit/TeamColour");
            if (shader == null)
                throw new InvalidOperationException("Shader 'Unlit/TeamColour' was not found.");
            for (int index = 0; index < materials.Length; index++)
            {
                var material = new Material(shader)
                {
                    name = $"SkinTest_SH1DE_Swordsman_Chop{index}",
                    hideFlags = HideFlags.HideAndDontSave
                };
                material.SetTexture("_TeamMask", maskTexture);
                material.SetFloat("_SpriteCutoff", index == 0 ? 0f : (index + 4f) / 20f);
                UnityEngine.Object.DontDestroyOnLoad(material);
                materials[index] = material;
            }

            if (!assets.GetModFileBinaryContent(SkinTestPlugin.PluginGuid, CastleAtlasPath, out byte[] castleBytes) || castleBytes == null)
                throw new InvalidOperationException($"Missing indexed mod asset: {CastleAtlasPath}");
            if (!assets.GetModFileTextContent(SkinTestPlugin.PluginGuid, CastleManifestPath, out string castleJson))
                throw new InvalidOperationException($"Missing indexed mod asset: {CastleManifestPath}");
            castleTexture = LoadTexture(castleBytes, "SkinTest_SH1DE_Castle");
            SparseAtlasManifest castleManifest = SparseAtlasManifest.ParseAndValidate(
                castleJson, castleTexture.width, castleTexture.height, "tile_castle ", 1467, 1569);
            castleSprites = new Sprite[1570];
            foreach (AtlasFrame frame in castleManifest.Frames)
            {
                Sprite sprite = Sprite.Create(castleTexture,
                    new Rect(frame.X, frame.Y, frame.Width, frame.Height),
                    new Vector2(frame.PivotX, frame.PivotY), frame.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = "SkinTest_" + frame.Name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(sprite);
                castleSprites[frame.Index] = sprite;
            }

            byte[] castleAnimBytes = ReadAssetBytes(assets, CastleAnimAtlasPath);
            if (!assets.GetModFileTextContent(SkinTestPlugin.PluginGuid, CastleAnimManifestPath, out string castleAnimJson))
                throw new InvalidOperationException($"Missing indexed mod asset: {CastleAnimManifestPath}");
            castleAnimTexture = LoadTexture(castleAnimBytes, "SkinTest_SH1DE_CastleAnimations");
            SparseAtlasManifest castleAnimManifest = SparseAtlasManifest.ParseAndValidate(
                castleAnimJson, castleAnimTexture.width, castleAnimTexture.height, "anim_castle ", 122, 138);
            castleAnimSprites = new Sprite[139];
            foreach (AtlasFrame frame in castleAnimManifest.Frames)
            {
                Sprite sprite = Sprite.Create(castleAnimTexture,
                    new Rect(frame.X, frame.Y, frame.Width, frame.Height),
                    new Vector2(frame.PivotX, frame.PivotY), frame.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = "SkinTest_" + frame.Name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(sprite);
                castleAnimSprites[frame.Index] = sprite;
            }

            string[] troopNames = { "UIBuildingsO011", "UIBuildingsO012", "UIButtonsK007", "UIButtonsK008" };
            for (int colour = 1; colour <= 8; colour++)
                for (int slot = 0; slot < troopNames.Length; slot++)
                    troopHudBytes[colour - 1, slot] = ReadAssetBytes(assets, $"{UiAssetRoot}{troopNames[slot]}_colour{colour}.png");
            towerHudBytes[0] = ReadAssetBytes(assets, UiAssetRoot + "UIBuildingsK009.png");
            towerHudBytes[1] = ReadAssetBytes(assets, UiAssetRoot + "UIBuildingsK010.png");
        }

        private void InstallHook()
        {
            MethodInfo method = typeof(SpriteMapping).GetMethod(nameof(SpriteMapping.SetBodySprite),
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SpriteRenderer), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException(nameof(SpriteMapping), nameof(SpriteMapping.SetBodySprite));
            hook = new Hook(method, (SetBodySpriteDelegate)SetBodySpriteHook);
            trampoline = hook.GenerateTrampoline<SetBodySpriteDelegate>();
            MethodInfo buildingMethod = typeof(SpriteMapping).GetMethod(nameof(SpriteMapping.setGenericBuildingTileGraphic),
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(GameMapTile), typeof(int), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException(nameof(SpriteMapping), nameof(SpriteMapping.setGenericBuildingTileGraphic));
            buildingHook = new Hook(buildingMethod, (SetBuildingTileSpriteDelegate)SetBuildingTileSpriteHook);
            buildingTrampoline = buildingHook.GenerateTrampoline<SetBuildingTileSpriteDelegate>();
            MethodInfo buildingAnimMethod = typeof(GameMap).GetMethod(nameof(GameMap.addUpdateBuildingAnim),
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException(nameof(GameMap), nameof(GameMap.addUpdateBuildingAnim));
            buildingAnimHook = new Hook(buildingAnimMethod, (AddUpdateBuildingAnimDelegate)AddUpdateBuildingAnimHook);
            buildingAnimTrampoline = buildingAnimHook.GenerateTrampoline<AddUpdateBuildingAnimDelegate>();
            MethodInfo wallFillinMethod = typeof(GameMap).GetMethod(nameof(GameMap.addUpdateWallFillin),
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(float), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException(nameof(GameMap), nameof(GameMap.addUpdateWallFillin));
            wallFillinHook = new Hook(wallFillinMethod, (AddUpdateWallFillinDelegate)AddUpdateWallFillinHook);
            wallFillinTrampoline = wallFillinHook.GenerateTrampoline<AddUpdateWallFillinDelegate>();
            LogInfo("Managed swordsman, round-tower tile and castle-animation hooks installed after the existing hook chains.");
        }

        public void RegisterTroopHudWithApiShared()
        {
            if (troopHudRegistrationAttempted)
                return;
            troopHudRegistrationAttempted = true;
            try
            {
                ApiShared.WhenReady(RegisterTroopHudOverrides);
            }
            catch (Exception ex)
            {
                WarnOnce("troop-hud-api-readiness-error",
                    $"APIShared HUD readiness registration failed closed; world skins remain active: {ex}");
            }
        }

        private void RegisterTroopHudOverrides(IApiShared api)
        {
            try
            {
                NativeCapabilityDiagnostic diagnostic = null;
                if (api == null || !api.TryGetUnitHudPresentation(SkinTestPlugin.PluginGuid,
                    out IUnitHudPresentationCapability capability, out diagnostic))
                {
                    WarnOnce("troop-hud-api-unavailable",
                        $"APIShared unit-HUD capability is unavailable; world skins remain active: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                    return;
                }

                UnitHudImageSlot[] slots =
                {
                    UnitHudImageSlot.UIBuildingsO011,
                    UnitHudImageSlot.UIBuildingsO012,
                    UnitHudImageSlot.UIButtonsK007,
                    UnitHudImageSlot.UIButtonsK008
                };
                bool complete = true;
                foreach (UnitHudImageSlot slot in slots)
                {
                    UnitHudImageSlot capturedSlot = slot;
                    var definition = new UnitHudImageOverrideDefinition(
                        "sh1de-swordsman-" + capturedSlot.ToString(), capturedSlot);
                    if (!capability.TryRegisterImageOverride(definition,
                        context => ResolveTroopHudImage(context, capturedSlot), out diagnostic))
                    {
                        complete = false;
                        WarnOnce("troop-hud-api-registration:" + capturedSlot,
                            $"APIShared HUD override registration failed for {capturedSlot}: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                    }
                }
                troopHudCapability = capability;
                if (activeMap)
                    troopHudCapability.RequestRefresh();
                LogInfo(complete
                    ? "Four SH1DE swordsman HUD overrides registered with APIShared."
                    : "APIShared accepted only part of the SH1DE swordsman HUD overrides; unavailable slots remain unchanged.");
            }
            catch (Exception ex)
            {
                WarnOnce("troop-hud-api-registration-error",
                    $"APIShared HUD override registration failed closed; world skins remain active: {ex}");
            }
        }

        private void OnUnitVisualSpawn(UnitUnityVisualSpawnEventArgs args)
        {
            if (args == null || args.UnitId <= 0 || ReferenceEquals(args.SpriteRenderer, null))
                return;
            unitByRenderer[args.SpriteRenderer] = args.UnitId;
        }

        private void OnUnitVisualInterpolate(UnitUnityVisualInterpolateEventArgs args)
        {
            Chimp chimp = args?.Chimp;
            if (chimp == null || chimp.objectID <= 0 || ReferenceEquals(chimp.sprRenderer, null))
                return;
            bool newlyBound = !unitByRenderer.TryGetValue(chimp.sprRenderer, out int priorUnitId) || priorUnitId != chimp.objectID;
            unitByRenderer[chimp.sprRenderer] = chimp.objectID;
            // Existing units receive an immediate repaint after loading instead of waiting for the next animation frame.
            if (newlyBound)
                SpriteMapping.SetBodySprite(chimp.sprRenderer, chimp.file1, chimp.image1, chimp.colour1,
                    chimp.altFrame1Set, chimp.chopFeet, chimp.transparency);
        }

        private void OnUnitVisualRemove(UnitUnityVisualRemoveEventArgs args)
        {
            SpriteRenderer renderer = args?.Chimp?.sprRenderer;
            if (!ReferenceEquals(renderer, null))
                unitByRenderer.Remove(renderer);
        }

        private void SetBodySpriteHook(SpriteRenderer renderer, int file, int image, int colour,
            bool alternateFrame, int chopFeet, int transparency)
        {
            // Vanilla and every earlier managed hook run first. SkinTest only replaces their untouched swordsman result.
            trampoline(renderer, file, image, colour, alternateFrame, chopFeet, transparency);
            try
            {
                int unitId = 0;
                bool rendererBound = !ReferenceEquals(renderer, null) && unitByRenderer.TryGetValue(renderer, out unitId);
                LogOnce("set-body-sprite-confirmed",
                    $"SetBodySprite detour confirmed: file={(ExtenderGM)file}, image={image}, alternate={alternateFrame}, rendererBound={rendererBound}, unitId={(rendererBound ? unitId : 0)}.");
                if (renderer == null)
                    return;
                if (file == (int)ExtenderGM.GM_CASTLE_ANIMS)
                {
                    CastleAnimContext context = castleAnimContexts.Count == 0 ? null : castleAnimContexts.Peek();
                    LogOnce("castle-anim-callback",
                        $"GM_CASTLE_ANIMS callback observed: image={image}, roundTowerContext={context != null}.");
                    if (context != null && context.Image == image)
                        TryReplaceRoundTowerAnimation(renderer, image, context);
                    return;
                }
                if (file != (int)ExtenderGM.GM_BODY_SWORDSMAN)
                    return;

                int frameIndex = SkinSelectionPolicy.ToAtlasFrameIndex(image);
                LogOnce("swordsman-callback",
                    $"Swordsman SetBodySprite callback: image={image}, frameIndex={frameIndex}, alternate={alternateFrame}, rendererBound={rendererBound}, unitId={(rendererBound ? unitId : 0)}.");
                if (spriteLoader.instance == null)
                {
                    WarnOnce("sprite-loader-missing", "Swordsman sprite callback occurred before spriteLoader was available.");
                    return;
                }
                if (!rendererBound)
                {
                    WarnOnce("swordsman-renderer-unbound",
                        $"Swordsman sprite callback has no renderer binding: image={image}, frameIndex={frameIndex}, alternate={alternateFrame}.");
                    return;
                }

                Sprite expected = frameIndex >= 0
                    ? spriteLoader.instance.GetGMSprite(GameGM.GM_BODY_SWORDSMAN, frameIndex, alternateFrame)
                    : null;
                bool expectedVanillaSprite = ReferenceEquals(renderer.sprite, expected);
                bool isSwordsman = false;
                bool unitFound = false;
                int ownerPlayerId = 0;

                unsafe
                {
                    if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                    {
                        unitFound = true;
                        isSwordsman = unit->r_UnitChimp == eChimps.CHIMP_TYPE_SWORDSMAN;
                        ownerPlayerId = unit->r_ControllableForPlayerId;
                    }
                }

                if (!unitFound)
                    WarnOnce("swordsman-unit-missing", $"Bound swordsman unit could not be resolved: unitId={unitId}.");
                else if (isSwordsman && ownerPlayerId <= 0)
                    WarnOnce("swordsman-owner-missing", $"Swordsman has no controllable owner: unitId={unitId}.");

                LordCulture culture = LordCulture.Unknown;
                int lordUnitId = 0;
                ExtenderGM lordMaterial = default;
                string cultureSource = "unresolved";
                int cultureValue = -1;
                if (isSwordsman && ownerPlayerId > 0)
                    culture = ResolveOwnerCulture(ownerPlayerId, out lordUnitId, out lordMaterial, out cultureSource, out cultureValue);
                if (isSwordsman && culture == LordCulture.Unknown)
                    WarnOnce($"swordsman-culture-unresolved:{ownerPlayerId}",
                        $"Swordsman culture is not yet safely resolvable: unitId={unitId}, ownerPlayerId={ownerPlayerId}, lordUnitId={lordUnitId}.");

                bool normalAvailable = frameIndex >= 0 && frameIndex < normalSprites.Length && normalSprites[frameIndex] != null;
                bool alternateAvailable = frameIndex >= 0 && frameIndex < alternateSprites.Length && alternateSprites[frameIndex] != null;
                SkinFrameChoice choice = SkinSelectionPolicy.SelectFrame(alternateFrame, normalAvailable, alternateAvailable);
                bool eligibleOwner = SkinSelectionPolicy.HasEligibleOwner(unitFound, ownerPlayerId, culture);
                if (isSwordsman && culture == LordCulture.NonEuropean)
                    LogOnce($"vanilla-culture:{cultureSource}:{cultureValue}",
                        $"Vanilla retained for non-European lord culture: unitId={unitId}, ownerPlayerId={ownerPlayerId}, lordUnitId={lordUnitId}, source={cultureSource}, value={cultureValue}.");
                if (!SkinSelectionPolicy.CanReplaceVanilla(isSwordsman, expectedVanillaSprite, eligibleOwner, choice))
                {
                    if (isSwordsman && eligibleOwner && !expectedVanillaSprite)
                        WarnOnce("conflict",
                            $"An earlier mod replaced the expected swordsman sprite; SkinTest leaves that result untouched: image={image}, frameIndex={frameIndex}, alternate={alternateFrame}, expected={DescribeSprite(expected)}, actual={DescribeSprite(renderer.sprite)}.");
                    return;
                }

                renderer.sprite = choice == SkinFrameChoice.Alternate ? alternateSprites[frameIndex] : normalSprites[frameIndex];
                renderer.sharedMaterial = materials[ChopMaterialIndex(chopFeet)];
                // renderer.color already contains Vanilla's player colour and transparency from the trampoline.
                LogOnce("skin-applied",
                    $"SH1DE skin applied: unitId={unitId}, ownerPlayerId={ownerPlayerId}, lordUnitId={lordUnitId}, source={cultureSource}, value={cultureValue}, lordGM={lordMaterial}, image={image}, frameIndex={frameIndex}, alternate={choice == SkinFrameChoice.Alternate}.");
            }
            catch (Exception ex)
            {
                WarnOnce("hook-error", $"Sprite replacement failed closed; the prior result remains active: {ex}");
            }
        }

        private void AddUpdateBuildingAnimHook(GameMap gameMap, int objectId, int x, int y, int tileX,
            int tileY, int animLayer, int file, int image, int colour, int transparency, int layerDelay,
            bool hasSubSpecial, int halfPixelX, int halfPixelY)
        {
            CastleAnimContext context = file == (int)ExtenderGM.GM_CASTLE_ANIMS
                ? ResolveRoundTowerAnimationContextSafely(gameMap, x, y, image)
                : null;
            if (context != null)
                castleAnimContexts.Push(context);
            try
            {
                buildingAnimTrampoline(gameMap, objectId, x, y, tileX, tileY, animLayer, file, image,
                    colour, transparency, layerDelay, hasSubSpecial, halfPixelX, halfPixelY);
                try
                {
                    if (context != null && gameMap != null && gameMap.buildingAnims.TryGetValue(objectId, out BuildingAnim visual))
                        TryReplaceRoundTowerAnimation(visual.sprRenderer, image, context);
                }
                catch (Exception ex)
                {
                    WarnOnce("castle-building-anim-hook-error",
                        $"Round-tower building animation replacement failed closed: {ex}");
                }
            }
            finally
            {
                PopCastleAnimContext(context);
            }
        }

        private void AddUpdateWallFillinHook(GameMap gameMap, int objectId, int x, int y,
            float heightAboveGround, int image, int xOffset)
        {
            CastleAnimContext context = ResolveRoundTowerAnimationContextSafely(gameMap, x, y, image);
            if (context != null)
                castleAnimContexts.Push(context);
            try
            {
                wallFillinTrampoline(gameMap, objectId, x, y, heightAboveGround, image, xOffset);
                try
                {
                    if (context != null && gameMap != null && gameMap.wallFillins.TryGetValue(objectId, out WallFillin visual))
                        TryReplaceRoundTowerAnimation(visual.sprRenderer, image, context);
                }
                catch (Exception ex)
                {
                    WarnOnce("castle-wall-fillin-hook-error",
                        $"Round-tower wall-fillin replacement failed closed: {ex}");
                }
            }
            finally
            {
                PopCastleAnimContext(context);
            }
        }

        private CastleAnimContext ResolveRoundTowerAnimationContextSafely(GameMap gameMap, int x, int y, int image)
        {
            try
            {
                return TryResolveRoundTowerAnimationContext(gameMap, x, y, image);
            }
            catch (Exception ex)
            {
                WarnOnce("castle-anim-context-error",
                    $"Round-tower animation context lookup failed closed: {ex}");
                return null;
            }
        }

        private void PopCastleAnimContext(CastleAnimContext context)
        {
            if (context == null)
                return;
            if (castleAnimContexts.Count == 0 || !ReferenceEquals(castleAnimContexts.Peek(), context))
            {
                castleAnimContexts.Clear();
                WarnOnce("castle-anim-context-order", "Round-tower animation context order differed; contexts were cleared fail-closed.");
                return;
            }
            castleAnimContexts.Pop();
        }

        private unsafe CastleAnimContext TryResolveRoundTowerAnimationContext(GameMap gameMap, int x, int y, int image)
        {
            if (gameMap == null)
                return null;
            GameMapTile tile = gameMap.getMapTile(x, y);
            if (tile == null)
            {
                WarnOnce("castle-anim-map-tile-missing",
                    $"Castle animation position could not be mapped to a tile: x={x}, y={y}, image={image}.");
                return null;
            }
            int tileId = GameTileManagerAPI.Instance.GetTileId(tile.gameMapX, tile.gameMapY);
            int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(tileId);
            if (buildingId <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
            {
                WarnOnce("castle-anim-building-unresolved",
                    $"Castle animation tile has no resolvable building: tileId={tileId}, buildingId={buildingId}, image={image}.");
                return null;
            }
            if (building->r_BuildingType != eStructs.STRUCT_TOWER5 &&
                building->r_BuildingType != eStructs.STRUCT_TOWER5_DESTROYED)
                return null;
            int ownerPlayerId = building->r_PlayerIdOwner;
            LordCulture culture = ResolveOwnerCulture(ownerPlayerId, out int lordUnitId,
                out ExtenderGM lordMaterial, out string source, out int value);
            LogOnce("round-tower-animation-context",
                $"Round-tower animation context resolved: buildingId={buildingId}, ownerPlayerId={ownerPlayerId}, image={image}, source={source}, value={value}, culture={culture}.");
            return new CastleAnimContext(buildingId, ownerPlayerId, image, culture, lordUnitId,
                lordMaterial, source, value);
        }

        private void TryReplaceRoundTowerAnimation(SpriteRenderer renderer, int image, CastleAnimContext context)
        {
            if (renderer == null || context == null || context.Image != image || spriteLoader.instance == null)
                return;
            if (context.Culture == LordCulture.NonEuropean)
            {
                LogOnce($"tower-animation-vanilla:{context.Source}:{context.Value}",
                    $"Vanilla round-tower animation retained for non-European owner: buildingId={context.BuildingId}, ownerPlayerId={context.OwnerPlayerId}, image={image}, source={context.Source}, value={context.Value}.");
                return;
            }
            if (context.Culture != LordCulture.European)
                return;
            if (image <= 0 || image >= castleAnimSprites.Length || castleAnimSprites[image] == null)
            {
                LogOnce($"tower-animation-frame-missing:{image}",
                    $"No SH1DE round-tower animation frame exists for image={image}; Vanilla remains active.");
                return;
            }
            Sprite replacement = castleAnimSprites[image];
            if (ReferenceEquals(renderer.sprite, replacement))
                return;
            Sprite expected = spriteLoader.instance.GetGMSprite(GameGM.GM_CASTLE_ANIMS, image, false);
            if (!ReferenceEquals(renderer.sprite, expected))
            {
                WarnOnce($"tower-animation-conflict:{image}",
                    $"An earlier mod replaced the expected castle animation sprite; SkinTest leaves it untouched: image={image}, expected={DescribeSprite(expected)}, actual={DescribeSprite(renderer.sprite)}.");
                return;
            }
            renderer.sprite = replacement;
            LogOnce("tower-animation-skin-applied",
                $"SH1DE round-tower animation applied: buildingId={context.BuildingId}, ownerPlayerId={context.OwnerPlayerId}, lordUnitId={context.LordUnitId}, lordGM={context.LordMaterial}, source={context.Source}, value={context.Value}, image={image}.");
        }

        private unsafe LordCulture ResolveOwnerCulture(int ownerPlayerId, out int lordUnitId,
            out ExtenderGM lordMaterial, out string source, out int value)
        {
            lordUnitId = GamePlayerManagerAPI.Instance.GetLordUnitId(ownerPlayerId);
            lordMaterial = default;
            source = "unresolved";
            value = -1;
            if (lordUnitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(lordUnitId, out GameUnit* lord))
            {
                lordMaterial = lord->r_GameMaterialIndex;
                LordCulture actual = SkinSelectionPolicy.ClassifyLordMaterial(lordMaterial);
                source = "lord-unit";
                value = (int)lordMaterial;
                if (cultureByPlayer.TryGetValue(ownerPlayerId, out CachedCulture cached) &&
                    cached.Culture != LordCulture.Unknown && actual != LordCulture.Unknown && cached.Culture != actual)
                {
                    WarnOnce($"culture-mismatch:{ownerPlayerId}",
                        $"Authoritative lord culture differs from early culture: ownerPlayerId={ownerPlayerId}, earlySource={cached.Source}, earlyValue={cached.Value}, earlyCulture={cached.Culture}, lordUnitId={lordUnitId}, lordGM={lordMaterial}, actualCulture={actual}.");
                }
                LordCulture reconciled = SkinSelectionPolicy.ReconcileEarlyAndActualCulture(
                    cultureByPlayer.TryGetValue(ownerPlayerId, out CachedCulture prior) ? prior.Culture : LordCulture.Unknown,
                    actual);
                if (actual != LordCulture.Unknown)
                {
                    cultureByPlayer[ownerPlayerId] = new CachedCulture(reconciled, source, value);
                    return reconciled;
                }
                WarnOnce($"unknown-lord-material:{ownerPlayerId}:{value}",
                    $"Actual lord has an unknown graphics material; a safely cached early culture remains valid: ownerPlayerId={ownerPlayerId}, lordUnitId={lordUnitId}, lordGM={lordMaterial}.");
                if (cultureByPlayer.TryGetValue(ownerPlayerId, out CachedCulture safeCached) &&
                    safeCached.Culture != LordCulture.Unknown)
                {
                    source = safeCached.Source;
                    value = safeCached.Value;
                    return safeCached.Culture;
                }
            }

            if (cultureByPlayer.TryGetValue(ownerPlayerId, out CachedCulture known))
            {
                source = known.Source;
                value = known.Value;
                return known.Culture;
            }

            LordCulture early = TryResolveEarlyCulture(ownerPlayerId, out source, out value);
            if (early != LordCulture.Unknown)
            {
                cultureByPlayer[ownerPlayerId] = new CachedCulture(early, source, value);
                LogOnce($"early-culture:{ownerPlayerId}",
                    $"Early culture resolved before lord spawn: ownerPlayerId={ownerPlayerId}, source={source}, value={value}, culture={early}.");
            }
            return early;
        }

        private LordCulture TryResolveEarlyCulture(int ownerPlayerId, out string source, out int value)
        {
            source = "unresolved";
            value = -1;
            try
            {
                Enums.AILords aiLord = GamePlayerManagerAPI.Instance.GetAILord(ownerPlayerId);
                int aicIndex = (int)aiLord;
                if (aiLord != Enums.AILords.SK_NULL)
                {
                    var aics = GameAIManagerAPI.Instance.GetAICArray();
                    if (aics.GetArrayAddress() != IntPtr.Zero && aicIndex > 0 && aicIndex < aics.Length)
                    {
                        value = aics.GetValue(aicIndex).lord_gfx_type;
                        source = "ai-aic";
                        LordCulture aiCulture = SkinSelectionPolicy.ClassifyLordGraphicsType(value);
                        if (aiCulture != LordCulture.Unknown)
                            return aiCulture;
                        WarnOnce($"ai-culture-unknown:{ownerPlayerId}:{value}",
                            $"AI AIC returned an unknown lord graphics type for ownerPlayerId={ownerPlayerId}: value={value}.");
                    }
                    else
                        WarnOnce($"ai-culture-unavailable:{ownerPlayerId}:{aicIndex}",
                            $"AI AIC entry is unavailable for ownerPlayerId={ownerPlayerId}, aicIndex={aicIndex}.");
                }
            }
            catch (Exception ex)
            {
                WarnOnce($"ai-culture-error:{ownerPlayerId}",
                    $"AI early-culture lookup failed closed for ownerPlayerId={ownerPlayerId}; local lookup remains eligible: {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                if (ownerPlayerId != GamePlayerManagerAPI.Instance.GetLocalPlayerId())
                    return LordCulture.Unknown;
                if (GameData.Instance != null && GameData.Instance.lastGameState != null)
                {
                    value = GameData.Instance.lastGameState.lord_Type;
                    source = "local-game-state";
                    LordCulture gameStateCulture = SkinSelectionPolicy.ClassifyLordGraphicsType(value);
                    if (gameStateCulture != LordCulture.Unknown)
                        return gameStateCulture;
                }
            }
            catch (Exception ex)
            {
                WarnOnce($"local-game-state-culture-error:{ownerPlayerId}",
                    $"Local game-state culture lookup failed closed for ownerPlayerId={ownerPlayerId}; settings lookup remains eligible: {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                value = ConfigSettings.Settings_LordType;
                source = "local-settings";
                return SkinSelectionPolicy.ClassifyLordGraphicsType(value);
            }
            catch (Exception ex)
            {
                WarnOnce($"local-settings-culture-error:{ownerPlayerId}",
                    $"Local settings culture lookup failed closed for ownerPlayerId={ownerPlayerId}: {ex.GetType().Name}: {ex.Message}");
            }
            source = "unresolved";
            value = -1;
            return LordCulture.Unknown;
        }

        private ImageSource ResolveTroopHudImage(UnitHudImageOverrideContext context, UnitHudImageSlot expectedSlot)
        {
            if (context == null || context.Slot != expectedSlot)
                return null;
            bool currentIsVanilla = ReferenceEquals(context.CurrentImage, context.VanillaImage);
            if (!SkinSelectionPolicy.CanInspectEuropeanHud(activeMap, context.Arabic, context.Colour,
                currentIsVanilla))
                return null;

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (!SkinSelectionPolicy.IsValidPlayerId(localPlayerId))
                return null;

            LordCulture culture = ResolveOwnerCulture(localPlayerId, out _, out _, out string source, out int value);
            if (!SkinSelectionPolicy.ShouldUseEuropeanHud(activeMap, context.Arabic, context.Colour, culture))
            {
                if (culture == LordCulture.NonEuropean)
                    LogOnce($"troop-hud-vanilla:{source}:{value}",
                        $"Vanilla troop HUD retained for non-European local lord culture: source={source}, value={value}.");
                return null;
            }

            MainViewModel instance = MainViewModel.Instance;
            if (instance == null)
                return null;
            int slot = TroopHudSlotIndex(expectedSlot);
            EnsureTroopHudSource(instance, context.Colour, slot);
            LogOnce($"troop-hud-applied:{context.Colour}",
                $"SH1DE swordsman HUD activated through APIShared for player colour {context.Colour}: source={source}, value={value}.");
            return troopHudSources[context.Colour - 1, slot];
        }

        private void EnsureTroopHudSource(MainViewModel instance, int colour, int slot)
        {
            if (troopHudSources[colour - 1, slot] != null)
                return;
            troopHudSources[colour - 1, slot] = instance.LoadImageFile(troopHudBytes[colour - 1, slot]);
            if (troopHudSources[colour - 1, slot] == null)
                throw new InvalidOperationException($"Noesis could not decode troop HUD colour={colour}, slot={slot}.");
        }

        private static int TroopHudSlotIndex(UnitHudImageSlot slot)
        {
            switch (slot)
            {
                case UnitHudImageSlot.UIBuildingsO011: return 0;
                case UnitHudImageSlot.UIBuildingsO012: return 1;
                case UnitHudImageSlot.UIButtonsK007: return 2;
                case UnitHudImageSlot.UIButtonsK008: return 3;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private unsafe void SetBuildingTileSpriteHook(GameMapTile tile, int file, int image, int light)
        {
            buildingTrampoline(tile, file, image, light);
            try
            {
                if (tile == null || file != (int)ExtenderGM.GM_CASTLES || image <= 0 ||
                    image >= castleSprites.Length || castleSprites[image] == null || spriteLoader.instance == null)
                    return;
                Sprite expected = spriteLoader.instance.GetGMSprite(GameGM.GM_CASTLES, image);
                if (!ReferenceEquals(tile.tileImage, expected))
                    return;
                int tileId = GameTileManagerAPI.Instance.GetTileId(tile.gameMapX, tile.gameMapY);
                int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(tileId);
                if (buildingId <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                    return;
                if (building->r_BuildingType != eStructs.STRUCT_TOWER5 &&
                    building->r_BuildingType != eStructs.STRUCT_TOWER5_DESTROYED)
                    return;
                int ownerPlayerId = building->r_PlayerIdOwner;
                LordCulture culture = ResolveOwnerCulture(ownerPlayerId, out int lordUnitId,
                    out ExtenderGM lordMaterial, out string source, out int value);
                if (culture == LordCulture.NonEuropean)
                {
                    LogOnce($"tower-vanilla:{source}:{value}",
                        $"Vanilla round tower retained for non-European owner: buildingId={buildingId}, ownerPlayerId={ownerPlayerId}, source={source}, value={value}.");
                    return;
                }
                bool isRoundTower = building->r_BuildingType == eStructs.STRUCT_TOWER5 ||
                    building->r_BuildingType == eStructs.STRUCT_TOWER5_DESTROYED;
                if (!SkinSelectionPolicy.CanReplaceBuilding(true, isRoundTower, ownerPlayerId, culture, castleSprites[image] != null))
                    return;
                tile.tileImage = castleSprites[image];
                LogOnce("tower-skin-applied",
                    $"SH1DE round-tower skin applied: buildingId={buildingId}, ownerPlayerId={ownerPlayerId}, lordUnitId={lordUnitId}, lordGM={lordMaterial}, source={source}, value={value}, image={image}, light={tile.light}.");
            }
            catch (Exception ex)
            {
                WarnOnce("tower-hook-error", $"Round-tower replacement failed closed; the prior result remains active: {ex}");
            }
        }

        private void OnMapStarted()
        {
            activeMap = true;
            troopHudCapability?.RequestRefresh();
            ApplyTowerHud();
        }

        private void ApplyTowerHud()
        {
            MainViewModel viewModel = MainViewModel.Instance;
            if (!activeMap || viewModel == null || viewModel.HUDmain == null)
                return;
            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (!SkinSelectionPolicy.IsValidPlayerId(localPlayerId))
                return;
            LordCulture culture = ResolveOwnerCulture(localPlayerId, out _, out _, out string source, out int value);
            UIElement button = viewModel.HUDmain.FindName("ButtonBuildTowerE") as UIElement;
            if (button == null)
            {
                WarnOnce("tower-hud-button-missing", "ButtonBuildTowerE was not found in the active HUD.");
                return;
            }
            if (!ReferenceEquals(towerButton, button))
            {
                RestoreTowerHud();
                towerButton = button;
                vanillaTowerSprite1 = PropEx.GetSprite1(button);
                vanillaTowerSprite2 = PropEx.GetSprite2(button);
            }
            if (culture != LordCulture.European)
            {
                PropEx.SetSprite1(button, vanillaTowerSprite1);
                PropEx.SetSprite2(button, vanillaTowerSprite2);
                return;
            }
            for (int index = 0; index < towerHudSources.Length; index++)
            {
                if (towerHudSources[index] == null)
                    towerHudSources[index] = viewModel.LoadImageFile(towerHudBytes[index]);
                if (towerHudSources[index] == null)
                    throw new InvalidOperationException($"Noesis could not decode round-tower HUD slot {index}.");
            }
            PropEx.SetSprite1(button, towerHudSources[0]);
            PropEx.SetSprite2(button, towerHudSources[1]);
            LogOnce("tower-hud-applied", $"SH1DE round-tower build HUD activated: source={source}, value={value}.");
        }

        private void RestoreTowerHud()
        {
            if (towerButton != null)
            {
                PropEx.SetSprite1(towerButton, vanillaTowerSprite1);
                PropEx.SetSprite2(towerButton, vanillaTowerSprite2);
            }
            towerButton = null;
            vanillaTowerSprite1 = null;
            vanillaTowerSprite2 = null;
        }

        private static byte[] ReadAssetBytes(GameAssetManagerAPI assets, string path)
        {
            if (!assets.GetModFileBinaryContent(SkinTestPlugin.PluginGuid, path, out byte[] bytes) || bytes == null)
                throw new InvalidOperationException($"Missing indexed mod asset: {path}");
            return bytes;
        }

        private static int ChopMaterialIndex(int chopFeet)
        {
            if (chopFeet <= 0)
                return 0;
            int index = chopFeet / 4;
            return index > 6 ? 6 : index;
        }

        private static string DescribeSprite(Sprite sprite)
        {
            if (ReferenceEquals(sprite, null))
                return "<null>";
            try
            {
                return sprite == null ? "<destroyed>" : $"{sprite.name}#{sprite.GetInstanceID()}";
            }
            catch
            {
                return "<unavailable>";
            }
        }

        private static Texture2D LoadTexture(byte[] bytes, string name)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidOperationException($"Unity could not decode texture '{name}'.");
            }
            UnityEngine.Object.DontDestroyOnLoad(texture);
            return texture;
        }

        private void ClearBindings()
        {
            activeMap = false;
            troopHudCapability?.RequestRefresh();
            RestoreTowerHud();
            unitByRenderer.Clear();
            cultureByPlayer.Clear();
            castleAnimContexts.Clear();
            warnings.Clear();
            LogInfo("Per-map renderer, culture and HUD bindings cleared for map unload.");
        }

        private void WarnOnce(string key, string message)
        {
            if (warnings.Add(key))
                log.LogWarning($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        private void LogOnce(string key, string message)
        {
            if (warnings.Add(key))
                LogInfo(message);
        }

        private void LogInfo(string message) => log.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {message}");

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            for (int index = subscriptions.Count - 1; index >= 0; index--)
                try { subscriptions[index]?.Dispose(); } catch { }
            subscriptions.Clear();
            ReleaseHook();
            unitByRenderer.Clear();
            cultureByPlayer.Clear();
            castleAnimContexts.Clear();
            DestroyAll(alternateSprites);
            DestroyAll(normalSprites);
            DestroyAll(castleSprites);
            DestroyAll(castleAnimSprites);
            DestroyAll(materials);
            if (maskTexture != null) UnityEngine.Object.Destroy(maskTexture);
            if (colourTexture != null) UnityEngine.Object.Destroy(colourTexture);
            if (castleTexture != null) UnityEngine.Object.Destroy(castleTexture);
            if (castleAnimTexture != null) UnityEngine.Object.Destroy(castleAnimTexture);
            alternateSprites = null;
            normalSprites = null;
            castleSprites = null;
            castleAnimSprites = null;
            materials = null;
            maskTexture = null;
            colourTexture = null;
            castleTexture = null;
            castleAnimTexture = null;
            LogInfo("Hook, bindings and private graphics resources released.");
        }

        private readonly struct CachedCulture
        {
            public readonly LordCulture Culture;
            public readonly string Source;
            public readonly int Value;

            public CachedCulture(LordCulture culture, string source, int value)
            {
                Culture = culture;
                Source = source;
                Value = value;
            }
        }

        private sealed class CastleAnimContext
        {
            public int BuildingId { get; }
            public int OwnerPlayerId { get; }
            public int Image { get; }
            public LordCulture Culture { get; }
            public int LordUnitId { get; }
            public ExtenderGM LordMaterial { get; }
            public string Source { get; }
            public int Value { get; }

            public CastleAnimContext(int buildingId, int ownerPlayerId, int image, LordCulture culture,
                int lordUnitId, ExtenderGM lordMaterial, string source, int value)
            {
                BuildingId = buildingId;
                OwnerPlayerId = ownerPlayerId;
                Image = image;
                Culture = culture;
                LordUnitId = lordUnitId;
                LordMaterial = lordMaterial;
                Source = source;
                Value = value;
            }
        }

        private void ReleaseHook()
        {
            ReleaseSingleHook(ref wallFillinHook);
            wallFillinTrampoline = null;
            ReleaseSingleHook(ref buildingAnimHook);
            buildingAnimTrampoline = null;
            ReleaseSingleHook(ref buildingHook);
            buildingTrampoline = null;
            ReleaseSingleHook(ref hook);
            trampoline = null;
        }

        private static void ReleaseSingleHook(ref Hook hookField)
        {
            Hook current = hookField;
            hookField = null;
            if (current == null) return;
            try { current.Undo(); } catch { }
            try { current.Dispose(); } catch { }
        }

        private static void DestroyAll<T>(T[] values) where T : UnityEngine.Object
        {
            if (values == null) return;
            foreach (T value in values)
                if (value != null) UnityEngine.Object.Destroy(value);
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
