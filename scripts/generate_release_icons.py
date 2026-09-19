"""Reproducible, text-free UI symbols. Requires Pillow; no downloaded/AI lettering.

256 px transparent masters, antialiased geometry and restrained bloom. Existing
Unity GUIDs are retained so regenerating the pack never breaks references.
"""
from pathlib import Path
import math
import re
import uuid
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "UnityProject/Assets/Resources/GeneratedUI"
S = 3
N = 256
CYAN = (0, 220, 255)
VIOLET = (150, 102, 255)
GOLD = (255, 201, 57)
MINT = (65, 241, 187)


def star(cx, cy, outer, inner, count=5):
    return [(cx + math.cos(-math.pi / 2 + i * math.pi / count) * (outer if i % 2 == 0 else inner),
             cy + math.sin(-math.pi / 2 + i * math.pi / count) * (outer if i % 2 == 0 else inner))
            for i in range(count * 2)]


class Symbol:
    def __init__(self, accent=CYAN):
        self.mask = Image.new("L", (N * S, N * S))
        self.d = ImageDraw.Draw(self.mask)
        self.accent = accent

    def box(self, xy, radius=12, fill=255, width=None):
        self.d.rounded_rectangle(tuple(int(v * S) for v in xy), radius * S,
                                 fill=fill if width is None else None,
                                 outline=fill if width else None, width=(width or 1) * S)

    def ellipse(self, xy, fill=255, width=None):
        self.d.ellipse(tuple(int(v * S) for v in xy), fill=fill if width is None else None,
                       outline=fill if width else None, width=(width or 1) * S)

    def line(self, points, width=12, fill=255):
        self.d.line([(int(x * S), int(y * S)) for x, y in points], fill, width * S, joint="curve")
        for x, y in [points[0], points[-1]]:
            self.ellipse((x - width / 2, y - width / 2, x + width / 2, y + width / 2), fill)

    def polygon(self, pts, fill=255):
        self.d.polygon([(int(x * S), int(y * S)) for x, y in pts], fill)

    def arc(self, xy, a, b, width=12):
        self.d.arc(tuple(int(v * S) for v in xy), a, b, 255, width * S)

    def save(self, name):
        # A vertical highlight gives all symbols the same material without a noisy tile.
        shade = Image.new("RGBA", self.mask.size)
        draw = ImageDraw.Draw(shade)
        for y in range(N * S):
            t = y / (N * S)
            light = max(0, (0.60 - t) * 0.8)
            rgb = tuple(int(c + (255 - c) * light) for c in self.accent)
            draw.line((0, y, N * S, y), fill=(*rgb, 255))
        shade.putalpha(self.mask)
        bloom = Image.new("RGBA", shade.size, (*self.accent, 0))
        bloom.putalpha(self.mask.filter(ImageFilter.GaussianBlur(6 * S)).point(lambda a: int(a * .25)))
        bloom.alpha_composite(shade)
        bloom.resize((N, N), Image.Resampling.LANCZOS).save(OUT / (name + ".png"))


