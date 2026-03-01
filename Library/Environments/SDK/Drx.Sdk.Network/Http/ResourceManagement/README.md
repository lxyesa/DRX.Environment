# ResourceManagement 目录 - 资源上传/下载管理

## 概述
ResourceManagement 目录实现了完整的资源上传和下载管理系统，支持断点续传、进度跟踪、元数据管理等功能。

## 文件说明

### 上传相关文件

#### ResourceUploadContext.cs
**资源上传上下文**
- 表示一个上传任务的状态和元数据
- 特点：
  - 生命周期回调
  - 进度跟踪
  - 元数据存储
  - 错误处理

**主要属性：**
- `UploadId` - 唯一上传 ID
- `FileName` - 文件名
- `FileSize` - 文件总大小
- `UploadedBytes` - 已上传字节数
- `Status` - 上传状态（进行中、完成、失败）
- `Metadata` - 上传元数据
- `CreatedAt` - 创建时间
- `LastActivityAt` - 最后活动时间

#### UploadStatus.cs
**上传状态**
- 枚举类型，表示上传的各种状态
- 状态值：
  - `Pending` - 等待中
  - `Uploading` - 上传中
  - `Paused` - 已暂停
  - `Completed` - 已完成
  - `Failed` - 已失败

### 下载相关文件

#### ResourceDownloadContext.cs
**资源下载上下文**
- 表示一个下载任务的状态和元数据
- 特点：
  - 生命周期回调
  - 进度跟踪
  - 元数据存储
  - 错误处理

**主要属性：**
- `DownloadId` - 唯一下载 ID
- `ResourceUrl` - 资源 URL
- `DestinationPath` - 目标路径
- `FileSize` - 文件总大小
- `DownloadedBytes` - 已下载字节数
- `Status` - 下载状态
- `Metadata` - 下载元数据
- `CreatedAt` - 创建时间
- `LastActivityAt` - 最后活动时间

#### DownloadStatus.cs
**下载状态**
- 枚举类型，表示下载的各种状态
- 状态值：
  - `Pending` - 等待中
  - `Downloading` - 下载中
  - `Paused` - 已暂停
  - `Completed` - 已完成
  - `Failed` - 已失败

### 元数据相关文件

#### ResourceIndexDocument.cs
**资源索引文档**
- 存储资源的元数据和索引信息
- 支持资源搜索和分类

**包含信息：**
- 资源列表
- 资源属性
- 分类标签
- 创建/修改时间

#### ResourceIndexEntry.cs
**资源索引条目**
- 表示一个资源的索引信息
- 特点：
  - 快速查询
  - 元数据完整

**主要属性：**
- `ResourceId` - 资源 ID
- `FileName` - 文件名
- `FileSize` - 文件大小
- `MimeType` - MIME 类型
- `Checksum` - 文件校验和
- `CreatedAt` - 创建时间
- `ModifiedAt` - 修改时间

#### ResourceIndexManager.cs
**资源索引管理器**
- 管理资源索引的创建、更新和查询
- 支持快速检索

## 生命周期

### 上传生命周期
```
上传开始 (OnUploadStart)
    ↓
数据传输 (进度更新)
    ↓
块验证
    ↓
块存储
    ↓
上传继续...
    ↓
最后验证
    ↓
上传完成 (OnUploadComplete)
    或
上传失败 (OnUploadFailed)
```

### 下载生命周期
```
下载开始 (OnDownloadStart)
    ↓
获取资源
    ↓
数据传输 (进度更新)
    ↓
块接收
    ↓
块保存
    ↓
下载继续...
    ↓
完整性验证
    ↓
下载完成 (OnDownloadComplete)
    或
下载失败 (OnDownloadFailed)
```

## 高级功能

### 1. 断点续传
- 记录已传输的字节
- 暂停后继续
- 恢复机制

### 2. 进度跟踪
- 实时字节数报告
- 百分比计算
- 传输速度估算

### 3. 完整性验证
- MD5/SHA256 校验
- 文件大小验证
- 内容验证

### 4. 元数据管理
- 存储额外信息
- 快速查询
- 资源分类

## 使用场景

1. **大文件上传** - 支持 GB 级文件
2. **大文件下载** - 断点续传和进度显示
3. **文件备份** - 备份到服务器
4. **媒体库** - 管理图片、视频等
5. **日志收集** - 收集客户端日志
6. **软件更新** - 发布和下载更新包

## 与其他模块的关系

- **与 Server 的关系** - Server 集成资源管理
- **与 Client 的关系** - Client 使用上传/下载 API
- **与 Protocol 的关系** - 使用 HttpRequest/HttpResponse
- **与 Performance 的关系** - 进度流包装

## 最佳实践

1. **分块上传** - 大文件分块处理，提高可靠性
2. **压缩** - 传输前压缩减少带宽
3. **并发** - 多线程并发下载提高速度
4. **验证** - 传输后验证完整性
5. **清理** - 定期清理过期的上传/下载记录
6. **监控** - 监控上传/下载队列状态
7. **限制** - 设置文件大小和数量限制

## 配置建议

```csharp
// 配置上传限制
var uploadContext = new ResourceUploadContext
{
    MaxFileSize = 5_000_000_000, // 5GB
    MaxConcurrentUploads = 5,
    ChunkSize = 1_000_000 // 1MB 分块
};

// 配置下载限制
var downloadContext = new ResourceDownloadContext
{
    MaxConcurrentDownloads = 10,
    ChunkSize = 1_000_000,
    EnableResumeSupport = true
};
```

## 相关文档
- 参见 [../Client/README.md](../Client/README.md) 了解客户端 API
- 参见 [../Server/README.md](../Server/README.md) 了解服务器集成
