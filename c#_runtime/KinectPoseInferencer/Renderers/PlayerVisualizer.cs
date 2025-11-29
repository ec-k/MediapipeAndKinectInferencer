using HelixToolkit.Wpf;
using K4AdotNet.BodyTracking;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KinectPoseInferencer.Helpers;
using K4AdotNet.Sensor;


namespace KinectPoseInferencer.Renderers;

public class PlayerVisualizer : IDisposable
{
    const double JointRadius = 0.024;
    const int MaxBodies = 6;

    readonly MeshGeometry3D _sphereMesh;
    readonly MeshGeometry3D _cylinderMesh;
    readonly PointCloudProcessor _pointCloudProcessor;
    readonly Color _pointCloudColor = Colors.White;
    readonly List<(JointType Parent, JointType Child)> _boneConnection = BodyTrackingHelper.GetBoneConnections();
    readonly List<List<ModelVisual3D>> _bodyVisuals;
    readonly List<JointType> _jointTypes = Enum.GetValues(typeof(JointType)).Cast<JointType>().ToList();

    List<Vertex> _pointCloudVertices = new();


    public PlayerVisualizer(Calibration calibration)
    {
        SphereGeometryBuilder.Build(36, 18, out var sphereVertices, out var sphereIndices);
        _sphereMesh = HelixGeometryFactory.CreateMesh(sphereVertices, sphereIndices);

        CylinderGeometryBuilder.Build(36, out var cylinderVertices, out var cylinderIndices);
        _cylinderMesh = HelixGeometryFactory.CreateMesh(cylinderVertices, cylinderIndices);

        _pointCloudProcessor = new PointCloudProcessor(calibration);

        _bodyVisuals = new();
        var totalVisualsPerBody = _jointTypes.Count + _boneConnection.Count;
        for(var i_body = 0; i_body < MaxBodies; i_body++)
        {
            var bodyVisual = new List<ModelVisual3D>();
            for (var i_visual = 0; i_visual < totalVisualsPerBody; i_visual++)
            {
                ModelVisual3D visual;
                if (i_visual < _jointTypes.Count)
                    visual = CreateInitialSphereVisual();
                else
                    visual = CreateInitialCylinderVisual();
                bodyVisual.Add(visual);
            }
            _bodyVisuals.Add(bodyVisual);
        }
    }

    ModelVisual3D CreateInitialSphereVisual()
    {
        var model = new ModelVisual3D();
        model.Content = new GeometryModel3D
        {
            Geometry = _sphereMesh,
            Material = MaterialHelper.CreateMaterial(Colors.Transparent),
            BackMaterial = MaterialHelper.CreateMaterial(Colors.Transparent),
            Transform = new Transform3DGroup(),
        };
        return model;
    }

    ModelVisual3D CreateInitialCylinderVisual()
    {
        var model = new ModelVisual3D();
        model.Content = new GeometryModel3D
        {
            Geometry = _cylinderMesh,
            Material = MaterialHelper.CreateMaterial(Colors.Transparent),
            BackMaterial = MaterialHelper.CreateMaterial(Colors.Transparent),
            Transform = new Transform3DGroup()
        };
        return model;
    }

