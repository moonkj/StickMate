import sys
sys.path.insert(0, "/private/tmp/claude-501/-Users-kjmoon-App-StickMate/b03562ef-02d9-492a-a8ed-a33855111fff/scratchpad/icon_proto")
from icon_kit import *
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import numpy as np

OUT = "/private/tmp/claude-501/-Users-kjmoon-App-StickMate/b03562ef-02d9-492a-a8ed-a33855111fff/scratchpad/icon_proto/"

PALETTES = {
    "cyber":  {"hi": (140, 235, 220), "lo": (0, 110, 96),  "dark": (0, 60, 52),  "trim": (235, 250, 246), "bg": ((10,20,20),(22,36,36))},
    "mine":   {"hi": (255, 195, 120), "lo": (170, 92, 0),  "dark": (90, 48, 0),  "trim": (250, 236, 214), "bg": ((22,16,10),(38,28,16))},
    "arcane": {"hi": (150, 235, 170), "lo": (0, 120, 46),  "dark": (0, 62, 26),  "trim": (236, 250, 238), "bg": ((10,20,14),(20,36,26))},
}

def base(pal_key):
    pal = PALETTES[pal_key]
    bg, mask, border = panel_background(pal["bg"][0], pal["bg"][1])
    ring = guide_ring()
    bg = Image.alpha_composite(bg, ring)
    return pal, bg, mask, border

def poly_mask(points):
    m = Image.new("L", (CW, CH), 0)
    d = ImageDraw.Draw(m)
    d.polygon(points, fill=255)
    return m

def ellipse_mask(box):
    m = Image.new("L", (CW, CH), 0)
    ImageDraw.Draw(m).ellipse(box, fill=255)
    return m

def pie_mask(box, a0, a1):
    m = Image.new("L", (CW, CH), 0)
    ImageDraw.Draw(m).pieslice(box, a0, a1, fill=255)
    return m

def render_shape(mask, pal, grad_center, grad_r, content, outline_w=7, hi=None, lo=None):
    hi = hi or pal["hi"]; lo = lo or pal["lo"]
    outline = outline_from_mask(mask, pal["trim"] + (255,), width=outline_w)
    fill, _ = fill_mask_gradient(mask, grad_center, grad_r, hi, lo)
    content = Image.alpha_composite(content, outline)
    content = Image.alpha_composite(content, fill)
    return content

def seam_line(content, box, a0, a1, color, width=2.4):
    seam = new_canvas(); sd = ImageDraw.Draw(seam)
    sd.arc(box, a0, a1, fill=color, width=int(SS*width))
    return Image.alpha_composite(content, seam)

def save(pal, bg, mask, border, content, name):
    out = finalize(bg, content, mask, border)
    out.save(OUT + name + ".png")
    print("saved", name)

# ============================================================ CYBER ============================================================

