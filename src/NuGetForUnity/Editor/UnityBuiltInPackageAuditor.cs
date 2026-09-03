using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NugetForUnity.Models;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    ///     Audits the <c>packages.config</c> and the installed packages against the packages built into Unity
    ///     (see <see cref="UnityBuiltInPackageOverrides" />). The audit never changes the project, packages are only removed
    ///     when the user uninstalls them using NuGetForUnity.
    /// </summary>
    internal static class UnityBuiltInPackageAuditor
    {
        /// <summary>
        ///     Audits the packages of the <c>packages.config</c> and the dependencies of all installed packages.
        /// </summary>
        /// <returns>The report.</returns>
        [NotNull]
        internal static UnityBuiltInPackageAuditReport Audit()
        {
            return Audit(InstalledPackagesManager.PackagesConfigFile.Packages, InstalledPackagesManager.InstalledPackages);
        }

        /// <summary>
        ///     Audits the given packages and the dependencies of the given installed packages.
        /// </summary>
        /// <param name="configuredPackages">The packages listed inside the <c>packages.config</c>.</param>
        /// <param name="installedPackages">The installed packages of which the dependencies (for the current target framework) are checked.</param>
        /// <returns>The report.</returns>
        [NotNull]
        internal static UnityBuiltInPackageAuditReport Audit(
            [NotNull] [ItemNotNull] IEnumerable<INugetPackageIdentifier> configuredPackages,
            [NotNull] [ItemNotNull] IEnumerable<INugetPackage> installedPackages)
        {
            var entries = new List<UnityBuiltInPackageAuditEntry>();
            var state = UnityBuiltInPackageOverrides.State;
            var source = UnityBuiltInPackageOverrides.Source;
            if (state != UnityBuiltInPackageOverridesState.Available)
            {
                return new UnityBuiltInPackageAuditReport(state, source, entries);
            }

            foreach (var configuredPackage in configuredPackages)
            {
                var checkResult = UnityBuiltInPackageOverrides.Check(configuredPackage, null);
                switch (checkResult.Compatibility)
                {
                    case UnityBuiltInPackageCompatibility.Satisfied:
                        entries.Add(
                            new UnityBuiltInPackageAuditEntry(
                                UnityBuiltInPackageAuditEntryKind.RedundantConfiguredPackage,
                                configuredPackage,
                                null,
                                checkResult));
                        break;
                    case UnityBuiltInPackageCompatibility.Incompatible:
                        entries.Add(
                            new UnityBuiltInPackageAuditEntry(
                                UnityBuiltInPackageAuditEntryKind.IncompatibleConfiguredPackage,
                                configuredPackage,
                                null,
                                checkResult));
                        break;
                }
            }

            foreach (var installedPackage in installedPackages)
            {
                foreach (var dependency in installedPackage.CurrentFrameworkDependencies)
                {
                    var checkResult = UnityBuiltInPackageOverrides.Check(dependency, installedPackage);
                    switch (checkResult.Compatibility)
                    {
                        case UnityBuiltInPackageCompatibility.Satisfied:
                            entries.Add(
                                new UnityBuiltInPackageAuditEntry(
                                    UnityBuiltInPackageAuditEntryKind.SatisfiedDependency,
                                    dependency,
                                    installedPackage,
                                    checkResult));
                            break;
                        case UnityBuiltInPackageCompatibility.Incompatible:
                            entries.Add(
                                new UnityBuiltInPackageAuditEntry(
                                    UnityBuiltInPackageAuditEntryKind.IncompatibleDependency,
                                    dependency,
                                    installedPackage,
                                    checkResult));
                            break;
                    }
                }
            }

            return new UnityBuiltInPackageAuditReport(state, source, entries);
        }

        /// <summary>
        ///     Writes the findings of the report to the log: incompatible findings as errors, redundant packages as warnings.
        ///     Nothing is logged if there is no finding, so it can be called after every restore.
        /// </summary>
        /// <param name="report">The report to log.</param>
        internal static void LogFindings([NotNull] UnityBuiltInPackageAuditReport report)
        {
            if (report.State == UnityBuiltInPackageOverridesState.Unavailable)
            {
                Debug.LogError(report.ToText(false));
                return;
            }

            if (report.HasIncompatibleEntries)
            {
                Debug.LogError(report.ToText(false));
            }
            else if (report.HasRedundantEntries)
            {
                Debug.LogWarning(report.ToText(false));
            }
            else
            {
                NugetLogger.LogVerbose("{0}", report.ToText(true));
            }
        }
    }
}
