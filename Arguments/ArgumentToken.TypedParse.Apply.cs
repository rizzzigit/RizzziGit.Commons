namespace RizzziGit.Commons.Arguments;

public partial record ArgumentToken
{
    private static void ApplySet(
        object instance,
        TypedOrdinalArgumentsSet? typedSet,
        OrdinalArgumentSet? parsedSet,
        TypedParserOptions options
    )
    {
        ApplyTags(instance, parsedSet?.Tags ?? [], typedSet?.Tags ?? [], options);
        ApplyOrdinal(instance, typedSet?.OrdinalValue, parsedSet?.OrdinalValue, options);
    }

    private static void ApplyOrdinal(
        object instance,
        TypedArgumentBinding<OrdinalArgumentAttribute>? typedOrdinalValue,
        string? parsedOrdinalValue,
        TypedParserOptions options
    )
    {
        if (typedOrdinalValue == null)
        {
            if (parsedOrdinalValue != null)
            {
                if (options.IgnoreUnexpectedOrdinalValues)
                {
                    throw new InvalidOperationException(
                        $"Unexpected ordinal value: {parsedOrdinalValue}"
                    );
                }
            }
        }
        else
        {
            if (parsedOrdinalValue == null)
            {
                if (typedOrdinalValue.IsRequired || !typedOrdinalValue.IsNullable)
                {
                    throw new InvalidOperationException(
                        $"Required or non-nullable ordinal value is missing: {typedOrdinalValue}"
                    );
                }
                else
                {
                    SetPropertyOrFieldValue(typedOrdinalValue.Member, instance, true, null!);
                }
            }
            else
            {
                SetPropertyOrFieldValue(
                    typedOrdinalValue.Member,
                    instance,
                    true,
                    parsedOrdinalValue
                );
            }
        }
    }

    private static void ApplyTags(
        object instance,
        OrdinalArgumentTag[] parsedTags,
        TypedArgumentBinding<ArgumentAttribute>[] typedTags,
        TypedParserOptions options
    )
    {
        List<TypedArgumentBinding<ArgumentAttribute>> setTags = [];

        foreach (OrdinalArgumentTag parsedTag in parsedTags)
        {
            foreach (TypedArgumentBinding<ArgumentAttribute> typedTag in typedTags)
            {
                string toSet;

                switch (parsedTag)
                {
                    case OrdinalArgumentTag.KeyValuePair(string key, string value):
                    {
                        if (typedTag.Attribute.Key != key)
                        {
                            if (!options.IgnoreUnknownArguments)
                            {
                                throw new InvalidOperationException($"Unexpected argument: {key}");
                            }

                            continue;
                        }

                        toSet = value;
                        break;
                    }

                    case OrdinalArgumentTag.ShorthandKeyValuePair(char key, string value):
                    {
                        if (typedTag.Attribute.ShorthandKey != key)
                        {
                            if (!options.IgnoreUnknownArguments)
                            {
                                throw new InvalidOperationException(
                                    $"Unexpected shorthand argument: {key}"
                                );
                            }

                            continue;
                        }

                        toSet = value;
                        break;
                    }

                    default:
                        throw new InvalidOperationException(
                            $"Invalid type: {parsedTag.GetType().Name}"
                        );
                }

                bool firstSet = !typedTags.Contains(typedTag);
                SetPropertyOrFieldValue(typedTag.Member, instance, firstSet, toSet);

                if (firstSet)
                {
                    setTags.Add(typedTag);
                }
            }
        }

        var unsetTags = typedTags.Except(typedTags).ToArray();

        foreach (var tag in unsetTags)
        {
            if (tag.IsRequired || !tag.IsNullable)
            {
                throw new InvalidOperationException(
                    $"Required or non-nullable argument is missing: {tag}"
                );
            }
        }
    }

    private static void ApplyRest(
        TypedArgumentBinding<RestArgumentAttribute>? member,
        object instance,
        string[]? values,
        TypedParserOptions options
    )
    {
        if (member == null)
        {
            if (values != null)
            {
                if (options.IgnoreUnexpectedRest)
                {
                    throw new InvalidOperationException("Unexpected rest argument.");
                }
            }
        }
        else
        {
            if (values == null)
            {
                if (!member.IsNullable)
                {
                    throw new InvalidOperationException("Rest argument is required.");
                }
                else
                {
                    SetPropertyOrFieldValue(member.Member, instance, true, null!);
                }
            }
            else
            {
                SetPropertyOrFieldValue(member.Member, instance, true, values);
            }
        }
    }
}
