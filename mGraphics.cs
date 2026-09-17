using System;
using System.Collections;
using SkiaSharp;
using UnityEngine;   // shim: Color, Rect, Vector2, GUIStyle, Texture2D, Image...

public class mGraphics
{
    // ============================================================
    //  HẰNG SỐ ANCHOR & TRANSFORM — GIỮ NGUYÊN
    // ============================================================
    public static int HCENTER = 1;
    public static int VCENTER = 2;
    public static int LEFT    = 4;
    public static int RIGHT   = 8;
    public static int TOP     = 16;
    public static int BOTTOM  = 32;

    public const int BASELINE = 64;
    public const int SOLID    = 0;
    public const int DOTTED   = 1;

    public const int TRANS_MIRROR         = 2;
    public const int TRANS_MIRROR_ROT180  = 1;
    public const int TRANS_MIRROR_ROT270  = 4;
    public const int TRANS_MIRROR_ROT90   = 7;
    public const int TRANS_NONE           = 0;
    public const int TRANS_ROT180         = 3;
    public const int TRANS_ROT270         = 6;
    public const int TRANS_ROT90          = 5;

    public static int zoomLevel = 1;
    public static int addYWhenOpenKeyBoard;

    // Giữ field để code cũ tham chiếu không lỗi; Skia không dùng cache này
    public static Hashtable cachedTextures = new Hashtable();

    // ============================================================
    //  TRẠNG THÁI MÀU / CLIP / TRANSLATE
    // ============================================================
    private float r, g, b, a;
    public int clipX, clipY, clipW, clipH;
    private bool isClip;
    private bool isTranslate;
    private int translateX, translateY;
    private float translateXf, translateYf;
    private int clipTX, clipTY;
    private int currentBGColor;

    // ============================================================
    //  SKIA — canvas do vòng lặp ngoài bơm vào
    // ============================================================
    public SKCanvas canvas;

