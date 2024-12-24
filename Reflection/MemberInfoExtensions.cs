using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace RizzziGit.Commons.Reflection;

public static class MemberInfoExtensions
{
    public static bool TryGetCustomAttribute<T>(
        this MemberInfo member,
        [NotNullWhen(true)] out T? attribute
    )
        where T : Attribute => (attribute = member.GetCustomAttribute<T>()) != null;
}
