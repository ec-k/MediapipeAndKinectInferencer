// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using K4AdotNet.BodyTracking;

namespace KinectPoseInferencer.Core
{
    public class FrameManager : IDisposable
    {
        private BodyFrame frame;

        public BodyFrame Frame
        {
            set
            {
                lock (this)
                {
                    frame?.Dispose();
                    frame = value;
                }
            }
        }

        public BodyFrame TakeFrameWithOwnership()
        {
            lock (this)
            {
                var result = frame;
                frame = null;
                return result;
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                frame?.Dispose();
                frame = null;
            }
        }
    }
}
