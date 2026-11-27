using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using APIShared;
using APIShared.Presentation;
using BepInEx.Bootstrap;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.API.Components.Sprite;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;
using ExtenderGM = SHCDESE.Interop.GM;
using TilePosition = System.Numerics.Vector2;

namespace UiAcceptance
{
    internal static class SkinVisualSteps
    {
        private const string SkinGuid = "SkinTest_Serp";
        private static int towerX, towerY, towerId, swordsmanId, previewX, previewY;
        private static long session;
        private static Sprite towerPaint, previewPaint, previewReference;
        private static bool invalidReference, observingPreview;
        private static int referenceCount, castleVisibleCount;
        private static GameMapTile previewTile;
        private static readonly Dictionary<GameMapTile, Sprite> references = new Dictionary<GameMapTile, Sprite>();

        internal static void Register()
        {
            Runner.Register("Skin.Configure", Configure);
            Runner.Register("Skin.World", World);
            Runner.Register("Skin.Override", Override);
            Runner.Register("Skin.Preview", Preview);
            Runner.Register("Skin.AnimationLease", AnimationLease);
        }

        private static IEnumerable<Wait> Configure(Step step)
        {
            Runner.Require(Chainloader.PluginInfos.ContainsKey(SkinGuid), "SkinTest package is not loaded");
            ConfigSettings.Settings_LordType = 0; // Vanilla's default Crusader lord, checked on the native unit after start.
            Runner.Require(PaintEvents.TryRegisterTileGraphic(UiAcceptancePlugin.Guid, "skin-acceptance-observer",
                ObserveTile, out string reason, order: 900), reason);
            yield break;
        }

        private static IEnumerable<Wait> World(Step step)
        {
            session = UiAcceptancePlugin.LastContext?.SessionId ?? 0;
            Runner.Require(session > 0 && !UiAcceptancePlugin.LastContext.IsEditor &&
                !UiAcceptancePlugin.LastContext.Mode.IsRealMultiplayer, "expected a local running gameplay session");
            var camera = CameraControls2D.instance;
            Runner.Require(camera != null && PerfectPixelWithZoom.instance != null, "camera is not initialized");
            float oldX = camera.getCameraXPos(), oldY = camera.getCameraYPos();
            int steady = 0;
            while (steady < 3)
            {
                yield return new Delay(500);
                float x = camera.getCameraXPos(), y = camera.getCameraYPos();
                steady = x == oldX && y == oldY ? steady + 1 : 0;
                oldX = x; oldY = y;
            }
            CreateFixture();
            GamePlayerManagerAPI.Instance.SetScreenCenterToTilePosition(new TilePosition(towerX, towerY));
            PerfectPixelWithZoom.instance.SetZoomImmediate(2f);
            SpriteRenderer renderer = null;
            yield return new Until("painted round tower and swordsman on the live viewport", () =>
                towerPaint != null && (renderer = ReadUnitRenderer(swordsmanId)) != null &&
                IsSkinSprite(renderer.sprite) && renderer.enabled && renderer.gameObject.activeInHierarchy);
            AcceptLog.Info("SKIN_WORLD tower=" + towerId + " at=" + towerX + "," + towerY +
                " towerSprite=" + towerPaint.name + " swordsman=" + swordsmanId + " body=" + renderer.sprite.name +
                " zoom=" + PerfectPixelWithZoom.instance.getZoom());
            GameSteps.CaptureScreenshot("skin-world");
        }

        private static IEnumerable<Wait> Override(Step step)
        {
            Runner.Require(session == UiAcceptancePlugin.LastContext?.SessionId, "the sprite fixture is no longer active");
            Chimp chimp = ReadUnit(swordsmanId);
            Runner.Require(chimp != null && chimp.file1 == (int)ExtenderGM.GM_BODY_SWORDSMAN &&
                IsSkinSprite(chimp.sprRenderer.sprite), "the local swordsman must already display SkinTest");
            const BindingFlags instanceField = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(GamePlayerManagerAPI).Assembly.GetType("SHCDESE.API.GameSpriteManagerAPI");
            var instance = type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var register = type?.GetMethod("RegisterCustomMaterials", instanceField, null,
                new[] { typeof(int), typeof(Material[]), typeof(SpriteMaterialMode) }, null);
            var get = type?.GetMethod("TryGetCustomMaterials", BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(Material[]).MakeByRefType(), typeof(SpriteMaterialMode).MakeByRefType() }, null);
            var cache = type?.GetField("_customMaterialCache", instanceField)?.GetValue(instance) as IDictionary;
            Runner.Require(instance != null && register != null && get != null && cache != null,
                "the Extender sprite material ownership contract changed");

