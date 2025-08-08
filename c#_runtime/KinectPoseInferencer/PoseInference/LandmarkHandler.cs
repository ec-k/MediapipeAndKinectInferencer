using System;
using System.Net;
using System.Net.Sockets;
using HumanLandmarks;
using Google.Protobuf;
using K4AdotNet.Sensor;
using K4AdotNet.BodyTracking;

namespace KinectPoseInferencer.PoseInference
{
    internal class LandmarkHandler : IDisposable
    {
        UdpClient _sender;
        string _senderUri = "127.0.0.1";
        int _senderPort = 9000;

        UdpClient _receiver;
        int _receiverPort = 9001;

        readonly TiltCorrector _tiltCorrector;

        readonly Action<SocketException> _socketExceptionCallback;
        readonly Action<ObjectDisposedException> _objectDisposedExceptionCallback;

        HolisticLandmarks _result;

        public LandmarkHandler(TiltCorrector tiltCorrector)
        {
            _sender = new UdpClient();
            var senderEndPoint = new IPEndPoint(IPAddress.Parse(_senderUri), _senderPort);
            _sender.Connect(senderEndPoint);
            _receiver = new UdpClient(_receiverPort);

            _result = new HolisticLandmarks();

            _receiver.BeginReceive(OnReceived, _receiver);

            _tiltCorrector = tiltCorrector ?? throw new ArgumentNullException(nameof(tiltCorrector));
        }

        internal void UpdateTiltRotation(ImuSample imuSample, Calibration calibration) => _tiltCorrector.UpdateTiltRotation(imuSample, calibration);
        internal void ResetTiltRotation() => _tiltCorrector.ResetTiltRotation();

        public void Update(Skeleton skeleton)
        {
            PackResults(skeleton);
        }

        (float, float, float) TransformCoordination(float x, float y, float z)
        {
            return (-x, -y, -z);
        }

        void OnReceived(IAsyncResult result) 
        {
            UdpClient getUdp = (UdpClient)result.AsyncState;
            IPEndPoint ipEnd = null;

            try
            {
                var getByte = getUdp.EndReceive(result, ref ipEnd);

                var receivedBody = HolisticLandmarks.Parser.ParseFrom(getByte);
                receivedBody.PoseLandmarks = _result.PoseLandmarks;

                _result = receivedBody;
            }
            catch (SocketException e)
            {
                _socketExceptionCallback(e);
                return;
            }
            catch (ObjectDisposedException e) 
            {
                _objectDisposedExceptionCallback(e); 
                return;
            }

            _receiver.BeginReceive(OnReceived, getUdp);
        }

        void PackResults(Skeleton skeleton)
        {
            var kinectBodyLandmarks = new KinectPoseLandmarks();
            const int poselmListSize = 32;
            var poseLandmarks = new Landmark[poselmListSize];

            var enumArr = Enum.GetValues(typeof(JointType));
            for(var jointId = 0; jointId < enumArr.Length; jointId++)
            {
                var joint = skeleton[(JointType)jointId];
                var lm = PackLandmark(joint);
                poseLandmarks[jointId] = lm;
            }
            for (var i = 0; i < poseLandmarks.Length; i++)
            {
                if (poseLandmarks[i] == null)
                    poseLandmarks[i] = new Landmark();
            }
            if(poseLandmarks != null && poseLandmarks.Length > 0)
                kinectBodyLandmarks.Landmarks.AddRange(poseLandmarks);

            _result.PoseLandmarks = kinectBodyLandmarks;
        }

        Landmark PackLandmark(Joint joint)
        {
            var lm = new Landmark();
            var position = new Position();

            // Convert position from millimeters to meters
            position.X = joint.PositionMm.X / 1000;
            position.Y = joint.PositionMm.Y / 1000;
            position.Z = joint.PositionMm.Z / 1000;

            (position.X, position.Y, position.Z) = _tiltCorrector.CorrectLandmarkPosition(position.X, position.Y, position.Z);
            (position.X, position.Y, position.Z) = TransformCoordination(position.X, position.Y, position.Z);

            lm.Position = position;
            lm.Confidence = joint.ConfidenceLevel switch
            {
                JointConfidenceLevel.None => 0f,
                JointConfidenceLevel.Low => 0.3f,
                JointConfidenceLevel.Medium => 0.6f,
                JointConfidenceLevel.High => 0.9f,
                _ => throw new InvalidOperationException()
            };

            return lm;
        }

        public void SendResults() 
        {
            var sendData = _result.ToByteArray();
            _sender.Send(sendData, sendData.Length);
        }

        public void Dispose()
        {
            _sender.Close(); _sender.Dispose();
            _receiver.Close(); _receiver.Dispose();
            _tiltCorrector.Dispose();
        }
    }
}
