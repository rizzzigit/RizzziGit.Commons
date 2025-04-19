using System.Runtime.ExceptionServices;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Arguments;

public static partial class ArgumentParser
{
    private sealed record MapGroup(TagMap[] Tags, OrdinalMap? OrdinalMap, RestMap? RestMap);

    private static IEnumerable<MapGroup> GetMapGroups(
        IEnumerable<IMember> members,
        IEnumerable<ArgumentToken> tokens,
        ArgumentParserOptions options
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
                    unknownTokens.AddRange(tokenGroups.Current.Tags);

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
                    foreach (TagMember tag in memberGroup.Tags)
                    {
                        if (
                            !ValidateNullability(
                                tag.RequiresValue,
                                tag.Type,
                                tag.IsNullable,
                                tag.HasDefaultValue,
                                tag.Attribute,
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

                List<TagMap> tags = [];
                OrdinalMap? ordinal = null;
                RestMap? rest = null;

                List<ArgumentToken.BaseTag> tokenTags = [.. tokenGroup.Tags];
                foreach (TagMember tagMember in memberGroup.Tags)
                {
                    ArgumentToken.BaseTag? tag = tokenTags.FirstOrDefault(
                        (baseTag) =>
                            (
                                baseTag is ArgumentToken.Tag tag
                                && tag.Key.Equals(
                                    tagMember.Attribute.Key,
                                    StringComparison.CurrentCultureIgnoreCase
                                )
                            )
                            || (
                                baseTag is ArgumentToken.ShortTag shortTag
                                && char.ToUpperInvariant(shortTag.Key)
                                    .Equals(char.ToUpperInvariant(tagMember.Attribute.ShortKey))
                            )
                    );

                    if (tag is null)
                    {
                        if (
                            !ValidateNullability(
                                tagMember.RequiresValue,
                                tagMember.Type,
                                tagMember.IsNullable,
                                tagMember.HasDefaultValue,
                                tagMember.Attribute,
                                out Exception? exception
                            )
                        )
                        {
                            requiredMembers.Add(exception);
                        }

                        continue;
                    }

                    tags.Add(new(tagMember, tag));
                    tokenTags.Remove(tag);
                }

                foreach (ArgumentToken.BaseTag tokenTag in tokenTags)
                {
                    unknownTokens.Add(tokenTag);
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

                yield return new MapGroup([.. tags], ordinal, rest);
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
                                    new UnknownArgumentException(token, "Unknown argument")
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
