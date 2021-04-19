namespace RobotControl.UI
{
    using System.IO;
    using System.Windows.Media.Imaging;

    static class Utilities
    {
        public static BitmapImage BitmapToBitmapImage(System.Drawing.Bitmap src)
        {
            var ms = new MemoryStream();
            var im = new BitmapImage();
            src.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            im.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            im.StreamSource = ms;
            im.EndInit();
            return im;
        }

        public static void UpdateImage(System.Windows.Controls.Image image, System.Drawing.Bitmap bitmap)
            => image.Dispatcher.Invoke(() => image.Source = BitmapToBitmapImage(bitmap));
    }
}
