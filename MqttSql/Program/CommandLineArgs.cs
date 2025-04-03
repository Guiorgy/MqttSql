using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace MqttSql;

[DebuggerDisplay("CLI Args: {TopLevelArgsDebugString}; Subcommands: [{SubCommandsAndArgsDebugString}]")]
public sealed partial class CommandLineArgs
{
    private readonly CommandAndArgs[] _commandsAndArgs;

    [GeneratedRegex(@"^(?:-(\w)|--(\w[\w\-]*\w))(?:=(.+))?$")]
    private static partial Regex _ArgAndValueRegex();
    private static readonly Regex ArgAndValueRegex = _ArgAndValueRegex();

    public CommandLineArgs(string[] cliArgs, bool toLower = true)
    {
        List<CommandAndArgs> commandsAndArgs = new(cliArgs.Length);

        string command = "";
        List<ArgNameAndValue> args = new(cliArgs.Length);

        foreach (string arg in (toLower ? cliArgs.Select(a => a.ToLowerInvariant()) : cliArgs))
        {
            if (arg.StartsWith('-'))
            {
                args.Add(ParseArgValue(arg));
            }
            else
            {
                if (!(command.Length == 0 && args.Count == 0))
                    commandsAndArgs.Add((command, [.. args]));

                command = arg;
                args.Clear();
            }
        }
        commandsAndArgs.Add((command, [.. args]));

        _commandsAndArgs = [.. commandsAndArgs];

        static ArgNameAndValue ParseArgValue(string arg) => ArgAndValueRegex.Match(arg) switch
        {
            { Success: false } => throw new FormatException($"Argument \"{arg}\" is not in a correct format"),
            { Groups: [_, { Success: true } name, { Success: false }, { Success: false }] } => (name.Value, ""),
            { Groups: [_, { Success: false }, { Success: true } name, { Success: false }] } => (name.Value, ""),
            { Groups: [_, { Success: true } name, { Success: false }, { Success: true } value] } => (name.Value, value.Value),
            { Groups: [_, { Success: false }, { Success: true } name, { Success: true } value] } => (name.Value, value.Value),
            _ => throw new UnreachableException()
        };
    }

    public ReadOnlySpan<CommandAndArgs> CommandsAndArgs => _commandsAndArgs;

    public CommandAndArgs TopLevelArgs => (_commandsAndArgs.Length == 0 || _commandsAndArgs[0].Command.Length != 0) ? new("", []) : _commandsAndArgs[0];

    public ReadOnlySpan<CommandAndArgs> SubcommandsAndArgs => (_commandsAndArgs.Length != 0 && _commandsAndArgs[0].Command.Length == 0) ? _commandsAndArgs[1..] : _commandsAndArgs;

    public CommandAndArgs this[int index] => _commandsAndArgs[index];

    public CommandAndArgs this[string command] => _commandsAndArgs.First(caa => caa.Command == command);

    public CommandAndArgs SubcommandArgs(string command) => this[command];

    public bool ContainsSubcommand(string command) => Array.Exists(_commandsAndArgs, caa => caa.Command == command);

    public bool ContainsAnySubcommand(params string[] commands) => _commandsAndArgs.Select(caa => caa.Command).Intersect(commands).Any();

    public bool ContainsSubcommands(params string[] commands) => _commandsAndArgs.Select(caa => caa.Command).Intersect(commands).Count() == commands.Length;

#if DEBUG
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Only used for debugger display", true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Info Code Smell", "S1133:Deprecated code should be removed", Justification = $"The {nameof(ObsoleteAttribute)} is used as a hack to prevent a misuse")]
    public string TopLevelArgsDebugString => string.Join(", ", [.. TopLevelArgs.Args]);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Only used for debugger display", true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Info Code Smell", "S1133:Deprecated code should be removed", Justification = $"The {nameof(ObsoleteAttribute)} is used as a hack to prevent a misuse")]
    public string SubCommandsAndArgsDebugString => string.Join(", ", ((IEnumerable<CommandAndArgs>)[.. SubcommandsAndArgs]).Select(caa => $"{caa.Command}: [{string.Join(", ", [.. caa.Args])}]"));
#endif

    [DebuggerDisplay("{Command}: [{ArgsDebugString}]")]
    public sealed class CommandAndArgs(string command, ArgNameAndValue[] args)
    {
        public readonly string Command = command;
        public ReadOnlySpan<ArgNameAndValue> Args => args;

        public static implicit operator CommandAndArgs((string Command, ArgNameAndValue[] Args) tuple) => new(tuple.Command, tuple.Args);

        public ArgNameAndValue this[int index] => args[index];

        public ArgNameAndValue this[string name] => args.First(anav => anav.Name == name);

        public ArgNameAndValue? ArgValue(string name) => args.FirstOrDefault(anav => anav.Name == name);

        public ArgNameAndValue? ArgValue(string longName, char shortName) => ArgValue(longName) ?? ArgValue(shortName.ToString());

        public bool ContainsArg(string longName, char shortName) => ArgValue(longName, shortName) != null;

        public bool ContainsAnyArg(params string[] names) => args.Select(anav => anav.Name).Intersect(names).Any();

        public bool ContainsArgs(params string[] names) => args.Select(anav => anav.Name).Intersect(names).Count() == names.Length;

#if DEBUG
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Only used for debugger display", true)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Info Code Smell", "S1133:Deprecated code should be removed", Justification = $"The {nameof(ObsoleteAttribute)} is used as a hack to prevent a misuse")]
        public string ArgsDebugString => string.Join(", ", [.. args]);
#endif
    }

    [DebuggerDisplay("{Name}: {Value}")]
    public sealed class ArgNameAndValue(string name, string value)
    {
        public readonly string Name = name;
        public readonly string Value = value;
        public string RequiredValue => HasValue() ? Value : throw new ArgumentException($"Value expected for the {Name} argument");

        public static implicit operator ArgNameAndValue((string Name, string Value) tuple) => new(tuple.Name, tuple.Value);

        public static implicit operator ArgNameAndValue(string value) => new("", value);

        public bool HasValue() => Value.Length != 0;

        public override string ToString() => $"{Name}: {Value}";
    }
}
