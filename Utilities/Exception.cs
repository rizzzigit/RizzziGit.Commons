namespace RizzziGit.Commons.Utilities;

public static class ExceptionExtensions
{
    public static string ToPrintable(this Exception exception, string? customMessage = null) =>
        $"{customMessage ?? exception.Message}{(exception.StackTrace != null ? $"\n{exception.StackTrace}" : "")}";
}
