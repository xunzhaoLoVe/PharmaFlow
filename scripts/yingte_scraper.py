import requests, json, sys, os, re, io
from datetime import datetime, timedelta
from ddddocr import DdddOcr
import cv2, numpy as np

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://220.191.160.242:8888"
USERNAME = "jssl123"
PASSWORD = "zj~123456"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "yingte_token.txt")
HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    "Origin": BASE, "Referer": f"{BASE}/", "X-Requested-With": "XMLHttpRequest",
}

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

# ═══ Attempt 1: Auto-login with OCR (multi-method) ═══
def solve_captcha(img_bytes):
    # Try multiple OCR methods, return math result or None
    ocr = DdddOcr(show_ad=False, beta=True)
    nparr = np.frombuffer(img_bytes, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_GRAYSCALE)

    # Method 1: Raw
    texts = [ocr.classification(img_bytes)]
    # Method 2: Dark pixels
    _, dark = cv2.threshold(img, 100, 255, cv2.THRESH_BINARY_INV)
    _, buf = cv2.imencode('.jpg', dark)
    texts.append(ocr.classification(io.BytesIO(buf).getvalue()))
    # Method 3: Adaptive
    adaptive = cv2.adaptiveThreshold(img, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY, 11, 2)
    _, buf = cv2.imencode('.jpg', adaptive)
    texts.append(ocr.classification(io.BytesIO(buf).getvalue()))

    for text in texts:
        cleaned = ''.join(c for c in text if c.isdigit() or c in '+-*/=xX?')
        m = re.search(r'(\d)\s*([+\-*/xX])\s*(\d)', cleaned)
        if m:
            a, op, b = int(m.group(1)), m.group(2), int(m.group(3))
            if op in ('x', 'X'): op = '*'
            return str(int(eval(f'{a}{op}{b}')))
    return None

try:
    for attempt in range(3):
        r = s.get(f"{BASE}/captcha/captchaImage?type=math&s={attempt*0.3}", headers=HEADERS, timeout=15)
        code = solve_captcha(r.content)
        if not code:
            continue
        r2 = s.post(f"{BASE}/login", data={
            "username": USERNAME, "password": PASSWORD,
            "validateCode": code, "rememberMe": "true"
        }, headers=HEADERS, timeout=15)
        if r2.json().get("code") == 0:
            logged_in = True
            print(f"Auto-login OK (attempt {attempt+1})", file=sys.stderr)
            break
        print(f"Attempt {attempt+1}: code={code}, {r2.json().get('msg')}", file=sys.stderr)
except Exception as e:
    print(f"Auto-login error: {e}", file=sys.stderr)

# ═══ Attempt 2: Use JSESSIONID from token file ═══
if not logged_in and os.path.exists(TOKEN_FILE):
    with open(TOKEN_FILE) as f:
        jsessionid = f.read().strip()
    if jsessionid:
        s = requests.Session()
        s.cookies.set("JSESSIONID", jsessionid, domain="220.191.160.242", path="/")
        print(f"Using token: {jsessionid[:16]}...", file=sys.stderr)
        logged_in = True

if not logged_in:
    print("No valid auth method", file=sys.stderr)
    print("[]")
    sys.exit(0)

# ═══ Query ═══
r3 = s.post(f"{BASE}/system/itMedicinegotoInoutroom/list", data={
    "invtypeid": "", "entryId": "", "goodsid": "", "buyerName": "", "supplier": "",
    "timeType": "0",
    "params[keepStartTime]": start, "params[creStartTime]": "",
    "params[keepEndTime]": end, "params[creEndTime]": "",
    "area": "", "buyerTypeId": "", "storagename": "",
    "pageSize": "9999", "pageNum": "1",
    "orderByColumn": "", "isAsc": "", "asc": ""
}, headers=HEADERS, timeout=30)
resp = r3.json()

if resp.get("code") == 1:
    print(f"Session expired: {resp.get('msg')}", file=sys.stderr)
    print("[]")
    sys.exit(0)

rows = resp.get("rows", [])
if isinstance(rows, dict):
    rows = rows.get("list", [])

FIELD_MAP = {
    "goodsname": "商品名称", "goodsid": "商品编码", "standard": "规格",
    "producingArea": "产地", "baseUnit": "单位", "batch": "批号",
    "buyerName": "客户名称", "buyerCode": "客户编码", "buyerBarnchname": "客户药房",
    "supplier": "供应商", "supplierCode": "供应商编码",
    "credate": "创建日期", "keepdate": "入库日期", "validDate": "效期",
    "quantity": "数量", "price": "单价", "amount": "金额",
    "entryName": "入库类型", "inoutCode": "出入库单号", "inoutItemCode": "明细号",
    "storagename": "仓库", "area": "区域", "city": "城市",
    "branchName": "分公司", "userName": "操作员",
    "memo": "备注", "piciMemo": "批次备注",
}

str_rows = []
for r in rows:
    mapped = {}
    for k, v in r.items():
        if k in FIELD_MAP:
            mapped[FIELD_MAP[k]] = str(v)
    str_rows.append(mapped)
print(f"Records: {len(str_rows)}", file=sys.stderr)
if str_rows:
    print(f"KEYS: {list(str_rows[0].keys())}", file=sys.stderr)
print(json.dumps(str_rows, ensure_ascii=False))
