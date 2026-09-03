using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using NugetForUnity.Models;
using UnityEngine;
#if !NUGETFORUNITY_CLI
using UnityEditor;
#endif

namespace NugetForUnity
{
    /// <summary>
    ///     Resolves the NuGet packages that are built into the Unity Editor as part of the Base Class Library extensions (Unity 6.5 or newer)
    ///     and checks whether the built-in version satisfies the version range requested by a NuGet package.
    ///     <para>
    ///         Since Unity 6.5 the Editor ships assemblies like <c>System.Text.Json</c> or <c>System.Collections.Immutable</c> inside
    ///         <c>{EditorApplication.applicationContentsPath}/BCLExtensions</c>. Unity always resolves to its built-in version and drops
    ///         any other version of the same assembly from the build (see <c>UUM-139823</c>). Therefore we can't install a different version
    ///         from NuGet. The list of built-in packages and versions is read from the <c>PackageOverrides.txt</c> of the
    ///         targeting pack, the same file the .NET SDK uses to drop package references that are satisfied by a framework.
    ///     </para>
    ///     <para>
    ///         The location and format of the file are Unity internals, so the detection needs to be re-validated after a Unity Editor update.
    ///         The results are cached for the lifetime of the domain, call <see cref="Reset" /> to re-read the information.
    ///     </para>
    /// </summary>
    internal static class UnityBuiltInPackageOverrides
    {
        /// <summary>
        ///     The name of the environment variable that can be used to specify the Unity Editor contents path
        ///     (the directory containing <c>BCLExtensions</c>) when running outside of Unity (e.g. in the CLI).
        /// </summary>
        internal const string ContentsPathEnvironmentVariable = "NUGETFORUNITY_UNITY_CONTENTS_PATH";

        /// <summary>
        ///     The path of the file listing the built-in packages, relative to the Unity Editor contents path.
        ///     Unity 6.5 ships a <c>netstandard2.1</c> and a <c>net8.0</c> targeting pack, the Mono based Editor and Players use the
        ///     <c>netstandard2.1</c> pack, which is also a superset of the <c>net8.0</c> pack.
        /// </summary>
        internal const string OverridesFileRelativePath = "BCLExtensions/TargetingPacks/netstandard2.1/data/PackageOverrides.txt";

        /// <summary>
        ///     The first Unity version that ships the Base Class Library extensions.
        /// </summary>
        private static readonly UnityVersion FirstUnityVersionWithBuiltInPackages = new UnityVersion(6000, 5, 0, 'a', 0);

        [CanBeNull]
        private static Snapshot snapshot;

        /// <summary>
        ///     Gets the availability of the information about the built-in packages.
        /// </summary>
        internal static UnityBuiltInPackageOverridesState State => GetSnapshot().State;

        /// <summary>
        ///     Gets a description of the source of the information about the built-in packages (the file path or the reason it is missing).
        /// </summary>
        [NotNull]
        internal static string Source => GetSnapshot().Source;

        /// <summary>
        ///     Gets the packages built into Unity keyed by the package id (case insensitive). Empty if the information is not available.
        /// </summary>
        [NotNull]
        internal static IReadOnlyDictionary<string, NugetPackageVersion> BuiltInPackages => GetSnapshot().Packages;

        /// <summary>
        ///     Gets the version of the package with the given id that is built into Unity.
        /// </summary>
        /// <param name="packageId">The id of the NuGet package.</param>
        /// <param name="builtInVersion">The version built into Unity.</param>
        /// <returns>True if Unity provides the package.</returns>
        internal static bool TryGetBuiltInVersion([NotNull] string packageId, out NugetPackageVersion builtInVersion)
        {
            return GetSnapshot().Packages.TryGetValue(packageId, out builtInVersion);
        }

