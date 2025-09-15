using System;
using Drx.Sdk.Shared.Serialization;

class Program
{
    static void Main()
    {
        var d = new DrxSerializationData();
        d.SetString("name", "Alice");
        d.SetInt("age", 30);
        d.SetBool("active", true);

        var meta = new DrxSerializationData();
        meta.SetString("role", "admin");
        meta.SetDouble("score", 99.5);
        d.SetObject("meta", meta);

        var bytes = d.Serialize();
        Console.WriteLine($"Serialized {bytes.Length} bytes");

        var d2 = DrxSerializationData.Deserialize(bytes);
        if (d2.TryGetString("name", out var name))
            Console.WriteLine($"name={name}");
        if (d2.TryGetInt("age", out var age))
            Console.WriteLine($"age={age}");
        if (d2.TryGetObject("meta", out var m) && m.TryGetString("role", out var role))
            Console.WriteLine($"meta.role={role}");
    }
}
