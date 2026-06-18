using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace OtcDataService.Views;

internal static class WindowIconFactory
{
    private const string ExitIconGeometry =
        "M9,3 L9,8 L4,8 L4,18 L14,18 L14,8 L11,8 L11,3 Z M11,3 L14,6 L11,6 Z M16,10 L20,13 L16,16 L16,14.5 L12,14.5 L12,11.5 L16,11.5 Z";

    public static WindowIcon Create()
    {
        const int size = 32;

        var visual = new Canvas
        {
            Width = size,
            Height = size,
            Children =
            {
                new Avalonia.Controls.Shapes.Path
                {
                    Data = StreamGeometry.Parse(ExitIconGeometry),
                    Fill = new SolidColorBrush(Color.FromRgb(0x1A, 0x6F, 0xD4)),
                    Width = size - 4,
                    Height = size - 4,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(2)
                }
            }
        };

        visual.Measure(new Size(size, size));
        visual.Arrange(new Rect(0, 0, size, size));

        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        bitmap.Render(visual);

        var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        return new WindowIcon(stream);
    }
}
