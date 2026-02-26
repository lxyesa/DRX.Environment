## KaxSocket — HTTP API 使用说明书（摘自 `KaxHttp.cs`） ✅

简短说明：下面列出当前服务器实现的所有公开 HTTP 接口、认证/权限要求、请求/响应示例、速率限制与常见错误。按需拷贝示例 curl 请求即可测试。💡
警告：该条目使用AI生成。

---

## 快速一览（端点索引）
| 路径 | 方法 | 认证 | 速率限制 | 用途 |
|---|---:|---|---:|---|
| `/api/user/register` | POST | 无 | 3 / 60s | 注册用户 |
| `/api/user/login` | POST | 无 | 5 / 60s | 登录，返回 `login_token` |
| `/api/user/verify/account` | POST | Bearer token | 60 / 60s | 验证令牌并返回权限信息 |
| `/api/user/profile` | GET | Bearer token | 60 / 60s | 获取当前登录用户的资料 |
| `/api/user/profile/{uid}` | GET | Bearer token | 60 / 60s | 获取指定用户的资料（公开信息） |
| `/api/user/profile` | POST | Bearer token | 10 / 60s | 更新当前用户的资料（需 targetUid 参数） |
| `/api/user/password` | POST | Bearer token | 6 / 60s | 修改用户密码 |
| `/api/user/avatar/{userId}` | GET | 无（公开） | 120 / 60s | 获取用户头像 |
| `/api/user/avatar` | POST | Bearer token | 10 / 60s | 上传用户头像 |
| `/api/user/stats` | GET | Bearer token | 60 / 60s | 获取用户统计信息 |
| `/api/user/unban?{userName}?{dev_code}` | POST | 无（需 dev_code） | — | 解除封禁（开发者码） |
| `/api/user/verify/asset/{assetId}` | GET | Bearer token | 60 / 60s | 校验用户是否拥有指定 asset |
| `/api/user/verify/asset/{assetId}/raw` | GET | Bearer token | 60 / 60s | 返回用户对 asset 的原始激活记录（activatedAt / expiresAt） |
| `/api/user/verify/asset/{assetId}/remaining` | GET | Bearer token | 60 / 60s | 返回 asset 的剩余时间（秒），永久返回 -1 |
| `/api/user/assets/active` | GET | Bearer token | 60 / 60s | 获取当前用户的激活资源列表 |
| `/api/user/favorites` | GET | Bearer token | 60 / 60s | 获取用户收藏列表 |
| `/api/user/favorites` | POST | Bearer token | 60 / 60s | 添加资源到收藏 |
| `/api/user/favorites/{assetId}` | DELETE | Bearer token | 60 / 60s | 从收藏中移除资源 |
| `/api/user/cart` | GET | Bearer token | 60 / 60s | 获取购物车 |
| `/api/user/cart` | POST | Bearer token | 60 / 60s | 添加资源到购物车 |
| `/api/user/cart/{assetId}` | DELETE | Bearer token | 60 / 60s | 从购物车移除资源 |
| `/api/shop/purchase` | POST | Bearer token | 20 / 60s | 购买资源 |
| `/api/cdk/activate` | POST | Bearer token | 20 / 60s | 激活 CDK 代码 |
| `/api/cdk/admin/inspect` | POST | Bearer token (Admin) | 60 / 60s | 检查 CDK 信息 |
| `/api/cdk/admin/generate` | POST | Bearer token (Admin) | 10 / 60s | 生成 CDK 代码 |
| `/api/cdk/admin/save` | POST | Bearer token (Admin) | 5 / 60s | 保存 CDK 代码 |
| `/api/cdk/admin/delete` | POST | Bearer token (Admin) | 180 / 60s | 删除 CDK 代码 |
| `/api/cdk/admin/list` | GET | Bearer token (Admin) | 60 / 60s | 列出 CDK 代码 |
| `/api/asset/admin/create` | POST | Bearer token (Admin) | 10 / 60s | 创建资源 |
| `/api/asset/admin/update` | POST | Bearer token (Admin) | 10 / 60s | 更新资源 |
| `/api/asset/admin/inspect` | POST | Bearer token (Admin) | 60 / 60s | 查询资源详情 |
| `/api/asset/admin/delete` | POST | Bearer token (Admin) | 10 / 60s | 删除资源（软删除） |
| `/api/asset/admin/restore` | POST | Bearer token (Admin) | 10 / 60s | 恢复资源 |
| `/api/asset/admin/list` | GET | Bearer token (Admin) | 无限制 | 列出资源（分页） |
| `/api/asset/list` | GET | 无（公开） | 60 / 60s | 获取资源列表 |
| `/api/asset/category/{category}` | GET | 无（公开） | 60 / 60s | 按分类获取资源 |
| `/api/asset/name/{assetId}` | GET | 无（公开） | 120 / 60s | 通过 assetId 获取资源名 |
| `/api/asset/detail/{id}` | GET | 无（公开） | 120 / 60s | 获取资源详情 |
| `/api/asset/{assetId}/plans` | GET | Bearer token | 60 / 60s | 获取资源的套餐列表 |
| `/api/asset/{assetId}/changePlan` | POST | Bearer token | 10 / 60s | 更变资源套餐 |
| `/api/asset/{assetId}/unsubscribe` | POST | Bearer token | 10 / 60s | 取消资源订阅 |

