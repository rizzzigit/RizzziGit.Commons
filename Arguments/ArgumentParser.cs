using System.Diagnostics.CodeAnalysis;

namespace RizzziGit.Commons.Arguments;

public static partial class ArgumentParser
{
    private abstract record ParserState
    {
        private ParserState() { }

        public sealed record PendingTag(string Key) : ParserState;

        public sealed record PendingShortTag(char Key) : ParserState;

        public sealed record None : ParserState;
    }

    public static IEnumerable<ArgumentToken> Parse(string[] arguments)
    {
        ParserState state = new ParserState.None();

        bool tryPushPending(string? value, [NotNullWhen(true)] out ArgumentToken? result)
        {
            if (state is ParserState.PendingTag(string key))
            {
                state = new ParserState.None();
                result = new ArgumentToken.Tag(key, value);
                return true;
            }

            if (state is ParserState.PendingShortTag(char shorthandKey))
            {
                state = new ParserState.None();
                result = new ArgumentToken.ShortTag(shorthandKey, value);
                return true;
            }

            result = null;
            return false;
        }

        for (int argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
        {
            string argument = arguments[argumentIndex];

            if (argument == "--")
            {
                if (tryPushPending(null, out ArgumentToken? token))
                {
                    yield return token;
                }

                yield return new ArgumentToken.Rest(arguments[(argumentIndex + 1)..]);
                yield break;
            }
            else if (argument.StartsWith("--"))
            {
                if (tryPushPending(null, out ArgumentToken? token))
                {
                    yield return token;
                }

                state = new ParserState.PendingTag(argument[2..]);
            }
            else if (argument.StartsWith('-'))
            {
                foreach (char argChar in argument[1..])
                {
                    if (tryPushPending(null, out ArgumentToken? token))
                    {
                        yield return token;
                    }

                    state = new ParserState.PendingShortTag(argChar);
                }
            }
            else if (tryPushPending(argument, out ArgumentToken? token))
            {
                yield return token;
            }
            else
            {
                yield return new ArgumentToken.Ordinal(argument);
            }
        }

        {
            if (tryPushPending(null, out ArgumentToken? entry))
            {
                yield return entry;
            }
        }
    }
}
