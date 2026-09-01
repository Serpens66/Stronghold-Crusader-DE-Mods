using CommandLine;
using SHCDESE.Dat2XAML;

public class Options
{
    [Value(0, Required = true, HelpText = "Input .dat file path")]
    public string InputFile { get; set; } = string.Empty;

    [Value(1, Required = true, HelpText = "Output directory")]
    public string OutputDirectory { get; set; } = string.Empty;
}

public class Program
{
    /// <summary>
    /// Usage:
    ///   SCHDESE.Dat2XAML inputFile.dat outputDirectory
    ///   SCHDESE.Dat2XAML --help
    /// </summary>
    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                ConvertFileToXAML(options.InputFile, options.OutputDirectory);
            })
            .WithNotParsed(errors =>
            {

            });
    }

    private static bool IsValid(string inputFilePath)
    {
        return File.ReadAllText(inputFilePath)
            .Contains("xaml", StringComparison.InvariantCultureIgnoreCase);
    }

    private static void ConvertFileToXAML(string inputFilePath, string outputDirectory)
    {
        if (!File.Exists(inputFilePath))
        {
            Console.WriteLine($"Input file does not exist: {inputFilePath}");
            return;
        }

        if (!IsValid(inputFilePath))
        {
            Console.WriteLine($"Input file does not contain XAML: {inputFilePath}");
            return;
        }

        try
        {
            NoesisDatFile noesisFile = new NoesisDatFile(inputFilePath);

            Console.WriteLine($"Class Name: {noesisFile.ClassName}");
            Console.WriteLine($"Path:       {noesisFile.Path}");
            Console.WriteLine("--- Content Start ---");
            Console.WriteLine(noesisFile.Content.Substring(0, Math.Min(100, noesisFile.Content.Length)));
            Console.WriteLine("--- Content End ---");

            string xamlFileName = Path.GetFileNameWithoutExtension(inputFilePath);
            string xamlDirectoryPath = Path.Combine(
                outputDirectory,
                Path.GetDirectoryName(noesisFile.Path) ?? string.Empty
            );

            Directory.CreateDirectory(xamlDirectoryPath);

            File.WriteAllText(
                Path.Combine(xamlDirectoryPath, xamlFileName + ".xaml"),
                noesisFile.Content
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing file: {ex}");
        }
    }
}
