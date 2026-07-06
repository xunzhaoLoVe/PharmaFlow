# 新增采集平台配置

PharmaFlow 数据采集模块的完整配置指南。当用户需要新增一个外部流向平台的采集源时使用此 skill。

---

## 一、采集架构速览

```
定时/手动触发 → 认证登录 → 数据导出 → 解析 → 清洗 → 去重存储 → 字段映射 → 提交汇总
```

**关键实体**: `CollectionSource`（采集源配置） → `CollectionRecord`（采集原始记录） → `DistributionRecord`（汇总流向记录）

**两种采集模式**:
| 模式 | `CollectionMode` | 适用场景 |
|------|-------------------|---------|
| JSON配置模式 | `JsonConfig` | 标准 HTTP API 平台，用 JSON 配置 Auth + Export |
| Python脚本模式 | `PythonScript` | 复杂协议（GWT-RPC/加密/验证码），用 Python 脚本接管全部流程 |

---

## 二、新增平台工作流

### Step 1: 分析目标平台

1. 在浏览器 F12 → Network 中观察平台的 HTTP 请求
2. 确定：
   - 登录方式（表单/Cookie/Token/无认证）
   - 数据导出接口的 URL、Method、Content-Type、请求体格式
   - 响应数据格式（JSON/HTML/Excel/chartMain/GWT-RPC）
   - 是否需要分页
3. 判断适合 **JsonConfig** 还是 **PythonScript**

### Step 2: 在采集源配置页面创建

路径：前端 → 数据采集 → 采集源配置 → 新建

#### 2a. JsonConfig 模式 — 标准 HTTP

**AuthConfig — 三种认证方式**:

```json
// 方式1: Cookie/Header Token（手动从浏览器复制）
{
  "token": { "cookie_name": "FLOW_TOKEN", "value": "eyJ..." },
  "headers": { "Origin": "http://...", "Referer": "http://..." }
}

// 方式2: 表单登录（自动获取 Cookie）
{
  "loginPath": "/api/login",
  "username": "your_user",
  "password": "your_pass",
  "extraFields": { "rememberMe": "true" }
}

// 方式3: JSON登录并从响应提取Token（如国药控股江西 PSI）
{
  "loginPath": "/prod-api/login",
  "loginMethod": "POST",
  "loginHeaders": {
    "Accept": "application/json, text/plain, */*",
    "Origin": "http://111.74.9.154:8097",
    "Referer": "http://111.74.9.154:8097/login?redirect=%2Findex",
    "isToken": "false"
  },
  "preLogin": {
    "url": "/prod-api/captchaImage",
    "extract": [
      { "name": "uuid", "type": "json", "path": "uuid", "required": true },
      { "name": "code", "type": "json", "path": "code" }
    ]
  },
  "loginBodyJson": "{\"username\":\"...\",\"password\":\"...\",\"code\":\"{{code}}\",\"uuid\":\"{{uuid}}\"}",
  "tokenExtract": {
    "source": "json",
    "paths": ["token", "data.token", "access_token", "data.access_token"],
    "required": true,
    "targets": [
      { "type": "header", "name": "Authorization", "prefix": "Bearer " },
      { "type": "cookie", "name": "Psi-Syn-Token" }
    ]
  },
  "headers": {
    "Accept": "application/json, text/plain, */*",
    "Origin": "http://111.74.9.154:8097",
    "Referer": "http://111.74.9.154:8097/psisyn/psisale"
  }
}

// 方式4: 无认证
{}
```

**ExportConfig — 四种导出方式**:

```json
// JSON POST（最常见，body_template 放外层！）
{
  "export": {
    "url": "/api/data/export",
    "method": "POST",
    "content_type": "application/json",
    "dataFormat": "json",
    "dataPath": "data.list"
  },
  "body_template": {
    "db": "db",
    "params": {
      "startDate": "{{dayBeforeYesterday}}",
      "endDate": "{{yesterday}}"
    }
  }
}

// Form POST（params 放 export 内层）
{
  "export": {
    "url": "/api/export",
    "method": "POST",
    "content_type": "application/x-www-form-urlencoded",
    "dataFormat": "json",
    "params": {
      "startDate": "{{startDate}}",
      "endDate": "{{endDate}}"
    }
  }
}

// GET 请求
{
  "export": {
    "url": "/api/data?page=1&size=100",
    "method": "GET",
    "dataFormat": "json"
  }
}

// HTML 表格解析
{
  "export": {
    "url": "/report/table",
    "method": "GET",
    "dataFormat": "html",
    "tableSelector": "table.data-table",
    "rowSelector": "tbody tr"
  }
}
```

