using JetBrains.Annotations;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     A single finding of the <see cref="NugetForUnity.UnityBuiltInPackageAuditor" />.
    /// </summary>
    internal sealed class UnityBuiltInPackageAuditEntry
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UnityBuiltInPackageAuditEntry" /> class.
        /// </summary>
        /// <param name="kind">The kind of the finding.</param>
        /// <param name="package">The requested package (id and version or version range).</param>
        /// <param name="requestedBy">The installed package that depends on the requested package, <c>null</c> for packages of the <c>packages.config</c>.</param>
        /// <param name="checkResult">The result of the compatibility check.</param>
        public UnityBuiltInPackageAuditEntry(
            UnityBuiltInPackageAuditEntryKind kind,
            [NotNull] INugetPackageIdentifier package,
            [CanBeNull] INugetPackageIdentifier requestedBy,
            UnityBuiltInPackageCheckResult checkResult)
        {
            Kind = kind;
            Package = package;
            RequestedBy = requestedBy;
            CheckResult = checkResult;
        }

        /// <summary>
        ///     Gets the kind of the finding.
        /// </summary>
        public UnityBuiltInPackageAuditEntryKind Kind { get; }

        /// <summary>
        ///     Gets the requested package (id and version or version range).
        /// </summary>
        [NotNull]
        public INugetPackageIdentifier Package { get; }

        /// <summary>
        ///     Gets the installed package that depends on the requested package, <c>null</c> for packages of the <c>packages.config</c>.
        /// </summary>
        [CanBeNull]
        public INugetPackageIdentifier RequestedBy { get; }

        /// <summary>
        ///     Gets the result of the compatibility check.
        /// </summary>
        public UnityBuiltInPackageCheckResult CheckResult { get; }

        /// <summary>
        ///     Gets a value indicating whether the finding is an error (the requested version range can't be satisfied by Unity).
        /// </summary>
        public bool IsIncompatible =>
            Kind == UnityBuiltInPackageAuditEntryKind.IncompatibleConfiguredPackage || Kind == UnityBuiltInPackageAuditEntryKind.IncompatibleDependency;

        /// <inheritdoc />
        public override string ToString()
        {
            var requester = RequestedBy == null ? "packages.config" : $"{RequestedBy.Id} {RequestedBy.Version}";
            return $"[{Kind}] {Package.Id} {Package.Version} (required by: {requester}, built into Unity: {CheckResult.BuiltInVersion})";
        }
    }
}
