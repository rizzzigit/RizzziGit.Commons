namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    private sealed record MemberGroup(PairMember[] Pairs, OrdinalMember? Ordinal, RestMember? Rest);

    private static IEnumerable<MemberGroup> GetMemberGroups(IEnumerable<IMember> members)
    {
        List<PairMember> pairs = [];
        OrdinalMember? ordinal = null;
        RestMember? rest = null;

        IEnumerator<IMember> membersEnumerator = members.GetEnumerator();

        while (membersEnumerator.MoveNext())
        {
            if (rest is not null)
            {
                List<IMember> unexpectedMembers = [];

                while (membersEnumerator.MoveNext())
                {
                    unexpectedMembers.Add(membersEnumerator.Current);
                }

                throw new ArgumentException(
                    $"Unexpected members after rest member: {string.Join(", ", unexpectedMembers)}",
                    nameof(members)
                );
            }

            if (membersEnumerator.Current is PairMember pair)
            {
                if (ordinal is not null)
                {
                    yield return new MemberGroup([.. pairs], ordinal, rest);

                    pairs.Clear();
                    ordinal = null;
                }

                pairs.Add(pair);
            }
            else if (membersEnumerator.Current is OrdinalMember ordinalMember)
            {
                if (ordinal is not null)
                {
                    yield return new MemberGroup([.. pairs], ordinal, rest);

                    pairs.Clear();
                }

                ordinal = ordinalMember;
            }
            else if (membersEnumerator.Current is RestMember restMember)
            {
                rest = restMember;
            }
        }

        yield return new MemberGroup([.. pairs], ordinal, rest);
    }
}
