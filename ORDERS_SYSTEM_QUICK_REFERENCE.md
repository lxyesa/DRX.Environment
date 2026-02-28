# 订单系统 - 快速参考指南

## 🎯 快速开始

### 用户端功能
1. 打开个人资料页面 → 点击"订单"标签
2. 查看订单列表（每页 20 条）
3. 使用搜索框查找特定订单
4. 通过下拉菜单筛选订单状态
5. 点击"查看详情"按钮查看完整订单信息

### 管理员功能
1. 访问 `/api/admin/orders/{userId}` 查看用户订单
2. 删除不适当的订单记录
3. 批量删除多条订单

---

## 📋 API 快速参考

### 获取用户订单列表
```bash
GET /api/user/orders?page=1&pageSize=50

# Header
Authorization: Bearer {jwt_token}

# 响应示例
{
  "code": 0,
  "message": "成功",
  "data": [
    {
      "id": "uuid...",
      "orderType": "cdk",
      "assetId": 0,
      "assetName": "CDK 兑换",
      "cdkCode": "VIP-2024-XXXX",
      "goldChange": 100,
      "description": "金币充值",
      "createdAt": 1705056000000
    }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 150
}
```

### 管理员获取指定用户订单
```bash
GET /api/admin/orders/{userId}?page=1&pageSize=50

# Header
Authorization: Bearer {admin_token}
```

### 删除单条订单
```bash
DELETE /api/admin/orders/{userId}/{orderId}

# Header
Authorization: Bearer {admin_token}

# 响应
{
  "code": 0,
  "message": "订单已删除"
}
```

### 批量删除订单
```bash
POST /api/admin/orders/{userId}/delete

# Header
Authorization: Bearer {admin_token}
Content-Type: application/json

# 请求体
{
  "orderIds": ["id1", "id2", "id3"]
}

# 响应
{
  "code": 0,
  "message": "批量删除完成",
  "removed": 3
}
```

---

## 🛠️ 常用代码片段

### 从 JavaScript 加载订单
```javascript
// 加载第一页
loadUserOrders(1);

// 搜索订单
document.getElementById('orderSearch').value = '关键词';
searchOrders();

// 应用状态筛选
document.getElementById('orderStatusFilter').value = 'paid';
loadUserOrders(1);
```

### 在其他页面添加订单快链接
```javascript
// 跳转到用户资料的订单标签
window.location.href = '/profile#orders';
```

### 处理订单数据
```javascript
// 显示订单详情
showOrderDetail(orderObject);

// 创建订单卡片
const card = createOrderCard(orderObject);
document.getElementById('myContainer').appendChild(card);
```

---

## 📊 数据模型

### 订单对象结构
```javascript
{
  id: "550e8400-e29b-41d4-a716-446655440000",  // UUID
  orderType: "cdk" | "purchase",                 // 类型
  assetId: 0 | 123,                              // 资产ID（CDK为0）
  assetName: "商品名称",                         // 商品名
  cdkCode: "VIP-2024-XXXX" | "",                 // CDK码
  goldChange: -100 | 50,                         // 金币变化
  description: "支付或兑换说明",                 // 备注
  createdAt: 1705056000000                       // 时间戳（毫秒）
}
```

---

## 🔄 订单类型说明

| OrderType | 含义 | GoldChange | 场景 |
|-----------|------|-----------|------|
| `cdk` | CDK 兑换 | 正数 | 用户输入CDK码兑换金币 |
| `purchase` | 金币购买 | 负数 | 用户花费金币购买资产 |

### 金币变化规则
- **正数** (+100) = 用户 **获得** 金币
- **负数** (-100) = 用户 **花费** 金币

---

## 🎨 UI 组件参考

### 订单状态标签样式
```javascript
// CDK 兑换 - 绿色
<span style="background:#10b981;color:#fff;padding:2px 6px;border-radius:3px;">CDK 兑换</span>

// 金币购买 - 橙色
<span style="background:#f59e0b;color:#fff;padding:2px 6px;border-radius:3px;">金币购买</span>
```

