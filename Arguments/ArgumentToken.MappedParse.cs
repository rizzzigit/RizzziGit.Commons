namespace RizzziGit.Commons.Arguments;

public sealed record ArgumentMap(OrdinalArgumentSet[] OrdinalSets, string[]? Rest) { }

public sealed record OrdinalArgumentSet(OrdinalArgumentTag[] Tags, string? OrdinalValue);

public abstract record OrdinalArgumentTag(string? Value)
{
    public sealed record KeyValuePair(string Key, string? Value) : OrdinalArgumentTag(Value)
    {
        public override string ToString() => $"--{Key} {Value}";
    }

    public sealed record ShorthandKeyValuePair(char Key, string? Value) : OrdinalArgumentTag(Value)
    {
        public override string ToString() => $"-{Key} {Value}";
    }
}

public partial record ArgumentToken
{
    public static ArgumentMap MappedParse(string[] args) => MappedParse(Parse(args));

    private static ArgumentMap MappedParse(IEnumerable<ArgumentToken> tokens)
    {
        List<OrdinalArgumentTag> currentTags = [];
        List<OrdinalArgumentSet> sets = [];

        foreach (ArgumentToken token in tokens)
        {
            switch (token)
            {
                case KeyValuePair keyValuePair:
                {
                    currentTags.Add(
                        new OrdinalArgumentTag.KeyValuePair(keyValuePair.Key, keyValuePair.Value)
                    );

                    break;
                }

                case ShorthandKeyValuePair shorthandKeyValuePair:
                {
                    currentTags.Add(
                        new OrdinalArgumentTag.ShorthandKeyValuePair(
                            shorthandKeyValuePair.Key,
                            shorthandKeyValuePair.Value
                        )
                    );

                    break;
                }

                case OrdinalValue ordinalValue:
                {
                    sets.Add(new([.. currentTags], ordinalValue.Value));
                    currentTags = [];

                    break;
                }

                case RestArgument restArgument:
                {
                    if (currentTags.Count > 0)
                    {
                        sets.Add(new([.. currentTags], null));
                        currentTags = [];
                    }

                    return new([.. sets], restArgument.Values);
                }

                default:
                    throw new InvalidOperationException($"Invalid type: {token}");
            }
        }

        if (currentTags.Count > 0)
        {
            sets.Add(new([.. currentTags], null));
        }

        return new([.. sets], null);
    }
}
