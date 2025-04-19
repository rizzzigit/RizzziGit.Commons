using System.Runtime.ExceptionServices;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Arguments;

public static partial class ArgumentParser
{
    private sealed record MemberGroup(TagMember[] Tags, OrdinalMember? Ordinal, RestMember? Rest);

    private static IEnumerable<MemberGroup> GetMemberGroups(IEnumerable<IMember> members)
    {
        List<TagMember> tags = [];
        OrdinalMember? ordinal = null;
        RestMember? rest = null;

        IEnumerator<IMember> membersEnumerator = members.GetEnumerator();

        while (membersEnumerator.MoveNext())
        {
            if (rest is not null)
            {
                List<Exception> unexpectedMembers = [];

                foreach (
                    IMember member in membersEnumerator
                        .ToEnumerable()
                        .Prepend(membersEnumerator.Current)
                )
                {
                    unexpectedMembers.Add(
                        ExceptionDispatchInfo.SetCurrentStackTrace(
                            new UnexpectedMemberException(
                                member,
                                "Unexpected member after rest member."
                            )
                        )
                    );
                }

                throw new AggregateException(unexpectedMembers);
            }

            if (membersEnumerator.Current is TagMember tag)
            {
                if (ordinal is not null)
                {
                    yield return new MemberGroup([.. tags], ordinal, rest);

                    tags.Clear();
                    ordinal = null;
                }

                tags.Add(tag);
            }
            else if (membersEnumerator.Current is OrdinalMember ordinalMember)
            {
                if (ordinal is not null)
                {
                    yield return new MemberGroup([.. tags], ordinal, rest);

                    tags.Clear();
                }

                ordinal = ordinalMember;
            }
            else if (membersEnumerator.Current is RestMember restMember)
            {
                rest = restMember;
            }
        }

        yield return new MemberGroup([.. tags], ordinal, rest);
    }
}
