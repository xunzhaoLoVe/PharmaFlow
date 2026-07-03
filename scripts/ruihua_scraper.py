import requests, json, sys
from datetime import datetime, timedelta

# Force UTF-8 stdout
if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

today = datetime.now()
start = (today - timedelta(days=7)).strftime("%Y-%m-%d")
end = (today - timedelta(days=1)).strftime("%Y-%m-%d")

BASE = "http://221.2.205.234:8890"
HEADERS = {
    "Origin": "http://221.2.205.234:8895",
    "Referer": "http://221.2.205.234:8895/",
    "Content-Type": "application/json;charset=UTF-8",
}

# Collect all pages
all_records = []
page = 1
while True:
    body = {
        "userid": "00014024",
        "start_rq": start,
        "end_rq": end,
        "fkfs": "", "dwmch": "", "psxl": "", "spmch": "", "spxl": "", "spzl": "",
        "page": page,
        "size": 1000,
    }
    r = requests.post(f"{BASE}/api/query/btb_xsjlcx", json=body, headers=HEADERS, timeout=30)
    resp = r.json()
    records = resp.get("data", {}).get("list", [])
    if not records:
        break
    # 字段名映射为中文 + 值转字符串
    FIELD_MAP = {
        "RQ": "日期", "DWMCH": "客户名称", "DWMCH_JH": "供货单位",
        "SPMCH": "商品名称", "SPBH": "商品编码", "SHPGG": "规格",
        "SHENGCCJ": "生产企业", "DW": "单位", "SHL": "数量",
        "HSHJ": "含税价", "HSJE": "含税金额", "HSHJJ": "含税基价",
        "PH": "批号", "SXRQ": "效期", "SPZL": "商品种类",
        "BAOZHIQI": "报至期", "HZBS": "货主标识", "XSCS": "销售次数",
    }
    str_records = []
    for r in records:
        mapped = {FIELD_MAP.get(k, k): str(v) for k, v in r.items()}
        str_records.append(mapped)
    all_records.extend(str_records)
    page += 1
    # Safety limit
    if page > 100:
        break

print(f"Pages: {page-1}, Records: {len(all_records)}", file=sys.stderr)
print(json.dumps(all_records, ensure_ascii=False))
