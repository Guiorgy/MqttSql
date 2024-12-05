using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using VerifyMSTest;
using VerifyTests;

namespace Tests;

public abstract class VerifyBaseWithDefaultSettings(bool hidePasswords = false) : VerifyBase
{
    [ModuleInitializer]
    public static void Initialize() => VerifyDiffPlex.Initialize();

    protected static VerifySettings GetDefaultVerifySettings(bool hidePasswords)
    {
        VerifySettings settings = new();
        settings.UseDirectory("VerifySnapshots");
        settings.UseDiffPlex();
        if (hidePasswords) settings.ScrubLinesWithReplace(HidePassword);
        return settings;

        static string HidePassword(string source)
        {
            if (!source.Contains("Password: ")) return source;

            var splits = source.Split(": ", 2);
            return splits[0] + ": " + new string('*', splits[1].Length);
        }
    }

    protected readonly VerifySettings defaultVerifySettings = GetDefaultVerifySettings(hidePasswords);

    [Pure]
    public new SettingsTask Verify(VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(settings ?? defaultVerifySettings, sourceFile);

    [Pure]
    public new SettingsTask Verify<T>(Func<Task<T>> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? defaultVerifySettings, sourceFile);

    [Pure]
    public new SettingsTask Verify<T>(Task<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? defaultVerifySettings, sourceFile);

    [Pure]
    public new SettingsTask Verify<T>(ValueTask<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? defaultVerifySettings, sourceFile);

    [Pure]
    public new SettingsTask Verify<T>(IAsyncEnumerable<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? defaultVerifySettings, sourceFile);

    [Pure]
    public SettingsTask Verify<T>(T? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? defaultVerifySettings, sourceFile);
}
