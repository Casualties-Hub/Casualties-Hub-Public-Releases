using System.Diagnostics;
using System.Text.Json;

namespace Casualties_Hub_Developer_Console;

internal static class Program
{
    private static readonly string HubDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CasualtiesHub");
    private static readonly string LogsPath = Path.Combine(HubDataPath, "Logs");
    private static readonly string CommandPath = Path.Combine(HubDataPath, "DeveloperCommand.json");
    private static readonly string ResponsePath = Path.Combine(HubDataPath, "DeveloperCommandResponse.json");

    private static void Main()
    {
        Console.Title = "Casualties Hub Developer Console";
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        PrintHeader();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1  Test: Cannot find Casualties Unknown");
            Console.WriteLine("2  Test: Missing BepInEx Plugins folder");
            Console.WriteLine("3  Test: Metadata request failure");
            Console.WriteLine("4  Test: Mod import failure");
            Console.WriteLine("5  Create a test crash report (does not crash Hub)");
            Console.WriteLine("6  Show latest Hub log");
            Console.WriteLine("7  Open Hub log folder");
            Console.WriteLine("R  Refresh Hub connection status");
            Console.WriteLine("Q  Quit");
            Console.Write("Select: ");

            var selection = Console.ReadLine()?.Trim().ToUpperInvariant();
            Console.WriteLine();
            switch (selection)
            {
                case "1": SendCommand("MissingGameLocation"); break;
                case "2": SendCommand("MissingPluginsFolder"); break;
                case "3": SendCommand("MetadataRequestFailed"); break;
                case "4": SendCommand("ImportFailed"); break;
                case "5": SendCommand("CreateCrashReport"); break;
                case "6": ShowLatestLog(); break;
                case "7": OpenLogFolder(); break;
                case "R": PrintConnectionStatus(); break;
                case "Q": return;
                default: Console.WriteLine("Unknown option. Use 1-7, R, or Q."); break;
            }
        }
    }

    private static void PrintHeader()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== Casualties Hub Developer Console ===");
        Console.ResetColor();
        Console.WriteLine("This console sends safe one-time test commands to the running Casualties Hub.");
        Console.WriteLine("It never deletes or changes game/mod files.");
        PrintConnectionStatus();
    }

    private static void PrintConnectionStatus()
    {
        var running = Process.GetProcessesByName("Casualties Hub").Length > 0;
        Console.ForegroundColor = running ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine(running
            ? "Hub process detected. Commands should be acknowledged in under 3 seconds."
            : "Hub process not detected. Start Casualties Hub.exe (v0.0.2.5 or newer) before testing.");
        Console.ResetColor();
    }

    private static void SendCommand(string command)
    {
        if (Process.GetProcessesByName("Casualties Hub").Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No running Casualties Hub process was found. Command was not sent.");
            Console.ResetColor();
            return;
        }

        try
        {
            Directory.CreateDirectory(HubDataPath);
            var requestId = Guid.NewGuid().ToString("N");
            var request = new DeveloperCommandRequest { RequestId = requestId, Command = command, RequestedUtc = DateTime.UtcNow };
            if (File.Exists(ResponsePath)) File.Delete(ResponsePath);
            var temporaryPath = CommandPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(request));
            File.Move(temporaryPath, CommandPath, true);
            Console.WriteLine($"Sent {command}. Waiting for Hub acknowledgement...");

            var deadline = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(150);
                if (!File.Exists(ResponsePath)) continue;
                var response = JsonSerializer.Deserialize<DeveloperCommandResponse>(File.ReadAllText(ResponsePath));
                if (response?.RequestId != requestId) continue;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Hub confirmed: " + response.Message);
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Hub did not acknowledge the command. Make sure the running Hub is v0.0.2.5 or newer, then try again.");
            Console.ResetColor();
        }
        catch (Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Could not send command: " + exception.Message);
            Console.ResetColor();
        }
    }

    private static void ShowLatestLog()
    {
        try
        {
            var latestLog = Directory.Exists(LogsPath)
                ? Directory.EnumerateFiles(LogsPath, "Log *.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;
            if (latestLog is null)
            {
                Console.WriteLine("No Hub log exists yet. Start the Hub first.");
                return;
            }
            Console.WriteLine("--- " + latestLog + " ---");
            using var stream = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            Console.WriteLine(reader.ReadToEnd());
            Console.WriteLine("--- end log ---");
        }
        catch (Exception exception)
        {
            Console.WriteLine("Could not read Hub log: " + exception.Message);
        }
    }

    private static void OpenLogFolder()
    {
        Directory.CreateDirectory(LogsPath);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{LogsPath}\"") { UseShellExecute = true });
    }

    private sealed class DeveloperCommandRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public DateTime RequestedUtc { get; set; }
    }

    private sealed class DeveloperCommandResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime RespondedUtc { get; set; }
    }
}
