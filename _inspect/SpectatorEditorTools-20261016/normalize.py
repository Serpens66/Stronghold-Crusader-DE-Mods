from pathlib import Path
root=Path(__file__).resolve().parents[2]
mod=root/'Testmods/SpectatorEditorBuildTest'
paths=list((mod/'src').glob('*.cs'))+list((mod/'tests').glob('*'))+list((mod/'Locales').glob('*.txt'))+[mod/'SpectatorEditorBuildTest.csproj',mod/'verify.ps1',mod/'build.bat',mod/'info.json',mod/'UpdateToNewDLL.md',mod/'Patches/Assets/GUI/XAML/IngameUIScreens.xaml']
paths += list(Path(__file__).parent.glob('*.py'))
for p in paths:
    if not p.is_file():continue
    value=p.read_text(encoding='utf-8-sig').replace('\r\n','\n').replace('\r','\n')
    expected=value.replace('\n','\r\n').encode('utf-8')
    p.write_bytes(expected)
    assert p.read_bytes()==expected
    assert b'\n' not in expected.replace(b'\r\n',b'')
print(f'CRLF verified: {len(paths)} files.')
