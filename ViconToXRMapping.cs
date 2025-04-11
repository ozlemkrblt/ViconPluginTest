using System.Collections.Generic;
using UnityEngine.XR.Hands;

public static class ViconToXRMapping
{
    // Customize this based on your Vicon subject and segment naming
    public static readonly Dictionary<string, XRHandJointID> LeftHandJointMap = new Dictionary<string, XRHandJointID>()
    {
        { "l_hand_root", XRHandJointID.Wrist },
        { "l_thumb_1", XRHandJointID.ThumbMetacarpal },
        { "l_thumb_2", XRHandJointID.ThumbProximal },
        { "l_thumb_3", XRHandJointID.ThumbDistal },
        { "l_index_1", XRHandJointID.IndexMetacarpal },
        { "l_index_2", XRHandJointID.IndexProximal },
        { "l_index_3", XRHandJointID.IndexIntermediate },
        { "l_index_4", XRHandJointID.IndexDistal },
        { "l_middle_1", XRHandJointID.MiddleMetacarpal },
        { "l_middle_2", XRHandJointID.MiddleProximal },
        { "l_middle_3", XRHandJointID.MiddleIntermediate },
        { "l_middle_4", XRHandJointID.MiddleDistal },
        { "l_ring_1", XRHandJointID.RingMetacarpal },
        { "l_ring_2", XRHandJointID.RingProximal },
        { "l_ring_3", XRHandJointID.RingIntermediate },
        { "l_ring_4", XRHandJointID.RingDistal },
        { "l_pinky_1", XRHandJointID.LittleMetacarpal },
        { "l_pinky_2", XRHandJointID.LittleProximal },
        { "l_pinky_3", XRHandJointID.LittleIntermediate },
        { "l_pinky_4", XRHandJointID.LittleDistal }
    };

    public static readonly Dictionary<string, XRHandJointID> RightHandJointMap = new Dictionary<string, XRHandJointID>()
    {
        { "r_hand_root", XRHandJointID.Wrist },
        { "r_thumb_1", XRHandJointID.ThumbMetacarpal },
        { "r_thumb_2", XRHandJointID.ThumbProximal },
        { "r_thumb_3", XRHandJointID.ThumbDistal },
        { "r_index_1", XRHandJointID.IndexMetacarpal },
        { "r_index_2", XRHandJointID.IndexProximal },
        { "r_index_3", XRHandJointID.IndexIntermediate },
        { "r_index_4", XRHandJointID.IndexDistal },
        { "r_middle_1", XRHandJointID.MiddleMetacarpal },
        { "r_middle_2", XRHandJointID.MiddleProximal },
        { "r_middle_3", XRHandJointID.MiddleIntermediate },
        { "r_middle_4", XRHandJointID.MiddleDistal },
        { "r_ring_1", XRHandJointID.RingMetacarpal },
        { "r_ring_2", XRHandJointID.RingProximal },
        { "r_ring_3", XRHandJointID.RingIntermediate },
        { "r_ring_4", XRHandJointID.RingDistal },
        { "r_pinky_1", XRHandJointID.LittleMetacarpal },
        { "r_pinky_2", XRHandJointID.LittleProximal },
        { "r_pinky_3", XRHandJointID.LittleIntermediate },
        { "r_pinky_4", XRHandJointID.LittleDistal }
    };
}
