using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;

namespace RobotControl.UI
{
    class TimeChart
    {
        class FloatColor
        {
            public float Value;
            public Color Color;
        }

        System.Windows.Controls.Image chartImage;
        float minimum;
        float maximum;
        float scaleFactor;
        float zeroY;
        TimeSpan updateInterval;
        ConcurrentQueue<FloatColor> timeValues = new ConcurrentQueue<FloatColor>();
        DateTime latestInput = DateTime.Now.AddHours(-2);
        object postLock = new object();

        public TimeChart(
            System.Windows.Controls.Image chartImage,
            float minimum,
            float maximum,
            TimeSpan updateInterval)
        {
            this.chartImage = chartImage;
            this.minimum = minimum;
            this.maximum = maximum;
            this.updateInterval = updateInterval;
            this.scaleFactor = (float)(this.chartImage.Height / (maximum - minimum));
            this.zeroY = ((maximum - minimum) / 2) * this.scaleFactor;
        }

        public void Post(float value, Color color)
        {
            lock (postLock)
            {
                if (DateTime.Now - latestInput > updateInterval)
                {
                    latestInput = DateTime.Now;
                    if (timeValues.Count > chartImage.Width)
                    {
                        timeValues.TryDequeue(out FloatColor dequeuedValue);
                    }

                    timeValues.Enqueue(new FloatColor { Value = value, Color = color });

                    var bitmap = new Bitmap((int)chartImage.Width, (int)chartImage.Height);
                    bitmap.MakeTransparent();

                    var points = new PointF[2];
                    using (var gr = Graphics.FromImage(bitmap))
                    {
                        for (int x = 0; x < timeValues.Count; x++)
                        {
                            var fc = timeValues.ElementAt(x);
                            float y = (fc.Value * scaleFactor) + zeroY;
                            points[0] = points[1] = new PointF((float)x, y);
                            points[1].X += 0.5f;
                            gr.DrawCurve(new Pen(fc.Color, 1), points);
                        }
                    }

                    Utilities.UpdateImage(chartImage, bitmap);
                }
            }
        }

    }
}
