# Claude 项目指引 — PharmaFlow 药品流向管理

## 项目概述

ASP.NET Core 8.0 + React 19 + PostgreSQL 的药品流向追踪管理平台，含 11 个功能模块（含数据采集）。

## 关键文件位置

| 文件 | 用途 |
|------|------|
| `src/PharmaFlow.WebApi/Program.cs` | 后端入口，含种子数据和中间件配置 |
| `src/PharmaFlow.WebApi/Controllers/` | API 控制器 |
| `src/PharmaFlow.Application/Services/` | 业务逻辑服务层 |
| `src/PharmaFlow.Application/DTOs/CommonDTOs.cs` | 所有 DTO 定义 |
| `src/PharmaFlow.Domain/Entities/` | 实体类 |
| `src/PharmaFlow.Infrastructure/Data/AppDbContext.cs` | EF Core DbContext |
| `src/PharmaFlow.Infrastructure/Data/EntityConfigurations/AllEntityConfigurations.cs` | 所有实体配置 |
| `src/PharmaFlow.WebClient/src/App.tsx` | 前端路由配置 |
| `src/PharmaFlow.WebClient/src/components/AppLayout.tsx` | 侧边栏菜单 + 权限过滤 |
| `src/PharmaFlow.WebClient/src/store/authStore.ts` | 认证状态 + canWrite 权限 |
| `src/PharmaFlow.WebClient/src/pages/` | 页面组件 |
| `src/PharmaFlow.WebApi/HostedServices/` | 后台定时服务（采集调度等） |
| `src/PharmaFlow.WebApi/HostedServices/CollectionSchedulerHostedService.cs` | 定时采集调度服务 |
| `src/PharmaFlow.Application/Services/CollectionExecutionService.cs` | 数据采集 HTTP 执行引擎 |
| `src/PharmaFlow.Application/Services/CollectionService.cs` | 采集服务 + 去重 + 提交 |
| `src/PharmaFlow.Application/Services/DataCleaner.cs` | 基础数据清洗器（字段映射） |
| `src/PharmaFlow.Application/Services/SmartDataCleaner.cs` | 智能清洗器（校验+匹配+异常检测） |
| `src/PharmaFlow.Application/Services/BatchProcessor.cs` | 批次处理器（提取+匹配+导入） |
| `src/PharmaFlow.Application/Services/SpecNormalizer.cs` | 规格规范化引擎 |
| `src/PharmaFlow.Application/Services/ImportRuleService.cs` | 导入规则服务 |
| `src/PharmaFlow.WebClient/src/pages/datacollection/` | 数据采集前端页面 |

... (remaining lines unchanged) 
| `nginx.conf` | Nginx 反向代理配置 |

## 开发工作流

### 本地启动
```bash
# 后端（SQLite，无需数据库）
cd src/PharmaFlow.WebApi && dotnet run
# → http://localhost:5009, Swagger: http://localhost:5009/swagger

# 前端（热更新）
cd src/PharmaFlow.WebClient && npm run dev
# → http://localhost:5173

# 默认账号: admin / admin123
```

### 构建与部署到服务器
```bash
# 后端：发布 + 上传 publish/ 到服务器 → docker compose build --no-cache backend → restart
# 前端：npm run build → 上传 dist/ → docker cp 到容器 → nginx -s reload
# 具体见下方"部署脚本模式"
```

## 服务器信息

- **IP**: 14.103.86.138
- **SSH**: root / 123123123qwe.
- **项目路径**: /opt/PharmaFlow
- **访问**: http://14.103.86.138

### SSH 连接（Python paramiko）
```python
import paramiko
host, user, pwd = '14.103.86.138', 'root', '123123123qwe.'
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect(host, username=user, password=pwd, timeout=30)
# SFTP: transport = paramiko.Transport((host,22)); transport.connect(username=user, password=pwd)
```

### 服务器常用命令
```bash
docker compose -f /opt/PharmaFlow/docker-compose.yml ps     # 查看状态
docker compose -f /opt/PharmaFlow/docker-compose.yml up -d   # 启动
docker compose -f /opt/PharmaFlow/docker-compose.yml down    # 停止
docker cp /tmp/new_dist/. pharmaflow-web:/usr/share/nginx/html/  # 更新前端
docker exec pharmaflow-web nginx -s reload                    # 重载 Nginx
docker exec pharmaflow-db psql -U pharmaflow -d PharmaFlow -c "..."  # 查询数据库
```

## 部署脚本模式

当用户提出"部署"或"提交服务器"，执行以下流程：

