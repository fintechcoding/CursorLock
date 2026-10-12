using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace CursorLock;
internal static class TrayIconFactory
{
    public static Icon Create(bool locked)
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var background = new SolidBrush(Color.FromArgb(25, 30, 42));
        graphics.FillEllipse(background, 1, 1, 62, 62);
        using var pen = new Pen(locked ? Color.FromArgb(131, 232, 202) : Color.FromArgb(210, 221, 241), 4);
        graphics.DrawRoundedRectangle(pen, new Rectangle(12, 14, 40, 29), 5);
        graphics.DrawLine(pen, 25, 50, 39, 50);
        graphics.DrawLine(pen, 32, 43, 32, 50);
        if (locked)
        {
            using var fill = new SolidBrush(Color.FromArgb(131, 232, 202));
            graphics.FillRectangle(fill, 26, 27, 13, 11);
            graphics.DrawArc(pen, 28, 20, 9, 11, 180, 180);
        }
        else
        {
            using var fill = new SolidBrush(Color.FromArgb(210, 221, 241));
            graphics.FillPolygon(fill, new[] { new Point(27, 23), new Point(27, 37), new Point(38, 31) });
        }
        IntPtr handle = bitmap.GetHicon();
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { NativeMethods.DestroyIcon(handle); }
    }
    private static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle rectangle, int radius)
    {
        using var path = new GraphicsPath();
        int diameter = radius * 2;
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.DrawPath(pen, path);
    }
}
