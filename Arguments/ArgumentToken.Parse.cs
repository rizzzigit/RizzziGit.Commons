using System.Diagnostics.CodeAnalysis;

namespace RizzziGit.Commons.Arguments;

public abstract partial record ArgumentToken
{
    private abstract record ParserState
    {
        private ParserState() { }

        public sealed record PendingKeyValuePair(string Key) : ParserState;

        public sealed record PendingShorthandKeyValuePair(char Key) : ParserState;

        public sealed record None : ParserState;
    }

    public static IEnumerable<ArgumentToken> Parse(string[] args)
    {
        ParserState state = new ParserState.None();

        bool tryPushPending(string? value, [NotNullWhen(true)] out ArgumentToken? result)
        {
            if (state is ParserState.PendingKeyValuePair(string key))
            {
                state = new ParserState.None();
                result = new KeyValuePair(key, value);
                return true;
            }

            if (state is ParserState.PendingShorthandKeyValuePair(char shorthandKey))
            {
                state = new ParserState.None();
                result = new ShorthandKeyValuePair(shorthandKey, value);
                return true;
            }

            result = null;
            return false;
        }

        for (int argIndex = 0; argIndex < args.Length; argIndex++)
        {
            string arg = args[argIndex];

            if (arg == "--")
            {
                if (tryPushPending(null, out ArgumentToken? entry))
                {
                    yield return entry;
                }

                yield return new RestArgument(args[(argIndex + 1)..]);
                yield break;
            }
            else if (arg.StartsWith("--"))
            {
                if (tryPushPending(null, out ArgumentToken? entry))
                {
                    yield return entry;
                }

                state = new ParserState.PendingKeyValuePair(arg[2..]);
            }
            else if (arg.StartsWith('-'))
            {
                foreach (char argChar in arg[1..])
                {
                    if (tryPushPending(null, out ArgumentToken? entry))
                    {
                        yield return entry;
                    }

                    state = new ParserState.PendingShorthandKeyValuePair(argChar);
                }
            }
            else if (tryPushPending(arg, out ArgumentToken? entry))
            {
                yield return entry;
            }
            else
            {
                yield return new OrdinalValue(arg);
            }
        }
    }
}
