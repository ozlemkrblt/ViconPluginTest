using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Management;

public class ViconXRLoader : XRLoader
{

        public override bool Deinitialize();
        public override T GetLoadedSubsystem<T>() where T : class, ISubsystem;
        public override List<GraphicsDeviceType> GetSupportedGraphicsDeviceTypes(bool buildingPlayer);
        public override bool Initialize();
        public override bool Start();
        public override bool Stop();
}

/*
 *5. Configure the XR Settings
Ensure that your loader is recognized by Unity's XR system. This involves creating a UnitySubsystemsManifest.json file and placing it in the appropriate directory within your project. This manifest informs Unity about your provider and its associated subsystems.

File Structure:

bash
Copy
Edit
/Assets
    /XR
        /ViconHandTracking
            UnitySubsystemsManifest.json
            /Plugins
                /x86_64
                    libViconPlugin.so
UnitySubsystemsManifest.json:

json
Copy
Edit
{
    "name": "ViconHandTracking",
    "id": "com.yourcompany.viconhandtracking",
    "version": "1.0.0",
    "subsystems": [
        {
            "id": "ViconHandSubsystem",
            "subsystemType": "Hand",
            "implementationType": "ViconHandSubsystem",
            "providerType": "ViconHandProvider",
            "loaderType": "ViconXRLoader",
            "requiresSettings": false
        }
    ]
}

    */