### 金币变化样式
```javascript
// 增加 - 绿色
<div style="color:#10b981;font-weight:600;">+100 💰</div>

// 减少 - 红色
<div style="color:#ef4444;font-weight:600;">-100 💰</div>
```

---

## 🔐 权限检查

### API 权限等级

| API | 需要权限 | 说明 |
|-----|---------|------|
| GET /api/user/orders | 普通用户 | 获取自己的订单 |
| GET /api/admin/orders/* | Admin(≤3) | 查看任何用户订单 |
| DELETE /api/admin/orders/* | Admin(≤3) | 删除订单 |
| POST /api/admin/orders/*/delete | Admin(≤3) | 批量删除 |

### 权限组映射
```csharp
0: System      // 最高权限
2: Console     // 次高权限
3: Admin       // 管理员
999: User      // 普通用户（默认）
```

---

## 🐛 常见问题排查

### Q: 订单列表为空
- [ ] 确认用户已登录（Token 有效）
- [ ] 检查后端数据库是否有订单记录
- [ ] 查看浏览器控制台是否有 API 错误
- [ ] 确认分页参数正确

### Q: 搜索没有结果
- [ ] 检查关键词是否正确（区分大小写）
- [ ] 尝试搜索资产名称而非 ID
- [ ] 清除状态筛选，搜索全部订单

### Q: Token 过期导致重定向
- [ ] 正常现象，会自动重定向到登录页
- [ ] 重新登录获取新 Token
- [ ] 检查 localStorage 中的 `kax_login_token`

### Q: 分页按钮灰化
- [ ] 已在第一页时，"上一页"按钮灰化
- [ ] 已在最后一页时，"下一页"按钮灰化
- [ ] 这是正常行为

---

## 💾 数据持久化

### 订单存储位置
```
UserData (主表)
  ↓
  └─ OrderRecords (子表 - TableList<UserOrderRecord>)
```

### 每条订单包含
- 唯一标识 (UUID)
- 订单类型 (CDK/Purchase)
- 资产快照 (AssetName - 防止资产删除后查看混乱)
- 时间戳 (CreatedAt, UpdatedAt)
- 交易详情 (金币变化、CDK码等)

---

## 🚀 性能优化建议

### 前端优化
```javascript
// 使用懒加载
let catalogTabLoaded = false;
if (!catalogTabLoaded) {
    catalogTabLoaded = true;
    loadUserOrders(1);
}

// 限制每页显示数
const ordersPageSize = 20; // 不用加载过多

// 客户端搜索而非多次 API 调用
filtered = allOrders.filter(o => o.name.includes(keyword));
```

### 后端优化建议
1. 为 `UserOrderRecord.CreatedAt` 添加数据库索引
2. 在大数据集上实现服务端搜索过滤
3. 考虑实现查询缓存

---

## 📝 日志示例

### 正常流程日志
```
[INFO] 用户 admin 获取订单列表：page=1, total=150
[INFO] 用户 alice 搜索订单：keyword="CDK", results=5
[INFO] 管理员 root 删除用户订单：userId=42, orderId=uuid
```

### 错误日志
```
[ERROR] 获取订单列表失败: 用户不存在
[ERROR] Token 验证失败: 过期或无效
[ERROR] 删除订单失败: 订单不存在
```

---

## 📞 支持和反馈

- **文档**：[ORDERS_SYSTEM_IMPLEMENTATION.md](./ORDERS_SYSTEM_IMPLEMENTATION.md)
- **API 文档**：查看 Handlers/api文档.md
- **代码位置**：
  - 前端：Views/html/profile.html, Views/js/profile.js
  - 后端：Handlers/KaxHttp.OrderManagement.cs
  - 模型：Model/DataModel.cs

---

最后更新：2026-02-28
