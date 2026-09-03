using JetBrains.Annotations;

namespace NugetForUnity.Models
{
    /// <summary>
    ///     Result of <see cref="NugetForUnity.UnityBuiltInPackageOverrides.Check" />.
    /// </summary>
    internal readonly struct UnityBuiltInPackageCheckResult
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UnityBuiltInPackageCheckResult" /> struct.
        /// </summary>
        /// <param name="compatibility">The compatibility of the requested package with the version built into Unity.</param>
        /// <param name="builtInVersion">The version of the package built into Unity or <c>null</c> if Unity doesn't provide the package.</param>
        /// <param name="message">A human readable description of the result.</param>
        public UnityBuiltInPackageCheckResult(
            UnityBuiltInPackageCompatibility compatibility,
            [CanBeNull] NugetPackageVersion builtInVersion,
            [NotNull] string message)
        {
            Compatibility = compatibility;
            BuiltInVersion = builtInVersion;
            Message = message;
        }

        /// <summary>
        ///     Gets the compatibility of the requested package with the version built into Unity.
        /// </summary>
        public UnityBuiltInPackageCompatibility Compatibility { get; }

        /// <summary>
        ///     Gets the version of the package built into Unity or <c>null</c> if Unity doesn't provide the package.
        /// </summary>
        [CanBeNull]
        public NugetPackageVersion BuiltInVersion { get; }

        /// <summary>
        ///     Gets a human readable description of the result.
        /// </summary>
        [NotNull]
        public string Message { get; }

        /// <summary>
        ///     Gets a value indicating whether the requested package must not be installed because of an error
        ///     (<see cref="UnityBuiltInPackageCompatibility.Incompatible" /> or <see cref="UnityBuiltInPackageCompatibility.Unverifiable" />).
        /// </summary>
        public bool IsError =>
            Compatibility == UnityBuiltInPackageCompatibility.Incompatible || Compatibility == UnityBuiltInPackageCompatibility.Unverifiable;
    }
}
