"""Reproduce the small packaged button assets from the installed Vanilla atlas.

Developer-only: UnityPy and Pillow; no asset generation or Python dependency at runtime.
"""
from pathlib import Path
import re
import sys
import UnityPy
from PIL import Image, ImageDraw, ImageFilter

game = Path(sys.argv[1])
workspace = Path(__file__).resolve().parents[3]
source = workspace / '_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/resources/xaml/Assets/GUI/Sprites/UI-MasterAtlas.xaml'
definition = source.read_text(encoding='utf-8')
atlas = None
for obj in UnityPy.load(str(game / 'Stronghold Crusader Definitive Edition_Data/globalgamemanagers.assets')).objects:
    if obj.type.name == 'Texture2D' and obj.peek_name() == 'UI-MasterAtlas':
        atlas = obj.read().image.convert('RGBA')
        break
if atlas is None:
    raise RuntimeError('Vanilla UI-MasterAtlas missing')

def sprite(name, expected):
    x,y,w,h = map(int, re.search('x:Key="'+name+'"[^>]*SourceRect="([^"]+)', definition)[1].split(','))
    if (w,h) != expected:
        raise RuntimeError('Reference sprite dimensions changed: '+name)
    return atlas.crop((x,y,x+w,y+h))

def background(highlight):
    # These different Vanilla icons share the same parchment. Agreement of three
    # sprites recovers actual background pixels without enlarging a bright edge.
    indices = (1,3,5,7,9,11,13,17,19)
    frames = [sprite('UI-Buildings A%03d' % (i + int(highlight)), (68,68)) for i in indices]
    result = frames[0].copy()
    known = {}
    missing = []
    for y in range(8,60):
        for x in range(8,60):
            samples = [frame.getpixel((x,y)) for frame in frames]
            minimum_red = 140 if 12 <= x < 56 and 14 <= y < 54 else 80
            # Atlas compression gives the same parchment slightly different RGB
            # values beside different icons. Match a tight cluster, keep an original pixel.
            candidates = [(sum(max(abs(a-b) for a,b in zip(pixel[:3],other[:3])) <= 14 for other in samples),pixel)
                          for pixel in samples if pixel[0] >= pixel[1] >= pixel[2] and pixel[0] > minimum_red]
            candidates = [(count,pixel) for count,pixel in candidates if count >= 3]
            if candidates:
                pixel = max(candidates)[1]
                known[x,y] = pixel
                result.putpixel((x,y),pixel)
            else:
                missing.append((x,y))
    if len(known) < 2000:
        raise RuntimeError('Shared Vanilla button background contract changed: recovered %d of 2704 pixels' % len(known))
    # The few places covered by every icon inherit nearby verified parchment grain.
    for x,y in missing:
        neighbour = min(known, key=lambda point: (point[0]-x)**2 + (point[1]-y)**2)
        result.putpixel((x,y),known[neighbour])
    print('background', 'hover' if highlight else 'normal', 'verified pixels', len(known), 'reconstructed', len(missing))
    return result

crown = sprite('UI-Buttons A056',(60,70)).crop((12,19,48,39))
mask = Image.new('L', crown.size)
draw = ImageDraw.Draw(mask)
# Crown silhouette, excluding the old wooden shield; RGB source pixels remain unchanged.
for x,y,w,h in [(0,0,6,3),(9,0,7,3),(19,0,8,3),(30,0,5,4),(1,3,5,1),(9,3,8,1),(19,3,8,1),(1,4,34,6),(2,10,32,3),(0,13,36,6)]:
    draw.rectangle((x,y,x+w-1,y+h-1), fill=255)
crown.putalpha(mask)
crown = crown.resize((51,28), Image.Resampling.LANCZOS)
output = Path(__file__).resolve().parents[1] / 'Override/Assets/GUI/Sprites'
output.mkdir(parents=True, exist_ok=True)
backgrounds = [background(False), background(True)]
for state,key,offset in [('Normal','UI-Buildings A001',0),('Hover','UI-Buildings A002',0),('Pressed','UI-Buildings A002',1)]:
    button = backgrounds[int(state != 'Normal')].copy()
    # Shadow and outline are separate layers: the frame and parchment are never tinted.
    shadow = Image.new('RGBA',crown.size,(41,27,12,0))
    shadow.putalpha(crown.getchannel('A').filter(ImageFilter.GaussianBlur(1.2)))
    button.alpha_composite(shadow,(9+offset,22+offset))
    outline = Image.new('RGBA',crown.size,(41,27,12,0))
    outline.putalpha(crown.getchannel('A').filter(ImageFilter.MaxFilter(3)))
    button.alpha_composite(outline,(8+offset,20+offset))
    button.alpha_composite(crown,(8+offset,20+offset))
    path = output / ('LordOverviewTest_Crown'+state+'.png')
    button.save(path, optimize=True)
    print(path, path.stat().st_size)
