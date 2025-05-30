﻿using UnityEngine;
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

    //Added for caching the last good pose

    private Quaternion m_LastGoodRotation;
    private Vector3 m_LastGoodPosition;
    private bool m_bHasCachedPose = false;
    public SubjectScript()
    {
    }

    void Update()
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

      //DEBUGGING
      Output_GetSegmentCount OGSRSC = Client.GetSegmentCount(SubjectName);
      if (OGSRSC.Result != Result.Success)
      {
        Debug.LogError("Failed to get root segment count.");
        return;
      }
      else
      {

        Debug.Log($"Subject Total Segment Count: {OGSRSC.SegmentCount}");

      }

      //

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

      //DEBUGGING
      print("Subject Segment Hiearchy:");
      DebugHierarchy(Root);
      Output_GetSegmentChildCount OutputGSCC = Client.GetSegmentChildCount(SubjectName, OGSRSN.SegmentName);
      if (OutputGSCC.Result != Result.Success)
      {
        Debug.LogError($"Failed to get segment child count: {OutputGSCC.Result}");
        return;
      }
      Debug.Log($"Segment Child Count: {OutputGSCC.SegmentCount}");

      Output_GetSegmentChildName OutputGSCN = Client.GetSegmentChildName(SubjectName, OGSRSN.SegmentName, 0);
      if (OutputGSCN.Result != Result.Success)
      {
        Debug.LogError($"Failed to get segment child name: {OutputGSCN.Result}");
        return;
      }
      Debug.Log($"Segment Child Name: {OutputGSCN.SegmentName}");
      //

      FindAndTransform(Root, OGSRSN.SegmentName);

      //DEBUGGING
      uint SubjectCount = Client.GetSubjectCount().SubjectCount;
      Debug.Log($"Total Subjects in Vicon: {SubjectCount}");

      for (uint i = 0; i < SubjectCount; i++)
      {
        string currentSubjectName = Client.GetSubjectName(i).SubjectName;
        Debug.Log($"Subject {i}: {currentSubjectName}");
      }
      //
      //DebugHierarchy(Root); // Print hierarchy for debugging
      //PrintMarkerData();
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

    //TODO: Continue to debugging from this point. It seems like segment count, segment names, children of the root segment etc. all are retrieved correctly in LateUpdate().
    //However in this method, it cannot retrieve the children because it looks for transform's children, not the segment's children.
    void FindAndTransform(Transform iTransform, string BoneName)
    {
      //Debug.Log($"Transform: {iTransform.name}");

      int ChildCount = iTransform.childCount;
      Debug.Log($"Checking child count: {ChildCount}"); // Log all child names to check against BoneName
      for (int i = 0; i < ChildCount; ++i)
      {
        Transform Child = iTransform.GetChild(i);
        Debug.Log($"Checking child name: {Child.name}"); // Log all child names to check against BoneName

        if (strip(Child.name) == BoneName)
        {
          Debug.Log($"Found Bone: {BoneName}");
          ApplyBoneTransform(Child);
          TransformChildren(Child);
          break;
        }
        // if not finding root in this layer, try the children
        Debug.Log($"Checking children of {Child.name}");
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
      Debug.Log($"Rotation Success: {ORot.Result}");

      if (ORot.Result == Result.Success && ORot.Occluded == false)
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

        Bone.localRotation = new Quaternion(-(float)ORot.Rotation[2], -(float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[3]);
        Debug.Log($"Applying Rotation: {Bone.name} -> {Bone.localRotation}");
        m_LastGoodRotation = Bone.localRotation;
        m_bHasCachedPose = true;
      }
      else
      {
        if (m_bHasCachedPose)
        {
          Debug.LogWarning("Vicon data is occluded, using last good pose");
          Bone.localRotation = m_LastGoodRotation;
        }
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

      Debug.Log($"Occluded: {OTran.Occluded},Translation Success: {OTran.Result}");

      if (OTran.Result == Result.Success && !OTran.Occluded)
      {
        // Input data is in Vicon co-ordinate space; z-up, x-forward, rhs.
        // We need it in Unity space, y-up, z-forward lhs
        //           Vicon Unity
        // forward    x     z 
        // up         z     y
        // right     -y     x
        // See https://gamedev.stackexchange.com/questions/157946/converting-a-quaternion-in-a-right-to-left-handed-coordinate-system

        Debug.Log($"Raw Vicon Translation: X={OTran.Translation[0]}, Y={OTran.Translation[1]}, Z={OTran.Translation[2]}");
        Debug.Log($"Converted Position: {-(float)OTran.Translation[2] * 0.001f}, {-(float)OTran.Translation[0] * 0.001f}, {(float)OTran.Translation[1] * 0.001f}");

        Bone.localPosition = new Vector3(-(float)OTran.Translation[2] * 0.001f, -(float)OTran.Translation[0] * 0.001f, (float)OTran.Translation[1] * 0.001f);
        //test if unity works correctly : 

        //Bone.localPosition += Vector3.right * 0.01f;


        Debug.Log($"Applying Translation: {Bone.name} -> {Bone.localPosition}");

        m_LastGoodPosition = Bone.localPosition;
        m_bHasCachedPose = true;
      }
      else
      {
        if (m_bHasCachedPose)
        {
          Debug.LogWarning("Vicon data is occluded, using last good pose");
          Bone.localPosition = m_LastGoodPosition;
        }
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


    void DebugHierarchy(Transform root, int depth = 0)
    {

      string indent = new string('-', depth * 2);
      Debug.Log($"{indent} {root.name} (Children: {root.childCount})");

      for (int i = 0; i < root.childCount; i++)
      {
        DebugHierarchy(root.GetChild(i), depth + 1);
      }
    }


  } //end of program
}// end of namespace

