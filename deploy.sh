#!/bin/bash
# ============================================
# PharmaFlow 一键部署脚本
# 用法: chmod +x deploy.sh && ./deploy.sh
# ============================================

set -e

echo "=== PharmaFlow 部署开始 ==="

# 1. 检查 Docker 环境
if ! command -v docker &> /dev/null; then
    echo "❌ 请先安装 Docker: https://docs.docker.com/engine/install/"
    exit 1
fi

if ! command -v docker compose &> /dev/null; then
    echo "❌ 请安装 Docker Compose v2"
    exit 1
fi

# 2. 检查 .env 文件
if [ ! -f .env ]; then
    echo "⚠️  未找到 .env 文件，从 .env.example 创建默认配置..."
    cp .env.example .env
    echo "❗ 请编辑 .env 文件修改默认密码，然后重新运行此脚本"
    exit 1
fi

# 3. 拉取代码（如果从 git 部署）
# git pull origin master

# 4. 构建并启动
echo "📦 构建 Docker 镜像..."
docker compose build --pull

echo "🚀 启动服务..."
docker compose up -d

# 5. 等待健康检查
echo "⏳ 等待数据库就绪..."
sleep 5

echo "⏳ 等待后端就绪..."
for i in {1..30}; do
    if curl -sf http://localhost:5000/swagger/index.html > /dev/null 2>&1; then
        echo "✅ 后端已就绪"
        break
    fi
    sleep 2
done

# 6. 显示状态
echo ""
echo "=== 部署完成 ==="
docker compose ps
echo ""
echo "📌 访问地址: http://$(hostname -I 2>/dev/null | awk '{print $1}' || echo 'YOUR-SERVER-IP')"
echo "📖 Swagger:  http://YOUR-SERVER-IP/swagger"
echo "👤 默认账号: admin / admin123"
echo ""
echo "常用命令:"
echo "  docker compose logs -f      查看日志"
echo "  docker compose restart      重启服务"
echo "  docker compose down         停止服务"
echo "  docker compose up -d        启动服务"
