using System;
using System.Net;
using System.Net.Sockets;
using HumanLandmarks;
using Google.Protobuf;
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

        readonly SkeletonToPoseLandmarksConverter _converter;

        readonly Action<SocketException> _socketExceptionCallback;
        readonly Action<ObjectDisposedException> _objectDisposedExceptionCallback;

        HolisticLandmarks _result;

        public LandmarkHandler(
            SkeletonToPoseLandmarksConverter converter
            )
        {
            _sender = new UdpClient();
            var senderEndPoint = new IPEndPoint(IPAddress.Parse(_senderUri), _senderPort);
            _sender.Connect(senderEndPoint);
            _receiver = new UdpClient(_receiverPort);

            _result = new HolisticLandmarks();

            _receiver.BeginReceive(OnReceived, _receiver);

            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public void Update(Skeleton skeleton)
        {
            _result.PoseLandmarks = _converter.Convert(skeleton);
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

        public void SendResults() 
        {
            var sendData = _result.ToByteArray();
            _sender.Send(sendData, sendData.Length);
        }

        public void Dispose()
        {
            _sender.Close(); _sender.Dispose();
            _receiver.Close(); _receiver.Dispose();
        }
    }
}
