using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;
using SHCDESE.API.Components.ModManager;
using SHCDESE.IO;
using SHCDESE.Logging;

namespace SHCDESE.API.Components.Archive;

/// <summary>
/// Represents a zip archive that is appended to a .map or .sav file.
/// This class handles finding, reading, and interacting with the embedded zip data.
/// It implements <see cref="T:System.IDisposable" /> to manage underlying file streams.
/// </summary>
public sealed class MapArchive : IDisposable
{
	/// <summary>
	/// Provides a stream source for SharpZipLib from an existing stream.
	/// </summary>
	public class StreamDataSource : IStaticDataSource
	{
		private readonly Stream _stream;

		public StreamDataSource(Stream stream)
		{
			_stream = stream;
		}

		public Stream GetSource()
		{
			_stream.Position = 0L;
			return _stream;
		}
	}

	private const int MAX_SIZE = 33554432;

	private long _archiveOffset;

	private string _filePath;

	/// <summary>
	/// Gets a value indicating whether the map archive was successfully found and loaded.
	/// </summary>
	public bool IsValid = true;

	/// <summary>
	/// Gets the underlying <see cref="T:ICSharpCode.SharpZipLib.Zip.ZipFile" /> instance for archive operations.
	/// </summary>
	public ZipFile? Archive { get; private set; }

	/// <summary>
	/// Gets the deserialized map metadata from 'info.json', if present.
	/// </summary>
	public ModInfo? Info { get; private set; }

