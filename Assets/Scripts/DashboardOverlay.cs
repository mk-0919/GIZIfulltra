using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Valve.VR;
using System;
using EasyLazyLibrary;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DashboardOverlay : MonoBehaviour
{
    private ulong dashboardHandle = OpenVR.k_ulOverlayHandleInvalid;
    private ulong thumbnailHandle = OpenVR.k_ulOverlayHandleInvalid;
    private EasyOpenVRUtil eou = new EasyOpenVRUtil();

    public RenderTexture renderTexture;
    public GraphicRaycaster graphicRaycaster;
    public EventSystem eventSystem;

    private void Start()
    {
        if(eou.StartOpenVR())
        {
            Debug.Log("初期化成功");
        }
        else
        {
            Debug.Log("初期化失敗");
        }

        var error = OpenVR.Overlay.CreateDashboardOverlay("GIZIfulltraDashboardKey", "GIZIfulltraController", ref dashboardHandle, ref thumbnailHandle);
        OpenVRErrorCheck(error, "ダッシュボードオーバーレイ作成");

        var thumbnailPictureFile = Application.streamingAssetsPath + "/DashBoardIcon.png";
        error = OpenVR.Overlay.SetOverlayFromFile(thumbnailHandle, thumbnailPictureFile);
        OpenVRErrorCheck(error, "サムネイル画像設定");

        error = OpenVR.Overlay.SetOverlayWidthInMeters(dashboardHandle, 2.5f);
        OpenVRErrorCheck(error, "オーバーレイサイズ設定");

        var mouseScalingFactor = new HmdVector2_t()
        {
            v0 = renderTexture.width,
            v1 = renderTexture.height,
        };
        error = OpenVR.Overlay.SetOverlayMouseScale(dashboardHandle, ref mouseScalingFactor);
        OpenVRErrorCheck(error, "マウス設定");
    }

    private void Update()
    {
        eou.AutoExitOnQuit();
        SetOverlayRenderTexture(dashboardHandle, renderTexture);

        var vrEvent = new VREvent_t();
        var uncbVREvent = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t));

        while(OpenVR.Overlay.PollNextOverlayEvent(dashboardHandle, ref vrEvent, uncbVREvent))
        {
            switch (vrEvent.eventType)
            {
                case (uint)EVREventType.VREvent_MouseButtonUp:
                    vrEvent.data.mouse.y = renderTexture.height - vrEvent.data.mouse.y;
                    var button = GetButtonByPosition(new Vector2(vrEvent.data.mouse.x, vrEvent.data.mouse.y));
                    if(button != null)
                    {
                        button.onClick.Invoke();
                    }
                    break;
            }
        }
        
    }

    private void OnApplicationQuit()
    {
        var error = OpenVR.Overlay.DestroyOverlay(dashboardHandle);
        OpenVRErrorCheck(error, "ダッシュボードオーバーレイ破棄");
    }

    private void OpenVRErrorCheck(EVROverlayError error, string processName)
    {
        if(error != EVROverlayError.None)
        {
            throw new Exception($"{processName}に失敗: " + error);
        }
    }

    private void SetOverlayRenderTexture(ulong handle, RenderTexture renderTexture)
    {
        if (!renderTexture.IsCreated())
            return;

        var nativeTexturePtr = renderTexture.GetNativeTexturePtr();
        var texture = new Texture_t
        {
            eColorSpace = EColorSpace.Auto,
            eType = ETextureType.DirectX,
            handle = nativeTexturePtr,
        };
        var error = OpenVR.Overlay.SetOverlayTexture(handle, ref texture);
        OpenVRErrorCheck(error, "レンダーテクスチャ適用");
    }

    private Button GetButtonByPosition(Vector2 position)
    {
        var pointerEventData = new PointerEventData(eventSystem);
        pointerEventData.position = position;
        
        var raycastResultList = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerEventData, raycastResultList);

        // リストからボタンだけを取り出す
        var raycastResult = raycastResultList.Find(element => element.gameObject.GetComponent<Button>());
        if (raycastResult.gameObject == null)
        {
            return null;
        }
        return raycastResult.gameObject.GetComponent<Button>();
    }
}
