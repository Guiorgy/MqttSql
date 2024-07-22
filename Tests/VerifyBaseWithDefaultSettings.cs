using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using VerifyMSTest;
using VerifyTests;

namespace Tests;

public abstract class VerifyBaseWithDefaultSettings : VerifyBase
{
    [ModuleInitializer]
    public static void Initialize(){
        VerifyDiffPlex.Initialize();
        VerifierSettings.UniqueForAssemblyConfiguration();
    }

    protected static VerifySettings GetDefaultVerifySettings()
    {
        VerifySettings settings = new();
        settings.UseDiffPlex();
        settings.UseDirectory("VerifySnapshots");
        return settings;
    }

    protected static readonly VerifySettings DefaultVerifySettings = GetDefaultVerifySettings();

#pragma warning disable CA1822 // Mark members as static
    [Pure]
    public new SettingsTask Verify(VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(settings ?? DefaultVerifySettings, sourceFile);

    [Pure]
    public new SettingsTask Verify<T>(Func<Task<T>> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? DefaultVerifySettings, sourceFile);

    [Pure]
    public new SettingsTask Verify<T>(Task<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? DefaultVerifySettings, sourceFile);

    [Pure]
    public new SettingsTask Verify<T>(ValueTask<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? DefaultVerifySettings, sourceFile);

    [Pure]
    public new SettingsTask Verify<T>(IAsyncEnumerable<T> target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? DefaultVerifySettings, sourceFile);

    [Pure]
    public SettingsTask Verify<T>(T? target, VerifySettings? settings = null, [CallerFilePath] string sourceFile = "") => Verifier.Verify(target, settings ?? DefaultVerifySettings, sourceFile);
#pragma warning restore CA1822 // Mark members as static
}
