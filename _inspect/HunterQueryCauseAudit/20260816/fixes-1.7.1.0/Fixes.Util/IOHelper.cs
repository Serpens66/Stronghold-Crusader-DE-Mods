using System.IO;
using System.Reflection;

namespace Fixes.Util;

internal static class IOHelper
{
	private static string modDirectory = string.Empty;

	private static string modDataDirectory = string.Empty;

	public static string ModDirectory
	{
		get
		{
			if (modDirectory == string.Empty)
			{
				modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			}
			return modDirectory;
		}
	}

	public static string ModDataDirectory
	{
		get
		{
			if (modDataDirectory == string.Empty)
			{
				modDataDirectory = Path.Combine(ModDirectory, "data");
			}
			return modDataDirectory;
		}
	}
}
