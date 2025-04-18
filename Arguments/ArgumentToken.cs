namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    private static string PrintKeyValuePair(string name, string? value) =>
        $"--{name}{(value != null ? $" {value}" : "")}";

    private static string PrintShorthandKeyValuePair(char name, string? value) =>
        $"-{name} {value}";

    private static string PrintOrdinal(string value) => value;

    private static string PrintRest(params string[] values) => string.Join(' ', values);

    public abstract record BasePair(string? Value) : ArgumentToken();

    public sealed record ShortPair(char Key, string? Value) : BasePair(Value)
    {
        public override string ToString() =>
            PrintShorthandKeyValuePair(Key, Value?.Replace(" ", "\\ "));
    }

    public sealed record Pair(string Key, string? Value) : BasePair(Value)
    {
        public override string ToString() => PrintKeyValuePair(Key, Value?.Replace(" ", "\\ "));
    }

    public sealed record Ordinal(string Value) : ArgumentToken()
    {
        public override string ToString() => PrintOrdinal(Value.Replace(" ", "\\ "));
    }

    public sealed record Rest(string[] Values) : ArgumentToken()
    {
        public override string ToString() =>
            PrintRest([.. Values.Select((value) => value.Replace(" ", "\\ "))]);
    }

    private ArgumentToken() { }
}
