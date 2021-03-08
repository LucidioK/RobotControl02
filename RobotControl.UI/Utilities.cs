using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RobotControl.UI
{
    static class Utilities
    {
        public static BitmapImage BitmapToBitmapImage(System.Drawing.Bitmap src)
        {
            MemoryStream ms = new MemoryStream();
            ((System.Drawing.Bitmap)src).Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();
            return image;
        }

        public static void UpdateImage(System.Windows.Controls.Image image, System.Drawing.Bitmap bitmap)
            => image.Dispatcher.Invoke(() => image.Source = BitmapToBitmapImage(bitmap));
    }
}
