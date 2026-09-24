param(
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot "..\UnityProject\Assets\Resources\ReleaseSkin"
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

function C([int]$a,[int]$r,[int]$g,[int]$b) { [System.Drawing.Color]::FromArgb($a,$r,$g,$b) }
function RectF([float]$x,[float]$y,[float]$w,[float]$h) { New-Object System.Drawing.RectangleF($x,$y,$w,$h) }

function RoundedPath([System.Drawing.RectangleF]$r,[float]$radius) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $p.AddArc($r.X,$r.Y,$d,$d,180,90)
    $p.AddArc($r.Right-$d,$r.Y,$d,$d,270,90)
    $p.AddArc($r.Right-$d,$r.Bottom-$d,$d,$d,0,90)
    $p.AddArc($r.X,$r.Bottom-$d,$d,$d,90,90)
    $p.CloseFigure()
    return $p
}

function Canvas([int]$w,[int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    return @($bmp,$g)
}

function SavePng($bmp,$g,[string]$name) {
    $g.Dispose()
    $path = Join-Path $OutputDir $name
    $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Wrote $path"
}

function DrawGlowBorder($g,[System.Drawing.RectangleF]$r,[float]$radius,[System.Drawing.Color]$color,[int]$layers=8) {
    for($i=$layers;$i -ge 1;$i--) {
        $alpha = [Math]::Max(6,[int](60/$i))
        $pen = New-Object System.Drawing.Pen((C $alpha $color.R $color.G $color.B), (2 + $i*2))
        $path = RoundedPath $r $radius
        $g.DrawPath($pen,$path)
        $pen.Dispose(); $path.Dispose()
    }
}

function DrawGlassPanel([string]$name,[int]$w,[int]$h,[bool]$violet,[bool]$gameplay) {
    $cg = Canvas $w $h; $bmp=$cg[0]; $g=$cg[1]
    $r = RectF 34 34 ($w-68) ($h-68)
    $radius = [Math]::Min(54,[Math]::Round($h*0.14))
    $cyan = C 255 0 210 255
    $blue = C 255 20 105 255
    $vio = C 255 153 71 255
    $accent = if($violet){$vio}else{$cyan}
    DrawGlowBorder $g $r $radius $accent 7

    $path = RoundedPath $r $radius
    $top = if($violet){ C 245 34 14 88 } elseif($gameplay){ C 248 5 28 70 } else { C 248 12 39 88 }
    $bottom = C 250 3 12 38
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($r,$top,$bottom,90)
    $g.FillPath($brush,$path)
    $brush.Dispose()

    $pen = New-Object System.Drawing.Pen((if($violet){C 230 158 66 255}else{C 230 0 201 255}),4)
    $g.DrawPath($pen,$path)
    $pen.Dispose()

    $inner = RectF ($r.X+8) ($r.Y+8) ($r.Width-16) ($r.Height-16)
    $innerPath=RoundedPath $inner ([Math]::Max(8,$radius-8))
    $innerPen=New-Object System.Drawing.Pen((C 125 49 110 255),2)
    $g.DrawPath($innerPen,$innerPath)
    $innerPen.Dispose(); $innerPath.Dispose()

    $shine = New-Object System.Drawing.Drawing2D.LinearGradientBrush((RectF $r.X $r.Y $r.Width ($r.Height*0.46)),(C 75 62 182 255),(C 0 62 182 255),90)
    $g.FillPath($shine,$path)
    $shine.Dispose()

    if($violet) {
        $starPen = New-Object System.Drawing.Pen((C 210 220 245 255),2)
        foreach($pt in @(@(90,92),@(118,158),@($w-120,95),@($w-95,$h-90))) {
            $g.DrawLine($starPen,$pt[0]-5,$pt[1],$pt[0]+5,$pt[1])
            $g.DrawLine($starPen,$pt[0],$pt[1]-5,$pt[0],$pt[1]+5)
        }
        $starPen.Dispose()
        $arrowPen=New-Object System.Drawing.Pen((C 245 203 123 255),14)
        $arrowPen.StartCap=[System.Drawing.Drawing2D.LineCap]::Round
        $arrowPen.EndCap=[System.Drawing.Drawing2D.LineCap]::Round
        $x=$w-92; $y=$h/2
        $g.DrawLines($arrowPen,[System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF($x-20,$y-26)),
            (New-Object System.Drawing.PointF($x+8,$y)),
            (New-Object System.Drawing.PointF($x-20,$y+26))
        ))
        $arrowPen.Dispose()
    }

    $path.Dispose()
    SavePng $bmp $g $name
}

