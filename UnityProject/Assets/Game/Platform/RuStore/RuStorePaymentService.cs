using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Platform.RuStore
{
    /// <summary>
    /// RuStore Pay adapter. Reflection keeps Game.Platform.RuStore independent from
    /// package assembly names while still invoking the public RuStorePayClient API.
    /// Product prices always come from GetProducts; no price is hardcoded by the game.
    /// </summary>
    public sealed class RuStorePaymentService : IPaymentService
    {
        public Task<IReadOnlyList<StoreProduct>> GetProductsAsync(IReadOnlyList<string> productIds)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (productIds == null) throw new ArgumentNullException(nameof(productIds));
            try
            {
                object client = GetPayClient(out Type clientType);
                Type productIdType = FindRuStoreType("ProductId") ?? throw new InvalidOperationException("RuStore ProductId type not found.");
                Array ids = Array.CreateInstance(productIdType, productIds.Count);
                for (int i = 0; i < productIds.Count; i++)
                    ids.SetValue(CreateScalar(productIdType, productIds[i]), i);

                MethodInfo method = FindMethod(clientType, "GetProducts", parameter =>
                    parameter.ParameterType.IsArray && parameter.ParameterType.GetElementType() == productIdType);
                if (method == null) throw new MissingMethodException(clientType.FullName, "GetProducts");

                var box = new ProductListCallbackBox();
                object[] arguments = BuildArguments(
                    method,
                    firstPayload: ids,
                    failureTarget: box,
                    failureMethod: nameof(ProductListCallbackBox.OnFailure),
                    successTarget: box,
                    successMethod: nameof(ProductListCallbackBox.OnSuccess));
                method.Invoke(client, arguments);
                return box.Task;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore GetProducts unavailable: {Unwrap(error).Message}");
                return Task.FromResult<IReadOnlyList<StoreProduct>>(Array.Empty<StoreProduct>());
            }
#else
            return Task.FromResult<IReadOnlyList<StoreProduct>>(Array.Empty<StoreProduct>());
#endif
        }

        public Task<PaymentPurchaseResult> PurchaseAsync(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("Product id is required.", nameof(productId));

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                object client = GetPayClient(out Type clientType);
                Type parametersType = FindRuStoreType("ProductPurchaseParams") ??
                                      throw new InvalidOperationException("RuStore ProductPurchaseParams type not found.");
                object purchaseParameters = CreatePurchaseParameters(parametersType, productId);

                MethodInfo method = FindPurchaseMethod(clientType, parametersType);
                if (method == null) throw new MissingMethodException(clientType.FullName, "Purchase");

                var box = new PurchaseCallbackBox(productId);
                object[] arguments = BuildPurchaseArguments(method, purchaseParameters, box);
                method.Invoke(client, arguments);
                return box.Task;
            }
            catch (Exception error)
            {
                Exception unwrapped = Unwrap(error);
                Debug.LogWarning($"RuStore Purchase unavailable: {unwrapped.Message}");
                return Task.FromResult(new PaymentPurchaseResult(
                    PurchaseOutcome.Failed,
                    productId,
                    errorMessage: unwrapped.Message));
            }
#else
            return Task.FromResult(new PaymentPurchaseResult(
                PurchaseOutcome.Failed,
                productId,
                errorMessage: "RuStore Pay is available only on Android builds."));
#endif
        }

        public Task<IReadOnlyList<string>> RestoreEntitlementsAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                object client = GetPayClient(out Type clientType);
                MethodInfo method = FindGetPurchasesMethod(clientType);
                if (method == null) throw new MissingMethodException(clientType.FullName, "GetPurchases");

                var box = new RestoreCallbackBox();
                object[] arguments = BuildArguments(
                    method,
                    firstPayload: null,
                    failureTarget: box,
                    failureMethod: nameof(RestoreCallbackBox.OnFailure),
                    successTarget: box,
                    successMethod: nameof(RestoreCallbackBox.OnSuccess));
                method.Invoke(client, arguments);
                return box.Task;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore restore unavailable: {Unwrap(error).Message}");
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }
#else
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
#endif
        }

        private static object GetPayClient(out Type clientType)
        {
            clientType = FindRuStoreType("RuStorePayClient") ??
                         throw new InvalidOperationException("RuStorePayClient type not found. Verify ru.rustore.pay package resolution.");
            PropertyInfo instanceProperty = clientType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static) ??
                                            clientType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            object client = instanceProperty?.GetValue(null);
            if (client == null) throw new InvalidOperationException("RuStorePayClient.Instance is unavailable.");
            return client;
        }

        private static MethodInfo FindMethod(Type type, string name, Func<ParameterInfo, bool> payloadMatcher)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                bool hasPayload = false;
                int delegateCount = 0;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (IsDelegate(parameters[p].ParameterType)) delegateCount++;
                    else if (payloadMatcher(parameters[p])) hasPayload = true;
                }
                if (hasPayload && delegateCount >= 2) return method;
            }
            return null;
        }

        private static MethodInfo FindPurchaseMethod(Type type, Type parametersType)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Purchase", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                bool hasParameters = false;
                int delegates = 0;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType == parametersType) hasParameters = true;
                    if (IsDelegate(parameters[p].ParameterType)) delegates++;
                }
                if (hasParameters && delegates >= 2) return method;
            }
            return null;
        }

        private static MethodInfo FindGetPurchasesMethod(Type type)
        {
            MethodInfo best = null;
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "GetPurchases", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                int delegates = 0;
                bool unsupportedRequired = false;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (IsDelegate(parameters[p].ParameterType)) delegates++;
                    else if (!parameters[p].IsOptional && !CanDefault(parameters[p].ParameterType)) unsupportedRequired = true;
                }
                if (delegates < 2 || unsupportedRequired) continue;
                if (best == null || parameters.Length < best.GetParameters().Length) best = method;
            }
            return best;
        }

        private static object[] BuildArguments(
            MethodInfo method,
            object firstPayload,
            object failureTarget,
            string failureMethod,
            object successTarget,
            string successMethod)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var result = new object[parameters.Length];
            bool payloadAssigned = firstPayload == null;
            bool failureAssigned = false;
            bool successAssigned = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                Type type = parameters[i].ParameterType;
                if (!payloadAssigned && firstPayload != null && type.IsInstanceOfType(firstPayload))
                {
                    result[i] = firstPayload;
                    payloadAssigned = true;
                }
                else if (IsDelegate(type))
                {
                    string name = parameters[i].Name ?? string.Empty;
                    bool looksFailure = name.IndexOf("failure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        name.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
                    if ((!failureAssigned && looksFailure) || successAssigned)
                    {
                        result[i] = CreateCallback(type, failureTarget, failureMethod);
                        failureAssigned = true;
                    }
                    else if (!successAssigned)
                    {
                        result[i] = CreateCallback(type, successTarget, successMethod);
                        successAssigned = true;
                    }
                    else
                    {
                        result[i] = null;
                    }
                }
                else
                {
                    result[i] = DefaultValue(parameters[i]);
                }
            }

            if (!failureAssigned || !successAssigned)
                throw new InvalidOperationException($"Could not bind callbacks for {method.Name}.");
            return result;
        }

        private static object[] BuildPurchaseArguments(MethodInfo method, object purchaseParameters, PurchaseCallbackBox box)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var result = new object[parameters.Length];
            bool failureAssigned = false;
            bool successAssigned = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                Type type = parameters[i].ParameterType;
                string name = parameters[i].Name ?? string.Empty;
                if (type.IsInstanceOfType(purchaseParameters))
                {
                    result[i] = purchaseParameters;
                }
                else if (IsDelegate(type))
                {
                    bool looksFailure = name.IndexOf("failure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        name.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
                    if ((!failureAssigned && looksFailure) || successAssigned)
                    {
                        result[i] = CreateCallback(type, box, nameof(PurchaseCallbackBox.OnFailure));
                        failureAssigned = true;
                    }
                    else if (!successAssigned)
                    {
                        result[i] = CreateCallback(type, box, nameof(PurchaseCallbackBox.OnSuccess));
                        successAssigned = true;
                    }
                    else
                    {
                        result[i] = null;
                    }
                }
                else if (type.IsEnum)
                {
                    result[i] = EnumValue(type, name.IndexOf("theme", StringComparison.OrdinalIgnoreCase) >= 0 ? "DARK" : "ONE_STEP");
                }
                else
                {
                    result[i] = DefaultValue(parameters[i]);
                }
            }

            if (!failureAssigned || !successAssigned)
                throw new InvalidOperationException("Could not bind RuStore Purchase callbacks.");
            return result;
        }

        private static object CreatePurchaseParameters(Type parametersType, string productId)
        {
            ConstructorInfo[] constructors = parametersType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            for (int c = 0; c < constructors.Length; c++)
            {
                ParameterInfo[] parameters = constructors[c].GetParameters();
                var values = new object[parameters.Length];
                bool productAssigned = false;
                bool valid = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    Type type = Nullable.GetUnderlyingType(parameters[i].ParameterType) ?? parameters[i].ParameterType;
                    if (string.Equals(type.Name, "ProductId", StringComparison.Ordinal))
                    {
                        values[i] = CreateScalar(type, productId);
                        productAssigned = true;
                    }
                    else if (string.Equals(type.Name, "Quantity", StringComparison.Ordinal))
                    {
                        values[i] = CreateScalar(type, 1);
                    }
                    else if (parameters[i].IsOptional)
                    {
                        values[i] = parameters[i].DefaultValue is DBNull ? Type.Missing : parameters[i].DefaultValue;
                    }
                    else if (!type.IsValueType || Nullable.GetUnderlyingType(parameters[i].ParameterType) != null)
                    {
                        values[i] = null;
                    }
                    else
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid && productAssigned)
                    return constructors[c].Invoke(values);
            }
            throw new InvalidOperationException("No compatible ProductPurchaseParams constructor found.");
        }

        private static object CreateScalar(Type type, object value)
        {
            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < constructors.Length; i++)
            {
                ParameterInfo[] parameters = constructors[i].GetParameters();
                if (parameters.Length != 1) continue;
                try
                {
                    object converted = Convert.ChangeType(value, parameters[0].ParameterType);
                    return constructors[i].Invoke(new[] { converted });
                }
                catch
                {
                    // Try next value-object constructor.
                }
            }
            throw new InvalidOperationException($"Could not create RuStore value type {type.FullName}.");
        }

        private static Delegate CreateCallback(Type delegateType, object target, string methodName)
        {
            Type[] genericArguments = delegateType.IsGenericType ? delegateType.GetGenericArguments() : Type.EmptyTypes;
            if (genericArguments.Length != 1)
                throw new InvalidOperationException($"Unexpected RuStore callback type: {delegateType.FullName}");

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            MethodInfo closed = method.MakeGenericMethod(genericArguments[0]);
            return Delegate.CreateDelegate(delegateType, target, closed);
        }

        private static bool IsDelegate(Type type) => typeof(Delegate).IsAssignableFrom(type);

        private static bool CanDefault(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null || type.IsEnum;

        private static object DefaultValue(ParameterInfo parameter)
        {
            if (parameter.IsOptional)
                return parameter.DefaultValue is DBNull ? Type.Missing : parameter.DefaultValue;
            Type type = parameter.ParameterType;
            if (!type.IsValueType || Nullable.GetUnderlyingType(type) != null) return null;
            if (type.IsEnum) return Enum.ToObject(type, 0);
            return Activator.CreateInstance(type);
        }

        private static object EnumValue(Type enumType, string preferred)
        {
            string[] names = Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], preferred, StringComparison.OrdinalIgnoreCase))
                    return Enum.Parse(enumType, names[i]);
            return Enum.ToObject(enumType, 0);
        }

        private static Type FindRuStoreType(string shortName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try { types = assemblies[a].GetTypes(); }
                catch (ReflectionTypeLoadException error) { types = error.Types; }
                if (types == null) continue;
                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !string.Equals(type.Name, shortName, StringComparison.Ordinal)) continue;
                    if (type.Namespace != null && type.Namespace.StartsWith("RuStore", StringComparison.Ordinal)) return type;
                }
            }
            return null;
        }

        private static object ReadMember(object source, string name)
        {
            if (source == null) return null;
            Type type = source.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null) return property.GetValue(source);
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return field?.GetValue(source);
        }

        private static string ReadString(object source, string name)
        {
            object value = ReadMember(source, name);
            if (value == null) return null;
            object nested = ReadMember(value, "value") ?? ReadMember(value, "Value");
            return (nested ?? value).ToString();
        }

        private static Exception Unwrap(Exception error) =>
            error is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : error;

        private sealed class ProductListCallbackBox
        {
            private readonly TaskCompletionSource<IReadOnlyList<StoreProduct>> _completion =
                new TaskCompletionSource<IReadOnlyList<StoreProduct>>();
            public Task<IReadOnlyList<StoreProduct>> Task => _completion.Task;

            public void OnFailure<T>(T error)
            {
                Debug.LogWarning($"RuStore GetProducts failed: {error}");
                _completion.TrySetResult(Array.Empty<StoreProduct>());
            }

            public void OnSuccess<T>(T response)
            {
                var products = new List<StoreProduct>();
                if (response is IEnumerable enumerable)
                {
                    foreach (object item in enumerable)
                    {
                        if (item == null) continue;
                        products.Add(new StoreProduct
                        {
                            Id = ReadString(item, "productId"),
                            Title = ReadString(item, "title"),
                            Description = ReadString(item, "description"),
                            PriceLabel = ReadString(item, "amountLabel"),
                            Type = ReadString(item, "type")
                        });
                    }
                }
                _completion.TrySetResult(products);
            }
        }

        private sealed class PurchaseCallbackBox
        {
            private readonly string _requestedProductId;
            private readonly TaskCompletionSource<PaymentPurchaseResult> _completion =
                new TaskCompletionSource<PaymentPurchaseResult>();
            public Task<PaymentPurchaseResult> Task => _completion.Task;

            public PurchaseCallbackBox(string requestedProductId) => _requestedProductId = requestedProductId;

            public void OnFailure<T>(T error)
            {
                string typeName = error?.GetType().Name ?? string.Empty;
                PurchaseOutcome outcome = typeName.IndexOf("Cancelled", StringComparison.OrdinalIgnoreCase) >= 0
                    ? PurchaseOutcome.Cancelled
                    : PurchaseOutcome.Failed;
                _completion.TrySetResult(new PaymentPurchaseResult(
                    outcome,
                    _requestedProductId,
                    errorMessage: error?.ToString()));
            }

            public void OnSuccess<T>(T response)
            {
                _completion.TrySetResult(new PaymentPurchaseResult(
                    PurchaseOutcome.Completed,
                    ReadString(response, "productId") ?? _requestedProductId,
                    ReadString(response, "purchaseId"),
                    ReadString(response, "invoiceId"),
                    ReadString(response, "subscriptionToken")));
            }
        }

        private sealed class RestoreCallbackBox
        {
            private readonly TaskCompletionSource<IReadOnlyList<string>> _completion =
                new TaskCompletionSource<IReadOnlyList<string>>();
            public Task<IReadOnlyList<string>> Task => _completion.Task;

            public void OnFailure<T>(T error)
            {
                Debug.LogWarning($"RuStore GetPurchases failed: {error}");
                _completion.TrySetResult(Array.Empty<string>());
            }

            public void OnSuccess<T>(T response)
            {
                var result = new List<string>();
                if (response is IEnumerable enumerable)
                {
                    foreach (object purchase in enumerable)
                    {
                        if (purchase == null) continue;
                        string status = ReadString(purchase, "status");
                        string type = ReadString(purchase, "productType");
                        if (!string.Equals(status, "CONFIRMED", StringComparison.OrdinalIgnoreCase)) continue;
                        if (type != null && type.IndexOf("NON_CONSUMABLE", StringComparison.OrdinalIgnoreCase) < 0 &&
                            type.IndexOf("NON-CONSUMABLE", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string productId = ReadString(purchase, "productId");
                        if (!string.IsNullOrWhiteSpace(productId) && !result.Contains(productId)) result.Add(productId);
                    }
                }
                _completion.TrySetResult(result);
            }
        }
    }
}
