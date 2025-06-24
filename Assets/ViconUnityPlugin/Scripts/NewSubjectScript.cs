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
    public class NewSubjectScript : MonoBehaviour
    {
        public string SubjectName = "";

        private bool IsScaled = true;

        public ViconDataStreamClient Client;

        //Added for caching the last good pose
        private Quaternion m_LastGoodRotation;
        private Vector3 m_LastGoodPosition;
        private bool m_bHasCachedPose = false;
        public Vector3 PositionOffset = Vector3.zero; // Default to no offset

        private string m_RootSegmentName;

        public NewSubjectScript()
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
            m_RootSegmentName = OGSRSN.SegmentName;

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

            Debug.Log($"Rotation Status: {ORot.Result}");

            if (ORot.Result == Result.Success)
            {

                Debug.Log($"Raw Vicon Rotation: X={ORot.Rotation[0]}, Y={ORot.Rotation[1]}, Z={ORot.Rotation[2]}, W={ORot.Rotation[3]}");

                Quaternion Rot = new Quaternion((float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[2], (float)ORot.Rotation[3]);
                Quaternion globalRot = new Quaternion(
              -Rot.x,  // Vicon Y → Unity X (negated)
               Rot.z,  // Vicon Z → Unity Y
              Rot.y,  // Vicon X → Unity Z (negated)
              -Rot.w   // W stays the same
          );

                if (Bone.parent != null 
                //&& Bone.name != m_RootSegmentName
                )
                {
                    Bone.localRotation = Quaternion.Inverse(Bone.parent.rotation) * globalRot;
                    Debug.Log($"Applying Local Rotation: {Bone.name} -> {Bone.localRotation}");
                }
                else
                {
                    Bone.rotation = globalRot;
                    Debug.Log($"Applying Rotation: {Bone.name} -> {Bone.rotation}");
                }

                m_LastGoodRotation = globalRot;
                m_bHasCachedPose = true;
            }
            else if (m_bHasCachedPose)
            {
                Debug.LogWarning("Vicon data is occluded, using last good pose");
                if (Bone.parent != null 
                //&& Bone.name != m_RootSegmentName
                )
                {
                    Bone.localRotation = Quaternion.Inverse(Bone.parent.rotation) * m_LastGoodRotation;
                }
                else
                {
                    Bone.rotation = m_LastGoodRotation;
                }

            }

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
                Debug.Log($"Raw Vicon Translation: X={OTran.Translation[0]}, Y={OTran.Translation[1]}, Z={OTran.Translation[2]}");

                Vector3 Translate = new Vector3((float)OTran.Translation[0] * 0.001f, (float)OTran.Translation[1] * 0.001f, (float)OTran.Translation[2] * 0.001f);

                Debug.Log($"Local Pos Before: {Bone.localPosition}");
                Debug.Log($"World Pos Before: {Bone.position}");

                //Corrected Mapping for Unity
                Vector3 globalPosition = new Vector3(
                     -Translate.x,  // Vicon X → Unity X
                     Translate.z,   // Vicon Z → Unity Y
                     Translate.y    // Vicon Y → Unity Z
                  );

                globalPosition += PositionOffset;



                if (Bone.parent != null 
                //&& BoneName != m_RootSegmentName
                )
                {
                    Bone.localPosition = Bone.parent.InverseTransformPoint(globalPosition);
                    Debug.Log($"Applying Local Position: {Bone.name} -> {Bone.localPosition}");
                }
                else
                {
                    Bone.position = globalPosition;
                    Debug.Log($"Applying Position: {Bone.name} -> {Bone.position}");
                }

            }
            else if (m_bHasCachedPose)
            {
                Debug.LogWarning("Vicon data is occluded, using last good pose");
                if (Bone.parent != null 
                //&& Bone.name != m_RootSegmentName
                )
                {
                    Bone.localPosition = Bone.parent.InverseTransformPoint(m_LastGoodPosition);
                }
                else
                {
                    Bone.rotation = m_LastGoodRotation;
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
