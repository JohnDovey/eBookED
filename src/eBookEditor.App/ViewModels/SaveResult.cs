namespace eBookEditor.App.ViewModels;

/// <summary>Outcome of a SaveProject call, shown to the user in a MessageWindow.</summary>
public record SaveResult(bool Success, string Message);