---

## 认证 & 权限
- 认证：在受保护接口中使用 `Authorization: Bearer <login_token>`。token 来自 `/api/user/login` 返回的 `login_token`。
- 管理权限（CDK / Asset 管理）：用户需属于权限组 `Console`、`Root` 或 `Admin`（由后端 `IsCdkAdminUser` / `IsAssetAdminUser` 校验）。
- 被封禁用户：若账号被封禁会收到 HTTP 403（Forbidden）。

> 重要：触发速率限制时框架会调用 `RateLimitCallback` —— 若短时间内过多（count > 20），会**自动封禁用户 60 秒**。

---

## 详细接口说明（带示例）

### 1) 用户注册 — POST /api/user/register
- 请求体（JSON）:
  ```json
  { "username":"alice", "password":"P@ssword1", "email":"a@ex.com" }
  ```
- 验证：
  - username 长度 5–12
  - password 最少 8
  - email 合法
- 成功：201 "注册成功。"
- 常见错误：400（格式/字段）、409（用户名或邮箱已注册）、500（服务器错误）
- 速率：3 次 / 60 秒

---

### 2) 用户登录 — POST /api/user/login
- 请求体（JSON）:
  ```json
  { "username":"alice", "password":"P@ssword1" }
  ```
- 成功响应（200）:
  ```json
  { "message":"登录成功。", "login_token":"<JWT>" }
  ```
- 错误：401（用户名或密码错误）、400（请求体为空）
- 速率：5 次 / 60 秒

示例（获取 token 后调用受保护接口）：
- curl 登录：
  curl -X POST -H "Content-Type: application/json" -d '{"username":"alice","password":"..."}' http://host/api/user/login

---

### 3) 验证登录与权限 — POST /api/user/verify/account
- 认证：必须带 `Authorization: Bearer <token>`
- 返回示例：
  ```json
  {
    "message":"令牌有效，欢迎您！",
    "user":"alice",
    "permissionGroup":2,
    "isAdmin": true
  }
  ```
- 错误：401（无效令牌）、403（账号被封禁）、404（用户不存在）
- 速率：60 次 / 60 秒（触发回调）

---

### 3.5) 获取用户资料 — GET /api/user/profile 与 GET /api/user/profile/{uid}
- 认证：必须带 `Authorization: Bearer <token>`
- 说明：
  - `GET /api/user/profile` — 返回当前登录用户的资料（完整信息）
  - `GET /api/user/profile/{uid}` — 返回指定 UID 用户的资料（公开信息）