	/// <summary>
	/// Gets the <see cref="T:System.IO.MemoryStream" /> that holds the raw zip archive data.
	/// </summary>
	public MemoryStream? ArchiveStream { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="T:SHCDESE.API.Components.Archive.MapArchive" /> class by reading zip data from a given file path.
	/// </summary>
	/// <param name="filePath">The path to the map file to scan for an appended zip archive.</param>
	public MapArchive(string filePath)
	{
		_filePath = filePath;
		long num = ZipUtil.FindEocdHeaderIndex(_filePath);
		if (num == -1)
		{
			LogHelper.Warning("ZIP EOCD-Header not found for [" + _filePath + "]. Not a valid archive.", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			IsValid = false;
			return;
		}
		_archiveOffset = ZipUtil.FindFirstZipHeaderIndex(_filePath);
		if (_archiveOffset == -1)
		{
			LogHelper.Information("EOCD found but no file headers. Treating as an empty ZIP archive.", ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			_archiveOffset = num;
		}
		LogHelper.Information("Reading ZIP data from offset: " + _archiveOffset.ToString("X8"), ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
		try
		{
			using (FileStream fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				fileStream.Seek(_archiveOffset, SeekOrigin.Begin);
				MemoryStream memoryStream = (ArchiveStream = new MemoryStream(checked((int)(fileStream.Length - _archiveOffset))));
				fileStream.CopyTo(memoryStream);
				memoryStream.Position = 0L;
				Archive = new ZipFile(memoryStream, leaveOpen: true);
			}
			IsValid = true;
			TryReadInfo();
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Failed to process ZIP archive for [" + _filePath + "]. Exception: " + ex.Message, ".ctor", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			IsValid = false;
			Archive?.Close();
			ArchiveStream?.Dispose();
		}
	}

	private MapArchive()
	{
	}

	/// <summary>
	/// Creates a new empty MapArchive without attempting to load existing data.
	/// Used for when you need to create an archive for a file that doesn't have one yet.
	/// </summary>
	/// <param name="filePath">The path to the .map or .sav file.</param>
	/// <returns>A new empty MapArchive instance.</returns>
	public static MapArchive CreateEmpty(string filePath)
	{
		LogHelper.Information("Creating empty archive for [" + filePath + "]", "CreateEmpty", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
		MapArchive obj = new MapArchive
		{
			_filePath = filePath,
			_archiveOffset = 0L,
			ArchiveStream = new MemoryStream()
		};
		obj.Archive = new ZipFile(obj.ArchiveStream, leaveOpen: true);
		obj.IsValid = true;
		LogHelper.Information("Empty archive created successfully", "CreateEmpty", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
		return obj;
	}

	/// <summary>
	/// Attempts to load a map archive from the specified file path.
	/// </summary>
	/// <param name="filePath">The path of the file to load.</param>
	/// <param name="mapArchive">When this method returns, contains the loaded <see cref="T:SHCDESE.API.Components.Archive.MapArchive" /> if successful, or <c>null</c> otherwise.</param>
	/// <returns><c>true</c> if the archive was found and loaded successfully; otherwise, <c>false</c>.</returns>
	public static bool TryLoad(string filePath, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MapArchive? mapArchive)
	{
		mapArchive = null;
		if (!File.Exists(filePath))
		{
			LogHelper.Error("File not found: " + filePath, "TryLoad", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			return false;
		}
		mapArchive = new MapArchive(filePath);
		if (!mapArchive.IsValid)
		{
			mapArchive = null;
			return false;
		}
		if (mapArchive.Info == null)
		{
			LogHelper.Warning("No info.json found: " + filePath, "TryLoad", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
		}
		if (!mapArchive.IsArchiveSafe())
		{
			LogHelper.Warning("Archive has been deemed not safe: " + filePath, "TryLoad", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			mapArchive.Dispose();
			mapArchive = null;
			return false;
		}
		return true;
	}

	/// <summary>
	/// Checks if the map archive has any unsafe files inside. (Crude)
	/// </summary>
	/// <returns>True if dangerous files found inside, false otherwise.</returns>
	private bool IsArchiveSafe()
	{
		if (Archive == null)
		{
			return true;
		}
		if (Info != null && Info.Manifest == ModManifest.BepInEx)
		{
			return true;
		}
		string[] array = new string[9] { "exe", "dll", "dylib", "so", "bat", "sh", "ps1", "pwsh", "cmd" };
		foreach (ZipEntry item in Archive)
		{
			if (item == null)
			{
				continue;
			}
			string[] array2 = array;
			foreach (string value in array2)
			{
				if (item.Name.EndsWith(value))
				{
					return false;
				}
			}
		}
		return true;
	}

	/// <summary>
	/// Attempts to read and deserialize 'info.json' from the archive root.
	/// </summary>
	/// <returns>True if 'info.json' was found and deserialized successfully, false otherwise.</returns>
	private bool TryReadInfo()
	{
		string text = TryReadTextFile("info.json");
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		Info = JsonSerializer.Deserialize<ModInfo>(text);
		return true;
	}

	/// <summary>
	/// Generates a string listing all file and directory entries in the archive.
	/// </summary>
	/// <returns>A formatted string of archive contents.</returns>
	public string PrintAllEntries()
	{
		if (Archive == null)
		{
			LogHelper.Warning("Archive is null", "PrintAllEntries", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (ZipEntry item in Archive)
		{
			if (item != null)
			{
				if (item.IsFile)
				{
					stringBuilder.AppendLine("FILE:\t" + item.Name + "]");
				}
				else if (item.IsDirectory)
				{
					stringBuilder.AppendLine("DIR.:\t" + item.Name);
				}
			}
		}
		return stringBuilder.ToString();
	}

	/// <summary>
	/// Returns a string that represents the current object.
	/// </summary>
	/// <returns>A string containing the number of files in the archive.</returns>
	public override string ToString()
	{
		return $"ZIP: files=[{Archive?.Count}]";
	}

	/// <summary>
	/// Reads an entry from the archive as a UTF-8 encoded string.
	/// </summary>
	/// <param name="entryName">The name of the entry to read.</param>
	/// <param name="ignoreCase">If true, the search for the entry is case-insensitive.</param>
	/// <returns>The content of the file as a string, or an empty string if not found.</returns>
	public string TryReadTextFile(string entryName, bool ignoreCase = true)
	{
		int num = Archive.FindEntry(entryName, ignoreCase);
		if (num == -1)
		{
			return string.Empty;
		}
		ZipEntry zipEntry = Archive[num];
		if (zipEntry == null || !zipEntry.IsFile)
		{
			return string.Empty;
		}
		using Stream stream = Archive.GetInputStream(zipEntry);
		using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
		return streamReader.ReadToEnd();
	}

	/// <summary>
	/// Reads an entry from the archive as a byte array.
	/// </summary>
	/// <param name="entryName">The name of the entry to read.</param>
	/// <param name="ignoreCase">If true, the search for the entry is case-insensitive.</param>
	/// <returns>The content of the file as a byte array, or <c>null</c> if not found.</returns>
	public byte[]? TryReadBinaryFile(string entryName, bool ignoreCase = true)
	{
		int num = Archive.FindEntry(entryName, ignoreCase);
		if (num == -1)
		{
			return null;
		}
		ZipEntry zipEntry = Archive[num];
		if (zipEntry == null || !zipEntry.IsFile)
		{
			return null;
		}
		using Stream stream = Archive.GetInputStream(zipEntry);
		using MemoryStream memoryStream = new MemoryStream();
		stream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	/// <summary>
	/// Writes string content to a new or existing entry in the archive.
	/// </summary>
	/// <param name="entryName">The name of the entry to write to.</param>
	/// <param name="contents">The string content to write.</param>
	/// <param name="ignoreCase">If true, checks for existing entries in a case-insensitive manner.</param>
	/// <param name="overwrite">If true, any existing entry with the same name will be replaced.</param>
	/// <returns><c>true</c> if the write operation was successful; otherwise, <c>false</c>.</returns>
	public bool TryWriteTextFile(string entryName, string contents, bool ignoreCase = true, bool overwrite = true)
	{
		if (Archive == null)
		{
			LogHelper.Error($"Write (text) [{entryName}, {contents.Length} characters] - Failed due to Archive being null.", "TryWriteTextFile", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			return false;
		}
		if (!Archive.IsUpdating)
		{
			Archive.BeginUpdate();
		}
		int num = Archive.FindEntry(entryName, ignoreCase);
		if (num != -1 && !overwrite)
		{
			return false;
		}
		if (num != -1)
		{
			Archive.Delete(entryName);
		}
		StreamDataSource dataSource = new StreamDataSource(new MemoryStream(Encoding.UTF8.GetBytes(contents)));
		Archive.Add(dataSource, entryName, CompressionMethod.Deflated);
		return true;
	}

	/// <summary>
	/// Writes byte content to a new or existing entry in the archive.
	/// </summary>
	/// <param name="entryName">The name of the entry to write to.</param>
	/// <param name="bytes">The byte content to write.</param>
	/// <param name="ignoreCase">If true, checks for existing entries in a case-insensitive manner.</param>
	/// <param name="overwrite">If true, any existing entry with the same name will be replaced.</param>
	/// <returns><c>true</c> if the write operation was successful; otherwise, <c>false</c>.</returns>
	public bool TryWriteBinaryFile(string entryName, byte[] bytes, bool ignoreCase = true, bool overwrite = true)
	{
		if (Archive == null)
		{
			LogHelper.Error($"Write (binary) [{entryName}, {bytes.Length} bytes] - Failed due to Archive being null.", "TryWriteBinaryFile", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			return false;
		}
		if (!Archive.IsUpdating)
		{
			Archive.BeginUpdate();
		}
		int num = Archive.FindEntry(entryName, ignoreCase);
		if (num != -1 && !overwrite)
		{
			return false;
		}
		if (num != -1)
		{
			Archive.Delete(entryName);
		}
		StreamDataSource dataSource = new StreamDataSource(new MemoryStream(bytes));
		Archive.Add(dataSource, entryName, CompressionMethod.Deflated);
		return true;
	}

	/// <summary>
	/// Extracts a specific folder (and all its contents) from the archive to a destination on disk.
	/// </summary>
	/// <param name="sourceFolderInZip">The path inside the ZIP to extract (e.g., "Data/Textures"). Pass empty string or "/" to extract the entire archive.</param>
	/// <param name="destinationPath">The physical directory on disk where the files should be placed.</param>
	/// <param name="overwrite">If true, overwrites existing files at the destination.</param>
	/// <returns>True if the operation completed without critical errors; otherwise false.</returns>
	public bool TryExtractFolder(string sourceFolderInZip, string destinationPath, bool overwrite = true)
	{
		if (Archive == null)
		{
			LogHelper.Error("Attempted to extract folder, but Archive is null.", "TryExtractFolder", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			return false;
		}
		try
		{
			string text = sourceFolderInZip.Replace('\\', '/').Trim('/');
			foreach (ZipEntry item in Archive)
			{
				if (item == null)
				{
					continue;
				}
				string text2 = item.Name.Replace('\\', '/');
				if (!string.IsNullOrEmpty(text) && !text2.Equals(text, StringComparison.InvariantCultureIgnoreCase) && !text2.StartsWith(text + "/", StringComparison.InvariantCultureIgnoreCase))
				{
					continue;
				}
				string text3 = (string.IsNullOrEmpty(text) ? text2 : text2.Substring(text.Length).TrimStart('/'));
				if (string.IsNullOrEmpty(text3))
				{
					Directory.CreateDirectory(destinationPath);
					continue;
				}
				string path = Path.Combine(destinationPath, text3);
				string fullPath = Path.GetFullPath(destinationPath);
				string fullPath2 = Path.GetFullPath(path);
				if (!fullPath2.StartsWith(fullPath, StringComparison.InvariantCultureIgnoreCase))
				{
					LogHelper.Warning("Blocked unsafe extraction path (Zip Slip): " + fullPath2, "TryExtractFolder", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
					continue;
				}
				if (item.IsDirectory)
				{
					Directory.CreateDirectory(path);
					continue;
				}
				string directoryName = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				if (File.Exists(path) && !overwrite)
				{
					continue;
				}
				using Stream stream = Archive.GetInputStream(item);
				using FileStream destination = File.Create(path);
				stream.CopyTo(destination);
			}
			return true;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Failed to extract folder [" + sourceFolderInZip + "] to [" + destinationPath + "].", "TryExtractFolder", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapArchive.cs");
			return false;
		}
	}

	/// <summary>
	/// Apply all changes and write a working zip file in-memory.
	/// </summary>
	public void CommitChanges()
	{
		if (Archive != null && Archive.IsUpdating)
		{
			Archive.CommitUpdate();
		}
	}

	/// <summary>
	/// Releases the resources used by the <see cref="T:SHCDESE.API.Components.Archive.MapArchive" />, such as closing the archive and its underlying stream.
	/// </summary>
	public void Dispose()
	{
		Archive?.Close();
		ArchiveStream?.Dispose();
	}
}
