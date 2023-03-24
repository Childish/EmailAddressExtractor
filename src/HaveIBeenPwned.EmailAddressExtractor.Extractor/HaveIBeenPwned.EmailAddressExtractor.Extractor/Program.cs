using HaveIBeenPwned.EmailAddressExtractor.Extractor;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

var app = new CommandApp<Extractor>();

app.Configure(config => config.PropagateExceptions());

try
{
    return app.Run(args);
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return -99;
}

internal sealed class Statistics
{
    public long TotalBytes { get; set; }
    public int TotalLines { get; set; }
    public int ExtractedEmails { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public double ExtractionsPerSecond => ExtractedEmails / (ElapsedMilliseconds / 1000.0);
}

/// <summary>
/// With parallelism set to 8 & the databases located on an SSD, it takes approximately 1 minute per gigabyte.
/// 20.96 GB in 20.24 minutes (565,736.96 extractions per second)
/// </summary>
internal sealed partial class Extractor : AsyncCommand<Extractor.Settings>
{
    private readonly Statistics _statistics = new();

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var sw = Stopwatch.StartNew();

        var table = new Table().Centered();

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn(), new RemainingTimeColumn(), new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                if (File.Exists(settings.Path))
                {
                    var fileName = Path.GetFileName(settings.Path);
                    await ExtractFile(settings.Path, settings.OutputFolder, ctx.AddTask(fileName));
                }
                else if (Directory.Exists(settings.Path))
                {
                    var files = Directory.GetFiles(settings.Path);

                    var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = settings.Parallelism };

                    await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
                    {
                        var fileName = Path.GetFileName(file);

                        //This actually errors if the file name has a + character (and maybe other special characters)
                        //TODO: Filter file
                        var task = ctx.AddTask(fileName);

                        var outputPath = Path.Combine(settings.OutputFolder, fileName);

                        await ExtractFile(file, outputPath, task);

                        task.StopTask();
                    });
                }
            });

        _statistics.ElapsedMilliseconds = sw.ElapsedMilliseconds;

        AnsiConsole.MarkupLine($"Finished extracting {_statistics.ExtractedEmails} emails from {_statistics.TotalLines}.");
        AnsiConsole.MarkupLine($"We checked {Helpers.FormatBytes(_statistics.TotalBytes)} in {Helpers.FormatMilliseconds(_statistics.ElapsedMilliseconds)} ({_statistics.ExtractionsPerSecond:N2} extractions per second).");

        return 0;
    }

    private async Task ExtractFile(string filePath, string outputPath, ProgressTask task)
    {
        //Set the task max value for the progress bar to the size of the file
        var fileInfo = new FileInfo(filePath);
        task.MaxValue = fileInfo.Length;

        int extractedEmails = 0;
        int totalLines = 0;
        using (var inputFileStream = new StreamReader(filePath))
        {
            string? line;

            using var outputFileStream = new StreamWriter(outputPath);
            while ((line = await inputFileStream.ReadLineAsync()) != null)
            {
                Match match = EmailRegex().Match(line);

                if (match.Success)
                {
                    await outputFileStream.WriteLineAsync(match.Value.ToLower());

                    task.Value = inputFileStream.BaseStream.Position;
                    extractedEmails++;
                }

                totalLines++;
            }
        }

        _statistics.TotalBytes += fileInfo.Length;
        _statistics.TotalLines += totalLines;
        _statistics.ExtractedEmails += extractedEmails;
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Path to the file/folder to extract emails from. Defaults to the generated folder \"Input\" in the running directory if not set.")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; set; }

        [Description("Name of the output folder. This defaults to the generated folder \"Output\" in the running directory if not set.")]
        [CommandArgument(1, "[outputFolder]")]
        public string OutputFolder { get; init; } = "Output";

        [Description("The number of files to open and extract emails from in parallel. Defaults to the number of processors on the machine.")]
        [CommandOption("-p|--parallelism")]
        public int Parallelism { get; set; } = Environment.ProcessorCount;

        public override ValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Path))
                return ValidationResult.Error("Path is null or empty.");

            //TODO: Check if the path exists as a folder or file.

            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            return ValidationResult.Success();
        }
    }

    [GeneratedRegex("\\b[a-zA-Z0-9\\.\\-_\\+]+@[a-zA-Z0-9\\.\\-_]+\\.[a-zA-Z]+\\b")]
    private partial Regex EmailRegex();
}