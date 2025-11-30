using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Shapes;

using KinectPoseInferencer.Helpers;

namespace KinectPoseInferencer.Renderers;

public class PlayerVisualizer : IDisposable
{
    readonly double JointRadius = 0.024;
    readonly int MaxBodies = 6;

    readonly List<(JointType Parent, JointType Child)> _boneConnection = BodyTrackingHelper.GetBoneConnections();
    readonly List<JointType> _jointTypes = Enum.GetValues(typeof(JointType)).Cast<JointType>().ToList();

    public class VisualData
    {
        public List<BodyVisualData> BodyData { get; set; } = new List<BodyVisualData>();
    }

    public class BodyVisualData
    {
        public Color BodyColor { get; set; }
        public List<JointVisualData> JointData { get; set; } = new List<JointVisualData>();
        public List<BoneVisualData> BoneData { get; set; } = new List<BoneVisualData>();
    }

    public class JointVisualData
    {
        public Point Position { get; set; }
    }

    public class BoneVisualData
    {
        public Point ParentPosition { get; set; }
        public Point ChildPosition { get; set; }
    }


    readonly List<Ellipse> _jointEllipses = new();
    readonly List<Line> _boneLines = new();
    readonly List<UIElement> _currentActiveVisualElements = new();

    public IEnumerable<UIElement> ActiveVisualElements => _currentActiveVisualElements;
    
    readonly Calibration _calibration;

    public PlayerVisualizer(Calibration calibration)
    {
        _calibration = calibration;
    }

    public void UpdateVisuals(VisualData data, double canvasWidth, double canvasHeight)
    {
        _currentActiveVisualElements.Clear();

        var kinectColorImageWidth = _calibration.ColorCameraCalibration.ResolutionWidth;
        var kinectColorImageHeight = _calibration.ColorCameraCalibration.ResolutionHeight;

        foreach (var bodyData in data.BodyData)
        {
            var brush = new SolidColorBrush(bodyData.BodyColor);

            // Render joints
            foreach (var jointData in bodyData.JointData)
            {
                if (jointData.Position.X >= 0 && jointData.Position.Y >= 0)
                {
                    Ellipse ellipse = GetOrCreateEllipse();

                    var scaledX = (jointData.Position.X / kinectColorImageWidth) * canvasWidth;
                    var scaledY = (jointData.Position.Y / kinectColorImageHeight) * canvasHeight;

                    ellipse.Width = JointRadius * 200 * (canvasWidth / kinectColorImageWidth);
                    ellipse.Height = JointRadius * 200 * (canvasHeight / kinectColorImageHeight);
                    ellipse.Fill = brush;
                    ellipse.Stroke = brush;
                    ellipse.StrokeThickness = 1;

                    Canvas.SetLeft(ellipse, scaledX - ellipse.Width / 2);
                    Canvas.SetTop(ellipse, scaledY - ellipse.Height / 2);
                    ellipse.Visibility = Visibility.Visible;

                    _currentActiveVisualElements.Add(ellipse);
                }
            }

            // Render bones
            foreach (var boneData in bodyData.BoneData)
            {
                if (boneData.ParentPosition.X >= 0 && boneData.ParentPosition.Y >= 0 &&
                    boneData.ChildPosition.X >= 0 && boneData.ChildPosition.Y >= 0)
                {
                    var line = GetOrCreateLine();

                    var scaledParentX = (boneData.ParentPosition.X / kinectColorImageWidth) * canvasWidth;
                    var scaledParentY = (boneData.ParentPosition.Y / kinectColorImageHeight) * canvasHeight;
                    var scaledChildX = (boneData.ChildPosition.X / kinectColorImageWidth) * canvasWidth;
                    var scaledChildY = (boneData.ChildPosition.Y / kinectColorImageHeight) * canvasHeight;

                    line.X1 = scaledParentX;
                    line.Y1 = scaledParentY;
                    line.X2 = scaledChildX;
                    line.Y2 = scaledChildY;
                    line.Stroke = brush;
                    line.StrokeThickness = 3 * (canvasWidth / kinectColorImageWidth);
                    line.Visibility = Visibility.Visible;

                    _currentActiveVisualElements.Add(line);
                }
            }
        }

        // Make invisible objects that are not utilized.
        foreach (var ellipse in _jointEllipses)
        {
            if (!_currentActiveVisualElements.Contains(ellipse))
            {
                ellipse.Visibility = Visibility.Collapsed;
            }
        }
        foreach (var line in _boneLines)
        {
            if (!_currentActiveVisualElements.Contains(line))
            {
                line.Visibility = Visibility.Collapsed;
            }
        }
    }