def item_patched_hood():
    pal, bg, mask, border = base("cyber")
    cx, top_y, dw, dh = CW*0.46, CH*0.18, CW*0.30, CH*0.19
    m = pie_mask([cx-dw, top_y, cx+dw, top_y+dh*2], 180, 360)
    band_top = top_y+dh*0.92; band_bot = band_top+CH*0.09
    m = ImageChops.lighter(m, Image.new("L",(CW,CH),0))
    band = Image.new("L",(CW,CH),0); ImageDraw.Draw(band).rectangle([cx-dw, band_top, cx+dw, band_bot], fill=255)
    m = ImageChops.lighter(m, band)
    side = Image.new("L",(CW,CH),0)
    ImageDraw.Draw(side).polygon([(cx-dw*1.02,band_bot-CH*0.03),(cx-dw*1.32,band_bot+CH*0.16),(cx-dw*1.02,band_bot+CH*0.20),(cx-dw*0.8,band_bot)], fill=255)
    m = ImageChops.lighter(m, side)
    m = m.filter(ImageFilter.GaussianBlur(SS*1.2)).point(lambda p:255 if p>110 else 0).filter(ImageFilter.GaussianBlur(SS*0.7))
    content = render_shape(m, pal, (cx-dw*0.3, top_y+dh*0.3), CW*0.4, new_canvas())
    content = seam_line(content, [cx-dw*0.55, top_y+dh*0.1, cx+dw*0.55, top_y+dh*1.85], 200, 340, pal["dark"]+(200,))
    hexm = Image.new("L",(CW,CH),0)
    hx, hy, hr = cx+dw*0.55, band_top+CH*0.02, CW*0.045
    pts = [(hx+hr*np.cos(a), hy+hr*np.sin(a)) for a in np.linspace(0, 2*np.pi, 7)]
    ImageDraw.Draw(hexm).polygon(pts, fill=255)
    hex_layer, _ = fill_mask_gradient(hexm, (hx-hr*0.3,hy-hr*0.3), hr*1.6, pal["hi"], pal["lo"], blur=0.6, thresh=110)
    hex_outline = outline_from_mask(hexm, pal["trim"]+(255,), width=4)
    content = Image.alpha_composite(content, hex_outline); content = Image.alpha_composite(content, hex_layer)
    dotm = ellipse_mask([hx-hr*0.22,hy-hr*0.22,hx+hr*0.22,hy+hr*0.22])
    content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),(20,40,38,255)), new_canvas(), dotm))
    save(pal, bg, mask, border, content, "pack_cyber_head_patched_hood")

def item_slit_visor():
    pal, bg, mask, border = base("cyber")
    cx, cy = CW*0.46, CH*0.42
    bw, bh = CW*0.30, CH*0.075
    m = Image.new("L",(CW,CH),0)
    ImageDraw.Draw(m).rounded_rectangle([cx-bw, cy-bh, cx+bw, cy+bh], radius=int(bh*0.5), fill=255)
    content = render_shape(m, pal, (cx-bw*0.3, cy-bh*0.6), CW*0.32, new_canvas(), outline_w=6)
    slit = new_canvas(); sd=ImageDraw.Draw(slit)
    sd.rounded_rectangle([cx-bw*0.86, cy-bh*0.28, cx+bw*0.86, cy+bh*0.28], radius=int(bh*0.28), fill=pal["dark"]+(255,))
    content = Image.alpha_composite(content, slit)
    lens_grad = radial_gradient((CW,CH), (cx-bw*0.2,cy), bw*0.9, (210,255,248), pal["lo"])
    lens_mask = Image.new("L",(CW,CH),0)
    ImageDraw.Draw(lens_mask).rounded_rectangle([cx-bw*0.82, cy-bh*0.22, cx+bw*0.82, cy+bh*0.22], radius=int(bh*0.2), fill=255)
    content = Image.alpha_composite(content, Image.composite(np_to_rgba(lens_grad,180), new_canvas(), lens_mask))
    plugm = Image.new("L",(CW,CH),0)
    pd = ImageDraw.Draw(plugm)
    pd.line([cx+bw*0.85, cy+bh*0.5, cx+bw*1.05, cy+bh*1.6], fill=255, width=int(SS*3))
    plug_layer = new_canvas(); ImageDraw.Draw(plug_layer).line([cx+bw*0.85, cy+bh*0.5, cx+bw*1.05, cy+bh*1.6], fill=pal["dark"]+(255,), width=int(SS*3))
    content = Image.alpha_composite(content, plug_layer)
    nodem = ellipse_mask([cx+bw*1.0,cy+bh*1.45,cx+bw*1.15,cy+bh*1.6])
    content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),pal["hi"]+(255,)), new_canvas(), nodem))
    for sx in (-1,1):
        rvt = ellipse_mask([cx+sx*bw*0.94-CW*0.012, cy-CW*0.012, cx+sx*bw*0.94+CW*0.012, cy+CW*0.012])
        content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),(230,240,238,255)), new_canvas(), rvt))
    save(pal, bg, mask, border, content, "pack_cyber_eyes_slit_visor")