function DrawButton([string]$name,[bool]$primary) {
    $w=768; $h=256
    $cg=Canvas $w $h; $bmp=$cg[0]; $g=$cg[1]
    $r=RectF 58 54 652 148; $radius=74
    $accent=if($primary){C 255 0 229 255}else{C 255 32 109 255}
    DrawGlowBorder $g $r $radius $accent 9
    $path=RoundedPath $r $radius
    if($primary){
        $b=New-Object System.Drawing.Drawing2D.LinearGradientBrush($r,(C 255 49 237 244),(C 255 19 81 234),0)
        $blend=New-Object System.Drawing.Drawing2D.ColorBlend
        $blend.Colors=[System.Drawing.Color[]]@((C 255 58 238 241),(C 255 18 174 243),(C 255 31 77 233))
        $blend.Positions=[single[]]@(0,0.48,1)
        $b.InterpolationColors=$blend
        $g.FillPath($b,$path); $b.Dispose()
        $hi=New-Object System.Drawing.Pen((C 190 188 255 255),4)
        $g.DrawArc($hi,82,70,598,88,195,150); $hi.Dispose()
    } else {
        $b=New-Object System.Drawing.Drawing2D.LinearGradientBrush($r,(C 255 11 44 99),(C 255 7 21 63),0)
        $g.FillPath($b,$path); $b.Dispose()
    }
    $pen=New-Object System.Drawing.Pen((if($primary){C 245 0 244 255}else{C 245 28 150 255}),4)
    $g.DrawPath($pen,$path); $pen.Dispose(); $path.Dispose()
    SavePng $bmp $g $name
}

function DrawBackground() {
    $w=720;$h=1280
    $cg=Canvas $w $h; $bmp=$cg[0]; $g=$cg[1]
    $r=RectF 0 0 $w $h
    $bg=New-Object System.Drawing.Drawing2D.LinearGradientBrush($r,(C 255 2 8 30),(C 255 3 17 58),90)
    $g.FillRectangle($bg,$r);$bg.Dispose()

    foreach($a in 1..18){
        $alpha=[int](7 + (19-$a)*1.8)
        $pen=New-Object System.Drawing.Pen((C $alpha 0 117 255),(2+$a*1.2))
        $pts=[System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF(-50,980)),
            (New-Object System.Drawing.PointF(110,880)),
            (New-Object System.Drawing.PointF(230,930)),
            (New-Object System.Drawing.PointF(390,790)),
            (New-Object System.Drawing.PointF(770,860))
        )
        $g.DrawCurve($pen,$pts,0.42);$pen.Dispose()
    }
    foreach($a in 1..14){
        $alpha=[int](6 + (15-$a)*1.7)
        $pen=New-Object System.Drawing.Pen((C $alpha 117 35 255),(2+$a*1.4))
        $pts=[System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF(-80,300)),
            (New-Object System.Drawing.PointF(120,240)),
            (New-Object System.Drawing.PointF(260,350)),
            (New-Object System.Drawing.PointF(500,250)),
            (New-Object System.Drawing.PointF(760,310))
        )
        $g.DrawCurve($pen,$pts,0.48);$pen.Dispose()
    }
    $rand=New-Object System.Random(14159)
    for($i=0;$i -lt 100;$i++){
        $x=$rand.Next(10,$w-10);$y=$rand.Next(10,$h-10)
        $sz=if($i%11 -eq 0){3}else{1}
        $col=if($i%4 -eq 0){C 210 142 93 255}else{C 175 55 198 255}
        $b=New-Object System.Drawing.SolidBrush($col)
        $g.FillEllipse($b,$x,$y,$sz,$sz);$b.Dispose()
    }
    SavePng $bmp $g "bg_release.png"
}

