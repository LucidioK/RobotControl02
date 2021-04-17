using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Threading.Tasks;

namespace RobotControl.ClassLibrary
{
    internal class ImageRecognitionFromCamera : IImageRecognitionFromCamera
    {
        private readonly ImageRecognitionFromCameraParameters parameters;

        private TinyYoloModel tinyYoloModel;
        private OnnxModelConfigurator onnxModelConfigurator;
        private OnnxOutputParser onnxOutputParser;
        private PredictionEngine<ImageInputData, TinyYoloPrediction> tinyYoloPredictionEngine;
        private VideoCapture videoCapture;
        private string[] labelsOfObjectsToDetect;
        private bool flipY;

        public ImageRecognitionFromCamera(ImageRecognitionFromCameraParameters parameters)
        {
            this.parameters = parameters;
        }


        public async Task StartAsync() => await Task.Run(() =>
        {
            labelsOfObjectsToDetect = parameters.LabelsOfObjectsToDetect;

            tinyYoloModel = new TinyYoloModel(parameters.OnnxFilePath);
            onnxModelConfigurator = new OnnxModelConfigurator(tinyYoloModel);
            onnxOutputParser = new OnnxOutputParser(tinyYoloModel);
            tinyYoloPredictionEngine = onnxModelConfigurator.GetMlNetPredictionEngine<TinyYoloPrediction>();
            OpenVideoCapture();
        });

        private void OpenVideoCapture()
        {
            if (string.IsNullOrEmpty(parameters.CameraUrl))
            {
                videoCapture = new VideoCapture(parameters.CameraId);
                videoCapture.Open(parameters.CameraId);
                flipY = true;
            }
            else
            {
                videoCapture = new VideoCapture(parameters.CameraUrl);
                videoCapture.Open(parameters.CameraUrl);
                flipY = false;
            }
        }

        public async Task<ImageRecognitionFromCameraResult> GetAsync() => await Task.Run(() =>
        {
            var frame = new Mat();
            var result = new ImageRecognitionFromCameraResult
            {
                HasData = false,
                ImageRecognitionFromCamera = this,
            };

            for (int i = 0; i < 8 && frame.Rows <= 0; i++)
            {
                videoCapture.Read(frame);
            }

            if (frame.Rows <= 0)
            {
                return result;
            }

            if (flipY) { frame.Flip(FlipMode.Y);  }
            result.Bitmap = BitmapConverter.ToBitmap(frame);
            var prediction = tinyYoloPredictionEngine.Predict(new ImageInputData { Image = result.Bitmap });
            var labels = prediction.PredictedLabels;
            var boundingBoxes = onnxOutputParser.ParseOutputs(labels);
            var filteredBoxes = onnxOutputParser.FilterBoundingBoxes(boundingBoxes, 5, 0.5f);
            if (filteredBoxes.Count == 0)
            {
                return result;
            }

            filteredBoxes = filteredBoxes.Where(b => labelsOfObjectsToDetect.Any(l => l.Equals(b.Label, StringComparison.InvariantCultureIgnoreCase))).ToList();
            if (filteredBoxes.Count == 0)
            {
                return result;
            }

            var highestConfidence = filteredBoxes.Select(b => b.Confidence).Max();
            var highestConfidenceBox = filteredBoxes.First(b => b.Confidence == highestConfidence);
            HighlightDetectedObject(result.Bitmap, highestConfidenceBox);
            var bbdfb = BoundingBoxDeltaFromBitmap.FromBitmap(result.Bitmap, highestConfidenceBox);
            result.HasData = true;

            result.XDeltaProportionFromBitmapCenter = bbdfb.XDeltaProportionFromBitmapCenter;
            result.Label = highestConfidenceBox.Label;

            return result;
        });

        public void Dispose()
        {
            videoCapture?.Dispose();
            tinyYoloPredictionEngine?.Dispose();
        }

        private static void HighlightDetectedObject(Bitmap bitmap, BoundingBox box)
        {
            var bbdfb = BoundingBoxDeltaFromBitmap.FromBitmap(bitmap, box);

            var x = box.Dimensions.X * bbdfb.CorrX;
            var y = box.Dimensions.Y * bbdfb.CorrY;
            var w = box.Dimensions.Width * bbdfb.CorrX;
            var h = box.Dimensions.Height * bbdfb.CorrY;
            var midX = x + (w / 2);
            var midY = y + (h / 2);
            if (w != 0)
            {
                using (var gr = Graphics.FromImage(bitmap))
                {
                    gr.DrawRectangle(new Pen(Color.Red, 3), x, y, w, h);
                    gr.DrawLine(new Pen(Color.Green, 2), 0, midY, bitmap.Width - 1, midY);
                    gr.DrawLine(new Pen(Color.Green, 2), midX, 0, midX, bitmap.Height - 1);
                }
            }
        }

