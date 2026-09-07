"""Prepare supplementary icons and integrate them without changing control bindings."""
from pathlib import Path
import json, re, shutil
import numpy as np
from scipy import ndimage
from PIL import Image, ImageDraw, ImageFont
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent
project=root.parents[2]
ui=project/'src/AlchemyStars.Avalonia'
names=['save','save-as','about','add','delete','move-up','move-down','restore-layout','fit-view','zoom-in','zoom-out','previous-frame','play','pause','next-frame','notification']
sheet=Image.open(root/'raw/generated.png').convert('RGBA')
boards={n:Image.new('RGB',(1000,1080),c) for n,c in [('preview','#f5f5f7'),('qa-magenta','#d946ef'),('qa-dark','#272729')]}
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
for i,name in enumerate(names):
    row,col=divmod(i,4)
    # This sheet has clear gutters; keep intentional disconnected components
    # inside each exact cell instead of proximity-linking them to one component.
    a=np.array(sheet.crop((round(col*sheet.width/4),round(row*sheet.height/4),round((col+1)*sheet.width/4),round((row+1)*sheet.height/4))))
    labels,_=ndimage.label(a[:,:,3]>160)
    counts=np.bincount(labels.ravel()); keep=np.flatnonzero(counts>=50);keep=keep[keep!=0]
    a[:,:,3]=np.where(np.isin(labels,keep),a[:,:,3],0)
    a[:,:,:3]=np.where((a[:,:,0]>170)[:,:,None],[255,255,255],[0,102,204])
    obj=Image.fromarray(a);obj=obj.crop(obj.getbbox())
    side=round(max(obj.size)*1.16)
    im=Image.new('RGBA',(side,side));im.paste(obj,((side-obj.width)//2,(side-obj.height)//2))
    for size in (512,128,64):
        dest=root/f'png-{size}';dest.mkdir(exist_ok=True)
        im.resize((size,size),Image.Resampling.LANCZOS).save(dest/f'{name}.png')
    thumb=im.resize((168,168),Image.Resampling.LANCZOS)
    for title,board in boards.items():
        board.paste(thumb,(col*250+41,row*250+65),thumb)
        ImageDraw.Draw(board).text((col*250+125,row*250+247),name,font=font,anchor='mm',fill='white' if title=='qa-dark' else '#1d1d1f')
for title,board in boards.items():
    ImageDraw.Draw(board).text((28,20),'ALCHEMY STARS / APPLE BLUE CONTROLS',font=font,fill='white' if title=='qa-dark' else '#1d1d1f')
    board.save(root/f'{title}.png')
spec=json.loads((root.parent/'apple-blue/style-spec.json').read_text(encoding='utf-8'))
spec.update(name='alchemy-apple-blue-controls',icons=[{'index':i+1,'name':n} for i,n in enumerate(names)])
(root/'style-spec.json').write_text(json.dumps(spec,ensure_ascii=False,indent=2),encoding='utf-8')
for n in names:shutil.copy2(root/'png-128'/f'{n}.png',ui/'Assets/AppleBlue'/f'{n}.png')

mapping={
'M10,2 A8,8 0 1,0 10,18 A8,8 0 1,0 10,2 M10,9':'about',
'M4,3 L17,3':'save','M4,3 L16,3':'save-as','M10,3 L10,17':'add',
'M4,6 L16,6':'delete','M4,12 L10,6':'move-up','M4,8 L10,14':'move-down',
'M4,6 A7,7':'restore-layout','M3,8 L3,3':'fit-view','M3,10 L17,10':'zoom-out',
'M4,3 L4,17':'previous-frame','M5,3 L17,10':'play','M6,3 L6,17':'pause','M16,3 L16,17':'next-frame',
'M10,2 A8,8 0 1,0 10,18 A8,8 0 1,0 10,2 M10,6':'notification'}
ns='{https://github.com/avaloniaui}'
for filename in ('MainWindow.axaml','CastPreviewView.axaml'):
    path=ui/filename;s=path.read_text(encoding='utf-8');before=ET.fromstring(s)
    def replace(m):
        e=ET.fromstring(m[0]);data=e.get('Data','')
        name=next((v for k,v in mapping.items() if data.startswith(k)),None)
        if name is None:raise ValueError('Unmapped glyph: '+data)
        if filename=='CastPreviewView.axaml' and name=='add':name='zoom-in'
        classes=e.get('Classes','')
        size='15' if 'transport-icon' in classes else ('17' if 'preview-icon' in classes else '20')
        attrs={k:v for k,v in e.attrib.items() if k not in ('Data','Classes','Stretch','IsHitTestVisible')}
        attrs.setdefault('Width',size);attrs.setdefault('Height',size)
        return f'<Image Classes="product-icon" Source="avares://AlchemyStars.Avalonia/Assets/AppleBlue/{name}.png" RenderOptions.BitmapInterpolationMode="HighQuality" Stretch="Uniform" IsHitTestVisible="False" '+ ' '.join(f'{k}="{v}"' for k,v in attrs.items())+' />'
    s=re.sub(r'<Path\b[^>]*?/>',replace,s)
    after=ET.fromstring(s)
    for tag in ('Button','ToggleButton','Grid','TextBox','ComboBox','GridSplitter'):
        assert [e.attrib for e in before.iter(ns+tag)]==[e.attrib for e in after.iter(ns+tag)],(filename,tag)
    path.write_text(s,encoding='utf-8')
print('16 icons, 48 PNGs, original control bindings and layouts preserved.')