def item_cable_collar():
    pal, bg, mask, border = base("cyber")
    cx, cy = CW*0.46, CH*0.44
    n = 7; amp = CH*0.05; rspan = CW*0.30
    pts = []
    for i in range(n+1):
        x = cx - rspan + (2*rspan)*i/n
        y = cy + (amp if i%2==0 else -amp)
        pts.append((x,y))
    band = new_canvas(); bd = ImageDraw.Draw(band)
    bd.line(pts, fill=pal["lo"]+(255,), width=int(SS*9), joint="curve")
    band = band.filter(ImageFilter.GaussianBlur(SS*0.3))
    band_mask = Image.new("L",(CW,CH),0)
    ImageDraw.Draw(band_mask).line(pts, fill=255, width=int(SS*9), joint="curve")
    fill_layer, _ = fill_mask_gradient(band_mask, (cx-rspan*0.3, cy-amp), CW*0.4, pal["hi"], pal["lo"], blur=0.8, thresh=90)
    outline = outline_from_mask(band_mask, pal["trim"]+(255,), width=5)
    content = Image.alpha_composite(new_canvas(), outline)
    content = Image.alpha_composite(content, fill_layer)
    plugm_line = [(cx+rspan*0.05, cy+amp), (cx+rspan*0.05, cy+amp+CH*0.09)]
    plug_layer = new_canvas(); ImageDraw.Draw(plug_layer).line(plugm_line, fill=pal["dark"]+(255,), width=int(SS*3.2))
    content = Image.alpha_composite(content, plug_layer)
    nodem = Image.new("L",(CW,CH),0)
    ImageDraw.Draw(nodem).rounded_rectangle([cx+rspan*0.05-CW*0.02, cy+amp+CH*0.08, cx+rspan*0.05+CW*0.02, cy+amp+CH*0.12], radius=int(CW*0.006), fill=255)
    content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),pal["hi"]+(255,)), new_canvas(), nodem))
    save(pal, bg, mask, border, content, "pack_cyber_neck_cable_collar")

def item_tarp_cape():
    pal, bg, mask, border = base("cyber")
    cx, top = CW*0.46, CH*0.20
    w = CW*0.30
    pts = [(cx-w, top), (cx+w, top), (cx+w*0.9, top+CH*0.62), (cx+w*0.5, top+CH*0.50),
           (cx+w*0.2, top+CH*0.66), (cx-w*0.1, top+CH*0.48), (cx-w*0.45, top+CH*0.60), (cx-w*0.9, top+CH*0.40)]
    m = poly_mask(pts)
    m = m.filter(ImageFilter.GaussianBlur(SS*0.8)).point(lambda p:255 if p>110 else 0)
    content = render_shape(m, pal, (cx-w*0.4, top+CH*0.05), CW*0.55, new_canvas(), outline_w=6)
    collar = new_canvas(); ImageDraw.Draw(collar).rounded_rectangle([cx-w*0.5, top-CH*0.02, cx+w*0.5, top+CH*0.08], radius=int(CW*0.02), fill=pal["dark"]+(255,))
    content = Image.alpha_composite(content, collar)
    for sx, sy in [(-0.15,0.20),(0.25,0.28),(-0.4,0.42)]:
        gm = poly_mask([(cx+w*sx-CW*0.018,cy_:=(top+CH*sy)-CH*0.02),(cx+w*sx+CW*0.018,cy_),(cx+w*sx,cy_+CH*0.045)])
        content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),pal["hi"]+(255,)), new_canvas(), gm))
    save(pal, bg, mask, border, content, "pack_cyber_back_tarp_cape")

# ============================================================ MINE ============================================================

