using System.Reflection;

namespace RizzziGit.Commons.Arguments;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
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

        if (OrdinalValue is not null)
        {
            names = names.Append($"{OrdinalValue.Member}");
        }

        return string.Join(", ", names);
    }
}

internal sealed record PropertyOrFieldInfoHolder(MemberInfo Member)
{
    [DoesNotReturn]
    public Exception ThrowNotPropertyOrFieldException() =>
        throw new InvalidOperationException($"Property {Member} is not a property nor a field.");

    public string Name => Member.Name;

    public bool IsRequired => Member.TryGetCustomAttribute<RequiredAttribute>(out _);

    public bool IsNullable
    {
        get
        {
            NullabilityInfoContext context = new();

            NullabilityInfo info =
                Member is PropertyInfo property ? context.Create(property)
                : Member is FieldInfo f ? context.Create(f)
                : throw ThrowNotPropertyOrFieldException();

            return info.WriteState is NullabilityState.Nullable;
        }
    }

    public object? GetValue(object instance) =>
        Member is PropertyInfo property ? property.GetValue(instance)
        : Member is FieldInfo field ? field.FieldType
        : throw ThrowNotPropertyOrFieldException();

    public void SetValue(object instance, object? value)
    {
        if (Member is PropertyInfo property)
        {
            property.SetValue(instance, value);
        }
        else if (Member is FieldInfo field)
        {
            field.SetValue(instance, value);
        }
        else
        {
            throw ThrowNotPropertyOrFieldException();
        }
    }

    public bool IsProperty([NotNullWhen(true)] out PropertyInfo? property) =>
        (property = Member as PropertyInfo) is not null;

    public bool IsField([NotNullWhen(true)] out FieldInfo? field) =>
        (field = Member as FieldInfo) is not null;

    public Type GetPropertyOrFieldType() =>
        Member is PropertyInfo property ? property.PropertyType
        : Member is FieldInfo field ? field.FieldType
        : throw ThrowNotPropertyOrFieldException();
}

internal sealed record TypedArgumentBinding<T>(
    object Instance,
    PropertyOrFieldInfoHolder Member,
    T Attribute
)
    where T : BaseArgumentAttribute
{
    public bool IsRequired =>
        Member.Member is PropertyInfo property
            ? property.TryGetCustomAttribute<RequiredAttribute>(out _)
        : Member.Member is FieldInfo a ? a.TryGetCustomAttribute<RequiredAttribute>(out _)
        : (Attribute is ArgumentAttribute argumentAttribute && argumentAttribute.IsRequired);

    public bool IsNullable => Member.IsNullable;

    public object? Value
    {
        get => Member.GetValue(Instance);
        set => Member.SetValue(Instance, value);
    }

    public Type GetPropertyOrFieldType() => Member.GetPropertyOrFieldType();

    public override string ToString() =>
        Attribute switch
        {
            ArgumentAttribute argument => $"{argument}",

            OrdinalArgumentAttribute ordinalArgument =>
                $"{(IsRequired || (IsRequired && !Member.GetPropertyOrFieldType().IsPrimitive && !IsNullable) ? $"<{ordinalArgument.Hint}>" : $"[{ordinalArgument.Hint}]")}",

            RestArgumentAttribute restArgument =>
                $"{(IsRequired || (IsRequired && !Member.GetPropertyOrFieldType().IsPrimitive && !IsNullable) ? $"<-- {restArgument.Hint}>" : $"[-- {restArgument.Hint}]")}",

            _ => throw new InvalidOperationException($"Invalid type: {Member}"),
        };
}

public partial record ArgumentToken
{
    public static T Parse<T>(string[] args, TypedParserOptions? options = null) =>
        (T)Parse(typeof(T), args, options);

    public static object Parse(Type type, string[] args, TypedParserOptions? options = null)
    {
        Exception? exception = null;

        try
        {
            if (type.IsAbstract)
            {
                throw new InvalidOperationException("Abstract input type is not supported.");
            }

            ConstructorInfo? constructor;

            if (
                options is not null
                && (
                    constructor = type.GetConstructor(
                        [typeof(IEnumerable<ArgumentToken>), typeof(TypedParserOptions)]
                    )
                )
                    is not null
            )
            {
                return constructor.Invoke([Parse(args), options]);
            }
            else if (
                options is null
                && (constructor = type.GetConstructor([typeof(IEnumerable<ArgumentToken>)]))
                    is not null
            )
            {
                return constructor.Invoke([Parse(args)]);
            }
            else if ((constructor = type.GetConstructor([])) is not null)
            {
                return Parse(type, constructor, Parse(args), options);
            }
        }
        catch (Exception e)
        {
            exception = e;
        }

        throw new InvalidOperationException(
            $"No suitable constructor for type {type.Name}.",
            exception
        );
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
            // ArgumentObjectMode.Unordered => UnorderedParse(type, constructor, tokens, options),
            _ => throw new InvalidOperationException($"Invalid type: {attribute.Mode}"),
        };
    }
}
