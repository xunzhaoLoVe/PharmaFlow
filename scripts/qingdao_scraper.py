import requests, re, json, sys, base64, os
from datetime import datetime, timedelta
from urllib.parse import urlencode

# Force UTF-8 stdout so C# can read Chinese characters correctly
if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if sys.stderr.encoding != 'utf-8':
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

today = datetime.now()
start_date = today - timedelta(days=7)
end_date = today - timedelta(days=1)  # 7天前 → 昨天
# GMDatePicker uses format without leading zeros: 2026/6/1 not 2026/06/01
start = f"{start_date.year}/{start_date.month}/{start_date.day}"
end = f"{end_date.year}/{end_date.month}/{end_date.day}"

s = requests.Session()

# ====== Step 1: Login ======
print("Step 1: Login...", file=sys.stderr)
r = s.get("http://222.173.24.186:9090/login.aspx?x=54", timeout=30)
r.encoding = 'gb2312'
h = r.text
vs = re.search(r'id="__VIEWSTATE" value="([^"]+)"', h, re.I).group(1)
vg = re.search(r'id="__VIEWSTATEGENERATOR" value="([^"]+)"', h, re.I).group(1)
ev = re.search(r'id="__EVENTVALIDATION" value="([^"]+)"', h, re.I).group(1)

# Get captcha and OCR
r2 = s.get("http://222.173.24.186:9090/login_Validata.aspx", timeout=30)
img_b64 = base64.b64encode(r2.content).decode("utf-8")
try:
    api_resp = requests.post("https://api.leepow.com/verifycode", json={"image": img_b64}, timeout=15)
    code = api_resp.json().get("data", "0000")
except Exception as e:
    print(f"OCR failed: {e}, trying local fallback", file=sys.stderr)
    code = "0000"

print(f"Captcha code: {code}", file=sys.stderr)

# POST login
r3 = s.post("http://222.173.24.186:9090/login.aspx?x=54", data={
    "__VIEWSTATE": vs,
    "__VIEWSTATEGENERATOR": vg,
    "__EVENTVALIDATION": ev,
    "tbaccount": "qdjssl",
    "tbpassword": "Qd88888888/",
    "tbvalid": code,
    "cb_save": "on",
    "imb_login.x": "55",
    "imb_login.y": "12"
}, allow_redirects=True, timeout=30)

print(f"Login done, cookies={dict(s.cookies)}", file=sys.stderr)

# ====== Step 2: GET query page ======
print("Step 2: GET query page...", file=sys.stderr)
r4 = s.get("http://222.173.24.186:9090/web_flow/flow_sale_query.aspx", timeout=30)
r4.encoding = 'gb2312'
h4 = r4.text
print(f"Query page: {len(h4)} bytes", file=sys.stderr)

# Check if login was successful (shouldn't redirect to login)
if "login.aspx" in r4.url.lower() or "tbaccount" in h4.lower():
    print("ERROR: Login failed - redirected to login page", file=sys.stderr)
    print("[]")
    sys.exit(0)

# ====== Step 3: Build form data in ASP.NET expected order ======
# EVENTTARGET/EVENTARGUMENT MUST be first for ASP.NET postback
form_data = {}
form_data["__EVENTTARGET"] = ""
form_data["__EVENTARGUMENT"] = ""

# Extract all hidden inputs with name and value
for m in re.finditer(r'<input[^>]*type=["\']hidden["\'][^>]*name=["\']([^"\']+)["\'][^>]*value=["\']([^"\']*)["\'][^>]*/?>', h4, re.I):
    name = m.group(1)
    if name not in form_data:
        form_data[name] = m.group(2)
# Also match without type (some hidden inputs might not have type="hidden")
for m in re.finditer(r'<input[^>]*name=["\']([^"\']+)["\'][^>]*value=["\']([^"\']*)["\'][^>]*/?>', h4, re.I):
    name = m.group(1)
    if name not in form_data:
        form_data[name] = m.group(2)

# Ensure critical fields from query page's ViewState
vs4 = re.search(r'id="__VIEWSTATE"[^>]*value="([^"]+)"', h4, re.I)
if vs4:
    form_data["__VIEWSTATE"] = vs4.group(1)
