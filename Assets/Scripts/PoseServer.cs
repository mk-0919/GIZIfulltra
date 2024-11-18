using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(uOSC.uOscClient))]
public class PoseServer : MonoBehaviour
{
    private Process pythonProcess;
    TcpListener imageServer;
    TcpListener landmarksServer;
    TcpClient imageClient;
    TcpClient landmarksClient;
    NetworkStream imageStream;
    NetworkStream landmarksStream;
    Thread imageRequestThread;
    Thread landmarksSendThread;
    Thread receiveThread;
    Texture2D texture;
    byte[] imageSizeBuffer = new byte[16];

    private byte[] imageData;
    private bool isNewImageAvailable = false;
    private bool isNewLandmarksAvailable = false;

    public Get_ThreePoints ThreePoints;
    uOSC.uOscClient client;

    TcpListener offsetServer;
    TcpClient offsetClient;
    NetworkStream offsetStream;
    Thread offsetThread;

    void Start()
    {
        texture = new Texture2D(640, 480);
        GetComponent<Renderer>().material.mainTexture = texture;

        // Pythonプロセスを起動
        StartPythonProcess();

        // 画像用サーバーのセットアップ
        imageServer = new TcpListener(IPAddress.Any, 9999);
        imageServer.Start();

        // ランドマーク用サーバーのセットアップ
        landmarksServer = new TcpListener(IPAddress.Any, 10000);
        landmarksServer.Start();

        offsetServer = new TcpListener(IPAddress.Any, 10001);
        offsetServer.Start();

        imageRequestThread = new Thread(new ThreadStart(AutoRequestImage));
        imageRequestThread.IsBackground = true;
        imageRequestThread.Start();

        landmarksSendThread = new Thread(new ThreadStart(AutoSendLandmarks));
        landmarksSendThread.IsBackground = true;
        landmarksSendThread.Start();

        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        offsetThread = new Thread(new ThreadStart(waitOffsetResult));
        offsetThread.IsBackground = true;
        offsetThread.Start();

        client = GetComponent<uOSC.uOscClient>();
    }

