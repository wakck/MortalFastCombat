"""Build the 1.1.2 add-on ZIP from an explicit public-file allowlist."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[1]
name = 'MortalFastCombat-1.1.2-Windows-x64-LOMAssistant.zip'
expected = '62C5F10371A2D6277D2462D8505F188F83CEC11797D3DCDB0A67B5F02C3B6016'
digest = lambda data: hashlib.sha256(data).hexdigest().upper()
assert digest((root/'dist/MortalFastCombat.dll').read_bytes()) == expected, 'Must package the tested plugin'
paths = ['README.md', 'Install.ps1', 'Update.ps1', 'Uninstall.ps1', 'Rollback.ps1',
         'THIRD_PARTY.md', 'dist/MortalFastCombat.dll', 'dist/MortalFastCombat.dll.sha256',
         'tools/Install-Common.ps1', 'tools/compatibility.json',
         'docs/LOMASSISTANT.zh-CN.md', 'docs/VALIDATION-1.1.2.zh-CN.md']
files = {}
for path in paths:
    data = (root/path).read_bytes()
    if path.endswith('.ps1'):
        data = data.decode('utf-8-sig').replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8-sig')
    files['MortalFastCombat/'+path] = data
assert not any(any(part in ('data','backup','validation','reports','tests','sandbox','cache') for part in Path(p).parts) for p in files)
assert not any(p.endswith(('.dat','.log','.cfg','.csv','.bak','.exe')) for p in files)
manifest = {p: dict(bytes=len(data), sha256=digest(data)) for p,data in sorted(files.items())}
files['MortalFastCombat/package-manifest.json'] = json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf-8')
release = root/'release'
release.mkdir(exist_ok=True)
archive = release/name
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for path,data in sorted(files.items()):
        info = zipfile.ZipInfo(path, date_time=(2026,11,1,0,0,0))
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o100644 << 16
        z.writestr(info,data)
with zipfile.ZipFile(archive) as z:
    assert set(z.namelist()) == set(files)
    for path,data in files.items():
        assert z.read(path) == data
checksum = digest(archive.read_bytes())
(release/(name+'.sha256')).write_text(checksum+'  '+name+'\n',encoding='ascii',newline='\n')
print(json.dumps(dict(name=name, files=len(files), bytes=archive.stat().st_size, sha256=checksum),indent=2))