        /// <summary>
        ///     Checks whether the requested package is built into Unity and if so whether the built-in version satisfies the requested version
        ///     range. A single version (e.g. <c>8.0.0</c>) is treated as the minimum version like NuGet does for dependencies.
        /// </summary>
        /// <param name="package">The requested package (id and version or version range).</param>
        /// <param name="requestedBy">The package that depends on the requested package or <c>null</c> if it is requested directly by the project.</param>
        /// <returns>The result of the check.</returns>
        internal static UnityBuiltInPackageCheckResult Check([NotNull] INugetPackageIdentifier package, [CanBeNull] INugetPackageIdentifier requestedBy)
        {
            var currentSnapshot = GetSnapshot();
            switch (currentSnapshot.State)
            {
                case UnityBuiltInPackageOverridesState.NotApplicable:
                case UnityBuiltInPackageOverridesState.Unknown:
                    return new UnityBuiltInPackageCheckResult(UnityBuiltInPackageCompatibility.NotBuiltIn, null, string.Empty);
                case UnityBuiltInPackageOverridesState.Unavailable:
                    return new UnityBuiltInPackageCheckResult(
                        UnityBuiltInPackageCompatibility.Unverifiable,
                        null,
                        $"Can't verify whether '{package.Id}' {package.Version} (required by {DescribeRequester(requestedBy)}) is compatible with the packages built into Unity {Application.unityVersion}: {currentSnapshot.Source}. NuGetForUnity doesn't assume compatibility. Check the Unity installation and use 'NuGet -> Audit Unity Built-in Packages' after fixing the issue.");
                case UnityBuiltInPackageOverridesState.Available:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown state: {currentSnapshot.State}");
            }

            if (!currentSnapshot.Packages.TryGetValue(package.Id, out var builtInVersion))
            {
                return new UnityBuiltInPackageCheckResult(UnityBuiltInPackageCompatibility.NotBuiltIn, null, string.Empty);
            }

            if (package.PackageVersion.InRange(builtInVersion))
            {
                return new UnityBuiltInPackageCheckResult(
                    UnityBuiltInPackageCompatibility.Satisfied,
                    builtInVersion,
                    $"'{package.Id}' {package.Version} (required by {DescribeRequester(requestedBy)}) is satisfied by the package built into Unity {Application.unityVersion}: '{package.Id}' {builtInVersion}. The package is not installed from NuGet.");
            }

            return new UnityBuiltInPackageCheckResult(
                UnityBuiltInPackageCompatibility.Incompatible,
                builtInVersion,
                $"Unity {Application.unityVersion} provides the built-in package '{package.Id}' {builtInVersion} (listed in '{currentSnapshot.Source}'), but {DescribeRequester(requestedBy)} requires '{package.Id}' {package.Version}. Unity always resolves to its built-in assembly and doesn't allow another version of the same assembly, so NuGetForUnity refuses to install '{package.Id}' {package.Version}. Use a version of the requesting package that is compatible with '{package.Id}' {builtInVersion}, e.g. by installing compatible dependency versions explicitly before installing the requesting package.");
        }

        /// <summary>
        ///     Parses the content of a <c>PackageOverrides.txt</c> file. Each non empty line has the format <c>{PackageId}|{Version}</c>.
        /// </summary>
        /// <param name="lines">The lines of the file.</param>
        /// <param name="invalidLines">Receives the lines that couldn't be parsed, can be <c>null</c>.</param>
        /// <returns>The built-in packages keyed by the package id (case insensitive).</returns>
        [NotNull]
        internal static Dictionary<string, NugetPackageVersion> Parse([NotNull] IEnumerable<string> lines, [CanBeNull] ICollection<string> invalidLines = null)
        {
            var packages = new Dictionary<string, NugetPackageVersion>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in lines)
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('|');
                var packageId = separatorIndex > 0 ? line.Substring(0, separatorIndex).Trim() : string.Empty;
                var versionString = separatorIndex > 0 ? line.Substring(separatorIndex + 1).Trim() : string.Empty;
                NugetPackageVersion version = null;
                if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(versionString))
                {
                    try
                    {
                        version = new NugetPackageVersion(versionString);
                    }
                    catch (Exception)
                    {
                        version = null;
                    }
                }

                if (version == null || version.HasVersionRange || string.IsNullOrEmpty(version.NormalizedVersion))
                {
                    invalidLines?.Add(rawLine);
                    continue;
                }

