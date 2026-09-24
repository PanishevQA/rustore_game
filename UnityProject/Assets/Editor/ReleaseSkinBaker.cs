#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    [InitializeOnLoad]
    internal static class ReleaseSkinBaker
    {
        private const string Folder = "Assets/Resources/ReleaseSkin";
        private const int Version = 4;

        static ReleaseSkinBaker()
        {
            EditorApplication.delayCall += EnsureBaked;
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ/Bake Release Skin")]
        public static void EnsureBaked()
        {
            Directory.CreateDirectory(Folder);
            string versionFile = Path.Combine(Folder, ".skin-version");
            bool needsBake = !File.Exists(versionFile) || File.ReadAllText(versionFile).Trim() != Version.ToString();
            string[] required = { "Backdrop.png", "Glass.png", "Gameplay.png", "Route.png", "NavViolet.png", "Primary.png", "Secondary.png", "ScoreRing.png" };
            foreach (string file in required) needsBake |= !File.Exists(Path.Combine(Folder, file));
            if (!needsBake) return;

            Write("Backdrop", MakeBackdrop(360, 640));
            Write("Glass", MakePanel(640, 320, C("#00D8FF"), C("#102B54E8"), 42, 3));
            Write("Gameplay", MakePanel(640, 420, C("#2DA8FF"), C("#082246F4"), 48, 3));
            Write("Route", MakeRoutePanel(640, 360));
            Write("NavViolet", MakePanel(640, 180, C("#9A55FF"), C("#191348F2"), 40, 3));
            Write("Primary", MakeButton(640, 170, true));
            Write("Secondary", MakeButton(640, 170, false));
            Write("ScoreRing", MakeScoreRing(512));

            File.WriteAllText(versionFile, Version.ToString());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Configure("Backdrop", 0, false);
            Configure("Glass", 56, true);
            Configure("Gameplay", 62, true);
            Configure("Route", 56, true);
            Configure("NavViolet", 50, true);
            Configure("Primary", 52, true);
            Configure("Secondary", 52, true);
            Configure("ScoreRing", 0, false);
            AssetDatabase.SaveAssets();
        }

        private static void Write(string name, Texture2D texture)
        {
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void Configure(string name, int border, bool sliced)
        {
            string path = Folder + "/" + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = name == "Backdrop" ? 1024 : 2048;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = sliced ? new Vector4(border, border, border, border) : Vector4.zero;
            importer.SaveAndReimport();
        }

        private static Texture2D MakeBackdrop(int w, int h)
        {
            Texture2D t = NewTexture(w,h);
            Color top = C("#071A42");
            Color mid = C("#06102B");
            Color bottom = C("#020611");
            for (int y=0;y<h;y++)
            {
                float ny=y/(float)(h-1);
                Color baseC = ny > .46f ? Color.Lerp(mid, top, (ny-.46f)/.54f) : Color.Lerp(bottom, mid, ny/.46f);
                for (int x=0;x<w;x++)
                {
                    float nx=x/(float)(w-1);
                    Color col=baseC;
                    float cg = Glow(nx,ny,.16f,.80f,.48f);
                    float vg = Glow(nx,ny,.88f,.62f,.55f);
                    col += new Color(.01f,.10f,.17f,0)*cg;
                    col += new Color(.09f,.02f,.18f,0)*vg;
                    float vig = Mathf.SmoothStep(0f,.12f,Mathf.Min(nx,1f-nx));
                    col = Color.Lerp(new Color(.002f,.004f,.012f,1f),col,.72f+.28f*vig);
                    int star=(x*37+y*73+x*y*3)%521;
                    if(ny>.28f && star==0) col=Color.Lerp(col,new Color(.30f,.75f,1f,1f),.62f);
                    t.SetPixel(x,y,col);
                }
            }
            t.Apply();
            return t;
        }

        private static Texture2D MakePanel(int w,int h,Color rim,Color fill,int radius,int rimPx)
        {
            Texture2D t=NewTexture(w,h);
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)
            {
                float a=RoundedMask(x,y,w,h,radius);
                if(a<=0){t.SetPixel(x,y,Color.clear);continue;}
                float edge=RoundedEdge(x,y,w,h,radius,rimPx);
                float sheen=Mathf.Clamp01((y-(h*.55f))/(h*.45f));
                Color body=fill + new Color(rim.r*.055f,rim.g*.055f,rim.b*.055f,0)*sheen;
                Color col=Color.Lerp(body,rim,edge*.92f);
                col.a*=a;
                t.SetPixel(x,y,col);
            }
            t.Apply(); return t;
        }

        private static Texture2D MakeRoutePanel(int w,int h)
        {
            Texture2D t=MakePanel(w,h,C("#33A8FF"),C("#071A37"),46,3);
            Color grid=new Color(.20f,.55f,.85f,.08f);
            for(int x=80;x<w;x+=80) for(int y=24;y<h-24;y++) Blend(t,x,y,grid);
            for(int y=60;y<h;y+=60) for(int x=24;x<w-24;x++) Blend(t,x,y,grid);
            DrawGlowLine(t,new[]{new Vector2(.08f,.28f),new Vector2(.20f,.52f),new Vector2(.34f,.68f),new Vector2(.48f,.48f),new Vector2(.64f,.34f),new Vector2(.78f,.57f),new Vector2(.92f,.72f)},new Color(.12f,.72f,1f,1f),10,26);
            DrawRing(t,(int)(w*.08f),(int)(h*.28f),18,8,Color.white);
            DrawRing(t,(int)(w*.92f),(int)(h*.72f),20,8,C("#35B8FF"));
            t.Apply(); return t;
        }

        private static Texture2D MakeButton(int w,int h,bool primary)
        {
            Color left=primary?C("#0B9BFF"):C("#071A36");
            Color right=primary?C("#6948FF"):C("#121343");
            Color rim=primary?C("#45E7FF"):C("#31A9FF");
            Texture2D t=NewTexture(w,h);
            int r=48;
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)
            {
                float a=RoundedMask(x,y,w,h,r);
                if(a<=0){t.SetPixel(x,y,Color.clear);continue;}
                float nx=x/(float)(w-1);
                Color col=Color.Lerp(left,right,nx);
                float highlight=Mathf.SmoothStep(.52f,1f,y/(float)(h-1));
                col += new Color(.12f,.12f,.18f,0)*highlight;
                float edge=RoundedEdge(x,y,w,h,r,3);
                col=Color.Lerp(col,rim,edge*.88f);
                col.a*=a; t.SetPixel(x,y,col);
            }
            t.Apply(); return t;
        }

        private static Texture2D MakeScoreRing(int size)
        {
            Texture2D t=NewTexture(size,size);
            Vector2 center=new Vector2((size-1)*.5f,(size-1)*.5f);
            float outer=size*.46f, inner=size*.31f;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float d=Vector2.Distance(new Vector2(x,y),center);
                float glow=Mathf.Clamp01(1f-Mathf.Abs(d-outer)/(size*.075f));
                float ring=(d<=outer && d>=inner)?1f:0f;
                Color col=new Color(.05f,.80f,1f,0);
                if(ring>0) col=Color.Lerp(C("#13CFFF"),C("#6C53FF"),(Mathf.Atan2(y-center.y,x-center.x)+Mathf.PI)/(2*Mathf.PI));
                col.a=Mathf.Max(ring,glow*.42f);
                t.SetPixel(x,y,col);
            }
            t.Apply(); return t;
        }

        private static Texture2D NewTexture(int w,int h)
        {
            Texture2D t=new Texture2D(w,h,TextureFormat.RGBA32,false);
            t.wrapMode=TextureWrapMode.Clamp; t.filterMode=FilterMode.Bilinear;
            return t;
        }

        private static Color C(string hex){ColorUtility.TryParseHtmlString(hex,out Color c);return c;}
        private static float Glow(float x,float y,float cx,float cy,float r){return Mathf.Pow(Mathf.Clamp01(1f-Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy))/r),1.65f);}
        private static float RoundedMask(int x,int y,int w,int h,int r)
        {
            float cx=x<r?r:x>=w-r?w-r-1:x, cy=y<r?r:y>=h-r?h-r-1:y;
            float d=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));
            return d<=r-.75f?1f:d<=r+.75f?Mathf.Clamp01(r+.75f-d):0f;
        }
        private static float RoundedEdge(int x,int y,int w,int h,int r,int px)
        {
            if(RoundedMask(x,y,w,h,r)<=0)return 0;
            if(x<px||y<px||x>=w-px||y>=h-px)return 1f;
            if(x<r||x>=w-r||y<r||y>=h-r)
            {
                float cx=x<r?r:x>=w-r?w-r-1:x, cy=y<r?r:y>=h-r?h-r-1:y;
                float d=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));
                return Mathf.SmoothStep(r-px,r,d);
            }
            return 0f;
        }
        private static void Blend(Texture2D t,int x,int y,Color src)
        {
            if(x<0||y<0||x>=t.width||y>=t.height)return;
            Color dst=t.GetPixel(x,y);
            t.SetPixel(x,y,Color.Lerp(dst,src,src.a));
        }
        private static void DrawGlowLine(Texture2D t,Vector2[] pts,Color c,int width,int glow)
        {
            for(int i=0;i<pts.Length-1;i++)
            {
                Vector2 a=new Vector2(pts[i].x*t.width,pts[i].y*t.height), b=new Vector2(pts[i+1].x*t.width,pts[i+1].y*t.height);
                int steps=Mathf.CeilToInt(Vector2.Distance(a,b)*1.4f);
                for(int s=0;s<=steps;s++)
                {
                    Vector2 p=Vector2.Lerp(a,b,s/(float)Mathf.Max(1,steps));
                    DrawDisc(t,(int)p.x,(int)p.y,glow,new Color(c.r,c.g,c.b,.05f));
                    DrawDisc(t,(int)p.x,(int)p.y,width,c);
                }
            }
        }
        private static void DrawDisc(Texture2D t,int cx,int cy,int r,Color c)
        {
            for(int y=-r;y<=r;y++) for(int x=-r;x<=r;x++) if(x*x+y*y<=r*r) Blend(t,cx+x,cy+y,c);
        }
        private static void DrawRing(Texture2D t,int cx,int cy,int r,int thickness,Color c)
        {
            int inner=r-thickness;
            for(int y=-r;y<=r;y++) for(int x=-r;x<=r;x++)
            {
                int d=x*x+y*y; if(d<=r*r&&d>=inner*inner) Blend(t,cx+x,cy+y,c);
            }
        }

        private sealed class BuildHook : IPreprocessBuildWithReport
        {
            public int callbackOrder => -10000;
            public void OnPreprocessBuild(BuildReport report) => EnsureBaked();
        }
    }
}
#endif
