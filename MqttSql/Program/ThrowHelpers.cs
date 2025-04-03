/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Linq;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.RuntimeInformation;

namespace MqttSql.Program;

public static class ThrowHelpers
{
    public static CommandBox ThrowIfCommand(string command) => new(command);

    public static void IsUnsuportedOnPlatforms(this CommandBox commandBox, params OSPlatform[] platforms)
    {
        string command = commandBox.Command;

        if (Array.Exists(platforms, IsOSPlatform))
            throw new NotSupportedException($"{command} command not supported on the {PrettyJoin(platforms)} platform{(platforms.Length > 1 ? "s" : "")}");
    }

    public static void IsOnlySuportedOnPlatforms(this CommandBox commandBox, params OSPlatform[] platforms)
    {
        string command = commandBox.Command;

        if (!Array.Exists(platforms, IsOSPlatform))
            throw new NotSupportedException($"{command} command only supported on the {PrettyJoin(platforms)} platform{(platforms.Length > 1 ? "s" : "")}");
    }

    public static CommandAndCommandsBox IsUsedWithCommands(this CommandBox commandBox, params string[] commands) => new(commandBox.Command, commands);

    public static void InArgs(this CommandAndCommandsBox commandAndCommandsBox, string[] args)
    {
        string command = commandAndCommandsBox.Command;
        string[] commands = commandAndCommandsBox.Commands;

        if (args.Contains(command) && args.ContainsAny(commands))
            throw new ArgumentException($"Can't use \"{command}\" with {PrettyJoin(commands, lastSeparator: " or ", empty: "any", quoted: true)} commands");
    }

    public static void InArgs(this CommandAndCommandsBox commandAndCommandsBox, CommandLineArgs args)
    {
        string command = commandAndCommandsBox.Command;
        string[] commands = commandAndCommandsBox.Commands;

        if (args.ContainsSubcommand(command) && args.ContainsAnySubcommand(commands))
            throw new ArgumentException($"Can't use \"{command}\" with {PrettyJoin(commands, lastSeparator: " or ", empty: "any", quoted: true)} commands");
    }

    private static string PrettyJoin(string[] strings, string separator = ", ", string lastSeparator = " and ", string empty = "none", bool quoted = false)
    {
        string quote = quoted ? "\"" : "";

        return strings.Length switch
        {
            0 => empty,
            1 => $"{quote}{strings[0]}{quote}",
            _ => quote + string.Join($"{quote}{separator}{quote}", strings[..^1]) + $"{quote}{lastSeparator}{quote}{strings[^1]}{quote}"
        };
    }

    private static string PrettyJoin(OSPlatform[] platforms) => PrettyJoin([.. platforms.Select(p => p.ToString())], ", ", " and ", "no", false);

    public sealed class CommandBox(string command)
    {
        public string Command { get; } = command;
    }

    public sealed class CommandAndCommandsBox(string command, string[] commands)
    {
        public string Command { get; } = command;
        public string[] Commands { get; } = commands;
    }
}
