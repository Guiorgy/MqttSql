using System.Runtime.CompilerServices;
using VerifyMSTest;
using VerifyTests;

namespace Tests.Source_Generators;

public abstract class VerifyBaseWithDefaultSettings : VerifyBase
{
    [ModuleInitializer]
    public static void Initialize() => VerifyDiffPlex.Initialize();

    protected static VerifySettings GetDefaultVerifySettings()
    {
        VerifySettings settings = new();
        settings.UseDiffPlex();
        settings.UseDirectory("VerifySnapshots");
        return settings;
    }

    protected static readonly VerifySettings DefaultVerifySettings = GetDefaultVerifySettings();

    public SettingsTask Verify(string? target, VerifySettings? settings = null) => base.Verify(target, settings ?? DefaultVerifySettings);
}