        #region ONNXImplementation
        public class BoundingBoxDimensions
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Height { get; set; }
            public float Width { get; set; }
        }

        public class BoundingBoxDeltaFromBitmap
        {
            private BoundingBoxDeltaFromBitmap() { }

            public float CorrX { get; private set; }
            public float CorrY { get; private set; }
            public float BitmapWidth { get; private set; }
            public float BitmapHeight { get; private set; }
            public float XDeltaFromBitmapCenter { get; private set; }
            public float YDeltaFromBitmapCenter { get; private set; }
            public float XDeltaProportionFromBitmapCenter { get => BitmapWidth > 0 ? XDeltaFromBitmapCenter / BitmapWidth : 0; }
            public float YDeltaProportionFromBitmapCenter { get => BitmapHeight > 0 ? YDeltaFromBitmapCenter / BitmapHeight : 0; }

            public static BoundingBoxDeltaFromBitmap FromBitmap(Bitmap bitmap, BoundingBox box)
            {
                var bbdfb = new BoundingBoxDeltaFromBitmap()
                {
                    BitmapWidth = R0(bitmap.Width),
                    BitmapHeight = R0(bitmap.Height),
                    CorrX = (float)bitmap.Width / ImageSettings.imageWidth,
                    CorrY = (float)bitmap.Height / ImageSettings.imageHeight,
                };

                var midXImg = bitmap.Width / 2;
                var midXBox = (box.Dimensions.X * bbdfb.CorrX) + (box.Dimensions.Width * bbdfb.CorrX / 2);
                bbdfb.XDeltaFromBitmapCenter = R1(midXBox - midXImg);

                var midYImg = bitmap.Height / 2;
                var midYBox = (box.Dimensions.Y * bbdfb.CorrY) + (box.Dimensions.Height * bbdfb.CorrY / 2);
                bbdfb.YDeltaFromBitmapCenter = R1(midYBox - midYImg);
                bbdfb.CorrX = R1(bbdfb.CorrX);
                bbdfb.CorrY = R1(bbdfb.CorrY);
                return bbdfb;
            }

            public static float R0(float n) => Round(n, 0);
            public static float R1(float n) => Round(n, 1);
            public static float Round(float n, int decimals) => (float)Math.Round((double)n, decimals);
        }

        public class BoundingBox
        {
            public BoundingBoxDimensions Dimensions { get; set; }

            public string Label { get; set; }

            public float Confidence { get; set; }

            public Color BoxColor { get; set; }

            public RectangleF Rect => new RectangleF(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height);

            public string Description => $"{Label} ({Confidence * 100:0}%)";
        }

        private struct ImageSettings
        {
            public const int imageHeight = 416;
            public const int imageWidth = 416;
        }

        private class ImageInputData
        {
            [ImageType(ImageSettings.imageHeight, ImageSettings.imageWidth)]
            public Bitmap Image { get; set; }
        }

        private interface IOnnxModel
        {
            string ModelPath { get; }
            string ModelInput { get; }
            string ModelOutput { get; }

            string[] Labels { get; }
            (float, float)[] Anchors { get; }
        }

        private interface IOnnxObjectPrediction
        {
            float[] PredictedLabels { get; set; }
        }

        private class OnnxModelConfigurator
        {
            private readonly MLContext mlContext = new MLContext();
            private readonly ITransformer mlModel;

            public OnnxModelConfigurator(IOnnxModel onnxModel) => mlModel = SetupMlNetModel(onnxModel);

            private ITransformer SetupMlNetModel(IOnnxModel onnxModel) =>
                mlContext
                    .Transforms
                        .ResizeImages(
                            resizing: ImageResizingEstimator.ResizingKind.Fill,
                            outputColumnName: onnxModel.ModelInput,
                            imageWidth: ImageSettings.imageWidth,
                            imageHeight: ImageSettings.imageHeight,
                            inputColumnName: nameof(ImageInputData.Image))
                        .Append(mlContext.Transforms.ExtractPixels(
                            outputColumnName: onnxModel.ModelInput))
                        .Append(mlContext.Transforms.ApplyOnnxModel(
                            modelFile: onnxModel.ModelPath,
                            outputColumnName: onnxModel.ModelOutput,
                            inputColumnName: onnxModel.ModelInput/*,
                            gpuDeviceId: 0,
                            fallbackToCpu: true*/))
                        .Fit(
                            mlContext.Data.LoadFromEnumerable(new List<ImageInputData>()));

            public PredictionEngine<ImageInputData, T> GetMlNetPredictionEngine<T>()
                where T : class, IOnnxObjectPrediction, new() => mlContext.Model.CreatePredictionEngine<ImageInputData, T>(mlModel);

