using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyLazyLibrary;

public class Get_ThreePoints : MonoBehaviour
{
    EasyOpenVRUtil eou;
    public Vector3 HeadPos, RHandPos, LHandPos;
    private void Start()
    {
        eou = new EasyOpenVRUtil();
        if(eou.StartOpenVR())
        {
            Debug.Log("èâä˙âªê¨å˜");
        }
        else
        {
            Debug.Log("èâä˙âªé∏îs");
        }
    }

    private void Update()
    {
        eou.AutoExitOnQuit();
        HeadPos = eou.GetHMDTransform().position;
        RHandPos = eou.GetRightControllerTransform().position;
        LHandPos = eou.GetLeftControllerTransform().position;
    }

    public Vector3 GetHeadPosition()
    {
        return eou.GetHMDTransform().position;
    }

    public Vector3 GetLeftHandPosition()
    {
        var hoge = eou.GetLeftControllerTransform();
        return hoge.position;
    }

    public Vector3 GetRightHandPosition()
    {
        return eou.GetRightControllerTransform().position;
    }
}
