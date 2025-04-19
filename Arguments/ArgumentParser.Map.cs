namespace RizzziGit.Commons.Arguments;

public static partial class ArgumentParser
{
    private abstract record Map { }

    private sealed record TagMap(
        TagMember Member,
       ArgumentToken. BaseTag Token
    ) : Map;

    private sealed record OrdinalMap(OrdinalMember Member, ArgumentToken.Ordinal Token) : Map;

    private sealed record RestMap(RestMember Member,ArgumentToken. Rest Token) : Map;
}
