
using UnityEngine;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using ViconDataStreamSDK.CSharp;

namespace UnityVicon
{

public class SubjectScriptwMarkerMapping : MonoBehaviour
{
  public string SubjectName = "";

  private bool IsScaled = true;
  public ViconDataStreamClient Client;

  Dictionary<string, string> markerToSegment = new Dictionary<string, string>()
{
  { "R_IndexMetacarpal", "R_IndexMetacarpal" },
  { "R_IndexProximal", "R_IndexMetacarpal" },
  { "R_IndexIntermediate", "R_IndexMetacarpal" },
  { "R_IndexDistal", "R_IndexMetacarpal" },
  { "R_IndexTip", "R_IndexMetacarpal" },
  { "R_MiddleMetacarpal", "R_MiddleMetacarpal" },
  { "R_MiddleProximal", "R_MiddleMetacarpal" },
  { "R_MiddleIntermediate", "R_MiddleMetacarpal" },
  { "R_MiddleDistal", "R_MiddleMetacarpal" },
  { "R_MiddleTip", "R_MiddleMetacarpal" },
  { "R_RingMetacarpal", "R_RingMetacarpal" },
  { "R_RingProximal", "R_RingMetacarpal" },
  { "R_RingIntermediate", "R_RingMetacarpal" },
  { "R_RingDistal", "R_RingMetacarpal" },
  { "R_RingTip", "R_RingMetacarpal" },
  { "R_LittleMetacarpal", "R_LittleMetacarpal" },
  { "R_LittleProximal", "R_LittleMetacarpal" },
  { "R_LittleIntermediate", "R_LittleMetacarpal" },
  { "R_LittleDistal", "R_LittleMetacarpal" },
  { "R_LittleTip", "R_LittleMetacarpal" },
  { "R_ThumbMetacarpal", "R_ThumbMetacarpal" },
  { "R_ThumbProximal", "R_ThumbMetacarpal" },
  { "R_ThumbDistal", "R_ThumbMetacarpal" },
  { "R_ThumbTip", "R_ThumbMetacarpal" },
  { "R_Wrist", "R_Wrist" },
  {"R_Palm", "R_Wrist"},
  // Add more mappings as necessary
};
  public SubjectScriptwMarkerMapping()
  {
  }

  void LateUpdate()
  {
    // Update all segments ( existing method):
    Output_GetSubjectRootSegmentName OGSRSN = Client.GetSubjectRootSegmentName(SubjectName);
    Transform Root = transform.root;
    //FindAndTransform(Root, OGSRSN.SegmentName);

    // Now update markers
    foreach (var kvp in markerToSegment)
    {
      string markerName = kvp.Key;
      string segmentName = kvp.Value;
      Debug.Log($"Processing marker: {markerName} for segment: {segmentName}");

      //  Get marker position
      Output_GetMarkerGlobalTranslation markerData = Client.GetMarkerGlobalTranslation(SubjectName, markerName);
      Debug.Log($"Marker {markerName} Position: {markerData.Translation[0]}, {markerData.Translation[1]}, {markerData.Translation[2]}");

      if (markerData.Result != Result.Success)
      {
        Debug.LogWarning($"No marker data found for {markerName}");
        continue;
      }

      Vector3 markerPosition = new Vector3(
          (float)markerData.Translation[0] * 0.01f,
          (float)markerData.Translation[1] * 0.01f,
          (float)markerData.Translation[2] * 0.01f
      );
      Vector3 unityMarkerPosition = new Vector3(
          -markerPosition.x,  // Vicon X → - Unity X
          markerPosition.z,  // Vicon Z → Unity Y
          markerPosition.y   // Vicon Y → Unity Z(-?)
      );

      // Find the parent segment
      Transform segmentTransform = FindSegment(transform, segmentName);
      if (segmentTransform == null)
      {
        Debug.LogWarning($"Segment {segmentName} not found for marker {markerName}");
        continue;
      }

      // Find or create the marker GameObject
      Transform markerTransform = segmentTransform.Find(markerName);
      if (markerTransform == null)
      {
        GameObject markerObj = new GameObject(markerName);
        markerTransform = markerObj.transform;
        markerTransform.SetParent(segmentTransform);
      }

      markerTransform.position = markerPosition;


      // No rotation for markers — inherit from segment
      markerTransform.rotation = segmentTransform.rotation;
    }
  }




  Transform FindSegment(Transform current, string segmentName)
  {
    if (strip(current.name) == segmentName)
      return current;

    foreach (Transform child in current)
    {
      Transform found = FindSegment(child, segmentName);
      if (found != null)
        return found;
    }

    return null;
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

    // update the bone transform from the data stream
    Output_GetSegmentLocalRotationQuaternion ORot = Client.GetSegmentRotation(SubjectName, BoneName);
    Debug.Log($"Rotation Status: {ORot.Result} {BoneName}");

    if (ORot.Result == Result.Success)
    {

      Debug.Log($"Raw Vicon Rotation: X={ORot.Rotation[0]}, Y={ORot.Rotation[1]}, Z={ORot.Rotation[2]}, W={ORot.Rotation[3]}");

      // mapping back to default data stream axis
      //Quaternion Rot = new Quaternion(-(float)ORot.Rotation[2], -(float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[3]);
      Quaternion Rot = new Quaternion((float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[2], (float)ORot.Rotation[3]);
      // mapping right hand to left hand flipping x
      Bone.localRotation = new Quaternion(Rot.x, Rot.z, Rot.y, -Rot.w);

      Debug.Log($"Applied Rotation: {Bone.name} -> {Bone.localRotation}");
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
      Debug.Log($"Raw Vicon Translation: X={OTran.Translation[0]}, Y={OTran.Translation[1]}, Z={OTran.Translation[2]}");
      //Vector3 Translate = new Vector3(-(float)OTran.Translation[2] * 0.001f, -(float)OTran.Translation[0] * 0.001f, (float)OTran.Translation[1] * 0.001f);
      Vector3 Translate = new Vector3((float)OTran.Translation[0] * 0.01f, (float)OTran.Translation[1] * 0.01f, (float)OTran.Translation[2] * 0.01f);
      Bone.localPosition = new Vector3(Translate.x, Translate.z, Translate.y);

      Debug.Log($"Applied Translation: {Bone.name} -> {Bone.localPosition}");

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
} //end of program 
} // end of namespace