            // Substitute only the exact active GM slot and mark the new sprite through the Extender.
            // The real SpriteMapping/Extender/SkinTest hook chain then paints the real swordsman.
            Sprite normal = SpriteMapping.getBodyImage(chimp.file1, chimp.image1, chimp.altFrame1Set);
            int index = chimp.image1 - 1;
            var loader = spriteLoader.instance;
            Sprite[] FindRow(string name)
            {
                var table = typeof(spriteLoader).GetField(name, instanceField)?.GetValue(loader) as Sprite[][];
                return table != null && chimp.file1 >= 0 && chimp.file1 < table.Length ? table[chimp.file1] : null;
            }
            Sprite[] row = chimp.altFrame1Set ? FindRow("gmAltSprites") : null;
            if (row == null || index < 0 || index >= row.Length || !ReferenceEquals(row[index], normal))
                row = FindRow("gmSprites");
            Runner.Require(normal != null && row != null && index >= 0 && index < row.Length &&
                ReferenceEquals(row[index], normal), "the live swordsman atlas slot changed");
            Material previousMaterial = chimp.sprRenderer.sharedMaterial;
            Runner.Require(previousMaterial != null, "the painted fixture has no material to clone");
            var probe = UnityEngine.Object.Instantiate(normal);
            probe.name = "UiAcceptance_ExtenderOverride";
            int id = probe.GetInstanceID();
            bool registeredProbe = false;
            var material = new Material(previousMaterial);
            void Paint() => SpriteMapping.SetBodySprite(chimp.sprRenderer, chimp.file1, chimp.image1,
                chimp.colour1, chimp.altFrame1Set, chimp.chopFeet, chimp.transparency);
            try
            {
                Runner.Require(!cache.Contains(id), "the test override has no independent sprite identity");
                row[index] = probe;
                registeredProbe = true;
                register.Invoke(instance, new object[] { id, new[] { material }, SpriteMaterialMode.Plain });
                object[] lookup = { id, null, SpriteMaterialMode.Auto };
                Runner.Require((bool)get.Invoke(instance, lookup) && ReferenceEquals(lookup[1] is Material[] registered ? registered[0] : null, material),
                    "the Extender did not register the fixture sprite");
                Runner.Require(ReferenceEquals(SpriteMapping.getBodyImage(chimp.file1, chimp.image1, chimp.altFrame1Set), probe),
                    "Vanilla did not resolve the replacement GM slot");
                Paint();
                Runner.Require(ReferenceEquals(chimp.sprRenderer.sprite, probe) &&
                    ReferenceEquals(chimp.sprRenderer.sharedMaterial, material),
                    "SkinTest replaced an Extender-owned sprite or its material");
                AcceptLog.Info("SKIN_FOREIGN_OVERRIDE id=" + swordsmanId + " exactSprite=true exactMaterial=true");
            }
            finally
            {
                row[index] = normal;
                if (registeredProbe) cache.Remove(id);
                Paint();
                UnityEngine.Object.Destroy(probe);
                UnityEngine.Object.Destroy(material);
            }
            Runner.Require(IsSkinSprite(chimp.sprRenderer.sprite), "SkinTest did not reapply after the scoped override was removed");
            yield break;
        }

