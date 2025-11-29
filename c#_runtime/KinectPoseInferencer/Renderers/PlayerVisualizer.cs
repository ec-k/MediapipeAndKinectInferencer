using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls; // For UIElement
using System.Windows.Shapes; // For Ellipse, Line

using KinectPoseInferencer.Helpers;
using K4AdotNet;

namespace KinectPoseInferencer.Renderers;

public class PlayerVisualizer : IDisposable
{
    const double JointRadius = 0.024; // This might still be useful for 2D visualization (e.g., circle size)
    const int MaxBodies = 6;

    readonly List<(JointType Parent, JointType Child)> _boneConnection = BodyTrackingHelper.GetBoneConnections();
    readonly List<JointType> _jointTypes = Enum.GetValues(typeof(JointType)).Cast<JointType>().ToList();

    // 中間データ構造の定義
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
        public Point Position { get; set; } // Changed to 2D Point
    }

    public class BoneVisualData
    {
        public Point ParentPosition { get; set; } // Changed to 2D Point
        public Point ChildPosition { get; set; } // Changed to 2D Point
    }

    // Kinect のキャリブレーション情報を保存するためのフィールドを追加
    readonly Calibration _calibration;

    public PlayerVisualizer(Calibration calibration)
    {
        _calibration = calibration; // Store calibration for 3D to 2D transformation
    }

    // 3D関連のメソッドは削除
    // ModelVisual3D CreateInitialSphereVisual() { ... }
    // ModelVisual3D CreateInitialCylinderVisual() { ... }
    // void UpdateSphereModel(ModelVisual3D visual, Vector3 position, Material material) { ... }
    // void UpdateCylinderModel(ModelVisual3D visual, Vector3 start, Vector3 end, Material material) { ... }
    // void HideAllVisuals() { ... }

    public List<UIElement> UpdateVisuals(VisualData data, double canvasWidth, double canvasHeight) // imageWidth/Height を canvasWidth/Height に変更
    {
        var elements = new List<UIElement>();

        // Kinect のカラー画像解像度を取得
        var kinectColorImageWidth = _calibration.ColorCameraCalibration.ResolutionWidth;
        var kinectColorImageHeight = _calibration.ColorCameraCalibration.ResolutionHeight;

        // 各Bodyのジョイントとボーンを描画
        foreach (var bodyData in data.BodyData)
        {
            var brush = new SolidColorBrush(bodyData.BodyColor);

            // ジョイントの描画
            foreach (var jointData in bodyData.JointData)
            {
                if (jointData.Position.X >= 0 && jointData.Position.Y >= 0) // 有効な点のみ描画
                {
                    // スケール調整: Kinect 解像度 -> Canvas 解像度
                    var scaledX = (jointData.Position.X / kinectColorImageWidth) * canvasWidth;
                    var scaledY = (jointData.Position.Y / kinectColorImageHeight) * canvasHeight;

                    var ellipse = new Ellipse
                    {
                        Width = JointRadius * 200 * (canvasWidth / kinectColorImageWidth), // スケール調整
                        Height = JointRadius * 200 * (canvasHeight / kinectColorImageHeight), // スケール調整
                        Fill = brush,
                        Stroke = brush,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(ellipse, scaledX - ellipse.Width / 2);
                    Canvas.SetTop(ellipse, scaledY - ellipse.Height / 2);
                    elements.Add(ellipse);
                }
            }

            // ボーンの描画
            foreach (var boneData in bodyData.BoneData)
            {
                if (boneData.ParentPosition.X >= 0 && boneData.ParentPosition.Y >= 0 &&
                    boneData.ChildPosition.X >= 0 && boneData.ChildPosition.Y >= 0) // 有効な点のみ描画
                {
                    // スケール調整: Kinect 解像度 -> Canvas 解像度
                    var scaledParentX = (boneData.ParentPosition.X / kinectColorImageWidth) * canvasWidth;
                    var scaledParentY = (boneData.ParentPosition.Y / kinectColorImageHeight) * canvasHeight;
                    var scaledChildX = (boneData.ChildPosition.X / kinectColorImageWidth) * canvasWidth;
                    var scaledChildY = (boneData.ChildPosition.Y / kinectColorImageHeight) * canvasHeight;

                    var line = new Line
                    {
                        X1 = scaledParentX,
                        Y1 = scaledParentY,
                        X2 = scaledChildX,
                        Y2 = scaledChildY,
                        Stroke = brush,
                        StrokeThickness = 3 * (canvasWidth / kinectColorImageWidth), // 太さもスケール調整
                    };
                    elements.Add(line);
                }
            }
        }
        return elements;
    }

    public IEnumerable<UIElement> GetAllVisuals() => new List<UIElement>(); // No longer returns 3D visuals

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
            var colorImageResolution = _calibration.ColorResolution;
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

    public void Dispose()
    {
        // No _pointCloudProcessor to dispose anymore
    }
}