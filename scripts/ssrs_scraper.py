import requests, json, sys, os, re, io, csv
from datetime import datetime, timedelta
from ddddocr import DdddOcr

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://xsyy.yy5u.com:8080"
REPORT_BASE = "http://app1.yy5u.com:8080"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ssrs_token.txt")
USERNAME = "250706"
PASSWORD = "zj~123456"

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
logged_in = False

# ═══ Attempt 1: Auto-login ═══
try:
    ocr = DdddOcr(show_ad=False, beta=True)
    # GET login page + captcha
    r = s.get(f"{BASE}/syslogin.aspx?result=5&txtCompanyID=22",
        headers={"User-Agent": "Mozilla/5.0"}, timeout=10)
    r2 = s.get(f"{BASE}/verifyimage.aspx?time=0.1",
        headers={"User-Agent": "Mozilla/5.0", "Referer": r.url}, timeout=10)
    captcha_text = ocr.classification(r2.content)
    # Extract digits only
    code = ''.join(c for c in captcha_text if c.isdigit())
    print(f"Captcha: {code}", file=sys.stderr)

    vs = re.search(r'id="__VIEWSTATE" value="([^"]+)"', r.text, re.I)
    vg = re.search(r'id="__VIEWSTATEGENERATOR" value="([^"]+)"', r.text, re.I)

    r3 = s.post(f"{BASE}/syslogin.aspx?result=5&txtCompanyID=22", data={
        "__VIEWSTATE": vs.group(1) if vs else "",
        "__VIEWSTATEGENERATOR": vg.group(1) if vg else "",
        "UserName": USERNAME, "PassWord": PASSWORD,
        "vcode": code, "result": "5", "txtCompanyID": "22"
    }, headers={"User-Agent": "Mozilla/5.0", "Referer": r.url,
        "Content-Type": "application/x-www-form-urlencoded"}, timeout=10,
        allow_redirects=False)
    if r3.status_code == 302:
        logged_in = True
        print("Auto-login OK", file=sys.stderr)
    else:
        print(f"Login failed: {r3.status_code}", file=sys.stderr)
except Exception as e:
    print(f"Auto-login error: {e}", file=sys.stderr)

# ═══ Attempt 2: Use token file ═══
if not logged_in and os.path.exists(TOKEN_FILE):
    with open(TOKEN_FILE) as f:
        sid = f.read().strip()
    if sid:
        s = requests.Session()
        s.cookies.set("ASP.NET_SessionId", sid)
        print(f"Using token: {sid[:16]}...", file=sys.stderr)
        logged_in = True

if not logged_in:
    print("No valid auth", file=sys.stderr)
    print("[]")
    sys.exit(0)

# ═══ Step 1: GET query page ═══
url = f"{BASE}/user/GoodsfluxwayQuery.aspx?reportUrl=http://app1.yy5u.com:8080/ReportServer?/&reportName=GoodsfluxwayQuery&frmheight=180"
r = s.get(url, headers={"User-Agent": "Mozilla/5.0"}, timeout=15)
vs = re.search(r'id="__VIEWSTATE" value="([^"]+)"', r.text, re.I)
vg = re.search(r'id="__VIEWSTATEGENERATOR" value="([^"]+)"', r.text, re.I)
if not vs:
    print("Session expired", file=sys.stderr)
    print("[]")
    sys.exit(0)

# ═══ Step 2: POST query ═══
r2 = s.post(r.url, data={
    "__VIEWSTATE": vs.group(1), "__VIEWSTATEGENERATOR": vg.group(1) if vg else "",
    "__EVENTTARGET": "btnQuery", "__EVENTARGUMENT": "",
    "BeginTime": start, "EndTime": end,
    "txtCustomName": "", "dropBizType": "", "txtGoodsNameSE": "全部",
    "txtGoodsID": "", "dropDepartName": ""
}, headers={"User-Agent": "Mozilla/5.0", "Referer": r.url,
    "Content-Type": "application/x-www-form-urlencoded"}, timeout=30)

# ═══ Step 3: Get report URL ═══
report_url = re.search(r"location\.href='([^']+)'", r2.text)
if not report_url:
    print("No report URL", file=sys.stderr)
    print("[]")
    sys.exit(0)

# ═══ Step 4: Get CSV ═══
csv_url = report_url.group(1) + "&rs:Format=CSV"
r3 = s.get(csv_url, headers={"User-Agent": "Mozilla/5.0"}, timeout=60)
r3.encoding = 'utf-8-sig'

# ═══ Step 5: Parse ═══
reader = csv.reader(io.StringIO(r3.text))
rows_data = list(reader)
if len(rows_data) < 2:
    print("No data", file=sys.stderr); print("[]"); sys.exit(0)

headers = [h.strip() for h in rows_data[0]]
FIELD_MAP = {
    "Bizdate": "日期", "BizType": "业务类型", "CompanyName": "客户名称",
    "Quantity": "数量", "textbox20": "单位", "RetailPrice": "零售价",
    "ShowOnTaxAmount": "含税金额", "GoodsName": "商品名称", "DepartName": "部门",
    "DeliverAddress": "配送地址", "AreaName": "区域", "remark": "备注",
    "BizSN_1": "单据号", "CustomerCode": "客户编码", "GoodsCode": "商品编码",
    "SyncDate": "同步日期",
}
result = []
for row in rows_data[1:]:
    vals = [v.strip() if v else '' for v in row]
    if any('汇' in v and '总' in v for v in vals):
        continue
    entry = {}
    for i, h in enumerate(headers):
        cn = FIELD_MAP.get(h, h)
        entry[cn] = vals[i] if i < len(vals) else ""
    if any(v for v in entry.values()):
        result.append(entry)

print(f"Records: {len(result)}", file=sys.stderr)
print(json.dumps(result, ensure_ascii=False))
