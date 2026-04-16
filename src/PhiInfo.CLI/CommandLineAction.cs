using System;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace PhiInfo.CLI;

internal sealed class CommandLineAction : SynchronousCommandLineAction
{
    private readonly Func<ParseResult, int> _syncAction;

    internal CommandLineAction(Func<ParseResult, int> action)
    {
        _syncAction = action;
    }

    public override int Invoke(ParseResult parseResult)
    {
        return _syncAction(parseResult);
    }
}