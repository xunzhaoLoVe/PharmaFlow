import requests, json, sys, os, hashlib, base64
from datetime import datetime, timedelta
from ddddocr import DdddOcr

if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://gk-nn.com:8075"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "gknn_token.txt")
USER_STK = "4254"
USER_PWD = "JSsl@1230"

today = datetime.now()
start = (today - timedelta(days=7)).strftime("%Y-%m-%d")
end = (today - timedelta(days=1)).strftime("%Y-%m-%d")
date_range = f"{start} ~ {end}"
print(f"Range: {date_range}", file=sys.stderr)

# Get token - try auto-login first, fallback to file
def auto_login():
    try:
        ocr = DdddOcr(show_ad=False, beta=True)
        r = requests.post(f"{BASE}/user/genVerifyCode", data={"verify_id": "0"},
            headers={"User-Agent": "Mozilla/5.0", "fromtype": "pc", "Origin": BASE,
                     "Referer": f"{BASE}/pc/html/base/login.html",
                     "Content-Type": "application/x-www-form-urlencoded"}, timeout=10)
        args = r.json().get("args", {})
        img = args.get("graphic_code", "")
        vid = args.get("verify_id", "0")
        if not img: return ""
        code = ocr.classification(base64.b64decode(img)).strip()
        pwd_hash = hashlib.md5(USER_PWD.encode()).hexdigest()
        r2 = requests.post(f"{BASE}/user/login",
            data={"compid": "1", "stk": USER_STK, "pwd": pwd_hash,
                  "verifycode": code, "verify_id": str(vid)},
            headers={"User-Agent": "Mozilla/5.0", "fromtype": "pc", "Origin": BASE,
                     "Referer": f"{BASE}/pc/html/base/login.html",
                     "Content-Type": "application/x-www-form-urlencoded"}, timeout=10)
        resp = r2.json()
        if resp.get("code") == 0:
            token = resp.get("args", {}).get("usertoken", "")
            if token:
                with open(TOKEN_FILE, "w") as f: f.write(token)
                print(f"Auto-login OK", file=sys.stderr)
                return token
    except Exception as e:
        print(f"Auto-login error: {e}", file=sys.stderr)
    return ""

token = auto_login()
if not token and os.path.exists(TOKEN_FILE):
    with open(TOKEN_FILE) as f:
        token = f.read().strip()
    print(f"Using saved token", file=sys.stderr)

HEADERS = {
    "usertoken": token, "fromtype": "pc",
    "User-Agent": "Mozilla/5.0", "Origin": BASE, "Referer": f"{BASE}/",
    "Content-Type": "application/x-www-form-urlencoded",
}

# ═══ Query ═══
r = requests.post(f"{BASE}/report/getList",
    data={"report_id": "xiaoshou", "ownerid": "all", "custname": "",
          "date_range": date_range, "goods": "", "name": "", "selectgoods": "", "order": ""},
    headers=HEADERS, timeout=30)
resp = r.json()
if resp.get("code") != 0:
    print(f"Error: {resp.get('content')}", file=sys.stderr); print("[]"); sys.exit(0)

rows = resp.get("args", {}).get("list", [])
FIELD_MAP = {
    "MAKEDAY": "日期", "GOODS": "商品代码", "NAME": "品名", "SPEC": "规格",
    "PRODUCER": "生产厂家", "CSTCODE": "客户代码", "DNAME": "客户名称",
    "FLOWNAME": "单据类型", "BILLNO": "订单号", "OWNERNAME": "货主",
    "PACKNUM": "包装", "MSUNITNO": "单位", "BILLQTY": "销售数量",
    "LOTNO": "批号", "BTHDESC": "批号信息",
    "PRC": "含税单价", "PRICE": "无税单价",
    "SUMVALUE": "含税金额", "VALUE": "无税金额", "TAXRATE": "税率",
    "CSTTYPE": "客户类型", "SALEDEPTNAME": "销售区域",
    "ADDRESSNAME": "送货地址", "BATCHNO": "批次号", "RATIFIER": "批准文号",
}
str_rows = [{FIELD_MAP.get(k, k): str(v) if v is not None else "" for k, v in row.items()} for row in rows]
print(f"Records: {len(str_rows)}", file=sys.stderr)
if str_rows:
    print(f"KEYS: {list(str_rows[0].keys())}", file=sys.stderr)
print(json.dumps(str_rows, ensure_ascii=False))
