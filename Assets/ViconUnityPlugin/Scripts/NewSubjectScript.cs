using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem; // For keyboard input in debug trigger
using ViconDataStreamSDK.CSharp;

/// 
/// NewSubjectScript extends the functionality of SubjectScript by:
/// - Adding support for a PositionOffset to adjust global positions.
/// - Caching the root segment name (m_RootSegmentName) for better handling of root vs. child transforms.
/// - Applying transforms differently for root and child segments, ensuring accurate hierarchy mapping.
/// - Improved handling of occluded data by distinguishing root and child segments during fallback.
/// - Uses external ViconFileLogger for clean background logging.
/// This script is more modular and robust for complex subject hierarchies.
/// 

namespace Assets.ViconUnityPlugin.Scripts
{
    public class NewSubjectScript : MonoBehaviour
    {
        public string SubjectName = "";

        // set it to false
        private readonly bool IsScaled = false;

        public ViconDataStreamClient Client;

        //Added for caching the last good pose
        private Dictionary<string, Quaternion> m_LastGoodRotations = new Dictionary<string, Quaternion>();
        private Dictionary<string, Vector3> m_LastGoodPositions = new Dictionary<string, Vector3>();

        public Vector3 PositionOffset = Vector3.zero; // Default to no offset , added to SubjectScript.cs

        private string m_RootSegmentName; // New:  used in transform logic to distinguish root from child segments

        // timer for debug
        private bool m_SpaceTimerActive = false;
        private float m_SpaceTimerStart = 0f;

        private ViconFileLogger m_Logger; // New: instance of the ViconFileLogger to manage logging

        private bool m_HasPrintedHierarchy = false; // New : flag to ensure we only print the hierarchy once for debugging
        void Start()
        {
            // Initialize ViconLogger
            m_Logger = new ViconFileLogger();
            m_Logger.Initialize();
        }

        void LateUpdate()
        {
            if (Client == null)
            {
                Debug.LogError("[CONSOLE] Vicon Client is NULL in SubjectScript! Make sure it's initialized.");
                return;
            }

            if (string.IsNullOrEmpty(SubjectName))
            {
                Debug.LogError("[CONSOLE] SubjectName is NULL or EMPTY! Make sure it's set before calling LateUpdate.");
                return;
            }

            Output_GetSegmentCount OGSRSC = Client.GetSegmentCount(SubjectName);
            if (OGSRSC.Result != Result.Success)
            {
                Debug.LogError("Failed to get root segment count." + OGSRSC.Result);
                return;
            }
            else
            {
                Debug.Log($"Successfully retrieved segment count for Subject '{SubjectName}'.");
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
                Debug.Log($"Successfully retrieved Root Segment Name for subject '{SubjectName}'.");
                Debug.Log($"Subject Root Segment Name: {OGSRSN.SegmentName}");
            }
            Transform Root = transform.root;

            if (Root == null)
            {
                Debug.LogError("Transform root is NULL. Make sure the GameObject has a valid hierarchy.");
                return;
            }

            if (!m_HasPrintedHierarchy)
            {
                string fullHierarchy = GetDebugHierarchy(Root);
                Debug.Log($"[CONSOLE] Subject Segment Hierarchy: \n -------------------------- \n {fullHierarchy} \n --------------------------");

                m_HasPrintedHierarchy = true;
            }

            //Debug here or below?
            FindAndTransform(Root, OGSRSN.SegmentName);

            // Checking the subject count and names in the Vicon system for debugging purposes. Can be commented out if not needed.
            //uint SubjectCount = Client.GetSubjectCount().SubjectCount;
            //Debug.Log($"Total Subjects in Vicon: {SubjectCount}");

            //for (uint i = 0; i < SubjectCount; i++)
            //{
            //    string currentSubjectName = Client.GetSubjectName(i).SubjectName;
            //    Debug.Log($"Subject {i}: {currentSubjectName}"); 
            //}

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (!m_SpaceTimerActive)
                {
                    // --- TURN ON ---
                    m_SpaceTimerActive = true;
                    m_SpaceTimerStart = Time.realtimeSinceStartup;

                    Debug.Log("[CONSOLE] --- MANUAL DEBUG TRIGGERED ---"); //ADD SPECIAL TAG TO MAKE THIS STAND OUT IN THE LOGS
                    Debug.Log($"[CONSOLE] Logs located at:{Application.persistentDataPath}");
                    m_Logger.AppendLog($"START,{DateTime.Now:O},{m_SpaceTimerStart:F6}");
                }
                else
                {
                    m_SpaceTimerActive = false;
                    float elapsed = Time.realtimeSinceStartup - m_SpaceTimerStart;

                    Debug.Log("[CONSOLE] --- MANUAL DEBUG FINISHED ---"); //ADD SPECIAL TAG TO MAKE THIS STAND OUT IN THE LOGS
                    Debug.Log($"Timer STOPPED. Elapsed: {elapsed:F6} seconds");
                    m_Logger.AppendLog($"STOP,{DateTime.Now:O},{elapsed:F6}");
                }

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

                Debug.Log("--------------------------");
                Debug.Log("Marker Data:");
                PrintMarkerData();
                Debug.Log("--------------------------");
            }
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
                Debug.Log($"Checking child name: {Child.name}");

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

