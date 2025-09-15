DrxSerializationData（DSD）
=====================

简介
----
DrxSerializationData 是一个轻量级的键值序列化容器（简称 DSD），用于在二进制格式下保存简单数据与嵌套对象。当前实现为基础骨架，支持常用类型并能序列化树形嵌套结构。

主要特性（当前实现）
- 支持类型：Null、Int64、Double、Bool、String、Bytes、嵌套 Object（DrxSerializationData）
- 简洁的 Set/Get API（类型化 TryGetXxx）
- 线程安全的单实例读写（使用 ReaderWriterLockSlim）
- 简单紧凑的二进制格式：每个条目包含 key、type 与 payload

限制与注意事项
- 当前只支持树形嵌套，循环引用（对象 A 引用 B，B 又引用 A）未被支持，会导致无限递归。建议在序列化前避免循环或等待未来版本支持引用表。
- 共享引用不会被保留：反序列化后相同的子对象会成为不同实例的副本。
- 对于大型对象、深度嵌套或高性能场景，建议使用流式 API（未来改进）或对序列化做快照。
- SetBytes 会复制传入数组以确保内部安全；如需要零拷贝可考虑扩展 API。

示例
----
参见项目 `Examples/DrxSerializationExample/Program.cs`：
- 创建对象，设置字符串、整数、布尔与嵌套对象
- 序列化为字节数组并反序列化回对象

快速开始
--------
1. 在代码中引用命名空间：
   ```csharp
   using Drx.Sdk.Shared.Serialization;
   ```
2. 创建并使用：
   ```csharp
   var d = new DrxSerializationData();
   d.SetString("name", "Alice");
   d.SetInt("age", 30);
   var bytes = d.Serialize();
   var d2 = DrxSerializationData.Deserialize(bytes);
   ```

下一步改进建议
- 支持流式 SerializeTo/DeserializeFrom（减少内存分配）
- 增加循环引用检测或引用表以支持共享引用
- 使用变长整数编码与字符串表以节省空间
- 提供不可变快照以便无锁序列化一致视图
- 添加单元测试覆盖并发/边界情形

许可证
----
项目内置许可遵循仓库约定。