    private readonly SKPaint fillPaint = new SKPaint
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false
    };

    private readonly SKPaint strokePaint = new SKPaint
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
        StrokeWidth = 1
    };

    private readonly SKPaint bitmapPaint = new SKPaint
    {
        IsAntialias = false,
        FilterQuality = SKFilterQuality.None   // pixel-perfect như Unity point filter
    };

    private SKColor currentColor = SKColors.White;

    // ============================================================
    //  FONT — typeface tĩnh, lazy load
    // ============================================================
    private static SKTypeface _typeface;
    private static SKPaint _textPaint;

    private static SKTypeface DefaultTextTypeface
    {
        get
        {
            if (_typeface == null)
            {
                // Ưu tiên font nhúng của game; fallback sang font hệ thống
                try { _typeface = AssetArchive.LoadGameFont(); }
                catch { _typeface = null; }

                if (_typeface == null)
                    _typeface = SKTypeface.FromFamilyName("Tahoma");
                if (_typeface == null)
                    _typeface = SKTypeface.Default;
            }
            return _typeface;
        }
    }

    private static SKPaint TextPaint
    {
        get
        {
            if (_textPaint == null)
            {
                _textPaint = new SKPaint
                {
                    Typeface = DefaultTextTypeface,
                    TextSize = 11f * zoomLevel,
                    IsAntialias = true
                };
            }
            return _textPaint;
        }
    }

    // ============================================================
    //  CONSTRUCTOR
    // ============================================================
    public mGraphics() { }

    public mGraphics(SKCanvas c)
    {
        canvas = c;
    }

    // ============================================================
    //  TRANSLATE / CLIP
    // ============================================================
    public void translate(int tx, int ty)
    {
        translateX += tx * zoomLevel;
        translateY += ty * zoomLevel;
        isTranslate = translateX != 0 || translateY != 0;
    }

    public void translate(float x, float y)
    {
        translateXf += x;
        translateYf += y;
        translateX += (int)(x * zoomLevel);
        translateY += (int)(y * zoomLevel);
        isTranslate = translateX != 0 || translateY != 0;
    }

    public int getTranslateX() => translateX / zoomLevel;
    public int getTranslateY() => translateY / zoomLevel + addYWhenOpenKeyBoard;

    public void setClip(int x, int y, int w, int h)
    {
        clipTX = translateX;
        clipTY = translateY;
        clipX = x * zoomLevel;
        clipY = y * zoomLevel;
        clipW = w * zoomLevel;
        clipH = h * zoomLevel;
        isClip = true;
    }

    public int getClipX()      => GameScr.cmx;
    public int getClipY()      => GameScr.cmy;
    public int getClipWidth()  => GameScr.gW;
    public int getClipHeight() => GameScr.gH;

    // ============================================================
    //  MÀU
    // ============================================================
    public void setColor(int rgb)
    {
        int cr = (rgb >> 16) & 0xFF;
        int cg = (rgb >> 8)  & 0xFF;
        int cb =  rgb        & 0xFF;
        b = cb / 256f;
        g = cg / 256f;
        r = cr / 256f;
        a = 1f;
        currentColor = new SKColor((byte)cr, (byte)cg, (byte)cb, 255);
    }

    public void setColor(int rgb, float alpha)
    {
        int cr = (rgb >> 16) & 0xFF;
        int cg = (rgb >> 8)  & 0xFF;
        int cb =  rgb        & 0xFF;
        b = cb / 256f;
        g = cg / 256f;
        r = cr / 256f;
        a = alpha;
        currentColor = new SKColor((byte)cr, (byte)cg, (byte)cb,
                                   (byte)Math.Min(255, alpha * 255f));
    }

    public void setColor(Color color)
    {
        b = color.b;
        g = color.g;
        r = color.r;
        a = color.a;
        currentColor = new SKColor(
            (byte)Math.Clamp(color.r * 255f, 0, 255),
            (byte)Math.Clamp(color.g * 255f, 0, 255),
            (byte)Math.Clamp(color.b * 255f, 0, 255),
            (byte)Math.Clamp(color.a * 255f, 0, 255));
    }

    public void setBgColor(int rgb)
    {
        if (rgb == currentBGColor) return;
        currentBGColor = rgb;
        setColor(rgb);
    }

    public Color setColorMiniMap(int rgb)
    {
        int num  = rgb & 0xFF;
        int num2 = (rgb >> 8) & 0xFF;
        int num3 = (rgb >> 16) & 0xFF;
        return new Color(num3 / 256f, num2 / 256f, num / 256f);
    }

    public float[] getRGB(Color cl)
        => new float[] { 256f * cl.r, 256f * cl.g, 256f * cl.b };

    public static Color setColorObj(int rgb)
    {
        int num  = rgb & 0xFF;
        int num2 = (rgb >> 8) & 0xFF;
        int num3 = (rgb >> 16) & 0xFF;
        return new Color(num3 / 256f, num2 / 256f, num / 256f);
    }

    // ============================================================
    //  FILL / DRAW RECT — vẽ trực tiếp, không cần Texture2D 1×1
    // ============================================================
    public void fillRect(int x, int y, int w, int h)
    {
        if (canvas == null) return;
        if (w < 0 || h < 0) return;

        int px = x * zoomLevel;
        int py = y * zoomLevel;
        int pw = w * zoomLevel;
        int ph = h * zoomLevel;

        if (isTranslate) { px += translateX; py += translateY; }

        // clip
        if (isClip)
        {
            int cx = clipX + clipTX;
            int cy = clipY + clipTY;
            int ix = Math.Max(px, cx);
            int iy = Math.Max(py, cy);
            int iw = Math.Min(px + pw, cx + clipW) - ix;
            int ih = Math.Min(py + ph, cy + clipH) - iy;
            if (iw <= 0 || ih <= 0) return;
            px = ix; py = iy; pw = iw; ph = ih;
        }

        fillPaint.Color = currentColor;
        canvas.DrawRect(new SKRect(px, py, px + pw, py + ph), fillPaint);
    }

    public void fillRect(int x, int y, int w, int h, int color, int alpha)
    {
        setColor(color, 0.5f);
        fillRect(x, y, w, h);
    }

    public void drawRect(int x, int y, int w, int h)
    {
        fillRect(x, y, w, 1);
        fillRect(x, y, 1, h);
        fillRect(x + w, y, 1, h + 1);
        fillRect(x, y + h, w + 1, 1);
    }

    public void drawRoundRect(int x, int y, int w, int h, int arcW, int arcH)
        => drawRect(x, y, w, h);

    public void fillRoundRect(int x, int y, int w, int h, int arcW, int arcH)
        => fillRect(x, y, w, h);

    public void fillArg(int i, int j, int k, int l, int m, int n)
        => fillRect(i, j, k, l);

    public void fillTrans(Image imgTrans, int x, int y, int w, int h)
    {
        setColor(0, 0.5f);
        fillRect(x * zoomLevel, y * zoomLevel, w * zoomLevel, h * zoomLevel);
    }

    // ============================================================
    //  DRAW LINE — vẽ trực tiếp, không cần shader
    // ============================================================
    public void drawLine(int x1, int y1, int x2, int y2)
    {
        if (canvas == null) return;

        x1 *= zoomLevel; y1 *= zoomLevel;
        x2 *= zoomLevel; y2 *= zoomLevel;

        if (isTranslate)
        {
            x1 += translateX; y1 += translateY;
            x2 += translateX; y2 += translateY;
        }

        if (isClip)
        {
            int cx = clipX + clipTX;
            int cy = clipY + clipTY;
            int xmin = Math.Min(x1, x2);
            int xmax = Math.Max(x1, x2);
            int ymin = Math.Min(y1, y2);
            int ymax = Math.Max(y1, y2);
            if (xmin >= cx + clipW || xmax <= cx ||
                ymin >= cy + clipH || ymax <= cy) return;
        }

        strokePaint.Color = currentColor;
        strokePaint.StrokeWidth = 1;
        canvas.DrawLine(x1, y1, x2, y2, strokePaint);
    }

    // Giữ chữ ký cũ — caller cũ vẫn compile
    public void drawLine(mGraphics g, int x, int y, int xTo, int yTo, int nLine, int color)
    {
        var saved = currentColor;
        setColor(color);
        for (int i = 0; i < nLine; i++)
            drawLine(x, y, xTo + i, yTo + i);
        currentColor = saved;
    }

    public void CreateLineMaterial() { /* Skia không cần */ }

    public void drawlineGL(MyVector totalLine)
    {
        if (canvas == null) { totalLine.removeAllElements(); return; }

        for (int i = 0; i < totalLine.size(); i++)
        {
            mLine line = (mLine)totalLine.elementAt(i);
            byte alpha = (byte)(Math.Min(Math.Max(line.a, 0f), 1f) * 255f);
            strokePaint.Color = new SKColor(
                (byte)Math.Min(255, line.r),
                (byte)Math.Min(255, line.g),
                (byte)Math.Min(255, line.b), alpha);
            strokePaint.StrokeWidth = 1;

            int x1 = line.x1 * zoomLevel;
            int y1 = line.y1 * zoomLevel;
            int x2 = line.x2 * zoomLevel;
            int y2 = line.y2 * zoomLevel;

            if (isTranslate)
            {
                x1 += translateX; y1 += translateY;
                x2 += translateX; y2 += translateY;
            }

            canvas.DrawLine(x1, y1, x2, y2, strokePaint);
        }
        totalLine.removeAllElements();
    }

    // ============================================================
    //  DRAW STRING — thay GUI.Label
    //  Bản Unity nhận GUIStyle, ta chỉ dùng fontSize + currentColor
    // ============================================================
    public void drawString(string s, int x, int y, GUIStyle style)
    {
        if (canvas == null || string.IsNullOrEmpty(s)) return;

        int px = x * zoomLevel;
        int py = y * zoomLevel;

        if (isTranslate) { px += translateX; py += translateY; }

        float fontSize = (style != null && style.fontSize > 0)
            ? style.fontSize * zoomLevel
            : 11f * zoomLevel;

        var paint = TextPaint;
        paint.Color = currentColor;
        paint.TextSize = fontSize;

        float baseline = py + fontSize - paint.FontMetrics.Descent;

        int save = canvas.Save();
        if (isClip)
        {
            int cx = clipX + clipTX;
            int cy = clipY + clipTY;
            canvas.ClipRect(new SKRect(cx, cy, cx + clipW, cy + clipH),
                            SKClipOperation.Intersect);
        }
        canvas.DrawText(s, px, baseline, paint);
        canvas.RestoreToCount(save);
    }

    public void drawString(string s, int x, int y, GUIStyle style, int w)
        => drawString(s, x, y - 4, style);

    // ============================================================
    //  DRAW REGION — trái tim của mọi sprite
    // ============================================================
    public void drawRegion(Image arg0, int x0, int y0, int w0, int h0,
                           int transform, int x, int y, int anchor)
        => DrawRegionCore(arg0, x0, y0, w0, h0, transform, x, y, anchor);

    public void drawRegion(Image arg0, int x0, int y0, int w0, int h0,
                           int transform, float x, float y, int anchor)
        => DrawRegionCore(arg0, x0, y0, w0, h0, transform,
                          (int)x, (int)y, anchor);

    // Overload có cờ isClip — Unity cũ có, caller cũ có thể gọi
    public void drawRegion(Image arg0, int x0, int y0, int w0, int h0,
                           int transform, int x, int y, int anchor, bool isClipFlag)
        => DrawRegionCore(arg0, x0, y0, w0, h0, transform, x, y, anchor);

    private void DrawRegionCore(Image img, int x0, int y0, int w0, int h0,
                                int transform, int x, int y, int anchor)
    {
        if (img == null || img.texture == null) return;

        SKBitmap bmp = img.texture.skBitmap;
        if (bmp == null || canvas == null) return;

        // scale theo zoomLevel
        x  *= zoomLevel;  y  *= zoomLevel;
        x0 *= zoomLevel;  y0 *= zoomLevel;
        w0 *= zoomLevel;  h0 *= zoomLevel;

        if (isTranslate) { x += translateX; y += translateY; }

        // anchor
        int dx = 0, dy = 0;
        if ((anchor & HCENTER) != 0) dx -= w0 / 2;
        if ((anchor & VCENTER) != 0) dy -= h0 / 2;
        if ((anchor & RIGHT)   != 0) dx -= w0;
        if ((anchor & BOTTOM)  != 0) dy -= h0;
        int destX = x + dx;
        int destY = y + dy;

        // kích thước sau khi xoay 90/270 thì đổi chỗ
        int pw = w0, ph = h0;
        if (transform == 4 || transform == 5 || transform == 6 || transform == 7)
        {
            pw = h0; ph = w0;
        }

        // clip check
        if (isClip)
        {
            int cx = clipX + clipTX;
            int cy = clipY + clipTY;
            int ix = Math.Max(destX, cx);
            int iy = Math.Max(destY, cy);
            int iw = Math.Min(destX + pw, cx + clipW) - ix;
            int ih = Math.Min(destY + ph, cy + clipH) - iy;
            if (iw <= 0 || ih <= 0) return;
        }

        // src rect (theo pixel của bitmap gốc)
        int sx = Math.Max(0, x0 / zoomLevel);
        int sy = Math.Max(0, y0 / zoomLevel);
        int sw = Math.Min(w0 / zoomLevel, bmp.Width  - sx);
        int sh = Math.Min(h0 / zoomLevel, bmp.Height - sy);
        if (sw <= 0 || sh <= 0) return;

        // transform
        float scaleX = 1f, scaleY = 1f, rot = 0f;
        switch (transform)
        {
            case 1: scaleX = -1f; rot = 180f; break;
            case 2: scaleX = -1f; break;
            case 3: rot = 180f; break;
            case 4: scaleX = -1f; rot = 270f; break;
            case 5: rot = 90f; break;
            case 6: rot = 270f; break;
            case 7: scaleX = -1f; rot = 90f; break;
        }

        int save = canvas.Save();
        if (isClip)
        {
            float clipScreenX = clipX + clipTX;
            float clipScreenY = clipY + clipTY;
            canvas.ClipRect(new SKRect(clipScreenX, clipScreenY,
                                       clipScreenX + clipW,
                                       clipScreenY + clipH),
                            SKClipOperation.Intersect);
        }

        canvas.Translate(destX + pw / 2f, destY + ph / 2f);
        if (rot != 0f) canvas.RotateDegrees(rot);
        if (scaleX != 1f || scaleY != 1f) canvas.Scale(scaleX, scaleY);

        float dz = zoomLevel;
        float halfW = sw * dz / 2f;
        float halfH = sh * dz / 2f;

        bitmapPaint.Color = new SKColor(
            (byte)Math.Clamp(img.colorBlend.r * 255f, 0, 255),
            (byte)Math.Clamp(img.colorBlend.g * 255f, 0, 255),
            (byte)Math.Clamp(img.colorBlend.b * 255f, 0, 255),
            (byte)Math.Clamp(img.colorBlend.a * 255f, 0, 255));

        canvas.DrawBitmap(bmp,
            new SKRectI(sx, sy, sx + sw, sy + sh),
            new SKRect(-halfW, -halfH, halfW, halfH),
            bitmapPaint);

        canvas.RestoreToCount(save);
    }

    // Overload nội bộ — Unity cũ có, giữ để compile
    public void __drawRegion(Image image, int x0, int y0, int w, int h,
                             int transform, float x, float y, int anchor)
        => DrawRegionCore(image, x0, y0, w, h, transform, (int)x, (int)y, anchor);

    public void _drawRegion(Image image, float x0, float y0, int w, int h,
                            int transform, int x, int y, int anchor)
        => DrawRegionCore(image, (int)x0, (int)y0, w, h, transform, x, y, anchor);

    public void drawRegionGui(Image image, float x0, float y0, int w, int h,
                              int transform, float x, float y, int anchor)
    {
        if (image == null || image.texture == null || canvas == null) return;

        SKBitmap bmp = image.texture.skBitmap;
        if (bmp == null) return;

        x *= zoomLevel; y *= zoomLevel;
        w *= zoomLevel; h *= zoomLevel;
        if (isTranslate) { x += translateX; y += translateY; }

        canvas.DrawBitmap(bmp,
            new SKRectI((int)x0, (int)y0, (int)(x0 + w), (int)(y0 + h)),
            new SKRect(x, y, x + w, y + h));
    }

    public void drawRegion2(Image image, float x0, float y0, int w, int h,
                            int transform, int x, int y, int anchor)
    {
        if (image == null || image.texture == null || canvas == null) return;

        SKBitmap bmp = image.texture.skBitmap;
        if (bmp == null) return;

        if (isTranslate) { x += translateX; y += translateY; }

        int dx = 0, dy = 0;
        if ((anchor & HCENTER) != 0) dx -= w / 2;
        if ((anchor & VCENTER) != 0) dy -= h / 2;
        if ((anchor & RIGHT)   != 0) dx -= w;
        if ((anchor & BOTTOM)  != 0) dy -= h;
        int destX = x + dx;
        int destY = y + dy;

        bitmapPaint.Color = new SKColor(
            (byte)Math.Clamp(image.colorBlend.r * 255f, 0, 255),
            (byte)Math.Clamp(image.colorBlend.g * 255f, 0, 255),
            (byte)Math.Clamp(image.colorBlend.b * 255f, 0, 255),
            (byte)Math.Clamp(image.colorBlend.a * 255f, 0, 255));

        int save = canvas.Save();
        if (isClip)
        {
            int cx = clipX + clipTX;
            int cy = clipY + clipTY;
            canvas.ClipRect(new SKRect(cx, cy, cx + clipW, cy + clipH),
                            SKClipOperation.Intersect);
        }

        canvas.DrawBitmap(bmp,
            new SKRectI((int)x0, (int)y0, (int)(x0 + w), (int)(y0 + h)),
            new SKRect(destX, destY, destX + w, destY + h),
            bitmapPaint);

        canvas.RestoreToCount(save);
    }

    public void drawImagaByDrawTexture(Image image, float x, float y)
    {
        if (image == null || image.texture == null || canvas == null) return;

        SKBitmap bmp = image.texture.skBitmap;
        if (bmp == null) return;

        x *= zoomLevel; y *= zoomLevel;
        int dw = image.getRealImageWidth() * zoomLevel;
        int dh = image.getRealImageHeight() * zoomLevel;

        canvas.DrawBitmap(bmp,
            new SKRect(x + translateX, y + translateY,
                       x + translateX + dw, y + translateY + dh));
    }

    public void drawImage(Image image, int x, int y, int anchor)
    {
        if (image == null) return;
        drawRegion(image, 0, 0, getImageWidth(image), getImageHeight(image),
                   0, x, y, anchor);
    }

    public void drawImage(Image image, int x, int y)
    {
        if (image == null) return;
        drawRegion(image, 0, 0, getImageWidth(image), getImageHeight(image),
                   0, x, y, TOP | LEFT);
    }

    public void drawImage(Image image, float x, float y, int anchor)
    {
        if (image == null) return;
        drawRegion(image, 0, 0, getImageWidth(image), getImageHeight(image),
                   0, x, y, anchor);
    }

    public void drawImageFog(Image image, int x, int y, int anchor)
    {
        if (image == null) return;
        drawRegion(image, 0, 0, image.texture.width, image.texture.height,
                   0, x, y, anchor);
    }

    public void drawImageScale(Image image, int x, int y, int w, int h, int transform)
    {
        if (image == null || image.texture == null || canvas == null) return;

        SKBitmap bmp = image.texture.skBitmap;
        if (bmp == null) return;

        x *= zoomLevel; y *= zoomLevel;
        w *= zoomLevel; h *= zoomLevel;

        int save = canvas.Save();
        if (isClip)
        {
            int cx = clipX + clipTX;
            int cy = clipY + clipTY;
            canvas.ClipRect(new SKRect(cx, cy, cx + clipW, cy + clipH),
                            SKClipOperation.Intersect);
        }

        canvas.Translate(x + translateX, y + translateY);
        if (transform != 0) canvas.Scale(-1f, 1f);

        canvas.DrawBitmap(bmp, new SKRect(0, 0, bmp.Width, bmp.Height));
        canvas.RestoreToCount(save);
    }

    public void drawImageSimple(Image image, int x, int y)
    {
        if (image == null || image.texture == null || canvas == null) return;

        SKBitmap bmp = image.texture.skBitmap;
        if (bmp == null) return;

        x *= zoomLevel; y *= zoomLevel;

        canvas.DrawBitmap(bmp,
            new SKRect(x, y,
                       x + image.w * zoomLevel,
                       y + image.h * zoomLevel));
    }

    // ============================================================
    //  RESET / HELPERS
    // ============================================================
    public void reset()
    {
        isClip = false;
        isTranslate = false;
        translateX = 0;
        translateY = 0;
        translateXf = 0;
        translateYf = 0;
    }

    public Rect intersectRect(Rect r1, Rect r2)
    {
        float x1 = Math.Max(r1.x, r2.x);
        float y1 = Math.Max(r1.y, r2.y);
        float x2 = Math.Min(r1.x + r1.width,  r2.x + r2.width);
        float y2 = Math.Min(r1.y + r1.height, r2.y + r2.height);
        if (x2 < x1) x2 = x1;
        if (y2 < y1) y2 = y1;
        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }

    public static int getImageWidth(Image image)     => image.getWidth();
    public static int getImageHeight(Image image)    => image.getHeight();
    public static int getRealImageWidth(Image img)   => img.w;
    public static int getRealImageHeight(Image img)  => img.h;

    public static bool isNotTranColor(Color color)
    {
        if (color == Color.clear || color == Color.white) return false;
        return true;
    }

    public static int getIntByColor(Color cl)
    {
        float num  = cl.r * 255f;
        float num2 = cl.b * 255f;
        float num3 = cl.g * 255f;
        return (((int)num & 0xFF) << 16) |
               (((int)num3 & 0xFF) << 8)  |
                ((int)num2 & 0xFF);
    }

    public static int blendColor(float level, int color, int colorBlend)
    {
        Color color2 = setColorObj(colorBlend);
        float num  = color2.r * 255f;
        float num2 = color2.g * 255f;
        float num3 = color2.b * 255f;
        Color color3 = setColorObj(color);
        float num4 = (num  + color3.r) * level + color3.r;
        float num5 = (num2 + color3.g) * level + color3.g;
        float num6 = (num3 + color3.b) * level + color3.b;
        num4 = Math.Clamp(num4, 0, 255);
        num5 = Math.Clamp(num5, 0, 255);
        num6 = Math.Clamp(num6, 0, 255);
        return (((int)num4 & 0xFF) << 16) |
               (((int)num5 & 0xFF) << 8)  |
                ((int)num6 & 0xFF);
    }

    // ============================================================
    //  BLEND — duyệt pixel qua SKBitmap (giữ nguyên hành vi)
    // ============================================================
    public static Image blend(Image img0, float level, int rgb)
    {
        if (img0 == null || img0.texture == null) return img0;

        SKBitmap src = img0.texture.skBitmap;
        if (src == null) return img0;

        var c = setColorObj(rgb);
        byte cr = (byte)Math.Clamp(c.r * 255f, 0, 255);
        byte cg = (byte)Math.Clamp(c.g * 255f, 0, 255);
        byte cb = (byte)Math.Clamp(c.b * 255f, 0, 255);

        var dst = new SKBitmap(src.Info);
        for (int yy = 0; yy < src.Height; yy++)
        {
            for (int xx = 0; xx < src.Width; xx++)
            {
                var p = src.GetPixel(xx, yy);
                if (p.Alpha == 0) { dst.SetPixel(xx, yy, SKColors.Transparent); continue; }

                byte nr = (byte)Math.Clamp(p.Red   + (cr - p.Red)   * level, 0, 255);
                byte ng = (byte)Math.Clamp(p.Green + (cg - p.Green) * level, 0, 255);
                byte nb = (byte)Math.Clamp(p.Blue  + (cb - p.Blue)  * level, 0, 255);
                dst.SetPixel(xx, yy, new SKColor(nr, ng, nb, p.Alpha));
            }
        }

        return Image.FromSKBitmap(dst, img0.w, img0.h);
    }

    // ============================================================
    //  STUB — giữ để code cũ compile
    // ============================================================
    internal void drawRegion(Small img, int p1, int p2, int p3, int p4,
                             int transform, int x, int y, int anchor)
    {
        // Small là kiểu khác — nếu cần thì bạn gửi Small.cs mình port tiếp
        throw new NotImplementedException();
    }
}