namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    private sealed record TokenGroup(BasePair[] Pairs, Ordinal? Ordinal, Rest? Rest);

    private static IEnumerable<TokenGroup> GetTokenGroups(IEnumerable<ArgumentToken> tokens)
    {
        List<BasePair> tokenPairs = [];
        Ordinal? tokenOrdinal = null;
        Rest? tokenRest = null;

        foreach (ArgumentToken token in tokens)
        {
            if (tokenRest is not null)
            {
                throw new ArgumentException(
                    $"Unexpected trailing token after rest: {token}",
                    nameof(tokens)
                );
            }

            if (token is BasePair pair)
            {
                if (tokenOrdinal is not null)
                {
                    yield return new TokenGroup([.. tokenPairs], tokenOrdinal, tokenRest);

                    tokenPairs.Clear();
                    tokenOrdinal = null;
                }

                tokenPairs.Add(pair);
            }
            else if (token is Ordinal ordinalToken)
            {
                if (tokenOrdinal is not null)
                {
                    yield return new TokenGroup([.. tokenPairs], tokenOrdinal, tokenRest);

                    tokenPairs.Clear();
                }

                tokenOrdinal = ordinalToken;
            }
            else if (token is Rest restToken)
            {
                tokenRest = restToken;
            }
            else
            {
                throw new InvalidOperationException($"Unknown type: {token?.GetType().Name}");
            }
        }

        yield return new TokenGroup([.. tokenPairs], tokenOrdinal, tokenRest);
    }
}