                packages[packageId] = version;
            }

            return packages;
        }

        /// <summary>
        ///     Clears the cached information so it is re-read from the Unity installation the next time it is accessed.
        /// </summary>
        internal static void Reset()
        {
            snapshot = null;
        }

        /// <summary>
        ///     Replaces the detected information, only intended for unit tests. Call <see cref="Reset" /> to return to the normal detection.
        /// </summary>
        /// <param name="state">The state to simulate.</param>
        /// <param name="overridesFileContent">The content of a <c>PackageOverrides.txt</c> file, only used if <paramref name="state" /> is <see cref="UnityBuiltInPackageOverridesState.Available" />.</param>
        /// <param name="source">The description of the source.</param>
        internal static void OverrideForTesting(
            UnityBuiltInPackageOverridesState state,
            [CanBeNull] string overridesFileContent = null,
            [CanBeNull] string source = null)
        {
            var packages = state == UnityBuiltInPackageOverridesState.Available ?
                Parse(SplitLines(overridesFileContent ?? string.Empty)) :
                new Dictionary<string, NugetPackageVersion>(StringComparer.OrdinalIgnoreCase);
            snapshot = new Snapshot(state, source ?? $"test override ({state})", packages);
        }

        [NotNull]
        private static Snapshot GetSnapshot()
        {
            if (snapshot == null)
            {
                snapshot = Detect();
            }

            return snapshot;
        }

        [NotNull]
        private static Snapshot Detect()
        {
            var noPackages = new Dictionary<string, NugetPackageVersion>(StringComparer.OrdinalIgnoreCase);
            var unityShipsBuiltInPackages = UnityVersion.Current >= FirstUnityVersionWithBuiltInPackages;
            var contentsPath = GetUnityContentsPath();
            if (string.IsNullOrEmpty(contentsPath))
            {
                if (!unityShipsBuiltInPackages)
                {
                    return new Snapshot(
                        UnityBuiltInPackageOverridesState.NotApplicable,
                        $"Unity {Application.unityVersion} doesn't ship Base Class Library extensions.",
                        noPackages);
                }

                var unknownSource =
                    $"The Unity Editor installation path is unknown, so the packages built into Unity {Application.unityVersion} can't be verified. Set the environment variable '{ContentsPathEnvironmentVariable}' to the Unity Editor contents directory (the directory containing 'BCLExtensions') to enable the check.";
                Debug.LogWarning(unknownSource);
                return new Snapshot(UnityBuiltInPackageOverridesState.Unknown, unknownSource, noPackages);
            }

            var overridesFilePath = Path.GetFullPath(Path.Combine(contentsPath, OverridesFileRelativePath));
            if (!File.Exists(overridesFilePath))
            {
                if (!unityShipsBuiltInPackages)
                {
                    return new Snapshot(
                        UnityBuiltInPackageOverridesState.NotApplicable,
                        $"Unity {Application.unityVersion} doesn't ship Base Class Library extensions ('{overridesFilePath}' doesn't exist).",
                        noPackages);
                }

                return new Snapshot(
                    UnityBuiltInPackageOverridesState.Unavailable,
                    $"Unity {Application.unityVersion} is expected to ship Base Class Library extensions but the file '{overridesFilePath}' doesn't exist.",
                    noPackages);
            }

            try
            {
                var invalidLines = new List<string>();
                var packages = Parse(File.ReadAllLines(overridesFilePath), invalidLines);
                if (invalidLines.Count > 0)
                {
                    Debug.LogWarningFormat(
                        "Ignored {0} line(s) of '{1}' because they don't have the format 'PackageId|Version': {2}",
                        invalidLines.Count,
                        overridesFilePath,
                        string.Join(" ; ", invalidLines));
                }

                if (packages.Count == 0)
                {
                    return new Snapshot(
                        UnityBuiltInPackageOverridesState.Unavailable,
                        $"The file '{overridesFilePath}' doesn't contain any entry with the format 'PackageId|Version'.",
                        noPackages);
                }

                NugetLogger.LogVerbose(
                    "Packages built into Unity {0} (from '{1}'): {2}",
                    Application.unityVersion,
                    overridesFilePath,
                    string.Join(", ", packages.Select(package => $"{package.Key} {package.Value}")));
                return new Snapshot(UnityBuiltInPackageOverridesState.Available, overridesFilePath, packages);
            }
            catch (Exception exception)
            {
                return new Snapshot(
                    UnityBuiltInPackageOverridesState.Unavailable,
                    $"Failed to read the file '{overridesFilePath}': {exception.Message}",
                    noPackages);
            }
        }

        [CanBeNull]
        private static string GetUnityContentsPath()
        {
#if NUGETFORUNITY_CLI
            return Environment.GetEnvironmentVariable(ContentsPathEnvironmentVariable);
#else
            return EditorApplication.applicationContentsPath;
#endif
        }

        [NotNull]
        private static string DescribeRequester([CanBeNull] INugetPackageIdentifier requestedBy)
        {
            return requestedBy == null ? "the project" : $"the package '{requestedBy.Id}' {requestedBy.Version}";
        }

        [NotNull]
        private static IEnumerable<string> SplitLines([NotNull] string content)
        {
            return content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        private sealed class Snapshot
        {
            public Snapshot(
                UnityBuiltInPackageOverridesState state,
                [NotNull] string source,
                [NotNull] Dictionary<string, NugetPackageVersion> packages)
            {
                State = state;
                Source = source;
                Packages = packages;
            }

            public UnityBuiltInPackageOverridesState State { get; }

            [NotNull]
            public string Source { get; }

            [NotNull]
            public IReadOnlyDictionary<string, NugetPackageVersion> Packages { get; }
        }
    }
}
