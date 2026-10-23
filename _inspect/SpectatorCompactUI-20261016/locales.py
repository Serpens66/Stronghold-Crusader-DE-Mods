from pathlib import Path
root=Path(__file__).resolve().parents[2]
locales=root/'Testmods/SpectatorEditorBuildTest/Locales'
for p in locales.glob('*.txt'):
    values={}
    for line in p.read_text(encoding='utf-8-sig').splitlines():
        if '=' in line:
            k,v=line.split('=',1)
            if k.startswith(('Tools.Plus','Tools.Minus')) or k in ('Tools.Previous','Tools.PreviousHelp','Tools.Next','Tools.NextHelp','Tools.PlacementTitle'):
                continue
            values[k]=v
    german=p.stem=='de-DE'
    values['Tools.Amount']='Menge' if german else 'Amount'
    values['Tools.PlacementTab']='Platzieren' if german else 'Placement'
    values['Tools.PlacementTabHelp']=('Platzierungswerkzeug anzeigen. Der Platziermodus wird mit dem eigenen Schalter aktiviert.' if german else 'Show the placement tool. Activate placement with its separate switch.')
    values['Tools.AmountHelp']=('Nur Ziffern 0–9, höchstens 2147483647. Leere Eingabe wird beim Verlassen zu 0. Goldbestand bzw. Lagerbestand und Lieferreserve müssen genug Platz für die Gutschrift lassen.' if german else 'Digits 0–9 only, at most 2147483647. Empty input becomes 0 on focus loss. Existing gold or stored plus incoming goods must leave enough room for the credit.')
    values['Tools.CreditHelp']=values['Tools.CreditHelp']+' '+values['Tools.ReserveHelp']
    values['Tools.PlacementHelp']=values['Tools.PlacementHelp']+' '+values['Tools.EditorNotice']
    p.write_text(''.join(f'{k}={v}\n' for k,v in sorted(values.items())),encoding='utf-8',newline='\r\n')