            public void SaveMLNetModel(string mlnetModelFilePath) => mlContext.Model.Save(mlModel, null, mlnetModelFilePath);
        }

        private class OnnxOutputParser
        {
            class BoundingBoxPrediction : BoundingBoxDimensions
            {
                public float Confidence { get; set; }
            }

            // The number of rows and columns in the grid the image is divided into.
            public const int ROW_COUNT = 13, COLUMN_COUNT = 13;

            // The number of features contained within a box (x, y, height, width, confidence).
            public const int FEATURES_PER_BOX = 5;

            // Labels corresponding to the classes the onnx model can predict. For example, the
            // Tiny YOLOv2 model included with this sample is trained to predict 20 different classes.
            private readonly string[] classLabels;

            // Predetermined anchor offsets for the bounding boxes in a cell.
            private readonly (float x, float y)[] boxAnchors;


            public OnnxOutputParser(IOnnxModel onnxModel)
            {
                classLabels = onnxModel.Labels;
                boxAnchors = onnxModel.Anchors;
            }

            // Applies the sigmoid function that outputs a number between 0 and 1.
            private float Sigmoid(float value)
            {
                var k = (float)Math.Exp(value);
                return k / (1.0f + k);
            }

            // Normalizes an input vector into a probability distribution.
            private float[] Softmax(float[] classProbabilities)
            {
                var max = classProbabilities.Max();
                var exp = classProbabilities.Select(v => (float)Math.Exp(v - max));
                var sum = exp.Sum();
                return exp.Select(v => v / sum).ToArray();
            }

            // Onnx outputst a tensor that has a shape of (for Tiny YOLOv2) 125x13x13. ML.NET flattens
            // this multi-dimensional into a one-dimensional array. This method allows us to access a
            // specific channel for a givin (x,y) cell position by calculating the offset into the array.
            private int GetOffset(int row, int column, int channel) => (channel * ROW_COUNT * COLUMN_COUNT) + (column * COLUMN_COUNT) + row;

            // Extracts the bounding box features (x, y, height, width, confidence) method from the model
            // output. The confidence value states how sure the model is that it has detected an object.
            // We use the Sigmoid function to turn it that confidence into a percentage.
            private BoundingBoxPrediction ExtractBoundingBoxPrediction(float[] modelOutput, int row, int column, int channel) =>
                new BoundingBoxPrediction
                {
                    X = modelOutput[GetOffset(row, column, channel++)],
                    Y = modelOutput[GetOffset(row, column, channel++)],
                    Width = modelOutput[GetOffset(row, column, channel++)],
                    Height = modelOutput[GetOffset(row, column, channel++)],
                    Confidence = Sigmoid(modelOutput[GetOffset(row, column, channel++)])
                };

            // The predicted x and y coordinates are relative to the location of the grid cell; we use
            // the logistic sigmoid to constrain these coordinates to the range 0 - 1. Then we add the
            // cell coordinates (0-12) and multiply by the number of pixels per grid cell (32).
            // Now x/y represent the center of the bounding box in the original 416x416 image space.
            // Additionally, the size (width, height) of the bounding box is predicted relative to the
            // size of an "anchor" box. So we transform the width/weight into the original 416x416 image space.
            private BoundingBoxDimensions MapBoundingBoxToCell(int row, int column, int box, BoundingBoxPrediction boxDimensions)
            {
                const float cellWidth = ImageSettings.imageWidth / COLUMN_COUNT;
                const float cellHeight = ImageSettings.imageHeight / ROW_COUNT;

                var mappedBox = new BoundingBoxDimensions
                {
                    X = (row + Sigmoid(boxDimensions.X)) * cellWidth,
                    Y = (column + Sigmoid(boxDimensions.Y)) * cellHeight,
                    Width = (float)Math.Exp(boxDimensions.Width) * cellWidth * boxAnchors[box].x,
                    Height = (float)Math.Exp(boxDimensions.Height) * cellHeight * boxAnchors[box].y,
                };

                // The x,y coordinates from the (mapped) bounding box prediction represent the center
                // of the bounding box. We adjust them here to represent the top left corner.
                mappedBox.X -= mappedBox.Width / 2;
                mappedBox.Y -= mappedBox.Height / 2;

                return mappedBox;
            }

            // Extracts the class predictions for the bounding box from the model output using the
            // GetOffset method and turns them into a probability distribution using the Softmax method.
            public float[] ExtractClassProbabilities(float[] modelOutput, int row, int column, int channel, float confidence)
            {
                var classProbabilitiesOffset = channel + FEATURES_PER_BOX;
                float[] classProbabilities = new float[classLabels.Length];
                for (int classProbability = 0; classProbability < classLabels.Length; classProbability++)
                    classProbabilities[classProbability] = modelOutput[GetOffset(row, column, classProbability + classProbabilitiesOffset)];
                return Softmax(classProbabilities).Select(p => p * confidence).ToArray();
            }

