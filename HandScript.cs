using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using ViconDataStreamSDK.CSharp;

// This script is attached to the Hand GameObject in Unity
// It gets the rotation data from Vicon and applies it to the Hand GameObject
// It also gets the rotation data for the thumb, index, middle, ring, and pinky fingers
// and applies it to the respective GameObjects in Unity
//This has more manual approach then the SubjectScript.cs , and not guaranteed to work. 
// TODO : Test this script and make sure it works as expected
// TODO : Add error handling and logging
// TODO : Add more features like scaling, translation, etc.
// TODO : Add more functionality to control the hand and fingers in Unity
namespace UnityVicon
{
    public HandScript : MonoBehaviour
    {
    public ViconDataStreamClient Client;
    public Transform handTransform;
    public Transform thumbTransform, middleFingerTransform, ringFingerTransform, pinkyTransform;


    public HandScript()
    { 
        //Assuming that the segments exist in the Vicon system and Unity scene :
    handTransform = GameObject.Find("Hand").transform;
    thumbTransform = GameObject.Find("Thumb").transform;
    indexFingerTransform = GameObject.Find("IndexFinger").transform;
    middleFingerTransform = GameObject.Find("MiddleFinger").transform;
    ringFingerTransform = GameObject.Find("RingFinger").transform;
    pinkyTransform = GameObject.Find("Pinky").transform;
    }   

    void Update()
    {

              if (Client == null)
          {
            Debug.LogError("Vicon Client is NULL in SubjectScript! Make sure it's initialized.");
            return;
          }
        handTransform.rotation = GetViconRotation("Hand", "Root");

        thumbTransform.rotation = GetViconRotation("Hand", "Thumb");
        indexFingerTransform.rotation = GetViconRotation("Hand", "IndexFinger");
        middleFingerTransform.rotation = GetViconRotation("Hand", "MiddleFinger");
        ringFingerTransform.rotation = GetViconRotation("Hand", "RingFinger");
        pinkyTransform.rotation = GetViconRotation("Hand", "Pinky");
    }

    // Helper function to get rotation from Vicon
    Quaternion GetViconRotation(string subject, string segment)
    {
        Output_GetSegmentGlobalRotationEuler rotationData = viconClient.GetSegmentGlobalRotationEuler(subject, segment);
        if (rotationData.Result == Result.Success)
        {
        //TODO: Check if the rotation data is in the correct format
            return Quaternion.Euler(rotationData.Rotation[0], rotationData.Rotation[1], rotationData.Rotation[2]);
        }
        return Quaternion.identity; // Default rotation if data fails
    }

    // for smoother movement, use this: 
    // handTransform.rotation = Quaternion.Slerp(handTransform.rotation, 
    //                                           GetViconRotation("Hand", "Root"), 
    //                                           Time.deltaTime * 10f);

    }
}

