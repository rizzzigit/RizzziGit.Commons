using System.Runtime.ExceptionServices;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    private sealed record MapGroup(PairMap[] Pairs, OrdinalMap? OrdinalMap, RestMap? RestMap);

    private static IEnumerable<MapGroup> GetMapGroups(
        IEnumerable<IMember> members,
        IEnumerable<ArgumentToken> tokens,
        ArgumentTokenOptions options
    )
    {
        IEnumerator<MemberGroup> memberGroups = GetMemberGroups(members).GetEnumerator();
        IEnumerator<TokenGroup> tokenGroups = GetTokenGroups(tokens).GetEnumerator();

        List<ArgumentToken> unknownTokens = [];
        List<Exception> requiredMembers = [];

        while (true)
        {
            if (!memberGroups.MoveNext())
            {
                while (tokenGroups.MoveNext())
                {
                    unknownTokens.AddRange(tokenGroups.Current.Pairs);

                    if (tokenGroups.Current.Ordinal is not null)
                    {
                        unknownTokens.Add(tokenGroups.Current.Ordinal);
                    }

                    if (tokenGroups.Current.Rest is not null)
                    {
                        unknownTokens.Add(tokenGroups.Current.Rest);
                    }
                }

                break;
            }
            else if (!tokenGroups.MoveNext())
            {
                foreach (
                    MemberGroup memberGroup in memberGroups
                        .ToEnumerable()
                        .Prepend(memberGroups.Current)
                )
                {
                    foreach (PairMember pair in memberGroup.Pairs)
                    {
                        if (
                            !ValidateNullability(
                                pair.RequiresValue,
                                pair.Type,
                                pair.IsNullable,
                                pair.HasDefaultValue,
                                pair.Attribute,
                                out Exception? exception
                            )
                        )
                        {
                            requiredMembers.Add(exception);
                        }
                    }
                    {
                        if (
                            memberGroup.Ordinal is not null
                            && !ValidateNullability(
                                memberGroup.Ordinal.RequiresValue,
                                memberGroup.Ordinal.Type,
                                memberGroup.Ordinal.IsNullable,
                                memberGroup.Ordinal.HasDefaultValue,
                                memberGroup.Ordinal.Attribute,
                                out Exception? exception
                            )
                        )
                        {
                            requiredMembers.Add(exception);
                        }
                    }
                    {
                        if (
                            memberGroup.Rest is not null
                            && !ValidateNullability(
                                memberGroup.Rest.RequiresValue,
                                memberGroup.Rest.Type,
                                memberGroup.Rest.IsNullable,
                                memberGroup.Rest.HasDefaultValue,
                                memberGroup.Rest.Attribute,
                                out Exception? exception
                            )
                        )
                        {
                            requiredMembers.Add(exception);
                        }
                    }
                }
            }
            else
            {
                MemberGroup memberGroup = memberGroups.Current;
                TokenGroup tokenGroup = tokenGroups.Current;

                List<PairMap> pairs = [];
                OrdinalMap? ordinal = null;
                RestMap? rest = null;

                List<BasePair> tokenPairs = [.. tokenGroup.Pairs];
                foreach (PairMember pairMember in memberGroup.Pairs)
                {
                    BasePair? pair = tokenPairs.FirstOrDefault(
                        (basePair) =>
                            (
                                basePair is Pair pair
                                && pair.Key.Equals(
                                    pairMember.Attribute.Key,
                                    StringComparison.CurrentCultureIgnoreCase
                                )
                            )
                            || (
                                basePair is ShortPair shortPair
                                && char.ToUpperInvariant(shortPair.Key)
                                    .Equals(char.ToUpperInvariant(pairMember.Attribute.ShortKey))
                            )
                    );

                    if (pair is null)
                    {
                        if (
                            !ValidateNullability(
                                pairMember.RequiresValue,
                                pairMember.Type,
                                pairMember.IsNullable,
                                pairMember.HasDefaultValue,
                                pairMember.Attribute,
                                out Exception? exception
                            )
                        )
                        {
                            requiredMembers.Add(exception);
                        }

                        continue;
                    }

                    pairs.Add(new(pairMember, pair));
                    tokenPairs.Remove(pair);
                }

                foreach (BasePair tokenPair in tokenPairs)
                {
                    unknownTokens.Add(tokenPair);
                }

                if (tokenGroup.Ordinal is null)
                {
                    if (
                        memberGroup.Ordinal is not null
                        && !ValidateNullability(
                            memberGroup.Ordinal.RequiresValue,
                            memberGroup.Ordinal.Type,
                            memberGroup.Ordinal.IsNullable,
                            memberGroup.Ordinal.HasDefaultValue,
                            memberGroup.Ordinal.Attribute,
                            out Exception? exception
                        )
                    )
                    {
                        requiredMembers.Add(exception);
                    }
                }
                else if (memberGroup.Ordinal is not null)
                {
                    ordinal = new(memberGroup.Ordinal, tokenGroup.Ordinal);
                }
                else
                {
                    unknownTokens.Add(tokenGroup.Ordinal);
                }

                if (tokenGroup.Rest is null)
                {
                    if (
                        memberGroup.Rest is not null
                        && !ValidateNullability(
                            memberGroup.Rest.RequiresValue,
                            memberGroup.Rest.Type,
                            memberGroup.Rest.IsNullable,
                            memberGroup.Rest.HasDefaultValue,
                            memberGroup.Rest.Attribute,
                            out Exception? exception
                        )
                    )
                    {
                        requiredMembers.Add(exception);
                    }
                }
                else if (memberGroup.Rest is not null)
                {
                    rest = new(memberGroup.Rest, tokenGroup.Rest);
                }
                else
                {
                    unknownTokens.Add(tokenGroup.Rest);
                }

                yield return new MapGroup([.. pairs], ordinal, rest);
            }
        }

        {
            List<Exception> exceptions = [];

            {
                if (unknownTokens.Count > 0 && !options.IgnoreUnknownTokens)
                {
                    exceptions.AddRange(
                        unknownTokens.Select(
                            (token) =>
                                ExceptionDispatchInfo.SetCurrentStackTrace(
                                    new ArgumentException($"Unknown token: {token}", nameof(tokens))
                                )
                        )
                    );
                }

                if (requiredMembers.Count > 0)
                {
                    exceptions.AddRange(requiredMembers);
                }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        yield break;
    }
}
