from pathlib import Path
text = Path('Library/Applications/Servers/KaxSocket/Handlers/KaxHttp.cs').read_text(encoding='utf-8')
needle = '/api/user/verify/asset/{assetId}'
idx = text.index(needle)
print(idx)
print(repr(text[idx-40:idx+200]))
