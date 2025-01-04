using System.Reflection;

namespace RizzziGit.Commons.Arguments;

using Reflection;

public abstract partial record ArgumentToken
{
    private static object OrderedParse(
        Type type,
        ConstructorInfo constructor,
        IEnumerable<ArgumentToken> tokens,
        TypedParserOptions options
    )
    {
        object instance = constructor.Invoke([]);

        TypedArgumentsMap typedMap = GenerateOrderedTypedMap(type, instance);
        ArgumentMap parsedMap = MappedParse(tokens);

        ApplyRest(typedMap.Rest, instance, parsedMap.Rest, options);

        for (
            int index = 0;
            index < int.Max(typedMap.Sets.Length, parsedMap.OrdinalSets.Length);
            index++
        )
        {
            ApplySet(
                instance,
                typedMap.Sets.ElementAtOrDefault(index),
                parsedMap.OrdinalSets.ElementAtOrDefault(index),
                options
            );
        }

        return instance;
    }

    private static TypedArgumentsMap GenerateOrderedTypedMap(Type type, object instance)
    {
        List<TypedArgumentBinding<ArgumentAttribute>> tags = [];
        List<TypedOrdinalArgumentsSet> sets = [];
        TypedArgumentBinding<RestArgumentAttribute>? rest = null;

        foreach (MemberInfo member in type.GetMembers())
        {
            if (rest != null)
            {
                throw new InvalidOperationException(
                    "The rest argument must be at the end of the object."
                );
            }

            if (
                !(member is FieldInfo or PropertyInfo)
                || !member.TryGetCustomAttribute(out BaseArgumentAttribute? baseArgumentAttribute)
            )
            {
                continue;
            }
            else if (member.GetCustomAttributes<BaseArgumentAttribute>().Count() > 1)
            {
                throw new InvalidOperationException(
                    "Cannot assign more than one argument attribute."
                );
            }
            switch (baseArgumentAttribute)
            {
                case OrdinalArgumentAttribute ordinalArgumentAttribute:
                {
                    sets.Add(new([.. tags], new(instance, new(member), ordinalArgumentAttribute)));
                    tags = [];
                    break;
                }

                case ArgumentAttribute argumentAttribute:
                {
                    if (
                        tags.Any(
                            (entry) =>
                                argumentAttribute.Key == entry.Attribute.Key
                                || argumentAttribute.ShorthandKey == entry.Attribute.ShorthandKey
                        )
                    )
                    {
                        throw new InvalidOperationException(
                            "Arguments cannot have duplicate keys or shorthand keys."
                        );
                    }

                    tags.Add(new(instance, new(member), argumentAttribute));
                    break;
                }

                case RestArgumentAttribute restArgumentAttribute:
                {
                    rest = new(instance, new(member), restArgumentAttribute);
                    break;
                }
            }
        }

        if (tags.Count > 0)
        {
            sets.Add(new([.. tags], null));
        }

        return new([.. sets], rest);
    }
}
