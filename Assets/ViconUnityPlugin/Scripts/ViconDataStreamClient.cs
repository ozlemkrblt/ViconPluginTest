using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

using ViconDataStreamSDK.CSharp;


public class ViconDataStreamClient : MonoBehaviour
{

  [Tooltip("The hostname or ip address of the Datastream server.")]
  public string HostName = "localhost";

  [Tooltip("The Datastream port number. Typically 804 for the low latency stream and 801 if off-line review is required.")]
  public string Port = "801";

  [Tooltip("Enter a comma separated list of subjects that are required in the stream. If empty, all subjects will be transmitted.")]
  public string SubjectFilter;

  //Data retrieval modes:
  [Tooltip("Switches to the pre-fetch streaming mode, which will request new frames from the server as required while minimizing latency, rather than all frames being streamed. This can potentially help minimise the disruption of data delivery lags on the network. See the datastream documentation for more details of operation.")]
  public bool UsePreFetch = false;

  [Tooltip("Use retiming output mode. This can help to smooth out temporal artifacts due to differences between render and system frame rates.")]
  public bool IsRetimed = false;

  [Tooltip("Adds a fixed time offset to retimed output data. Only valid in retiming mode. Can be used to compensate for known render delays.")]
  public float Offset = 0;

  [Tooltip("Log timing information to a file.")]
  public bool Log = false;

  [Tooltip("Enable adapter settings to improve latency on wireless connections.")]
  public bool ConfigureWireless = true;

  private ViconDataStreamSDK.CSharp.Client m_Client;
  private ViconDataStreamSDK.CSharp.RetimingClient m_RetimingClient;

  private bool UseLightweightData = false;
  private bool GetFrameThread = true; //Controls frame retrieval in a separate thread.
  private static bool bConnected = false;
  private bool bSubjectFilterSet = false;

  private bool bThreadRunning = false; //Manages multi-threading.
  Thread m_Thread; //The actual background thread for connecting.
  



  /** Delegates are function pointers in C#. 
  * This allows OnConnected to be assigned dynamically. 
  * OnConnected: Updates bConnected when a connection is established.
  * ConnectionHandler: Stores the function pointer. 
@params i_bConnected: A boolean value that indicates whether a connection is established.
  **/
  public delegate void ConnectionCallback(bool i_bConnected);
  public static void OnConnected(bool i_bConnected)
  {
    bConnected = i_bConnected;
  }

  ConnectionCallback ConnectionHandler = OnConnected;

  /**
  * Sets up logging for the Vicon DataStream client by creating log files for the client and motion capture data.
  **/
  private void SetupLog() //Creates log files for the client and motion capture data.
  {
    String DateTime = System.DateTime.Now.ToString();
    DateTime = DateTime.Replace(" ", "_");
    DateTime = DateTime.Replace("/", "_");
    DateTime = DateTime.Replace(":", "_");
    String ClientPathName = Application.dataPath + "/../Logs/" + DateTime + "_ClientLog.csv";
    String StreamPathName = Application.dataPath + "/../Logs/" + DateTime + "_StreamLog.csv";

    bool bLogSuccess = false;
    if (IsRetimed)
    {
      bLogSuccess = m_RetimingClient.SetTimingLogFile(ClientPathName, StreamPathName).Result == Result.Success;
    }
    else
    {
      bLogSuccess = m_Client.SetTimingLogFile(ClientPathName, StreamPathName).Result == Result.Success;
    }

    if (bLogSuccess)
    {
      print("Writing log to " + ClientPathName + " and " + StreamPathName);
    }
    else
    {
      print("Failed to create logs: " + ClientPathName + ", " + StreamPathName);
    }
  }

