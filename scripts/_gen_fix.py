import sqlite3, json, os

conn = sqlite3.connect('e:/流向平台/PharmaFlow/src/PharmaFlow.WebApi/PharmaFlow.db')
conn.row_factory = sqlite3.Row
rows = conn.execute("SELECT Id, Name, ExportConfig FROM CollectionSources WHERE CollectionMode = 'PythonScript'").fetchall()
conn.close()

statements = []
for r in rows:
    cfg = json.loads(r['ExportConfig'])
    script = cfg.get('export', {}).get('pythonScript', '')
    filename = os.path.basename(script)
    cfg['export']['pythonScript'] = '/app/scripts/' + filename
    new_cfg = json.dumps(cfg).replace("'", "''")
    stmt = "UPDATE \"CollectionSources\" SET \"ExportConfig\" = '" + new_cfg + "' WHERE \"Id\" = '" + r['Id'] + "';"
    statements.append(stmt)
    print(f"{r['Name']}: {filename}")

with open('e:/流向平台/PharmaFlow/scripts/_fix_paths.sql', 'w', encoding='utf-8') as f:
    f.write('\n'.join(statements))

print(f'\nGenerated {len(statements)} UPDATE statements')