### 仅前端修改
```bash
# 1. 本地构建
cd "e:\流向平台\PharmaFlow\src\PharmaFlow.WebClient" && npm run build

# 2. 上传到服务器 + 更新容器
python -c "
import paramiko, os
h='14.103.86.138'; u='root'; p='123123123qwe.'
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(h,username=u,password=p,timeout=30)
c.exec_command('mkdir -p /tmp/fd/assets',timeout=5)
c.close()
t=paramiko.Transport((h,22)); t.connect(username=u,password=p)
s=paramiko.SFTPClient.from_transport(t)
d=r'e:\流向平台\PharmaFlow\src\PharmaFlow.WebClient\dist'
for f in os.listdir(d):
    fp=os.path.join(d,f)
    if os.path.isfile(fp): s.put(fp,f'/tmp/fd/{f}')
for f in os.listdir(os.path.join(d,'assets')):
    s.put(os.path.join(d,'assets',f),f'/tmp/fd/assets/{f}')
s.close(); t.close()
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(h,username=u,password=p,timeout=30)
c.exec_command('docker exec pharmaflow-web sh -c \"rm -f /usr/share/nginx/html/assets/*.js\" && docker cp /tmp/fd/. pharmaflow-web:/usr/share/nginx/html/ && docker exec pharmaflow-web nginx -s reload',timeout=30)
c.close()
"

### 后端修改
```bash
# 1. 本地发布
export PATH=\"\$HOME/AppData/Local/Microsoft/dotnet:\$PATH\"
rm -rf \"e:\\流向平台\\PharmaFlow\\_publish\"
cd \"e:\\流向平台\\PharmaFlow\\src\\PharmaFlow.WebApi\"
dotnet publish -c Release -r linux-x64 -o \"e:\\流向平台\\PharmaFlow\\_publish\"

# 2. 上传 + 重建容器
python -c "
import paramiko, os, time
h='14.103.86.138'; u='root'; p='123123123qwe.'
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(h,username=u,password=p,timeout=30)
c.exec_command('rm -rf /opt/PharmaFlow/publish && mkdir -p /opt/PharmaFlow/publish',timeout=10)
c.close()
t=paramiko.Transport((h,22)); t.connect(username=u,password=p)
s=paramiko.SFTPClient.from_transport(t)
pub=r'e:\流向平台\PharmaFlow\_publish'
for f in os.listdir(pub):
    fp=os.path.join(pub,f)
    if os.path.isfile(fp): s.put(fp,f'/opt/PharmaFlow/publish/{f}')
s.close(); t.close()
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(h,username=u,password=p,timeout=30)
c.exec_command('cd /opt/PharmaFlow && docker compose rm -fs backend && docker compose build --no-cache backend 2>&1',timeout=300)
c.exec_command('cd /opt/PharmaFlow && docker compose up -d backend 2>&1',timeout=60)
c.close()
"
```

### 前后端都修改
分别执行上面两个流程。

## 数据库

- PostgreSQL: `pharmaflow-db:5432`, 数据库 `PharmaFlow`, 用户 `pharmaflow`, 密码在 `.env`
- 本地开发: SQLite, 文件 `PharmaFlow.WebApi/PharmaFlow.db`
- 表名在 PostgreSQL 中区分大小写，需加双引号查询

## 已知注意事项

1. **前端构建**: `package.json` 的 build 脚本已改为 `vite build`（跳过了 `tsc -b`），因为 Ant Design 6 类型与 TypeScript 6 有兼容问题。运行时正常。
2. **PostgreSQL**: 
   - DateTime 需转 UTC（`UtcDateTimeConverter`）
   - `HasFilter` 中的布尔值需用 `"IsDeleted" = false`（带双引号）
   - Npgsql 版本锁定 8.0.x，与 EF Core PostgreSQL 8.0.x 配套
3. **SSH 连接频率限制**: 短时间多次 SSH 会被服务器拒绝，每次连接间隔 ≥ 3 秒
4. **docker compose down -v** 会删除数据库卷，谨慎使用
5. **密码**: admin 账号密码为 `admin123`，服务器 ROOT 密码 `123123123qwe.`
6. **权限体系**: 管理员 `*` | 操作员 9 模块 | 只读用户 9 模块（无写权限）
   - 前端通过 `canWrite` 控制按钮显隐
   - 菜单通过 `filterMenu(permissions)` 过滤
   - 权限 key: `dashboard`, `drugs`, `customers`, `purchases`, `sales`, `inventory`, `distribution`, `reports`, `datacollection`, `users`

## 常用代码模式

### 新增页面
1. 创建 `src/PharmaFlow.WebClient/src/pages/xxx/XxxPage.tsx`
2. 在 `App.tsx` 中 import + 添加 Route
3. 在 `AppLayout.tsx` 的 `ALL_MENU_ITEMS` 中添加菜单项（含 `perm` 字段）

### 新增 API
1. Controller 放在 `Controllers/`，继承 `ControllerBase`，加 `[ApiController]` 和 `[Route("api/[controller]")]`
2. Service 放在 `Application/Services/`，接口 `I*Service.cs` + 实现 `*Service.cs`
3. 在 `Program.cs` 注册: `builder.Services.AddScoped<I*Service, *Service>();`
4. DTO 放在 `Application/DTOs/CommonDTOs.cs`

### 权限按钮控制
```tsx
import { useAuthStore } from '../../store/authStore';
const canWrite = useAuthStore(s => s.canWrite);
// 用 {canWrite && <Button...>} 包裹写操作按钮
```

### API 调用
```tsx
import apiClient from '../../api/client';
const res = await apiClient.get<ApiResponse<XxxDto[]>>('/xxx');
const data = (res as any).data || [];
```

### 数据采集

**三个 Tab 页面**：
- **采集源配置**：管理采集源（认证+导出 JSON 配置），支持 Cookie/Token/表单登录，JSON/HTML/Excel/chartMain 解析
- **采集数据管理**：合并视图按源分组，查看数据、配置映射、提交到汇总、自动提交开关
- **采集汇总**：统一固定列表格，按源筛选，支持清空

**采集源配置 JSON 结构**：

```json
// AuthConfig — 认证配置（支持 Cookie Token / Header Token / 表单登录）
{ "token": { "cookie_name": "FLOW_TOKEN", "value": "eyJ..." },
  "headers": { "Origin": "...", "Referer": "..." } }

