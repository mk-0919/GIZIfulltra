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
            Debug.Log("����������");
        }
        else
        {
            Debug.Log("���������s");
        }

        var error = OpenVR.Overlay.CreateDashboardOverlay("GIZIfulltraDashboardKey", "GIZIfulltraController", ref dashboardHandle, ref thumbnailHandle);
        OpenVRErrorCheck(error, "�_�b�V���{�[�h�I�[�o�[���C�쐬");

        var thumbnailPictureFile = Application.streamingAssetsPath + "/DashBoardIcon.png";
        error = OpenVR.Overlay.SetOverlayFromFile(thumbnailHandle, thumbnailPictureFile);
        OpenVRErrorCheck(error, "�T���l�C���摜�ݒ�");

        error = OpenVR.Overlay.SetOverlayWidthInMeters(dashboardHandle, 2.5f);
        OpenVRErrorCheck(error, "�I�[�o�[���C�T�C�Y�ݒ�");

        var mouseScalingFactor = new HmdVector2_t()
        {
            v0 = renderTexture.width,
            v1 = renderTexture.height,
        };
        error = OpenVR.Overlay.SetOverlayMouseScale(dashboardHandle, ref mouseScalingFactor);
        OpenVRErrorCheck(error, "�}�E�X�ݒ�");
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
        OpenVRErrorCheck(error, "�_�b�V���{�[�h�I�[�o�[���C�j��");
    }

    private void OpenVRErrorCheck(EVROverlayError error, string processName)
    {
        if(error != EVROverlayError.None)
        {
            throw new Exception($"{processName}�Ɏ��s: " + error);
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
        OpenVRErrorCheck(error, "�����_�[�e�N�X�`���K�p");
    }

    private Button GetButtonByPosition(Vector2 position)
    {
        var pointerEventData = new PointerEventData(eventSystem);
        pointerEventData.position = position;
        
        var raycastResultList = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerEventData, raycastResultList);

        // ���X�g����{�^�����������o��
        var raycastResult = raycastResultList.Find(element => element.gameObject.GetComponent<Button>());
        if (raycastResult.gameObject == null)
        {
            return null;
        }
        return raycastResult.gameObject.GetComponent<Button>();
    }
}
