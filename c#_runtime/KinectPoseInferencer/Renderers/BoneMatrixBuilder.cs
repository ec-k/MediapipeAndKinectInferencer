using System;
using System.Numerics;

namespace KinectPoseInferencer.Renderers;

public static class BoneMatrixBuilder
{
    const float BaseScale = 0.01f;

    /// <summary>
    /// Calculates the Model Matrix for a bone segment represented by a unit cylinder.
    /// The matrix scales the cylinder to the correct radius/length, positions it in the center,
    /// and rotates it from the Z-axis (cylinder default axis) to align with the bone's central axis.
    /// (Ported from original CylinderRenderer.cs Render method)
    /// </summary>
    /// <param name="start">Start position of the bone.</param>
    /// <param name="end">End position of the bone.</param>
    /// <returns>The bone's transformation matrix (Model Matrix) as Matrix4x4.</returns>
    public static Matrix4x4 Build(Vector3 start, Vector3 end)
    {
        var boneVector = end - start;
        var length = boneVector.Length();
        var centerPosition = (start + end) / 2;
        
        // 1. Scale Matrix
        var scaleMatrix = Matrix4x4.CreateScale(BaseScale, BaseScale, length);

        // 2. Rotation Matrix
        var defaultAxis = Vector3.UnitZ;
        var targetAxis = Vector3.Normalize(boneVector);

        Matrix4x4 rotationMatrix;

        if (targetAxis.LengthSquared() < 1e-6)                     // approximatly zero vector
            rotationMatrix = Matrix4x4.Identity;
        else if (Vector3.Dot(defaultAxis, targetAxis) > 0.999f)    // same directions
            rotationMatrix = Matrix4x4.Identity;
        else if (Vector3.Dot(defaultAxis, targetAxis) < -0.999f)   // inverse directions
            rotationMatrix = Matrix4x4.CreateRotationX(MathF.PI);
        else
        {
            var rotationAxis = Vector3.Normalize(Vector3.Cross(defaultAxis, targetAxis));
            var angle = MathF.Acos(Vector3.Dot(defaultAxis, targetAxis));
            rotationMatrix = Matrix4x4.CreateFromAxisAngle(rotationAxis, angle);
        }

        // 3. Translation Matrix
        var translationMatrix = Matrix4x4.CreateTranslation(centerPosition);

        var model = translationMatrix * rotationMatrix * scaleMatrix;

        return model;
    }
}
