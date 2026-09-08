import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

SS = 2
W, H = 512, 512
CW, CH = W * SS, H * SS

def radial_gradient(size, center, radius, color_in, color_out):
    w, h = size
    yy, xx = np.mgrid[0:h, 0:w]
    cx, cy = center
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / max(radius, 1e-3)
    d = np.clip(d, 0, 1)[..., None]
    a = np.array(color_in, dtype=np.float32)
    b = np.array(color_out, dtype=np.float32)
    return (a * (1 - d) + b * d).astype(np.uint8)

def linear_gradient(size, angle_deg, color_a, color_b):
    w, h = size
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    theta = np.deg2rad(angle_deg)
    proj = xx * np.cos(theta) + yy * np.sin(theta)
    proj -= proj.min(); proj /= (proj.max() + 1e-6)
    proj = proj[..., None]
    a = np.array(color_a, dtype=np.float32); b = np.array(color_b, dtype=np.float32)
    return (a * (1 - proj) + b * proj).astype(np.uint8)

def np_to_rgba(arr, alpha=255):
    h, w, _ = arr.shape
    out = np.dstack([arr, np.full((h, w), alpha, dtype=np.uint8)])
    return Image.fromarray(out.astype(np.uint8))

def new_canvas():
    return Image.new("RGBA", (CW, CH), (0, 0, 0, 0))

def panel_background(base_dark, base_light):
    bg = new_canvas()
    grad = radial_gradient((CW, CH), (CW*0.42, CH*0.38), CW*0.75, base_light, base_dark)
    panel_img = np_to_rgba(grad)
    mask = Image.new("L", (CW, CH), 0)
    md = ImageDraw.Draw(mask)
    r = int(CW * 0.09)
    md.rounded_rectangle([CW*0.03, CH*0.03, CW*0.97, CH*0.97], radius=r, fill=255)
    bg = Image.composite(panel_img, bg, mask)
    border = new_canvas()
    bd = ImageDraw.Draw(border)
    bd.rounded_rectangle([CW*0.03, CH*0.03, CW*0.97, CH*0.97], radius=r, outline=(58,64,84,255), width=int(SS*2))
    return bg, mask, border

def guide_ring(gcx_f=0.5, gcy_f=0.62, grx_f=0.20, gry_f=0.12, color=(70,76,96,120)):
    g = new_canvas()
    gd = ImageDraw.Draw(g)
    gcx, gcy = CW*gcx_f, CH*gcy_f
    grx, gry = CW*grx_f, CH*gry_f
    gd.ellipse([gcx-grx, gcy-gry, gcx+grx, gcy+gry], outline=color, width=int(SS*2.2))
    return g

def fill_mask_gradient(mask, center, radius, color_in, color_out, blur=1.2, thresh=110):
    m = mask.filter(ImageFilter.GaussianBlur(SS*blur))
    m = m.point(lambda p: 255 if p > thresh else 0)
    m = m.filter(ImageFilter.GaussianBlur(SS*0.7))
    grad = radial_gradient((CW, CH), center, radius, color_in, color_out)
    fill_img = np_to_rgba(grad)
    layer = new_canvas()
    layer = Image.composite(fill_img, layer, m)
    return layer, m

def outline_from_mask(mask, color, width=7):
    outline_mask = mask.filter(ImageFilter.MaxFilter(int(SS*width)//2*2+1))
    outline_only = ImageChops.subtract(outline_mask, mask)
    layer = new_canvas()
    color_img = Image.new("RGBA", (CW, CH), color)
    layer = Image.composite(color_img, layer, outline_only)
    return layer

def circle_gem(canvas, center, r, core_color, edge_color, ring_color=(30,20,10,255), shine=True, glow_color=None, glow_strength=140):
    cx, cy = center
    if glow_color is not None:
        glow = new_canvas()
        yy, xx = np.mgrid[0:CH, 0:CW]
        dist = np.sqrt((xx-cx)**2 + (yy-cy)**2) / (r*3.0)
        a = np.clip(1 - dist, 0, 1) ** 2 * glow_strength
        glow_alpha = Image.fromarray(a.astype(np.uint8))
        glow_color_img = Image.new("RGBA", (CW,CH), glow_color)
        glow.paste(glow_color_img, (0,0), glow_alpha)
        glow = glow.filter(ImageFilter.GaussianBlur(SS*6))
        canvas = Image.alpha_composite(canvas, glow)
    bezel = new_canvas()
    bzd = ImageDraw.Draw(bezel)
    bzd.ellipse([cx-r*1.28, cy-r*1.28, cx+r*1.28, cy+r*1.28], fill=ring_color, outline=(245,232,210,255), width=int(SS*2.0))
    canvas = Image.alpha_composite(canvas, bezel)
    grad = radial_gradient((CW,CH), (cx - r*0.3, cy - r*0.3), r*1.5, core_color, edge_color)
    core_img = np_to_rgba(grad)
    core_mask = Image.new("L", (CW,CH), 0)
    cmd = ImageDraw.Draw(core_mask)
    cmd.ellipse([cx-r, cy-r, cx+r, cy+r], fill=255)
    canvas = Image.alpha_composite(canvas, Image.composite(core_img, new_canvas(), core_mask))
    if shine:
        sh = new_canvas()
        shd = ImageDraw.Draw(sh)
        shd.ellipse([cx-r*0.45, cy-r*0.55, cx-r*0.05, cy-r*0.15], fill=(255,255,255,235))
        sh = sh.filter(ImageFilter.GaussianBlur(SS*1.0))
        canvas = Image.alpha_composite(canvas, sh)
    return canvas

def drop_shadow(bg, content):
    alpha = content.split()[3]
    shadow = new_canvas()
    shadow.paste((0,0,0,110), (0,0), alpha)
    shadow = shadow.filter(ImageFilter.GaussianBlur(SS*5))
    shadow = ImageChops.offset(shadow, int(SS*3), int(SS*6))
    out = new_canvas()
    out = Image.alpha_composite(out, bg)
    out = Image.alpha_composite(out, shadow)
    out = Image.alpha_composite(out, content)
    return out

def finalize(bg, content, panel_mask, border):
    out = drop_shadow(bg, content)
    out = Image.composite(out, new_canvas(), panel_mask)
    out = Image.alpha_composite(out, border)
    return out.resize((W, H), Image.LANCZOS)