    public List<ModelVisual3D> UpdateVisuals(BodyFrame bodyFrame, Image depthImage)
    {
        var visualModels = new List<ModelVisual3D>();

        // Render point clouds
        //if(depthImage is not null)
        //{
        //    _pointCloudProcessor.ComputePointCloud(depthImage, ref _pointCloudVertices);

        //    if (_pointCloudVertices.Any())
        //    {
        //        var positions = _pointCloudVertices.Select(v => v.Position).ToList();

        //        var pointsVisual = PointCloudAdapter.CreatePointsVisual(positions);
        //        pointsVisual.Color = _pointCloudColor;
        //        visualModels.Add(pointsVisual);
        //    }
        //}

        // Render skeletons
        if (bodyFrame is null)
        {
            HideAllVisuals();
            return visualModels;
        }

        var visualIndexOffset = 0;
        for (var i_body = 0; i_body < Math.Min(bodyFrame.BodyCount, MaxBodies); i_body++)
        {
            bodyFrame.GetBodySkeleton(i_body, out var skeleton);
            var bodyId = bodyFrame.GetBodyId(i_body);
            var bodyColor = BodyTrackingHelper.GetBodyColor(bodyId.Value);
            var bodyMaterial = MaterialHelper.CreateMaterial(bodyColor);

            var bodyVisualModels = _bodyVisuals[i_body];

            // Update joints
            for(var i_joint=0;i_joint< _jointTypes.Count; i_joint++)
            {
                var jointType = _jointTypes[i_joint];
                var joint = skeleton[jointType];
                var position = BodyTrackingHelper.ConvertPositionToMeters(joint.PositionMm);

                var jointVisual = bodyVisualModels[i_joint];
                UpdateSphereModel(jointVisual, position, bodyMaterial);

                visualModels.Add(jointVisual);
            }


            // Render bones
            visualIndexOffset = _jointTypes.Count;
            for (var i_bones = 0; i_bones < _boneConnection.Count; i_bones++)
            {
                var bone = _boneConnection[i_bones];
                var parentJoint = skeleton[bone.Parent];
                var childJoint = skeleton[bone.Child];

                var parentPosition = BodyTrackingHelper.ConvertPositionToMeters(parentJoint.PositionMm);
                var childPosition = BodyTrackingHelper.ConvertPositionToMeters(childJoint.PositionMm);

                var boneVisual = bodyVisualModels[visualIndexOffset + i_bones];
                UpdateCylinderModel(boneVisual, parentPosition, childPosition, bodyMaterial);

                visualModels.Add(boneVisual);
            }
        }

        return visualModels;
    }

    void UpdateSphereModel(ModelVisual3D visual, Vector3 position, Material material)
    {
        // 1. Update materials
        var content = (GeometryModel3D)visual.Content;
        content.Material = material;
        content.BackMaterial = material;

        // 2. Update transforms
        var transformGroup = (Transform3DGroup)content.Transform;

        var translate = transformGroup.Children.OfType<TranslateTransform3D>().FirstOrDefault();
        if (translate is null)
        {
            translate = new TranslateTransform3D(position.X, position.Y, position.Z);

            var newTransformGroup = new Transform3DGroup();
            newTransformGroup.Children.Add(new ScaleTransform3D(JointRadius, JointRadius, JointRadius));
            newTransformGroup.Children.Add(new TranslateTransform3D(position.X, position.Y, position.Z));
            content.Transform = newTransformGroup;
        }
        else
        {
            // NOTE: Transforms should be updated in UI thread.
            translate.OffsetX = position.X;
            translate.OffsetY = position.Y;
            translate.OffsetZ = position.Z;
        }
    }

    void UpdateCylinderModel(ModelVisual3D visual, Vector3 start, Vector3 end, Material material)
    {
        // 1. Update materials
        var content = (GeometryModel3D)visual.Content;
        content.Material = material;
        content.BackMaterial = material;

        // 2. Update transforms
        var modelMatrix = BoneMatrixBuilder.Build(start, end);
        var boneTransform = Transform3DBuilder.CreateTransform(modelMatrix);

        // NOTE: Transforms should be updated in UI thread.
        content.Transform = boneTransform;
    }

    void HideAllVisuals()
    {
        foreach(var visuals in _bodyVisuals)
            foreach(var visual in visuals)
                ((GeometryModel3D)visual.Content).Material = MaterialHelper.CreateMaterial(Colors.Transparent);
    }

    public IEnumerable<ModelVisual3D> GetAllVisuals() => _bodyVisuals.SelectMany(list => list);


    public void Dispose()
    {
        _pointCloudProcessor?.Dispose();
    }
}
