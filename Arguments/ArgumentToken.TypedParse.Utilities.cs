using System.Reflection;

namespace RizzziGit.Commons.Arguments;

public partial record ArgumentToken
{
    private static object? CastToType(
        BaseArgumentAttribute attribute,
        object instance,
        object? source,
        Type targetType
    )
    {
        if (source is null)
        {
            return null;
        }

        if (source.GetType() == targetType || source.GetType().IsAssignableTo(targetType))
        {
            return source;
        }

        if (source is IConvertible && targetType.IsAssignableTo(typeof(IConvertible)))
        {
            return Convert.ChangeType(source, targetType);
        }

        if (attribute.ParserMethodName != null)
        {
            MethodInfo meth;

            {
                MethodInfo[] meths =
                [
                    .. instance
                        .GetType()
                        .GetMethods()
                        .Where((meth) => meth.Name == attribute.ParserMethodName),
                ];

                if (meths.Length == 0)
                {
                    throw new ArgumentException(
                        $"No method found by the name of `{attribute.ParserMethodName}`.",
                        nameof(attribute)
                    );
                }

                if (meths.Length > 1)
                {
                    throw new ArgumentException(
                        $"Method name `{attribute.ParserMethodName}` is ambiguous.",
                        nameof(attribute)
                    );
                }

                meth = meths[0];
            }

            if (!meth.IsStatic)
            {
                throw new ArgumentException(
                    $"`{meth.Name}` method must be static.",
                    nameof(attribute)
                );
            }

            if (!meth.ReturnType.IsAssignableTo(targetType))
            {
                throw new ArgumentException(
                    $"The return type ({meth.ReturnType.Name}) of the parser method cannot be assigned to the target type ({targetType.Name}).",
                    nameof(attribute)
                );
            }

            {
                ParameterInfo[] parameters = meth.GetParameters();

                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
                {
                    throw new ArgumentException(
                        $"Parser methods must have only one string argument.",
                        nameof(attribute)
                    );
                }
            }

            return meth.Invoke(null, [source]);
        }

        throw new InvalidCastException($"This value ({source}) cannot be cast to {targetType.Name}.");
    }
}
