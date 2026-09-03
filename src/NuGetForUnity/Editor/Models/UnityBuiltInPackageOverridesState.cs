namespace NugetForUnity.Models
{
    /// <summary>
    ///     Describes whether the information about the NuGet packages that are built into the Unity Editor is available.
    /// </summary>
    internal enum UnityBuiltInPackageOverridesState
    {
        /// <summary>
        ///     The current Unity version doesn't ship the Base Class Library extensions (Unity versions before 6.5).
        ///     The classic detection (<see cref="NugetForUnity.UnityPreImportedLibraryResolver" />) is used unchanged.
        /// </summary>
        NotApplicable,

        /// <summary>
        ///     The <c>PackageOverrides.txt</c> shipped with the Unity Editor was found and parsed.
        /// </summary>
        Available,

        /// <summary>
        ///     The location of the Unity Editor installation is unknown (e.g. running outside of Unity in the CLI),
        ///     so the built-in packages can't be verified.
        /// </summary>
        Unknown,

        /// <summary>
        ///     The current Unity version is expected to ship the override information but it couldn't be read.
        ///     We don't assume compatibility in this case, see <see cref="UnityBuiltInPackageCompatibility.Unverifiable" />.
        /// </summary>
        Unavailable,
    }
}
