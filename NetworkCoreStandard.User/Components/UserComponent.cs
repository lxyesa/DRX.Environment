using System;
using NetworkCoreStandard.Events;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.User.Components;

public class UserComponent : IComponent
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public override void Awake()
    {

    }

    public override void Start()
    {

    }
}
