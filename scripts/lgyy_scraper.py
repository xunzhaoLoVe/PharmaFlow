import requests, json, sys, os
from datetime import datetime, timedelta

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://119.3.15.153:8100"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "lgyy_token.txt")
USERNAME = "jssl"
PASSWORD = "jssl"

today = datetime.now()
first_this_month = today.replace(day=1)
start_month = first_this_month.month - 3
start_year = first_this_month.year
if start_month <= 0:
    start_month += 12
    start_year -= 1
start = first_this_month.replace(year=start_year, month=start_month).strftime("%Y-%m-%d")
end = (first_this_month - timedelta(days=1)).strftime("%Y-%m-%d")

def get_token():
    if os.path.exists(TOKEN_FILE):
        with open(TOKEN_FILE) as f:
            t = f.read().strip()
        if t: return t
    return login()

def login():
    print("Getting new token...", file=sys.stderr)
    try:
        r = requests.post(f"{BASE}/LGYY/api/login",
            json={"username": USERNAME, "password": PASSWORD}, timeout=15)
        t = r.json().get("access_token", "")
        if t:
            with open(TOKEN_FILE, "w") as f: f.write(t)
            print(f"Token obtained", file=sys.stderr)
            return t
    except Exception as e:
        print(f"Login error: {e}", file=sys.stderr)
    return ""

token = get_token()
if not token:
    print("[]"); sys.exit(0)

HEADERS = {
    "Authorization": f"Bearer {token}",
    "User-Agent": "Mozilla/5.0", "Origin": "http://119.3.15.153:4100",
    "Referer": "http://119.3.15.153:4100/",
}

FIELD_MAP = {
    "id": "序号",
    "itemNo": "商品编号", "itemName": "商品名称", "spec": "商品规格",
    "manufacture": "生产厂家", "unit": "单位", "batchNo": "批号",
    "customerName": "客户名称", "customerNo": "客户编码",
    "customerType": "客户类型", "salesQty": "销售数量",
    "salesDepartment": "销售部门",
    "subVender": "销售员",
    "address": "送货地址",
    "price": "含税单价", "number": "含税金额",
    "salesDate": "销售日期", "stockDate": "入库日期",
    "area": "区域", "isGPO": "是否GPO",
    "stockoutNo": "出库单号", "venderName": "经销商",
}

# ═══ Paginated query ═══
all_records = []
offset = 0
while True:
    r = requests.get(f"{BASE}/LGYY/api/sales",
        params={"offset": offset, "max": 1000, "startTime": start, "endTime": end,
                "salesCustomer": "", "name": ""},
        headers=HEADERS, timeout=30)
    if r.status_code == 401:
        print("Token expired, refreshing...", file=sys.stderr)
        token = login()
        if not token: break
        HEADERS["Authorization"] = f"Bearer {token}"
        continue
    resp = r.json()
    rows = resp.get("results", [])
    if not rows: break
    for row in rows:
        entry = {}
        for k, v in row.items():
            entry[FIELD_MAP.get(k, k)] = str(v) if v is not None else ""
        all_records.append(entry)
    total = resp.get("total", 0)
    offset += len(rows)
    if offset >= total: break

# Only keep mapped fields
clean = [{k: v for k, v in r.items() if k in FIELD_MAP.values()} for r in all_records]
print(f"Records: {len(clean)}", file=sys.stderr)
if clean: print(json.dumps(clean, ensure_ascii=False))
else: print("[]")
