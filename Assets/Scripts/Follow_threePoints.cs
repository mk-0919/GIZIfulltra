using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using EasyLazyLibrary;

[RequireComponent(typeof(uOSC.uOscClient))]
public class Follow_threePoints : MonoBehaviour
{
    EasyOpenVRUtil eou;
    uOSC.uOscClient client;
    void Start()
    {
        eou = new EasyOpenVRUtil();
        eou.StartOpenVR();
        client = GetComponent<uOSC.uOscClient>();
    }

    void Update()
    {
        eou.AutoExitOnQuit();
        var HMDTransform = eou.GetHMDTransform();
        var RControllerTransform = eou.GetRightControllerTransform();
        var LControllerTransform = eou.GetLeftControllerTransform();
        Debug.Log(HMDTransform.position);
        //HMDTracker
        client.Send("/VMT/Room/Unity", 0, 1, 0f,
            (float)HMDTransform.position.x, (float)HMDTransform.position.y, (float)HMDTransform.position.z + 1,
            (float)HMDTransform.rotation.x, (float)HMDTransform.rotation.y, (float)HMDTransform.rotation.z, (float)HMDTransform.rotation.w);
        //RTracker
        client.Send("/VMT/Room/Unity", 1, 1, 0f,
            (float)RControllerTransform.position.x, (float)RControllerTransform.position.y, (float)RControllerTransform.position.z,
            (float)RControllerTransform.rotation.x, (float)RControllerTransform.rotation.y, (float)RControllerTransform.rotation.z, (float)RControllerTransform.rotation.w);
        //LTracker
        client.Send("/VMT/Room/Unity", 2, 1, 0f,
            (float)LControllerTransform.position.x, (float)LControllerTransform.position.y, (float)LControllerTransform.position.z,
            (float)LControllerTransform.rotation.x, (float)LControllerTransform.rotation.y, (float)LControllerTransform.rotation.z, (float)LControllerTransform.rotation.w);
    }
}
