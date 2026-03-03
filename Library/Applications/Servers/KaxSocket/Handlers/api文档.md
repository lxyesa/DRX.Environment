# KaxSocket HTTP API 文档

## 目录

1. [概述](#概述)
2. [全局配置](#全局配置)
3. [认证](#认证)
4. [API 端点](#api-端点)
   - [用户认证](#用户认证)
   - [用户资料](#用户资料)
   - [用户资产](#用户资产)
   - [资源管理](#资源管理)
   - [资源查询](#资源查询)
   - [资源验证](#资源验证)
   - [购物与订阅](#购物与订阅)
   - [订单管理](#订单管理)
   - [CDK 管理](#cdk-管理)
   - [系统控制台](#系统控制台)
5. [错误响应](#错误响应)
6. [命令系统](#命令系统)

---

## 概述

KaxSocket 是一个功能完整的资产/商品销售平台 HTTP 服务器，采用 **Partial Classes** 分离不同功能模块。

### 核心特性

- **认证方式**：JWT Bearer Token（有效期 1 小时）
- **速率限制**：支持端点级别的请求限制，超限自动封禁用户
- **权限管理**：基于权限组的访问控制（System > Console > Admin > User）
- **数据库**：SQLite V2 ORM + TableList 一对多关系
- **即时通信**：SSE 日志推送（控制台模块）

### 权限组说明

| 权限组 | 值 | 权限 |
|-------|---|------|
| System | 0 | 最高权限，系统内部使用 |
| Console | 1 | 控制台权限，可执行命令 |
| Admin | 2 | 管理员权限，可管理资源和 CDK |
| User | 3 | 普通用户权限 |
| Banned | 4 | 已封禁用户 |

---

## 全局配置

### JWT 配置

```csharp
JwtHelper.Configure(new JwtHelper.JwtConfig
{
    SecretKey = "A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6",
    Issuer = "KaxSocket",
    Audience = "KaxUsers",
    Expiration = TimeSpan.FromHours(1)
});
```

### 速率限制回调

当请求超过限制时：
- **≤ 20次超限** → 返回 429 状态码
- **> 20次超限** → 自动封禁用户 60 秒

---

## 认证

### 令牌获取

所有需要认证的 API 都需要在请求头中提供 JWT 令牌：

```
Authorization: Bearer <token>
```

### 令牌生成

通过 `/api/user/login` 获取令牌后，在后续请求中使用。

---

## API 端点

### 商店域迁移冻结契约（2026-03）

> 本节优先级高于历史示例，用于 `shop` / `shop_detail` 页面迁移。实现与联调以本节为准。

#### 统一响应外壳

成功：
```json
{ "code": 0, "message": "成功", "data": {} }
```

失败：
```json
{ "code": 4000, "message": "错误描述", "data": null }
```

#### 1) 商品列表

```http
GET /api/asset/list?q=&category=&minPrice=&maxPrice=&sort=updated&page=1&pageSize=24
```

`data` 结构：
```json
{
  "items": [
    {
      "id": 1,
      "name": "资源名",
      "category": "工具",
      "coverImage": "https://...",
      "priceYuan": 9.9,
      "authorName": "作者",
      "purchaseCount": 300,
      "favoriteCount": 150
    }
  ],
  "total": 50,
  "page": 1,
  "pageSize": 24
}
```

#### 2) 商品详情

```http
GET /api/asset/detail/{id}
```

`data` 结构：
```json
{
  "id": 1,
  "name": "资源名",
  "description": "描述",
  "category": "工具",
  "coverImage": "https://...",
  "iconImage": "https://...",
  "screenshots": ["https://..."],
  "badges": ["热门"],
  "features": ["高性能"],
  "prices": [
    {
      "id": "price-1",
      "name": "月付",
      "priceYuan": 9.9,
      "originalPriceYuan": 19.9,
      "unit": "month",
      "duration": 1,
      "stock": -1
    }
  ]
}
```

#### 3) 相关推荐

```http
GET /api/asset/related/{id}?top=4
```

`data` 结构：
```json
[
  {
    "id": 2,
    "name": "相关资源",
    "category": "工具",
    "coverImage": "https://...",
    "priceYuan": 6.9
  }
]
```

#### 4) 收藏

```http
GET /api/user/favorites
POST /api/user/favorites
DELETE /api/user/favorites/{assetId}
```

`GET` 成功时 `data` 固定为：
```json
[101, 102, 103]
```

#### 5) 购物车

```http
GET /api/user/cart
POST /api/user/cart
DELETE /api/user/cart/{assetId}
```

`GET` 成功时 `data` 固定为：
```json
[
  { "assetId": 101, "priceId": "price-1", "quantity": 1 }
]
```

`POST` 请求体：
```json
{ "assetId": 101, "priceId": "price-1" }
```

#### 6) 购买

```http
POST /api/shop/purchase
```

请求体：
```json
{ "assetId": 101, "priceId": "price-1" }
```

成功时 `data`：
```json
{ "assetId": 101, "orderId": "order-abc", "remainingGold": 1200 }
```

#### 迁移约束

1. 商店域价格语义统一为“元（decimal）”。
2. 前端不再依赖 `id/assetId`、`data/items` 等历史多形态兼容路径。
3. 详情页统一路由为 `/asset/detail/{id}`。

### 用户认证

#### 1. 用户注册

```http
POST /api/user/register
Content-Type: application/json

{
  "username": "user123",
  "password": "password123",
  "email": "user@example.com"
}
```

**限制**：3 req/60s

**验证规则**：
- 用户名：5~12 字符
- 密码：≥ 8 字符
- 邮箱：有效格式

**响应**：
```json
{
  "StatusCode": 201,
  "Body": "注册成功。"
}
```

---

#### 2. 用户登录

```http
POST /api/user/login
Content-Type: application/json

{
  "username": "user123",
  "password": "password123"
}
```

**限制**：5 req/60s

**响应成功**：
```json
{
  "StatusCode": 200,
  "Body": {
    "message": "登录成功。",
    "login_token": "eyJhbGciOiJIUzI1NiIs..."
  }
}
```

**响应失败**：
```json
{
  "StatusCode": 401,
  "Body": "用户名或密码错误。"
}
```

---

#### 3. 账户验证

```http
POST /api/user/verify/account
Authorization: Bearer <token>
```

**限制**：60 req/60s

**功能**：验证当前登录令牌的有效性

**响应**：
```json
{
  "code": 0,
  "message": "验证成功"
}
```

---

### 用户资料

#### 1. 获取当前用户资料

```http
GET /api/user/profile
Authorization: Bearer <token>
```

**限制**：60 req/60s

**响应**：
```json
{
  "id": 1,
  "user": "user123",
  "displayName": "User Display",
  "email": "user@example.com",
  "bio": "个人简介",
  "signature": "个性签名",
  "registeredAt": 1700000000,
  "lastLoginAt": 1700000100,
  "permissionGroup": 3,
  "isBanned": false,
  "avatarUrl": "/api/user/avatar/1?v=1700000100",
  "resourceCount": 5,
  "gold": 1000,
  "recentActivity": 100,
  "cdkCount": 2
}
```

---

#### 2. 获取指定用户资料

```http
GET /api/user/profile/{uid}
Authorization: Bearer <token>
```

**限制**：60 req/60s

**参数**：
- `uid` (路径参数)：用户 ID

**响应**：同上（仅返回公开信息）

---

#### 3. 更新用户资料

```http
POST /api/user/profile
Authorization: Bearer <token>
Content-Type: application/json

{
  "targetUid": 1,
  "displayName": "新名称",
  "email": "newemail@example.com",
  "bio": "新的个人简介",
  "signature": "新的个性签名"
}
```

**限制**：10 req/60s

**说明**：该接口为**全量更新兼容接口**，适合一次更新多个字段。

**响应**：
```json
{
  "message": "资料已更新"
}
```

---

#### 4. 单字段更新用户资料

```http
POST /api/user/profile/update-field
Authorization: Bearer <token>
Content-Type: application/json

{
  "targetUid": 1,
  "field": "email",
  "value": "newemail@example.com"
}
```

**限制**：20 req/60s

**支持字段**：
- `displayName`
- `email`
- `bio`
- `signature`

**说明**：用于前端单项编辑（如只改邮箱），避免覆盖其它字段。

**响应**：
```json
{
  "message": "字段已更新",
  "field": "email",
  "value": "newemail@example.com"
}
```

---

#### 5. 修改密码

```http
POST /api/user/password
Authorization: Bearer <token>
Content-Type: application/json

{
  "oldPassword": "oldpass123",
  "newPassword": "newpass456"
}
```

**限制**：6 req/60s

**验证**：新密码长度 ≥ 8 字符

---

#### 6. 上传头像

```http
POST /api/user/avatar
Authorization: Bearer <token>
Content-Type: multipart/form-data

[二进制图像数据]
```

**限制**：10 req/60s

**支持格式**：JPG, PNG

**图像处理**：
- 自动转码为 PNG
- 保存路径为 `resources/user/icon/{uid}.png`

---

#### 7. 获取用户头像

```http
GET /api/user/avatar/{userId}?v={timestamp}
```

**限制**：120 req/60s

**缓存**：使用 LRU 缓存（100 项，1小时过期）

---

#### 8. 获取用户统计数据

```http
GET /api/user/stats
Authorization: Bearer <token>
```

**限制**：60 req/60s

**响应**：
```json
{
  "code": 0,
  "data": {
    "totalOrders": 25,
    "totalSpent": 50000,
    "activeAssets": 3,
    "favoriteCount": 12
  }
}
```

---

### 用户资产

#### 1. 获取激活资产列表

```http
GET /api/user/assets/active
Authorization: Bearer <token>
```

**限制**：60 req/60s

**响应**：
```json
{
  "code": 0,
  "message": "成功",
  "data": [
    {
      "id": "asset-1",
      "assetId": 101,
      "activatedAt": 1700000000000,
      "expiresAt": 1707000000000,
      "remainingSeconds": 604800,
      "isExpired": false
    }
  ]
}
```

---

#### 2. 获取用户收藏

```http
GET /api/user/favorites
Authorization: Bearer <token>
```

**限制**：60 req/60s

**响应**：
```json
{
  "code": 0,
  "message": "成功",
  "data": [101, 102, 103]
}
```

---

#### 3. 添加收藏

```http
POST /api/user/favorites
Authorization: Bearer <token>
Content-Type: application/json

{
  "assetId": 101
}
```

**限制**：60 req/60s

---

#### 4. 取消收藏

```http
DELETE /api/user/favorites/{assetId}
Authorization: Bearer <token>
```

**限制**：60 req/60s

---

#### 5. 获取购物车

```http
GET /api/user/cart
Authorization: Bearer <token>
```

**限制**：60 req/60s

**响应**：
```json
{
  "code": 0,
  "message": "成功",
  "data": [
    {
      "assetId": 101,
      "priceId": "price-1",
      "quantity": 1
    }
  ]
}
```

---

#### 6. 添加购物车

```http
POST /api/user/cart
Authorization: Bearer <token>
Content-Type: application/json

{
  "assetId": 101,
  "priceId": "price-1"
}
```

**限制**：60 req/60s

---

#### 7. 删除购物车项

```http
DELETE /api/user/cart/{assetId}
Authorization: Bearer <token>
```

**限制**：60 req/60s

---

### 资源管理

#### 1. 创建资源

```http
POST /api/asset/admin/create
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "资源名称",
  "version": "1.0.0",
  "author": "作者名",
  "description": "资源描述",
  "category": "工具",
  "prices": [
    {
      "originalPrice": 1000,
      "discountRate": 0.1,
      "unit": "month",
      "duration": 1,
      "stock": -1
    }
  ],
  "primaryImage": "https://...",
  "thumbnailImage": "https://...",
  "fileSize": 1024000,
  "license": "MIT",
  "downloadUrl": "https://...",
  "compatibility": "Windows 10+"
}
```

**限制**：10 req/60s

**权限**：Admin 或以上

**验证**：
- 资源名称：1~100 字符
- 版本号：1~50 字符
- 作者：1~100 字符
- 描述：≤ 500 字符

**响应**：
```json
{
  "message": "资源创建成功",
  "id": 1
}
```

---

#### 2. 更新资源

```http
POST /api/asset/admin/update
Authorization: Bearer <token>
Content-Type: application/json

{
  "id": 1,
  "name": "更新后的名称",
  "prices": [...],
  ...
}
```

**限制**：10 req/60s

**权限**：Admin 或以上

**说明**：该接口为**全量更新兼容接口**，适合一次更新多个字段。

---

#### 3. 单字段更新资源

```http
POST /api/asset/admin/update-field
Authorization: Bearer <token>
Content-Type: application/json

{
  "id": 1,
  "field": "category",
  "value": "工具"
}
```

**限制**：20 req/60s

**权限**：Admin 或以上

**支持字段（常用）**：
- 基础信息：`name` `version` `description` `category` `tags`
- 媒体信息：`coverImage` `iconImage` `screenshots`
- 展示扩展：`badges` `features`
- 规格字段：`fileSize` `rating` `reviewCount` `compatibility` `downloads` `uploadDate` `license` `downloadUrl`
- 价格方案：`prices`（数组）

**说明**：用于单项编辑（例如仅改分类、仅改下载地址）。

---

#### 4. 检查资源

```http
POST /api/asset/admin/inspect
Authorization: Bearer <token>
Content-Type: application/json

{
  "id": 1
}
```

**限制**：60 req/60s

**权限**：Admin 或以上

**功能**：获取资源完整信息（包括软删除状态）

---

#### 5. 删除资源

```http
POST /api/asset/admin/delete
Authorization: Bearer <token>
Content-Type: application/json

{
  "id": 1
}
```

**限制**：10 req/60s

**权限**：Admin 或以上

**功能**：软删除资源（标记 IsDeleted = true）

---

#### 6. 恢复资源

```http
POST /api/asset/admin/restore
Authorization: Bearer <token>
Content-Type: application/json

{
  "id": 1
}
```

**限制**：10 req/60s

**权限**：Admin 或以上

**功能**：恢复已删除的资源

---

#### 7. 管理员资源列表

```http
GET /api/asset/admin/list?page=1&pageSize=20&includeDeleted=false&q=搜索词
Authorization: Bearer <token>
```

**限制**：无限制

**权限**：Admin 或以上

**查询参数**：
- `page` (int)：页码，默认 1
- `pageSize` (int)：每页数量，默认 20
- `includeDeleted` (bool)：是否包含已删除资源
- `q` (string)：搜索关键词

**响应**：
```json
{
  "total": 100,
  "page": 1,
  "pageSize": 20,
  "data": [...]
}
```

---

### 资源查询

#### 1. 获取资源列表

```http
GET /api/asset/list?q=搜索词&category=工具&minPrice=0&maxPrice=10000&sort=updated&page=1&pageSize=24
```

**限制**：60 req/60s

**查询参数**：
- `q` (string)：关键词搜索（名称/描述）
- `category` (string)：分类筛选
- `minPrice` (int)：最低价格
- `maxPrice` (int)：最高价格
- `sort` (string)：排序方式
  - `updated`：最近更新（默认）
  - `price_asc`：价格升序
  - `price_desc`：价格降序
- `page` (int)：页码
- `pageSize` (int)：每页数量（默认 24）

**响应**：
```json
{
  "total": 50,
  "page": 1,
  "pageSize": 24,
  "data": [
    {
      "id": 1,
      "name": "资源名",
      "version": "1.0.0",
      "author": "作者",
      "description": "描述",
      "category": "工具",
      "primaryImage": "...",
      "thumbnailImage": "...",
      "fileSize": 1024000,
      "rating": 4.5,
      "reviewCount": 120,
      "downloads": 5000,
      "license": "MIT",
      "downloadUrl": "...",
      "purchaseCount": 300,
      "favoriteCount": 150,
      "viewCount": 10000,
      "lastUpdatedAt": 1700000000000
    }
  ]
}
```

---

#### 2. 按分类查询

```http
GET /api/asset/category/{category}
```

**限制**：60 req/60s

**参数**：
- `category` (路径参数)：分类名称

---

#### 3. 获取资源名称

```http
GET /api/asset/name/{assetId}
Authorization: Bearer <token>
```

**限制**：120 req/60s

**功能**：获取用户已激活资源的名称

---

#### 4. 获取资源详情

```http
GET /api/asset/detail/{id}
Authorization: Bearer <token>
```

**限制**：120 req/60s

**功能**：获取资源完整信息，自动增加浏览量

**响应**：
```json
{
  "code": 0,
  "message": "成功",
  "data": {
    "id": 1,
    "name": "资源名",
    "version": "1.0.0",
    "author": "作者",
    "description": "描述",
    "category": "工具",
    "isDeleted": false,
    "primaryImage": "...",
    "thumbnailImage": "...",
    "screenshots": ["...", "..."],
    "tags": ["高性能", "安全"],
    "prices": [
      {
        "id": "price-1",
        "parentId": 1,
        "createdAt": 1700000000000,
        "updatedAt": 1700000000000,
        "price": 900,
        "unit": "month",
        "duration": 1,
        "originalPrice": 1000,
        "discountRate": 0.1,
        "stock": -1
      }
    ],
    "specs": {
      "fileSize": 1024000,
      "rating": 4.5,
      "reviewCount": 120,
      "compatibility": "Windows 10+",
      "downloads": 5000,
      "uploadDate": 1700000000000,
      "license": "MIT",
      "downloadUrl": "...",
      "purchaseCount": 300,
      "favoriteCount": 150,
      "viewCount": 10000,
      "lastUpdatedAt": 1700000000000
    }
  }
}
```

---

#### 5. 获取相关推荐

```http
GET /api/asset/related/{id}?top=4
```

**限制**：60 req/60s

**查询参数**：
- `top` (int)：返回数量（1~20，默认 4）

**排序算法**：热度评分 = Downloads × 0.6 + Rating × 10 + PurchaseCount × 0.3

---

#### 6. 获取套餐列表

```http
GET /api/asset/{assetId}/plans
Authorization: Bearer <token>
```

**限制**：60 req/60s

**功能**：获取资源的所有价格方案

**响应**：
```json
{
  "code": 0,
  "message": "成功",
  "data": [
    {
      "id": "price-1",
      "price": 900,
      "unit": "month",
      "duration": 1,
      "originalPrice": 1000,
      "discountRate": 0.1,
      "stock": -1
    }
  ]
}
```

---

### 资源验证

#### 1. 验证用户资源

```http
GET /api/user/verify/asset/{assetId}
Authorization: Bearer <token>
```

**限制**：60 req/60s

**响应**：
```json
{
  "assetId": 1,
  "has": true,
  "code": 0
}
```

**响应码**：
- `0`：拥有该资源
- `2004`：未拥有

---

#### 2. 获取资源激活记录

```http
GET /api/user/verify/asset/{assetId}/raw
Authorization: Bearer <token>
```

**限制**：60 req/60s

**响应**：
```json
{
  "assetId": 1,
  "activatedAt": 1700000000000,
  "expiresAt": 1707000000000,
  "has": true,
  "code": 0
}
```

---

#### 3. 获取资源剩余时间

```http
GET /api/user/verify/asset/{assetId}/remaining
Authorization: Bearer <token>
```

**限制**：60 req/60s

**响应**：
```json
{
  "assetId": 1,
  "has": true,
  "remainingSeconds": 604800,
  "code": 0
}
```

**说明**：
- `remainingSeconds = -1`：永久激活
- `remainingSeconds > 0`：剩余秒数
- `remainingSeconds = 0`：已过期

---

### 购物与订阅

#### 1. 购买资源

```http
POST /api/shop/purchase
Authorization: Bearer <token>
Content-Type: application/json

{
  "assetId": 1,
  "priceId": "price-1",
  "durationOverride": 2592000000
}
```

**限制**：20 req/60s

**参数**：
- `assetId` (int)：资源 ID
- `priceId` (string)：价格方案 ID
- `durationOverride` (long, 可选)：自定义购买期限（毫秒），0 表示永久

**逻辑**：
1. 验证用户令牌和账户状态
2. 检查资源存在性和删除状态
3. 验证金币余额
4. 计算购买有效期
5. 已拥有则延长，已过期则重新激活
6. 扣除金币并记录订单

**响应成功**：
```json
{
  "code": 0,
  "message": "购买成功",
  "assetId": 1
}
```

**响应失败**：
```json
{
  "code": 403,
  "message": "金币不足，需要至少 900 点金币才能购买此资产"
}
```

---

#### 2. 切换订阅方案

```http
POST /api/asset/{assetId}/changePlan
Authorization: Bearer <token>
Content-Type: application/json

{
  "newPriceId": "price-2"
}
```

**限制**：10 req/60s

**功能**：
- 升级套餐：新套餐费用 - 当前套餐剩余价值
- 降级套餐：退款（以最小粒度计算）

---

#### 3. 取消订阅

```http
POST /api/asset/{assetId}/unsubscribe
Authorization: Bearer <token>
```

**限制**：10 req/60s

**功能**：
1. 移除用户的激活资源
2. 记录订单
3. 处理部分退款（按剩余时长比例）

---

### 订单管理

#### 1. 获取用户订单

```http
GET /api/user/orders?page=1&pageSize=50
Authorization: Bearer <token>
```

**限制**：60 req/60s

**查询参数**：
- `page` (int)：页码
- `pageSize` (int)：每页数量（1~200，默认 50）

**响应**：
```json
{
  "code": 0,
  "message": "成功",
  "data": [
    {
      "id": "order-1",
      "orderType": "PURCHASE",
      "assetId": 1,
      "assetName": "资源名",
      "cdkCode": "",
      "goldChange": -900,
      "goldChangeReason": "购买资源",
      "planTransition": "无旧方案 -> price-1",
      "description": "购买了资源",
      "createdAt": 1700000000000
    }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 100
}
```

---

#### 2. 管理员查询用户订单

```http
GET /api/admin/orders/{userId}?page=1&pageSize=50
Authorization: Bearer <token>
```

**限制**：60 req/60s

**权限**：Admin 或以上

---

#### 3. 管理员删除单个订单

```http
DELETE /api/admin/orders/{userId}/{orderId}
Authorization: Bearer <token>
```

**限制**：60 req/60s

**权限**：Admin 或以上

---

#### 4. 管理员批量删除订单

```http
POST /api/admin/orders/{userId}/delete
Authorization: Bearer <token>
Content-Type: application/json

{
  "orderIds": ["order-1", "order-2"]
}
```

**限制**：30 req/60s

**权限**：Admin 或以上

---

### CDK 管理

#### 1. 检查 CDK

```http
POST /api/cdk/admin/inspect
Authorization: Bearer <token>
Content-Type: application/json

{
  "code": "ABCD1234"
}
```

**限制**：60 req/60s

**权限**：CDK Admin 或以上

**响应**：
```json
{
  "contains": true,
  "data": {
    "id": 1,
    "code": "ABCD1234",
    "description": "CDK 描述",
    "isUsed": false,
    "goldValue": 1000,
    "expiresInSeconds": 2592000,
    "createdAt": 1700000000000,
    "createdBy": "admin",
    "usedAt": 0,
    "usedBy": ""
  }
}
```

---

#### 2. 生成 CDK 代码

```http
POST /api/cdk/admin/generate
Authorization: Bearer <token>
Content-Type: application/json

{
  "prefix": "TEST",
  "count": 100,
  "length": 12
}
```

**限制**：10 req/60s

**权限**：CDK Admin 或以上

**参数**：
- `prefix` (string)：CDK 前缀
- `count` (int)：生成数量（1~1000，默认 1）
- `length` (int)：CDK 长度（4~256，默认 8）

**响应**：
```json
{
  "message": "生成成功",
  "codes": ["TEST-ABCD1234", "TEST-EFGH5678", ...]
}
```

---

#### 3. 保存 CDK

```http
POST /api/cdk/admin/save
Authorization: Bearer <token>
Content-Type: application/json

{
  "code": "ABCD1234",
  "description": "CDK 描述",
  "goldValue": 1000,
  "expiresInSeconds": 2592000
}
```

**限制**：5 req/60s

**权限**：CDK Admin 或以上

---

#### 4. 删除 CDK

```http
POST /api/cdk/admin/delete
Authorization: Bearer <token>
Content-Type: application/json

{
  "code": "ABCD1234"
}
```

**限制**：180 req/60s

**权限**：CDK Admin 或以上

---

#### 5. CDK 列表

```http
GET /api/cdk/admin/list?page=1&pageSize=50&includeUsed=false
Authorization: Bearer <token>
```

**限制**：60 req/60s

**权限**：CDK Admin 或以上

**查询参数**：
- `page` (int)：页码
- `pageSize` (int)：每页数量
- `includeUsed` (bool)：是否包含已使用 CDK

---

#### 6. CDK 搜索

```http
GET /api/cdk/admin/search?q=TEST&limit=10
Authorization: Bearer <token>
```

**限制**：60 req/60s

**权限**：CDK Admin 或以上

**查询参数**：
- `q` (string)：搜索关键词
- `limit` (int)：返回数量

---

#### 7. 激活 CDK

```http
POST /api/cdk/activate
Authorization: Bearer <token>
Content-Type: application/json

{
  "code": "ABCD1234"
}
```

**限制**：20 req/60s

**功能**：用户激活 CDK，获得对应的金币

**响应成功**：
```json
{
  "code": 0,
  "message": "CDK 激活成功",
  "goldValue": 1000
}
```

**响应失败**：
```json
{
  "code": 2001,
  "message": "CDK 不存在"
}
```

**错误码**：
- `2001`：CDK 不存在
- `2002`：CDK 已被使用
- `2003`：CDK 已过期

---

### 系统控制台

#### 1. 执行命令

```http
POST /api/console/execute
Authorization: Bearer <token>
Content-Type: application/json

{
  "command": "help",
  "args": ["asset"]
}
```

**限制**：30 req/60s

**权限**：Console 或以上

**功能**：执行服务器命令

---

#### 2. 获取命令列表

```http
GET /api/console/commands
Authorization: Bearer <token>
```

**功能**：获取所有可用命令

**响应**：
```json
{
  "commands": [
    {
      "name": "ban",
      "description": "封禁用户",
      "usage": "ban <username> <reason> [seconds]"
    }
  ]
}
```

---

#### 3. 日志流（SSE）

```http
GET /api/console/logs
Authorization: Bearer <token>

Accept: text/event-stream
```

**功能**：实时推送系统日志

**事件格式**：
```
event: log
data: {"message":"...", "level":"INFO", "time":"2024-01-01 12:00:00"}

event: connected
data: {"subscribers":5}
```

---

## 错误响应

### 通用错误码

| 状态码 | 含义 | 场景 |
|-------|------|------|
| 400 | 请求错误 | 参数无效、请求体格式错误 |
| 401 | 未授权 | 令牌缺失或无效 |
| 403 | 禁止访问 | 权限不足、用户被封禁 |
| 404 | 未找到 | 资源、用户、订单不存在 |
| 409 | 冲突 | 用户名/邮箱已注册 |
| 429 | 请求过于频繁 | 超过速率限制 |
| 500 | 服务器错误 | 内部处理异常 |

### 业务错误码

| 错误码 | 含义 |
|-------|------|
| 0 | 成功 |
| 2001 | CDK 不存在 |
| 2002 | CDK 已被使用 |
| 2003 | CDK 已过期 |
| 2004 | 资源未拥有 |

---

## 命令系统

### 命令类型

#### 用户管理命令 (UserCommandHandler)
- `ban <username> <reason> [seconds]`：封禁用户
- `unban <username>`：解禁用户
- `promote <username> <level>`：提升权限

#### 资源管理命令 (AssetCommandHandler)
- `asset.list`：列出所有资源
- `asset.verify <assetId>`：验证资源
- `asset.delete <assetId>`：删除资源

#### 系统命令 (SystemCommandHandler)
- `help [command]`：获取帮助
- `stats`：系统统计
- `clear.logs`：清空日志

---

## 数据模型

### AssetModel（资源/商品）

```csharp
public class AssetModel : IDataBase
{
    // 基本信息
    public int Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }
    public string Author { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public string Tags { get; set; }
    
    // 媒体资源
    public string PrimaryImage { get; set; }
    public string ThumbnailImage { get; set; }
    public string Screenshots { get; set; }
    
    // 价格与规格
    public TableList<AssetPrice> Prices { get; set; }
    public AssetSpecs Specs { get; set; }
    
    // 软删除
    public bool IsDeleted { get; set; }
    public long DeletedAt { get; set; }
}
```

### UserData（用户）

```csharp
public class UserData : IDataBase
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public string PasswordHash { get; set; }
    public string Email { get; set; }
    public string DisplayName { get; set; }
    public int Gold { get; set; }
    public TableList<UserActiveAsset> ActiveAssets { get; set; }
    public TableList<UserOrderRecord> OrderRecords { get; set; }
    public UserStatusData Status { get; set; }
    // 其他字段...
}
```

---

## 最佳实践

### 1. 令牌管理
- 在登录后立即保存令牌到本地存储
- 每小时重新获取新令牌
- 在请求失败时检查令牌有效性

### 2. 错误处理
- 总是检查 HTTP 状态码
- 对 429 状态码进行指数退避重试
- 记录 500 错误便于调试

### 3. 缓存策略
- 头像缓存 1 小时
- 资源列表可缓存 5 分钟
- 用户资料可缓存 10 分钟

### 4. 安全建议
- 不在 URL 中传输敏感数据
- 使用 HTTPS 传输令牌
- 定期更新密码
- 启用两因素认证（如可用）

---

**最后更新**：2026-03-03  
**API 版本**：1.1