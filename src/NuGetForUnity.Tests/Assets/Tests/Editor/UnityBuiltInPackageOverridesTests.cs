using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NugetForUnity;
using NugetForUnity.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
///     Tests for the handling of NuGet packages that are built into the Unity Editor (Base Class Library extensions of Unity 6.5+).
///     The tests simulate the <c>PackageOverrides.txt</c> of Unity 6000.5.5f1 so they run on every Unity version.
/// </summary>
public class UnityBuiltInPackageOverridesTests
{
    /// <summary>
    ///     Content of 'BCLExtensions/TargetingPacks/netstandard2.1/data/PackageOverrides.txt' of Unity 6000.5.5f1.
    /// </summary>
    private const string Unity6000505Overrides = @"Microsoft.Bcl.AsyncInterfaces|8.0.0
Microsoft.Extensions.DependencyInjection.Abstractions|8.0.2
Microsoft.Extensions.Logging.Abstractions|8.0.3
System.Buffers|4.5.1
System.Collections.Immutable|8.0.0
System.Diagnostics.DiagnosticSource|8.0.1
System.IO.Hashing|8.0.0
System.Memory|4.5.5
System.Numerics.Vectors|4.4.0
System.Reflection.Metadata|8.0.1
System.Runtime.CompilerServices.Unsafe|6.1.2
System.Text.Encodings.Web|8.0.0
System.Text.Json|8.0.6
System.Threading.Tasks.Extensions|4.5.4
";

    [TearDown]
    public void Cleanup()
    {
        UnityBuiltInPackageOverrides.Reset();
        NugetPackageUninstaller.UninstallAll(InstalledPackagesManager.InstalledPackages.ToList());
        foreach (var configuredPackage in InstalledPackagesManager.PackagesConfigFile.Packages.ToList())
        {
            InstalledPackagesManager.PackagesConfigFile.RemovePackage(configuredPackage);
        }

        InstalledPackagesManager.PackagesConfigFile.Save();
    }

    [Test]
    public void ParseIgnoresEmptyAndInvalidLines()
    {
        var invalidLines = new List<string>();
        var packages = UnityBuiltInPackageOverrides.Parse(
            new[] { string.Empty, "  ", "# comment", "System.Text.Json|8.0.6", " System.Memory | 4.5.5 ", "NoSeparator", "|1.0.0", "NoVersion|", "Range|[1.0,2.0)" },
            invalidLines);

        Assert.That(packages.Count, Is.EqualTo(2));
        Assert.That(packages["system.text.json"].ToString(), Is.EqualTo("8.0.6"));
        Assert.That(packages["System.Memory"].ToString(), Is.EqualTo("4.5.5"));
        Assert.That(invalidLines, Is.EquivalentTo(new[] { "NoSeparator", "|1.0.0", "NoVersion|", "Range|[1.0,2.0)" }));
    }

    [Test]
    public void ParseReadsUnity6000505Overrides()
    {
        var packages = UnityBuiltInPackageOverrides.Parse(Unity6000505Overrides.Split('\n'));

        Assert.That(packages.Count, Is.EqualTo(14));
        Assert.That(packages["System.Text.Json"].ToString(), Is.EqualTo("8.0.6"));
        Assert.That(packages["Microsoft.Bcl.AsyncInterfaces"].ToString(), Is.EqualTo("8.0.0"));
    }