def item_miner_helmet():
    pal, bg, mask, border = base("mine")
    cx, top_y, dw, dh = CW*0.46, CH*0.20, CW*0.34, CH*0.20
    m = pie_mask([cx-dw, top_y, cx+dw, top_y+dh*2], 180, 360)
    band_top = top_y+dh*0.92; band_bot = band_top+CH*0.075
    band = Image.new("L",(CW,CH),0); ImageDraw.Draw(band).rectangle([cx-dw,band_top,cx+dw,band_bot], fill=255)
    m = ImageChops.lighter(m, band)
    brim = pie_mask([cx-dw*1.05, band_bot-CH*0.02, cx+dw*1.1, band_bot+CH*0.11], 0, 180)
    m = ImageChops.lighter(m, brim)
    m = m.filter(ImageFilter.GaussianBlur(SS*1.2)).point(lambda p:255 if p>110 else 0).filter(ImageFilter.GaussianBlur(SS*0.7))
    content = render_shape(m, pal, (cx-dw*0.35, top_y+dh*0.35), CW*0.42, new_canvas())
    content = seam_line(content, [cx-dw*0.55, top_y+dh*0.15, cx+dw*0.55, top_y+dh*1.9], 195, 345, pal["dark"]+(200,))
    for xo in (-1,1):
        rvt = ellipse_mask([cx+xo*dw*0.62-CW*0.014, band_top+CH*0.02-CW*0.014, cx+xo*dw*0.62+CW*0.014, band_top+CH*0.02+CW*0.014])
        content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),(40,26,10,255)), new_canvas(), rvt))
        content = Image.alpha_composite(content, outline_from_mask(rvt, pal["trim"]+(255,), width=3))
    lamp_cx, lamp_cy, lamp_r = cx+dw*0.72, top_y+dh*1.15, CW*0.075
    content = circle_gem(content, (lamp_cx,lamp_cy), lamp_r, (255,250,225), (214,138,30), glow_color=(255,244,190,255))
    strap = new_canvas(); sd = ImageDraw.Draw(strap)
    sd.line([cx-dw*0.95, band_bot+CH*0.10, cx-dw*0.15, band_bot+CH*0.05], fill=(20,14,8,255), width=int(SS*3.4))
    sd.line([cx+dw*0.15, band_bot+CH*0.05, cx+dw*0.95, band_bot+CH*0.11], fill=(20,14,8,255), width=int(SS*3.4))
    content = Image.alpha_composite(content, strap)
    save(pal, bg, mask, border, content, "pack_mine_head_miner_helmet")

def item_dust_goggles():
    pal, bg, mask, border = base("mine")
    cx, cy = CW*0.46, CH*0.42
    lr = CW*0.11
    m = new_canvas().convert("L") if False else Image.new("L",(CW,CH),0)
    d = ImageDraw.Draw(m)
    for sx in (-1,1):
        d.ellipse([cx+sx*lr*1.05-lr, cy-lr, cx+sx*lr*1.05+lr, cy+lr], fill=255)
    bridge = Image.new("L",(CW,CH),0)
    ImageDraw.Draw(bridge).rectangle([cx-lr*0.35, cy-lr*0.25, cx+lr*0.35, cy+lr*0.25], fill=255)
    m = ImageChops.lighter(m, bridge)
    m = m.filter(ImageFilter.GaussianBlur(SS*1.0)).point(lambda p:255 if p>110 else 0).filter(ImageFilter.GaussianBlur(SS*0.6))
    content = render_shape(m, pal, (cx-lr, cy-lr*0.6), CW*0.4, new_canvas(), outline_w=6, hi=pal["trim"], lo=pal["lo"])
    for sx in (-1,1):
        lens_grad = radial_gradient((CW,CH), (cx+sx*lr*1.05-lr*0.3, cy-lr*0.3), lr*1.3, pal["hi"], (40,24,6))
        lens_mask = ellipse_mask([cx+sx*lr*1.05-lr*0.78, cy-lr*0.78, cx+sx*lr*1.05+lr*0.78, cy+lr*0.78])
        content = Image.alpha_composite(content, Image.composite(np_to_rgba(lens_grad), new_canvas(), lens_mask))
        content = Image.alpha_composite(content, outline_from_mask(lens_mask, (30,20,10,255), width=4))
        sh = new_canvas(); ImageDraw.Draw(sh).ellipse([cx+sx*lr*1.05-lr*0.45, cy-lr*0.6, cx+sx*lr*1.05-lr*0.05, cy-lr*0.25], fill=(255,255,255,160))
        content = Image.alpha_composite(content, sh.filter(ImageFilter.GaussianBlur(SS*0.8)))
    strap = new_canvas(); sd=ImageDraw.Draw(strap)
    sd.line([cx-lr*2.0, cy, cx-lr*2.7, cy+CH*0.02], fill=(30,20,10,255), width=int(SS*4))
    sd.line([cx+lr*2.0, cy, cx+lr*2.7, cy+CH*0.02], fill=(30,20,10,255), width=int(SS*4))
    content = Image.alpha_composite(content, strap)
    save(pal, bg, mask, border, content, "pack_mine_eyes_dust_goggles")

