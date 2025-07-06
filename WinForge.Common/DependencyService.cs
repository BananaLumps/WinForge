using System.Collections.Concurrent;

namespace WinForge.Common
{
    public class DependencyService : IDependencyService
    {
        private readonly ConcurrentDictionary<Type, object> _services = new();

        /// <summary> Registers a dependency instance of type T. </summary>
        /// example: `dependencyService.Register<ILogger>(new Logger());`
        public void Register<T>(T instance) where T : class
        {
            if (instance is null)
                throw new ArgumentNullException(nameof(instance), "Cannot register a null dependency.");

            var type = typeof(T);
            _services[type] = instance;

            // Also register by interface types if the instance implements any
            foreach (var interfaceType in type.GetInterfaces())
            {
                _services.TryAdd(interfaceType, instance);
            }
        }

        /// <inheritdoc />
        public T GetDependency<T>() where T : class, new()
        {
            var type = typeof(T);

            foreach (var key in _services.Keys)

                if (_services.TryGetValue(type, out var instance))
                    return (T)instance;

            var created = new T();
            _services[type] = created;

            // Also register by interface types
            foreach (var interfaceType in type.GetInterfaces())
            {
                _services.TryAdd(interfaceType, created);
            }

            return created;
        }

        /// <inheritdoc />
        public bool TryGetDependency<T>(out T? instance) where T : class
        {
            var type = typeof(T);

            // Try direct lookup first
            if (_services.TryGetValue(type, out var existing) && existing != null)
            {
                instance = (T)existing;
                return true;
            }

            // Fallback: search for assignable types
            foreach (var kvp in _services)
            {
                if (type.IsAssignableFrom(kvp.Key))
                {
                    instance = (T)kvp.Value;
                    return true;
                }
            }

            instance = null;
            return false;
        }

        /// <summary> Stops all registered modules by invoking their Stop method if available. </summary>
        public void StopAllModules()
        {
            foreach (var service in _services.Values)
            {
                var stopMethod = service.GetType().GetMethod("Stop", Type.EmptyTypes);
                if (stopMethod != null)
                {
                    stopMethod.Invoke(service, null);
                    Console.WriteLine($"Stopped service: {service.GetType().Name}");
                }
            }
        }
    }
}
