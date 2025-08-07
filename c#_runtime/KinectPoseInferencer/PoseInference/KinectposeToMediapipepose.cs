using Microsoft.Azure.Kinect.BodyTracking;
using MediapipeJointId = HumanLandmarks.PoseLandmarks.Types.LandmarkIndex;

namespace KinectPoseInferencer.PoseInference
{
    static internal class LandmarkUtils
    {
        static internal MediapipeJointId KinectJoint2MediapipeJoint(JointId joint) => joint switch
        {
            JointId.Head => MediapipeJointId.Nose,
            JointId.HipLeft => MediapipeJointId.LeftHip,
            JointId.HipRight => MediapipeJointId.RightHip,
            JointId.ShoulderLeft => MediapipeJointId.LeftShoulder,
            JointId.ElbowLeft => MediapipeJointId.LeftElbow,
            JointId.WristLeft => MediapipeJointId.LeftWrist,
            JointId.HandLeft => MediapipeJointId.LeftPinky,
            JointId.ThumbLeft => MediapipeJointId.LeftThumb,
            JointId.ShoulderRight => MediapipeJointId.RightShoulder,
            JointId.ElbowRight => MediapipeJointId.RightElbow,
            JointId.WristRight => MediapipeJointId.RightWrist,
            JointId.HandRight => MediapipeJointId.RightPinky,
            JointId.ThumbRight => MediapipeJointId.RightThumb,
            JointId.KneeLeft => MediapipeJointId.LeftKnee,
            JointId.AnkleLeft => MediapipeJointId.LeftAnkle,
            JointId.FootLeft => MediapipeJointId.LeftFootIndex,
            JointId.KneeRight => MediapipeJointId.RightKnee,
            JointId.AnkleRight => MediapipeJointId.RightAnkle,
            JointId.FootRight => MediapipeJointId.RightFootIndex,
            _ => (MediapipeJointId) (-1)
        };
    }
}
