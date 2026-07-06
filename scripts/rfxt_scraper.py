import requests, json, sys, os, re, html as html_mod
from datetime import datetime, timedelta

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://rfxt.xmain.net"
USERNAME = "wangping"
PASSWORD = "1"
REPORT_ID = "0N8VKGCMFC4"
REPORT_DATA_ID = "0N8VKG2MTVK"
PARAM_JSON = "Params_1BA2PEJA4VC4A671BA2PEJA4VC4A671B"

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

s = requests.Session()
s.headers.update({"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"})

# ═══ Login ═══
r = s.get(f"{BASE}/Account/Login", timeout=15)
action = re.search(r'<form[^>]*action="([^"]+)"', r.text, re.I)
if not action:
    print("Login page error", file=sys.stderr); print("[]"); sys.exit(0)
login_url = BASE + html_mod.unescape(action.group(1))
csrf = re.search(r'__RequestVerificationToken[^>]*value="([^"]+)"', r.text, re.I)
r2 = s.post(login_url, data={
    "__RequestVerificationToken": csrf.group(1) if csrf else "",
    "UserName": USERNAME, "Password": PASSWORD,
}, headers={"Content-Type": "application/x-www-form-urlencoded", "Referer": r.url}, timeout=15)
if ".kAUTH" not in str(s.cookies):
    print("Login failed", file=sys.stderr); print("[]"); sys.exit(0)
print("Logged in", file=sys.stderr)

# ═══ Query report data ═══
r3 = s.post(f"{BASE}/ReportView/ReportSearchData", data={
    "reportid": REPORT_ID, "reportdataid": REPORT_DATA_ID, "param_json": PARAM_JSON,
}, headers={"Content-Type": "application/x-www-form-urlencoded", "X-Requested-With": "XMLHttpRequest",
             "Referer": f"{BASE}/"}, timeout=30)

# Parse HTML table
trs = re.findall(r'<tr[^>]*>(.*?)</tr>', r3.text, re.I | re.S)
data_rows = []
for tr in trs:
    tds = re.findall(r'<t[dh][^>]*>(.*?)</t[dh]>', tr, re.I | re.S)
    cells = [re.sub(r'<[^>]+>', ' ', td).strip() for td in tds]
    cells = [c for c in cells if c]
    if len(cells) >= 5:
        data_rows.append(cells)

if len(data_rows) < 2:
    print("No data", file=sys.stderr); print("[]"); sys.exit(0)

FIELD_MAP = {
    "报表名称": "报表名称", "时间": "日期", "销售时间": "销售时间",
    "单据编号": "单据编号", "客户名称": "客户名称",
    "产品编码": "商品编码", "产品名称": "商品名称",
    "规格": "规格", "单位": "单位", "数量": "数量",
    "批号": "批号", "有效期": "效期", "生产厂家": "生产企业",
    "业务类型": "业务类型", "金额": "金额", "价格": "单价",
    "供应商": "供应商", "客户编码": "客户编码",
}

headers = data_rows[0]
result = []
for row in data_rows[1:]:
    entry = {}
    for i, cell in enumerate(row):
        key = headers[i] if i < len(headers) else f"Col{i}"
        entry[FIELD_MAP.get(key, key)] = cell
    if any(v.strip() for v in entry.values()):
        result.append(entry)

print(f"Records: {len(result)}", file=sys.stderr)
print(json.dumps(result, ensure_ascii=False))
