using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Arguments;

public static partial class ArgumentParser
{
    public static T Parse<T>(string[] args) => (T)Parse(typeof(T), args);

    public static object Parse(Type type, string[] args, ArgumentParserOptions? options = null)
    {
        options ??= new();

        ArgumentObjectAttribute argumentObjectAttribute =
            type.GetCustomAttribute<ArgumentObjectAttribute>()
            ?? throw new ArgumentObjectException(type, "Missing argument object attribute.");

        if (argumentObjectAttribute.Method == ParseMethod.Ordinal)
        {
            return ParseOrdered(type, args, options);
        }
        else
        {
            throw new ArgumentObjectException(
                type,
                $"Unknown parse method: {argumentObjectAttribute.Method}."
            );
        }
    }

    private static object ParseOrdered(Type type, string[] arguments, ArgumentParserOptions options)
    {
        ConstructorInfo constructor =
            type.GetConstructor([])
            ?? throw new ArgumentObjectException(type, "Missing zero-parameter constructor.");

        object instance = constructor.Invoke([]);

        foreach (
            MapGroup mapGroup in GetMapGroups(GetMembers(type, instance), Parse(arguments), options)
        )
        {
            foreach (TagMap tagMap in mapGroup.Tags)
            {
                SetValue(
                    tagMap.Member.MemberInfo,
                    tagMap.Member.Type,
                    instance,
                    tagMap.Member.Attribute,
                    type,
                    tagMap.Token.Value,
                    false
                );
            }

            if (mapGroup.OrdinalMap is not null)
            {
                SetValue(
                    mapGroup.OrdinalMap.Member.MemberInfo,
                    mapGroup.OrdinalMap.Member.Type,
                    instance,
                    mapGroup.OrdinalMap.Member.Attribute,
                    type,
                    mapGroup.OrdinalMap.Token.Value,
                    false
                );
            }

            if (mapGroup.RestMap is not null)
            {
                SetValue(
                    mapGroup.RestMap.Member.MemberInfo,
                    mapGroup.RestMap.Member.Type,
                    instance,
                    mapGroup.RestMap.Member.Attribute,
                    type,
                    mapGroup.RestMap.Token.Values,
                    true
                );
            }
        }

        return instance;
    }

    private static bool ValidateNullability(
        bool memberRequiresValue,
        Type memberType,
        bool memberIsNullable,
        bool memberHasDefaultValue,
        ArgumentAttribute attribute,
        [NotNullWhen(false)] out Exception? exception
    )
    {
        if (attribute.RequiresValue)
        {
            exception = ExceptionDispatchInfo.SetCurrentStackTrace(
                new MissingArgumentException(
                    attribute,
                    $"`{nameof(TagArgumentAttribute.RequiresValue)}` property set to true in its argument attribute declaration."
                )
            );
        }
        else if (memberRequiresValue)
        {
            exception = ExceptionDispatchInfo.SetCurrentStackTrace(
                new MissingArgumentException(
                    attribute,
                    $"Has a required modifier in its member declaration."
                )
            );
        }
        else if (!memberType.IsPrimitive && !memberIsNullable && !memberHasDefaultValue)
        {
            exception = ExceptionDispatchInfo.SetCurrentStackTrace(
                new MissingArgumentException(attribute, $"Not nullable and has no default value.")
            );
        }
        else
        {
            exception = null;
        }

        return exception is null;
    }

    private static void SetValue(
        MemberInfo memberInfo,
        Type memberType,
        object instance,
        ArgumentAttribute attribute,
        Type type,
        object? value,
        bool isRest
    )
    {
        if (value is null)
        {
            return;
        }

        void setValue(object? value)
        {
            if (memberInfo is PropertyInfo propertyInfo)
            {
                propertyInfo.SetValue(instance, value);
            }
            else if (memberInfo is FieldInfo fieldInfo)
            {
                fieldInfo.SetValue(instance, value);
            }
        }

        Type valueType = value.GetType();
        Type instanceType = instance.GetType();

        if (attribute.Parser is not null)
        {
            MethodInfo method;

            {
                MethodInfo[] parserMethods =
                [
                    .. instanceType
                        .GetMethods(
                            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
                        )
                        .Where(
                            (method) =>
                            {
                                ParameterInfo[] parameters = method.GetParameters();

                                if (method.Name != attribute.Parser || parameters.Length != 1)
                                {
                                    return false;
                                }

                                if (isRest)
                                {
                                    if (
                                        !parameters[0]
                                            .ParameterType.IsAssignableTo(typeof(string[]))
                                    )
                                    {
                                        return false;
                                    }
                                }
                                else if (
                                    !parameters[0].ParameterType.IsAssignableTo(typeof(string))
                                )
                                {
                                    return false;
                                }

                                return true;
                            }
                        ),
                ];

                if (parserMethods.Length > 1)
                {
                    throw new ArgumentParserMethodException(
                        attribute.Parser,
                        "Ambiguous parser method name."
                    );
                }

                if (parserMethods.Length != 1)
                {
                    throw new ArgumentParserMethodException(
                        attribute.Parser,
                        "No parser found with the name."
                    );
                }

                method = parserMethods[0];
            }

            if (method.ReturnType != memberType)
            {
                throw new ArgumentParserMethodException(
                    attribute.Parser,
                    $"Parser return type ({method.ReturnType.FullName}) and member type ({memberType.FullName}) does not match."
                );
            }

            try
            {
                setValue(method.Invoke(null, [value]));
            }
            catch (TargetInvocationException invocationException)
            {
                throw new ArgumentParserMethodException(
                    attribute.Parser,
                    $"Error invoking parser method: {invocationException.InnerException?.Message ?? "Unknown error."}",
                    invocationException.InnerException
                );
            }
        }
        else if (valueType == memberType)
        {
            setValue(value);
        }
        else if (type.GetType().IsAssignableTo(typeof(IConvertible)))
        {
            if (value is null)
            {
                throw new MissingArgumentException(attribute, $"Value cannot be null.");
            }

            setValue(Convert.ChangeType(value, memberType));
        }
        else
        {
            throw new ArgumentObjectException(
                type,
                $"Value type {valueType.FullName} cannot be automatically converted to type {memberType.FullName}."
            );
        }
    }
}
