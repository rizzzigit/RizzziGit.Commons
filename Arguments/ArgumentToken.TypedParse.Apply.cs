using System.Reflection;

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
                    typedOrdinalValue.Value = null;
                }
            }
            else
            {
                typedOrdinalValue.Value = parsedOrdinalValue;
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

        foreach (var typedTag in typedTags)
        {
            var result = parsedTags
                .Where(
                    (tag) =>
                        (
                            tag is OrdinalArgumentTag.KeyValuePair kvp
                            && kvp.Key == typedTag.Attribute.Key
                        )
                        || (
                            tag is OrdinalArgumentTag.ShorthandKeyValuePair skvp
                            && skvp.Key == typedTag.Attribute.ShorthandKey
                        )
                )
                .ToArray();

            if (result.Length != 0)
            {
                Type memberType = typedTag.GetPropertyOrFieldType();

                object? toSet;

                if (result.Length > 1)
                {
                    if (!memberType.IsArray)
                    {
                        throw new InvalidOperationException(
                            $"Multiple {typedTag} tags is not supported."
                        );
                    }

                    toSet = result
                        .Select(
                            (parsedTag) =>
                                CastToType(parsedTag.Value!, memberType.GetElementType()!)
                        )
                        .ToArray();
                }
                else
                {
                    toSet = CastToType(result.First().Value!, memberType);
                }

                if (toSet is null && !typedTag.IsNullable)
                {
                    throw new InvalidOperationException(
                        $"Parameter {typedTag.Attribute} requires a value."
                    );
                }

                typedTag.Value = toSet;
            }
            else
            {
                if (typedTag.IsRequired || !typedTag.IsNullable)
                {
                    throw new InvalidOperationException(
                        $"Required or non-nullable parameter is missing: {typedTag}"
                    );
                }
            }
        }

        if (!options.IgnoreUnknownArguments)
        {
            var a = parsedTags
                .Where(
                    (e) =>
                        !(
                            (
                                e is OrdinalArgumentTag.KeyValuePair kvp
                                && typedTags.Any((tag) => kvp.Key == tag.Attribute.Key)
                            )
                            || (
                                e is OrdinalArgumentTag.ShorthandKeyValuePair skvp
                                && typedTags.Any((tag) => skvp.Key == tag.Attribute.ShorthandKey)
                            )
                        )
                )
                .ToArray();

            if (a.Length != 0)
            {
                throw new InvalidOperationException(
                    $"Unknown paramters: {string.Join(", ", a.Select((tag) => $"{tag}"))}"
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
                    member.Value = null!;
                }
            }
            else
            {
                member.Value = values;
            }
        }
    }
}
