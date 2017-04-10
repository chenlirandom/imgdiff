using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;

namespace imgdiff
{
    /// <summary>
    /// Interaction logic for ImageDiff.xaml
    /// </summary>
    public partial class ImageDiff : UserControl
    {
        class Bgra32Image
        {
            Task genDiffTask;

            public ImageSource ImageSource { get; private set; }
            public uint[] Pixels { get; private set; }
            public int W { get; private set; }
            public int H { get; private set; }

            public event EventHandler<int> GenDiffComplete;

            public Bgra32Image(BitmapSource source, int w, int h)
            {
                Debug.Assert(null == source || (source.PixelWidth <= w && source.PixelHeight <= h));
                this.Pixels = new uint[w * h];
                this.W = w;
                this.H = h;
                var bmp = new WriteableBitmap(this.W, this.H, 96, 96, PixelFormats.Bgra32, null);
                this.ImageSource = bmp;
                if (null != source)
                {
                    source.CopyPixels(this.Pixels, w*4, 0);
                    bmp.WritePixels(new Int32Rect(0, 0, this.W, this.H), this.Pixels, this.W * 4, 0);
                }
            }

            public void RefreshDiffAsync(Bgra32Image a, Bgra32Image b, int tolerance)
            {
                // do some basic check
                Debug.Assert(a.W == b.W && a.H == b.H);
                Debug.Assert(a.W == this.W && a.H == this.H);
                if (tolerance < 0) tolerance = 0;
                if (tolerance > 255) tolerance = 255;

                // create a new diff task
                Task task = null;
                task = new Task((object state) =>
                {
                    uint[] diff   = new uint[this.Pixels.Length];
                    uint[] color = new uint[this.Pixels.Length];
                    int taskTolerance = (int)state;
                    for (int i = 0; i < a.Pixels.Length; ++i)
                    {
                        if (task != this.genDiffTask)
                        {
                            return;
                        }
                        uint actualDiff, diffColor;
                        GetDiff(out actualDiff, out diffColor, a.Pixels[i], b.Pixels[i], taskTolerance);
                        diff[i] = actualDiff;
                        color[i] = diffColor;
                    }
                    Application.Current.Dispatcher.BeginInvoke((Action)(() => 
                    {
                        if (this.genDiffTask == task)
                        {
                            // Update member variables, trigger events, when the task is done.
                            this.Pixels = diff;
                            Debug.Assert(this.ImageSource is WriteableBitmap);
                            (this.ImageSource as WriteableBitmap).WritePixels(new Int32Rect(0, 0, this.W, this.H), color, this.W * 4, 0);
                            this.GenDiffComplete(this, taskTolerance);
                        }
                    }));
                }, tolerance);
                this.genDiffTask = task;
                task.Start();
            }

            public string GetPixelValueText(int x, int y)
            {
                if (x < 0 || x >= this.W || y < 0 || y >= this.H)
                {
                    return "out of range";
                }
                else
                {
                    uint pixel = this.Pixels[y * this.W + x];
                    return string.Format("R={0,-3} G={1,-3} B={2,-3}", R(pixel), G(pixel), B(pixel));
                }
            }

            public static Bgra32Image CreateDiff(Bgra32Image a, Bgra32Image b, int tolerance)
            {
                Debug.Assert(a.W == b.W && a.H == b.H);
                var diff = new Bgra32Image(null, a.W, a.H);
                diff.RefreshDiffAsync(a, b, tolerance);
                return diff;
            }

            static uint R(uint pixel) { return (pixel >> 16) & 0xFF; }
            static uint G(uint pixel) { return (pixel >> 8) & 0xFF; }
            static uint B(uint pixel) { return pixel & 0xFF; }
            static uint Abs(uint a, uint b) { return a > b ? (a - b) : (b - a); }

            static void GetDiff(out uint actualDiff, out uint diffColor, uint a, uint b, int tolerance)
            {
                actualDiff = 0xFF000000;
                diffColor = 0xFF000000;

                uint diffB = Abs(B(a), B(b));
                if (diffB > tolerance)
                {
                    diffColor |= 0xFF;
                }
                actualDiff |= diffB;

                uint diffG = Abs(G(a), G(b));
                if (diffG > tolerance)
                {
                    diffColor |= 0xFF00;
                }
                actualDiff |= diffG << 8;

                uint diffR = Abs(R(a), R(b));
                if (diffR > tolerance)
                {
                    diffColor |= 0xFF0000;
                }
                actualDiff |= diffR << 16;
            }
        };

        enum MainImageType
        {
            LEFT, DIFF, RIGHT,
        };

        Bgra32Image leftImage, rightImage, diffImage;
        int tolerance = -1;
        MainImageType mainImageType;