        private static IEnumerable<Wait> AnimationLease(Step step)
        {
            Runner.Require(session == UiAcceptancePlugin.LastContext?.SessionId, "the sprite fixture is no longer active");
            lock (HuntSteps.Engine())
            {
                var map = GameMap.instance;
                var anims = ReadVisuals<BuildingAnim>("buildingAnims");
                var fillins = ReadVisuals<WallFillin>("wallFillins");
                var tiles = GameTileManagerAPI.Instance;
                const int file = (int)ExtenderGM.GM_CASTLE_ANIMS;
                const int frame = 4; // This round-tower frame was observed in the native draw list of this fixture.
                const int probeId = 9_000_001;
                Runner.Require(!anims.ContainsKey(probeId) && !fillins.ContainsKey(probeId), "the lease test id is already in use");
                map.mapGameTileToTilemapCoord(towerX, towerY, out int x, out int y);
                GameMapTile towerTile = map.getMapTile(x, y);
                Runner.Require(towerTile != null && tiles.GetTileBuildingId(tiles.GetTileId(towerTile.gameMapX, towerTile.gameMapY)) == towerId,
                    "the animation probe is not targeting the verified round tower tile");
                var manager = GameBuildingManagerAPI.Instance;
                int owner = manager.GetOwner(towerId);
                Runner.Require(owner == GamePlayerManagerAPI.Instance.GetLocalPlayerId() && owner > 0,
                    "the live tower is not owned by the local player");
                Sprite vanilla = SpriteMapping.getBodyImage(file, frame, false);
                Runner.Require(vanilla != null && !IsSkinSprite(vanilla), "the live tower frame lacks a Vanilla sprite");

                // Reuse a single real renderer in each layer and keep the render inputs identical across ownership changes.
                void Update() {
                    map.addUpdateBuildingAnim(probeId, x, y, 0, 0, 0, file, frame, 0, 0, 0);
                    map.addUpdateWallFillin(probeId, x, y, 0, frame, 0);
                }
                try
                {
                    Update();
                    Runner.Require(anims.TryGetValue(probeId, out BuildingAnim anim) && IsSkinSprite(anim.sprRenderer?.sprite),
                        "the lease probe did not paint an animation variant");
                    Runner.Require(fillins.TryGetValue(probeId, out WallFillin fill) && IsSkinSprite(fill.sprRenderer?.sprite),
                        "the lease probe did not paint a wall-fillin variant");
                    SpriteRenderer animRenderer = anim.sprRenderer, fillRenderer = fill.sprRenderer;
                    manager.SetOwner(towerId, 0);
                    Runner.Require(manager.GetOwner(towerId) == 0, "the temporary unowned state did not apply");
                    Update();
                    Runner.Require(ReferenceEquals(anim.sprRenderer, animRenderer) && ReferenceEquals(fill.sprRenderer, fillRenderer) &&
                        ReferenceEquals(animRenderer.sprite, vanilla) && ReferenceEquals(fillRenderer.sprite, vanilla),
                        "an unchanged layer frame retained SkinTest artwork after losing its owner");
                    manager.SetOwner(towerId, owner);
                    Update();
                    Runner.Require(manager.GetOwner(towerId) == owner && IsSkinSprite(animRenderer.sprite) && IsSkinSprite(fillRenderer.sprite),
                        "the same layer renderers did not pick up the owner's artwork again");
                    AcceptLog.Info("SKIN_ANIMATION_LEASE tower=" + towerId + " frame=" + frame + " animationRestored=true wallFillRestored=true");
                }
                finally
                {
                    if (manager.GetOwner(towerId) != owner) manager.SetOwner(towerId, owner);
                    map.deleteBuildingAnim(probeId);
                    map.deleteWallFillin(probeId);
                }
            }
            yield break;
        }

