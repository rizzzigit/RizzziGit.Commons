namespace RizzziGit.Commons.Arguments;

[AttributeUsage(AttributeTargets.Class)]
public class ArgumentObjectAttribute : Attribute
{
    public required ParseMethod Method;
}

public enum ParseMethod : byte
{
    Ordinal,
}
