using System.Reflection;

namespace RizzziGit.Commons.Arguments;

public partial record ArgumentToken
{
    private static object CastToType(object source, Type targetType)
    {
        if (source.GetType() == targetType || source.GetType().IsAssignableTo(targetType))
        {
            return source;
        }
        else if (source is IConvertible && targetType.IsAssignableTo(typeof(IConvertible)))
        {
            return Convert.ChangeType(source, targetType);
        }

        throw new InvalidCastException($"This value cannot be cast to {targetType.Name}.");
    }

    private static void SetPropertyOrFieldValue(
        MemberInfo info,
        object instance,
        bool firstSet,
        object value
    )
    {
        object newValue(Type type, object? oldValue, object newValue)
        {
            if (type.IsArray)
            {
                type = type.GetElementType()!;

                object[] array =
                    oldValue is null || firstSet
                        ? [CastToType(newValue, type)]
                        : [.. (Array)oldValue, CastToType(newValue, type)];

                return array;
            }
            else
            {
                return newValue;
            }
        }

        switch (info)
        {
            case PropertyInfo property:
            {
                property.SetValue(
                    instance,
                    newValue(property.PropertyType, property.GetValue(instance), value)
                );
                return;
            }

            case FieldInfo field:
            {
                field.SetValue(
                    instance,
                    newValue(field.FieldType, field.GetValue(instance), value)
                );
                return;
            }
        }

        throw new InvalidOperationException($"Member {info.Name} is not an field nor a property.");
    }
}
