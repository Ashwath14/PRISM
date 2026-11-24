namespace PRISM.Core.Models;

public class EnvironmentApplicationResult
{
    public bool IsSuccess { get; set; }
    public bool IsPartial { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;

    public bool DisplaySucceeded { get; set; }
    public bool AudioSucceeded { get; set; }

    public string? DisplayMessage { get; set; }
    public string? AudioMessage { get; set; }
    public string FeedbackMessage { get; set; } = string.Empty;

    public static EnvironmentApplicationResult Success(string name, string message) => new()
    {
        IsSuccess = true,
        IsPartial = false,
        EnvironmentName = name,
        DisplaySucceeded = true,
        AudioSucceeded = true,
        FeedbackMessage = message
    };

    public static EnvironmentApplicationResult Partial(string name, bool dispOk, bool audOk, string? dispErr, string? audErr) => new()
    {
        IsSuccess = dispOk || audOk,
        IsPartial = true,
        EnvironmentName = name,
        DisplaySucceeded = dispOk,
        AudioSucceeded = audOk,
        DisplayMessage = dispErr,
        AudioMessage = audErr,
        FeedbackMessage = $"Partial Environment: {(dispOk ? "Display updated" : "Display failed")}, {(audOk ? "Audio updated" : "Audio unavailable")}"
    };

    public static EnvironmentApplicationResult Failure(string name, string message) => new()
    {
        IsSuccess = false,
        IsPartial = false,
        EnvironmentName = name,
        DisplaySucceeded = false,
        AudioSucceeded = false,
        FeedbackMessage = message
    };
}