function DrawRoutePanel() {
    $w=768;$h=576
    $cg=Canvas $w $h; $bmp=$cg[0]; $g=$cg[1]
    $r=RectF 34 34 ($w-68) ($h-68)
    DrawGlowBorder $g $r 48 (C 255 0 201 255) 6
    $path=RoundedPath $r 48
    $b=New-Object System.Drawing.Drawing2D.LinearGradientBrush($r,(C 250 8 31 79),(C 250 2 14 48),90)
    $g.FillPath($b,$path);$b.Dispose()
    $p=New-Object System.Drawing.Pen((C 220 14 157 255),3);$g.DrawPath($p,$path);$p.Dispose()

    $gridPen=New-Object System.Drawing.Pen((C 34 38 102 164),1)
    for($x=72;$x -lt $w-50;$x+=50){$g.DrawLine($gridPen,$x,70,$x,$h-70)}
    for($y=86;$y -lt $h-60;$y+=50){$g.DrawLine($gridPen,60,$y,$w-60,$y)}
    $gridPen.Dispose()

    $pts=[System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(110,420)),
        (New-Object System.Drawing.PointF(220,300)),
        (New-Object System.Drawing.PointF(330,250)),
        (New-Object System.Drawing.PointF(430,330)),
        (New-Object System.Drawing.PointF(540,300)),
        (New-Object System.Drawing.PointF(650,160))
    )
    for($i=18;$i -ge 3;$i-=3){
        $pen=New-Object System.Drawing.Pen((C ([Math]::Max(12,90-$i*3)) 0 223 255),$i)
        $pen.StartCap=[System.Drawing.Drawing2D.LineCap]::Round;$pen.EndCap=[System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawCurve($pen,$pts,0.45);$pen.Dispose()
    }
    $line=New-Object System.Drawing.Pen((C 255 76 219 255),7)
    $line.StartCap=[System.Drawing.Drawing2D.LineCap]::Round;$line.EndCap=[System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawCurve($line,$pts,0.45);$line.Dispose()

    foreach($m in @(@(110,420,0),@(330,250,1),@(540,300,1),@(650,160,2))){
        $x=$m[0];$y=$m[1];$kind=$m[2]
        $glow=New-Object System.Drawing.SolidBrush((C 70 0 223 255));$g.FillEllipse($glow,$x-24,$y-24,48,48);$glow.Dispose()
        $br=New-Object System.Drawing.SolidBrush((if($kind -eq 2){C 255 179 73 255}else{C 255 32 223 255}))
        $g.FillEllipse($br,$x-10,$y-10,20,20);$br.Dispose()
        $in=New-Object System.Drawing.SolidBrush((C 255 8 31 70));$g.FillEllipse($in,$x-4,$y-4,8,8);$in.Dispose()
    }
    $path.Dispose()
    SavePng $bmp $g "panel_route.png"
}

function DrawScoreRing() {
    $w=640;$h=640
    $cg=Canvas $w $h; $bmp=$cg[0]; $g=$cg[1]
    $cx=320;$cy=320;$r=215
    for($glow=30;$glow -ge 6;$glow-=4){
        $pen=New-Object System.Drawing.Pen((C ([Math]::Max(6,54-$glow)) 30 168 255),$glow)
        $g.DrawEllipse($pen,$cx-$r,$cy-$r,$r*2,$r*2);$pen.Dispose()
    }
    $segments=96
    for($i=0;$i -lt $segments;$i++){
        $t=$i/[double]$segments
        if($t -lt .45){
            $u=$t/.45
            $rr=[int](0 + (32-0)*$u);$gg=[int](229 + (118-229)*$u);$bb=[int](255 + (255-255)*$u)
        } elseif($t -lt .78){
            $u=($t-.45)/.33
            $rr=[int](32 + (142-32)*$u);$gg=[int](118 + (63-118)*$u);$bb=[int](255 + (246-255)*$u)
        } else {
            $u=($t-.78)/.22
            $rr=[int](142 + (255-142)*$u);$gg=[int](63 + (72-63)*$u);$bb=[int](246 + (167-246)*$u)
        }
        $pen=New-Object System.Drawing.Pen((C 255 $rr $gg $bb),30)
        $pen.StartCap=[System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($pen,$cx-$r,$cy-$r,$r*2,$r*2,[float](-90+$i*(360/$segments)),[float](360/$segments+1))
        $pen.Dispose()
    }
    $innerB=New-Object System.Drawing.SolidBrush((C 235 2 11 38));$g.FillEllipse($innerB,140,140,360,360);$innerB.Dispose()
    $spark=New-Object System.Drawing.Pen((C 220 195 237 255),3)
    foreach($a in 0,35,78,132,205,265,315){
        $rad=$a*[Math]::PI/180;$x=$cx+[Math]::Cos($rad)*260;$y=$cy+[Math]::Sin($rad)*260
        $g.DrawLine($spark,$x-7,$y,$x+7,$y);$g.DrawLine($spark,$x,$y-7,$x,$y+7)
    }
    $spark.Dispose()
    SavePng $bmp $g "score_ring.png"
}

DrawBackground
DrawButton "button_primary.png" $true
DrawButton "button_secondary.png" $false
DrawGlassPanel "panel_glass.png" 768 576 $false $false
DrawGlassPanel "panel_gameplay.png" 768 512 $false $true
DrawGlassPanel "panel_nav_violet.png" 768 256 $true $false
DrawRoutePanel
DrawScoreRing

function WriteMeta([string]$file,[string]$guid,[bool]$sprite,[int]$border,[int]$maxSize) {
    $textureType = if($sprite){8}else{0}
    $spriteMode = if($sprite){1}else{0}
    $meta = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: $maxSize
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: $spriteMode
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: $border, y: $border, z: $border, w: $border}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: $textureType
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: $maxSize
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Android
    maxTextureSize: $maxSize
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    Set-Content -Path (Join-Path $OutputDir ($file + ".meta")) -Value $meta -Encoding UTF8
}

WriteMeta "bg_release.png" "f3c2a773d4eb4c1587bcb4fe7c44c001" $false 0 2048
WriteMeta "button_primary.png" "f3c2a773d4eb4c1587bcb4fe7c44c002" $true 92 1024
WriteMeta "button_secondary.png" "f3c2a773d4eb4c1587bcb4fe7c44c003" $true 92 1024
WriteMeta "panel_glass.png" "f3c2a773d4eb4c1587bcb4fe7c44c004" $true 72 1024
WriteMeta "panel_gameplay.png" "f3c2a773d4eb4c1587bcb4fe7c44c005" $true 72 1024
WriteMeta "panel_nav_violet.png" "f3c2a773d4eb4c1587bcb4fe7c44c006" $true 86 1024
WriteMeta "panel_route.png" "f3c2a773d4eb4c1587bcb4fe7c44c007" $true 64 1024
WriteMeta "score_ring.png" "f3c2a773d4eb4c1587bcb4fe7c44c008" $true 0 1024

$folderMeta = @"
fileFormatVersion: 2
guid: f3c2a773d4eb4c1587bcb4fe7c44c000
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
Set-Content -Path ($OutputDir + ".meta") -Value $folderMeta -Encoding UTF8
Write-Host "Release texture skin generated in $OutputDir"