    void StartPythonProcess()
    {
        string pythonFilePath = Path.Combine(Application.streamingAssetsPath, "mediapipe", "poseClient.py"); // Pythonスクリプトのパス
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Application.streamingAssetsPath, "env", "Scripts", "python.exe"),
            Arguments = "-u " + pythonFilePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        pythonProcess = new Process();
        pythonProcess.StartInfo = startInfo;
        pythonProcess.OutputDataReceived += (sendar, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnityEngine.Debug.Log("Python Output: " + e.Data);
            }
        };
        pythonProcess.ErrorDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnityEngine.Debug.LogError("Python Error: " + e.Data);
            }
        };

        pythonProcess.Start();
        pythonProcess.BeginOutputReadLine();
        pythonProcess.BeginErrorReadLine();

        if (pythonProcess != null)
        {
            UnityEngine.Debug.Log("Pythonプロセスが起動しました。");
        }
    }

    void ReceiveData()
    {
        imageClient = imageServer.AcceptTcpClient();
        imageStream = imageClient.GetStream();
        UnityEngine.Debug.Log("Pythonの画像クライアントと接続しました。");

        landmarksClient = landmarksServer.AcceptTcpClient();
        landmarksStream = landmarksClient.GetStream();
        UnityEngine.Debug.Log("Pythonのランドマーククライアントと接続しました。");

        while (true)
        {
            if (imageStream.DataAvailable)
            {
                byte[] requestBuffer = new byte[128];
                int bytesRead = imageStream.Read(requestBuffer, 0, requestBuffer.Length);
                string requestType = Encoding.UTF8.GetString(requestBuffer, 0, bytesRead).Trim();

                if (requestType == "IMAGE")
                {
                    ReceiveImage();
                }
            }
        }
    }

    void AutoRequestImage()
    {
        while(true)
        {
            RequestImage();
            Thread.Sleep(1 / 30);  
        }
    }

    void AutoSendLandmarks()
    {
        while (true)
        {
            SendLandmarks(); 
            Thread.Sleep(1 / 10);
        }
    }

    void Update()
    {
        //Debug用手動リクエスト
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RequestImage();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            SendLandmarks();
        }

        if (isNewImageAvailable)
        {
            ApplyTexture(imageData);
            isNewImageAvailable = false;
        }

        if (isNewLandmarksAvailable)
        {

            isNewLandmarksAvailable = false;
        }
    }

    void RequestImage()
    {
        if (imageClient == null || imageStream == null)
            return;

        byte[] requestMessage = Encoding.UTF8.GetBytes("REQUEST_IMAGE");
        imageStream.Write(requestMessage, 0, requestMessage.Length);
    }

    public void SendLandmarks()
    {
        if (landmarksClient == null || landmarksStream == null)
            return;

        byte[] requestMessage = Encoding.UTF8.GetBytes("SEND_LANDMARKS");
        landmarksStream.Write(requestMessage, 0, requestMessage.Length);
    }

    void ReceiveImage()
    {
        imageStream.Read(imageSizeBuffer, 0, imageSizeBuffer.Length);
        int imageSize = int.Parse(Encoding.UTF8.GetString(imageSizeBuffer).Trim());

        byte[] receivedImageData = new byte[imageSize];
        int bytesReceived = 0;
        while (bytesReceived < imageSize)
        {
            bytesReceived += imageStream.Read(receivedImageData, bytesReceived, imageSize - bytesReceived);
        }
        imageData = receivedImageData;
        isNewImageAvailable = true;
    }

    void ApplyTexture(byte[] imageData)
    {
        texture.LoadImage(imageData);
        texture.Apply();
    }

    private Vector3 GetHeadPosition()
    {
        if(ThreePoints)
        {
            return ThreePoints.HeadPos;
        }
        return new Vector3(0, 1.8f, 0);
    }

    private Vector3 GetLeftHandPosition()
    {
        if(ThreePoints)
        {
            return ThreePoints.LHandPos;
        }
        return new Vector3(-0.5f, 1.0f, 0);
    }

    private Vector3 GetRightHandPosition()
    {
        if(ThreePoints)
        {
            return ThreePoints.RHandPos;
        }
        return new Vector3(0.5f, 1.0f, 0);
    }

    private void waitOffsetResult()
    {
        offsetClient = offsetServer.AcceptTcpClient();
        offsetStream = offsetClient.GetStream();
        while(true)
        {
            if(offsetStream.DataAvailable)
            {
                byte[] requestBuffer = new byte[1024];
                int bytesRead = offsetStream.Read(requestBuffer, 0, requestBuffer.Length);
                string requestType = Encoding.UTF8.GetString(requestBuffer, 0, bytesRead).Trim();

                if (requestType == "SUCCSESS")
                {
                    print("offset success");
                }
            }
        }
    }

    public void send_offset_data()
    {
        if (offsetClient == null || offsetStream == null)
            return;

        // ここでUnityの頭と両手の座標を送信
        Vector3 headPosition = GetHeadPosition();
        Vector3 leftHandPosition = GetLeftHandPosition();
        Vector3 rightHandPosition = GetRightHandPosition();

        string coordinatesJson = JsonUtility.ToJson(new CoordinatesData
        {
            Head = headPosition,
            LeftHand = leftHandPosition,
            RightHand = rightHandPosition
        });

        byte[] coordinatesData = Encoding.UTF8.GetBytes(coordinatesJson);
        offsetStream.Write((coordinatesData.Length.ToString()).PadLeft(16).ToCharArray().Select(c => (byte)c).ToArray(), 0, 16);
        offsetStream.Write(coordinatesData, 0, coordinatesData.Length);
    }

    private void OnApplicationQuit()
    {
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
        }

        imageRequestThread.Abort();
        landmarksSendThread.Abort();
        receiveThread.Abort();
        imageStream.Close();
        landmarksStream.Close();
        imageClient.Close();
        landmarksClient.Close();
        imageServer.Stop();
        landmarksServer.Stop();
    }

    // 座標データ用のクラス
    [Serializable]
    private class CoordinatesData
    {
        public Vector3 Head;
        public Vector3 LeftHand;
        public Vector3 RightHand;
    }
    //mediapipe座標デシリアライズ用
    [Serializable]
    private class MediapipeCoordinates
    {
        public float x;
        public float y;
        public float z;
        public float visibility;
    }
    private class JsonWraper
    {
        public static T[] FromJson<T>(string json)
        {
            string wrappedJson = "{ \"array\": " + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
            return wrapper.array;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }
}
