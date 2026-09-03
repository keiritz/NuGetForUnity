namespace NugetForUnity.Models
{
    /// <summary>
    ///     The result of comparing a requested NuGet package (version range) with the version built into the Unity Editor.
    /// </summary>
    internal enum UnityBuiltInPackageCompatibility
    {
        /// <summary>
        ///     Unity doesn't provide the package, the normal NuGetForUnity handling applies.
        /// </summary>
        NotBuiltIn,

        /// <summary>
        ///     The version built into Unity satisfies the requested version range, so the package must not be installed from NuGet.
        /// </summary>
        Satisfied,

        /// <summary>
        ///     The version built into Unity doesn't satisfy the requested version range.
        ///     As Unity always resolves to its built-in assembly the package can't be installed from NuGet.
        /// </summary>
        Incompatible,

        /// <summary>
        ///     Unity ships built-in packages but the override information couldn't be read, so compatibility can't be verified.
        /// </summary>
        Unverifiable,
    }
}