def item_mine_lamp():
    pal, bg, mask, border = base("mine")
    cx, cy = CW*0.46, CH*0.36
    top_l, top_r = (cx-CW*0.22, CH*0.16), (cx+CW*0.22, CH*0.16)
    apex = (cx, cy)
    strap = new_canvas(); sd = ImageDraw.Draw(strap)
    for pt in (top_l, top_r):
        sd.line([pt, apex], fill=pal["lo"]+(255,), width=int(SS*7))
    strap_mask = Image.new("L",(CW,CH),0); smd = ImageDraw.Draw(strap_mask)
    for pt in (top_l, top_r):
        smd.line([pt, apex], fill=255, width=int(SS*7))
    outline = outline_from_mask(strap_mask, pal["trim"]+(255,), width=5)
    fill_layer,_ = fill_mask_gradient(strap_mask, (cx-CW*0.1, CH*0.2), CW*0.4, pal["hi"], pal["lo"], blur=0.6, thresh=90)
    content = Image.alpha_composite(new_canvas(), outline)
    content = Image.alpha_composite(content, fill_layer)
    pend_cy = cy + CH*0.14
    content = circle_gem(content, (cx, pend_cy), CW*0.085, (255,250,225), (214,138,30), glow_color=(255,244,190,255), glow_strength=110)
    save(pal, bg, mask, border, content, "pack_mine_neck_mine_lamp")

