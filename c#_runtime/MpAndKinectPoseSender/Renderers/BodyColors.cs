// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using System.Drawing;
using System.Numerics;

namespace MpAndKinectPoseSender.Renderers
{
    static class BodyColors
    {
        private static readonly Color[] colorSet =
        {
            Color.Red,
            Color.Blue,
            Color.Green,
            Color.Yellow,
            Color.DeepSkyBlue,
            Color.HotPink,
            Color.Purple
        };

        private static Color GetColor(uint i)
        {
            return colorSet[i % colorSet.Length];
        }

        public static Vector4 GetColorAsVector(uint i)
        {
            var color = GetColor(i);
            return new Vector4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
        }
    }
}