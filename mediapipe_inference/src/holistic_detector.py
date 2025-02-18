import mediapipe as mp
from mediapipe.tasks import python as mp_tasks
from pathlib import Path
from result_data import HolisticLandmarks
import cv2
import time
import concurrent.futures


root_directory = str(Path(__file__).parent.parent)

class HolisticDetector:
    def __init__(self):
        self.__hand = HandDetector(root_directory + "/models/hand_landmarker.task")
        self.__face = FaceDetector(root_directory + "/models/face_landmarker.task")

        self.latest_time_ms = 0

    def inference(self, image):
        t_ms = int(time.time() * 1000)
        if t_ms <= self.latest_time_ms:
            return

        mp_image = mp.Image(image_format = mp.ImageFormat.SRGB, data = image)
        
        # Run detections in parallel using ThreadPoolExecutor
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as executor:
            futures = [
                executor.submit(self.__hand.inference, mp_image, t_ms),
                executor.submit(self.__face.inference, mp_image, t_ms)
            ]
            concurrent.futures.wait(futures)

        self.latest_time_ms = t_ms

    @property
    def results(self):
        return HolisticLandmarks(
            None,
            self.__hand.results,
            self.__face.results
        )

class PoseDetector:
    def __init__(self, model_path):
        options = mp_tasks.vision.PoseLandmarkerOptions(
            base_options = mp_tasks.BaseOptions(model_asset_buffer = open(model_path, "rb").read()),
            running_mode = mp_tasks.vision.RunningMode.LIVE_STREAM,
            result_callback = self.__save_results
            )
        self.landmarker = mp_tasks.vision.PoseLandmarker.create_from_options(options)
        self.__results = None

    @property
    def results(self):
        return self.__results

    def __save_results(self, results, output_image, timesamp_ms: int):
        self.__results = results

    def inference(self, image, frame_timestamp_ms):
        self.landmarker.detect_async(image, frame_timestamp_ms)


class HandDetector:
    def __init__(self, model_path):
        options = mp_tasks.vision.HandLandmarkerOptions(
            base_options = mp_tasks.BaseOptions(model_asset_buffer = open(model_path, "rb").read()),
            num_hands = 2,
            running_mode = mp_tasks.vision.RunningMode.LIVE_STREAM,
            result_callback=self.__save_results)

        self.landmarker = mp_tasks.vision.HandLandmarker.create_from_options(options)
        self.__results = None

    @property
    def results(self):
        return self.__results

    def __save_results(self, results, output_image, timesamp_ms: int):
        self.__results = results

    def inference(self, image, frame_timestamp_ms):
        self.landmarker.detect_async(image, frame_timestamp_ms)


class FaceDetector:
    def __init__(self, model_path):
        options = mp_tasks.vision.FaceLandmarkerOptions(
            base_options = mp_tasks.BaseOptions(model_asset_buffer = open(model_path, "rb").read()),
            output_face_blendshapes = True,
            output_facial_transformation_matrixes = True,
            num_faces = 1,
            running_mode = mp_tasks.vision.RunningMode.LIVE_STREAM,
            result_callback=self.__save_results)

        self.landmarker = mp_tasks.vision.FaceLandmarker.create_from_options(options)
        self.__results = None

    @property
    def results(self):
        return self.__results

    def __save_results(self, results, output_image, timesamp_ms: int):
        self.__results = results

    def inference(self, image, frame_timestamp_ms):
        self.landmarker.detect_async(image, frame_timestamp_ms)


def ResultVisualizer(image, results, isFlipped = True):
    mp_drawing = mp.solutions.drawing_utils
    mp_drawing_styles = mp.solutions.drawing_styles

    # TODO: Rewrite this to use MediaPipe.Task results
    mp_holistic = mp.solutions.holistic
    mp_drawing.draw_landmarks(
        image,
        results.face_landmarks,
        mp_holistic.FACEMESH_CONTOURS,
        landmark_drawing_spec=None,
        connection_drawing_spec=mp_drawing_styles
        .get_default_face_mesh_contours_style())
    mp_drawing.draw_landmarks(
        image,
        results.pose_landmarks,
        mp_holistic.POSE_CONNECTIONS,
        landmark_drawing_spec=mp_drawing_styles
        .get_default_pose_landmarks_style())

    # Flip the image horizontally for a selfie-view display.
    if isFlipped:
        cv2.imshow('MediaPipe Results', cv2.flip(image, 1))