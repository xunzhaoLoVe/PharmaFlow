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

# ═══════════════ 解析 v4 — 7个二进制块 + 共享JSON ═══════════════
body = text[4:]  # Remove //OK

# 1) Parse shared JSON data section
last_bracket = text.rfind('"],')
shared_values = []
if last_bracket >= 0:
    json_part = text[:last_bracket + 2]
    open_bracket = json_part.rfind('[')
    if open_bracket >= 0:
        json_part = json_part[open_bracket:]
        json_part = json_part.replace(r'\x3D', '=')
        try:
            items = json.loads(json_part)
            for item in items:
                if isinstance(item, str) and not re.match(r'(com\.|java\.|rO0)', item) and 'PagingLoad' not in item and 'ArrayList' not in item and 'FlowSaList' not in item:
                    shared_values.append(str(item))
        except:
            pass

# 2) Parse binary blocks for unique record IDs
blocks = re.split(r'(?<=,3,)(?=22,9,)', body)
block_ids = []
for block in blocks:
    if '22,9,' in block:
        nums = re.findall(r'(\d{6,})', block)
        if nums:
            block_ids.append(nums[0])

print(f"Found {len(block_ids)} records via blocks, {len(shared_values)} shared fields", file=sys.stderr)

# 3) Build output: one record per block with shared values + unique ID
# GWT JSON order: 0=批准文号, 1=客户名称, 2=数量1, 3=金额1, 4=商业公司,
# 5=生产厂家, 6=商品名称, 7=数量2, 8=规格, 9=单位, 10=批号,
# 11=生产企业简称, 12=地址, 13=金额2, 14=单价, 15=数量3,
# 16=金额3, 17=数量4, 18=金额4, 19=数量5, 20=金额5, 21=批号2
sv = shared_values
output = []
for bid in block_ids:
    entry = {
        "发货单位": sv[4] if len(sv) > 4 else "",
        "销售日期": "",
        "客户ID": bid,
        "客户名称": sv[1] if len(sv) > 1 else "",
        "商品ID": "",
        "品名": sv[6] if len(sv) > 6 else "",
        "规格": sv[8] if len(sv) > 8 else "",
        "厂家": sv[5] if len(sv) > 5 else "",
        "数量": sv[2] if len(sv) > 2 else "",
        "单位": sv[9] if len(sv) > 9 else "",
        "单价": sv[14] if len(sv) > 14 else "",
        "金额": sv[3] if len(sv) > 3 else "",
        "批号": sv[10] if len(sv) > 10 else "",
        "失效日期": "",
        "供应商": sv[5] if len(sv) > 5 else "",
        "收货地址": sv[12] if len(sv) > 12 else "",
        "流向提供商": "重庆桐君阁",
    }
    output.append(entry)

if not output and shared_values:
    entry = {
        "发货单位": sv[4] if len(sv) > 4 else "",
        "销售日期": "",
        "客户ID": "",
        "客户名称": sv[1] if len(sv) > 1 else "",
        "商品ID": "",
        "品名": sv[6] if len(sv) > 6 else "",
        "规格": sv[8] if len(sv) > 8 else "",
        "厂家": sv[5] if len(sv) > 5 else "",
        "数量": sv[2] if len(sv) > 2 else "",
        "单位": sv[9] if len(sv) > 9 else "",
        "单价": sv[14] if len(sv) > 14 else "",
        "金额": sv[3] if len(sv) > 3 else "",
        "批号": sv[10] if len(sv) > 10 else "",
        "失效日期": "",
        "供应商": sv[5] if len(sv) > 5 else "",
        "收货地址": sv[12] if len(sv) > 12 else "",
        "流向提供商": "重庆桐君阁",
    }
    output.append(entry)

print(f"Records: {len(output)}", file=sys.stderr)
for r in output[:1]:
    for k in ["商品名称", "规格", "客户名称", "批号", "记录ID"]:
        v = r.get(k, "")
        if len(v) > 50: v = v[:50] + "..."
        print(f"  {k}: {v}", file=sys.stderr)

print(json.dumps(output, ensure_ascii=False))
