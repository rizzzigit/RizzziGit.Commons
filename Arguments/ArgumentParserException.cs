using System.Reflection;

namespace RizzziGit.Commons.Arguments;

public abstract class ArgumentParserException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class UnknownArgumentException(
    ArgumentToken token,
    string message,
    Exception? innerException = null
) : ArgumentParserException($"{message} (token: {token})", innerException)
{
    public readonly ArgumentToken ArgumentToken = token;
}

public sealed class MissingArgumentException(
    ArgumentAttribute attribute,
    string message,
    Exception? innerException = null
) : ArgumentParserException($"{message} (argument: {attribute})", innerException)
{
    public readonly ArgumentAttribute Attribute = attribute;
}

public sealed class UnexpectedMemberException(
    ArgumentParser.IMember member,
    string message,
    Exception? innerException = null
) : ArgumentParserException($"{message} (member: {member})", innerException)
{
    public readonly ArgumentParser.IMember Member = member;
}

public sealed class ArgumentParserMethodException(
    string methodName,
    string message,
    Exception? innerException = null
) : ArgumentParserException($"{message} (name: {methodName})", innerException)
{
    public readonly string MethodName = methodName;
}

public sealed class ArgumentObjectException(
    Type type,
    string message,
    Exception? innerException = null
) : ArgumentParserException($"{message} (type: {type.FullName})", innerException)
{
    public readonly Type Type = type;
}