**日期变量**（采集时自动替换）:
| 变量 | 含义 |
|------|------|
| `{{startDate}}` | 30天前 |
| `{{endDate}}` | 昨天 |
| `{{7daysAgo}}` | 7天前 |
| `{{today}}` | 今天 |
| `{{yesterday}}` | 昨天 |
| `{{dayBeforeYesterday}}` | 前天 |

#### 2b. PythonScript 模式 — 复杂协议

当 JsonConfig 无法满足时使用（GWT-RPC、加密请求、验证码等）。

**配置**:
```json
{
  "export": {
    "pythonScript": "e:/流向平台/PharmaFlow/scripts/xxx_scraper.py",
    "dataFormat": "json"
  }
}
```

收集模式选 `PythonScript`，AuthConfig 留空 `{}`。

**脚本规范**:
- 接收 0 个命令行参数（日期在脚本内计算）
- stdout 输出 JSON 数组 `[{...}, {...}]`
- stderr 输出日志（`print("msg", file=sys.stderr)`）
- 退出码 0 表示成功
- 脚本放在 `scripts/` 目录下
- 依赖文件（token、payload）也放 `scripts/`

**Python 脚本模板**:
```python
import json, sys, requests
from datetime import datetime, timedelta

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# 1. 计算日期范围
today = datetime.now()
if today.month == 1:
    sm, sy = 12, today.year - 1
else:
    sm, sy = today.month - 1, today.year
start_date = f'{sy}-{sm:02d}-01'
end_date = f'{today.year}-{today.month:02d}-{today.day:02d}'

# 2. 认证
s = requests.Session()
r = s.post(f"{BASE}/api/login", json={"username": "u", "password": "p"})
token = r.json().get("token")

# 3. 导出数据
r = s.post(f"{BASE}/api/export",
    json={"startDate": start_date, "endDate": end_date},
    headers={"Authorization": f"Bearer {token}"})
data = r.json()

# 4. 输出 JSON 数组到 stdout
print(json.dumps(data, ensure_ascii=False))
```

### Step 3: 测试采集

1. 保存采集源配置
2. 点击「采集」按钮，等待后端执行
3. 切到「采集数据管理」Tab，查看采集到的数据条数
4. 如果 0 条 → 查看后端日志排查（参见 `collection-troubleshoot` skill）

### Step 4: 配置字段映射

1. 在「采集数据管理」中找到该采集源
2. 点击「配置映射」
3. 设置目标实体（通常是 `DistributionRecord`）
4. 逐个字段配置源字段到目标字段的映射
5. 配置校验规则、主数据匹配、智能清洗选项
6. 保存

### Step 5: 提交到汇总

1. 在「采集数据管理」中点击「提交」
2. 系统执行：字段映射 → 校验 → 主数据匹配 → 清洗 → 去重 → 写入 `DistributionRecord`
3. 切到「采集汇总」Tab 验证结果

---

## 三、已知平台配置参考

| 平台 | 模式 | 关键点 |
|------|------|--------|
| 华润chartMain | JsonConfig | dataFormat: chartMain，需处理 null 元素 |
| GWT流向平台 | PythonScript | GWT-RPC 协议，二进制 payload，gzip 解压 |
| 青岛流向平台 | JsonConfig | 标准 JSON API + Cookie 认证 |

---

## 四、常见陷阱

1. **body_template 位置**: 放在外层（与 `export` 同级），不要放在 `export` 内部
2. **Form POST 参数**: `params` 放在 `export` 节点内部
3. **日期变量拼写**: `{{dayBeforeYesterday}}` 完整驼峰，不要简写
4. **Token 过期**: Python 脚本需实现自动登录 + token 保存机制
5. **采集模式**: 使用 PythonScript 时 CollectionMode 必须选 PythonScript，否则走 HTTP 流程
6. **脚本路径**: Windows 环境用正斜杠或双反斜杠 `e:/xxx/script.py` 或 `e:\\xxx\\script.py`
7. **编码**: Python 脚本 stdout 必须 UTF-8，中文平台尤其注意
