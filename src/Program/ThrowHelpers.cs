/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
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
            throw new NotSupportedException($"{command} command not supported on the current platform");
    }

    public static void IsOnlySuportedOnPlatforms(this CommandBox commandBox, params OSPlatform[] platforms)
    {
        string command = commandBox.Command;

        if (!Array.Exists(platforms, IsOSPlatform))
            throw new NotSupportedException($"{command} command not supported on the current platform");
    }

    public static CommandAndCommandsBox IsUsedWithCommands(this CommandBox commandBox, params string[] commands) => new(commandBox.Command, commands);

    public static void InArgs(this CommandAndCommandsBox commandAndCommandsBox, string[] args)
    {
        string command = commandAndCommandsBox.Command;
        string[] commands = commandAndCommandsBox.Commands;

        if (args.Contains(command) && args.ContainsAny(commands))
            throw new ArgumentException($"Can't use \"{command}\" with {CommandsToString(commands)}");

        static string CommandsToString(string[] commands) => commands.Length switch
        {
            0 => throw new UnreachableException(),
            1 => $"\"{commands[0]}\"",
            _ => '"' + string.Join("\", \"", commands[..^1]) + $"\" or \"{commands[^1]}\""
        };
    }

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
