namespace Drx.Sdk.Registry;

public class Registry
{
    private static readonly Dictionary<string, object> _registry = new();

    public static void Register<T>(Identifier identifier, T value, bool returnIfExists = false)
    {
        string key = identifier.ToString();

        if (_registry.ContainsKey(key))
        {
            if (!returnIfExists)
            {
                throw new InvalidOperationException($"标识符 '{key}' 已在注册表中注册");
            }
            return;
        }

        _registry[key] = value ?? throw new ArgumentNullException(nameof(value));
    }

    public static T GetRegisteredValue<T>(Identifier identifier)
    {
        string key = identifier.ToString();

        if (!_registry.TryGetValue(key, out var value1))
        {
            throw new KeyNotFoundException($"找不到标识符 '{key}' 的注册值");
        }

        if (value1 is T value)
        {
            return value;
        }

        throw new InvalidCastException($"无法将注册值转换为类型 '{typeof(T).Name}'");
    }
}