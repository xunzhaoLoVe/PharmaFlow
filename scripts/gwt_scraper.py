"""GWT 流向平台采集脚本 v2 — 使用 requests + 正确解析 GWT-RPC 响应"""
import json, sys, os, re, requests
from datetime import datetime

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://183.71.233.2:8999"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
TOKEN_FILE = os.path.join(SCRIPT_DIR, "gwt_token.txt")
PAYLOAD_FILE = os.path.join(SCRIPT_DIR, "gwt_payload.bin")

# ── 日期范围：上月1日 ~ 昨天 ──
today = datetime.now()
if today.month == 1:
    sm, sy = 12, today.year - 1
else:
    sm, sy = today.month - 1, today.year
start_date = f'{sy}-{sm:02d}-01'
end_date = f'{today.year}-{today.month:02d}-{today.day:02d}'
print(f"Range: {start_date} ~ {end_date}", file=sys.stderr)

# ═══════════════ 自动登录 ═══════════════
def auto_login():
    try:
        salt = '*&^%Ybghs154.'
        s = requests.Session()
        s.get(f"{BASE}/gwtapp/", headers={"User-Agent": "Mozilla/5.0"}, timeout=10)
        r = s.post(f"{BASE}/gwtapp/user/login",
            json={"comefrom": "web", "userType": "2", "userId": "11341",
                  "passWord": salt + "11341"},
            headers={"User-Agent": "Mozilla/5.0", "Referer": f"{BASE}/gwtapp/",
                     "Content-Type": "application/json",
                     "X-Requested-With": "XMLHttpRequest",
                     "Origin": BASE, "Accept": "application/json"}, timeout=10)
        if r.json().get("UseStatus") == 1:
            sid = s.cookies.get("JSESSIONID")
            with open(TOKEN_FILE, "w") as f:
                f.write(sid)
            print("Auto-login OK", file=sys.stderr)
            return s
    except Exception as e:
        print(f"Auto-login error: {e}", file=sys.stderr)
    return None


# ═══════════════ 主流程 ═══════════════
session = auto_login()
if session is None:
    print("Login failed", file=sys.stderr)
    print("[]")
    sys.exit(0)

if not os.path.exists(PAYLOAD_FILE):
    print("No payload template", file=sys.stderr)
    print("[]")
    sys.exit(0)

# 读取并替换日期
with open(PAYLOAD_FILE, 'rb') as f:
    payload = f.read()
payload = payload.replace(b"2026-06-01", start_date.encode())
payload = payload.replace(b"2026-07-02 23:59:59", (end_date + " 23:59:59").encode())

# 发送 GWT-RPC 请求
headers = {
    'Content-Type': 'text/x-gwt-rpc; charset=UTF-8',
    'X-GWT-Module-Base': f'{BASE}/gwtapp/saflow/',
    'X-GWT-Permutation': '97246FD42C3EEA52AB9C2FF29A5E7C25',
    'Origin': BASE,
    'Referer': f'{BASE}/gwtapp/main.jsp',
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
}
try:
    r = session.post(f'{BASE}/gwtapp/saflow/dataService',
                     data=payload, headers=headers, timeout=60)
    text = r.text
except Exception as e:
    print(f"HTTP error: {e}", file=sys.stderr)
    print("[]")
    sys.exit(0)

if not text.startswith('//OK'):
    print(f"Error: {text[:200]}", file=sys.stderr)
    print("[]")
    sys.exit(0)

print(f"Response: {len(text)} chars", file=sys.stderr)

# ═══════════════ 解析 GWT-RPC 响应 ═══════════════
# 1) 找到双引号数据段 (从 "com.sencha 开始)
dq_start = re.search(r'"com\.sencha', text)
if not dq_start:
    print("No data section found", file=sys.stderr)
    print("[]")
    sys.exit(0)

data_part = text[dq_start.start():]

# 2) 截断尾部 GWT 元数据 ("],0,7 等)
end_marker = re.search(r'"\s*\]\s*,', data_part)
if end_marker:
    data_part = data_part[:end_marker.start() + 1]

# 3) 修复转义字符
data_part = data_part.replace(r'\x3D', '=').replace(r'\x3d', '=')

# 4) 解析为 JSON 字符串数组
try:
    items = json.loads('[' + data_part + ']')
except json.JSONDecodeError as e:
    print(f"JSON parse error: {e}", file=sys.stderr)
    print("[]")
    sys.exit(0)

# 5) 过滤: 跳过 Java 类型标记，FlowSaList 标记新行
def is_marker(val):
    """Java 类型标记、序列化头、容器类型"""
    if not isinstance(val, str):
        return True
    if re.match(r'(com\.|java\.|rO0)', val):
        return True
    if 'PagingLoadResultBean' in val or 'ArrayList' in val:
        return True
    return False

# 提取数据行：FlowSaList 标记行开始
rows = []
current_row = []
for item in items:
    if isinstance(item, str) and 'FlowSaList' in item:
        if current_row:
            rows.append(current_row)
        current_row = []
        continue
    if is_marker(item):
        continue
    current_row.append(str(item))

if current_row:
    rows.append(current_row)

print(f"Data rows: {len(rows)}", file=sys.stderr)

# ═══════════════ 字段映射 ═══════════════
# FlowSaList bean 字段顺序（跳过 Java 类型字段: Timestamp, Integer, BigDecimal）
FIELD_NAMES = [
    "批准文号",      # 0
    "客户名称",      # 1
    "数量1",         # 2
    "金额1",         # 3
    "商业公司",      # 4
    "生产厂家",      # 5
    "商品名称",      # 6
    "数量2",         # 7
    "规格",          # 8
    "单位",          # 9
    "批号",          # 10
    "生产企业简称",  # 11
    "地址",          # 12
    "金额2",         # 13
    "单价",          # 14
    "数量3",         # 15
    "金额3",         # 16
    "数量4",         # 17
    "金额4",         # 18
    "数量5",         # 19
    "金额5",         # 20
    "批号2",         # 21
]

output = []
for row in rows:
    entry = {}
    for i, val in enumerate(row):
        if i < len(FIELD_NAMES):
            entry[FIELD_NAMES[i]] = val
        else:
            entry[f"Col{i}"] = val
    output.append(entry)

print(f"Records: {len(output)}", file=sys.stderr)
for r in output:
    for k in ["商品名称", "规格", "客户名称", "批号", "数量1", "金额1"]:
        v = r.get(k, "")
        if len(v) > 50:
            v = v[:50] + "..."
        print(f"  {k}: {v}", file=sys.stderr)

# 输出 JSON 到 stdout
print(json.dumps(output, ensure_ascii=False))
