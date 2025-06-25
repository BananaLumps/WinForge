namespace WinForge.Common
{
    /// <summary>
    /// Defines a contract for managing service dependencies.
    /// </summary>
    public interface IDependencyService
    {
        /// <summary>
        /// Gets a dependency instance of type T, creating it if it does not exist.
        /// </summary>
        /// <typeparam name="T">The type of dependency to retrieve.</typeparam>
        /// <returns>An instance of type T.</returns>
        T GetDependency<T>() where T : class, new();

        /// <summary>
        /// Tries to get a dependency instance of type T. Returns null if it does not exist.
        /// </summary>
        /// <typeparam name="T">The type of dependency to retrieve.</typeparam>
        /// <param name="instance">When this method returns, contains the instance of type T if found; otherwise, null.</param>
        /// <returns>true if the dependency was found; otherwise, false.</returns>
        bool TryGetDependency<T>(out T? instance) where T : class;
    }
}