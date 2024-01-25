using System;
using TableCloth.Models;

namespace TableCloth.Components;

public sealed class CommandLineArguments : ICommandLineArguments
{
    private readonly Lazy<CommandLineArgumentModel> _argvModelFactory
        = new(() => CommandLineArgumentModel.ParseFromArgv());

    public CommandLineArgumentModel Current
        => _argvModelFactory.Value;
}
