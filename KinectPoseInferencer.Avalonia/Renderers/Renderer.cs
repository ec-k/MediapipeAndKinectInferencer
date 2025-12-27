// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using K4AdotNet.BodyTracking;
using OpenGL;
using OpenGL.CoreUI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Renderers
{
    public class Renderer
    {
        private SphereRenderer SphereRenderer;
        private CylinderRenderer CylinderRenderer;
        private PointCloudRenderer PointCloudRenderer;

        private readonly Core.FrameManager visualizerData;
        private List<Vertex> pointCloud = null;

        public Renderer(Core.FrameManager visualizerData)
        {
            this.visualizerData = visualizerData;
        }

        public bool IsActive { get; private set; }

        public void StartVisualizationThread()
        {
            Task.Run(() =>
            {
                using NativeWindow nativeWindow = NativeWindow.Create();
                IsActive = true;
                nativeWindow.ContextCreated += NativeWindow_ContextCreated;
                nativeWindow.Render += NativeWindow_Render;
                nativeWindow.KeyDown += (object obj, NativeWindowKeyEventArgs e) =>
                {
                    switch (e.Key)
                    {
                        case KeyCode.Escape:
                            nativeWindow.Stop();
                            IsActive = false;
                            break;

                        case KeyCode.F:
                            nativeWindow.Fullscreen = !nativeWindow.Fullscreen;
                            break;
                    }
                };
                nativeWindow.Animation = true;

                nativeWindow.Create(0, 0, 640, 480, NativeWindowStyle.Overlapped);

                nativeWindow.Show();
                nativeWindow.Run();
            });
        }

        private void NativeWindow_ContextCreated(object sender, NativeWindowEventArgs e)
        {
            Gl.ReadBuffer(ReadBufferMode.Back);

            Gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);

            Gl.Enable(EnableCap.Blend);
            Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            Gl.LineWidth(2.5f);

            CreateResources();
        }

        private static float ToRadians(float degrees)
        {
            return degrees / 180.0f * (float)Math.PI;
        }

        private void NativeWindow_Render(object sender, NativeWindowEventArgs e)
        {
            using (var lastFrame = visualizerData.TakeFrameWithOwnership())
            {
                if (lastFrame == null)
                {
                    return;
                }

                NativeWindow nativeWindow = (NativeWindow)sender;

                Gl.Viewport(0, 0, (int)nativeWindow.Width, (int)nativeWindow.Height);
                Gl.Clear(ClearBufferMask.ColorBufferBit);

                // Update model/view/projective matrices in shader
                var proj = Matrix4x4.CreatePerspectiveFieldOfView(ToRadians(65.0f), (float)nativeWindow.Width / nativeWindow.Height, 0.1f, 150.0f);
                var view = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitZ, -Vector3.UnitY);

                SphereRenderer.View = view;
                SphereRenderer.Projection = proj;

                CylinderRenderer.View = view;
                CylinderRenderer.Projection = proj;

                PointCloudRenderer.View = view;
                PointCloudRenderer.Projection = proj;

                PointCloud.ComputePointCloud(lastFrame.Capture.DepthImage, ref pointCloud);
                PointCloudRenderer.Render(pointCloud, new Vector4(1, 1, 1, 1));

                for (uint i = 0; i < lastFrame.BodyCount; ++i)
                {
                    Skeleton skeleton;
                    lastFrame.GetBodySkeleton((int)i, out skeleton);
                    var bodyId = lastFrame.GetBodyId((int)i);
                    var bodyColor = BodyColors.GetColorAsVector((uint)bodyId.Value);

                    var tmpArr = Enum.GetValues(typeof(JointType));
                    for (int jointId = 0; jointId < tmpArr.Length; ++jointId)
                    {
                        var joint = skeleton[jointId];

                        // Render the joint as a sphere.
                        const float radius = 0.024f;
                        var jointPositionVector = new Vector3(joint.PositionMm.X, joint.PositionMm.Y, joint.PositionMm.Z);
                        SphereRenderer.Render(jointPositionVector / 1000, radius, bodyColor);

                        try
                        {
                            var jointType = (JointType)Enum.ToObject(typeof(JointType), jointId);
                            var parent = jointType.GetParent();
                            var parentJoint = skeleton[parent];
                            var parentJointVector = new Vector3(parentJoint.PositionMm.X, parentJoint.PositionMm.Y, parentJoint.PositionMm.Z);
                            // Render a bone connecting this joint and its parent as a cylinder.
                            CylinderRenderer.Render(jointPositionVector / 1000, parentJointVector / 1000, bodyColor);
                        }
                        catch { }
                    }
                }
            }
        }

        void RenderColorImage(object sender, NativeWindowEventArgs e)
        {
            using var lastFrame = visualizerData.TakeFrameWithOwnership();
            if (lastFrame == null)
            {
                return;
            }
            NativeWindow nativeWindow = (NativeWindow)sender;
            Gl.Viewport(0, 0, (int)nativeWindow.Width, (int)nativeWindow.Height);
            Gl.Clear(ClearBufferMask.ColorBufferBit);
        }

        private void CreateResources()
        {
            SphereRenderer = new SphereRenderer();
            CylinderRenderer = new CylinderRenderer();
            PointCloudRenderer = new PointCloudRenderer();
        }
    }
}