        public ImageDiff()
        {
            InitializeComponent();
            this.mainImage.MouseMove += (s, e) => { UpdatePixelUnderCursor(); };
            this.Unloaded += (s, e) => { if (null != this.diffImage) this.diffImage.GenDiffComplete -= DiffImage_GenDiffComplete; };
        }

        public void SetImages(BitmapSource left, BitmapSource right, int tolerance)
        {
            // check tolerance
            if (tolerance < 0 || tolerance > 255)
            {
                ErrorLogLine("Tolerance must be integer from 0 to 255");
                return;
            }

            // Load images
            int w = Math.Max(left.PixelWidth, right.PixelWidth);
            int h = Math.Max(left.PixelHeight, right.PixelHeight);
            var image1 = GetImage2D(left, w, h);
            var image2 = GetImage2D(right, w, h);
            var imaged = new Bgra32Image(null, w, h);
            if (null == image1 || null == image2) return;

            // done
            this.leftImage = image1;
            this.rightImage = image2;
            if (null != this.diffImage) this.diffImage.GenDiffComplete -= DiffImage_GenDiffComplete;
            this.diffImage = new Bgra32Image(null, w, h);
            if (null != this.diffImage) this.diffImage.GenDiffComplete += DiffImage_GenDiffComplete;
            this.leftThumbnail.Source = this.leftImage.ImageSource;
            this.rightThumbnail.Source = this.rightImage.ImageSource;
            this.diffThumbnail.Source = this.diffImage.ImageSource;
            SwitchMainImage(MainImageType.DIFF);
            RefreshDiffImage(tolerance);
        }

        void RefreshDiffImage(int tolerance)
        {
            if (null == this.leftImage || null == this.rightImage) return;
            if (tolerance == this.tolerance) return;
            this.tolerance = tolerance;
            this.toleranceTextBlock.Text = string.Format("{0,-3}", tolerance);
            this.diffImage.RefreshDiffAsync(this.leftImage, this.rightImage, tolerance);
        }

        bool updatingSlider = false;
        void DiffImage_GenDiffComplete(object sender, int tolerance)
        {
            try
            {
                this.updatingSlider = true;
                this.toleranceSlider.Value = tolerance;
            }
            finally
            {
                this.updatingSlider = false;
            }
        }

        void SwitchMainImage(MainImageType type)
        {
            var selected = Brushes.Red;
            var unselected = Brushes.DarkGray;
            switch(type)
            {
                case MainImageType.LEFT:
                    this.mainImage.Source = this.leftImage.ImageSource;
                    this.leftButton.BorderBrush = selected;
                    this.rightButton.BorderBrush = unselected;
                    this.diffButton.BorderBrush = unselected;
                    break;

                case MainImageType.RIGHT:
                    this.mainImage.Source = this.rightImage.ImageSource;
                    this.leftButton.BorderBrush = unselected;
                    this.rightButton.BorderBrush = selected;
                    this.diffButton.BorderBrush = unselected;
                    break;

                default:
                    this.mainImage.Source = this.diffImage.ImageSource;
                    this.leftButton.BorderBrush = unselected;
                    this.rightButton.BorderBrush = unselected;
                    this.diffButton.BorderBrush = selected;
                    break;
            }
            this.mainImageType = type;
        }

        void UpdatePixelUnderCursor()
        {
            var pos = Mouse.GetPosition(this.mainImage);
            int x = (this.mainImage.ActualWidth <= 0) ? 0 : (int)(pos.X * this.mainImage.Source.Width / this.mainImage.ActualWidth);
            int y = (this.mainImage.ActualHeight <= 0) ? 0 : (int)(pos.Y * this.mainImage.Source.Height / this.mainImage.ActualHeight);
            this.leftPixelValueText.Text = this.leftImage.GetPixelValueText(x, y);
            this.rightPixelValueText.Text = this.rightImage.GetPixelValueText(x, y);
            this.diffPixelValueText.Text = this.diffImage.GetPixelValueText(x, y);
            this.cursorPositionTextBlock.Text = string.Format("[{0,-3}, {1,-3}]", x, y);
        }

        Bgra32Image GetImage2D(BitmapSource source, int w, int h)
        {
            source = ConvertToBGRA32(source);
            if (null == source) return null;
            return new Bgra32Image(source, w, h);
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

        void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.updatingSlider)
            {
                RefreshDiffImage((int)e.NewValue);
            }
        }

        void Button_Click(object sender, RoutedEventArgs e)
        {
            MainImageType type;
            if (sender == this.leftButton)
            {
                type = MainImageType.LEFT;
            }
            else if (sender == this.rightButton)
            {
                type = MainImageType.RIGHT;
            }
            else
            {
                type = MainImageType.DIFF;
            }
            SwitchMainImage(type);
        }
    }
}