    [Test]
    [TestCase("Microsoft.Bcl.AsyncInterfaces", "8.0.0", UnityBuiltInPackageCompatibility.Satisfied)]
    [TestCase("Microsoft.Bcl.AsyncInterfaces", "6.0.0", UnityBuiltInPackageCompatibility.Satisfied)]
    [TestCase("Microsoft.Bcl.AsyncInterfaces", "[6.0.0,9.0.0)", UnityBuiltInPackageCompatibility.Satisfied)]
    [TestCase("Microsoft.Bcl.AsyncInterfaces", "10.0.0", UnityBuiltInPackageCompatibility.Incompatible)]
    [TestCase("Microsoft.Bcl.AsyncInterfaces", "[8.0.1,)", UnityBuiltInPackageCompatibility.Incompatible)]
    [TestCase("System.Text.Json", "10.0.9", UnityBuiltInPackageCompatibility.Incompatible)]
    [TestCase("System.Text.Json", "8.0.6", UnityBuiltInPackageCompatibility.Satisfied)]
    [TestCase("System.Text.Json", "8.0.0", UnityBuiltInPackageCompatibility.Satisfied)]
    [TestCase("System.Text.Json", "[8.0.0]", UnityBuiltInPackageCompatibility.Incompatible)]
    [TestCase("system.text.json", "8.0.6", UnityBuiltInPackageCompatibility.Satisfied)]
    [TestCase("System.Runtime.CompilerServices.Unsafe", "6.0.0", UnityBuiltInPackageCompatibility.Satisfied)]
    [TestCase("Newtonsoft.Json", "13.0.1", UnityBuiltInPackageCompatibility.NotBuiltIn)]
    public void CheckComparesRequestedRangeWithBuiltInVersion(string packageId, string version, UnityBuiltInPackageCompatibility expected)
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);

        var result = UnityBuiltInPackageOverrides.Check(new NugetPackageIdentifier(packageId, version), null);

        Assert.That(result.Compatibility, Is.EqualTo(expected));
        if (expected == UnityBuiltInPackageCompatibility.NotBuiltIn)
        {
            Assert.That(result.BuiltInVersion, Is.Null);
        }
        else
        {
            Assert.That(result.BuiltInVersion, Is.Not.Null);
            Assert.That(result.Message, Does.Contain(packageId).IgnoreCase);
            Assert.That(result.Message, Does.Contain(version));
        }
    }

    [Test]
    public void CheckIncludesRequestingPackageInMessage()
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);

        var result = UnityBuiltInPackageOverrides.Check(
            new NugetPackageIdentifier("Microsoft.Bcl.AsyncInterfaces", "10.0.0"),
            new NugetPackageIdentifier("Microsoft.Bcl.TimeProvider", "10.0.0"));

        Assert.That(result.Compatibility, Is.EqualTo(UnityBuiltInPackageCompatibility.Incompatible));
        Assert.That(result.Message, Does.Contain("Microsoft.Bcl.TimeProvider' 10.0.0"));
        Assert.That(result.Message, Does.Contain("8.0.0"));
    }

    [Test]
    [TestCase(UnityBuiltInPackageOverridesState.NotApplicable)]
    [TestCase(UnityBuiltInPackageOverridesState.Unknown)]
    public void CheckKeepsLegacyBehaviorWhenUnityHasNoBuiltInPackages(UnityBuiltInPackageOverridesState state)
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(state);

        var result = UnityBuiltInPackageOverrides.Check(new NugetPackageIdentifier("System.Text.Json", "10.0.9"), null);

        Assert.That(result.Compatibility, Is.EqualTo(UnityBuiltInPackageCompatibility.NotBuiltIn));
    }

    [Test]
    public void CheckDoesNotAssumeCompatibilityWhenOverridesAreUnavailable()
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Unavailable, null, "PackageOverrides.txt is missing");

        var result = UnityBuiltInPackageOverrides.Check(new NugetPackageIdentifier("System.Text.Json", "8.0.0"), null);

        Assert.That(result.Compatibility, Is.EqualTo(UnityBuiltInPackageCompatibility.Unverifiable));
        Assert.That(result.IsError, Is.True);
        Assert.That(result.Message, Does.Contain("PackageOverrides.txt is missing"));
    }

    [Test]
    public void InstallSkipsPackageProvidedByUnity([Values] bool slimRestore)
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);
        var package = new NugetPackageIdentifier("Microsoft.Bcl.AsyncInterfaces", "8.0.0") { IsManuallyInstalled = true };

        var installed = NugetPackageInstaller.InstallIdentifier(package, false, slimRestore);

        Assert.That(installed, Is.True);
        Assert.That(InstalledPackagesManager.IsInstalled(package, false), Is.False, "The package provided by Unity must not be installed.");
        Assert.That(InstalledPackagesManager.PackagesConfigFile.Packages, Is.Empty);
    }

    [Test]
    public void InstallRejectsPackageIncompatibleWithUnityWithoutDownloading([Values] bool slimRestore)
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);
        var package = new NugetPackageIdentifier("System.Text.Json", "10.0.9") { IsManuallyInstalled = true };
        var cachedPackagePath = Path.Combine(PackageCacheManager.CacheOutputDirectory, package.PackageFileName);
        if (File.Exists(cachedPackagePath))
        {
            File.Delete(cachedPackagePath);
        }

        LogAssert.Expect(LogType.Error, new Regex("System\\.Text\\.Json.*8\\.0\\.6.*10\\.0\\.9"));
        var installed = NugetPackageInstaller.InstallIdentifier(package, false, slimRestore);

        Assert.That(installed, Is.False);
        Assert.That(InstalledPackagesManager.IsInstalled(package, false), Is.False);
        Assert.That(File.Exists(cachedPackagePath), Is.False, "The incompatible package must not be downloaded.");
        Assert.That(InstalledPackagesManager.PackagesConfigFile.Packages, Is.Empty);
    }

    [Test]
    public void InstallRejectsPackageWhenOverridesAreUnavailable()
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Unavailable, null, "PackageOverrides.txt is missing");
        var package = new NugetPackageIdentifier("Newtonsoft.Json", "13.0.1") { IsManuallyInstalled = true };

        LogAssert.Expect(LogType.Error, new Regex("Can't verify.*Newtonsoft\\.Json.*PackageOverrides\\.txt is missing"));
        var installed = NugetPackageInstaller.InstallIdentifier(package, false);

        Assert.That(installed, Is.False);
        Assert.That(InstalledPackagesManager.IsInstalled(package, false), Is.False);
    }

    [Test]
    public void InstallFailsWhenDependencyIsIncompatibleWithUnity()
    {
        // Microsoft.Bcl.TimeProvider 8.0.0 depends on Microsoft.Bcl.AsyncInterfaces >= 6.0.0, simulate a Unity that only provides 5.0.0.
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, "Microsoft.Bcl.AsyncInterfaces|5.0.0");
        var package = new NugetPackageIdentifier("Microsoft.Bcl.TimeProvider", "8.0.0") { IsManuallyInstalled = true };

        LogAssert.Expect(LogType.Error, new Regex("Microsoft\\.Bcl\\.AsyncInterfaces.*5\\.0\\.0.*Microsoft\\.Bcl\\.TimeProvider' 8\\.0\\.0"));
        LogAssert.Expect(LogType.Error, new Regex("Unable to install package Microsoft\\.Bcl\\.TimeProvider 8\\.0\\.0"));
        var installed = NugetPackageInstaller.InstallIdentifier(package, false);

        Assert.That(installed, Is.False);
        Assert.That(InstalledPackagesManager.IsInstalled("Microsoft.Bcl.AsyncInterfaces", false), Is.False);
    }

    [Test]
    public void InstallR3WithDependenciesProvidedByUnity()
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);

        // pin the verified dependency versions first so the dependency resolution keeps them
        var timeProvider = new NugetPackageIdentifier("Microsoft.Bcl.TimeProvider", "8.0.0") { IsManuallyInstalled = true };
        var channels = new NugetPackageIdentifier("System.Threading.Channels", "8.0.0") { IsManuallyInstalled = true };
        var annotations = new NugetPackageIdentifier("System.ComponentModel.Annotations", "5.0.0") { IsManuallyInstalled = true };
        Assert.That(NugetPackageInstaller.InstallIdentifier(timeProvider, false), Is.True);
        Assert.That(NugetPackageInstaller.InstallIdentifier(channels, false), Is.True);
        Assert.That(NugetPackageInstaller.InstallIdentifier(annotations, false), Is.True);

        var r3 = new NugetPackageIdentifier("R3", "1.3.1") { IsManuallyInstalled = true };
        Assert.That(NugetPackageInstaller.InstallIdentifier(r3, false), Is.True);

        Assert.That(InstalledPackagesManager.IsInstalled(r3, false), Is.True);
        Assert.That(InstalledPackagesManager.IsInstalled(timeProvider, false), Is.True);
        Assert.That(InstalledPackagesManager.IsInstalled(channels, false), Is.True);
        Assert.That(InstalledPackagesManager.IsInstalled(annotations, false), Is.True);

        var installedIds = InstalledPackagesManager.InstalledPackages.Select(package => package.Id).ToList();
        Assert.That(installedIds, Has.None.EqualTo("Microsoft.Bcl.AsyncInterfaces").IgnoreCase);
        Assert.That(installedIds, Has.None.EqualTo("System.Runtime.CompilerServices.Unsafe").IgnoreCase);
        Assert.That(installedIds, Has.None.EqualTo("System.Memory").IgnoreCase);
        Assert.That(installedIds, Has.None.EqualTo("System.Buffers").IgnoreCase);
        Assert.That(installedIds, Has.None.EqualTo("System.Threading.Tasks.Extensions").IgnoreCase);

        var report = UnityBuiltInPackageAuditor.Audit();
        Assert.That(report.HasIncompatibleEntries, Is.False, report.ToText(true));
        Assert.That(report.HasRedundantEntries, Is.False, report.ToText(true));
        Assert.That(
            report.Entries.Where(entry => entry.Kind == UnityBuiltInPackageAuditEntryKind.SatisfiedDependency).Select(entry => entry.Package.Id),
            Has.Some.EqualTo("Microsoft.Bcl.AsyncInterfaces").IgnoreCase);
    }

    [Test]
    public void SlimRestoreRejectsIncompatibleConfiguredPackage()
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);
        var package = new NugetPackageIdentifier("System.Text.Json", "10.0.9") { IsManuallyInstalled = true };
        InstalledPackagesManager.PackagesConfigFile.AddPackage(package);
        InstalledPackagesManager.PackagesConfigFile.Save();

        // one error from the rejected install and one from the audit of the packages.config
        LogAssert.Expect(LogType.Error, new Regex("System\\.Text\\.Json.*8\\.0\\.6.*10\\.0\\.9"));
        LogAssert.Expect(LogType.Error, new Regex("Incompatible packages inside packages.config"));
        PackageRestorer.Restore(true);

        Assert.That(InstalledPackagesManager.IsInstalled(package, false), Is.False);
    }

    [Test]
    public void SlimRestoreSkipsConfiguredPackageProvidedByUnityAndReportsItAsRedundant()
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);
        var package = new NugetPackageIdentifier("Microsoft.Bcl.AsyncInterfaces", "8.0.0") { IsManuallyInstalled = true };
        InstalledPackagesManager.PackagesConfigFile.AddPackage(package);
        InstalledPackagesManager.PackagesConfigFile.Save();

        LogAssert.Expect(LogType.Warning, new Regex("Redundant packages inside packages.config"));
        PackageRestorer.Restore(true);

        Assert.That(InstalledPackagesManager.IsInstalled(package, false), Is.False);
        Assert.That(InstalledPackagesManager.PackagesConfigFile.Packages.Count, Is.EqualTo(1), "The audit must not change the packages.config.");
    }

    [Test]
    public void AuditReportsConfiguredPackagesAndDependencies()
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);
        var configuredPackages = new List<INugetPackageIdentifier>
        {
            new NugetPackageIdentifier("System.Text.Json", "10.0.9"),
            new NugetPackageIdentifier("Microsoft.Bcl.AsyncInterfaces", "8.0.0"),
            new NugetPackageIdentifier("Newtonsoft.Json", "13.0.1"),
        };

        var report = UnityBuiltInPackageAuditor.Audit(configuredPackages, new List<INugetPackage>());

        Assert.That(report.State, Is.EqualTo(UnityBuiltInPackageOverridesState.Available));
        Assert.That(report.Entries.Count, Is.EqualTo(2));
        Assert.That(report.HasIncompatibleEntries, Is.True);
        Assert.That(report.HasRedundantEntries, Is.True);
        Assert.That(
            report.Entries.Single(entry => entry.Kind == UnityBuiltInPackageAuditEntryKind.IncompatibleConfiguredPackage).Package.Id,
            Is.EqualTo("System.Text.Json"));
        Assert.That(
            report.Entries.Single(entry => entry.Kind == UnityBuiltInPackageAuditEntryKind.RedundantConfiguredPackage).Package.Id,
            Is.EqualTo("Microsoft.Bcl.AsyncInterfaces"));
        var text = report.ToText(false);
        Assert.That(text, Does.Contain("System.Text.Json 10.0.9"));
        Assert.That(text, Does.Contain("built into Unity: 8.0.6"));
        Assert.That(text, Does.Contain("Microsoft.Bcl.AsyncInterfaces 8.0.0"));
        Assert.That(text, Does.Not.Contain("Newtonsoft.Json"));
    }

    [Test]
    public void AuditReportsIncompatibleDependencyOfInstalledPackage()
    {
        // install with the real Unity 6.5 information, then simulate a Unity that provides an older version than required
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, Unity6000505Overrides);
        var timeProvider = new NugetPackageIdentifier("Microsoft.Bcl.TimeProvider", "8.0.0") { IsManuallyInstalled = true };
        Assert.That(NugetPackageInstaller.InstallIdentifier(timeProvider, false), Is.True);

        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.Available, "Microsoft.Bcl.AsyncInterfaces|5.0.0");
        var report = UnityBuiltInPackageAuditor.Audit();

        Assert.That(report.HasIncompatibleEntries, Is.True, report.ToText(true));
        var entry = report.Entries.Single(auditEntry => auditEntry.Kind == UnityBuiltInPackageAuditEntryKind.IncompatibleDependency);
        Assert.That(entry.Package.Id, Is.EqualTo("Microsoft.Bcl.AsyncInterfaces"));
        Assert.That(entry.RequestedBy, Is.Not.Null);
        Assert.That(entry.RequestedBy.Id, Is.EqualTo("Microsoft.Bcl.TimeProvider"));
        Assert.That(entry.CheckResult.BuiltInVersion.ToString(), Is.EqualTo("5.0.0"));
        Assert.That(InstalledPackagesManager.IsInstalled(timeProvider, false), Is.True, "The audit must not uninstall packages.");
    }

    [Test]
    public void AuditIsEmptyWhenUnityHasNoBuiltInPackages()
    {
        UnityBuiltInPackageOverrides.OverrideForTesting(UnityBuiltInPackageOverridesState.NotApplicable);

        var report = UnityBuiltInPackageAuditor.Audit(
            new List<INugetPackageIdentifier> { new NugetPackageIdentifier("System.Text.Json", "10.0.9") },
            new List<INugetPackage>());

        Assert.That(report.State, Is.EqualTo(UnityBuiltInPackageOverridesState.NotApplicable));
        Assert.That(report.Entries, Is.Empty);
    }
}
