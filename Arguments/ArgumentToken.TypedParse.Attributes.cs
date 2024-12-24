namespace RizzziGit.Commons.Arguments;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ArgumentObjectAttribute() : Attribute
{
    public required ArgumentObjectMode Mode;
}

public abstract class BaseArgumentAttribute : Attribute
{
    public required string Title;
    public required string[] Description;
    public required string Hint;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ArgumentAttribute() : BaseArgumentAttribute
{
    public bool IsRequired = false;
    public required string Key;
    public char ShorthandKey;

    public override string ToString() =>
        $"<--{Key} | -{ShorthandKey}> {(IsRequired ? $"<{Hint}>" : $"[{Hint}]")}";
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class OrdinalArgumentAttribute() : BaseArgumentAttribute;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class RestArgumentAttribute() : BaseArgumentAttribute;