  void Start()
  {
    m_Client = new Client(); //Initializes the Vicon client.
    m_RetimingClient = new RetimingClient();

    // If we're using the retimer, we don't want to use our own thread for getting frames.
    GetFrameThread = !IsRetimed;

    // if (ConfigureWireless) //Configures wireless settings.
    // {
    //   Output_ConfigureWireless WifiConfig = m_Client.ConfigureWireless();
    //   if (WifiConfig.Result != Result.Success)
    //   {
    //     print("Failed to configure wireless: " + WifiConfig.ToString());
    //   }
    //   else
    //   {
    //     print("Configured adapter for wireless settings");
    //   }
    // }
    print("Vicon Client Connected: " + m_Client.IsConnected());
    print("Starting...");
    Output_GetVersion OGV = m_Client.GetVersion(); //Retrieves and prints the Vicon DataStream SDK version.
    print("Using Datastream version " + OGV.Major + "." + OGV.Minor + "." + OGV.Point + "." + OGV.Revision);

    if (Log)
    {
      SetupLog();
    }

    m_Thread = new Thread(ConnectClient); //Starts a background thread (m_Thread) to connect.
    m_Thread.Start();
  }

  void OnValidate()
  {
    if (bConnected)
    {
      if (bThreadRunning)
      {
        bThreadRunning = false;
        m_Thread.Join();

        DisConnect();
        m_Thread = new Thread(ConnectClient);
        m_Thread.Start();
      }
    }
  }

  /**
  * Disconnects the Vicon DataStream client and retiming client.
  **/
  void DisConnect()
  {
    if (m_RetimingClient.IsConnected().Connected)
    {
      m_RetimingClient.Disconnect();
    }
    if (m_Client.IsConnected().Connected)
    {
      m_Client.Disconnect();
    }
  }

  /**
  * Connects to the Vicon DataStream server using the specified hostname and port.
  * Handles connecting to multiple Vicon hosts and configures the streaming mode.
  **/
  private void ConnectClient() //Handles connecting to Multiple Vicon hosts.
  {
    // Set the thread running flag to true, indicating the connection process has started

    bThreadRunning = true;
    //Step 1: Build a combined hostname string:
    //***************************************************

    // We have to handle the multi-route syntax, which is of the form HostName1:Port;Hostname2:Port
    String CombinedHostnameString = "";
    String[] Hosts = HostName.Split(';');
    foreach (String Host in Hosts)
    {
      String TrimmedString = Host.Trim();
      String HostWithPort = null;

      // Check whether the hostname already contains a port and add if it doesn't
      if (TrimmedString.Contains(":"))
      {
        HostWithPort = TrimmedString;
      }
      else
      {
        HostWithPort = TrimmedString + ":" + Port;
      }

      if (!String.IsNullOrEmpty(CombinedHostnameString))
      {
        CombinedHostnameString += ";";
      }

      CombinedHostnameString += HostWithPort;
    }

    print("Connecting to " + CombinedHostnameString + "...");

    if (IsRetimed)
    {
      //Step 2: Attempt connection in a loop until successful or thread stops
      //*************************************************
      while (bThreadRunning == true && !m_RetimingClient.IsConnected().Connected)
      {
        // Try to connect to the Vicon server using the combined hostname

        Output_Connect OC = m_RetimingClient.Connect(CombinedHostnameString);
        // Wait for 200 milliseconds before retrying to prevent spamming the connection request

        print("Connect result: " + OC.Result);

        System.Threading.Thread.Sleep(200);
      }

      print("Connected using retimed client.");

      if (UseLightweightData)
      {
        // Retiming client will have segment data enabled by default
        if (m_RetimingClient.EnableLightweightSegmentData().Result == Result.Success)
        {
          print("Using lightweight segment data");
        }
        else
        {
          print("Unable to use lightweight segment data: Using standard segment data");
        }
      }
      else
      {
        print("Using standard segment data");
      }


      SetAxisMapping(Direction.Forward, Direction.Left, Direction.Up);
      //SetAxisMapping(Direction.Right, Direction.Up, Direction.Backward);
      ConnectionHandler(true);

      bThreadRunning = false;
      return;
    }
    //******Step 2: Attempt connection in a loop until successful or thread stops
    //*************************************************
    while (bThreadRunning == true && !m_Client.IsConnected().Connected)
    {
      Output_Connect OC = m_Client.Connect(CombinedHostnameString);
      print("Connect result: " + OC.Result);

      System.Threading.Thread.Sleep(200);
    }

    //*********Step 3: Configure the streaming mode
    //*************************************************
    if (UsePreFetch)
    {
      // If UsePreFetch is enabled, set the client to pre-fetch streaming mode
      // Pre-fetch Streaming Mode: The client fetches on demand , and it has more control over when to retrieve the frames
      m_Client.SetStreamMode(StreamMode.ClientPullPreFetch);
      print("Using pre-fetch streaming mode");
    }
    else
    {
      // Otherwise, use the default server push mode

      m_Client.SetStreamMode(StreamMode.ServerPush);
    }

    // Get a frame first, to ensure we have received supported type data from the server before
    // trying to determine whether lightweight data can be used.
    GetNewFrame();

    if (UseLightweightData)
    {
      if (m_Client.EnableLightweightSegmentData().Result != Result.Success)
      {
        print("Unable to use lightweight segment data: Using standard segment data");
        m_Client.EnableSegmentData();
      }
      else
      {
        print("Using lightweight segment data");
      }
    }
    else
    {
      print("Using standard segment data");
      m_Client.EnableSegmentData();
    }

    m_Client.EnableMarkerData();
    print("Marker Data Enabled:" + m_Client.IsMarkerDataEnabled().Enabled);

    SetAxisMapping(Direction.Forward, Direction.Left, Direction.Up);
    //SetAxisMapping(Direction.Right, Direction.Up, Direction.Backward);
    //FOR UNITY CONVERSION: SetAxisMapping(Direction.Right, Direction.Up, Direction.Forward);

    ConnectionHandler(true);

    // Get frames in this separate thread if we've asked for it.
    while (GetFrameThread && bThreadRunning)
    {
      GetNewFrame();
        if (m_Client.GetFrame().Result != Result.Success)
      {
        Debug.LogWarning("Failed to get new frame data.");
      }
    }

    bThreadRunning = false;
  }
  
