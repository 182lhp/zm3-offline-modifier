"""Reject private data and binary build output from the public source tree."""
from pathlib import Path
import hashlib, json, re, sys

ROOT = Path(__file__).resolve().parents[1]
SKIP = {'bin', 'obj', '.git', '__pycache__', '.vs', '.idea', '.venv', 'venv'}
PRIVATE_DIRS = {'saves', 'rundata', 'logs', 'backup', 'SharedObjects', '.gm-backups'}
PRIVATE_NAMES = {'vip.json', 'local_gifts.json', 'local_gifts.transaction.json',
                 'appearance.json', 'navigation.json', 'catalog-view.json', '设置.ini', 'settings.ini'}
BINARY = {'.exe', '.dll', '.pyd', '.pdb', '.swf', '.swc', '.zip', '.7z', '.rar', '.sol', '.delta', '.dmp', '.marshal', '.pyz', '.lnk'}
TEXT = {'.cs', '.xaml', '.py', '.json', '.md', '.ps1', '.txt', '.as', '.c', '.h', '.csproj', '.config'}
# Construct patterns from pieces so the checker does not report its own source.
SECRETS = [r'gh[pousr]' + r'_[A-Za-z0-9]{20,}', r'github_' + r'pat_[A-Za-z0-9_]{30,}',
           r'-----BEGIN ' + r'[A-Z ]*PRIVATE KEY-----']

def check_json(value, path, errors):
    if isinstance(value, dict):
        for key, item in value.items():
            if key in {'_localSaveId', 'saveId', 'claims', 'receipts'} and item:
                errors.append(str(path) + ': personal-state JSON field ' + key)
            check_json(item, path, errors)
    elif isinstance(value, list):
        for item in value:
            check_json(item, path, errors)

def audit(root=ROOT):
    errors, files = [], []
    for path in sorted(root.rglob('*')):
        rel = path.relative_to(root)
        if set(rel.parts) & SKIP or not path.is_file():
            continue
        name = path.name.lower()
        if path.is_symlink() or getattr(path, 'is_junction', lambda: False)():
            errors.append(rel.as_posix() + ': links are forbidden')
        if set(rel.parts) & PRIVATE_DIRS or name in PRIVATE_NAMES or re.fullmatch(r'slot\d+\.json', name):
            errors.append(rel.as_posix() + ': private file or directory')
        if name.startswith('.env') and name != '.env.example':
            errors.append(rel.as_posix() + ': local environment file')
        if path.suffix.lower() in BINARY or name.endswith('.log') or name.endswith('.lock'):
            errors.append(rel.as_posix() + ': binary, diagnostic or runtime output')
        data = path.read_bytes()
        if path.suffix.lower() in TEXT or name == '.gitignore':
            text = data.decode('utf-8-sig')
            for pattern in SECRETS:
                if re.search(pattern, text):
                    errors.append(rel.as_posix() + ': credential pattern')
            if re.search(r'(?i)C:[/\\]+Users[/\\]+lee[/\\]+', text):
                errors.append(rel.as_posix() + ': personal absolute path')
            if path.suffix.lower() == '.json':
                check_json(json.loads(text), rel, errors)
        files.append(dict(path=rel.as_posix(), size=len(data), sha256=hashlib.sha256(data).hexdigest()))
    if errors:
        raise ValueError('\n'.join(errors))
    return files

if __name__ == '__main__':
    try:
        rows = audit()
        print(json.dumps(dict(status='PASS', files=len(rows), personalDataFiles=0)))
    except (ValueError, UnicodeError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1)