            // --- ROTATION ---

            //Output_GetSegmentLocalRotationQuaternion ORot = Client.GetSegmentRotation(SubjectName, BoneName);
            Output_GetSegmentGlobalRotationQuaternion ORot = Client.GetSegmentGlobalRotationQuaternion(SubjectName, BoneName);

            Debug.Log($"Rotation Status: {ORot.Result}");

            if (ORot.Result == Result.Success)
            {

                Debug.Log($"Raw Vicon Rotation: X={ORot.Rotation[0]}, Y={ORot.Rotation[1]}, Z={ORot.Rotation[2]}, W={ORot.Rotation[3]}");

                //old : 
                //Quaternion Rot = new Quaternion((float)ORot.Rotation[0], (float)ORot.Rotation[1], (float)ORot.Rotation[2], (float)ORot.Rotation[3]);
              //Quaternion globalRot = new Quaternion(
              //ot.x,  // Vicon Y → Unity X (negated)
              //ot.z,  // Vicon Z → Unity Y
              //t.y,  // Vicon X → Unity Z (negated)
              //ot.w   // W stays the same
              //);
    
                    //Corrected Mapping for Unity
                    Quaternion globalRot = new Quaternion(
                         (float)ORot.Rotation[0],  // Vicon X → Unity X 
                         (float)ORot.Rotation[2],   // Vicon Z → Unity Y
                         -(float)ORot.Rotation[1],   // Vicon Y → Unity Z (negated)
                         (float)ORot.Rotation[3]    // W stays the same
          );

                //if (Bone.parent != null
                //&& BoneName != m_RootSegmentName
                //)
                //{
                //    Bone.localRotation = Quaternion.Inverse(Bone.parent.rotation) * globalRot;
                //    Debug.Log($"Applying Local Rotation: {Bone.name} -> {Bone.localRotation}");
                //}
                //else
                //{
                    Bone.rotation = globalRot;
                    Debug.Log($"Applying Rotation: {Bone.name} -> {Bone.rotation}");
                //}

                m_LastGoodRotations[BoneName] = globalRot;
            }
            else if (m_LastGoodRotations.ContainsKey(BoneName)) //For occluded data, uses cached pose and applies it differently for root and child segments (using parent transforms for children).
            {
                Debug.LogWarning("Vicon data is occluded, using last good pose");
               //if (Bone.parent != null
               //&& Bone.name != m_RootSegmentName
               //)
               //{
               //    Bone.localRotation = Quaternion.Inverse(Bone.parent.rotation) * m_LastGoodRotations[BoneName];
               //}
               //else
               //{
                    Bone.rotation = m_LastGoodRotations[BoneName];
                //}

            }

            // ----TRANSLATION----
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

                // 1.Choice: Applies position differently for root vs. child segments:
                //  •	For root: sets Bone.position.
                //  •	For children: sets Bone.localPosition using parent transforms.

                //if (Bone.parent != null
                //&& BoneName != m_RootSegmentName
                //)
                //{
                //    Bone.localPosition = Bone.parent.InverseTransformPoint(globalPosition);
                //    Debug.Log($"Applying Local Position: {Bone.name} -> {Bone.localPosition}");
                //}
                //else
                //{
                //    Bone.position = globalPosition;
                //    Debug.Log($"Applying Position: {Bone.name} -> {Bone.position}");
                //}

                //m_LastGoodPositions[BoneName] = globalPosition;


                // 2.Choice: Only apply translation if it is the ROOT segment. 
                // Children are physically attached hinges, so we ignore their translation
                // and let Unity's hierarchy keep them glued to the parent.
                if (BoneName == m_RootSegmentName)
                {
                    Bone.position = globalPosition;
                    m_LastGoodPositions[BoneName] = globalPosition;
                    Debug.Log($"Applying Root Position: {Bone.name} -> {Bone.position}");
                }

            }
            else if (m_LastGoodPositions.ContainsKey(BoneName))
            {
                Debug.LogWarning("Vicon data is occluded, using last good pose");
                if (Bone.parent != null
                && Bone.name != m_RootSegmentName
                )
                {
                    Bone.localPosition = Bone.parent.InverseTransformPoint(m_LastGoodPositions[BoneName]);
                }
                else
                {
                    Bone.position = m_LastGoodPositions[BoneName];
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


        string GetDebugHierarchy(Transform root)
        {
            StringBuilder sb = new StringBuilder();
            BuildHierarchyString(root, sb, 0);
            return sb.ToString(); // Returns the completely built string
        }

        void BuildHierarchyString(Transform root, StringBuilder sb, int depth)
        {
            string indent = new string('-', depth * 2);

            // AppendLine adds the text and automatically drops down to the next line
            sb.AppendLine($"{indent} {root.name} (Children: {root.childCount})");

            for (int i = 0; i < root.childCount; i++)
            {
                BuildHierarchyString(root.GetChild(i), sb, depth + 1);
            }
        }


        void OnDestroy()
        {
            // Make sure the logger correctly restores the Unity console and frees the file when the script
            // is destroyed
            m_Logger?.Dispose();
        }
    } //end of program
}// end of namespace
