namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    public abstract record BaseTag(string? Value) : ArgumentToken
    {
        public override abstract string ToString();
    }

    public sealed record ShortTag(char Key, string? Value) : BaseTag(Value)
    {
        public override string ToString() =>
            $"-{Key} {Value?.Replace(" ", "\\ ")}";
    }

    public sealed record Tag(string Key, string? Value) : BaseTag(Value)
    {
        public override string ToString() => $"--{Key} {(Value != null ? Value.Replace(" ", "\\ ") : "")}";
    }

    public sealed record Ordinal(string Value) : ArgumentToken
    {
        public override string ToString() => Value.Replace(" ", "\\ ");
    }

    public sealed record Rest(string[] Values) : ArgumentToken
    {
        public override string ToString() =>
            string.Join(' ', Values.Select((value) => value.Replace(" ", "\\ ")));
    }

    private ArgumentToken() { }
}
