using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace InternetMonitor.Infrastructure.Helpers;

public static class IconGenerator
{
    public static void GenerateAppIcon(string outputPath)
    {
        // 256x256 ikon oluştur
        using var bitmap = new Bitmap(256, 256);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Arka plan dairesi (koyu mavi)
        using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        g.FillEllipse(bgBrush, 10, 10, 236, 236);

        // Dış halka (mavi)
        using var ringPen = new Pen(Color.FromArgb(0, 122, 204), 8);
        g.DrawEllipse(ringPen, 30, 30, 196, 196);

        // WiFi sinyal çizgileri
        using var signalPen = new Pen(Color.FromArgb(76, 175, 80), 6);
        signalPen.StartCap = LineCap.Round;
        signalPen.EndCap = LineCap.Round;

        // Sinyal yayları
        g.DrawArc(signalPen, 78, 78, 100, 100, 225, 90);
        g.DrawArc(signalPen, 98, 98, 60, 60, 225, 90);

        // Merkez nokta
        using var centerBrush = new SolidBrush(Color.FromArgb(76, 175, 80));
        g.FillEllipse(centerBrush, 118, 118, 20, 20);

        // Pulse efekti (dış halka)
        using var pulsePen = new Pen(Color.FromArgb(100, 76, 175, 80), 3);
        g.DrawEllipse(pulsePen, 50, 50, 156, 156);

        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    public static void GenerateIcoFile(string pngPath, string icoPath)
    {
        using var png = new Bitmap(pngPath);

        // Farklı boyutlarda ikonlar
        var sizes = new[] { 16, 32, 48, 64, 128, 256 };

        using var fs = new FileStream(icoPath, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // ICO header
        bw.Write((short)0);      // Reserved
        bw.Write((short)1);      // Type (1 = ICO)
        bw.Write((short)sizes.Length); // Number of images

        var imageDataList = new List<byte[]>();
        var offset = 6 + (sizes.Length * 16); // Header + directory entries

        // Directory entries
        foreach (var size in sizes)
        {
            using var resized = new Bitmap(png, size, size);
            using var ms = new MemoryStream();
            resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var imageData = ms.ToArray();
            imageDataList.Add(imageData);

            bw.Write((byte)(size == 256 ? 0 : size)); // Width
            bw.Write((byte)(size == 256 ? 0 : size)); // Height
            bw.Write((byte)0);    // Color palette
            bw.Write((byte)0);    // Reserved
            bw.Write((short)1);   // Color planes
            bw.Write((short)32);  // Bits per pixel
            bw.Write(imageData.Length); // Image size
            bw.Write(offset);     // Image offset
            offset += imageData.Length;
        }

        // Image data
        foreach (var imageData in imageDataList)
        {
            bw.Write(imageData);
        }
    }
}
