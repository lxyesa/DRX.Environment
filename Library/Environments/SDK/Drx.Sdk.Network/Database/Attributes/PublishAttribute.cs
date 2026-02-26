using System;

namespace Drx.Sdk.Network.DataBase;

/// <summary>
/// 标记子表中需要发布到主表的属性
/// 被标记的属性值会在同步时自动复制到主表的同名字段
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class PublishAttribute : Attribute
{
}
