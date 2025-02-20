# USING UNITY's QUATERNION CONVERSION

Quaternion viconQuat = new Quaternion(
    (float)ORot.Rotation[0], 
    (float)ORot.Rotation[1], 
    (float)ORot.Rotation[2], 
    (float)ORot.Rotation[3]
);

// Convert from Right-Handed (Vicon) to Left-Handed (Unity)
Quaternion unityQuat = new Quaternion(viconQuat.y, -viconQuat.z, -viconQuat.x, viconQuat.w);

Root.localRotation = unityQuat;