- 返回示例：
  ```json
  {
    "id": 123,
    "user": "alice",
    "displayName": "Alice Smith",
    "email": "alice@example.com",
    "bio": "Software Engineer",
    "signature": "Best regards",
    "registeredAt": 1670000000,
    "lastLoginAt": 1670100000,
    "permissionGroup": 3,
    "isBanned": false,
    "bannedAt": 0,
    "banExpiresAt": 0,
    "banReason": "",
    "avatarUrl": "/api/user/avatar/123?v=1670100000",
    "resourceCount": 5,
    "gold": 100,
    "recentActivity": 10,
    "cdkCount": 3
  }
  ```
- 错误：401（未授权）、403（账号被封禁）、404（用户不存在）
- 速率：60 次 / 60 秒（触发回调）

---

### 3.6) 更新用户资料 — POST /api/user/profile
- 认证：必须带 `Authorization: Bearer <token>`
- 请求体（JSON）：
  ```json
  {
    "displayName": "Alice Smith",
    "email": "alice@example.com",
    "bio": "Software Engineer",
    "signature": "Best regards",
    "targetUid": 123
  }
  ```
- 参数说明：
  - `displayName`（可选）：显示名称，1–100 字符
  - `email`（可选）：电子邮箱，必须合法且唯一
  - `bio`（可选）：个人简介，最多 500 字符
  - `signature`（可选）：签名，最多 200 字符
  - `targetUid`（必填）：目标用户 ID，**必须与当前登录用户 ID 一致**，否则返回 403
- 成功响应（200）：
  ```json
  { "message": "资料已更新" }
  ```
- 权限验证：
  - 若 `targetUid` 与当前用户 ID 不一致，返回 **403 Forbidden**（无权修改他人资料）
  - 若 `targetUid` 参数缺失或无效，返回 **400 Bad Request**
- 错误：400（参数无效）、401（未授权）、403（无权修改他人资料）、404（用户不存在）、409（邮箱已被占用）
- 速率：10 次 / 60 秒（触发回调）

---

### 4) 开发者解除封禁 — POST /api/user/unban?{userName}?{dev_code}
- 路径参数：`userName`, `dev_code`（必须为 `yuerzuikeai001`）
- 注意：无登录即可调用（仅靠 dev_code）——仅限开发/运维工具使用
- 成功：200，403（dev_code 不正确）

---

### 5) 校验用户是否拥有资源 — GET /api/user/verify/asset/{assetId}
- 认证：Bearer token
- 返回（HTTP 200）样例：
  - 拥有： `{ "assetId": 123, "has": true, "code": 0 }`
  - 不拥有： `{ "assetId": 123, "has": false, "code": 2004 }`
- 参数：`assetId` 必须为 > 0 的整数
- 错误：401（未授权）、403（被封禁）、400（assetId 无效）
- 速率：60 次 / 60 秒（触发回调）

---

### 6) 获取当前用户的激活资源列表 — GET /api/user/assets/active
- 认证：Bearer token
- 返回（HTTP 200）样例：
   ```json
   {
      "code": 0,
      "message": "成功",
      "data": [
         { "id": 1, "assetId": 123, "activatedAt": 1670000000000, "expiresAt": 0, "remainingSeconds": -1 },
         { "id": 2, "assetId": 124, "activatedAt": 1670001000000, "expiresAt": 1672593000000, "remainingSeconds": 2592000 }
      ]
   }
   ```
- 字段说明：
   - id: 激活记录 id（内部使用）
   - assetId: 资源 id
   - activatedAt / expiresAt: 以毫秒为单位的时间戳（UTC）
   - remainingSeconds: 剩余秒数；若永久则为 -1
- 错误：401（未授权）、403（被封禁）、404（用户不存在）
- 速率：60 次 / 60 秒

---

### 7) 返回资产原始激活记录（raw）— GET /api/user/verify/asset/{assetId}/raw
- 认证：Bearer token
- 返回（HTTP 200）样例：
   - 拥有： `{ "assetId": 123, "activatedAt": 1670000000000, "expiresAt": 0, "has": true, "code": 0 }`
   - 不拥有： `{ "assetId": 123, "activatedAt": 0, "expiresAt": 0, "has": false, "code": 2004 }`
