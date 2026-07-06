import requests, json, sys, os, re
from datetime import datetime, timedelta
from ddddocr import DdddOcr

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://tzsjdyy.com:81"
USERNAME = "1650"
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
s.headers.update({"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"})

# ═══ Login ═══
r = s.get(f"{BASE}/zb/login.aspx", timeout=30)
r2 = s.get(f"{BASE}/zb/Confirmation.aspx", headers={"Referer": r.url}, timeout=30)
ocr = DdddOcr(show_ad=False)
code = ''.join(c for c in ocr.classification(r2.content) if c.isdigit())
vs = re.search(r'id="__VIEWSTATE" value="([^"]+)"', r.text, re.I)
vg = re.search(r'id="__VIEWSTATEGENERATOR" value="([^"]+)"', r.text, re.I)
r3 = s.post(f"{BASE}/zb/login.aspx", data={
    "__VIEWSTATE": vs.group(1), "__VIEWSTATEGENERATOR": vg.group(1) if vg else "",
    "txtusername": USERNAME, "txtpassword": PASSWORD, "txtcheck": code,
    "BtnLogin.x": "10", "BtnLogin.y": "10",
}, headers={"Referer": r.url, "Content-Type": "application/x-www-form-urlencoded"}, timeout=30)
if "txtusername" in r3.text.lower():
    print("Login failed", file=sys.stderr); print("[]"); sys.exit(0)
print(f"Logged in (captcha: {code})", file=sys.stderr)

# ═══ GET query page, extract dynamic fields ═══
r4 = s.get(f"{BASE}/zb/QueryModel.aspx?QID=76084", headers={"Referer": r3.url}, timeout=30)
html = r4.text
vs2 = re.search(r'id="__VIEWSTATE" value="([^"]+)"', html, re.I)
vg2 = re.search(r'id="__VIEWSTATEGENERATOR" value="([^"]+)"', html, re.I)
tsm = re.search(r'id="ctl00_ToolkitScriptManager1_HiddenField" value="([^"]*)"', html, re.I)

# ═══ POST query with AJAX ═══
r5 = s.post(r4.url, data={
    "QID": "76084", "__VIEWSTATE": vs2.group(1), "__VIEWSTATEGENERATOR": vg2.group(1) if vg2 else "",
    "__EVENTTARGET": "", "__EVENTARGUMENT": "",
    "ctl00$ToolkitScriptManager1": "ctl00$ContentPlaceHolder3$UpdatePanel1|ctl00$ContentPlaceHolder3$UDQRefreshButton",
    "ctl00_ToolkitScriptManager1_HiddenField": tsm.group(1) if tsm else "",
    "ctl00$ContentPlaceHolder3$ZBPAGEBARID": "1;2",
    "ctl00$ContentPlaceHolder3$ZBALLCOLS": "sortid|BusiType|GoodsNo|GoodsName|spec|medicaltype|calcunit|producer|Amount|batchNo|ValidDate|BusiDate|CustomID|Custom|old_no",
    "ctl00$ContentPlaceHolder3$ZBDate1": start, "ctl00$ContentPlaceHolder3$ZBDate2": end,
    "ctl00$ContentPlaceHolder3$WA1_ZBGridPageSize": "9999", "ctl00$ContentPlaceHolder3$WA1_ZBToPage": "1",
    "ctl00$ContentPlaceHolder3$WA2_ZBGridPageSize": "9999", "ctl00$ContentPlaceHolder3$WA2_ZBToPage": "1",
    "ctl00$ContentPlaceHolder3$ctl04": "1", "ctl00$ContentPlaceHolder3$ctl05": "0",
    "ctl00$ContentPlaceHolder3$UDQRefreshButton": "查询",
}, headers={"Content-Type": "application/x-www-form-urlencoded", "X-MicrosoftAjax": "Delta=true",
             "Referer": r4.url}, timeout=30)

# ═══ Parse AJAX pipe response ═══
resp = r5.text
m = re.search(r'updatePanel\|[^|]+\|(.+?)(?:\|\d+\|)', resp)
html_content = m.group(1) if m else resp

tables = re.findall(r'<table[^>]*>(.*?)</table>', html_content, re.I | re.S)
best_rows = []
for t in tables:
    trs = re.findall(r'<tr[^>]*>(.*?)</tr>', t, re.I | re.S)
    rows_data = []
    for tr in trs:
        tds = re.findall(r'<t[dh][^>]*>(.*?)</t[dh]>', tr, re.I | re.S)
        cells = [re.sub(r'<[^>]+>', ' ', td).strip() for td in tds]
        cells = [c for c in cells if c]
        if len(cells) >= 3:
            rows_data.append(cells)
    if len(rows_data) > len(best_rows):
        best_rows = rows_data

if len(best_rows) < 2:
    print("No data", file=sys.stderr); print("[]"); sys.exit(0)

# ═══ Build result ═══
FIELD_MAP = {
    "编号": "编号", "业务标识": "业务类型", "商品编号": "商品编码",
    "商品名称": "商品名称", "规格": "规格", "类型": "类型",
    "计量单位": "单位", "生产企业": "生产企业", "数量": "数量",
    "批号": "批号", "有效日期": "效期", "业务日期": "日期",
    "客户单位编码": "客户编码", "客户单位": "客户名称", "原数据库号": "原单号",
}
headers = best_rows[0]
result = []
for row in best_rows[1:]:
    entry = {}
    for i, cell in enumerate(row):
        key = headers[i] if i < len(headers) else f"Col{i}"
        entry[FIELD_MAP.get(key, key)] = cell
    # Filter: skip empty rows and summary rows
    vals = [v.strip() for v in entry.values()]
    if not any(v and v != '&nbsp;' for v in vals):
        continue
    if any('合计' in v or '小计' in v for v in vals):
        continue
    result.append(entry)

print(f"Records: {len(result)}", file=sys.stderr)
print(json.dumps(result, ensure_ascii=False))
