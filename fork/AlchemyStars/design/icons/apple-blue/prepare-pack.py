"""Normalize generated cutouts and build a visual QA contact sheet."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

root = Path(__file__).resolve().parent
spec = json.loads((root / 'style-spec.json').read_text(encoding='utf-8'))
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 18)
small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 14)
boards = {name: Image.new('RGB', (1120, 1240), color) for name, color in [('preview', '#f5f5f7'), ('qa-magenta', '#d946ef'), ('qa-dark', '#272729')]}
for item in spec['icons']:
    src = root / 'png-512' / f"{item['index']:02}.png"
    named = src.with_name(item['name'] + '.png')
    im = Image.open(src if src.exists() else named).convert('RGBA')
    if item['index'] in (4, 6, 9):
        # These symbols have intentionally detached functional parts. The generic
        # slicer's proximity rule removed them; retain all substantial components
        # inside their original grid cell (the sheet has clear empty gutters).
        sheet = Image.open(root / 'raw/generated.png').convert('RGBA')
        cw, ch = sheet.width / 4, sheet.height / 4
        row, col = divmod(item['index'] - 1, 4)
        cell = np.array(sheet.crop((round(col*cw), round(row*ch), round((col+1)*cw), round((row+1)*ch))))
        labels, count = ndimage.label(cell[:, :, 3] > 160)
        sizes = np.bincount(labels.ravel())
        keep = np.flatnonzero(sizes >= 35)
        keep = keep[keep != 0]
        cell[:, :, 3] = np.where(np.isin(labels, keep), cell[:, :, 3], 0)
        obj = Image.fromarray(cell)
        obj = obj.crop(obj.getbbox())
        side = round(max(obj.size) * 1.16)
        im = Image.new('RGBA', (side, side))
        im.paste(obj, ((side-obj.width)//2, (side-obj.height)//2))
        im = im.resize((512, 512), Image.Resampling.LANCZOS)
    pixels = np.array(im)
    # Lock generated strokes and white insets to the product palette. Keep alpha.
    # The generator introduced cyan fringe pixels; those belong to the blue edge.
    white = pixels[:, :, 0] > 170
    pixels[:, :, :3] = np.where(white[:, :, None], [255, 255, 255], [0, 102, 204])
    im = Image.fromarray(pixels)
    im.save(src.with_name(item['name'] + '.png'))
    if src.exists():
        src.unlink()
    for size in (128, 64):
        dest = root / f'png-{size}'
        dest.mkdir(exist_ok=True)
        im.resize((size, size), Image.Resampling.LANCZOS).save(dest / (item['name'] + '.png'))
    idx = item['index'] - 1
    x, y = (idx % 4) * 280, (idx // 4) * 280 + 90
    for name, board in boards.items():
        board.paste(im.resize((200, 200), Image.Resampling.LANCZOS), (x + 40, y + 12), im.resize((200, 200), Image.Resampling.LANCZOS))
        d = ImageDraw.Draw(board)
        color = '#ffffff' if name == 'qa-dark' else '#1d1d1f'
        d.text((x + 140, y + 230), item['name'], font=small, fill=color, anchor='mm')
for name, board in boards.items():
    d = ImageDraw.Draw(board)
    color = '#ffffff' if name == 'qa-dark' else '#1d1d1f'
    d.text((40, 28), 'ALCHEMY STARS  /  APPLE BLUE ICONS', font=font, fill=color)
    d.text((40, 58), '16 feature icons  /  Action Blue #0066cc  /  transparent PNG', font=small, fill=color)
    board.save(root / f'{name}.png')
print('16 icons exported at 512, 128, 64 pixels; 3 QA sheets created')
