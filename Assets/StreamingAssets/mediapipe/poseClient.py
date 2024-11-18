import cv2
import mediapipe as mp
import socket
import numpy as np
import json
import struct
import threading
from pythonosc import udp_client
from pythonosc.osc_message_builder import OscMessageBuilder
from scipy.spatial.transform import Rotation as R

# MediaPipeの初期化
mp_pose = mp.solutions.pose
pose = mp_pose.Pose()

# カメラのセットアップ
cap = cv2.VideoCapture(0)

# ソケットの設定
image_client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
landmarks_client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
offset_client = socket.socket(socket.AF_INET,socket.SOCK_STREAM)

# UnityサーバーのIPアドレスとポート番号
SERVER_IP = 'localhost'
IMAGE_PORT = 9999
LANDMARKS_PORT = 10000
OFFSET_PORT = 10001

# サーバーに接続
image_client.connect((SERVER_IP, IMAGE_PORT))
landmarks_client.connect((SERVER_IP, LANDMARKS_PORT))
offset_client.connect((SERVER_IP, OFFSET_PORT))

#offset
offset_y = 0
is_send_tracker = False
offset_z = [0.0 for i in range(33)]

client = udp_client.UDPClient('127.0.0.1',39570)

# 画像を送信する処理
def send_image():
    while True:
        request_message = image_client.recv(1024)

        ret, frame = cap.read()
        if not ret:
            continue

        image_client.sendall(b"IMAGE".ljust(128))

        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = pose.process(image_rgb)

        if results.pose_landmarks:
            mp.solutions.drawing_utils.draw_landmarks(frame, results.pose_landmarks, mp_pose.POSE_CONNECTIONS)

        _, buffer = cv2.imencode('.jpg', frame)
        data = np.array(buffer)
        stringData = data.tobytes()

        image_client.sendall((str(len(stringData))).encode().ljust(16) + stringData)

# ランドマークを送信する処理
def send_landmarks():
    global offset_y,offset_z,client
    while True:
        # Unityから座標データを受け取る
        coordinates_data = landmarks_client.recv(1024)
        #coordinates = json.loads(coordinates_data.decode('utf-8'))

        ret, frame = cap.read()
        if not ret:
            continue

        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = pose.process(image_rgb)

        if not results.pose_world_landmarks:
            continue

        mp_landmarks = results.pose_world_landmarks.landmark

        landmarks = []
        for index,landmark in enumerate(mp_landmarks):
            coord = np.array([-landmark.x,-landmark.y+offset_y,-landmark.z + offset_z[index]])

            landmarks.append(coord)

        mp_indexes = [25,26,27,28]
        for i,index in enumerate(mp_indexes):
            msg = OscMessageBuilder(address='/VMT/Room/Unity')
            msg.add_arg(i)
            msg.add_arg(1)
            msg.add_arg(0.0)
            msg.add_arg(landmarks[index][0])
            msg.add_arg(landmarks[index][1])
            msg.add_arg(landmarks[index][2])
            for i in range(4):
                msg.add_arg(0.0)

            m = msg.build()
            client.send(m)

#offset更新
def set_offset():
    global offset_y,offset_z
    while True:
        # Unityから座標データを受け取る
        size_data = offset_client.recv(16)
        size = int(size_data.decode('utf-8').strip())
        coordinates_data = offset_client.recv(size)
        coordinates = json.loads(coordinates_data.decode('utf-8'))

        ret, frame = cap.read()
        if not ret:
            continue

        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = pose.process(image_rgb)

        if not results.pose_world_landmarks:
            continue

        mp_landmarks = results.pose_world_landmarks.landmark

        # MediaPipeのワールド座標から頭と両手のランドマークを取得
        head_mp = np.array([mp_landmarks[0].x, -mp_landmarks[0].y, mp_landmarks[0].z])  # 頭
        left_hand_mp = np.array([mp_landmarks[15].x, -mp_landmarks[15].y, mp_landmarks[15].z])  # 左手i
        right_hand_mp = np.array([mp_landmarks[16].x, -mp_landmarks[16].y, mp_landmarks[16].z]) # 右手

        # Unityの座標
        head_unity = np.array([coordinates['Head']['x'], coordinates['Head']['y'], coordinates['Head']['z']])
        left_hand_unity = np.array([coordinates['LeftHand']['x'], coordinates['LeftHand']['y'], coordinates['LeftHand']['z']])
        right_hand_unity = np.array([coordinates['RightHand']['x'], coordinates['RightHand']['y'], coordinates['RightHand']['z']])

        for index,landmark in enumerate(mp_landmarks):
            offset_z[index] = +landmark.z + head_unity[2]

        offset_y = (mp_landmarks[29].y + mp_landmarks[30].y) / 2

        offset_client.sendall(b"SUCCSESS")

# 画像とランドマークの送信を別々のスレッドで実行
image_thread = threading.Thread(target=send_image)
landmarks_thread = threading.Thread(target=send_landmarks)
offset_thread = threading.Thread(target=set_offset)

image_thread.start()
landmarks_thread.start()
offset_thread.start()

image_thread.join()
landmarks_thread.join()
offset_thread.join()

cap.release()
image_client.close()
landmarks_client.close()
offset_client.close()
