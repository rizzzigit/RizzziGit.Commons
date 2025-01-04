using System.Reflection;

namespace RizzziGit.Commons.Arguments;

public partial record ArgumentToken
{
    private static object? CastToType(object? source, Type targetType)
    {
        if (source is null)
        {
            return null;
        }
        else if (source.GetType() == targetType || source.GetType().IsAssignableTo(targetType))
        {
            return source;
        }
        else if (source is IConvertible && targetType.IsAssignableTo(typeof(IConvertible)))
        {
            return Convert.ChangeType(source, targetType);
        }

        throw new InvalidCastException($"This value cannot be cast to {targetType.Name}.");
    }
}