- 参数：`assetId` 必须为 > 0 的整数
- 错误：401（未授权）、403（被封禁）、400（assetId 无效）
- 速率：60 次 / 60 秒

---

### 8) 返回资产剩余时间 — GET /api/user/verify/asset/{assetId}/remaining
- 认证：Bearer token
- 返回（HTTP 200）样例：
   - 拥有且永久： `{ "assetId": 123, "has": true, "remainingSeconds": -1, "code": 0 }`
   - 拥有但已过期： `{ "assetId": 123, "has": false, "remainingSeconds": 0, "code": 2004 }`
   - 未拥有： `{ "assetId": 123, "has": false, "remainingSeconds": 0, "code": 2004 }`
- 说明：当资源为永久时返回 remainingSeconds = -1
- 错误：401（未授权）、403（被封禁）、400（assetId 无效）
- 速率：60 次 / 60 秒

---

### 9) 通过 assetId 获取资源名 — GET /api/asset/name/{assetId}
- 认证：公开（无需 token），用于前端在无需鉴权场景下显示资源名
- 返回（HTTP 200）样例： `{ "assetId": 123, "name": "My Resource", "code": 0 }`
- 资源不存在时返回 HTTP 404 携带 `{ "assetId": 123, "name": "", "code": 2004 }`
- 参数：`assetId` 必须为 > 0 的整数
- 错误：400（assetId 无效）、500（服务器错误）
- 速率：120 次 / 60 秒（较高的公开读取限流）

示例（无需登录）：
```
curl http://host/api/asset/name/123
```

---

### CDK 管理（需管理员权限）
- 公共说明：管理员组（Console/Root/Admin）可调用以下接口。

1. POST /api/cdk/admin/inspect  
   - Body: `{ "code": "ABC123" }`  
   - 返回：是否包含、映射信息（assetId、description、isUsed、usedBy）

2. POST /api/cdk/admin/generate  
   - Body 支持：`prefix`、`count`（1..1000）`length`（4..256）  
   - 返回：`{ "codes": [ "PREFIXXXXX", ... ] }`

3. POST /api/cdk/admin/save  
   - Body 可为 `codes` 数组，或使用 `prefix`/`count`/`length` 生成再保存  
   - 必须包含 `assetId`（>0），可选 `description`  
   - 返回：保存数量（若新增记录 >0 返回 201）

4. POST /api/cdk/admin/delete  
   - Body: `{ "code": "ABC123" }`  
   - 删除时做大小写不敏感匹配，返回删除数量

5. GET /api/cdk/admin/list  
   - 返回最近最多 200 条 CDK：`{ code, isUsed, createdAt, assetId, description }`

- 速率：各接口以 attribute 标注（多数为 60 次/60s 或更严格）

---

### Asset（资源）管理（需管理员权限）
1. POST /api/asset/admin/create  
   - Body: `{ name, version, author, description? }`  
   - 验证：name 1–100、version ≤50、author ≤100、description ≤500  
   - 返回：创建成功与 `id`

2. POST /api/asset/admin/update  
   - Body: `{ id, version?, author?, description? }`  
   - 更新 `LastUpdatedAt`

3. POST /api/asset/admin/inspect  
   - Body: `{ id }` -> 返回 asset 详情（name/version/author/…）

4. POST /api/asset/admin/delete  
   - Body: `{ id }` -> 软删除（IsDeleted = true, DeletedAt = now）

5. POST /api/asset/admin/restore  
   - Body: `{ id }` -> 恢复软删除

6. GET /api/asset/admin/list  
   - Query: `q`, `author`, `version`, `page` (默认1), `pageSize` (默认20), `includeDeleted` (默认 false)  
   - 返回分页 `{ data: [...], page, pageSize, total }`  
   - 速率限制：无（RateLimitMaxRequests = 0）

---

### 10) 修改用户密码 — POST /api/user/password
- 认证：Bearer token
- 请求体（JSON）：
  ```json
  {
    "oldPassword": "OldPass123",
    "newPassword": "NewPass456",
    "targetUid": 123
  }
  ```
