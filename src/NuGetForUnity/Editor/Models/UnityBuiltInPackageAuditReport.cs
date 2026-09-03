using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     The result of <see cref="NugetForUnity.UnityBuiltInPackageAuditor.Audit()" />.
    /// </summary>
    internal sealed class UnityBuiltInPackageAuditReport
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UnityBuiltInPackageAuditReport" /> class.
        /// </summary>
        /// <param name="state">The availability of the information about the built-in packages.</param>
        /// <param name="source">The description of the source of the information about the built-in packages.</param>
        /// <param name="entries">The findings.</param>
        public UnityBuiltInPackageAuditReport(
            UnityBuiltInPackageOverridesState state,
            [NotNull] string source,
            [NotNull] [ItemNotNull] List<UnityBuiltInPackageAuditEntry> entries)
        {
            State = state;
            Source = source;
            Entries = entries;
        }

        /// <summary>
        ///     Gets the availability of the information about the built-in packages.
        /// </summary>
        public UnityBuiltInPackageOverridesState State { get; }

        /// <summary>
        ///     Gets the description of the source of the information about the built-in packages.
        /// </summary>
        [NotNull]
        public string Source { get; }

        /// <summary>
        ///     Gets the findings.
        /// </summary>
        [NotNull]
        [ItemNotNull]
        public List<UnityBuiltInPackageAuditEntry> Entries { get; }

        /// <summary>
        ///     Gets a value indicating whether the audit found a version range that can't be satisfied by the packages built into Unity.
        /// </summary>
        public bool HasIncompatibleEntries => Entries.Any(entry => entry.IsIncompatible);

        /// <summary>
        ///     Gets a value indicating whether the audit found a redundant package inside the <c>packages.config</c>.
        /// </summary>
        public bool HasRedundantEntries => Entries.Any(entry => entry.Kind == UnityBuiltInPackageAuditEntryKind.RedundantConfiguredPackage);

        /// <summary>
        ///     Creates a human readable summary of the report.
        /// </summary>
        /// <param name="includeSatisfiedDependencies">True to also list the dependencies that are satisfied by Unity.</param>
        /// <returns>The summary.</returns>
        [NotNull]
        public string ToText(bool includeSatisfiedDependencies)
        {
            var builder = new StringBuilder();
            builder.Append("Unity built-in package audit (Unity ").Append(Application.unityVersion).Append(", state: ").Append(State).AppendLine(")");
            builder.Append("Source: ").AppendLine(Source);
            if (State != UnityBuiltInPackageOverridesState.Available)
            {
                return builder.ToString();
            }

            AppendSection(
                builder,
                "Incompatible packages inside packages.config (Unity provides a version outside of the requested range, remove or downgrade the package):",
                Entries.Where(entry => entry.Kind == UnityBuiltInPackageAuditEntryKind.IncompatibleConfiguredPackage));
            AppendSection(
                builder,
                "Incompatible dependencies of installed packages (the installed package requires a version Unity doesn't provide):",
                Entries.Where(entry => entry.Kind == UnityBuiltInPackageAuditEntryKind.IncompatibleDependency));
            AppendSection(
                builder,
                "Redundant packages inside packages.config (Unity provides a compatible version, the NuGet package can be uninstalled using NuGetForUnity):",
                Entries.Where(entry => entry.Kind == UnityBuiltInPackageAuditEntryKind.RedundantConfiguredPackage));
            if (includeSatisfiedDependencies)
            {
                AppendSection(
                    builder,
                    "Dependencies satisfied by Unity (not installed from NuGet):",
                    Entries.Where(entry => entry.Kind == UnityBuiltInPackageAuditEntryKind.SatisfiedDependency));
            }

            if (!HasIncompatibleEntries && !HasRedundantEntries)
            {
                builder.AppendLine("No incompatible or redundant packages found.");
            }

            return builder.ToString();
        }

        private static void AppendSection(
            [NotNull] StringBuilder builder,
            [NotNull] string title,
            [NotNull] [ItemNotNull] IEnumerable<UnityBuiltInPackageAuditEntry> entries)
        {
            var entryList = entries.ToList();
            if (entryList.Count == 0)
            {
                return;
            }

            builder.AppendLine(title);
            foreach (var entry in entryList)
            {
                var requester = entry.RequestedBy == null ? "packages.config" : $"{entry.RequestedBy.Id} {entry.RequestedBy.Version}";
                builder.Append("  - ")
                    .Append(entry.Package.Id)
                    .Append(' ')
                    .Append(entry.Package.Version)
                    .Append(" | required by: ")
                    .Append(requester)
                    .Append(" | built into Unity: ")
                    .Append(entry.CheckResult.BuiltInVersion)
                    .AppendLine();
            }
        }
    }
}