def item_pick_harness():
    pal, bg, mask, border = base("mine")
    cx, cy = CW*0.42, CH*0.46
    strap = new_canvas(); sd = ImageDraw.Draw(strap)
    p1a,p1b = (cx-CW*0.18, cy-CH*0.20), (cx+CW*0.18, cy+CH*0.20)
    p2a,p2b = (cx+CW*0.18, cy-CH*0.20), (cx-CW*0.18, cy+CH*0.20)
    smask = Image.new("L",(CW,CH),0); smd = ImageDraw.Draw(smask)
    smd.line([p1a,p1b], fill=255, width=int(SS*8))
    smd.line([p2a,p2b], fill=255, width=int(SS*8))
    outline = outline_from_mask(smask, pal["trim"]+(255,), width=5)
    fill_layer,_ = fill_mask_gradient(smask, (cx-CW*0.1, cy-CH*0.1), CW*0.4, pal["hi"], pal["lo"], blur=0.6, thresh=90)
    content = Image.alpha_composite(new_canvas(), outline)
    content = Image.alpha_composite(content, fill_layer)
    buckle = ellipse_mask([cx-CW*0.03,cy-CW*0.03,cx+CW*0.03,cy+CW*0.03])
    content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),pal["dark"]+(255,)), new_canvas(), buckle))
    content = Image.alpha_composite(content, outline_from_mask(buckle, pal["trim"]+(255,), width=4))
    hx, hy = cx+CW*0.22, cy-CH*0.28
    head = new_canvas(); hd = ImageDraw.Draw(head)
    hd.polygon([(hx-CW*0.09,hy-CH*0.01),(hx+CW*0.10,hy-CH*0.07),(hx+CW*0.13,hy-CH*0.03),(hx-CW*0.06,hy+CH*0.05)], fill=pal["hi"]+(255,))
    hd.polygon([(hx-CW*0.09,hy-CH*0.01),(hx-CW*0.22,hy+CH*0.06),(hx-CW*0.20,hy+CH*0.10),(hx-CW*0.06,hy+CH*0.05)], fill=pal["hi"]+(255,))
    handle = new_canvas(); hld = ImageDraw.Draw(handle)
    hld.line([hx,hy, hx-CW*0.02, hy+CH*0.24], fill=pal["dark"]+(255,), width=int(SS*5))
    content = Image.alpha_composite(content, handle)
    content = Image.alpha_composite(content, head)
    pick_outline_mask = Image.new("L",(CW,CH),0); pod = ImageDraw.Draw(pick_outline_mask)
    pod.polygon([(hx-CW*0.09,hy-CH*0.01),(hx+CW*0.10,hy-CH*0.07),(hx+CW*0.13,hy-CH*0.03),(hx-CW*0.06,hy+CH*0.05)], fill=255)
    pod.polygon([(hx-CW*0.09,hy-CH*0.01),(hx-CW*0.22,hy+CH*0.06),(hx-CW*0.20,hy+CH*0.10),(hx-CW*0.06,hy+CH*0.05)], fill=255)
    content = Image.alpha_composite(content, outline_from_mask(pick_outline_mask, pal["trim"]+(255,), width=4))
    clip = ellipse_mask([cx-CW*0.20,cy+CH*0.19,cx-CW*0.20+CW*0.05,cy+CH*0.19+CW*0.05])
    content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),pal["hi"]+(255,)), new_canvas(), clip))
    content = Image.alpha_composite(content, outline_from_mask(clip, pal["dark"]+(255,), width=3))
    save(pal, bg, mask, border, content, "pack_mine_back_pick_harness")

# ============================================================ ARCANE ============================================================

def item_wizard_hat():
    pal, bg, mask, border = base("arcane")
    cx, apex_y = CW*0.48, CH*0.14
    base_y = CH*0.42
    pts = [(cx, apex_y), (cx+CW*0.14, base_y-CH*0.02), (cx+CW*0.30, base_y+CH*0.03), (cx-CW*0.10, base_y-CH*0.01)]
    m = poly_mask(pts)
    brim = ellipse_mask([cx-CW*0.30, base_y-CH*0.03, cx+CW*0.30, base_y+CH*0.07])
    m = ImageChops.lighter(m, brim)
    m = m.filter(ImageFilter.GaussianBlur(SS*0.8)).point(lambda p:255 if p>110 else 0)
    content = render_shape(m, pal, (cx-CW*0.05, apex_y+CH*0.05), CW*0.5, new_canvas(), outline_w=6)
    content = seam_line(content, [cx-CW*0.02,apex_y+CH*0.02,cx+CW*0.02,base_y], 250,270, pal["dark"]+(180,), width=1.6)
    charm_cx, charm_cy = cx+CW*0.14, base_y-CH*0.09
    moon = new_canvas(); md = ImageDraw.Draw(moon)
    md.ellipse([charm_cx-CW*0.045,charm_cy-CW*0.045,charm_cx+CW*0.045,charm_cy+CW*0.045], fill=pal["trim"]+(255,))
    md.ellipse([charm_cx-CW*0.03,charm_cy-CW*0.05,charm_cx+CW*0.055,charm_cy+CW*0.04], fill=(0,0,0,0))
    moon_mask = Image.new("L",(CW,CH),0); mmd = ImageDraw.Draw(moon_mask)
    mmd.ellipse([charm_cx-CW*0.045,charm_cy-CW*0.045,charm_cx+CW*0.045,charm_cy+CW*0.045], fill=255)
    mmd.ellipse([charm_cx-CW*0.015,charm_cy-CW*0.055,charm_cx+CW*0.075,charm_cy+CW*0.035], fill=0)
    content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),pal["trim"]+(255,)), new_canvas(), moon_mask))
    for i,dy in enumerate([0.10, 0.20]):
        gm = ellipse_mask([charm_cx-CW*0.01, charm_cy+CH*dy-CW*0.012, charm_cx+CW*0.01, charm_cy+CH*dy+CW*0.012])
        content = Image.alpha_composite(content, Image.composite(Image.new("RGBA",(CW,CH),pal["hi"]+(255,)), new_canvas(), gm))
        content = Image.alpha_composite(content, outline_from_mask(gm, pal["dark"]+(255,), width=2))
    gemxy = (cx-CW*0.02, base_y-CH*0.12)
    content = circle_gem(content, gemxy, CW*0.028, (220,255,225), pal["lo"], glow_color=None, shine=True)
    save(pal, bg, mask, border, content, "pack_arcane_head_wizard_hat")

