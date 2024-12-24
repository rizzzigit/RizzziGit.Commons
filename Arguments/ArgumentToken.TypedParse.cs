using System.Reflection;

namespace RizzziGit.Commons.Arguments;

using Reflection;

public enum ArgumentObjectMode
{
    Ordered,
}

public sealed record TypedParserOptions
{
    public bool IgnoreUnknownArguments = false;
    public bool IgnoreUnexpectedRest = false;
    public bool IgnoreUnexpectedOrdinalValues = false;
}

internal sealed record TypedArgumentsMap(
    TypedOrdinalArgumentsSet[] Sets,
    TypedArgumentBinding<RestArgumentAttribute>? Rest
);

internal sealed record TypedOrdinalArgumentsSet(
    TypedArgumentBinding<ArgumentAttribute>[] Tags,
    TypedArgumentBinding<OrdinalArgumentAttribute>? OrdinalValue
)
{
    public bool IsNullable =>
        Tags.Any((tag) => tag.IsNullable) && (OrdinalValue?.IsNullable ?? true);

    public bool IsRequired =>
        Tags.Any((tag) => tag.IsRequired) && (OrdinalValue?.IsRequired ?? false);

    public override string ToString()
    {
        IEnumerable<string> names = Tags.Select((tag) => $"{tag.Attribute}");

        if (OrdinalValue != null)
        {
            names = names.Append($"{OrdinalValue.Member}");
        }

        return string.Join(", ", names);
    }
}

internal sealed record TypedArgumentBinding<T>(MemberInfo Member, T Attribute)
    where T : BaseArgumentAttribute
{
    private static bool IsMemberNullable(MemberInfo member)
    {
        NullabilityInfoContext context = new();

        NullabilityInfo info =
            member is FieldInfo f ? context.Create(f)
            : member is PropertyInfo p ? context.Create(p)
            : throw new InvalidOperationException(
                $"Member {member.Name} is not an field nor a property."
            );

        return info.WriteState is NullabilityState.Nullable;
    }

    public bool IsRequired => Attribute is ArgumentAttribute attribute && !attribute.IsRequired;
    public bool IsNullable => IsMemberNullable(Member);

    public override string ToString() =>
        Attribute switch
        {
            ArgumentAttribute argument => $"{argument}",

            OrdinalArgumentAttribute ordinalArgument =>
                $"{(IsRequired || !IsNullable ? $"<{ordinalArgument.Hint}>" : $"[{ordinalArgument.Hint}]")}",

            RestArgumentAttribute restArgument =>
                $"{(IsRequired || !IsNullable ? $"<-- {restArgument.Hint}>" : $"[-- {restArgument.Hint}]")}",

            _ => throw new InvalidOperationException($"Invalid type: {Member}"),
        };
}

public partial record ArgumentToken
{
    public static T Parse<T>(string[] args, TypedParserOptions? options = null) =>
        (T)Parse(typeof(T), args, options);

    public static object Parse(Type type, string[] args, TypedParserOptions? options = null)
    {
        if (type.IsAbstract)
        {
            throw new InvalidOperationException("Abstract input type is not supported.");
        }

        ConstructorInfo? constructor;

        if (
            options != null
            && (
                constructor = type.GetConstructor(
                    [typeof(IEnumerable<ArgumentToken>), typeof(TypedParserOptions)]
                )
            ) != null
        )
        {
            return constructor.Invoke([Parse(args), options]);
        }
        else if (
            options == null
            && (constructor = type.GetConstructor([typeof(IEnumerable<ArgumentToken>)])) != null
        )
        {
            return constructor.Invoke([Parse(args)]);
        }
        else if ((constructor = type.GetConstructor([])) != null)
        {
            return Parse(type, constructor, Parse(args), options);
        }

        throw new InvalidOperationException($"No suitable constructor for type {type.Name}.");
    }

    private static object Parse(
        Type type,
        ConstructorInfo constructor,
        IEnumerable<ArgumentToken> tokens,
        TypedParserOptions? options = null
    )
    {
        options ??= new();

        if (!type.TryGetCustomAttribute(out ArgumentObjectAttribute? attribute))
        {
            throw new InvalidOperationException(
                $"Specified type {type.Name} does not have a {typeof(ArgumentObjectAttribute).Name} attribute."
            );
        }

        return attribute.Mode switch
        {
            ArgumentObjectMode.Ordered => OrderedParse(type, constructor, tokens, options),
            _ => throw new InvalidOperationException($"Invalid type: {attribute.Mode}"),
        };
    }
}
