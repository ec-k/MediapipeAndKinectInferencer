using HelixToolkit.Wpf;
using K4AdotNet.BodyTracking;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace KinectPoseInferencer.Renderers;


public class PlayerVisualizer
{
    readonly MeshGeometry3D _sphereMesh;
    readonly MeshGeometry3D _cylinderMesh;

    readonly Material _pointCloudMaterial = MaterialHelper.CreateMaterial(Colors.White);

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

            var color = GetBodyColor(bodyId.Value);
            var bodyMaterial = MaterialHelper.CreateMaterial(color);

            var jointTypes = Enum.GetValues(typeof(JointType)).Cast<JointType>();
            foreach (var jointType in jointTypes)
            {
                var joint = skeleton[jointType];
                var position = new Vector3(joint.PositionMm.X, joint.PositionMm.Y, joint.PositionMm.Z) / 1000f; // mm -> m

                // Create sphere (joint model)
                var jointVisual = new ModelVisual3D();

                var jointTransformGroup = new Transform3DGroup();
                jointTransformGroup.Children.Add(new ScaleTransform3D(0.024, 0.024, 0.024));
                jointTransformGroup.Children.Add(new TranslateTransform3D(position.X, position.Y, position.Z));

                jointVisual.Content = new GeometryModel3D
                {
                    Geometry = _sphereMesh,
                    Material = bodyMaterial,
                    Transform = jointTransformGroup
                };
                visualModels.Add(jointVisual);

                // Render bones (cylinders)
                try
                {
                    var parentType = jointType.GetParent();
                    var parentJoint = skeleton[parentType];

                    var parentPosition = new Vector3(parentJoint.PositionMm.X, parentJoint.PositionMm.Y, parentJoint.PositionMm.Z) / 1000f;

                    var modelMatrix = BoneMatrixBuilder.Build(parentPosition, position);
                    var boneTransform = Transform3DBuilder.CreateTransform(modelMatrix);

                    var boneVisual = new ModelVisual3D();
                    boneVisual.Content = new GeometryModel3D
                    {
                        Geometry = _cylinderMesh,
                        Material = bodyMaterial,
                        Transform = boneTransform
                    };
                    visualModels.Add(boneVisual);
                }
                catch { /* skip for Joints that have no parents (like Plvis) */ }
            }
        }

        return visualModels;
    }

    Color GetBodyColor(int bodyId)
    {
        var colors = new List<Color> { Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow };
        return colors[(bodyId % colors.Count)];
    }
}
