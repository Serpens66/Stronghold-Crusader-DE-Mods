using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace BepInEx;

/// <summary>
/// The base plugin type that is used by the BepInEx plugin loader.
/// </summary>
public abstract class BaseUnityPlugin : MonoBehaviour
{
	/// <summary>
	/// Information about this plugin as it was loaded.
	/// </summary>
	public PluginInfo Info { get; }

	/// <summary>
	/// Logger instance tied to this plugin.
	/// </summary>
	protected ManualLogSource Logger { get; }

	/// <summary>
	/// Default config file tied to this plugin. The config file will not be created until 
	/// any settings are added and changed, or <see cref="M:BepInEx.Configuration.ConfigFile.Save" /> is called.
	/// </summary>
	public ConfigFile Config { get; }

	/// <summary>
	/// Create a new instance of a plugin and all of its tied in objects.
	/// </summary>
	/// <exception cref="T:System.InvalidOperationException">BepInPlugin attribute is missing.</exception>
	protected BaseUnityPlugin()
	{
		BepInPlugin metadata = MetadataHelper.GetMetadata(this);
		if (metadata == null)
		{
			throw new InvalidOperationException("Can't create an instance of " + ((object)this).GetType().FullName + " because it inherits from BaseUnityPlugin and the BepInPlugin attribute is missing.");
		}
		if (!Chainloader.IsEditor && Chainloader.PluginInfos.TryGetValue(metadata.GUID, out var value))
		{
			Info = value;
		}
		else
		{
			Info = new PluginInfo
			{
				Metadata = metadata,
				Instance = this,
				Dependencies = MetadataHelper.GetDependencies(((object)this).GetType()),
				Processes = MetadataHelper.GetAttributes<BepInProcess>(((object)this).GetType()),
				Location = ((object)this).GetType().Assembly.Location
			};
		}
		Logger = BepInEx.Logging.Logger.CreateLogSource(metadata.Name);
		string text = (Chainloader.IsEditor ? "." : Paths.ConfigPath);
		Config = new ConfigFile(Utility.CombinePaths(text, metadata.GUID + ".cfg"), saveOnInit: false, metadata);
	}
}
