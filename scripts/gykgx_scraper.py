import json, sys, os, hashlib, requests
from datetime import datetime, timedelta

if sys.stdout.encoding != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = "http://183.230.195.143:8787"
ACCOUNT = "0250"
PASSWORD = "jssl123456@"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
TOKEN_FILE = os.path.join(SCRIPT_DIR, "gykgx_token.txt")

today = datetime.now()
first_this_month = today.replace(day=1)
start_month = first_this_month.month - 3
start_year = first_this_month.year
if start_month <= 0:
    start_month += 12
    start_year -= 1
start_date = first_this_month.replace(year=start_year, month=start_month).strftime("%Y-%m-%d")
end_date = (first_this_month - timedelta(days=1)).strftime("%Y-%m-%d")

def auto_login():
    try:
        import ddddocr
        ocr = ddddocr.DdddOcr(show_ad=False)
        s = requests.Session()
        s.get(BASE + "/Login/Index", headers={"User-Agent": "Mozilla/5.0"}, timeout=10)
        code = ""
        for attempt in range(3):
            r = s.get(BASE + "/Login/VerifyCode",
                headers={"Referer": BASE + "/Login/Index", "User-Agent": "Mozilla/5.0"}, timeout=10)
            if r.status_code == 200 and len(r.content) > 100:
                code = ocr.classification(r.content).strip()
                if code and code.isdigit() and len(code) >= 3:
                    break
        if not code or len(code) < 3:
            return None
        pwd_md5 = hashlib.md5(PASSWORD.encode()).hexdigest()
        r = s.post(BASE + "/Login/CheckLogin",
            data={"Account": ACCOUNT, "Password": pwd_md5, "Vcode": code},
            headers={"Referer": BASE + "/Login/Index", "User-Agent": "Mozilla/5.0"}, timeout=10)
        token = s.cookies.get("LoginUserKey", "")
        if token and len(token) > 50:
            with open(TOKEN_FILE, "w") as f:
                f.write(token)
            return s
        return None
    except Exception as e:
        print("Login error: {}".format(e), file=sys.stderr)
        return None

session = auto_login()
if session is None:
    print("[]", file=sys.stderr)
    sys.exit(0)

params_json = json.dumps([
    {"paramName": "makedate", "paramValue": start_date, "Operation": "GreaterThan", "Logic": "AND"},
    {"paramName": "makedate", "paramValue": end_date, "Operation": "LessThan"}
], ensure_ascii=False)

params = {
    "ParameterJson": params_json,
    "_search": "false", "rows": "500", "page": "1",
    "sidx": "makedate", "sord": "asc"
}

r = session.get(BASE + "/ScmModule/PubStmCardGrp/GridPageListJson2",
    params=params,
    headers={"Referer": BASE + "/ScmModule/PubStmCardGrp/Index2",
             "X-Requested-With": "XMLHttpRequest", "Accept": "application/json"}, timeout=30)

try:
    data = r.json()
    rows = data.get("rows", [])
    str_rows = [{k: str(v) if v is not None else "" for k, v in row.items()} for row in rows]
    print(json.dumps(str_rows, ensure_ascii=False))
except Exception as e:
    print("[]", file=sys.stderr)
