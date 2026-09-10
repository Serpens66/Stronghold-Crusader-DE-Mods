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
using ExtenderGM = SHCDESE.Interop.GM;
using GameGM = Enums.GM;

namespace SkinTest
{
    internal sealed class SwordsmanSkinRuntime : IDisposable
    {
        private const string AtlasPath = "Assets/CrusaderSwordsman/atlas.png";
        private const string MaskPath = "Assets/CrusaderSwordsman/atlas_m.png";
        private const string ManifestPath = "Assets/CrusaderSwordsman/atlas.json";
        private delegate void SetBodySpriteDelegate(SpriteRenderer renderer, int file, int image, int colour,
            bool alternateFrame, int chopFeet, int transparency);

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
        private Hook hook;
        private SetBodySpriteDelegate trampoline;
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
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => ClearBindings()));
                LogInfo($"Validated private atlas: normal={normalSprites.Length}, alternate={alternateSprites.Length}, mask=yes.");
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
        }

        private void InstallHook()
        {
            MethodInfo method = typeof(SpriteMapping).GetMethod(nameof(SpriteMapping.SetBodySprite),
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SpriteRenderer), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(int) }, null)
                ?? throw new MissingMethodException(nameof(SpriteMapping), nameof(SpriteMapping.SetBodySprite));
            hook = new Hook(method, (SetBodySpriteDelegate)SetBodySpriteHook);
            trampoline = hook.GenerateTrampoline<SetBodySpriteDelegate>();
            LogInfo("Managed SpriteMapping.SetBodySprite hook installed after the existing hook chain.");
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
                if (renderer == null || file != (int)ExtenderGM.GM_BODY_SWORDSMAN)
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
                cultureByPlayer[ownerPlayerId] = new CachedCulture(actual, source, value);
                return actual;
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

        private static LordCulture TryResolveEarlyCulture(int ownerPlayerId, out string source, out int value)
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
                        return SkinSelectionPolicy.ClassifyLordGraphicsType(value);
                    }
                }

                if (ownerPlayerId == GamePlayerManagerAPI.Instance.GetLocalPlayerId())
                {
                    if (GameData.Instance != null && GameData.Instance.lastGameState != null)
                    {
                        value = GameData.Instance.lastGameState.lord_Type;
                        source = "local-game-state";
                    }
                    else
                    {
                        value = ConfigSettings.Settings_LordType;
                        source = "local-settings";
                    }
                    return SkinSelectionPolicy.ClassifyLordGraphicsType(value);
                }
            }
            catch
            {
                source = "unresolved";
                value = -1;
            }
            return LordCulture.Unknown;
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
            unitByRenderer.Clear();
            cultureByPlayer.Clear();
            warnings.Clear();
            LogInfo("Renderer bindings cleared for map unload.");
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
            DestroyAll(alternateSprites);
            DestroyAll(normalSprites);
            DestroyAll(materials);
            if (maskTexture != null) UnityEngine.Object.Destroy(maskTexture);
            if (colourTexture != null) UnityEngine.Object.Destroy(colourTexture);
            alternateSprites = null;
            normalSprites = null;
            materials = null;
            maskTexture = null;
            colourTexture = null;
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

        private void ReleaseHook()
        {
            Hook current = hook;
            hook = null;
            trampoline = null;
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
