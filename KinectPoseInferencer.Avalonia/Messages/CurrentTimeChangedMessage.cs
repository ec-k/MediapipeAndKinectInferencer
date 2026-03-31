using System;

namespace KinectPoseInferencer.Avalonia.Messages;

public record CurrentTimeChangedMessage(TimeSpan CurrentTime, TimeSpan PlaybackLength);
