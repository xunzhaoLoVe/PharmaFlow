# 数据采集问题排查

PharmaFlow 数据采集的常见问题诊断和修复方法。

---

## 一、问题定位总则

### 查看后端日志
```bash
# 本地开发 — 直接看控制台输出
cd src/PharmaFlow.WebApi && dotnet run

# 服务器 — 看 Docker 日志
docker logs pharmaflow-backend --tail 100

# 搜索采集相关日志
docker logs pharmaflow-backend 2>&1 | grep -E "采集|Collection|Python|导出"
```

### 查看采集记录
```bash
# 本地 SQLite
cd src/PharmaFlow.WebApi
sqlite3 PharmaFlow.db "SELECT Id, Name, LastCollectionStatus, LastCollectionAt FROM CollectionSources;"
sqlite3 PharmaFlow.db "SELECT COUNT(*) FROM CollectionRecords WHERE SourceId='<SourceId>';"

# 服务器 PostgreSQL
docker exec pharmaflow-db psql -U pharmaflow -d PharmaFlow -c "SELECT \"Id\", \"Name\", \"LastCollectionStatus\" FROM \"CollectionSources\";"
```

---

## 二、常见问题

### 问题1: 采集为 0 条记录

**症状**: 采集执行成功但记录数为 0。

**排查步骤**:

1. **检查后端日志** — 看 "导出" 相关输出
   ```bash
   # 找类似日志
   docker logs pharmaflow-backend 2>&1 | grep -i "导出\|Export\|解析\|parse"
   ```

2. **检查 API 响应** — Python 脚本模式看 stderr 输出
   - 脚本中的 `print("Records: N", file=sys.stderr)` 会出现在日志中
   - 检查 API 是否返回了数据

3. **检查日期范围** — 可能日期范围内的确无数据
   - 确认 `startDate` / `endDate` 变量替换是否生效
   - 尝试手动扩大日期范围

4. **检查认证** — Token/Cookie 可能已过期
   - Python 脚本: 检查 token 文件，确认自动登录逻辑正常
   - JsonConfig: 重新从浏览器复制 Token

5. **检查响应格式** — 数据解析可能失败
   - chartMain: 确认响应中包含正确的数组结构
   - HTML: 确认 `tableSelector` / `rowSelector` 选择器匹配
   - GWT: 确认 `//OK` 响应头和 gzip 解压正常

### 问题2: 502 Bad Gateway

**症状**: 前端请求返回 502。

**原因**: 后端服务未运行或已崩溃。

**修复**:
```bash
# 本地
cd src/PharmaFlow.WebApi && dotnet run

# 服务器
cd /opt/PharmaFlow
docker compose ps                          # 确认 backend 状态
docker compose up -d backend               # 启动
docker compose restart backend             # 重启
```

### 问题3: Token/Cookie 过期

**症状**: 之前能采集，突然返回空或认证错误。

**JsonConfig 模式修复**:
1. 浏览器 F12 → Application → Cookies
2. 复制新的 Token/Cookie 值
3. 在采集源配置中更新 AuthConfig 的 `token.value`
4. 或用前端的「更新Token」按钮（如有）

**Python 脚本模式修复**:
1. 确认脚本有自动登录逻辑
2. 删除保存的 token 文件（如 `gwt_token.txt`），触发重新登录
3. 检查账号密码是否仍然有效

### 问题4: body_template 变量不替换

**症状**: 请求体中的 `{{yesterday}}` 没有被替换成实际日期。

**修复**: 确保 `body_template` 放在 **外层**（与 `export` 同级），而不是 `export` 内部。

```json
// ❌ 错误 — body_template 在 export 内
{
  "export": {
    "url": "/api/data",
    "body_template": { "params": { "date": "{{yesterday}}" } }
  }
}

// ✅ 正确 — body_template 在外层
{
  "export": { "url": "/api/data", "method": "POST" },
  "body_template": { "params": { "date": "{{yesterday}}" } }
}
```

### 问题5: Form POST 参数缺失

**症状**: Form POST 请求的参数没有被发送。

**修复**: Form POST 用 `params` 放在 `export` **内部**（与 JSON POST 相反）。

```json
{
  "export": {
    "url": "/api/export",
    "method": "POST",
    "content_type": "application/x-www-form-urlencoded",
    "params": {
      "startDate": "{{startDate}}",
      "endDate": "{{endDate}}"
    }
  }
}
```

### 问题6: Python 脚本报错

**症状**: 日志中出现 Python 异常。

**常见原因**:
1. **缺少依赖**: `pip install requests` 等
2. **文件路径错误**: `TOKEN_FILE` / `PAYLOAD_FILE` 路径拼接问题
3. **编码错误**: 确保 `sys.stdout.reconfigure(encoding='utf-8')`
4. **curl 在 Windows 不可用**: 用 `requests` 替代 `subprocess.run(['curl', ...])`

**手动测试脚本**:
```bash
cd scripts
python gwt_scraper.py
# 检查 stdout 是否是有效 JSON 数组
# 检查 stderr 的日志输出
```

### 问题7: chartMain 解析 null 元素

**症状**: 华润等 chartMain 格式采集报错或数据不完整。

**说明**: chartMain 响应中可能有 Java null 对象。解析器已处理此情况，但如果仍有问题：
1. 检查响应中数组结构是否正确
2. 确认 dataFormat 设置为 `chartMain`

### 问题8: 采集调度不执行

**症状**: 设置了 Cron 表达式但到时间不执行。

**排查**:
1. 确认采集源 `IsEnabled = true`
2. 确认 `ScheduleCron` 格式正确（5 段式: `分 时 日 月 周`）
3. `CollectionSchedulerHostedService` 每 60 秒检查一次，等待最多 1 分钟
4. 检查后端日志中 "Schedule" 相关输出

---

## 三、快速诊断命令

```bash
# 一次性诊断（服务器）
docker exec pharmaflow-db psql -U pharmaflow -d PharmaFlow -c "
  SELECT \"Name\", \"LastCollectionStatus\", \"LastCollectionAt\", \"CollectionMode\"
  FROM \"CollectionSources\"
  WHERE \"IsDeleted\" = false
  ORDER BY \"LastCollectionAt\" DESC NULLS LAST;
"

# 查看最近采集记录数
docker exec pharmaflow-db psql -U pharmaflow -d PharmaFlow -c "
  SELECT s.\"Name\", COUNT(*) as cnt, MAX(r.\"CollectedAt\") as last_record
  FROM \"CollectionRecords\" r
  JOIN \"CollectionSources\" s ON r.\"SourceId\" = s.\"Id\"
  GROUP BY s.\"Name\";
"
```
