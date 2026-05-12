/* using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using ViconDataStreamSDK.CSharp;


namespace UnityVicon
{
  public class RBScript : MonoBehaviour
  {
    public string ObjectName = "";
    public ViconDataStreamClient Client;

    private Quaternion m_LastGoodRotation;
    private Vector3 m_LastGoodPosition;
    private bool m_bHasCachedPose = false;

    public RBScript()
    {
    }

    void Update()
    {

      if (Client == null)
      {
        Debug.LogError("Vicon Client is null! Make sure it's initialized before use.");
        return;
      }

      Output_GetSubjectRootSegmentName OGSRSN = Client.GetSubjectRootSegmentName(ObjectName);
      string SegRootName = OGSRSN.SegmentName;

      // UNITY-49 - Don't apply root motion to parent object
      Transform Root = transform;
      if (Root == null)
      {
        throw new Exception("fbx doesn't have root");
      }
      // Debug.Log("ObjectName in Unity: " + ObjectName);

      Output_GetSegmentLocalRotationQuaternion ORot = Client.GetSegmentRotation(ObjectName, SegRootName);
      Output_GetSegmentLocalTranslation OTran = Client.GetSegmentTranslation(ObjectName, SegRootName);
      Debug.Log($"Occluded: {OTran.Occluded}, Rotation Success: {ORot.Result}, Translation Success: {OTran.Result}");

      if (ORot.Result == Result.Success && OTran.Result == Result.Success && !OTran.Occluded)
      {
        // Input data is in Vicon co-ordinate space; z-up, x-forward, rhs.
        // We need it in Unity space, y-up, z-forward lhs
        //           Vicon Unity
        // forward    x     z
        // up         z     y
        // right     -y     x
        // See https://gamedev.stackexchange.com/questions/157946/converting-a-quaternion-in-a-right-to-left-handed-coordinate-system

        Debug.Log($"Vicon Raw Rotation: ({ORot.Rotation[0]}, {ORot.Rotation[1]}, {ORot.Rotation[2]}, {ORot.Rotation[3]})");


        //Root.localRotation = new Quaternion(-(float)ORot.Rotation[1], (float)ORot.Rotation[2], (float)ORot.Rotation[0], (float)ORot.Rotation[3]);
        Root.localRotation = new Quaternion((float)ORot.Rotation[0], (float)ORot.Rotation[2], -(float)ORot.Rotation[1], (float)ORot.Rotation[3]);


        Debug.Log($"Converted Unity Rotation: {Root.localRotation.eulerAngles}");


        Root.localPosition = new Vector3(-(float)OTran.Translation[1] * 0.001f, (float)OTran.Translation[2] * 0.001f, (float)OTran.Translation[0] * 0.001f);
        
        m_LastGoodPosition = Root.localPosition;
        m_LastGoodRotation = Root.localRotation;
        m_bHasCachedPose = true;

        uint SubjectCount = Client.GetSubjectCount().SubjectCount;
        Debug.Log($"Total Subjects in Vicon: {SubjectCount}");
        for (uint i = 0; i < SubjectCount; i++)
        {
          string SubjectName = Client.GetSubjectName(i).SubjectName;
          Debug.Log($"Subject {i}: {SubjectName}");
        }

        PrintMarkerData();
      }
      else
      {
        if (m_bHasCachedPose)
        {
          Debug.LogWarning("Vicon data is occluded, using last good pose");
          Root.localRotation = m_LastGoodRotation;
          Root.localPosition = m_LastGoodPosition;
        }
      }

    }

    private void PrintMarkerData()
    {
      if (Client == null)
      {
        Debug.LogError("Vicon Client is null!");
        return;
      }

      // Ensure ObjectName is valid
      if (string.IsNullOrEmpty(ObjectName))
      {
        Debug.LogError("ObjectName is not set!");
        return;
      }
      // Count the number of markers
      uint MarkerCount = Client.GetMarkerCount(ObjectName).MarkerCount;
      Debug.Log($"Marker count for {ObjectName}: {MarkerCount}");
      Debug.Log($"Markers: ({MarkerCount}):");
      for (uint MarkerIndex = 0; MarkerIndex < MarkerCount; ++MarkerIndex)
      {
        // Get the marker name
        string MarkerName = Client.GetMarkerName(ObjectName, MarkerIndex).MarkerName;

        // Get the marker parent
        string MarkerParentName = Client.GetMarkerParentName(ObjectName, MarkerName).SegmentName;

        // Get the global marker translation
        Output_GetMarkerGlobalTranslation _Output_GetMarkerGlobalTranslation =
          Client.GetMarkerGlobalTranslation(ObjectName, MarkerName);

        Debug.Log($"      Marker #{MarkerIndex}: {MarkerName} ({_Output_GetMarkerGlobalTranslation.Translation[0]}, " +
                  $"{_Output_GetMarkerGlobalTranslation.Translation[1]}, {_Output_GetMarkerGlobalTranslation.Translation[2]}) " +
                  $"{_Output_GetMarkerGlobalTranslation.Occluded}");
      }
    }

  } //end of program
}// end of namespace

 */

 ﻿using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using ViconDataStreamSDK.CSharp;


