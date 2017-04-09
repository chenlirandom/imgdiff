using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace imgdiff
{
    /// <summary>
    /// Interaction logic for ImageDiff.xaml
    /// </summary>
    public partial class ImageDiff : UserControl
    {
        struct Bgra32Pixel
        {
            public byte B;
            public byte G;
            public byte R;
            public byte A;
            public static Bgra32Pixel GetDiff(Bgra32Pixel a, Bgra32Pixel b, int tolerance)
            {
                var diff = new Bgra32Pixel
                {
                    B = (byte)Math.Abs(a.B - b.B),
                    G = (byte)Math.Abs(a.G - b.G),
                    R = (byte)Math.Abs(a.R - b.R),
                    A = (byte)Math.Abs(a.A - b.A),
                };
                if (diff.B <= tolerance) diff.B = 0;
                if (diff.G <= tolerance) diff.G = 0;
                if (diff.R <= tolerance) diff.R = 0;
                if (diff.A <= tolerance) diff.A = 0;
                return diff;
            }
        };

        class Image2D
        {
            public Bgra32Pixel[] Pixels { get; private set; }
            public int W { get; private set; }
            public int H { get; private set; }

            public Image2D(int w, int h)
            {
                if (w <= 0) w = 1;
                if (h <= 0) h = 1;
                this.Pixels = new Bgra32Pixel[w * h];
                this.W = w;
                this.H = h;
            }

            public void Resize(int width, int height)
            {
                if (width <= 0) width = 1;
                if (height <= 0) height = 1;
                if (width == this.W && height == this.H) return;

                var buffer = new Bgra32Pixel[width * height];

                int minW = Math.Min(this.W, width);
                int minH = Math.Min(this.H, height);
                for (int y = 0; y < minH; ++y)
                {
                    for (int x = 0; x < minW; ++x)
                    {
                        int src = y * this.W + x;
                        int dst = y * width + x;
                        buffer[dst] = this.Pixels[src];
                    }
                }
                this.Pixels = buffer;
                this.W = width;
                this.H = height;
            }

            public static Image2D CreateDiff(Image2D a, Image2D b, int tolerance)
            {
                Debug.Assert(a.W == b.W && a.H == b.H);
                if (tolerance < 0) tolerance = 0;
                if (tolerance > 255) tolerance = 255;
                var diff = new Image2D(a.W, a.H);
                for(int i = 0; i < a.Pixels.Length; ++i)
                {
                    diff.Pixels[i] = Bgra32Pixel.GetDiff(a.Pixels[i], b.Pixels[i], tolerance);
                }
                return diff;
            }
        };

        public ImageDiff()
        {
            InitializeComponent();
        }

        public void SetImages(BitmapSource leftImage, BitmapSource rightImage, int tolerance)
        {
            // check tolerance
            if (tolerance < 0 || tolerance > 255)
            {
                ErrorLogLine("Tolerance must be integer from 0 to 255");
                return;
            }

            // Load images
            var image1 = GetImage2D(leftImage);
            var image2 = GetImage2D(rightImage);
            if (null == image1 || null == image2) return;

            // create diff
            int w = Math.Max(image1.W, image2.W);
            int h = Math.Max(image1.H, image2.H);
            image1.Resize(w, h);
            image2.Resize(w, h);
            var diff = Image2D.CreateDiff(image1, image2, tolerance);
        }

        Image2D GetImage2D(BitmapSource source)
        {
            source = ConvertToBGRA32(source);
            if (null == source) return null;
            var image = new Image2D(source.PixelWidth, source.PixelHeight);
            source.CopyPixels(image.Pixels, source.PixelWidth, 0);
            return image;
        }

        BitmapSource ConvertToBGRA32(BitmapSource source)
        {
            if (null == source)
            {
                return new WriteableBitmap(1, 1, 72, 72, PixelFormats.Bgra32, null);
            }

            if (source.Format == PixelFormats.Bgra32)
            {
                return source;
            }

            try
            {
                var fc = new FormatConvertedBitmap();
                fc.BeginInit();
                fc.Source = source;
                fc.DestinationFormat = PixelFormats.Bgra32;
                fc.EndInit();
                return fc;
            }
            catch(Exception ex)
            {
                ErrorLogLine(ex.ToString());
                return null;
            }
        }

        void ErrorLogLine(string message)
        {
            (Application.Current.MainWindow as MainWindow).ErrorLogLine(message);
        }
    }
}
