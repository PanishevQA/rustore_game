using System;
using System.Reflection;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Platform.RuStore
{
    /// <summary>
    /// Adapter for RuStore Install Referrer. Reflection is intentional here: the public
    /// Unity API is stable (InstallReferrerClient.Init/GetInstallReferrer), while assembly
    /// names have changed between plugin packaging variants. Gameplay never depends on it.
    /// </summary>
    public sealed class RuStoreInstallReferrerService : IReferrerService
    {
        public Task<InstallReferrerResult> ConsumeInstallReferrerAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Type clientType = FindRuStoreType("InstallReferrerClient");
                if (clientType == null)
                {
                    Debug.LogWarning("RuStore Install Referrer client type was not found.");
                    return Task.FromResult(new InstallReferrerResult(false, null));
                }

                PropertyInfo instanceProperty = clientType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object client = instanceProperty?.GetValue(null);
                if (client == null)
                    return Task.FromResult(new InstallReferrerResult(false, null));

                PropertyInfo initializedProperty = clientType.GetProperty("IsInitialized", BindingFlags.Public | BindingFlags.Instance);
                bool initialized = initializedProperty != null && initializedProperty.PropertyType == typeof(bool) &&
                                   (bool)initializedProperty.GetValue(client);
                if (!initialized)
                {
                    MethodInfo initMethod = clientType.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    initMethod?.Invoke(client, null);
                }

                MethodInfo getMethod = FindGetInstallReferrerMethod(clientType);
                if (getMethod == null)
                {
                    Debug.LogWarning("RuStore Install Referrer GetInstallReferrer method was not found.");
                    return Task.FromResult(new InstallReferrerResult(false, null));
                }

                ParameterInfo[] parameters = getMethod.GetParameters();
                var callbacks = new CallbackBox();
                Delegate failure = CreateCallback(parameters[0].ParameterType, callbacks, nameof(CallbackBox.OnFailure));
                Delegate success = CreateCallback(parameters[1].ParameterType, callbacks, nameof(CallbackBox.OnSuccess));
                getMethod.Invoke(client, new object[] { failure, success });
                return callbacks.Task;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Install Referrer unavailable: {Unwrap(error).Message}");
                return Task.FromResult(new InstallReferrerResult(false, null));
            }
#else
            return Task.FromResult(new InstallReferrerResult(false, null));
#endif
        }

        private static MethodInfo FindGetInstallReferrerMethod(Type clientType)
        {
            MethodInfo[] methods = clientType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "GetInstallReferrer", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    typeof(Delegate).IsAssignableFrom(parameters[0].ParameterType.BaseType) &&
                    typeof(Delegate).IsAssignableFrom(parameters[1].ParameterType.BaseType))
                    return method;
            }
            return null;
        }

        private static Delegate CreateCallback(Type delegateType, CallbackBox target, string methodName)
        {
            Type[] genericArguments = delegateType.IsGenericType ? delegateType.GetGenericArguments() : Type.EmptyTypes;
            if (genericArguments.Length != 1)
                throw new InvalidOperationException($"Unexpected RuStore callback type: {delegateType.FullName}");

            MethodInfo openMethod = typeof(CallbackBox).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            MethodInfo closedMethod = openMethod.MakeGenericMethod(genericArguments[0]);
            return Delegate.CreateDelegate(delegateType, target, closedMethod);
        }

        private static Type FindRuStoreType(string shortName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (ReflectionTypeLoadException loadError)
                {
                    types = loadError.Types;
                }

                if (types == null) continue;
                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !string.Equals(type.Name, shortName, StringComparison.Ordinal)) continue;
                    if (type.Namespace != null && type.Namespace.StartsWith("RuStore", StringComparison.Ordinal))
                        return type;
                }
            }
            return null;
        }

        private static Exception Unwrap(Exception error) =>
            error is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : error;

        private sealed class CallbackBox
        {
            private readonly TaskCompletionSource<InstallReferrerResult> _completion =
                new TaskCompletionSource<InstallReferrerResult>();

            public Task<InstallReferrerResult> Task => _completion.Task;

            public void OnFailure<T>(T error)
            {
                Debug.LogWarning($"RuStore Install Referrer request failed: {error}");
                _completion.TrySetResult(new InstallReferrerResult(false, null));
            }

            public void OnSuccess<T>(T result)
            {
                if (ReferenceEquals(result, null))
                {
                    _completion.TrySetResult(new InstallReferrerResult(true, null));
                    return;
                }

                Type resultType = result.GetType();
                PropertyInfo property = resultType.GetProperty("referrerId", BindingFlags.Public | BindingFlags.Instance) ??
                                        resultType.GetProperty("ReferrerId", BindingFlags.Public | BindingFlags.Instance);
                string referrerId = property?.GetValue(result)?.ToString();
                _completion.TrySetResult(new InstallReferrerResult(true, string.IsNullOrWhiteSpace(referrerId) ? null : referrerId));
            }
        }
    }
}
