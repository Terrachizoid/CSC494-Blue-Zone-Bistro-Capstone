# Preview renderer for the scenery mood LUTs (Data/ColorGrade.md). Mirrors BistroLutMood.Map and BistroLut.Blend.
# Edit MOODS to match the UI Theme, then run: python ColorMoods.py <image> <out.png>  (needs numpy + Pillow, offline only).
# Mirror of BistroLutMood / BistroLut in C# (same formulas), for previews.
import numpy as np
from PIL import Image, ImageDraw, ImageFont
LUMA=np.array([.299,.587,.114])
def mood_map(c, m):
    c=np.asarray(c,dtype=np.float64)
    off=np.zeros_like(c)
    for f,t,reach in m['shifts']:
        f=np.array(f);t=np.array(t)
        d2=((c-f)**2).sum(-1,keepdims=True)
        off+=np.exp(-d2/(reach*reach))*(t-f)
    x=c+off
    l=(x*LUMA).sum(-1,keepdims=True)
    x=l+(x-l)*m['sat']
    x=(x-.5)*m['con']+.5
    x=x*m['bri']
    return np.clip(x,0,1)
def grid():
    r=np.arange(16)/15
    G,B,R=np.meshgrid(r,r,r,indexing='ij')  # [g][b][r]
    return np.stack([R,G,B],-1)            # strip pixel (y=g, x=b*16+r)
def blend(m, u, v):
    """u: popularity (-1..1 around start), v: wellbeing. Corners: Blue(+,+) Popular(+,-) Niche(-,+) Struggling(-,-)."""
    g=grid(); ident=g
    C={k:mood_map(g,m[k]) for k in m}
    a,b=abs(u),abs(v)
    pu='hi' if u>=0 else 'lo'; pv='hi' if v>=0 else 'lo'
    corner={('hi','hi'):'Blue',('hi','lo'):'Popular',('lo','hi'):'Niche',('lo','lo'):'Struggling'}
    Eu=(C[corner[(pu,'hi')]]+C[corner[(pu,'lo')]])/2
    Ev=(C[corner[('hi',pv)]]+C[corner[('lo',pv)]])/2
    Cq=C[corner[(pu,pv)]]
    return ident*(1-a)*(1-b)+Eu*a*(1-b)+Ev*(1-a)*b+Cq*a*b
def apply(img, lut):
    x=np.asarray(img,dtype=np.float64)/255
    p=x*15; i0=np.floor(p).astype(int); i1=np.minimum(i0+1,15); f=p-i0
    out=np.zeros_like(x)
    for dr in (0,1):
        for dg in (0,1):
            for db in (0,1):
                ir=np.where(dr,i1[...,0],i0[...,0]); ig=np.where(dg,i1[...,1],i0[...,1]); ib=np.where(db,i1[...,2],i0[...,2])
                w=(f[...,0] if dr else 1-f[...,0])*(f[...,1] if dg else 1-f[...,1])*(f[...,2] if db else 1-f[...,2])
                out+=lut[ig,ib,ir]*w[...,None]
    return Image.fromarray((np.clip(out,0,1)*255+.5).astype(np.uint8))
MOODS={
 'Blue':dict(sat=1.12,con=1.06,bri=1.02,shifts=[
   ((.41,.51,.58),(.33,.54,.64),.22),((.49,.50,.46),(.38,.56,.36),.14),((.38,.39,.36),(.27,.42,.28),.12),((.80,.78,.76),(.86,.84,.78),.20)]),
 'Popular':dict(sat=1.10,con=1.08,bri=1.0,shifts=[
   ((.80,.78,.76),(.93,.80,.64),.25),((.41,.51,.58),(.42,.52,.52),.20),((.49,.50,.46),(.55,.53,.38),.15),((.30,.31,.30),(.32,.25,.24),.18)]),
 'Niche':dict(sat=.95,con=.92,bri=1.04,shifts=[
   ((.49,.50,.46),(.45,.56,.45),.15),((.41,.51,.58),(.44,.57,.63),.20),((.80,.78,.76),(.86,.85,.80),.20)]),
 'Struggling':dict(sat=.75,con=.88,bri=.94,shifts=[
   ((.80,.78,.76),(.76,.68,.60),.25),((.41,.51,.58),(.46,.48,.47),.20),((.49,.50,.46),(.51,.47,.40),.15)]),
}

if __name__ == '__main__':
    import sys
    src, out = sys.argv[1], sys.argv[2]
    im = Image.open(src).convert('RGB'); im.thumbnail((420, 420))
    cw, ch = im.size; S = Image.new('RGB', (3*cw+32, 3*(ch+24)+48), (245, 243, 238)); d = ImageDraw.Draw(S)
    names = {(1,1):'Blue',(1,0):'popular',(1,-1):'Popular',(0,1):'healthy',(0,0):'START',(0,-1):'unhealthy',(-1,1):'Niche',(-1,0):'unpopular',(-1,-1):'Struggling'}
    for row, u in enumerate((1, 0, -1)):
        for col, v in enumerate((-1, 0, 1)):
            x = 8+col*(cw+8); y = 44+row*(ch+24)
            S.paste(apply(im, blend(MOODS, u, v)), (x, y+20)); d.text((x, y+2), names[(u, v)], fill=(30, 30, 30))
    S.save(out)
