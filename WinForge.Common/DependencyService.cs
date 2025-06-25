using System.Collections.Concurrent;

namespace WinForge.Common
{
    public class DependencyService : IDependencyService
    {
        private readonly ConcurrentDictionary<Type, object> _services = new();

        /// <summary> Registers a dependency instance of type T. </summary>
        public void Register<T>(T instance) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                return;
            }
            _services[type] = instance;
        }

        /// <inheritdoc />
        public T GetDependency<T>() where T : class, new()
        {
            var type = typeof(T);

            if (_services.TryGetValue(type, out var instance))
                return (T)instance;

            var created = new T();
            _services[type] = created;
            return created;
        }

        /// <inheritdoc />
        public bool TryGetDependency<T>(out T? instance) where T : class
        {
            var type = typeof(T);

            if (_services.TryGetValue(type, out var existing))
            {
                instance = (T)existing;
                return true;
            }

            instance = null;
            return false;
        }
    }
}