  /**
  * Retrieves a new frame from the Vicon DataStream client on late update if a separate frame acquisition thread is not used.
  **/
  void LateUpdate()
  {
    // Get frame on late update if we've not got a separate frame acquisition thread
    if (!GetFrameThread)
    {
      if (!bConnected)
      {
        return;
      }
      GetNewFrame();
      if (m_Client.GetFrame().Result == Result.Success)
      {
        Debug.Log("New frame received!");
      }
      else
      {
        Debug.LogWarning("Failed to get new frame data.");
      }
    }
  }

  public Output_GetSubjectCount GetSubjectCount ( ){
    if (IsRetimed)
    {
      return m_RetimingClient.GetSubjectCount();
    }
    else
    {
      return m_Client.GetSubjectCount();
    }
  }
 public Output_GetSubjectName GetSubjectName(uint SubjectIndex)
  {
    if (IsRetimed)
    {
      return m_RetimingClient.GetSubjectName(SubjectIndex);
    }
    else
    {
      return m_Client.GetSubjectName(SubjectIndex);
    }
  }
 
 
  /**
  * Gets the local rotation quaternion of a specified segment for a given subject.

  * @param SubjectName">The name of the subject.
  * @param SegmentName">The name of the segment.
  * @return The local rotation quaternion of the specified segment.
    **/
  public Output_GetSegmentLocalRotationQuaternion GetSegmentRotation(string SubjectName, string SegmentName)
  {
    if (IsRetimed)
    {
      return m_RetimingClient.GetSegmentLocalRotationQuaternion(SubjectName, SegmentName);
    }
    else
    {
      return m_Client.GetSegmentLocalRotationQuaternion(SubjectName, SegmentName);
    }

  }

