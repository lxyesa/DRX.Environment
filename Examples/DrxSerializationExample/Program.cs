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
        var name = d2.TryGetString("name");
        if (name is not null)
            Console.WriteLine($"name={name}");
        var age = d2.TryGetInt("age");
        if (age is not null)
            Console.WriteLine($"age={age}");
        var m = d2.TryGetObject("meta");
        if (m is not null)
        {
            var role = m.TryGetString("role");
            if (role is not null)
                Console.WriteLine($"meta.role={role}");
        }
    }
}
