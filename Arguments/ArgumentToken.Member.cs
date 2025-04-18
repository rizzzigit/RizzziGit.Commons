using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using RizzziGit.Commons.Reflection;
using RizzziGit.Commons.Utilities;

namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    private interface IMember
    {
        public MemberInfo MemberInfo { get; }
        public Type Type { get; }
        public bool HasDefaultValue { get; }
        public bool RequiresValue { get; }
        public bool IsNullable { get; }
        public string ToString();
    }

    private abstract record Member<T>(
        MemberInfo MemberInfo,
        T Attribute,
        Type Type,
        bool HasDefaultValue,
        bool RequiresValue,
        bool IsNullable
    ) : IMember
        where T : BaseArgumentAttribute
    {
        public sealed override string ToString() => Attribute.ToString();
    }

    private sealed record PairMember(
        MemberInfo MemberInfo,
        ArgumentAttribute Attribute,
        Type Type,
        bool HasDefaultValue,
        bool RequiresValue,
        bool IsNullable
    )
        : Member<ArgumentAttribute>(
            MemberInfo,
            Attribute,
            Type,
            HasDefaultValue,
            RequiresValue,
            IsNullable
        );

    private sealed record OrdinalMember(
        MemberInfo MemberInfo,
        OrdinalArgumentAttribute Attribute,
        Type Type,
        bool HasDefaultValue,
        bool RequiresValue,
        bool IsNullable
    )
        : Member<OrdinalArgumentAttribute>(
            MemberInfo,
            Attribute,
            Type,
            HasDefaultValue,
            RequiresValue,
            IsNullable
        );

    private sealed record RestMember(
        MemberInfo Member,
        RestArgumentAttribute Attribute,
        Type Type,
        bool HasDefaultValue,
        bool RequiresValue,
        bool IsNullable
    )
        : Member<RestArgumentAttribute>(
            Member,
            Attribute,
            Type,
            HasDefaultValue,
            RequiresValue,
            IsNullable
        );

    private static IEnumerable<IMember> GetMembers(Type type, object instance)
    {
        foreach (MemberInfo member in type.GetMembers())
        {
            if (TryGetMember(member, instance, out IMember? argumentMember))
            {
                yield return argumentMember;
            }
        }
    }

    private static bool TryGetMember(
        MemberInfo memberInfo,
        object instance,
        [NotNullWhen(true)] out IMember? result
    )
    {
        if (!memberInfo.TryGetCustomAttribute(out BaseArgumentAttribute? attribute))
        {
            result = null;
            return false;
        }

        Type memberType;
        bool memberHasDefaultValue;
        bool memberRequiresValue =
            memberInfo.GetCustomAttribute<RequiredAttribute>() is not null
            || memberInfo.GetCustomAttribute<RequiredMemberAttribute>() is not null;
        bool memberIsNullable;

        if (memberInfo is PropertyInfo propertyInfo)
        {
            AnalyzeNullability(
                propertyInfo.PropertyType,
                propertyInfo.GetCustomAttribute<NullableAttribute>,
                propertyInfo.GetCustomAttribute<NullableContextAttribute>,
                () => propertyInfo.GetValue(instance),
                out memberType,
                out memberIsNullable,
                out memberHasDefaultValue
            );
        }
        else if (memberInfo is FieldInfo fieldInfo)
        {
            AnalyzeNullability(
                fieldInfo.FieldType,
                fieldInfo.GetCustomAttribute<NullableAttribute>,
                fieldInfo.GetCustomAttribute<NullableContextAttribute>,
                () => fieldInfo.GetValue(instance),
                out memberType,
                out memberIsNullable,
                out memberHasDefaultValue
            );
        }
        else
        {
            result = null;
            return false;
        }

        if (attribute is ArgumentAttribute baseArgumentAttribute)
        {
            result = new PairMember(
                memberInfo,
                baseArgumentAttribute,
                memberType,
                memberHasDefaultValue,
                memberRequiresValue,
                memberIsNullable
            );
        }
        else if (attribute is OrdinalArgumentAttribute ordinalArgumentAttribute)
        {
            result = new OrdinalMember(
                memberInfo,
                ordinalArgumentAttribute,
                memberType,
                memberHasDefaultValue,
                memberRequiresValue,
                memberIsNullable
            );
        }
        else if (attribute is RestArgumentAttribute restArgumentAttribute)
        {
            result = new RestMember(
                memberInfo,
                restArgumentAttribute,
                memberType,
                memberHasDefaultValue,
                memberRequiresValue,
                memberIsNullable
            );
        }
        else
        {
            result = null;
        }

        return result is not null;
    }

    private static void AnalyzeNullability(
        Type fieldType,
        Func<NullableAttribute?> getNullableAttribute,
        Func<NullableContextAttribute?> getNullableContextAttribute,
        Func<object?> getValue,
        out Type resultType,
        out bool isNullable,
        out bool hasDefaultValue
    )
    {
        if (fieldType.IsValueType)
        {
            Type? nullableType = Nullable.GetUnderlyingType(fieldType);

            if (nullableType is not null)
            {
                resultType = nullableType;
                isNullable = true;
            }
            else
            {
                resultType = fieldType;
                isNullable = false;
            }
        }
        else if (fieldType.IsByRef || fieldType.IsPointer)
        {
            resultType = fieldType.GetElementType()!;
            isNullable = false;
        }
        else
        {
            resultType = fieldType;
            isNullable = false;

            {
                NullableAttribute? nullableAttribute = getNullableAttribute();

                if (nullableAttribute is not null && nullableAttribute.NullableFlags.First() == 2)
                {
                    isNullable = true;
                }
                else
                {
                    NullableContextAttribute? nullableContextAttribute =
                        getNullableContextAttribute();

                    if (nullableContextAttribute is not null && nullableContextAttribute.Flag == 2)
                    {
                        resultType = fieldType;
                        isNullable = true;
                    }
                }
            }
        }

        hasDefaultValue = resultType.IsPrimitive || getValue() is not null;
    }
}
