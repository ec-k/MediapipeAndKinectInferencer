This application captures and merges pose data from Azure Kinect (body) and MediaPipe (face, hands) and sends it as a unified data stream.

## Concept

- **Azure Kinect**: Provides robust body tracking data.
- **MediaPipe**: Provides detailed face and hand landmarks.
- **C# Runtime**: Manages the Kinect sensor, visualizes the data, and communicates with the Python backend.
- **Python Inferencer**: Receives image data from the C# runtime, performs MediaPipe inference, and sends the combined pose data to a final client.
- **Protobuf**: Defines the data structure (`HolisticLandmarks`) for communication between all components.

## Installation & Setup

Before running the application, you need to set up the environment and build the necessary communication code.

**1. Clone the Repository**

```bash
git clone --recursive https://github.com/ec-k/mediapipe-and-kinect-pose-sender.git
cd mediapipe-and-kinect-pose-sender
```

**2. Generate Protobuf Code**

This project uses Protobuf to ensure data consistency between the C# and Python applications. Run the build script to generate the necessary code.

**On Windows:**

```bash
.\build_proto.bat
```

This will create `HolisticLandmarks.cs` in the C# project and `holistic_landmarks_pb2.py` in the Python project. This step is only required once, or whenever you modify the `.proto` file.

**3. Set up C# Environment**

- Open `c#_runtime/mediapipe-and-kinect-pose-sender.sln` in Visual Studio.
- Restore the NuGet packages.
- Build the solution.

**4. Set up Python Environment**

The Python inferencer uses Poetry for dependency management.

```bash
cd mediapipe_inferencer
poetry install
```

**5. Download MediaPipe Models**

Place the required MediaPipe model files (`.task` files) into the `mediapipe_inferencer/models/` directory.

## How to Run

1.  **Start the C# Runtime**: Run the `mediapipe-and-kinect-pose-sender` project from Visual Studio. This will start the Kinect, open a visualization window, and wait for the Python backend.
2.  **Start the Python Inferencer**:
    ```bash
    cd mediapipe_inferencer
    poetry run python src/inference_by_mmap.py
    ```
3.  **Run Your Client Application**: Start your own application that will receive the final pose data (which conforms to `holistic_landmarks.proto`).

## License

<!-- The source code in this repository created by the author is licensed under the **MIT License**. -->

<!-- However,  -->

This project depends on third-party software, and your use of this application is subject to their respective licenses.

- **Microsoft Azure Kinect Body Tracking SDK**: The C# runtime component relies on NuGet packages governed by the **MICROSOFT SOFTWARE LICENSE TERMS**. By building or using the compiled application, you agree to these terms. Please review them carefully.

- **Python Inferencer Dependencies**: The Python inferencer utilizes various third-party libraries. A complete list of these dependencies and their licenses is available in the `ThirdPartyNotices.txt` file at the following location: <https://github.com/ec-k/mediapipe-inferencer/blob/main/ThirdPartyNotices.txt>
