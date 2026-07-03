# 项目框架速查

PharmaFlow 项目的完整架构、代码模式和快速开发指引。新对话时加载此 skill 即可快速上手，无需重新探索项目。

---

## 一、项目架构

```
PharmaFlow/
├── src/
│   ├── PharmaFlow.Domain/          # 领域层：实体 + 枚举
│   │   ├── Entities/               # 24 个实体类
│   │   └── Enums/Enums.cs          # 所有枚举定义
│   ├── PharmaFlow.Application/     # 应用层：业务逻辑
│   │   ├── DTOs/CommonDTOs.cs      # 所有 DTO 定义（单文件）
│   │   └── Services/               # 15+ 个业务服务 + 采集引擎
│   ├── PharmaFlow.Infrastructure/  # 基础设施层：EF Core
│   │   └── Data/
│   │       ├── AppDbContext.cs      # DbContext
│   │       └── EntityConfigurations/AllEntityConfigurations.cs  # 所有实体配置
│   └── PharmaFlow.WebApi/          # Web API 层
│       ├── Controllers/            # 12 个 API 控制器
│       ├── HostedServices/         # 后台定时服务
│       ├── Program.cs              # 入口 + 种子数据 + 中间件
│       └── PharmaFlow.db           # 本地 SQLite 数据库
└── src/PharmaFlow.WebClient/       # React 19 前端
    ├── src/
    │   ├── App.tsx                 # 路由配置
    │   ├── api/client.ts           # Axios API 客户端
    │   ├── components/AppLayout.tsx # 侧边栏菜单 + 权限过滤
    │   ├── store/authStore.ts      # Zustand 认证状态
    │   ├── pages/                  # 16 个页面组件
    │   └── types/index.ts          # 前端类型定义
    └── package.json                # build 脚本已改为 vite build
```

---

## 二、技术栈

| 层 | 技术 | 版本 |
|----|------|------|
| 后端框架 | ASP.NET Core | 8.0 |
| ORM | EF Core + Npgsql | 8.0.x |
| 数据库 | PostgreSQL (服务器) / SQLite (本地) | 16 / 3 |
| 前端框架 | React + TypeScript | 19 + 6.x |
| 构建工具 | Vite | 5 |
| UI 库 | Ant Design | 6 |
| 状态管理 | Zustand | - |
| 容器化 | Docker Compose | - |

---

## 三、核心实体关系

```
User ──< Role (多对一)
Drug ──< DrugCategory, DrugPrice
Customer ──< CustomerCategory
Supplier
PurchaseOrder ──< PurchaseOrderItem ──< Drug
SalesOrder ──< SalesOrderItem ──< Drug
Inventory ──< Drug, InventoryLog
DistributionRecord     # 流向汇总（采集数据最终目标）
DistributionTrace      # 流向追溯

# 数据采集模块
CollectionSource ──< CollectionTask, CollectionRecord
ImportRule ──< ModuleRuleConfig
SystemConfig
OperationLog
```

---

## 四、代码模式速查

### 新增业务模块（完整流程）

1. **Entity**: `src/PharmaFlow.Domain/Entities/Xxx.cs`
   ```csharp
   public class Xxx : AuditableEntity  // 或 BaseEntity
   {
       public string Name { get; set; } = string.Empty;
       // ... 属性
   }
   ```

2. **EntityConfig**: `src/PharmaFlow.Infrastructure/Data/EntityConfigurations/AllEntityConfigurations.cs`
   在文件末尾添加 `builder.Entity<Xxx>()` 配置

3. **DTO**: `src/PharmaFlow.Application/DTOs/CommonDTOs.cs`
   在文件末尾添加 XxxDto 类

4. **Service 接口**: `src/PharmaFlow.Application/Services/IXxxService.cs`
5. **Service 实现**: `src/PharmaFlow.Application/Services/XxxService.cs`
6. **注册**: `Program.cs` → `builder.Services.AddScoped<IXxxService, XxxService>();`

7. **Controller**: `src/PharmaFlow.WebApi/Controllers/XxxController.cs`
   ```csharp
   [ApiController, Route("api/[controller]")]
   public class XxxController : ControllerBase { ... }
   ```

8. **前端页面**: `src/PharmaFlow.WebClient/src/pages/xxx/XxxPage.tsx`
9. **路由**: `App.tsx` → import + `<Route>`
10. **菜单**: `AppLayout.tsx` → `ALL_MENU_ITEMS` 添加（含 `perm` 字段）
11. **权限**: `Enums.cs` → 如有新权限 key 需要添加

### 后端代码模式

