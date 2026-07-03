# 前后端部署发布

PharmaFlow 项目的完整部署流程。目标服务器: 14.103.86.138，Docker Compose 部署。

---

## 一、部署前检查

- [ ] 代码已提交 (`git status`)
- [ ] 本地测试通过 (`dotnet run` + `npm run dev`)
- [ ] SSH 能连上服务器
- [ ] 确认是仅前端 / 仅后端 / 前后端都部署

---

## 二、仅前端部署

适用: 只改了 React 代码（页面、组件、样式等）。

```bash
# Step 1: 本地构建
cd "e:\流向平台\PharmaFlow\src\PharmaFlow.WebClient" && npm run build

# Step 2: 上传 + 更新容器（单次 Python 脚本，避免多次 SSH）
python -c "
import paramiko, os, time
h='14.103.86.138'; u='root'; p='123123123qwe.'
# 连接 + 建目录
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(h,username=u,password=p,timeout=30)
c.exec_command('mkdir -p /tmp/fd/assets',timeout=5)
c.close()
time.sleep(3)  # 避免 SSH 频率限制
# SFTP 上传
t=paramiko.Transport((h,22)); t.connect(username=u,password=p)
s=paramiko.SFTPClient.from_transport(t)
d=r'e:\流向平台\PharmaFlow\src\PharmaFlow.WebClient\dist'
for f in os.listdir(d):
    fp=os.path.join(d,f)
    if os.path.isfile(fp): s.put(fp,f'/tmp/fd/{f}')
for f in os.listdir(os.path.join(d,'assets')):
    s.put(os.path.join(d,'assets',f),f'/tmp/fd/assets/{f}')
s.close(); t.close()
time.sleep(3)
# 替换容器内文件 + reload
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(h,username=u,password=p,timeout=30)
c.exec_command('docker exec pharmaflow-web sh -c \"rm -f /usr/share/nginx/html/assets/*.js\" && docker cp /tmp/fd/. pharmaflow-web:/usr/share/nginx/html/ && docker exec pharmaflow-web nginx -s reload',timeout=30)
c.close()
print('前端部署完成')
"
```

**验证**: 浏览器访问 http://14.103.86.138，Ctrl+F5 强制刷新，检查新功能。

---

## 三、仅后端部署

适用: 只改了 C# 代码（Controller/Service/Entity 等）。

```bash
# Step 1: 本地发布
export PATH="$HOME/AppData/Local/Microsoft/dotnet:$PATH"
rm -rf "e:\流向平台\PharmaFlow\_publish"
cd "e:\流向平台\PharmaFlow\src\PharmaFlow.WebApi"
dotnet publish -c Release -r linux-x64 -o "e:\流向平台\PharmaFlow\_publish"

# Step 2: 上传 + 重建容器（需要 5 分钟以上）
python -c "
import paramiko, os, time
h='14.103.86.138'; u='root'; p='123123123qwe.'
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(h,username=u,password=p,timeout=30)
c.exec_command('rm -rf /opt/PharmaFlow/publish && mkdir -p /opt/PharmaFlow/publish',timeout=10)
c.close()
time.sleep(3)
t=paramiko.Transport((h,22)); t.connect(username=u,password=p)
s=paramiko.SFTPClient.from_transport(t)
pub=r'e:\流向平台\PharmaFlow\_publish'
for f in os.listdir(pub):
    fp=os.path.join(pub,f)
    if os.path.isfile(fp): s.put(fp,f'/opt/PharmaFlow/publish/{f}')
s.close(); t.close()
time.sleep(3)
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(h,username=u,password=p,timeout=30)
c.exec_command('cd /opt/PharmaFlow && docker compose rm -fs backend && docker compose build --no-cache backend 2>&1',timeout=300)
c.exec_command('cd /opt/PharmaFlow && docker compose up -d backend 2>&1',timeout=60)
c.close()
print('后端部署完成')
"

# Step 3: 等待启动 + 验证
# 等 10 秒让服务启动
python -c "
import paramiko, time
time.sleep(10)
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect('14.103.86.138',username='root',password='123123123qwe.',timeout=30)
_, out, _ = c.exec_command('docker logs pharmaflow-backend --tail 5',timeout=10)
print(out.read().decode())
c.close()
"
```

**验证**: 
```bash
# 检查 API 是否正常
curl -s http://14.103.86.138/api/dashboard | head -100

# 检查容器状态
# SSH 到服务器: docker compose -f /opt/PharmaFlow/docker-compose.yml ps
```

---

## 四、前后端都部署

先执行后端部署（Step 1-3），再执行前端部署（Step 1-2）。

先后端再前端的顺序是因为后端构建慢（~5分钟），前端可以并行准备。

---

## 五、快速回滚

```bash
# 回滚后端（用上一个镜像）
cd /opt/PharmaFlow
docker compose down backend
docker compose up -d backend  # 用 docker-compose.yml 中指定的 image

# 回滚前端（重新构建旧代码 + 上传）
# 或服务器上保留上一个 dist 备份
cp -r /tmp/fd_backup/* /tmp/fd/
docker cp /tmp/fd/. pharmaflow-web:/usr/share/nginx/html/
docker exec pharmaflow-web nginx -s reload
```

---

## 六、注意事项

1. **SSH 频率限制**: 短时间多次 SSH 连接会被服务器拒绝。每次连接间隔 ≥ 3 秒（脚本中已加 `time.sleep(3)`）
2. **Docker 构建**: 后端 `--no-cache` 构建需要 2-5 分钟，耐心等待
3. **不要 `down -v`**: `docker compose down -v` 会删除数据库卷，数据全丢！
4. **前端缓存**: 部署后用户需 Ctrl+F5 强制刷新
5. **检查服务**: 部署后确认 `docker compose ps` 中所有服务状态为 Up
6. **数据库迁移**: 如果新增了 Entity/修改了表结构，EF Core 会在启动时自动迁移（Development 环境）