            // IoU (Intersection over union) measures the overlap between 2 boundaries. We use that to
            // measure how much our predicted boundary overlaps with the ground truth (the real object
            // boundary). In some datasets, we predefine an IoU threshold (say 0.5) in classifying
            // whether the prediction is a true positive or a false positive. This method filters
            // overlapping bounding boxes with lower probabilities.
            private float IntersectionOverUnion(RectangleF boundingBoxA, RectangleF boundingBoxB)
            {
                var areaA = boundingBoxA.Width * boundingBoxA.Height;
                var areaB = boundingBoxB.Width * boundingBoxB.Height;

                if (areaA <= 0 || areaB <= 0)
                    return 0;

                var minX = (float)Math.Max(boundingBoxA.Left, boundingBoxB.Left);
                var minY = (float)Math.Max(boundingBoxA.Top, boundingBoxB.Top);
                var maxX = (float)Math.Min(boundingBoxA.Right, boundingBoxB.Right);
                var maxY = (float)Math.Min(boundingBoxA.Bottom, boundingBoxB.Bottom);

                var intersectionArea = (float)Math.Max(maxY - minY, 0) * (float)Math.Max(maxX - minX, 0);

                return intersectionArea / (areaA + areaB - intersectionArea);
            }

            public List<BoundingBox> ParseOutputs(float[] modelOutput, float probabilityThreshold = .3f)
            {
                var boxes = new List<BoundingBox>();

                for (int row = 0; row < ROW_COUNT; row++)
                {
                    for (int column = 0; column < COLUMN_COUNT; column++)
                    {
                        for (int box = 0; box < boxAnchors.Length; box++)
                        {
                            var channel = box * (classLabels.Length + FEATURES_PER_BOX);

                            var boundingBoxPrediction = ExtractBoundingBoxPrediction(modelOutput, row, column, channel);

                            var mappedBoundingBox = MapBoundingBoxToCell(row, column, box, boundingBoxPrediction);

                            if (boundingBoxPrediction.Confidence < probabilityThreshold)
                                continue;

                            float[] classProbabilities = ExtractClassProbabilities(modelOutput, row, column, channel, boundingBoxPrediction.Confidence);

                            var (topProbability, topIndex) = classProbabilities.Select((probability, index) => (Score: probability, Index: index)).Max();

                            if (topProbability < probabilityThreshold)
                                continue;

                            boxes.Add(new BoundingBox
                            {
                                Dimensions = mappedBoundingBox,
                                Confidence = topProbability,
                                Label = classLabels[topIndex]
                            });
                        }
                    }
                }
                return boxes;
            }

            public List<BoundingBox> FilterBoundingBoxes(List<BoundingBox> boxes, int limit, float iouThreshold)
            {
                var results = new List<BoundingBox>();
                var filteredBoxes = new bool[boxes.Count];
                var sortedBoxes = boxes.OrderByDescending(b => b.Confidence).ToArray();

                for (int i = 0; i < boxes.Count; i++)
                {
                    if (filteredBoxes[i])
                        continue;

                    results.Add(sortedBoxes[i]);

                    if (results.Count >= limit)
                        break;

                    for (var j = i + 1; j < boxes.Count; j++)
                    {
                        if (filteredBoxes[j])
                            continue;

                        if (IntersectionOverUnion(sortedBoxes[i].Rect, sortedBoxes[j].Rect) > iouThreshold)
                            filteredBoxes[j] = true;

                        if (filteredBoxes.Count(b => b) <= 0)
                            break;
                    }
                }
                return results;
            }
        }

        private class TinyYoloModel : IOnnxModel
        {
            public string ModelPath { get; private set; }

            public string ModelInput { get; } = "image";
            public string ModelOutput { get; } = "grid";

            public string[] Labels => AvailableLabels;

            public static string[] AvailableLabels =
            {
                "aeroplane", "bicycle", "bird", "boat", "bottle",
                "bus", "car", "cat", "chair", "cow",
                "diningtable", "dog", "horse", "motorbike", "person",
                "pottedplant", "sheep", "sofa", "train", "tvmonitor"
            };

            public (float, float)[] Anchors { get; } = { (1.08f, 1.19f), (3.42f, 4.41f), (6.63f, 11.38f), (9.42f, 5.11f), (16.62f, 10.52f) };

            public TinyYoloModel(string modelPath)
            {
                if (File.Exists(modelPath))
                {
                    ModelPath = Path.GetFullPath(modelPath);
                }
                else
                {
                    throw new FileNotFoundException(modelPath);
                }
            }
        }

        private class TinyYoloPrediction : IOnnxObjectPrediction
        {
            [ColumnName("grid")]
            public float[] PredictedLabels { get; set; }
        }

        #endregion

    }
}