namespace UnityVicon
{
    public class RBScript : MonoBehaviour
    {
        public string ObjectName = "";
        public ViconDataStreamClient Client;

        
        public Vector3 PositionOffset = Vector3.zero; // Default to no offset

        private Quaternion m_LastGoodRotation;
        private Vector3 m_LastGoodPosition;
        private bool m_bHasCachedPose = false;

        public RBScript()
        {
        }

        void Update()
        {
            Output_GetSubjectRootSegmentName OGSRSN = Client.GetSubjectRootSegmentName(ObjectName);
            string SegRootName = OGSRSN.SegmentName;

            Transform Root = transform; // This is the object the script is attached to.

            Output_GetSegmentLocalRotationQuaternion ORot = Client.GetSegmentRotation(ObjectName, SegRootName);
            Output_GetSegmentLocalTranslation OTran = Client.GetSegmentTranslation(ObjectName, SegRootName);

            if (ORot.Result == Result.Success && OTran.Result == Result.Success && !OTran.Occluded)
            {
                
                Vector3 viconTranslation = new Vector3((float)OTran.Translation[0], (float)OTran.Translation[1], (float)OTran.Translation[2]);
                
                Quaternion viconRotation = new Quaternion((float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[2], (float)ORot.Rotation[3]);

                
                Vector3 unityGlobalPosition = new Vector3(-viconTranslation.y * 0.001f, viconTranslation.z * 0.001f , viconTranslation.x * 0.001f);

                // Apply the user-defined position offset
                unityGlobalPosition += PositionOffset;

                
                Quaternion unityGlobalRotation = new Quaternion((float)ORot.Rotation[1], -(float)ORot.Rotation[2], -(float)ORot.Rotation[0], (float)ORot.Rotation[3]);


                // If the object has a parent in Unity, calculate local position and rotation
                if (Root.parent != null)
                {
                    // Convert global position to local position relative to the parent
                    Root.localPosition = Root.parent.InverseTransformPoint(unityGlobalPosition);

                    // Convert global rotation to local rotation relative to the parent
                    Root.localRotation = Quaternion.Inverse(Root.parent.rotation) * unityGlobalRotation;
                }
                else
                {
                    // If no parent, apply directly as global
                    Root.position = unityGlobalPosition; // Use .position for global
                    Root.rotation = unityGlobalRotation; // Use .rotation for global
                }

                // Cache the *global* position and rotation for when tracking is lost
                m_LastGoodPosition = Root.position; // Store actual world position
                m_LastGoodRotation = Root.rotation; // Store actual world rotation
                m_bHasCachedPose = true;
            }
            else
            {
                if (m_bHasCachedPose)
                {
                    
                    if (Root.parent != null)
                    {
                        Root.localPosition = Root.parent.InverseTransformPoint(m_LastGoodPosition);
                        Root.localRotation = Quaternion.Inverse(Root.parent.rotation) * m_LastGoodRotation;
                    }
                    else
                    {
                        Root.position = m_LastGoodPosition;
                        Root.rotation = m_LastGoodRotation;
                    }
                }
            }
        }
    } //end of program
}// end of namespace