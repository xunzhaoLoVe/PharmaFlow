# PharmaFlow 项目记忆索引

## Skills（可复用的工作流）

- [新增采集平台配置](.claude/skills/data-collection-platform.md) — 新增外部流向平台采集源的完整配置指南
- [前后端部署发布](.claude/skills/deploy.md) — 本地构建→上传服务器→Docker 更新的完整流程
- [数据采集问题排查](.claude/skills/collection-troubleshoot.md) — 采集0条/502/Token过期等问题诊断
- [项目框架速查](.claude/skills/project-framework.md) — 架构/实体/API/前端模式/快速开发指引

## 项目关键信息

- **CLAUDE.md**: 项目总指引（技术栈/文件位置/部署脚本/注意事项）
- **本地运行**: 后端 `dotnet run` (5009端口) + 前端 `npm run dev` (5173端口)
- **服务器**: 14.103.86.138, SSH root/123123123qwe., Docker Compose
- **数据库**: PostgreSQL (服务器) / SQLite (本地)
- **默认账号**: admin / admin123