vg4 = re.search(r'id="__VIEWSTATEGENERATOR"[^>]*value="([^"]+)"', h4, re.I)
if vg4:
    form_data["__VIEWSTATEGENERATOR"] = vg4.group(1)
ev4 = re.search(r'id="__EVENTVALIDATION"[^>]*value="([^"]+)"', h4, re.I)
if ev4:
    form_data["__EVENTVALIDATION"] = ev4.group(1)

# Set query parameters
form_data["GMDatePicker1$ctl00"] = start
form_data["GMDatePicker2$ctl00"] = end
form_data["tbbh"] = ""
# btchaxun must be the GB2312-encoded value for "查询"
# In Python requests, we use the raw Chinese string
form_data["btchaxun"] = "查询"

# Add screen dimensions (some ASP.NET apps require these)
if "tbwidth" not in form_data:
    form_data["tbwidth"] = "1920"
if "tbheight" not in form_data:
    form_data["tbheight"] = "1040"

# Ensure GridView fields exist
gv_defaults = {
    "MyGridView1$tb_page": "1",
    "MyGridView1$hd_flag": "",
    "MyGridView1$hd_sql": "",
    "MyGridView1$hd_KeyName1": "",
    "MyGridView1$hd_KeyName2": "",
    "MyGridView1$hd_width": "1670",
    "MyGridView1$hd_height": "624",
    "MyGridView1$hd_relativevalue": "3600",
    "MyGridView1$hd_showselect": "false",
    "MyGridView1$hd_showfooter": "false",
    "MyGridView1$hd_sumcol1": "0",
    "MyGridView1$hd_sumcol2": "0",
}
for k, v in gv_defaults.items():
    if k not in form_data:
        form_data[k] = v

print(f"Form fields: {len(form_data)}", file=sys.stderr)

# ====== Step 4: POST query ======
print("Step 4: POST query...", file=sys.stderr)
headers = {
    "Referer": "http://222.173.24.186:9090/web_flow/flow_sale_query.aspx",
    "Content-Type": "application/x-www-form-urlencoded",
}

# Encode form data in GB2312 (ASP.NET expects this, not UTF-8)
encoded_body = urlencode(form_data, encoding='gb2312')
headers["Content-Type"] = "application/x-www-form-urlencoded"
r5 = s.post(
    "http://222.173.24.186:9090/web_flow/flow_sale_query.aspx",
    data=encoded_body,
    headers=headers,
    timeout=60
)
# Force GB2312 encoding (server declares charset=gb2312 but requests misdetects it)
r5.encoding = 'gb2312'
h5 = r5.text
print(f"Response: {len(h5)} bytes, status={r5.status_code}, url={r5.url}", file=sys.stderr)

# Check for error page
if "error.aspx" in h5[:2000] or "error.aspx" in r5.url:
    print("ERROR: ASP.NET error page returned", file=sys.stderr)
    # Save for debugging
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "error_debug.html"), "w", encoding="utf-8") as f:
        f.write(h5)
    print(f"Saved error page for debugging", file=sys.stderr)

# ====== Step 5: Parse ======
all_trs = re.findall(r'<tr[^>]*>(.*?)</tr>', h5, re.I | re.S)
rows = []
for tr in all_trs:
    tds = re.findall(r'<t[dh][^>]*>(.*?)</t[dh]>', tr, re.I | re.S)
    cells = [re.sub(r'<[^>]+>', ' ', td).strip() for td in tds]
    cells = [c for c in cells if c]
    if len(cells) >= 6:
        rows.append(cells)

print(f"Data rows: {len(rows)}", file=sys.stderr)

if len(rows) > 1:
    headers_row = rows[0]
    result = []
    for row in rows[1:]:
        entry = {}
        for j, cell in enumerate(row):
            key = headers_row[j] if j < len(headers_row) else f"Col{j}"
            entry[key] = cell
        result.append(entry)
    print(json.dumps(result, ensure_ascii=False))
else:
    print("[]")
