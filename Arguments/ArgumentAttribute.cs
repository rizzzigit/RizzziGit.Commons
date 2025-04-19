namespace RizzziGit.Commons.Arguments;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public abstract class ArgumentAttribute : Attribute
{
    public required string Title;
    public required string Description;

    public bool RequiresValue = false;
    public string? Parser = null;

    public string Hint = "value";

    public abstract override string ToString();
}

public class TagArgumentAttribute : ArgumentAttribute
{
    public required string Key;
    public required char ShortKey;

    public override string ToString() =>
        $"{(RequiresValue ? $"<-{ShortKey}|--{Key} {Hint}>" : $"[-{ShortKey}|--{Key} {Hint}]")}";
}

public sealed class OrdinalArgumentAttribute : ArgumentAttribute
{
    public override string ToString() => $"{(RequiresValue ? $"<{Hint}>" : $"[{Hint}]")}";
}

public sealed class RestArgumentAttribute : ArgumentAttribute
{
    public override string ToString() => $"{(RequiresValue ? "<...>" : "[...]")}";
}
