using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using ViconDataStreamSDK.CSharp;


namespace UnityVicon
{
  public class SubjectScript : MonoBehaviour
  {
    public string SubjectName = "";

    private bool IsScaled = true;

    public ViconDataStreamClient Client;

    public SubjectScript()
    {
    }

    void LateUpdate()
    {
      if (Client == null)
      {
        Debug.LogError("Vicon Client is NULL in SubjectScript! Make sure it's initialized.");
        return;
      }

      if (string.IsNullOrEmpty(SubjectName))
      {
        Debug.LogError("SubjectName is NULL or EMPTY! Make sure it's set before calling LateUpdate.");
        return;
      }

      Output_GetSubjectRootSegmentName OGSRSN = Client.GetSubjectRootSegmentName(SubjectName);
      if (OGSRSN.Result != Result.Success)
      {
        Debug.LogError("Failed to get root segment name.");
        return;
      }
      if (string.IsNullOrEmpty(OGSRSN.SegmentName))
      {
        Debug.LogError("Root Segment Name is NULL or EMPTY! Make sure it's set before calling LateUpdate.");
        return;
      }
      else
      {

        Debug.Log($"Subject Root Segment Name: {OGSRSN.SegmentName}");

      }

      Transform Root = transform.root;
      if (Root == null)
      {
        Debug.LogError("Transform root is NULL. Make sure the GameObject has a valid hierarchy.");
        return;
      }

      FindAndTransform(Root, OGSRSN.SegmentName);
      uint SubjectCount = Client.GetSubjectCount().SubjectCount;
      Debug.Log($"Total Subjects in Vicon: {SubjectCount}");

      for (uint i = 0; i < SubjectCount; i++)
      {
        string SubjectName = Client.GetSubjectName(i).SubjectName;
        Debug.Log($"Subject {i}: {SubjectName}");
      }

      PrintMarkerData();


    }

    string strip(string BoneName)
    {
      if (BoneName.Contains(":"))
      {
        string[] results = BoneName.Split(':');
        return results[1];
      }
      return BoneName;
    }
    void FindAndTransform(Transform iTransform, string BoneName)
    {
      int ChildCount = iTransform.childCount;
      for (int i = 0; i < ChildCount; ++i)
      {
        Transform Child = iTransform.GetChild(i);
        Debug.Log($"Child: {Child.name}");
        if (strip(Child.name) == BoneName)
        {
          ApplyBoneTransform(Child);
          TransformChildren(Child);
          break;
        }
        // if not finding root in this layer, try the children
        FindAndTransform(Child, BoneName);
      }
    }
    void TransformChildren(Transform iTransform)
    {
      int ChildCount = iTransform.childCount;
      for (int i = 0; i < ChildCount; ++i)
      {
        Transform Child = iTransform.GetChild(i);
        ApplyBoneTransform(Child);
        TransformChildren(Child);
      }
    }
    // map the orientation back for forward

    private void ApplyBoneTransform(Transform Bone)
    {
      string BoneName = strip(Bone.gameObject.name);


      Output_GetSegmentLocalRotationQuaternion ORot = Client.GetSegmentRotation(SubjectName, BoneName);
      if (ORot.Result == Result.Success)
      {
        // mapping back to default data stream axis

        // Rotate the bone in Unity space
        // Input data is in Vicon co-ordinate space; z-up, x-forward, rhs.
        // We need it in Unity space, y-up, z-forward lhs
        //           Vicon Unity
        // forward    x     z
        // up         z     y
        // right     -y     x
        // See https://gamedev.stackexchange.com/questions/157946/converting-a-quaternion-in-a-right-to-left-handed-coordinate-system 

        Quaternion Rot = new Quaternion(-(float)ORot.Rotation[2], -(float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[3]);
                // mapping right hand to left hand flipping x
        Bone.localRotation = new Quaternion(-Rot.x, Rot.y, Rot.z, -Rot.w);
        Debug.Log($"Applying Rotation: {Bone.name} -> {Bone.localRotation}");
        
      }

      Output_GetSegmentLocalTranslation OTran;
      if (IsScaled)
      {
        OTran = Client.GetScaledSegmentTranslation(SubjectName, BoneName);
      }
      else
      {
        OTran = Client.GetSegmentTranslation(SubjectName, BoneName);
      }

      if (OTran.Result == Result.Success)
      {
        // Input data is in Vicon co-ordinate space; z-up, x-forward, rhs.
        // We need it in Unity space, y-up, z-forward lhs
        //           Vicon Unity
        // forward    x     z 
        // up         z     y
        // right     -y     x
        // See https://gamedev.stackexchange.com/questions/157946/converting-a-quaternion-in-a-right-to-left-handed-coordinate-system


        Vector3 Translate = new Vector3(-(float)OTran.Translation[2] * 0.001f, -(float)OTran.Translation[0] * 0.001f, (float)OTran.Translation[1] * 0.001f);
        Bone.localPosition = new Vector3(-Translate.x, Translate.y, Translate.z);
      }

      // If there's a scale for this subject in the datastream, apply it here.
      if (IsScaled)
      {
        Output_GetSegmentStaticScale OScale = Client.GetSegmentScale(SubjectName, BoneName);
        if (OScale.Result == Result.Success)
        {
          Bone.localScale = new Vector3((float)OScale.Scale[0], (float)OScale.Scale[1], (float)OScale.Scale[2]);
        }
      }
    }

    private void PrintMarkerData()
    {
      if (Client == null)
      {
        Debug.LogError("Vicon Client is NULL!");
        return;
      }

      if (string.IsNullOrEmpty(SubjectName))
      {
        Debug.LogError("SubjectName is not set!");
        return;
      }

      // Count the number of markers
      uint MarkerCount = Client.GetMarkerCount(SubjectName).MarkerCount;
      Debug.Log($"Marker count for {SubjectName}: {MarkerCount}\n");

      for (uint MarkerIndex = 0; MarkerIndex < MarkerCount; ++MarkerIndex)
      {
        // Get the marker name
        string MarkerName = Client.GetMarkerName(SubjectName, MarkerIndex).MarkerName;

        // Get the marker parent segment
        string MarkerParentName = Client.GetMarkerParentName(SubjectName, MarkerName).SegmentName;

        // Get the global marker translation
        Output_GetMarkerGlobalTranslation MarkerTranslation =
            Client.GetMarkerGlobalTranslation(SubjectName, MarkerName);


        Debug.Log($"Marker {MarkerIndex}: {MarkerName} | Parent: {MarkerParentName} | "
            + $"Position: ({MarkerTranslation.Translation[0]}, {MarkerTranslation.Translation[1]}, {MarkerTranslation.Translation[2]}) | "
            + $"Occluded: {MarkerTranslation.Occluded}");

      }
    }







  } //end of program
}// end of namespace

