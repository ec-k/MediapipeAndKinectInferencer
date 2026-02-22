using K4AdotNet.BodyTracking;

namespace KinectPoseInferencer.Core;

/// <summary>
/// Holds skeleton data extracted from BodyFrame for thread-safe rendering.
/// </summary>
public record SkeletonData(Skeleton Skeleton, int BodyId);
