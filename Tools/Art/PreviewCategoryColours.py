"""Non-destructive colour study. Dependencies are external, never vendored into Git.
Usage: python PreviewCategoryColours.py --deps <external-library-folder> --output <external-preview-folder>
Produces preview copies only; does not change CSV content, classifications or live assets.
"""
import argparse, csv, json, sys
from collections import deque
from pathlib import Path

args=argparse.ArgumentParser()
args.add_argument('--deps',required=True)
args.add_argument('--output',required=True)
options=args.parse_args()
sys.path.insert(0,options.deps)
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[2]
out=Path(options.output).resolve()
if out == root or root in out.parents:
    raise ValueError('Keep preview output outside the submitted repository.')
out.mkdir(parents=True,exist_ok=True)
palette={r['category']:r for r in csv.DictReader((root/'Tools/Art/CategoryPalette.csv').open())}
foods=list(csv.DictReader((root/'Assets/GameData/CSV/Blue_Zone_Bistro_Foods.csv').open(encoding='utf-8-sig')))
report=[]
for food in foods:
    if food['recipe_category'] not in palette: continue
    front=root/food['icon_path'];back=root/food['back_path']
    if not front.is_file() or not back.is_file():continue
    target=np.array([int(palette[food['recipe_category']]['colour'][i:i+2],16) for i in (1,3,5)])/255.
    reference=Image.open(back).convert('RGB')
    rgb=np.asarray(reference)/255.
    hsv=np.asarray(reference.convert('HSV'))/255.
    # Dominant hue of the quiet top strip. The separate back is not a pixel-aligned mask.
    strip=hsv[:max(1,hsv.shape[0]//8)]
    hist,_=np.histogram(strip[:,:,0],bins=72,range=(0,1),weights=strip[:,:,1]+.05)
    hue=(hist.argmax()+.5)/72
    base_y=np.median(rgb@np.array([.2126,.7152,.0722]))
    sat_limit=min(.65,float(np.quantile(hsv[:,:,1],.9))+.14)
    for source,suffix in [(front,'front'),(back,'back')]:
        im=Image.open(source).convert('RGBA');pixels=np.asarray(im).copy();src=pixels[:,:,:3]/255.
        values=np.asarray(im.convert('RGB').convert('HSV'))/255.
        y=src@np.array([.2126,.7152,.0722])
        mapped=np.clip(target+(y-base_y)[:,:,None],0,1)
        if suffix=='back':weight=np.ones(y.shape)
        else:
            distance=np.abs(values[:,:,0]-hue);distance=np.minimum(distance,1-distance)
            weight=np.clip((.095-distance)/.045,0,1)*np.clip((sat_limit-values[:,:,1])/.12,0,1)
            # Protect very dark food contours and highlights close to white.
            weight*=np.clip((y-.15)/.18,0,1)*np.clip((.98-y)/.09,0,1)
            # Only recolour candidate regions connected to the image edge. This protects isolated
            # pale highlights on berries/food that happen to share the original background hue.
            candidate=weight>.02;connected=np.zeros(candidate.shape,dtype=bool)
            height,width=candidate.shape;queue=deque()
            for row,col in [(0,x) for x in range(width)]+[(height-1,x) for x in range(width)]+[(z,0) for z in range(height)]+[(z,width-1) for z in range(height)]:
                if candidate[row,col] and not connected[row,col]:connected[row,col]=True;queue.append((row,col))
            while queue:
                row,col=queue.popleft()
                for rr,cc in ((row-1,col),(row+1,col),(row,col-1),(row,col+1)):
                    if 0<=rr<height and 0<=cc<width and candidate[rr,cc] and not connected[rr,cc]:connected[rr,cc]=True;queue.append((rr,cc))
            weight*=connected
        pixels[:,:,:3]=np.round((src*(1-weight[:,:,None])+mapped*weight[:,:,None])*255).astype('uint8')
        Image.fromarray(pixels).save(out/f"{food['id']}_{suffix}.png")
    report.append({'id':food['id'],'category':food['recipe_category'],'colour':palette[food['recipe_category']]['colour'],'source_hue':float(hue)})
(out/'manifest.json').write_text(json.dumps({'status':'Experimental selective filter; flattened fronts mean foreground isolation is approximate. Originals unchanged.','foods':report},indent=2))
print(f'Created {len(report)} preview pairs in {out}')