def item_astro_lens():
    pal, bg, mask, border = base("arcane")
    cx, cy = CW*0.42, CH*0.40
    r = CW*0.09
    content = new_canvas()
    ring = new_canvas(); rd = ImageDraw.Draw(ring)
    rd.ellipse([cx-r*1.25,cy-r*1.25,cx+r*1.25,cy+r*1.25], outline=pal["hi"]+(255,), width=int(SS*7))
    content = Image.alpha_composite(content, ring)
    content = circle_gem(content, (cx,cy), r, (220,255,225), pal["lo"], glow_color=pal["hi"]+(255,), glow_strength=90)
    for a in np.linspace(0, 2*np.pi, 8, endpoint=False):
        x0,y0 = cx+r*1.25*np.cos(a), cy+r*1.25*np.sin(a)
        x1,y1 = cx+r*1.42*np.cos(a), cy+r*1.42*np.sin(a)
        spike = new_canvas(); ImageDraw.Draw(spike).line([(x0,y0),(x1,y1)], fill=pal["hi"]+(255,), width=int(SS*2.4))
        content = Image.alpha_composite(content, spike)
    chain = new_canvas(); cd = ImageDraw.Draw(chain)
    cd.line([cx+r*1.1, cy+r*1.0, cx+r*2.6, cy+r*2.2], fill=pal["dark"]+(255,), width=int(SS*2.6))
    content = Image.alpha_composite(content, chain)
    dx,dy = cx+r*2.6, cy+r*2.2
    diamond = new_canvas(); dd = ImageDraw.Draw(diamond)
    dd.polygon([(dx,dy-CW*0.03),(dx+CW*0.022,dy),(dx,dy+CW*0.03),(dx-CW*0.022,dy)], fill=pal["hi"]+(255,), outline=pal["trim"]+(255,))
    content = Image.alpha_composite(content, diamond)
    save(pal, bg, mask, border, content, "pack_arcane_eyes_astro_lens")

