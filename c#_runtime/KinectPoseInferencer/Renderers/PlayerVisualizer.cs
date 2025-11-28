using HelixToolkit.Wpf;
using K4AdotNet.BodyTracking;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KinectPoseInferencer.Helpers;

namespace KinectPoseInferencer.Renderers;


public class PlayerVisualizer
{
    readonly MeshGeometry3D _sphereMesh;
    readonly MeshGeometry3D _cylinderMesh;

    readonly Material _pointCloudMaterial = MaterialHelper.CreateMaterial(Colors.White);
    readonly List<(JointType Parent, JointType Child)> _boneConnection = BodyTrackingHelper.GetBoneConnections();

    public PlayerVisualizer()
    {
        SphereGeometryBuilder.Build(36, 18, out var sphereVertices, out var sphereIndices);
        _sphereMesh = HelixGeometryFactory.CreateMesh(sphereVertices, sphereIndices);

        CylinderGeometryBuilder.Build(36, out var cylinderVertices, out var cylinderIndices);
        _cylinderMesh = HelixGeometryFactory.CreateMesh(cylinderVertices, cylinderIndices);
    }

    public List<ModelVisual3D> UpdateVisuals(BodyFrame bodyFrame)
    {
        var visualModels = new List<ModelVisual3D>();

        if (bodyFrame is null) return visualModels;


        for (var i = 0; i < bodyFrame.BodyCount; ++i)
        {
            bodyFrame.GetBodySkeleton(i, out var skeleton);
            var bodyId = bodyFrame.GetBodyId((int)i);
            var bodyColor = BodyTrackingHelper.GetBodyColor(bodyId.Value);
            var bodyMaterial = MaterialHelper.CreateMaterial(bodyColor);

            // Render joints
            var jointTypes = Enum.GetValues(typeof(JointType)).Cast<JointType>();
            foreach (var jointType in jointTypes)
            {
                var joint = skeleton[jointType];
                var position = BodyTrackingHelper.ConvertPositionToMeters(joint.PositionMm);
                visualModels.Add(CreateSphereModel(position, bodyMaterial));
            }


            // Render bones
            foreach(var bone in _boneConnection)
            {
                var parentJoint = skeleton[bone.Parent];
                var childJoint = skeleton[bone.Child];

                var parentPosition = BodyTrackingHelper.ConvertPositionToMeters(parentJoint.PositionMm);
                var childPosition = BodyTrackingHelper.ConvertPositionToMeters(childJoint.PositionMm);

                visualModels.Add(CreateCylinderModel(parentPosition, childPosition, bodyMaterial));
            }
        }

        return visualModels;
    }

    ModelVisual3D CreateSphereModel(Vector3 position, Material material)
    {
        const double Radius = 0.024;

        var jointTransformGroup = new Transform3DGroup();
        jointTransformGroup.Children.Add(new ScaleTransform3D(Radius, Radius, Radius));
        jointTransformGroup.Children.Add(new TranslateTransform3D(position.X, position.Y, position.Z));

        var jointVisual = new ModelVisual3D();
        jointVisual.Content = new GeometryModel3D
        {
            Geometry = _sphereMesh,
            Material = material,
            Transform = jointTransformGroup
        };
        return jointVisual;
    }

    ModelVisual3D CreateCylinderModel(Vector3 start, Vector3 end, Material material)
    {
        var modelMatrix = BoneMatrixBuilder.Build(start, end);
        var boneTransform = Transform3DBuilder.CreateTransform(modelMatrix);

        var boneVisual = new ModelVisual3D();
        boneVisual.Content = new GeometryModel3D
        {
            Geometry = _cylinderMesh,
            Material = material,
            Transform = boneTransform
        };
        return boneVisual;
    }
}
