using System.Runtime.ExceptionServices;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Arguments;

public static partial class ArgumentParser
{
    private sealed record TokenGroup(
        ArgumentToken.BaseTag[] Tags,
        ArgumentToken.Ordinal? Ordinal,
        ArgumentToken.Rest? Rest
    );

    private static IEnumerable<TokenGroup> GetTokenGroups(IEnumerable<ArgumentToken> tokens)
    {
        List<ArgumentToken.BaseTag> tokenTags = [];
        ArgumentToken.Ordinal? tokenOrdinal = null;
        ArgumentToken.Rest? tokenRest = null;

        IEnumerator<ArgumentToken> tokensEnumerator = tokens.GetEnumerator();

        while (tokensEnumerator.MoveNext())
        {
            if (tokenRest is not null)
            {
                List<Exception> unexpectedTokens = [];

                foreach (
                    ArgumentToken token in tokensEnumerator
                        .ToEnumerable()
                        .Prepend(tokensEnumerator.Current)
                )
                {
                    unexpectedTokens.Add(
                        ExceptionDispatchInfo.SetCurrentStackTrace(
                            new UnknownArgumentException(
                                token,
                                "Unexpected trailing argument after rest."
                            )
                        )
                    );
                }

                throw new AggregateException(unexpectedTokens);
            }

            if (tokensEnumerator.Current is ArgumentToken.BaseTag tag)
            {
                if (tokenOrdinal is not null)
                {
                    yield return new TokenGroup([.. tokenTags], tokenOrdinal, tokenRest);

                    tokenTags.Clear();
                    tokenOrdinal = null;
                }

                tokenTags.Add(tag);
            }
            else if (tokensEnumerator.Current is ArgumentToken.Ordinal ordinalToken)
            {
                if (tokenOrdinal is not null)
                {
                    yield return new TokenGroup([.. tokenTags], tokenOrdinal, tokenRest);

                    tokenTags.Clear();
                }

                tokenOrdinal = ordinalToken;
            }
            else if (tokensEnumerator.Current is ArgumentToken.Rest restToken)
            {
                tokenRest = restToken;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown type: {tokensEnumerator.Current?.GetType().Name}"
                );
            }
        }

        yield return new TokenGroup([.. tokenTags], tokenOrdinal, tokenRest);
    }
}