def item_moon_clasp():
    pal, bg, mask, border = base("arcane")
    cx, cy = CW*0.46, CH*0.44
    band = new_canvas(); bd = ImageDraw.Draw(band)
    bd.arc([cx-CW*0.30, cy-CH*0.16, cx+CW*0.30, cy+CH*0.20], 200, 340, fill=pal["lo"]+(255,), width=int(SS*8))
    band_mask = Image.new("L",(CW,CH),0)
    ImageDraw.Draw(band_mask).arc([cx-CW*0.30, cy-CH*0.16, cx+CW*0.30, cy+CH*0.20], 200, 340, fill=255, width=int(SS*8))
    outline = outline_from_mask(band_mask, pal["trim"]+(255,), width=5)
    fill_layer,_ = fill_mask_gradient(band_mask, (cx-CW*0.15, cy-CH*0.1), CW*0.4, pal["hi"], pal["lo"], blur=0.6, thresh=90)
    content = Image.alpha_composite(new_canvas(), outline)
    content = Image.alpha_composite(content, fill_layer)
    r = CW*0.11
    cres = Image.new("L",(CW,CH),0); crd = ImageDraw.Draw(cres)
    crd.ellipse([cx-r,cy-r,cx+r,cy+r], fill=255)
    crd.ellipse([cx-r+CW*0.045,cy-r-CW*0.02,cx+r+CW*0.045,cy+r-CW*0.02], fill=0)
    outline2 = outline_from_mask(cres, pal["trim"]+(255,), width=5)
    fill2,_ = fill_mask_gradient(cres, (cx-r*0.3,cy-r*0.3), r*1.6, pal["hi"], pal["lo"], blur=0.6, thresh=90)
    content = Image.alpha_composite(content, outline2)
    content = Image.alpha_composite(content, fill2)
    content = circle_gem(content, (cx-r*0.05, cy), CW*0.022, (220,255,225), pal["lo"], shine=True, glow_color=pal["hi"]+(255,), glow_strength=70)
    save(pal, bg, mask, border, content, "pack_arcane_neck_moon_clasp")

def item_moon_robe():
    pal, bg, mask, border = base("arcane")
    cx, top = CW*0.46, CH*0.20
    w = CW*0.30
    pts = [(cx-w*0.5, top), (cx+w*0.5, top), (cx+w, top+CH*0.20), (cx+w*0.75, top+CH*0.62),
           (cx+w*0.35, top+CH*0.48), (cx, top+CH*0.66), (cx-w*0.35, top+CH*0.48),
           (cx-w*0.75, top+CH*0.62), (cx-w, top+CH*0.20)]
    m = poly_mask(pts)
    m = m.filter(ImageFilter.GaussianBlur(SS*0.8)).point(lambda p:255 if p>110 else 0)
    content = render_shape(m, pal, (cx-w*0.3, top+CH*0.05), CW*0.6, new_canvas(), outline_w=6)
    collar = new_canvas(); ImageDraw.Draw(collar).rounded_rectangle([cx-w*0.45, top-CH*0.02, cx+w*0.45, top+CH*0.07], radius=int(CW*0.02), fill=pal["dark"]+(255,))
    content = Image.alpha_composite(content, collar)
    for sx, sy, sc in [(0,0.22,CW*0.022),(-0.3,0.36,CW*0.016),(0.32,0.40,CW*0.016)]:
        gx, gy = cx+w*sx, top+CH*sy
        gm = new_canvas(); gd = ImageDraw.Draw(gm)
        gd.polygon([(gx,gy-sc),(gx+sc*0.7,gy),(gx,gy+sc),(gx-sc*0.7,gy)], fill=pal["trim"]+(255,))
        content = Image.alpha_composite(content, gm)
        gm2 = new_canvas(); gd2 = ImageDraw.Draw(gm2)
        gd2.polygon([(gx,gy-sc*0.65),(gx+sc*0.42,gy),(gx,gy+sc*0.65),(gx-sc*0.42,gy)], fill=pal["hi"]+(255,))
        content = Image.alpha_composite(content, gm2)
    save(pal, bg, mask, border, content, "pack_arcane_back_moon_robe")


for fn in [item_patched_hood, item_slit_visor, item_cable_collar, item_tarp_cape,
           item_miner_helmet, item_dust_goggles, item_mine_lamp, item_pick_harness,
           item_wizard_hat, item_astro_lens, item_moon_clasp, item_moon_robe]:
    fn()

print("ALL DONE")
