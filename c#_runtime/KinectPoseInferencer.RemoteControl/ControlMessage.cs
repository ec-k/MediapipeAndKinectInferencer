using System.Text.Json.Serialization;

namespace KinectPoseInferencer.RemoteControl;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Command")]
[JsonDerivedType(typeof(PlayMessage), typeDiscriminator: "Play")]
[JsonDerivedType(typeof(PauseMessage), typeDiscriminator: "Pause")]
[JsonDerivedType(typeof(RewindMessage), typeDiscriminator: "Rewind")]
[JsonDerivedType(typeof(SetConfigurationMessage), typeDiscriminator: "SetConfiguration")]
public abstract record ControlMessage;

public record PlayMessage : ControlMessage;
public record PauseMessage : ControlMessage;
public record RewindMessage : ControlMessage;

public record SetConfigurationMessage(InferencerConfiguration Config) : ControlMessage;
public record InferencerConfiguration(bool IsKinectEnabled);