```csharp
// Controller 模式
[ApiController, Route("api/[controller]")]
public class XxxController : ControllerBase
{
    private readonly IXxxService _service;
    public XxxController(IXxxService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // 获取当前用户
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = User.IsInRole("admin");
        var result = await _service.GetAllAsync(userId, isAdmin);
        return Ok(new ApiResponse { Data = result });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] XxxDto dto) { ... }
}
```

### 前端代码模式

```tsx
// API 调用
import apiClient from '../../api/client';
const res = await apiClient.get<ApiResponse<XxxDto[]>>('/xxx');
const data = (res as any).data || [];
// 注: 用 as any 绕过类型检查，因为 ApiResponse 包装

// 权限控制
import { useAuthStore } from '../../store/authStore';
const canWrite = useAuthStore(s => s.canWrite);
// {canWrite && <Button>写操作</Button>}

// 菜单项格式 (AppLayout.tsx)
{ key: '/xxx', label: '模块名', icon: <Icon />, perm: 'xxx' }
```

---

## 五、数据采集模块架构

```
采集管道:
  CollectionSource (配置)
    → CollectionExecutionService (HTTP/Python 执行)
      → SmartDataCleaner (三阶段清洗: 校验→匹配→异常检测)
        → CollectionService (去重存储)
          → CollectionRecord (原始记录)
            → TransformConfig 字段映射
              → DistributionRecord (汇总记录)
```

**核心文件**:
| 文件 | 职责 |
|------|------|
| `CollectionExecutionService.cs` | HTTP 引擎 + Python 脚本模式 + 解析器 |
| `CollectionService.cs` | 采集服务 + 去重 + 提交到汇总 |
| `SmartDataCleaner.cs` | 三阶段清洗 |
| `DataCleaner.cs` | 基础字段映射清洗 |
| `BatchProcessor.cs` | 批次处理 |
| `SpecNormalizer.cs` | 规格规范化 |
| `CollectionSchedulerHostedService.cs` | 定时调度 |

**采集模式判断逻辑**:
```
CollectionMode == "PythonScript"
  → 读取 ExportConfig.export.pythonScript 路径
  → subprocess.run("python script.py") → 读 stdout JSON
CollectionMode == "JsonConfig"
  → 解析 AuthConfig → HTTP 登录 → 解析 ExportConfig → HTTP 导出
  → 根据 dataFormat 解析: json / html / excel / chartMain
```

---

## 六、权限体系

| 角色 | 权限 Key | 说明 |
|------|----------|------|
| 管理员 | `*` | 全部模块 + 全部操作 |
| 操作员 | 9 个模块 key | 可读写，无 `users` |
| 只读用户 | 9 个模块 key | 只读，`canWrite = false` |

**模块权限 key**: `dashboard`, `drugs`, `customers`, `purchases`, `sales`, `inventory`, `distribution`, `reports`, `datacollection`, `users`

**权限判断**:
- 后端: `User.IsInRole("admin")` + `User.FindFirst(ClaimTypes.NameIdentifier)`
- 前端菜单: `filterMenu(permissions)` 在 AppLayout.tsx 中过滤
- 前端按钮: `canWrite` 控制新增/编辑/删除按钮显隐

---

## 七、数据库注意事项

1. **SQLite vs PostgreSQL**: 本地用 SQLite (`PharmaFlow.db`)，服务器用 PostgreSQL
2. **大小写**: PostgreSQL 中表名和列名需加双引号，SQLite 不需要
3. **DateTime**: 服务器存储 UTC 时间（`UtcDateTimeConverter`），读取时转换
4. **EF Core 过滤器**: `HasFilter` 中的布尔值用 `"IsDeleted" = false`（双引号）
5. **Npgsql**: 版本锁定 8.0.x
6. **种子数据**: `Program.cs` 中 `SeedData()` 函数

---

## 八、本地运行

```bash
# 后端 (http://localhost:5009)
cd src/PharmaFlow.WebApi && dotnet run

# 前端 (http://localhost:5173)
cd src/PharmaFlow.WebClient && npm run dev

# 默认账号: admin / admin123
```

---

## 九、服务器信息速查

| 项目 | 值 |
|------|-----|
| IP | 14.103.86.138 |
| SSH | root / 123123123qwe. |
| 项目路径 | /opt/PharmaFlow |
| 数据库容器 | pharmaflow-db:5432 |
| 后端容器 | pharmaflow-backend |
| 前端容器 | pharmaflow-web (Nginx) |

---

## 十、已知约束

1. 前端 build 跳过 `tsc -b`（Ant Design 6 类型与 TypeScript 6 不兼容）
2. SSH 连接间隔 ≥ 3 秒
3. `docker compose down -v` 会清空数据库
4. Python 脚本 stdout 必须 UTF-8 编码
5. body_template 在外层，Form params 在 export 内层（采集配置）