        private static unsafe void CreateFixture()
        {
            lock (HuntSteps.Engine())
            {
                int owner = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                Runner.Require(LordSkins.TryGetForPlayer(owner, out LordSkin skin) && skin == LordSkin.Crusader,
                    "the local fixture has no verified Crusader lord");
                Runner.Require(BuildingAccess.TryGetById(GamePlayerManagerAPI.Instance.GetPlayerKeepId(owner),
                    out GameBuilding* keep, out _) && keep != null, "the local keep is not ready");
                int keepX = keep->r_TilePositionXBegin, keepY = keep->r_TilePositionYBegin;
                Runner.Require(HuntSteps.Place(owner, keepX, keepY, eMappers.MAPPER_TOWER5,
                    eStructs.STRUCT_TOWER5, 9, out towerX, out towerY), "unable to place the local round tower near the keep");
                var tiles = GameTileManagerAPI.Instance;
                towerId = tiles.GetTileBuildingId(tiles.GetTileId(towerX, towerY));
                Runner.Require(towerId > 0 && BuildingAccess.TryGetById(towerId, out GameBuilding* tower, out _) &&
                    tower->r_BuildingType == eStructs.STRUCT_TOWER5 && tower->r_PlayerIdOwner == owner,
                    "the verified fixture tile does not belong to the round tower");
                FindClearSquare(towerX, towerY, 3, 1, out int x, out int y);
                swordsmanId = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y,
                    tiles.GetTileHeight(tiles.GetTileId(x, y)), eChimps.CHIMP_TYPE_SWORDSMAN));
                Runner.Require(swordsmanId > 0 && UnitAccess.TryGetById(swordsmanId, out GameUnit* sword, out _) &&
                    sword->r_UnitChimp == eChimps.CHIMP_TYPE_SWORDSMAN && sword->r_ControllableForPlayerId == owner,
                    "the generated unit is not the local swordsman");
            }
        }

        private static IEnumerable<Wait> Preview(Step step)
        {
            yield return new Delay(500); // Let the preceding world screenshot finish before moving the camera.
            Runner.Require(session == UiAcceptancePlugin.LastContext?.SessionId && towerPaint != null,
                "the live sprite fixture is no longer active");
            const Enums.eMappers mapper = Enums.eMappers.MAPPER_TOWER5;
            var players = GamePlayerManagerAPI.Instance;
            int owner = players.GetLocalPlayerId();
            Runner.Require(owner == EditorDirector.instance.ActivePlayerID, "the tower preview requires the local player's own view");
            EditorDirector.instance.placeBuildingInteraction(mapper);
            Runner.Require(MainControls.instance.CurrentAction == 5 && MainControls.instance.CurrentSubAction == (int)mapper,
                "Vanilla did not select the round tower build interaction");
            AcceptLog.Info("SKIN_PREVIEW_SETUP owner=" + owner + " stone=" + players.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS));
            var candidates = new List<(int X, int Y)>();
            lock (HuntSteps.Engine())
            {
                foreach (var point in ClearSquares(towerX, towerY, 7, 3))
                {
                    if (candidates.Exists(other => Math.Abs(point.X - other.X) < 3 && Math.Abs(point.Y - other.Y) < 3)) continue;
                    candidates.Add(point);
                    if (candidates.Count == 5) break;
                }
            }
            Runner.Require(candidates.Count > 0, "no flat and unobstructed round tower preview site is available");
            var camera = CameraControls2D.instance;
            bool cameraWasLocked = camera.isMapLocked();
            bool cameraCouldMove = camera.AllowMove;
            float cameraX = camera.getCameraXPos(), cameraY = camera.getCameraYPos();
            try
            {
                // Vanilla camera bounds move this edge-of-map fixture outside the viewport at the test zoom.
                if (cameraWasLocked) camera.toggleMapLocked();
                camera.AllowMove = false;
                foreach (var point in candidates)
                {
                    previewX = point.X; previewY = point.Y;
                    players.SetScreenCenterToTilePosition(new TilePosition(previewX, previewY));
                    yield return new Delay(500);
                    GameMap.instance.mapGameTileToTilemapCoord(previewX, previewY, out int mapX, out int mapY);
                    GameMapTile hoverTile = GameMap.instance.getMapTile(mapX, mapY);
                    Runner.Require(hoverTile != null && hoverTile.gameMapX == previewX && hoverTile.gameMapY == previewY,
                        "the preview target is outside the live tile-map");
                    Vector3 world = GameMap.instance.getSpritePosVector(mapX, mapY);
                    world.y += hoverTile.height;
                    Vector3 viewport = Camera.main.WorldToViewportPoint(world);
                    Runner.Require(viewport.x > 0.1f && viewport.x < 0.9f && viewport.y > 0.15f && viewport.y < 0.9f,
                        "the preview target is outside the playable viewport: " + viewport);
                    referenceCount = castleVisibleCount = 0;
                    references.Clear();
                    observingPreview = true;
                    lock (HuntSteps.Engine()) InputSteps.HoverBuilding(previewX, previewY, (int)mapper, owner);
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    while (!invalidReference && previewPaint == null && clock.ElapsedMilliseconds < 1250)
                    {
                        Runner.Require(MainControls.instance.CurrentAction == 5, "Vanilla canceled the round tower preview");
                        yield return NextFrame.Instance;
                    }
                    AcceptLog.Info("SKIN_PREVIEW_HOVER tile=" + previewX + "," + previewY +
                        " camera=" + GameMap.instance.ScreenCentreTileX + "," + GameMap.instance.ScreenCentreTileY +
                        " nativeCalls=" + InputSteps.BuildingHoverCalls + " status=" + InputSteps.BuildingHoverStatus +
                        " referencePasses=" + referenceCount + " castleVisiblePasses=" + castleVisibleCount);
                    if (previewPaint != null || invalidReference) break;
                    lock (HuntSteps.Engine()) InputSteps.ClearHoverTile();
                    observingPreview = false;
                }
                yield return new Delay(300);
                GameSteps.CaptureScreenshot(previewPaint == null ? "skin-preview-failed" : "skin-preview");
                yield return new Delay(750);
                Runner.Require(!invalidReference && previewTile != null && previewPaint != null && previewReference != null,
                    "no valid SkinTest tower preview at " + previewX + "," + previewY +
                    " referencePasses=" + referenceCount + " castleVisiblePasses=" + castleVisibleCount +
                    " referenceReplaced=" + invalidReference);
                Runner.Require(ReferenceEquals(previewTile.tileImage, previewPaint) &&
                    ReferenceEquals(previewTile.constructionOrigImage, previewReference) &&
                    !IsSkinSprite(previewTile.constructionOrigImage), "the stored preview hit-test reference was replaced");
                AcceptLog.Info("SKIN_PREVIEW at=" + previewX + "," + previewY +
                    " visible=" + previewPaint.name + " reference=" + previewReference.name);
            }
            finally
            {
                lock (HuntSteps.Engine()) InputSteps.ClearHoverTile();
                EditorDirector.instance.CancelPlacement();
                camera.AllowMove = cameraCouldMove;
                if (camera.isMapLocked() != cameraWasLocked) camera.toggleMapLocked();
                camera.setCameraPos(cameraX, cameraY);
                GameMap.instance.PreCalcScreenCentre();
                observingPreview = false;
                references.Clear();
            }
        }

        private static void FindClearSquare(int aroundX, int aroundY, int minimum, int halfWidth, out int x, out int y)
        {
            foreach (var point in ClearSquares(aroundX, aroundY, minimum, halfWidth))
            {
                x = point.X; y = point.Y;
                return;
            }
            throw new StepFailure("no walkable, empty test square near the round tower");
        }

        private static IEnumerable<(int X, int Y)> ClearSquares(int aroundX, int aroundY, int minimum, int halfWidth)
        {
            var tiles = GameTileManagerAPI.Instance;
            for (int radius = minimum; radius < minimum + 25; radius++)
                foreach (var offset in new[] { (radius, 0), (-radius, 0), (0, radius), (0, -radius) })
                {
                    int x = aroundX + offset.Item1, y = aroundY + offset.Item2;
                    if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                    int height = tiles.GetTileHeight(tiles.GetTileId(x, y));
                    bool clear = true;
                    for (int dx = -halfWidth; dx <= halfWidth && clear; dx++)
                        for (int dy = -halfWidth; dy <= halfWidth && clear; dy++)
                        {
                            int tileX = x + dx, tileY = y + dy;
                            if (!tiles.IsTileInsideMapBounds(tileX, tileY))
                            {
                                clear = false;
                                break;
                            }
                            int id = tiles.GetTileId(tileX, tileY);
                            clear = tiles.IsTileWalkableAndUnoccupied(id) && tiles.GetTileHeight(id) == height;
                        }
                    if (clear) yield return (x, y);
                }
        }

        private static void ObserveTile(ref TileGraphicPaint paint)
        {
            if (session == 0 || session != UiAcceptancePlugin.LastContext?.SessionId || paint.Tile == null) return;
            GameMapTile tile = paint.Tile;
            if (!observingPreview)
            {
                if (paint.Pass == TileGraphicPass.VisibleTile && paint.ClaimedBy == SkinGuid &&
                    IsSkinSprite(tile.tileImage) && Math.Abs(tile.gameMapX - towerX) <= 5 &&
                    Math.Abs(tile.gameMapY - towerY) <= 5)
                    towerPaint = tile.tileImage;
                return;
            }
            if (Math.Abs(tile.gameMapX - previewX) > 6 || Math.Abs(tile.gameMapY - previewY) > 6) return;
            if (paint.Pass == TileGraphicPass.ConstructionReference)
            {
                referenceCount++;
                if (IsSkinSprite(tile.tileImage)) invalidReference = true;
                references[tile] = tile.tileImage;
            }
            else if (paint.Pass == TileGraphicPass.VisibleTile && paint.File == (int)ExtenderGM.GM_CASTLES)
            {
                castleVisibleCount++;
                if (paint.ClaimedBy == SkinGuid && IsSkinSprite(tile.tileImage) &&
                    references.TryGetValue(tile, out Sprite reference) && reference != null &&
                    ReferenceEquals(tile.constructionOrigImage, reference))
                {
                    previewTile = tile;
                    previewPaint = tile.tileImage;
                    previewReference = reference;
                }
            }
        }

        private static SpriteRenderer ReadUnitRenderer(int id) => ReadUnit(id)?.sprRenderer;

        private static Chimp ReadUnit(int id)
        {
            var units = ReadVisuals<Chimp>("chimps");
            return units.TryGetValue(id, out Chimp chimp) ? chimp : null;
        }

        private static Dictionary<int, T> ReadVisuals<T>(string name)
        {
            var field = typeof(GameMap).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Runner.Require(field?.FieldType == typeof(Dictionary<int, T>), "private live renderer lookup changed for " + name);
            return (Dictionary<int, T>)field.GetValue(GameMap.instance);
        }

        private static bool IsSkinSprite(Sprite sprite) =>
            sprite != null && sprite.name.StartsWith("SkinTest_", StringComparison.Ordinal);
    }
}