- 参数说明：
  - `oldPassword`（必填）：当前密码
  - `newPassword`（必填）：新密码，最少 8 字符
  - `targetUid`（必填）：目标用户 ID，**必须与当前登录用户 ID 一致**
- 成功响应（200）：`{ "message": "密码已更新" }`
- 错误：400（参数无效）、401（未授权或旧密码错误）、403（无权修改他人密码）、404（用户不存在）
- 速率：6 次 / 60 秒（严格限制）

---

### 11) 获取用户头像 — GET /api/user/avatar/{userId}
- 认证：公开（无需 token）
- 说明：返回用户头像图片文件（二进制）
- 参数：`userId` 必须为有效的用户 ID
- 成功：HTTP 200，Content-Type: image/*
- 错误：404（用户不存在或无头像）、400（userId 无效）
- 速率：120 次 / 60 秒（较高的公开读取限流）

---

### 12) 上传用户头像 — POST /api/user/avatar
- 认证：Bearer token
- 说明：上传用户头像图片（multipart/form-data）
- 请求格式：
  ```
  POST /api/user/avatar
  Authorization: Bearer <token>
  Content-Type: multipart/form-data
  
  file: <image file>
  targetUid: 123
  ```
- 参数说明：
  - `file`（必填）：图片文件，支持 jpg/png/gif，最大 5MB
  - `targetUid`（必填）：目标用户 ID，**必须与当前登录用户 ID 一致**
- 成功响应（200）：`{ "message": "头像已上传", "avatarUrl": "/api/user/avatar/123?v=1670100000" }`
- 错误：400（文件无效或过大）、401（未授权）、403（无权修改他人头像）、404（用户不存在）
- 速率：10 次 / 60 秒

---

### 13) 获取用户统计信息 — GET /api/user/stats
- 认证：Bearer token
- 说明：返回当前用户的统计数据（资源数、金币、活跃度等）
- 返回示例（HTTP 200）：
  ```json
  {
    "userId": 123,
    "username": "alice",
    "resourceCount": 5,
    "gold": 1000,
    "recentActivity": 42,
    "cdkCount": 3,
    "favoriteCount": 10,
    "cartCount": 2,
    "totalPurchases": 15,
    "registeredDaysAgo": 180
  }
  ```
- 错误：401（未授权）、403（被封禁）、404（用户不存在）
- 速率：60 次 / 60 秒

---

### 14) 获取用户收藏列表 — GET /api/user/favorites
- 认证：Bearer token
- 返回示例（HTTP 200）：
  ```json
  {
    "code": 0,
    "message": "成功",
    "data": [
      { "assetId": 123, "name": "Resource A", "addedAt": 1670000000000 },
      { "assetId": 124, "name": "Resource B", "addedAt": 1670001000000 }
    ]
  }
  ```
- 错误：401（未授权）、403（被封禁）、404（用户不存在）
- 速率：60 次 / 60 秒

---

### 15) 添加资源到收藏 — POST /api/user/favorites
- 认证：Bearer token
- 请求体（JSON）：`{ "assetId": 123 }`
- 成功响应（200）：`{ "message": "已添加到收藏" }`
- 错误：400（assetId 无效）、401（未授权）、403（被封禁）、404（资源不存在）、409（已在收藏中）
- 速率：60 次 / 60 秒

---

### 16) 从收藏中移除资源 — DELETE /api/user/favorites/{assetId}
- 认证：Bearer token
- 成功响应（200）：`{ "message": "已从收藏中移除" }`
- 错误：400（assetId 无效）、401（未授权）、403（被封禁）、404（资源不在收藏中）
- 速率：60 次 / 60 秒

---

### 17) 获取购物车 — GET /api/user/cart
- 认证：Bearer token
- 返回示例（HTTP 200）：
  ```json
  {
    "code": 0,
    "message": "成功",
    "data": [
      { "assetId": 123, "name": "Resource A", "price": 99, "addedAt": 1670000000000 },
      { "assetId": 124, "name": "Resource B", "price": 199, "addedAt": 1670001000000 }
    ],
    "totalPrice": 298
  }
  ```
- 错误：401（未授权）、403（被封禁）、404（用户不存在）
- 速率：60 次 / 60 秒

---

### 18) 添加资源到购物车 — POST /api/user/cart
- 认证：Bearer token
- 请求体（JSON）：`{ "assetId": 123 }`
- 成功响应（200）：`{ "message": "已添加到购物车" }`
- 错误：400（assetId 无效）、401（未授权）、403（被封禁）、404（资源不存在）、409（已在购物车中）
- 速率：60 次 / 60 秒

---

### 19) 从购物车移除资源 — DELETE /api/user/cart/{assetId}
- 认证：Bearer token
- 成功响应（200）：`{ "message": "已从购物车中移除" }`
- 错误：400（assetId 无效）、401（未授权）、403（被封禁）、404（资源不在购物车中）
- 速率：60 次 / 60 秒

---

### 20) 购买资源 — POST /api/shop/purchase
- 认证：Bearer token
- 请求体（JSON）：
  ```json
  {
    "assetIds": [123, 124],
    "planIds": [1, 2]
  }
  ```
- 参数说明：
  - `assetIds`（必填）：资源 ID 数组
  - `planIds`（可选）：对应的套餐 ID 数组，若不指定则使用默认套餐
- 成功响应（200）：
  ```json
  {
    "message": "购买成功",
    "orderId": "ORD20231201001",
    "totalPrice": 298,
    "purchasedAssets": [123, 124]
  }
  ```
- 错误：400（参数无效）、401（未授权）、403（被封禁或余额不足）、404（资源不存在）
- 速率：20 次 / 60 秒

---

### 21) 激活 CDK 代码 — POST /api/cdk/activate
- 认证：Bearer token
- 请求体（JSON）：`{ "code": "ABC123XYZ" }`
- 成功响应（200）：
  ```json
  {
    "message": "CDK 已激活",
    "assetId": 123,
    "assetName": "Resource A",
    "activatedAt": 1670000000000,
    "expiresAt": 0
  }
  ```
- 错误：400（code 无效）、401（未授权）、403（被封禁或 CDK 已被使用）、404（CDK 不存在）
- 速率：20 次 / 60 秒

---

### 22) 获取资源列表 — GET /api/asset/list
- 认证：公开（无需 token）
- Query 参数：
  - `page`（可选，默认 1）：页码
  - `pageSize`（可选，默认 20）：每页数量
  - `category`（可选）：按分类筛选
  - `search`（可选）：按名称搜索
- 返回示例（HTTP 200）：
  ```json
  {
    "code": 0,
    "message": "成功",
    "data": [
      { "id": 123, "name": "Resource A", "category": "tools", "author": "admin", "version": "1.0", "price": 99 },
      { "id": 124, "name": "Resource B", "category": "plugins", "author": "admin", "version": "2.0", "price": 199 }
    ],
    "page": 1,
    "pageSize": 20,
    "total": 100
  }
  ```
- 错误：400（参数无效）、500（服务器错误）
- 速率：60 次 / 60 秒

---

### 23) 按分类获取资源 — GET /api/asset/category/{category}
- 认证：公开（无需 token）
- 参数：`category` 为资源分类名称
- Query 参数：`page`、`pageSize` 同上
- 返回格式同 `/api/asset/list`
- 错误：400（分类无效）、404（分类不存在）
- 速率：60 次 / 60 秒

---

### 24) 获取资源详情 — GET /api/asset/detail/{id}
- 认证：公开（无需 token）
- 参数：`id` 为资源 ID
- 返回示例（HTTP 200）：
  ```json
  {
    "code": 0,
    "message": "成功",
    "data": {
      "id": 123,
      "name": "Resource A",
      "category": "tools",
      "author": "admin",
      "version": "1.0",
      "description": "A useful resource",
      "price": 99,
      "createdAt": 1670000000000,
      "lastUpdatedAt": 1670100000000,
      "downloadCount": 1000,
      "rating": 4.5,
      "plans": [
        { "id": 1, "name": "Basic", "price": 99, "duration": 30, "unit": "days" },
        { "id": 2, "name": "Premium", "price": 199, "duration": 365, "unit": "days" }
      ]
    }
  }
  ```
- 错误：400（id 无效）、404（资源不存在）
- 速率：120 次 / 60 秒

---

### 25) 获取资源的套餐列表 — GET /api/asset/{assetId}/plans
- 认证：Bearer token
- 参数：`assetId` 为资源 ID
- 返回示例（HTTP 200）：
  ```json
  {
    "code": 0,
    "message": "成功",
    "data": [
      { "id": 1, "name": "Basic", "price": 99, "duration": 30, "unit": "days", "description": "30 days access" },
      { "id": 2, "name": "Premium", "price": 199, "duration": 365, "unit": "days", "description": "1 year access" },
      { "id": 3, "name": "Lifetime", "price": 499, "duration": 0, "unit": "permanent", "description": "Permanent access" }
    ]
  }
  ```
- 错误：400（assetId 无效）、401（未授权）、403（被封禁）、404（资源不存在）
- 速率：60 次 / 60 秒

---

### 26) 更变资源套餐 — POST /api/asset/{assetId}/changePlan
- 认证：Bearer token
- 参数：`assetId` 为资源 ID
- 请求体（JSON）：
  ```json
  {
    "newPlanId": 2,
    "targetUid": 123
  }
  ```
- 参数说明：
  - `newPlanId`（必填）：新套餐 ID
  - `targetUid`（必填）：目标用户 ID，**必须与当前登录用户 ID 一致**
- 成功响应（200）：
  ```json
  {
    "message": "套餐已更新",
    "assetId": 123,
    "newPlanId": 2,
    "newExpiresAt": 1672593000000,
    "costGold": 100
  }
  ```
- 错误：400（参数无效）、401（未授权）、403（被封禁或无权修改）、404（资源或套餐不存在）、409（用户未拥有该资源）
- 速率：10 次 / 60 秒

---

### 27) 取消资源订阅 — POST /api/asset/{assetId}/unsubscribe
- 认证：Bearer token
- 参数：`assetId` 为资源 ID
- 请求体（JSON）：
  ```json
  {
    "targetUid": 123
  }
  ```
- 参数说明：
  - `targetUid`（必填）：目标用户 ID，**必须与当前登录用户 ID 一致**
- 成功响应（200）：
  ```json
  {
    "message": "订阅已取消",
    "assetId": 123,
    "refundGold": 50
  }
  ```
- 错误：400（参数无效）、401（未授权）、403（被封禁或无权修改）、404（资源不存在或用户未拥有）
- 速率：10 次 / 60 秒

---

## 常见 HTTP 状态码 & 业务码
- 200 — 成功（一般 JSON 返回）
- 201 — 已创建（例如 CDK/资源成功保存）
- 400 — 请求格式或字段验证失败
- 401 — 未认证 / 令牌无效
- 403 — 权限不足或账号被封禁
- 404 — 资源未找到
- 409 — 冲突（用户名/邮箱已存在）
- 429 — 请求过于频繁（触发速率限制）
- 500 — 服务器内部错误

业务码：
- `/api/user/verify/asset` 返回 `code: 0` 表示拥有，`code: 2004` 表示未拥有。

---

## 使用示例（登录后调用受保护接口）
1. 登录并取 token：
   curl -X POST -H "Content-Type: application/json" -d '{"username":"alice","password":"..."}' http://host/api/user/login
2. 使用 token 调用受保护接口：
   curl -H "Authorization: Bearer <token>" http://host/api/user/verify/account

---

## 注意与建议 🛡️
- 所有敏感通信请走 HTTPS；不要在客户端硬编码 `login_token`。
- 管理接口仅限 `Console/Root/Admin`；谨慎分配权限。
- `RateLimitCallback` 会在高频请求时自动临时封禁用户（count > 20 → 封禁 60 秒）。
- `user/unban` 接口使用固定开发者码，请仅在受控环境下使用。

---