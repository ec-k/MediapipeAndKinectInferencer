using K4AdotNet.Sensor;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;


namespace KinectPoseInferencer.Renderers;

public class PointCloudProcessor : IDisposable
{
    Vector3[,] _pointCloudCache;
    readonly Calibration _calibration;
    readonly Transformation _transformation;

    public PointCloudProcessor(Calibration calibration)
    {
        _calibration = calibration;
        _transformation = calibration.CreateTransformation();

        ComputePointCloudCache();
    }

    void ComputePointCloudCache()
    {
        using var fakeDepth = new Image(
            ImageFormat.Depth16,
            _calibration.DepthCameraCalibration.ResolutionWidth,
            _calibration.DepthCameraCalibration.ResolutionHeight);
        MemoryMarshal.Cast<byte, ushort>(fakeDepth.GetSpan<byte>()).Fill(1000);

        using var pointCloudImage = new Image(
            ImageFormat.Custom,
            fakeDepth.WidthPixels,
            fakeDepth.HeightPixels,
            sizeof(short) * 3 * fakeDepth.WidthPixels);

        _transformation.DepthImageToPointCloud(fakeDepth, CalibrationGeometry.Depth, pointCloudImage);

        {
            var width = _calibration.DepthCameraCalibration.ResolutionWidth;
            var height = _calibration.DepthCameraCalibration.ResolutionHeight;
            var pointCloudBuffer = MemoryMarshal.Cast<byte, short>(pointCloudImage.GetSpan<byte>());

            _pointCloudCache = new Vector3[height, width];
            for (int k = 0, v = 0; v < height; ++v)
            {
                for (int u = 0; u < width; ++u, k += 3)
                {
                    // mm -> m
                    var point = new Vector3(pointCloudBuffer[k], pointCloudBuffer[k + 1], pointCloudBuffer[k + 2]) / 1000;
                    _pointCloudCache[v, u] = point;
                }
            }
        }
    }

    public void ComputePointCloud(Image depthImage, ref List<Vertex> pointCloud)
    {
        if (_pointCloudCache == null)
        {
            throw new InvalidOperationException("Point cloud cache has not been computed.");
        }

        if (pointCloud == null)
        {
            pointCloud = new List<Vertex>(depthImage.HeightPixels * depthImage.WidthPixels);
        }

        pointCloud.Clear();

        var depthPixels = MemoryMarshal.Cast<byte, ushort>(depthImage.GetSpan<byte>());

        for (int v = 0, pixelIndex = 0; v < depthImage.HeightPixels; ++v)
        {
            for (int u = 0; u < depthImage.WidthPixels; ++u, ++pixelIndex)
            {
                var depthInMillimeters = depthPixels[pixelIndex];

                var positionPerMeterDepth = _pointCloudCache[v, u];

                if (depthInMillimeters > 0 && depthInMillimeters != ushort.MaxValue)
                {
                    var depthInMeters = depthInMillimeters / 1000f; // mm -> m

                    pointCloud.Add(new Vertex
                    {
                        Position = positionPerMeterDepth * depthInMeters,
                        Normal = Vector3.UnitZ,
                    });
                }
            }
        }
    }

    public void Dispose()
    {
        _transformation?.Dispose();
    }
}
