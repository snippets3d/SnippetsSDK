using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Static class that holds the references to all the globally accessible services
    /// </summary>
    /// <remarks>
    /// This is used to remove singletons from the project and apply some sort of dependency injection
    /// </remarks>
    public static class Services
    {
        /// <summary>
        /// The Dictionary holding all the current services
        /// </summary>
        private static Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// Gets if there is a service associated with this type
        /// </summary>
        /// <typeparam name="T">Type of service we are looking for. May be a class or an interface</typeparam>
        /// <returns>Bool if an actual implementation of the service requested exists, false otherwise</returns>
        public static bool HasService<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Gets the service associated with this type
        /// </summary>
        /// <typeparam name="T">Type of service we are looking for. May be a class or an interface</typeparam>
        /// <returns>Actual implementation of the service requested</returns>
        /// <exception cref="KeyNotFoundException">If the service is not found</exception>
        public static T GetService<T>() where T : class
        {
            Debug.Assert(_services.ContainsKey(typeof(T)));

            return _services[typeof(T)] as T;
        }

        /// <summary>
        /// Gets the service associated with this type, waiting until it is available
        /// </summary>
        /// <remarks>
        /// Use with caution: if the service is never set, this may wait indefinitely
        /// </remarks>
        /// <typeparam name="T">Type of service we are looking for. May be a class or an interface</typeparam>
        /// <param name="cancellationToken">The cancellation token to cancel the wait</param>
        /// <returns>Actual implementation of the service requested</returns>
        public static async UniTask<T> GetWaitService<T>(CancellationToken cancellationToken = default) where T : class
        {
            await UniTask.WaitUntil(() => HasService<T>(), cancellationToken: cancellationToken);

            return GetService<T>();
        }

        /// <summary>
        /// Sets the service associated with a specific type
        /// </summary>
        /// <typeparam name="T">Type of service we want to assign. May be a class or an interface</typeparam>
        /// <param name="service">The service to attach. If it is null, the service type is removed from the list</param>
        public static void SetService<T>(T service) where T : class
        {
            if (service != null)
            {
                _services[typeof(T)] = (object)service;

                Debug.Log($"[Services] Added service of type {typeof(T)}");
            }
            else if (_services.ContainsKey(typeof(T)))
            {
                _services.Remove(typeof(T));

                Debug.Log($"[Services] Removed service of type {typeof(T)}");
            }

        }

        /// <summary>
        /// Clears all the services, restoring the class to the initial state
        /// </summary>
        public static void ClearServices()
        {
            _services.Clear();
        }

    }

}