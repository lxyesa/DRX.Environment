namespace Drx.Sdk.Registry;

public readonly struct Identifier(string nameSpace, string name) : IEquatable<Identifier>
{
    private readonly string _namespace = nameSpace ?? throw new ArgumentNullException(nameof(nameSpace));
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));

    public override string ToString()
    {
        return $"{_namespace}:{_name}";
    }

    public override bool Equals(object? obj)
    {
        return obj is Identifier identifier && Equals(identifier);
    }

    public bool Equals(Identifier other)
    {
        return _namespace == other._namespace && _name == other._name;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_namespace, _name);
    }

    public static bool operator ==(Identifier left, Identifier right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Identifier left, Identifier right)
    {
        return !(left == right);
    }
}