def make_pack():
    a = Symbol(); a.box((45, 62, 211, 216), 20); a.box((58, 105, 198, 200), 8, 0)
    a.line([(83, 42), (83, 79)], 14); a.line([(174, 42), (174, 79)], 14)
    a.polygon(star(128, 149, 35, 16)); a.save("icon_daily")
    a = Symbol(VIOLET); a.polygon([(33, 207), (102, 91), (158, 207)])
    a.polygon([(107, 207), (157, 139), (218, 207)]); a.line([(129, 50), (129, 111)], 10)
    a.polygon([(132, 42), (208, 68), (132, 98)]); a.save("icon_campaign")
    a = Symbol(); a.polygon([(69, 41), (217, 128), (69, 215)]); a.save("icon_training")
    a = Symbol();
    for x, y in [(45, 132), (105, 53), (165, 99)]: a.box((x, y, x + 43, 215), 9)
    a.save("icon_stats")
    a = Symbol(); a.line([(35, 50), (61, 50), (86, 173), (195, 173)], 12)
    a.polygon([(65, 78), (221, 78), (202, 148), (80, 148)])
    a.ellipse((84, 191, 111, 218)); a.ellipse((173, 191, 200, 218)); a.save("icon_store")
    a = Symbol(GOLD); a.ellipse((38, 38, 218, 218)); a.ellipse((52, 52, 204, 204), 0)
    a.ellipse((63, 63, 193, 193)); a.polygon(star(128, 128, 44, 21), 0); a.save("coin")
    a = Symbol(GOLD); a.ellipse((66, 35, 190, 159)); a.box((93, 134, 163, 183), 12)
    a.line([(101, 199), (155, 199)], 10); a.line([(113, 218), (143, 218)], 10); a.save("icon_hint")
    a = Symbol(); a.polygon([(23, 128), (65, 77), (128, 53), (191, 77), (233, 128), (191, 179), (128, 203), (65, 179)])
    a.ellipse((78, 78, 178, 178), 0); a.ellipse((99, 99, 157, 157)); a.save("icon_eye")
    a = Symbol(); a.arc((50, 50, 212, 212), 215, 505, 17)
    a.polygon([(38, 52), (96, 75), (46, 114)]); a.save("icon_replay")
    a = Symbol(); a.line([(78, 128), (181, 66)], 12); a.line([(78, 128), (181, 191)], 12)
    for x, y in [(65, 128), (189, 57), (189, 199)]: a.ellipse((x-25, y-25, x+25, y+25))
    a.save("icon_share")
    a = Symbol(VIOLET)
    for flip in [False, True]:
        pts = [(72, 45), (91, 47), (201, 178), (181, 197), (61, 65)]
        a.polygon([(256-x if flip else x, y) for x,y in pts])
        pts = [(160, 183), (200, 147)]
        a.line([(256-x if flip else x, y) for x,y in pts], 11)
    a.save("icon_challenge")
    a = Symbol(); a.box((31, 62, 225, 198), 20, width=12)
    a.polygon([(94, 94), (151, 129), (94, 163)])
    for y in (91, 125, 159): a.box((189, y, 202, y+12), 3)
    a.save("icon_ad")
    a = Symbol();
    for i in range(8):
        t=i*math.pi/4; a.line([(128+math.cos(t)*70,128+math.sin(t)*70),(128+math.cos(t)*92,128+math.sin(t)*92)],24)
    a.ellipse((57,57,199,199)); a.ellipse((99,99,157,157),0); a.save("icon_settings")
    a = Symbol(MINT); a.ellipse((40,40,216,216), width=10); a.line([(82,128),(115,161),(177,94)],16); a.save("icon_check")
    a = Symbol((149,169,199)); a.arc((78,37,178,153),180,360,14); a.box((55,106,201,214),20)
    a.ellipse((116,141,140,165),0); a.box((122,154,134,184),5,0); a.save("icon_lock")
    a = Symbol(VIOLET); a.ellipse((37,37,218,218)); a.ellipse((133,139,218,226),0)
    for x,y in [(82,101),(127,73),(172,98),(75,157)]: a.ellipse((x-14,y-14,x+14,y+14),0)
    a.save("item_cosmetic")
    a = Symbol((255,109,122)); a.ellipse((36,36,220,220),width=13); a.box((74,82,182,176),9,width=9)
    a.polygon([(112,107),(147,129),(112,151)]); a.line([(63,194),(194,63)],15); a.save("item_no_ads")
    for name, color, empty in [("star_filled",GOLD,False),("star_empty",(137,156,185),True)]:
        a=Symbol(color); a.polygon(star(128,128,96,45))
        if empty: a.polygon(star(128,128,74,35),0)
        a.save(name)
    for name,color in [("medal_bronze",(223,141,82)),("medal_silver",(178,199,223)),("medal_gold",GOLD)]:
        a=Symbol(color); a.polygon(star(128,128,106,90,12)); a.ellipse((51,51,205,205),0)
        a.ellipse((61,61,195,195)); a.polygon(star(128,127,45,22),0); a.save(name)
    for name,color in [("marker_start_glow",CYAN),("marker_end_glow",GOLD)]:
        a=Symbol(color); a.ellipse((51,51,205,205)); a.ellipse((64,64,192,192),0)
        a.ellipse((76,76,180,180)); a.ellipse((101,95,123,117),210); a.save(name)


def import_settings():
    template=(OUT/"icon_training.png.meta").read_text(encoding="utf-8")
    replacements={"enableMipMap":0,"maxTextureSize":256,"spriteMode":1,
                  "alphaIsTransparency":1,"textureType":8,"nPOTScale":0,
                  "wrapU":1,"wrapV":1,"wrapW":1,"spriteGenerateFallbackPhysicsShape":0}
    for key,value in replacements.items():
        template=re.sub(rf"(?m)^(\s*{key}:) .*",rf"\g<1> {value}",template)
    template=re.sub(r"(?m)^  platformSettings:.*?(?=^  spriteSheet:)",
        "  platformSettings:\n  - serializedVersion: 3\n    buildTarget: DefaultTexturePlatform\n"
        "    maxTextureSize: 256\n    resizeAlgorithm: 0\n    textureFormat: -1\n"
        "    textureCompression: 0\n    compressionQuality: 100\n    crunchedCompression: 0\n"
        "    allowsAlphaSplitting: 0\n    overridden: 0\n    ignorePlatformSupport: 0\n"
        "    androidETC2FallbackOverride: 0\n    forceMaximumCompressionQuality_BC6H_BC7: 0\n",
        template,flags=re.S|re.M)
    for png in sorted(OUT.glob("*.png")):
        meta=png.with_suffix(".png.meta")
        old=meta.read_text(encoding="utf-8") if meta.exists() else ""
        guid=re.search(r"^guid: (\w+)",old,re.M)
        content=re.sub(r"^guid: \w+", "guid: "+(guid[1] if guid else uuid.uuid5(uuid.NAMESPACE_URL,png.name).hex), template,flags=re.M)
        meta.write_text(content,encoding="utf-8",newline="\n")


if __name__=="__main__":
    make_pack()
    import_settings()
    preview=Image.new("RGB",(6*180,4*205),(5,13,29))
    draw=ImageDraw.Draw(preview)
    for i,p in enumerate(sorted(OUT.glob("*.png"))):
        im=Image.open(p).convert("RGBA"); im.load()
        preview.paste(im.resize((144,144)),((i%6)*180+18,(i//6)*205+10),im.resize((144,144)))
        draw.text(((i%6)*180+8,(i//6)*205+166),p.stem,fill=(185,203,228))
    target=ROOT/"artifacts/ui-release/icon-pack.png"
    target.parent.mkdir(parents=True,exist_ok=True)
    preview.save(target)
    print(f"Generated {len(list(OUT.glob('*.png')))} valid UI sprites, preserving existing GUIDs.")