// 表单登录（自动获取 Cookie）
{ "loginPath": "/login", "username": "user", "password": "pass",
  "extraFields": { "rememberMe": "true" } }

// JSON登录并从响应提取Token（如国药控股江西 PSI）
{ "loginPath": "/prod-api/login", "loginMethod": "POST",
  "loginHeaders": { "Origin": "http://...", "Referer": "http://.../login", "isToken": "false" },
  "preLogin": { "url": "/prod-api/captchaImage", "extract": [
    { "name": "uuid", "type": "json", "path": "uuid", "required": true },
    { "name": "code", "type": "mathCaptcha", "path": "img", "required": true } ] },
  "passwordEncrypt": { "type": "rsa", "name": "password", "value": "...", "publicKey": "MIIB..." },
  "loginBodyJson": "{\"username\":\"...\",\"password\":\"{{password}}\",\"code\":\"{{code}}\",\"uuid\":\"{{uuid}}\"}",
  "tokenExtract": { "source": "json", "paths": ["token", "data.token", "access_token"], "required": true,
    "targets": [ { "type": "header", "name": "Authorization", "prefix": "Bearer " },
                 { "type": "cookie", "name": "Psi-Syn-Token" } ] },
  "headers": { "Origin": "http://...", "Referer": "http://.../psisyn/psisale" } }

// ExportConfig — 导出配置（JSON POST / Form POST / GET，JSON/HTML/Excel 解析）
{ "export": { "url": "...", "method": "POST", "content_type": "application/json", "dataFormat": "json" },
  "body_template": { "db": "db", "params": { "startDate": "{{dayBeforeYesterday}}", "endDate": "{{yesterday}}" } } }
// 注：body_template 在外层，不在 export 内层；Form POST 用 params 在 export 内

// TransformConfig — 字段映射+清洗（在「配置映射」中可视化配置）
{ "targetEntity": "DistributionRecord",
  "fieldMappings": { "DrugName": {"source":"FPRODUCTNAME"}, ... },
  "validation": { "requiredFields": [...], "fieldRules": {...} },
  "masterDataMatch": { "drug": {...}, "customer": {...} },
  "smartCleaning": { "normalizeText": true, "deduplicate": {...}, "anomalyDetection": {...} } }
```

**日期变量**（采集时自动替换）：
| 变量 | 含义 |
|------|------|
| `{{startDate}}` | 30天前 |
| `{{endDate}}` | 昨天 |
| `{{7daysAgo}}` | 7天前 |
| `{{today}}` | 今天 |
| `{{yesterday}}` | 昨天 |
| `{{dayBeforeYesterday}}` | 前天 |

**采集管道**：定时/手动触发 → HTTP 登录 → 分页导出 → JSON/HTML/Excel/chartMain 解析 → SmartDataCleaner 三阶段清洗 → CollectionRecord → 去重存储 → 配置映射 → 提交汇总

**定时调度**: `CollectionSchedulerHostedService` (BackgroundService) 每 60 秒轮询 `ScheduleCron` 表达式（如 `0 8 * * *` 每天8点）

**新增采集平台**: 编辑采集源 → 配置 AuthConfig + ExportConfig JSON → 保存 → 采集 → 配置映射 → 提交

**常见问题排查**：
- 采集为0条：检查后端日志 `cat tasks/*.output | grep "导出"` 查看API响应
- 502错误：后端未启动，`dotnet run` 重启
- body_template变量不生效：确保放在外层（与export同级），不要放在export内部
- Form POST参数缺失：params需放在export节点内部
- 日期变量不替换：确认变量名拼写正确，`{{dayBeforeYesterday}}` 带完整驼峰
- Token过期：从浏览器F12→Application→Cookies复制新Token，或用「更新Token」按钮
- 华润chartMain null元素：解析器已处理
