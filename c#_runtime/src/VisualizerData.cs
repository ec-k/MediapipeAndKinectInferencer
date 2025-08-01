// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using Microsoft.Azure.Kinect.BodyTracking;
using System;

namespace Scripts
{
    public class VisualizerData : IDisposable
    {
        private Frame frame;

        public Frame Frame
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

        public Frame TakeFrameWithOwnership()
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
