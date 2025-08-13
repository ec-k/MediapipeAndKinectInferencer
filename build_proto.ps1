Write-Host "Generating Protobuf code using custom Docker image..."

$PROTO_PATH = "./proto/Proto"
$PYTHON_OUT_DIR = ".\mediapipe_inferencer\src\proto_generated"
$CSHARP_OUT_DIR = ".\c#_runtime\KinectPoseInferencer\Proto"

# Docker image settings for proto compiler
$DOCKERFILE_PATH = ".\tools\protobuf-codegen\Dockerfile"
$PROTO_VERSION = "25.3"
$PROTO_IMAGE_NAME = "protoc-generator:$PROTO_VERSION"

# Create output directories if it doesn't exist
if (-not (Test-Path -Path $PYTHON_OUT_DIR -PathType Container)){
    New-Item -ItemType Directory -Path $PYTHON_OUT_DIR | Out-Null
}
if (-not (Test-Path -Path $CSHARP_OUT_DIR -PathType Container)){
    New-Item -ItemType Directory -Path $CSHARP_OUT_DIR | Out-Null
}

Write-Host "Building Docker image '$PROTO_IMAGE_NAME' from '$DOCKERFILE_PATH'..."
docker build -f $DOCKERFILE_PATH -t $PROTO_IMAGE_NAME . --build-arg PROTOBUF_VERSION=$PROTO_VERSION

# Get .proto files
$protoFiles = Get-ChildItem -Path $PROTO_PATH -Filter "*.proto" | ForEach-Object { $_.Name }

Write-Host "Generating Python code..."
foreach ($file in $protoFiles) {
    Write-Host "  Generating Python for $file"

    $volumePathProto = (Convert-Path $PROTO_PATH).Replace('\', '/')
    $volumePathPython = (Convert-Path $PYTHON_OUT_DIR).Replace('\', '/')

    docker run --rm `
        -v `"$volumePathProto`":/app/proto `
        -v `"$volumePathPython`":/app/python_out `
        $PROTO_IMAGE_NAME `
        protoc -I/app/proto --python_out=/app/python_out /app/proto/$file
}

# Create an empty __init__.py file
New-Item -Path "$PYTHON_OUT_DIR\__init__.py" -ItemType File -Force | Out-Null

Write-Host "Generating C# code..."
foreach ($file in $protoFiles) {
    Write-Host "  Generating C# for $file"

    $volumePathProto = (Convert-Path $PROTO_PATH).Replace('\', '/')
    $volumePathCsharp = (Convert-Path $CSHARP_OUT_DIR).Replace('\', '/')

    docker run --rm `
        -v `"$volumePathProto`":/app/proto `
        -v `"$volumePathCsharp`":/app/csharp_out `
        $PROTO_IMAGE_NAME `
        protoc -I/app/proto --csharp_out=/app/csharp_out /app/proto/$file
}

Write-Host "Done."