    private Ellipse GetOrCreateEllipse()
    {
        // Use invisible elipses when it is existed.
        foreach (var ellipse in _jointEllipses)
        {
            if (ellipse.Visibility == Visibility.Collapsed)
            {
                return ellipse;
            }
        }

        // Create new when no lines existed
        var newEllipse = new Ellipse();
        _jointEllipses.Add(newEllipse);
        return newEllipse;
    }

    Line GetOrCreateLine()
    {
        // Use invisible lines when it is existed.
        foreach (var line in _boneLines)
        {
            if (line.Visibility == Visibility.Collapsed)
            {
                return line;
            }
        }

        // Create new when no lines existed
        var newLine = new Line();
        _boneLines.Add(newLine);
        return newLine;
    }
    
    public void Dispose()
    {
        // leave cleanup to GC.
    }

    public VisualData ProcessFrame(BodyFrame bodyFrame, K4AdotNet.Sensor.Image depthImage)
    {
        var visualData = new VisualData();

        // Render skeletons
        if (bodyFrame is null)
        {
            return visualData;
        }

        // --- Start 3D to 2D Transformation Logic ---
        using (var transformation = _calibration.CreateTransformation())
        {
            var colorImageResolution = _calibration.ColorCameraCalibration.ResolutionWidth;
            var depthWidth = _calibration.DepthCameraCalibration.ResolutionWidth;
            var depthHeight = _calibration.DepthCameraCalibration.ResolutionHeight;

            // Create a dummy depth image to get the 2D coordinates in color space
            // This is a workaround as TransformTo2D requires a depth image (even if dummy)
            // The size of dummyDepthImage should match the depth camera resolution for correct transformation.
            using (var dummyDepthImage = new K4AdotNet.Sensor.Image(ImageFormat.Depth16, depthWidth, depthHeight))
            {
                for (var i_body = 0; i_body < Math.Min(bodyFrame.BodyCount, MaxBodies); i_body++)
                {
                    bodyFrame.GetBodySkeleton(i_body, out var skeleton);
                    var bodyId = bodyFrame.GetBodyId(i_body);
                    var bodyColor = BodyTrackingHelper.GetBodyColor(bodyId.Value);

                    var bodyVisualData = new BodyVisualData { BodyColor = bodyColor };

                    // Process joints
                    foreach (var jointType in _jointTypes)
                    {
                        var joint = skeleton[jointType];
                        // Transform 3D joint position to 2D color image coordinate
                        var point2D = _calibration.Convert3DTo2D(joint.PositionMm, CalibrationGeometry.Depth, CalibrationGeometry.Color);

                        // K4AdotNet TransformTo2D returns 0,0 for invalid points, check for that
                        if (point2D is not null && point2D?.X != 0 && point2D?.Y != 0)
                        {
                            // Map to actual image size (if different from calibration resolution)
                            // Assuming the target image (e.g., in UI) will be scaled to match original color image resolution
                            bodyVisualData.JointData.Add(new JointVisualData { Position = new Point((double)point2D?.X, (double)point2D?.Y) });
                        }
                        else
                        {
                            // If invalid, add a default/invisible point (e.g., -1,-1 or NaN)
                            bodyVisualData.JointData.Add(new JointVisualData { Position = new Point(-1, -1) }); // Indicate invalid point
                        }
                    }

                    // Process bones - connect only valid joints
                    foreach (var bone in _boneConnection)
                    {
                        var parentJoint = skeleton[bone.Parent];
                        var childJoint = skeleton[bone.Child];

                        var parentPoint2D = _calibration.Convert3DTo2D(parentJoint.PositionMm, CalibrationGeometry.Depth, CalibrationGeometry.Color);
                        var childPoint2D = _calibration.Convert3DTo2D(childJoint.PositionMm, CalibrationGeometry.Depth, CalibrationGeometry.Color);

                        if (parentPoint2D is not null && childPoint2D is not null
                            && parentPoint2D?.X != 0 && parentPoint2D?.Y != 0 
                            && childPoint2D?.X != 0 && childPoint2D?.Y != 0)
                        {
                            bodyVisualData.BoneData.Add(new BoneVisualData
                            {
                                ParentPosition = new Point((double)parentPoint2D?.X, (double)parentPoint2D?.Y),
                                ChildPosition = new Point((double)childPoint2D?.X, (double)childPoint2D?.Y)
                            });
                        }
                    }
                    visualData.BodyData.Add(bodyVisualData);
                }
            }
        }
        // --- End 3D to 2D Transformation Logic ---

        return visualData;
    }
}