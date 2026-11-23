using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildShaderBundle
{
    public static void Build()
    {
        const string shaderPath = "Assets/ProjectileTrails.shader";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("The projectile shader did not import successfully.");

        const BuildTarget target = BuildTarget.StandaloneWindows64;
        PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
        PlayerSettings.SetGraphicsAPIs(target, new[] {
            GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan
        });
        string output = Path.GetFullPath("Bundles");
        Directory.CreateDirectory(output);
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] { new AssetBundleBuild
        {
            assetBundleName = "projectiletrails-shader",
            assetNames = new[] { shaderPath }
        }}, BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.UncompressedAssetBundle, target);
        if (manifest == null || !File.Exists(Path.Combine(output, "projectiletrails-shader")))
            throw new InvalidOperationException("The projectile shader bundle was not built.");
    }
}
