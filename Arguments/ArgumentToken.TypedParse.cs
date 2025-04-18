using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    public static T Parse<T>(string[] args) => (T)Parse(typeof(T), args);

    public static object Parse(Type type, string[] args, ArgumentTokenOptions? options = null)
    {
        options ??= new();

        ArgumentObjectAttribute argumentObjectAttribute =
            type.GetCustomAttribute<ArgumentObjectAttribute>()
            ?? throw new ArgumentException(
                $"Type {type.Name} does not have a {nameof(ArgumentObjectAttribute)}"
            );

        if (argumentObjectAttribute.Method == ParseMethod.Ordinal)
        {
            return ParseOrdered(type, args, options);
        }
        else
        {
            throw new ArgumentException($"Unknown parse method: {argumentObjectAttribute.Method}");
        }
    }

    private static object ParseOrdered(Type type, string[] arguments, ArgumentTokenOptions options)
    {
        ConstructorInfo constructor =
            type.GetConstructor([])
            ?? throw new ArgumentException($"Type {type.Name} does not have a default constructor");

        object instance = constructor.Invoke([]);

        foreach (
            MapGroup mapGroup in GetMapGroups(GetMembers(type, instance), Parse(arguments), options)
        )
        {
            foreach (PairMap pairMap in mapGroup.Pairs)
            {
                SetValue(
                    pairMap.Member.MemberInfo,
                    pairMap.Member.Type,
                    instance,
                    pairMap.Member.Attribute,
                    pairMap.Member.Type,
                    pairMap.Token.Value,
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
                    mapGroup.OrdinalMap.Member.Type,
                    mapGroup.OrdinalMap.Token,
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
                    mapGroup.RestMap.Member.Type,
                    mapGroup.RestMap.Token,
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
        BaseArgumentAttribute attribute,
        [NotNullWhen(false)] out Exception? exception
    )
    {
        if (attribute.RequiresValue)
        {
            exception = ExceptionDispatchInfo.SetCurrentStackTrace(
                new ArgumentException(
                    $"Argument {attribute} needs a value since it has {nameof(ArgumentAttribute.RequiresValue)} property set to true in its argument attribute declaration."
                )
            );
        }
        else if (memberRequiresValue)
        {
            exception = ExceptionDispatchInfo.SetCurrentStackTrace(
                new ArgumentException(
                    $"Argument {attribute} needs a value since it has a required modifier in its member declaration."
                )
            );
        }
        else if (!memberType.IsPrimitive && !memberIsNullable && !memberHasDefaultValue)
        {
            exception = ExceptionDispatchInfo.SetCurrentStackTrace(
                new ArgumentException(
                    $"Argument {attribute} needs a value since it is not nullable and has no default value."
                )
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
        BaseArgumentAttribute attribute,
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

                                return
                                    method.Name == attribute.Parser
                                    && parameters.Length == 1
                                    && isRest
                                    ? parameters[0].ParameterType.IsAssignableTo(typeof(string[]))
                                    : parameters[0].ParameterType.IsAssignableTo(typeof(string));
                            }
                        ),
                ];

                if (parserMethods.Length > 1)
                {
                    throw new ArgumentException(
                        $"Parser method name `{attribute.Parser}` is ambiguous."
                    );
                }

                if (parserMethods.Length != 1)
                {
                    throw new ArgumentException(
                        $"Type {type.Name} does not have a `{attribute.Parser}(string)` method."
                    );
                }

                method = parserMethods[0];
            }

            if (method.ReturnType != memberType)
            {
                throw new ArgumentException(
                    $"Parser method `{attribute.Parser}` return type ({method.ReturnType.FullName}) does not match the expected declared type ({memberType.FullName}) of the property or field where the argument attribute is applied."
                );
            }

            setValue(method.Invoke(null, [value]));
        }
        else if (valueType == memberType)
        {
            setValue(value);
        }
        else if (type.GetType().IsAssignableTo(typeof(IConvertible)))
        {
            if (value is null)
            {
                throw new ArgumentException($"Argument {attribute} has a required attribute");
            }

            setValue(Convert.ChangeType(value, memberType));
        }
        else
        {
            throw new ArgumentException(
                $"Value type {valueType.FullName} cannot be automatically converted to type {memberType.FullName}."
            );
        }
    }
}
