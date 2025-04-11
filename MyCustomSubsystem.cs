using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
public class MyCustomSubsystem : XRHandSubsystem
{

    // Register the subsystem descriptor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterDescriptor()
    {
        var descriptorInfo = new XRHandSubsystemDescriptor.Cinfo
        {
            id = "MyCustomSubsystem",
            providerType = typeof(ViconHandProvider),
            subsystemTypeOverride = typeof(MyCustomSubsystem),
            supportsHandTracking = true,
            // Additional configuration as needed
        };
        XRHandSubsystemDescriptor.Register(descriptorInfo);
    }
}

