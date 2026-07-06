import requests, json, sys, os
from datetime import datetime, timedelta

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://39.102.206.115:40444"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "salesapi_token.txt")
USERNAME = "jiangsushenlongyy"
PASSWORD = "111111"

today = datetime.now()
first_this_month = today.replace(day=1)
start_month = first_this_month.month - 3
start_year = first_this_month.year
if start_month <= 0:
    start_month += 12
    start_year -= 1
start = first_this_month.replace(year=start_year, month=start_month).strftime("%Y-%m-%d")
end = (first_this_month - timedelta(days=1)).strftime("%Y-%m-%d")
print(f"Range: {start} ~ {end}", file=sys.stderr)

def get_token():
    """Try existing token first, auto-login if needed"""
    # Check saved token
    if os.path.exists(TOKEN_FILE):
        with open(TOKEN_FILE) as f:
            t = f.read().strip()
        if t:
            return t
    # Auto-login
    return login()

def login():
    """Get new token from server"""
    print("Getting new token...", file=sys.stderr)
    r = requests.post(f"{BASE}/service/token", data={
        "userName": USERNAME, "password": PASSWORD, "grant_type": "password",
    }, headers={
        "User-Agent": "Mozilla/5.0", "Referer": f"{BASE}/",
        "Content-Type": "application/x-www-form-urlencoded",
    }, timeout=15)
    resp = r.json()
    token = resp.get("access_token", "")
    if token:
        with open(TOKEN_FILE, "w") as f:
            f.write(token)
        print(f"Token expires: {resp.get('.expires', '?')}", file=sys.stderr)
        return token
    print(f"Login failed: {resp}", file=sys.stderr)
    return ""

token = get_token()
if not token:
    print("[]")
    sys.exit(0)

HEADERS = {
    "Authorization": f"Bearer {token}",
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    "Referer": f"{BASE}/", "Content-Type": "application/x-www-form-urlencoded",
    "X-Requested-With": "XMLHttpRequest",
}

FIELD_MAP = {
    "salesDate": "销售日期", "productCode": "商品编码", "productName": "商品名称",
    "specification": "规格", "productUnit": "单位", "batchNumber": "批号",
    "productionDate": "生产日期", "validityPeriod": "效期", "dosageForm": "剂型",
    "quantity": "数量", "amountMoney": "金额", "taxIncluded": "含税",
    "businessName": "客户名称", "customerCode": "客户编码",
    "supplierName": "供应商", "supplierNumber": "供应商编码",
    "orgName": "机构", "vendorsName": "经销商",
    "businessType": "业务类型", "manufacturer": "生产企业",
    "deliveryAddress": "配送地址", "medicalpracticehospital": "院内药房",
    "clientProvince": "省", "clientCity": "市", "clientCounty": "区县",
    "salesTime": "销售时间", "enterpriseType": "企业类型",
}

# ═══ Query ═══
all_records = []
page = 1
while True:
    r = requests.post(f"{BASE}/service/api/Sales/GetSalesInFor", data={
        "startDate": start, "endDate": end,
        "orgCode": "0", "productName": "", "vendorsName": "", "orgName": "",
        "pagerConfig[pageSize]": "1000", "pagerConfig[currentPage]": str(page),
    }, headers=HEADERS, timeout=30)
    # Token expired? Auto-login and retry
    if r.status_code == 401:
        print("Token expired, refreshing...", file=sys.stderr)
        token = login()
        if not token:
            break
        HEADERS["Authorization"] = f"Bearer {token}"
        r = requests.post(f"{BASE}/service/api/Sales/GetSalesInFor", data={
            "startDate": start, "endDate": end,
            "orgCode": "0", "productName": "", "vendorsName": "", "orgName": "",
            "pagerConfig[pageSize]": "1000", "pagerConfig[currentPage]": str(page),
        }, headers=HEADERS, timeout=30)
    resp = r.json()
    if resp.get("isError"):
        print(f"API error: {resp.get('errorMsg')}", file=sys.stderr)
        break
    rows = resp.get("datas", [])
    if not rows:
        break
    for row in rows:
        entry = {}
        for k, v in row.items():
            cn = FIELD_MAP.get(k, k)
            entry[cn] = str(v) if v is not None else ""
        all_records.append(entry)
    total = resp.get("dataCount", 0)
    if len(all_records) >= total:
        break
    page += 1
    if page > 100:
        break

# Only keep mapped fields
clean = [{k: v for k, v in r.items() if k in FIELD_MAP.values()} for r in all_records]
print(f"Records: {len(clean)}", file=sys.stderr)
if clean:
    print(json.dumps(clean, ensure_ascii=False))
else:
    print("[]")
