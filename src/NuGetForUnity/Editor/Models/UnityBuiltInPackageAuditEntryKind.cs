namespace NugetForUnity.Models
{
    /// <summary>
    ///     The kind of a finding of the <see cref="NugetForUnity.UnityBuiltInPackageAuditor" />.
    /// </summary>
    internal enum UnityBuiltInPackageAuditEntryKind
    {
        /// <summary>
        ///     A package listed in the <c>packages.config</c> is provided by Unity in a compatible version, so the NuGet package is redundant.
        /// </summary>
        RedundantConfiguredPackage,

        /// <summary>
        ///     A package listed in the <c>packages.config</c> is provided by Unity in an incompatible version.
        /// </summary>
        IncompatibleConfiguredPackage,

        /// <summary>
        ///     A dependency of an installed package is provided by Unity in a compatible version.
        /// </summary>
        SatisfiedDependency,

        /// <summary>
        ///     A dependency of an installed package is provided by Unity in an incompatible version.
        /// </summary>
        IncompatibleDependency,
    }
}
