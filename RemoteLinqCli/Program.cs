using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Octokit;
using Terminal.Gui;
using Application = Terminal.Gui.Application;
using Label = Terminal.Gui.Label;
using ProductHeaderValue = Octokit.ProductHeaderValue;

internal class Program
{
    private static string _githubPat = "";
    private static string _org = "";
    private static string _repo = "";
    private static List<string> _allScripts = [];

    private static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true);

        var configuration = builder.Build();
        _githubPat = configuration["GitHubSettings:Pat"] ?? string.Empty;
        _org = configuration["GitHubSettings:Org"] ?? string.Empty;
        _repo = configuration["GitHubSettings:Repo"] ?? string.Empty;

        if (args.Length >= 3)
        {
            // Command-line arguments provided: RepoOwner, RepoName, ScriptPath
            var repoOwner = args[0];
            var repoName = args[1];
            var scriptPath = args[2];

            var scriptContent = await DownloadFileContent(repoOwner, repoName, scriptPath);
            if (!string.IsNullOrEmpty(scriptContent))
            {
                var localPath = SaveScriptLocally(repoName, scriptPath, scriptContent);
                RunScriptWithLinqPad(localPath);
            }
            else
            {
                Console.WriteLine("Failed to download script content.");
            }
        }
        else
        {
            // Interactive TUI
            Application.Init();
            var top = Application.Top;

            var leftPane = new Window("GitHub LINQPad Script Runner")
            {
                X = 0,
                Y = 1, // Leave one row for the top level menu
                Width = Dim.Percent(50),
                Height = Dim.Fill()
            };

            var rightPane = new Window("Script Output")
            {
                X = Pos.Right(leftPane),
                Y = 1, // Leave one row for the top level menu
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            top.Add(leftPane, rightPane);

            var repoOwnerLabel = new Label("Repo Owner:")
            {
                X = 3,
                Y = 2
            };
            var repoOwnerText = new TextField(_org)
            {
                X = Pos.Right(repoOwnerLabel) + 1,
                Y = Pos.Top(repoOwnerLabel),
                Width = 40
            };

            var repoNameLabel = new Label("Repo Name:")
            {
                X = 3,
                Y = Pos.Bottom(repoOwnerLabel) + 1
            };
            var repoNameText = new TextField(_repo)
            {
                X = Pos.Right(repoNameLabel) + 1,
                Y = Pos.Top(repoNameLabel),
                Width = 40
            };

            var searchButton = new Button("Search")
            {
                X = 3,
                Y = Pos.Bottom(repoNameLabel) + 2
            };

            var searchTextField = new TextField("")
            {
                X = 3,
                Y = Pos.Bottom(searchButton) + 2,
                Width = Dim.Fill(3)
            };

            var scriptListView = new ListView(new List<string>())
            {
                X = 3,
                Y = Pos.Bottom(searchTextField) + 1,
                Width = Dim.Fill(3),
                Height = Dim.Fill(3)
            };

            var scriptOutputTextView = new TextView
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(1),
                ReadOnly = true
            };

            var statusLabel = new Label("Status: Idle")
            {
                X = 1,
                Y = Pos.Bottom(scriptOutputTextView) + 1,
                Width = Dim.Fill(1)
            };

            rightPane.Add(scriptOutputTextView, statusLabel);

            leftPane.Add(repoOwnerLabel, repoOwnerText, repoNameLabel, repoNameText, searchButton, searchTextField,
                scriptListView);

            searchButton.Clicked += async () =>
            {
                var repoOwner = repoOwnerText.Text.ToString();
                var repoName = repoNameText.Text.ToString();

                _allScripts = await SearchForScripts(repoOwner, repoName);
                await scriptListView.SetSourceAsync(_allScripts);
            };

            searchTextField.TextChanged += args =>
            {
                var searchQuery = searchTextField.Text.ToString();
                var filteredScripts = FuzzySearch(searchQuery);
                scriptListView.SetSource(filteredScripts);
            };

            scriptListView.OpenSelectedItem += async args =>
            {
                var selectedScript = args.Value.ToString();
                var repoOwner = repoOwnerText.Text.ToString();
                var repoName = repoNameText.Text.ToString();

                var scriptContent = await DownloadFileContent(repoOwner, repoName, selectedScript);
                if (!string.IsNullOrEmpty(scriptContent))
                {
                    var localPath = SaveScriptLocally(repoName, selectedScript, scriptContent);
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    var cmd = $"\"{exePath}\" \"{repoOwner}\" \"{repoName}\" \"{selectedScript}\"";

                    var response = MessageBox.Query("Run or Copy",
                        "Would you like to copy the command to clipboard or run the script now?", "Copy", "Run");

                    if (response == 0)
                    {
                        Clipboard.TrySetClipboardData(cmd);
                        MessageBox.Query("Copied", "The command has been copied to the clipboard.", "OK");
                    }
                    else if (response == 1)
                    {
                        scriptOutputTextView.Text = "";
                        statusLabel.Text = "Status: Running";
                        RunScriptWithLinqPad(localPath, scriptOutputTextView, statusLabel);
                    }
                }
                else
                {
                    MessageBox.ErrorQuery("Error", "Failed to download script content.", "OK");
                }
            };

            Application.Run();
        }
    }

    private static async Task<List<string>> SearchForScripts(string repoOwner, string repoName)
    {
        var tokenAuth = new Credentials(_githubPat);
        var client = new GitHubClient(new ProductHeaderValue("RepoSearch"))
        {
            Credentials = tokenAuth // Ensure that tokenAuth is properly initialized
        };

        const string searchString = "LINQPad"; // Adjust this if needed
        var searchRequest = new SearchCodeRequest(searchString)
        {
            In = new[] { CodeInQualifier.Path },
            Repos = [$"{repoOwner}/{repoName}"]
        };

        try
        {
            var searchResults = await client.Search.SearchCode(searchRequest);
            return searchResults.Items.Select(item => item.Path).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Error searching GitHub: {ex.Message}", "OK");
            return [];
        }
    }

    private static List<string> FuzzySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _allScripts;

        var results = FuzzySharp.Process.ExtractSorted(query, _allScripts, s => s, cutoff: 50);
        return results.Select(r => r.Value).ToList();
    }

    private static async Task<string> DownloadFileContent(string organization, string repoName, string filePath)
    {
        var tokenAuth = new Credentials(_githubPat);
        var client = new GitHubClient(new ProductHeaderValue("RepoSearch"))
        {
            Credentials = tokenAuth
        };

        try
        {
            var content = await client.Repository.Content.GetRawContent(organization, repoName, filePath);
            var scriptContent = Encoding.UTF8.GetString(content);
            return scriptContent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading file content: {ex.Message}");
            return string.Empty;
        }
    }

    private static string SaveScriptLocally(string repoName, string filePath, string content)
    {
        var localScriptPath = Path.Combine(Path.GetTempPath(), $"{repoName}_{Path.GetFileName(filePath)}");
        File.WriteAllText(localScriptPath, content);
        return localScriptPath;
    }

    private static void RunScriptWithLinqPad(string scriptPath)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = @"C:\Program Files\LINQPad8\LPRun8.exe",
            Arguments = $"\"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
        process.ErrorDataReceived += (sender, e) => Console.WriteLine($"Error: {e.Data}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();
    }

    private static void RunScriptWithLinqPad(string scriptPath, TextView outputTextView, Label statusLabel)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = @"C:\Program Files\LINQPad8\LPRun8.exe",
            Arguments = $"\"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process();
        process.StartInfo = processInfo;
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) Application.MainLoop.Invoke(() => outputTextView.Text += e.Data + "\n");
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) Application.MainLoop.Invoke(() => outputTextView.Text += "Error: " + e.Data + "\n");
        };
        process.Exited += (sender, e) => Application.MainLoop.Invoke(() => statusLabel.Text = "Status: Idle");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
}
