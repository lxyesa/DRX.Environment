## 用户头像本地缓存系统

### 概述
实现了一个高效的用户头像本地缓存系统，避免频繁的磁盘 I/O 操作，提升头像请求的响应速度。

### 架构设计

#### 1. 缓存管理器 (`AvatarCacheManager.cs`)
- **位置**: `Cache/AvatarCacheManager.cs`
- **功能**: 管理内存中的头像缓存
- **特性**:
  - LRU（最近最少使用）驱逐策略
  - 自动过期机制（默认 1 小时）
  - 线程安全的并发访问
  - 可配置的最大缓存条目数（默认 100）

#### 2. 二进制结果类 (`BytesResult.cs`)
- **位置**: `Results/BytesResult.cs`
- **功能**: 返回二进制数据的 HTTP 响应
- **用途**: 将缓存的头像字节数据返回给客户端

#### 3. HTTP 处理器集成 (`KaxHttp.cs`)
- **缓存实例**: 静态单例 `_avatarCache`
- **集成点**:
  - `Get_UserAvatar`: 优先从缓存读取，缓存未命中时从磁盘读取并缓存
  - `Post_UploadUserAvatar`: 上传新头像后清除该用户的缓存

### 工作流程

```
用户请求头像
    ↓
检查缓存是否存在且未过期
    ↓
[缓存命中] → 直接返回缓存数据（快速）
    ↓
[缓存未命中] → 从磁盘读取 → 存入缓存 → 返回数据
    ↓
用户上传新头像
    ↓
保存到磁盘 → 清除缓存 → 返回成功
```

### 性能优势

| 场景 | 无缓存 | 有缓存 |
|------|-------|-------|
| 首次请求 | 磁盘 I/O | 磁盘 I/O + 缓存存储 |
| 后续请求（同用户） | 磁盘 I/O | 内存读取（极快） |
| 高并发请求 | 多次磁盘 I/O | 单次磁盘 I/O + 多次内存读取 |

### 配置参数

在 `KaxHttp.cs` 中修改缓存实例化参数：

```csharp
private static readonly AvatarCacheManager _avatarCache = 
    new(maxCacheSize: 100, cacheExpirationSeconds: 3600);
```

- `maxCacheSize`: 最大缓存条目数（默认 100）
  - 每个条目存储一个用户的头像
  - 超过限制时自动驱逐最久未使用的条目
  
- `cacheExpirationSeconds`: 缓存过期时间（秒，默认 3600 = 1 小时）
  - 过期后自动从缓存移除
  - 下次请求时重新从磁盘加载

### 缓存清除场景

1. **用户上传新头像**: 自动清除旧缓存
2. **缓存过期**: 自动移除过期条目
3. **缓存满**: 驱逐最久未使用的条目
4. **手动清除**: 调用 `_avatarCache.Clear()` 清空所有缓存

### 内存占用估算

- 单个头像平均大小: ~50-200 KB
- 最大缓存条目数: 100
- 最大内存占用: ~5-20 MB（可根据需求调整）

### 线程安全

所有缓存操作都使用 `lock` 保护，确保多线程环境下的数据一致性。

### 监控与调试

获取缓存统计信息：

```csharp
var (cacheCount, maxSize) = _avatarCache.GetStats();
Logger.Info($"缓存统计: {cacheCount}/{maxSize}");
```

### 相关文件

- `Cache/AvatarCacheManager.cs` - 缓存管理器实现
- `Results/BytesResult.cs` - 二进制响应类
- `Handlers/KaxHttp.cs` - HTTP 处理器集成
- `Model/DataModel.cs` - 用户数据模型（无需修改）