  /**
  * Gets the local translation(position data) of a specified segment for a given subject.
  * @param SubjectName The name of the subject.
  * @param SegmentName The name of the segment.
  * @return The local translation of the specified segment.
  **/
  public Output_GetSegmentLocalTranslation GetSegmentTranslation(string SubjectName, string SegmentName)
  {
    if (IsRetimed)
    {
      return m_RetimingClient.GetSegmentLocalTranslation(SubjectName, SegmentName);
    }
    else
    {
      return m_Client.GetSegmentLocalTranslation(SubjectName, SegmentName);
    }

  }
  /**
  * Gets the static scale of a specified segment for a given subject.
  * @param SubjectName The name of the subject.
  * @param SegmentName The name of the segment.
  * @return  static scale of the specified segment.
  **/
  public Output_GetSegmentStaticScale GetSegmentScale(string SubjectName, string SegmentName)
  {
    if (IsRetimed)
    {
      return m_RetimingClient.GetSegmentStaticScale(SubjectName, SegmentName);
    }
    else
    {
      return m_Client.GetSegmentStaticScale(SubjectName, SegmentName);
    }

  }
  /**
  * Returns the local translation for a bone, scaled according to its scale 
  * and the scale of the bones above it in the hierarchy, apart from the root translation.
  * @param SubjectName">The name of the subject.</param>
  * @param SegmentName">The name of the segment.</param>
  * @return The scaled local translation of the specified segment.
**/
  public Output_GetSegmentLocalTranslation GetScaledSegmentTranslation(string SubjectName, string SegmentName)
  {
    double[] OutputScale = new double[3];
    OutputScale[0] = OutputScale[1] = OutputScale[2] = 1.0;

    // Check first whether we have a parent, as we don't wish to scale the root node's position
    Output_GetSegmentParentName Parent = GetSegmentParentName(SubjectName, SegmentName);

    string CurrentSegmentName = SegmentName;
    if (Parent.Result == Result.Success)
    {

      do
      {
        // We have a parent. First get our scale, and then iterate through the nodes above us
        Output_GetSegmentStaticScale Scale = GetSegmentScale(SubjectName, CurrentSegmentName);
        if (Scale.Result == Result.Success)
        {
          for (uint i = 0; i < 3; ++i)
          {
            if (Scale.Scale[i] != 0.0) OutputScale[i] = OutputScale[i] * Scale.Scale[i];
          }
        }

        Parent = GetSegmentParentName(SubjectName, CurrentSegmentName);
        if (Parent.Result == Result.Success)
        {
          CurrentSegmentName = Parent.SegmentName;
        }
      } while (Parent.Result == Result.Success);
    }

    Output_GetSegmentLocalTranslation Translation = GetSegmentTranslation(SubjectName, SegmentName);
    if (Translation.Result == Result.Success)
    {
      for (uint i = 0; i < 3; ++i)
      {
        Translation.Translation[i] = Translation.Translation[i] / OutputScale[i];
      }
    }
    return Translation;
  }




  public Output_GetSubjectRootSegmentName GetSubjectRootSegmentName(string SubjectName)
  {
    if (IsRetimed)
    {
      return m_RetimingClient.GetSubjectRootSegmentName(SubjectName);
    }
    else
    {
      return m_Client.GetSubjectRootSegmentName(SubjectName);
    }

  }

  /** 
  * Retrieves the parent segment of a given segment.
  * This method queries the Vicon DataStream to get the parent segment name of the specified segment.
  * It checks if the client is in retimed mode or not and calls the appropriate method.
  * 
  * @param SubjectName The name of the subject.
  * @param SegmentName The name of the segment whose parent is to be retrieved.
  * @return An Output_GetSegmentParentName object containing the result and the parent segment name.
  **/
  public Output_GetSegmentParentName GetSegmentParentName(string SubjectName, string SegmentName)
  {
    if (IsRetimed)
    {
      return m_RetimingClient.GetSegmentParentName(SubjectName, SegmentName);
    }
    else
    {
      return m_Client.GetSegmentParentName(SubjectName, SegmentName);
    }

  }

  //MARKER OPERATIONS:

  /**
  * Retrieves the name of a marker for a given subject.
  * @param SubjectName The name of the subject.
  * @param MarkerIndex The index of the marker.
  * @return The name of the marker.
  **/


