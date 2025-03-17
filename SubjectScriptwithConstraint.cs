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

    //Added for caching the last good pose

    private Quaternion m_LastGoodRotation;
    private Vector3 m_LastGoodPosition;
    private bool m_bHasCachedPose = false;
    Vector3 SpinePosition;
    private Quaternion m_InitialRotation;  // To store the initial static rotation
    private bool m_bHasInitialRotation = false; // Flag to check if initial rotation is set
    private Vector3 m_InitialPosition;  // To store the initial static position
    private bool m_bHasInitialPosition = false; // Flag to check if initial position is set
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

      FindAndTransform(Root, OGSRSN.SegmentName);

      //DEBUGGING
      uint SubjectCount = Client.GetSubjectCount().SubjectCount;
      Debug.Log($"Total Subjects in Vicon: {SubjectCount}");

      for (uint i = 0; i < SubjectCount; i++)
      {
        string currentSubjectName = Client.GetSubjectName(i).SubjectName;
        Debug.Log($"Subject {i}: {currentSubjectName}");
      }

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

      if (BoneName == "Spine")
      {
        SpinePosition = Bone.localPosition;
      }
      Output_GetSegmentLocalRotationQuaternion ORot = Client.GetSegmentRotation(SubjectName, BoneName);
                    // Retrieve the current rotation of the segment
        Output_GetSegmentStaticRotationQuaternion ORotS = Client.GetSegmentStaticRotationQuaternion(SubjectName, BoneName);
        if (ORotS.Result == Result.Success)
        {
            // If it's the first frame or if we haven't captured the initial rotation yet, set it
            if (!m_bHasInitialRotation)
            {
                // Capture the initial static rotation as the subject's standing rotation
                m_InitialRotation = new Quaternion(
                    -(float)ORot.Rotation[1],  
                    (float)ORot.Rotation[2],  
                    -(float)ORot.Rotation[0],  
                    (float)ORot.Rotation[3]    
                );

                m_bHasInitialRotation = true; // Mark initial rotation as set
                Debug.Log($"Initial Rotation Set: {m_InitialRotation}");
            }

            // Compare the current rotation with the initial rotation to check if it has changed
            Quaternion currentRotation = new Quaternion(
                -(float)ORot.Rotation[1],  // Vicon Z → Unity X (negated)
                (float)ORot.Rotation[2],  // Vicon X → Unity Y (negated)
                -(float)ORot.Rotation[0],   // Vicon Y → Unity Z
                (float)ORot.Rotation[3]    // W component remains the same
            );

            // Check for significant changes in rotation (you can adjust the threshold as needed)
            if (Quaternion.Angle(m_InitialRotation, currentRotation) > 1.0f)  // Example threshold: 1 degree
            {
                // Update the rotation if the angle difference exceeds the threshold
                Debug.Log("Subject has rotated. Updating rotation...");
                m_InitialRotation = currentRotation;  // Store the new rotation if needed
            }
            else
            {
                // Subject is considered to be in the same position
                Debug.Log("Subject has not rotated significantly.");
            }

            Bone.localRotation = m_InitialRotation;  // Apply the rotation
        }



  //     Debug.Log($"Rotation Status: {ORot.Result}");

  //     if (ORot.Result == Result.Success)
  //     {

  //       //Default Conversion:
  //       //Bone.localRotation = new Quaternion(-(float)ORot.Rotation[2], -(float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[3]);

  //       // Rotate the bone in Unity space
  //       // Input data is in Vicon co-ordinate space; z-up, x-forward, rhs.
  //       // We need it in Unity space, y-up, z-forward lhs
  //       //           Vicon Unity
  //       // forward    x     z
  //       // up         z     y
  //       // right     -y     x
  //       // See https://gamedev.stackexchange.com/questions/157946/converting-a-quaternion-in-a-right-to-left-handed-coordinate-system 
  //       Debug.Log($"Raw Vicon Rotation: X={ORot.Rotation[0]}, Y={ORot.Rotation[1]}, Z={ORot.Rotation[2]}, W={ORot.Rotation[3]}");
  //       // Bone.localRotation = new Quaternion(
  //       // -(float)ORot.Rotation[2], // Vicon z to Unity x (negated)
  //       // -(float)ORot.Rotation[0], // Vicon x to Unity y (negated)
  //       // (float)ORot.Rotation[1],  // Vicon y to Unity z
  //       // (float)ORot.Rotation[3]   // w component remains the same
  //       // );

  //       //Conversion according to the vicon calibration data:
  //       //Bone.localRotation = new Quaternion(
  //       //(float)ORot.Rotation[0], // Vicon x to Unity x 
  //       //(float)ORot.Rotation[2], // Vicon z to Unity y 
  //       //-(float)ORot.Rotation[1],  // Vicon y to Unity z (negate)
  //       //(float)ORot.Rotation[3]   // w component remains the same
  //       //);


  //       Quaternion Rot = new Quaternion((float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[2], (float)ORot.Rotation[3]);

  //       // Usage in other codes:
  //       //Bone.localRotation = new Quaternion(Rot.x, -Rot.y, -Rot.z, Rot.w);
  //       Bone.localRotation = new Quaternion(
  //     -Rot.y,  // Vicon Y → Unity X (negated)
  //      Rot.z,  // Vicon Z → Unity Y
  //     -Rot.x,  // Vicon X → Unity Z (negated)
  //      Rot.w   // W stays the same
  // );


  //       Debug.Log($"Applying Rotation: {Bone.name} -> {Bone.localRotation}");
  //       m_LastGoodRotation = Bone.localRotation;
  //       m_bHasCachedPose = true;
  //     }
  //     else if (m_bHasCachedPose)
  //     {
  //       Debug.LogWarning("Vicon data is occluded, using last good pose");
  //       Bone.localRotation = m_LastGoodRotation;

  //     }
              Output_GetSegmentStaticTranslation OTS = Client.GetSegmentStaticTranslation(SubjectName, BoneName);

      Output_GetSegmentLocalTranslation OTran;
      if (IsScaled)
      {
        Debug.Log("Using Scaled Translation");
        OTran = Client.GetScaledSegmentTranslation(SubjectName, BoneName);
      }
      else
      {
        OTran = Client.GetSegmentTranslation(SubjectName, BoneName);
      }

      Debug.Log($"Occluded: {OTran.Occluded},Translation Success: {OTran.Result}");
      
      if (OTran.Result == Result.Success)
      {
          if (!m_bHasInitialPosition)
            {
                // Capture the initial static position as the subject's standing position
                m_InitialPosition = new Vector3(
                    -(float)OTS.Translation[0] * 0.001f,  // X component
                    (float)OTS.Translation[2] * 0.001f ,  // Y component
                    (float)OTS.Translation[1] * 0.001f  // Z component
                );

                m_bHasInitialPosition = true; // Mark initial position as set
                Debug.Log($"Initial Position Set: {m_InitialPosition}");
            }

            // Compare the current translation with the initial position to check if it has moved
            Vector3 currentPosition = new Vector3(
                (float)OTS.Translation[0] * 0.001f,  // X component
                -(float)OTS.Translation[2] * 0.001f,  // Y component
                -(float)OTS.Translation[1] * 0.001f   // Z component
            );

            // Check for significant movement (you can adjust the threshold as needed)
            if (Vector3.Distance(m_InitialPosition, currentPosition) > 0.05f)  // Example threshold: 5 cm
            {
                // Update the position if the movement exceeds the threshold
                Debug.Log("Subject has moved. Updating position...");
                m_InitialPosition = currentPosition;  // Store the new position if needed
            }
            else
            {
                // Subject is considered to be in the same position
                Debug.Log("Subject has not moved significantly.");
            }
                  Bone.localPosition = m_InitialPosition;

        // Input data is in Vicon co-ordinate space; z-up, x-forward, rhs.
        // We need it in Unity space, y-up, z-forward lhs
        //           Vicon Unity
        // forward    x     z 
        // up         z     y
        // right     -y     x
        // See https://gamedev.stackexchange.com/questions/157946/converting-a-quaternion-in-a-right-to-left-handed-coordinate-system

        //Debug.Log($"Raw Vicon Translation: X={OTran.Translation[0]}, Y={OTran.Translation[1]}, Z={OTran.Translation[2]}");

        //Default Conversion: 
        //Bone.localPosition = new Vector3(-(float)OTran.Translation[2] * 0.001f, -(float)OTran.Translation[0] * 0.001f, (float)OTran.Translation[1] * 0.001f);


        //Bone.localPosition = new Vector3(
        //   -(float)OTran.Translation[1] * 0.001f, // Vicon right (-y) to Unity x
        //    (float)OTran.Translation[2] * 0.001f,  // Vicon up (z) to Unity y
        // (float)OTran.Translation[0] * 0.001f   // Vicon forward (x) to Unity z
        //);

        //conversion according to the vicon calibration data:
        //Bone.localPosition = new Vector3( (float)OTran.Translation[0]* 0.001f, (float)OTran.Translation[2]* 0.001f, -(float)OTran.Translation[1]* 0.001f  );
       // Vector3 Translate = new Vector3((float)OTran.Translation[0] * 0.001f, (float)OTran.Translation[1] * 0.001f, (float)OTran.Translation[2] * 0.001f);
        //Usage in other codes:
        //Bone.localPosition =  new Vector3(-Translate.x, Translate.y, Translate.z);
        //Debug.Log($"Unity World Pos Before: {Bone.localPosition}");
        /**Test if unity works correct indepent of the vicon data: 
        Bone.localPosition += Vector3.right * 0.01f;**/

        // Corrected Mapping for Unity
       // Bone.localPosition = new Vector3(
       //     -Translate.x,  // Vicon X → Unity -X
       //     Translate.z,   // Vicon Z → Unity Y
       //     Translate.y    // Vicon Y → Unity Z
        //);
       // Debug.Log($"Unity World Pos After: {Bone.localPosition}");

        //Debug.Log($"localPosition: {Bone.name} -> {Bone.localPosition}");



       // Vector3 expectedOffset = Bone.localPosition - SpinePosition;

       // Debug.Log($"{BoneName} Offset from Spine: {expectedOffset}");
        ////////////////////////
        //         Vector3 worldPosition = new Vector3(-(float)OTran.Translation[0] * 0.001f, (float)OTran.Translation[2] * 0.001f, (float)OTran.Translation[1] * 0.001f);
        //         if (Bone.parent != null)
        //         {
        //           Bone.localPosition = Bone.parent.InverseTransformPoint(worldPosition);
        //         }
        //         else
        //         {
        //           Bone.position = worldPosition;
        //         }
        //         ///////////////////
        //         m_LastGoodPosition = Bone.localPosition;
        //         m_bHasCachedPose = true;

        //         if (BoneName == "Spine") {
        //     Vector3 spinePosition = new Vector3(
        //         (float)OTran.Translation[0] * 0.001f, 
        //         (float)OTran.Translation[1] * 0.001f, 
        //         (float)OTran.Translation[2] * 0.001f
        //     );
        //     Debug.Log($"Spine Position: {spinePosition}");
        // } 

        // if (BoneName == "Front" || BoneName == "Back") {
        //     Vector3 bonePosition = new Vector3(
        //         (float)OTran.Translation[0] * 0.001f, 
        //         (float)OTran.Translation[1] * 0.001f, 
        //         (float)OTran.Translation[2] * 0.001f
        //     );

        //     // Calculate offset from spine
        //     Vector3 expectedOffset = bonePosition - m_LastGoodPosition; // m_LastGoodPosition should store the last good spine position.
        //     Debug.Log($"{BoneName} Position: {bonePosition}");
        //     Debug.Log($"{BoneName} Offset from Spine: {expectedOffset}");
        // }
      }
      else if (m_bHasCachedPose)
      {
        Debug.LogWarning("Vicon data is occluded, using last good pose");
        Bone.localPosition = m_LastGoodPosition;

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

