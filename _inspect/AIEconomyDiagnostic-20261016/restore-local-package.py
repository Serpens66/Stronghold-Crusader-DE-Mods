from pathlib import Path
import subprocess

# The cancelled Bugfix build removed its local output before its compile failed.
# Restore only missing, previously clean tracked outputs; never touch the installed package.
root = Path.cwd()
prefix = 'BugfixesAndQoL/BepInEx/plugins/BugfixesAndQoL_Serp'
paths = subprocess.check_output(['git', 'ls-files', '-z', '--', prefix]).decode().split('\0')
count = 0
for name in filter(None, paths):
    target = root / name
    if target.exists() and subprocess.call(['git', 'diff', '--quiet', '--', name]) != 0:
        continue
    data = subprocess.check_output(['git', 'cat-file', '--filters', 'HEAD:' + name])
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    count += 1
print(f'Restored {count} missing tracked local package files; installed Bugfix package untouched.')