  public Output_GetMarkerCount GetMarkerCount(string SubjectName)
  {
    if (IsRetimed)
    {
      Debug.LogWarning("RetimingClient does not support GetMarkerCount in retimed mode. Using default values.");
      Output_GetMarkerCount output = new Output_GetMarkerCount
      {
        Result = Result.NotImplemented,
        MarkerCount = 0
      };
      return output;

    }
    else
    {
      return m_Client.GetMarkerCount(SubjectName);
    }
  }
  public Output_GetMarkerName GetMarkerName(string SubjectName, uint MarkerIndex)
  {
    if (IsRetimed)
    {
      Debug.LogWarning("RetimingClient does not support GetMarkerName in retimed mode. Using default values.");
      Output_GetMarkerName output = new Output_GetMarkerName
      {
        Result = Result.NotImplemented,
      };
      return output;
    }
    else
    {
      return m_Client.GetMarkerName(SubjectName, MarkerIndex);
    }
  }

  public Output_GetMarkerParentName GetMarkerParentName(string SubjectName, string MarkerName)
  {
    if (IsRetimed)
    {
      Debug.LogWarning("RetimingClient does not support GetMarkerParentName. Using default values.");
      Output_GetMarkerParentName output = new Output_GetMarkerParentName();
      output.Result = Result.NotImplemented;
      output.SegmentName = "NotImplemented"; // Default value
      return output;

    }
    else
    {
      return m_Client.GetMarkerParentName(SubjectName, MarkerName);
    }
  }


  public Output_GetMarkerGlobalTranslation GetMarkerGlobalTranslation(string SubjectName, string MarkerName)
  {
    if (IsRetimed)
    {
      Debug.LogWarning("RetimingClient does not support GetMarkerGlobalTranslation. Using default values.");
      Output_GetMarkerGlobalTranslation output = new Output_GetMarkerGlobalTranslation();
      output.Result = Result.NotImplemented;
      output.Translation = new double[] { 0.0, 0.0, 0.0 }; // Default values
      output.Occluded = true;
      return output;
    }
    else
    {
      return m_Client.GetMarkerGlobalTranslation(SubjectName, MarkerName);
    }
  }
  /**
  * Sets the axis mapping for the Vicon DataStream to the specified directions.
  * @param X The direction to map to the X axis.
  * @param Y The direction to map to the Y axis.
  * @param Z The direction to map to the Z axis.
  * @return An Output_SetAxisMapping object containing the result of the operation.
  **/
  public Output_SetAxisMapping SetAxisMapping(Direction X, Direction Y, Direction Z)
  {
    if (IsRetimed)
    {
      return m_RetimingClient.SetAxisMapping(X, Y, Z);
    }
    else
    {
      return m_Client.SetAxisMapping(X, Y, Z);
    }
  }
  public void GetNewFrame()
  {
    if (IsRetimed)
    {
      m_RetimingClient.UpdateFrame(Offset);
    }
    else
    {
      m_Client.GetFrame();
    }
    UpdateSubjectFilter();
  }
  public uint GetFrameNumber()
  {
    if (IsRetimed)
    {
      return 0;
    }
    else
    {
      return m_Client.GetFrameNumber().FrameNumber;
    }
  }

  private void OnDisable()
  {
    if (bThreadRunning)
    {
      bThreadRunning = false;
      m_Thread.Join(); //  blocks the main thread (Unity's main game loop) until the m_Thread completes execution.
                       //This prevents issues where the background thread might still be running when the object is being destroyed or disabled.
    }

  }


  /**
  * Filters the data stream to include only specific subjects (objects being tracked in the Vicon system).
  **/
  private void UpdateSubjectFilter()
  {
    if (!String.IsNullOrEmpty(SubjectFilter) && !bSubjectFilterSet)
    {
      string[] Subjects = SubjectFilter.Split(',');
      foreach (string Subject in Subjects)
      {
        if (IsRetimed)
        {
          if (m_RetimingClient.AddToSubjectFilter(Subject.Trim()).Result == Result.Success)
          {
            bSubjectFilterSet = true;
          }
        }
        else
        {
          if (m_Client.AddToSubjectFilter(Subject.Trim()).Result == Result.Success)
          {
            bSubjectFilterSet = true;
          }
        }
      }
    }
  }
  void OnDestroy()
  {
    DisConnect();

    m_Client = null;
    m_RetimingClient = null;
  }

}


