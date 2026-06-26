# 💊 PharmaFlow — 药品流向管理系统

基于 ASP.NET Core + React 构建的药品流向追踪与管理平台，涵盖药品管理、客户管理、采购销售、库存管理、流向查询、串货预警、报表统计等核心功能。

## 🛠 技术栈

| 层级 | 技术 |
|------|------|
| 后端框架 | ASP.NET Core 8.0 Web API |
| 数据库 | SQLite（开发）/ PostgreSQL（生产） |
| ORM | Entity Framework Core 8.0 |
| 前端框架 | React 19 + TypeScript 6 |
| UI 组件 | Ant Design 6 |
| 构建工具 | Vite 8 |
| 状态管理 | Zustand |
| 图表 | ECharts / Recharts |
| Excel | xlsx / ExcelJS |
| 认证 | JWT (HMAC-SHA256) |
| 部署 | Docker + Docker Compose + Nginx |

## 📁 项目结构

```
PharmaFlow/
├── src/
│   ├── PharmaFlow.Domain/          # 领域实体与枚举
│   ├── PharmaFlow.Application/     # 应用服务层与 DTO
│   ├── PharmaFlow.Infrastructure/  # 数据访问与 EF Core
│   ├── PharmaFlow.WebApi/          # ASP.NET Core Web API
│   └── PharmaFlow.WebClient/       # React 前端
├── Dockerfile.backend              # 后端 Docker 镜像
├── Dockerfile.frontend             # 前端 Nginx Docker 镜像
├── docker-compose.yml              # Docker Compose 编排
├── nginx.conf                      # Nginx 配置
├── deploy.sh                       # 一键部署脚本
└── .env.example                    # 环境变量模板
```

## 🚀 本地开发

### 环境要求
- .NET 8.0 SDK
- Node.js 20+

### 启动后端
```bash
cd src/PharmaFlow.WebApi
dotnet run
# API: http://localhost:5009
# Swagger: http://localhost:5009/swagger
```

### 启动前端
```bash
cd src/PharmaFlow.WebClient
npm install
npm run dev
# 前端: http://localhost:5173
```

### 登录账号
- 用户名：`admin`
- 密码：`admin123`

## 🐳 Docker 部署

```bash
# 1. 克隆仓库
git clone https://github.com/xunzhaoLoVe/PharmaFlow.git
cd PharmaFlow

# 2. 配置环境变量
cp .env.example .env
nano .env  # 修改数据库密码和 JWT 密钥

# 3. 一键部署
chmod +x deploy.sh
./deploy.sh
```

部署完成后访问 `http://服务器IP`。

### 服务端口
| 服务 | 端口 | 说明 |
|------|------|------|
| 前端 (Nginx) | 80 | React SPA |
| 后端 API | 5000 | 仅内部通信 |
| PostgreSQL | 5432 | 仅内部通信 |

## 🔐 权限体系

| 角色 | 权限范围 | 说明 |
|------|---------|------|
| 管理员 | `*` 全部 | 可管理用户、分配角色 |
| 操作员 | 8 模块 | 全部读写权限 |
| 只读用户 | 8 模块 | 仅查看，无操作按钮 |

密码安全：SHA256 加盐哈希 + 5 次错误锁定 15 分钟 + 记住我功能。

## 📋 功能模块

- **仪表盘** — 统计卡片、经销商构成、终端客户省份地图
- **药品管理** — 增删改查 + Excel 导入导出
- **客户管理** — 经销商与终端客户管理 + Excel 导入
- **采购管理** — 本公司出货至经销商
- **销售管理** — 下游流转记录
- **库存管理** — 经销商库存查询 + 低库存预警
- **流向查询** — 药品流向追溯图（分层 DAG）
- **串货预警** — 渠道冲突检测
- **报表统计** — 终端客户销售明细 + 日期/药品/规格筛选
- **用户管理** — 用户 CRUD + 角色分配

## ⚠️ 注意事项

1. 生产环境务必修改 `.env` 中的 `DB_PASSWORD` 和 `JWT_SECRET`
2. 前端构建跳过了 `tsc` 类型检查（Ant Design 6 类型不兼容），运行时正常
3. 地图数据 (`china-geo.json`) 已内置于 `public/` 目录
4. 开发环境默认使用 SQLite，Docker 环境使用 PostgreSQL

## 📄 License

MIT
