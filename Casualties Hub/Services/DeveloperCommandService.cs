using System.IO;
using System.Text.Json;

namespace Casualties_Hub.Services;

/// <summary>
/// Receives safe test commands from the separate Developer Console. Commands live only
/// in the current Windows user's AppData and are deleted after the Hub handles them.
/// </summary>
public sealed class DeveloperCommandService
{
    private static readonly string CommandPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CasualtiesHub", "DeveloperCommand.json");

    private static readonly string ResponsePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CasualtiesHub", "DeveloperCommandResponse.json");

    public bool TryTake(out DeveloperCommandRequest request)
    {
        request = new DeveloperCommandRequest();
        try
        {
            if (!File.Exists(CommandPath)) return false;
            var json = File.ReadAllText(CommandPath);
            request = JsonSerializer.Deserialize<DeveloperCommandRequest>(json) ?? new DeveloperCommandRequest();
            File.Delete(CommandPath);
            if (string.IsNullOrWhiteSpace(request?.Command)) return false;
            return true;
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not read Developer Console command", exception);
            return false;
        }
    }

    public void Acknowledge(DeveloperCommandRequest request, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResponsePath)!);
            var response = new DeveloperCommandResponse
            {
                RequestId = request.RequestId,
                Command = request.Command,
                Message = message,
                RespondedUtc = DateTime.UtcNow
            };
            var temporaryPath = ResponsePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(response));
            File.Move(temporaryPath, ResponsePath, true);
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not acknowledge Developer Console command", exception);
        }
    }
}

public sealed class DeveloperCommandRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public DateTime RequestedUtc { get; set; }
}

public sealed class DeveloperCommandResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime RespondedUtc { get; set; }
}
