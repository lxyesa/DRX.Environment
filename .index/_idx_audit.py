import json, os, collections
p = r'd:/Code/.index/CODE_INDEX.json'
with open(p, 'r', encoding='utf-8') as f:
    d = json.load(f)
e = d.get('entries', [])
n = len(e)
norm = [('/'.join(str((i.get('file') or '')).replace('\\\\', '/').split('/'))).lower() for i in e]
c = collections.Counter(norm)
dup = sum(v-1 for v in c.values() if v > 1)
dup_keys = [k for k, v in c.items() if v > 1]
md_missing = sum(1 for i in e if (i.get('md_file') is None) or (isinstance(i.get('md_file'), str) and i.get('md_file').strip() == ''))
missing = [i.get('file', '') for i in e if i.get('file') and not os.path.exists(os.path.join('d:/Code', i.get('file')))]
print(f'TOTAL={n}')
print(f'MD_MISSING={md_missing}')
print(f'MD_MISSING_RATE={(md_missing*100/n) if n else 0:.2f}')
print(f'DUP_ENTRIES={dup}')
print(f'DUP_RATE={(dup*100/n) if n else 0:.2f}')
print(f'MISSING_CODE={len(missing)}')
print(f'MISSING_CODE_RATE={(len(missing)*100/n) if n else 0:.2f}')
print('DUP_KEYS:')
for k in dup_keys[:50]:
    print(k)
print('MISSING_CODE_FILES:')
for k in missing[:50]:
    print(k)
