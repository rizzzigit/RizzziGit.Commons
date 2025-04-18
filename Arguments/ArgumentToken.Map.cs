namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    private abstract record Map { }

    private sealed record PairMap(
        PairMember Member,
        BasePair Token
    ) : Map;

    private sealed record OrdinalMap(OrdinalMember Member, Ordinal Token) : Map;

    private sealed record RestMap(RestMember Member, Rest Token) : Map;
}
