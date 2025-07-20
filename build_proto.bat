@echo off
echo Generating Protobuf code...

SET PROTO_PATH=./proto
mkdir .\\mediapipe_inferencer\\src\\proto_generated || echo proto_generated dir already exists.

echo Generating Python code...
protoc -I=%PROTO_PATH% --python_out=./mediapipe_inferencer/src/proto_generated %PROTO_PATH%/holistic_landmarks.proto
type nul > .\\mediapipe_inferencer\\src\\proto_generated\\__init__.py

echo Generating C# code...
protoc -I=%PROTO_PATH% --csharp_out=./c#_runtime %PROTO_PATH%/holistic_landmarks.proto

echo Done.
