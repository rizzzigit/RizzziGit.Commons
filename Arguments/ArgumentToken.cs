namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    public sealed record KeyValuePair(string Key, string? Value) : ArgumentToken()
    {
        public override string ToString() =>
            $"--{Key}{(Value != null ? $" {Value.Replace(" ", "\\ ")}" : "")}";
    }

    public sealed record ShorthandKeyValuePair(char Key, string? Value) : ArgumentToken()
    {
        public override string ToString() =>
            $"-{Key}{(Value != null ? $" {Value.Replace(" ", "\\ ")}" : "")}";
    }

    public sealed record OrdinalValue(string Value) : ArgumentToken()
    {
        public override string ToString() => Value.Replace(" ", "\\ ");
    }

    public sealed record RestArgument(string[] Values) : ArgumentToken()
    {
        public override string ToString() =>
            string.Join(' ', [.. Values.Select((value) => value.Replace(" ", "\\ "))]);
    }

    private ArgumentToken() { }
}
