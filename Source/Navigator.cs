/*
 * Copyright 2013, 2014, 2015 John Jore
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

/*
 * http://static.mapfactor.com/files/Navigator_RemoteCommands_-_KB_1.pdf
 * 
 * Move SendCommand and receive to its own thread?
 * Resolve all /**/

/* 
 * This is the main CS file
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using centrafuse.Plugins;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Reflection;            //Extra debug information
using System.Globalization;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using Timer = System.Windows.Forms.Timer;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Navigator
{
	/// <summary>
	/// Mapfactor Navigator plugin for CentraFuse
	/// </summary>
    //[System.ComponentModel.DesignerCategory("Code")]
    public partial class Navigator : CFNavPlugin
	{

#region Variables
		private const string PluginName = "Navigator";
        private const string EXEName = "PC_Navigator.exe";
        private const string PluginPath = @"plugins\" + PluginName + @"\";
        private const string REGNavigatorBase = "SOFTWARE\\MapFactor\\set\\pcnavigator_";  // Updated to reflect correct version at runtime
		private const string LogFile= PluginName + ".log";
        public static string LogFilePath = CFTools.AppDataPath + "\\" + PluginPath + LogFile;
        public static string settingsPath = CFTools.AppDataPath + "\\system\\settings.xml";
        public static string configPath = CFTools.AppDataPath + "\\system\\config.xml";	//LK, 20-nov-2013: Needed to check if this is the current navigation app
        private static string atlas_free = "atlas_pcn_free.idc";            // Move to config file?
        private static string atlas_paid = "atlas_pcn.idc";                 // Move to config file?
        private static string strCFCam = "LiveTraffic.mca";                 // Database with traffic cameras
        private string REGNavigator = REGNavigatorBase + "12";              // Updated to reflect correct version at runtime by comparing to version number in EXE file
        private string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; //Active separator: , or .

        //This should be moved to a AppConfiguration class?
        private string strTRUE = "True";                    // True
        private string strFALSE = "False";                  // False
        private string strEXEPath = "";                     // Folder and EXE name
        private string strEXEParameters = "";               // Paramters to use
        private bool boolFullScreen = false;                // Full screen?
        private bool boolTRIMDIGITS = false;                // Trim number of digits for speed etc?
        private bool boolLocalize = false;                  // Localize GPS Status screen
        private int intExitCounter = 10;                    // Number of retries before Navigator is forcefully closed
        public bool boolExit = false;                       // Set True if hibernating or we want to exit
        private bool boolFREE = true;                       // Free edition?
        private bool boolOSMOK = false;                     // If true, supresses OSM License prompt
        private bool boolAlerts = false;                    // Show alerts if NOT active plugin?
        private bool boolNamedPipes = false;                // Use Louk's named pipes for mute/unmute?
        private bool boolMainScreen = true;                 // Start in main navigation screen
        private bool boolInMutePeriod = false;              // True if already in MUTE period
        private int muteCFTimerInterval = 1800;             //LK, 30-nov-2013: Cache MuteCfTimer Interval (JJ: Value in milliseconds)
        private int intCFVolumeLevel = 0;                   // CF's volume level before "ATT"
        CFControls.CFPanel thepanel = null;                 // The panel to 'project' Navigator into        
        Process pNavigator = null;                          // Navigator's process
        private bool boolCurrentNightMode = false;          // Are we currently in night mode? (We don't actually know this)
        private bool boolCurrentCallMode = false;           // Are we currently on the phone?
        private bool boolSuspend = false;                   // Are we currently suspending?
        private string strAppDataPath = "";                 // Path to Navigator's XML file
        private CFNavLocation navCurrentLocation = new CFNavLocation();       // Navigator's current location
        private NavStats _navStats = new NavStats();        // Navigation statistics
        private Unit SpeedUnit = Unit.UNKNOWN;              // Default to unknown unit
        private Unit DistUnit = Unit.UNKNOWN;               // Default to unknown unit
        private decimal decNavigatorVersion = new decimal(0.0);  // Navigator version, 12.4.4 => 12.4
        private int intNavigatorRevision = 0;               // Navigator revision 12.4.5 => 5
        private string strProtocolVersion = "";             // Navigator TCP Protocol version
        private bool boolUseCFMixerforATT = false;          // Use CF mixer for ATT, or use external commands
        private int intOSRMPort = 5000;                     // Default OSRM port number
        private bool boolOSRMEnabled = false;               // OSRM is not enabled by default
        private string strMCAFolder = "";                   // Navigators data folder
        private string strPocket_GPS_Folder = "";           // Folder were Pocket GPS zip files are stored. If blank, no PocketGPS support
        private bool boolDynamicAudioLevel = false;         // Enable background thread to adjust Navigator Audio Level to match CF Audio level

        //List
        List<Waypoint> waypoints = new List<Waypoint>();    //List of all waypoints from MapFactor Navigator

        //Timers
        Timer nightTimer = new System.Windows.Forms.Timer(); // timer for switching day/night skin      
        Timer muteCFTimer = new System.Windows.Forms.Timer();    // timer for mute'ing CF
        Timer CallStatusTimer = new System.Windows.Forms.Timer();    // timer for checking if a call is in progress
        Timer NavDestinationTimer = new System.Windows.Forms.Timer();    // timer for checking for destination proximity if not active plugin
        Timer NavStatustimer = new System.Windows.Forms.Timer();        //timer for updating GPS status screen

        //Threads
        Thread ThreadCheckSpeedCameraData;  // thread to process new traffic speed cameras
        Thread ThreadDynamicAudioLevel;     // thread to process Navigator audio level
        
        //Mouse constants
        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const int MOUSEEVENTF_MIDDLEUP = 0x0040;      

        //From Mark
        public override event CFNavVoiceEventHandler CF_navVoiceEvent;
        private delegate void VoidDelegate();
                
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int uMsg, Int16 wParam, int lParam);

        [DllImport("User32.dll")]
        public static extern int PostMessage(IntPtr hWnd, int uMsg, Int16 wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern bool BlockInput(bool block);
#endregion

#region CFPlugin methods

		/// <summary>
		/// Initializes the plugin.  This is called from the main application when the plugin is first loaded.
		/// </summary>
		public override void CF_pluginInit()
		{
			try
			{
                // Call writeModuleLog() with the string startup() to keep only last 2 runtimes...
                // Note CF_loadConfig() must be called before WriteLog() can be used
                CFTools.writeLog(PluginName + " starting");
                CFTools.writeLog(PluginName + "CF_pluginInit");

                // CF3_initPlugin() Will configure pluginConfig and pluginLang automatically
                CF3_initPlugin(PluginName, true);

                ICFSetup = new NavSetup(this, pluginConfig, pluginLang);

                //Clear old values from log file
                CFTools.writeModuleLog("startup", LogFilePath);

                //Log current version of DLL for debug purposes
                WriteLog("Plugin Version: '" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + "'");
                WriteLog("CF Version: '" + Assembly.GetCallingAssembly().GetName().Version.ToString() + "'");
                             
                // All controls should be created or Setup in CF_localskinsetup.
                // This method is also called when the resolution or skin has changed.
                CF_localskinsetup();
                
                //Thread for updating Speed cameras
                ThreadCheckSpeedCameraData = new Thread(SpeedCamera_Worker);

                //Thread for updating Navigator Audio Level
                ThreadDynamicAudioLevel = new Thread(DynamicAudioLevel_Worker);
                
                //Get configuration settings
                LoadSettings();

                //Setup the Panel used by PC_Navigator.exe
                WriteLog("Create the panel to use for mapFactor Navigator");
                thepanel = new CFControls.CFPanel();

                //Timer for day/night skin swap                
                nightTimer.Interval = 2500; // Check every 2.5 seconds for a change
                nightTimer.Enabled = false;
                nightTimer.Tick += new EventHandler(nightTimer_Tick);

                //Timer for mute'ing CF while Navigator speaks
                muteCFTimer.Interval = muteCFTimerInterval; // Unpause audio after this duration
                muteCFTimer.Enabled = false;
                muteCFTimer.Tick += new EventHandler(muteCFTimer_Tick);

                //Timer for getting Navigation Stats
                CallStatusTimer.Interval = 2000; // Check every
                CallStatusTimer.Enabled = false;
                CallStatusTimer.Tick += new EventHandler(CallStatusTimer_Tick);
               
                //Timer to use to check if arriving at destination
                NavDestinationTimer.Interval = 5000; // Wait this long...
                NavDestinationTimer.Enabled = false;
                NavDestinationTimer.Tick += new EventHandler(NavDestinationTimer_Tick);

                //LK, 30-nov-2013: Moved from Navigation.cs
                //Timer to update GPS Status screen
                NavStatustimer.Interval = 500; // Wait this long between the next update
                NavStatustimer.Enabled = false;
                NavStatustimer.Tick += new EventHandler(NavStatustimer_Tick);
                
                // Creates new events to catch power mode change
                this.CF_events.CFPowerModeChanged += OnPowerModeChanged;

                //Start with blank values
                _navStats.DistanceDestination = 0;
                _navStats.DistanceNextWaypoint = 0;
                _navStats.TimeSecondsDestination = 0;
                _navStats.TimeSecondsNextWaypoint = 0;
                _navStats.Street = "";

                //Check if already running
                if (TerminateOrphanedProcess(true))
                {
                    if (TerminateOrphanedProcess(true)) this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/EMBEDDINGFAILED"), "AUTOHIDE");
                }

                // Active navigation engine?
                if (ReadCFValue("/APPCONFIG/NAVENGINE", "NAVIGATOR", configPath))
                {
                    //Modify Navigator's Settings XML file...
                    ConfigureNavigatorXML();

                    //Launch navigator
                    //LK, 30-nov-2013: Moved common code to new method  
                    StartNavigator();
                }
                else
                {
                    WriteLog("Not active CFNav engine");
                }
            }
			catch(Exception errmsg) { CFTools.writeError(errmsg.ToString()); }
		}

		/// <summary>
		/// This is called to setup the skin.  This will usually be called in CF_pluginInit.  It will 
        /// also called by the system when the resolution has been changed.
		/// </summary>
		public override void CF_localskinsetup()
		{
            WriteLog("CF_localskinsetup() - start");

            // Handle async invocation
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new VoidDelegate(CF_localskinsetup), new object[] { });
                    return;
                }
            }
            catch (Exception ex)
            {
                WriteLog("skin setup failed: '" + ex.Message); //LK, 28-nov-2013: Text adjusted
            }

            //LK, 28-nov-2013: Catch any errors (a lot can go wrong here)
            try
            {
                // Read the skin file, controls from the skin will be automatically created
                // CF_localskinsetup() should always call CF3_initSection() first, with the exception of setting any
                // CF_displayHooks flags, which affect the behaviour of the CF3_initSection() call.

                if (boolMainScreen)//LK, 30-nov-2013: Allow alternative sections to be loaded
                {
                    WriteLog("Configure for Navigator (Not GPSStatus)");
                    CF3_initSection("Navigator");

                    WriteLog("Create and configure Panel");
                    //Associate 'thepanel' with the panel defined in the skin.xml
                    thepanel = panelArray[CF_getPanelID("PanelNavigator")];

                    //LK, 18-nov-2013: Added some panel settings that might help parenting
                    thepanel.ParentForm = this;
                    thepanel.ParentFocus = true;
                    thepanel.PreRenderPreviousImage = true;
                    thepanel.BackColor = Color.DarkGray;
                    thepanel.Enabled = true;
                    thepanel.Visible = true;
                    thepanel.Name = "ThePanel";

                    //LK, 22-nov-2013: In the case of a skin change, adjust panel size.
                    if (thepanel != null)   //Anytime, but the first (when called from CF_pluginInit())
                    {
                        WriteLog("Panel configured. Configure screen-size");

                        //LK, 30-nov-2013: Instead of keeping the old panels (leeds to trouble when changing sections), dock again
                        if (pNavigator != null)
                        {
                            try
                            {
                                WriteLog("thepanel.Handle: 0x" + thepanel.Handle.ToString("X") + ", pNavigator.MainWindowHandle 0x" + pNavigator.MainWindowHandle.ToString("X"));
                                SetParent(pNavigator.MainWindowHandle, thepanel.Handle);
                                WriteLog("Connected to new panel again");
                            }
                            catch (Exception errMsg) { WriteLog("Failed to SetParent(): " + errMsg.Message); }

                            //Set screen size
                            if (boolFullScreen)
                                SetFullScreen();
                            else
                                SetNonFullScreen();
                        }
                        else
                        {
                            WriteLog("pNavigator == null");
                        }
                    }
                    else
                    {
                        WriteLog("thepanel == null");
                    }
                }
                else
                {
                    WriteLog("Configure for GPSStatus (Not Navigator)");
                    CF3_initSection("GPSStatus");
                }

                //Refresh screen
                this.Invalidate();

                WriteLog("CF_localskinsetup() - end");
            }
            catch (Exception errMsg) { CFTools.writeError(errMsg.Message, errMsg.StackTrace); }
		}
       
		/// <summary>
		/// This is called by the system when it exits or the plugin has been deleted.
		/// </summary>
		public override void CF_pluginClose()
		{
            WriteLog("CF_pluginClose() - Start");

            //Stop the threads
            ThreadCheckSpeedCameraData.Abort(); //Make sure thread does not try to restart Navigator
            ThreadDynamicAudioLevel.Abort(); //Make sure thread does not try to modify Navigator Audio Level

            //Close Navigator before disconnecting, but expect TCP link to die, as Paid version does not support normal close command
            if (boolFREE == false)
            {
                //CloseMainWindow() does not work on paid version
                SendCommand("$exit\r\n", false, TCPCommand.Exit);
                Thread.Sleep(1000);
            }
                                    
            //By closing the connection before closing Navigator, no TCP communication errors are logged
            try
            {
                WriteLog("Shutdown TCP connection");
                server.Shutdown(SocketShutdown.Both);
            }
            catch (Exception errMsg) { WriteLog("Failed to close connection: " + errMsg.Message); } //LK, 29-nov-2013: Add reason to message
            
            //Handles all things related to Navigator
            CloseNavigator();
            
            //We can discard the pipeServer here, but not in CloseNavigator() as CloseNavigator() is re-used to restart Navigator
            this.pipeServer = null;

            //We can discard the TCP server connection here
            try
            {
                server.Close();
                WriteLog("TCP connection closed ");
            }
            catch (Exception errMsg) { WriteLog("Failed to dispose of TCP connection: " + errMsg.Message); }

            //Debug information for TCPCommandQueue
            if (TCPCommandQueue.Count > 0)
            {
                WriteLog("TCPCommandQueue not empty, " + TCPCommandQueue.Count.ToString() + ", commands outstanding");
                while (TCPCommandQueue.Count > 0)
                {
                    WriteLog("Outstanding in TCPCommandQueue: " + TCPCommandQueue.Dequeue().ToString());
                }
            }
            else
            {
                WriteLog("Success - TCPCommandQueue empty");
            }

            //Only put files back, if user wants to flip XML files around
            if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/SETTINGSXMLSWAP")) == true)
            {
                //Put the configuration files back again
                try
                {
                    System.IO.File.Move(strAppDataPath + "\\settings.xml", strAppDataPath + "\\settings.xml.CF");  //LK,28-nov-2013: Add reason to message
                }
                catch (Exception errMsg)
                {
                    WriteLog("Failed to restore settings.xml to .CF: " + errMsg.Message);
                }

                try
                {
                    System.IO.File.Move(strAppDataPath + "\\settings.xml.NAV", strAppDataPath + "\\settings.xml");  //LK,28-nov-2013: Add reason to message
                }
                catch (Exception errMsg)
                {
                    WriteLog("Failed to restore .NAV to settings.xml: " + errMsg.Message);
                }
            }

            WriteLog("Remove PowerModeChange event handler...");
            CF_events.CFPowerModeChanged -= OnPowerModeChanged;

            WriteLog("Remove NavigatorExit event handler...");
            pNavigator.Exited -= pNavigator_Exited;

            WriteLog("Remove timer event handlers...");
            nightTimer.Tick -= nightTimer_Tick;
            muteCFTimer.Tick -= muteCFTimer_Tick;
            NavDestinationTimer.Tick -= NavDestinationTimer_Tick;
            NavStatustimer.Tick -= NavStatustimer_Tick;
          
            base.CF_pluginClose(); // calls form Dispose() method
            //This works on W7?!?
            WriteLog("CF_pluginClose() - End");
        }
		

		/// <summary>
		/// This is called by the system when a button with this plugin action has been clicked.
		/// </summary>
		public override void CF_pluginShow()
		{
            try
            {
                WriteLog("Start: CF_pluginShow");

                //LK, 30-nov-2013: When we became the new navigation app and PC_Navigator isn't loaded yet, load it now
                //JJ: How can CF_pluginShow() be called if we're not the active navigation plugin?!?
                if (ReadCFValue("/APPCONFIG/NAVENGINE", "NAVIGATOR", configPath) && pNavigator == null)
                {
                    //Modify Navigator's Settings XML file...
                    ConfigureNavigatorXML();

                    //Start Navigator
                    StartNavigator();
                }
                else
                {
                    WriteLog("Not active CFNav engine or Navigator already initialized");
                }

                if (boolMainScreen) //LK, 30-nov-2013: Only do this when in the main screen (not the status screen)
                {
                    //LK, 18-nov-2013: Just make the panel visible (don't load again)
                    //Note: PC_navigator will unhide itself; don't try fight that...
                    thepanel.Visible = true;

                    //Configure night mode toggle option
                    SetDayNightToggle();

                    //Resume window
                    SendCommand("$maximize\r\n", false, TCPCommand.Maximize);
                }
                base.CF_pluginShow(); // sets form Visible property
            }
            catch (Exception errMsg) { WriteLog("Failed to show navigation window: " + errMsg.Message); }  //30-nov-2013: Added reason for exception
            
		}

        /// <summary>
        /// This is called by the system when this plugin is minimized/exited (when screen is left).
        /// </summary>
        public override void CF_pluginHide()
        {
            try
            {
                WriteLog("Start: CF_pluginHide");

                if (boolMainScreen) //LK, 30-nov-2013: Only do this when in the main screen (not the status screen)
                {
                    //LK, 18-nov-2013: Just make the panel invisible
                    thepanel.Visible = false;

                    //Make sure its not ontop of CF
                    SendCommand("$minimize\r\n", false, TCPCommand.Minimize);
                }

                //Don't check for skin change. Plugin not visible => no update required
                nightTimer.Enabled = false;
            }
            catch (Exception errMsg) { WriteLog("Failed to close navigation window: " + errMsg.Message); }  //30-nov-2013: Added reason for exception

            base.CF_pluginHide(); // sets form !Visible property
        }


		/// <summary>
		/// This method is called by the system when it pauses all audio.
		/// </summary>
		public override void CF_pluginPause()
		{
            WriteLog("CF_pluginPause");
		}


		/// <summary>
		/// This is called by the system when it resumes all audio.
		/// </summary>
		public override void CF_pluginResume()
		{
            WriteLog("CF_pluginResume");
		}
        
		/// <summary>
		/// Used for plugin to plugin communication. Parameters can be passed into CF_Main_systemCommands
		/// with CF_Actions.PLUGIN, plugin name, plugin command, and a command parameter.
		/// </summary>
		/// <param name="command">The command to execute.</param>
		/// <param name="param1">The first parameter.</param>
		/// <param name="param2">The second parameter.</param>
		public override void CF_pluginCommand(string command, string param1, string param2)
		{
            //WriteLog("CF_pluginCommand: " + command + " " + param1 + ", " + param2);

            //Capture and act upon the hotkeys
            try
            {
                switch (command.ToUpper())
                {
                    case "MINMAX":
                        WriteLog("MINMAX command");
                        //Set correct size
                        this.btnMinMax_Click(null, null);
                        break;
                    case "TOGGLESCREEN":
                        WriteLog("TOGGLESCREEN command");
                        //Toggle GPS Status and Navigator sections
                        btnSectionStatus_Click(null, null);
                        break;
                    case "RESTARTNAV":
                        //Restart MapFactor Navigator
                        WriteLog("RESTARTNAV");
                        try
                        {
                            //Stop Navigator
                            WriteLog("Setup - Closenavigator()");
                            CloseNavigator();

                            //Start Navigator
                            WriteLog("Setup - StartNavigator()");
                            StartNavigator();

                            //User does not really want to exit Navigator anymore
                            boolExit = true;

                            //CF_pluginShow() must be called if restart was initiated with plugin active (visible)
                            if (this.Visible == true) CF_pluginShow();
                        }
                        catch (Exception ex)
                        {
                            WriteLog("Failed to run 'RESTARTNAV', " + ex.ToString());
                        }
                        break;
                    case "TCPCOMMAND":
                        //Send TCP Command to MapFactor Navigator
                        try
                        {  
                            //WriteLog("TCP Command : '" + param1 + "', '" + param2 + "'");
                            SendCommand(param1, false, (TCPCommand)Enum.Parse(typeof(TCPCommand), param2, true));
                        }
                        catch (Exception ex)
                        {
                            WriteLog("Failed to parse and run TCP Command: '" + param1.ToString() + "', '" + param2.ToString() + "', " + ex.ToString());
                        }
                        break;
                    default:
                        WriteLog("Unknown command");
                        break;
                }
            }
            catch (Exception errMsg) { WriteLog("Failed to handle pluginCommand: " + errMsg.Message);  }
		}


        //LK, 30-nov-2013: New method to start PC_navigator, including pipes
        //JJ: Replaced my new StartNavigator :)
        public bool StartNavigator()
        {
            try
            {
                //Must run this before Launching Navigator, else we can't patch the EXE!
                //Configure named pipe. If failed, fall back to non named-pipe for audio
                if (!SetupNamedPipe()) boolNamedPipes = false;

                if (LaunchNavigator())
                {
                    //LK, 23-nov-2013: Start timers
                    CallStatusTimer.Enabled = true;
                    NavDestinationTimer.Enabled = true;

                    //Get Fullscreen information
                    boolFullScreen = CF_getConfigFlag(CF_ConfigFlags.GPSFullscreen);

                    //Set correct size
                    if (boolFullScreen) SetFullScreen(); else SetNonFullScreen();

                    //Configure navigator using TCP commands
                    ConfigureNavigator();

                    //All went OK
                    return true;
                }
                else
                    return false;
            }
            catch (Exception errMsg) { WriteLog("Failed to start " + EXEName + " :" + errMsg.Message); return false; }
        }

        //Launch Navigator
        private bool LaunchNavigator()
        {
            //Launch Navigator                    
            try
            {
                //This check is redundant. No paths lead to this without this already checked
                if (ReadCFValue("/APPCONFIG/NAVENGINE", "NAVIGATOR", configPath))
                {
                    pNavigator = new Process();
                    pNavigator.StartInfo.FileName = strEXEPath + "\\" + EXEName;
                    pNavigator.StartInfo.Arguments = "--window_border=no " + strEXEParameters + " --window_position=" + this.pluginConfig.ReadField("/APPCONFIG/WINDOWSIZE");
                    try
                    {
                        if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/NOHIRES")) == true)
                        {
                            pNavigator.StartInfo.Arguments = pNavigator.StartInfo.Arguments + " --nohires";
                        }
                    }
                    catch { WriteLog("Failed to interpret NOHIRES setting"); }
                    //This does not work: "--tcpserver=127.0.0.1:" + intTCPPort.ToString(); Settings.XML modified instead
                    WriteLog("Launching Navigator using: '" + pNavigator.StartInfo.FileName + "'");
                    WriteLog("Parameters: '" + pNavigator.StartInfo.Arguments + "'");
                    pNavigator.EnableRaisingEvents = true;
                    
                    //LK, 18-nov-2013: Avoid flickering windows at startup
                    pNavigator.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                    //In some, rare, instances, it could already be running. Terminate it now as we're about to start it
                    TerminateOrphanedProcess(true);

                    //Start the EXE
                    pNavigator.Start();

                    //Make sure keyboard and mouse inputs are not working while Navigator starts
                    //Prevents user from pressing OK to the OSM License dialog and the subsequent 'Enter' opens up Menu in Navigator
                    try
                    {
                        if (boolOSMOK && boolFREE)
                        {
                            WriteLog("Blocking input: True");
                            BlockInput(true);
                        }
                    }
                    catch (Exception errMsg) { WriteLog("Failed to disable mouse/keyboard input: " + errMsg.Message); }

                    //Wait for Navigator to start
                    TimeSpan totalProcessorTime = new TimeSpan();
                    totalProcessorTime = pNavigator.TotalProcessorTime;
                    pNavigator.PriorityClass = ProcessPriorityClass.High;   //LK, 24-nov-2013: Top priority while starting 
                    System.Threading.Thread.Sleep(500); // Allow the process to open it's window
                    pNavigator.WaitForInputIdle();     //Dont use this, the window location is messed up. Can't press OK

                    //LK, 18-nov-2013: Attach to hidden panel right away
                    int iRetry = 0;

                    WriteLog("Navigator started, waiting for process to idle");
                    while (pNavigator.TotalProcessorTime > totalProcessorTime || pNavigator.TotalProcessorTime == TimeSpan.Zero)
                    {
                        totalProcessorTime = pNavigator.TotalProcessorTime;
                        WriteLog("Waiting for Navigator to get idle... (totalProcessorTime used = " + totalProcessorTime);

                        //Max 10 seconds wait
                        if (iRetry++ > 20)
                            break;

                        System.Threading.Thread.Sleep(500);
                        pNavigator.WaitForInputIdle();
                    };

                    //Re-run now, else thepanel does not have a handle when resuming...
                    CF_localskinsetup();
                  
                    //Is Navigator running?
                    if (TerminateOrphanedProcess(false)) WriteLog("Navigator process exists"); else WriteLog("Warning - Can't find navigator process");

                    try
                    {
                        WriteLog("MainWindowHandle before parenting = 0x" + pNavigator.MainWindowHandle.ToString("X"));
                        WriteLog("thepanel.Handle = 0x" + thepanel.Handle.ToString("X"));
                    }
                    catch (Exception errMsg) { WriteLog("Failed to inspect the handles" + errMsg.Message); }

                    //Embed
                    if (SetParent(pNavigator.MainWindowHandle, thepanel.Handle) == IntPtr.Zero)
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        WriteLog("Docking failed, last error = 0x" + lastError.ToString("X"));  //LK, 29-nov-2013: Display Hex value
                        System.Threading.Thread.Sleep(500);

                        //Make sure mouse and keyboard work again
                        try
                        {
                            if (boolOSMOK && boolFREE)
                            {
                                WriteLog("Blocking input: False");
                                BlockInput(false);
                            }
                        }
                        catch (Exception errMsg) { WriteLog("Failed to enable mouse/keyboard input: " + errMsg.Message); }

                        //Let the user know it failed
                        CF_systemDisplayDialog(CF_Dialogs.OkBox, pluginLang.ReadField("/APPLANG/NAVIGATOR/FAILEDTODOCK"));
                    }
                    else
                    {
                        WriteLog("Connected to panel");

                        //Ensure Navigator is restarted if it crashes, or user manages to close it. No Navigator => no Nav data in CF
                        pNavigator.Exited += new EventHandler(pNavigator_Exited);
                    }

                    WriteLog("MainWindowHandle after parenting = 0x" + pNavigator.MainWindowHandle.ToString("X"));

                    //JJ: Why is this set to AboveNormal and not Normal?!?
                    pNavigator.PriorityClass = ProcessPriorityClass.AboveNormal;    //LK, 24-nov-2013: Lower the priority to "normal"

                    //Hide panel
                    thepanel.Visible = false;
                    WriteLog("Panel hidden");

                    //Say YES to OSM data usage, if user changed to ON
                    try
                    {
                        if (boolOSMOK && boolFREE)
                        {
                            System.Threading.Thread.Sleep(500); // Allow the process to open it's window

                            WriteLog("Coordinates : " + this.pluginConfig.ReadField("/APPCONFIG/WINDOWSIZE"));
                            string[] mCoordinates= this.pluginConfig.ReadField("/APPCONFIG/WINDOWSIZE").Split(',');
                            int mWidth = int.Parse(mCoordinates[2]) / 2;
                            int mHeight = int.Parse(mCoordinates[3]) / 100 * 95;
                            WriteLog("Mouse Position mWidth: " + mWidth.ToString() + ", mHeight: " + mHeight.ToString());

                            //Jump to location
                            WriteLog("Positioning Mouse Cursor: " + SetCursorPos(mWidth, mHeight).ToString());

                            //Send click. Both left and right for swapped mouse buttons
                            WriteLog("Sending Mouse Clicks");
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                        }
                    }
                    catch { WriteLog("Failed to send OK (Mouse click) to OSM usage"); }

                    //JJ: Re-run now, else panels not resized correctly
                    CF_localskinsetup();

                    WriteLog("Sending minimize command to window with handle 0x" + pNavigator.MainWindowHandle.ToString("X"));
                    //ShowWindowAsync(pNavigator.MainWindowHandle, (int)showWindowAttribute.SW_MINIMIZE);
                    //LK, 29-nov-2013: Last parameter of SendMessage and PostMessage is LWord (int), not string
                    //PostMessage(pNavigator.MainWindowHandle, (int)WindowManagerEvents.WM_COMMAND, unchecked((short)SC.SC_MINIMIZE), 0);
                    ShowWindow(pNavigator.MainWindowHandle, (int)showWindowAttribute.SW_MINIMIZE);

                    //Make sure mouse and keyboard work again
                    try
                    {
                        if (boolOSMOK && boolFREE)
                        {
                            WriteLog("Blocking input: False");
                            BlockInput(false);
                        }                            
                    }
                    catch (Exception errMsg) { WriteLog("Failed to re-enable mouse/keyboard input: " + errMsg.Message); }

                    //Navigator should be launched and running
                    return true;
                }
                else
                {
                    WriteLog("Not active CFNav engine");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (boolOSMOK && boolFREE)
                    {
                        WriteLog("Blocking input: False");
                        BlockInput(false);
                    }
                }
                catch (Exception errMsg) { WriteLog("Failed to re-enable mouse/keyboard input: " + errMsg.Message); }

                WriteLog("Failed to launch Navigator: " + ex.Message);
                CFTools.writeError(ex.Message, ex.StackTrace);

                //Let the user know it failed
                CF_systemDisplayDialog(CF_Dialogs.OkBox, pluginLang.ReadField("/APPLANG/NAVIGATOR/FAILEDTODOCK"));

                return false;
            }

            //Didn't launch Navigator
            return false;
        }

        //Configure Navigator after launching it
        private void ConfigureNavigator()
        {
            //Act as navigation plugin
            SendCommand("$gps_sending=start;nmea\r\n", false, TCPCommand.GPSSending);

            //Set initial day/night mode, if user has enabled this in CF
            if (ReadCFValue("/APPCONFIG/AUTOSWITCHSKIN", "True", configPath))
            {
                boolCurrentNightMode = CF_getConfigFlag(CF_ConfigFlags.NightSkinFlag);
                if (boolCurrentNightMode) SendCommand("$set_mode=night\r\n", false, TCPCommand.DayNight); else SendCommand("$set_mode=day\r\n", false, TCPCommand.DayNight);
            }

            //Enable or disable Navigator voice prompts?
            if (CF_getConfigFlag(CF_ConfigFlags.GPSEnableVoice) == true)
            {
                //Enable alerts
                SendCommand("$navigation_info=sound_warning:on\r\n", false, TCPCommand.NavInfoSoundWarning);

                //Set to on
                SendCommand("$sound_volume=on\r\n", false, TCPCommand.SoundVolume);

                //Configure Navigator Audio volume
                if (CF_getConfigFlag(CF_ConfigFlags.GPSSetNavSoundLevel) == true)
                {
                    SendCommand("$sound_volume=" + CF_getConfigSetting(CF_ConfigSettings.GPSNavSoundLevel).ToString() + "\r\n", false, TCPCommand.SoundVolume);
                }
            }
            else
            {
                SendCommand("$navigation_info=sound_warning:off\r\n", false, TCPCommand.NavInfoSoundWarning);
                SendCommand("$sound_volume=0\r\n", false, TCPCommand.SoundVolume);
                SendCommand("$sound_volume=off\r\n", false, TCPCommand.SoundVolume);
            }

            //Do we want to know?
            if (boolAlerts)
            {
                SendCommand("$navigation_info=waypoint_info:on\r\n", false, TCPCommand.NavInfoWaypointInfo);
                SendCommand("$navigation_info=recalculation_warning:on\r\n", false, TCPCommand.NavInfoRecalculationWarning);
            }
            else
            {
                SendCommand("$navigation_info=waypoint_info:off\r\n", false, TCPCommand.NavInfoWaypointInfo);
                SendCommand("$navigation_info=recalculation_warning:off\r\n", false, TCPCommand.NavInfoRecalculationWarning);
            }
        }

        delegate void pNavigator_ExitedDelegate(object sender, System.EventArgs e);

        // Handle Navigator Exited event
        private void pNavigator_Exited(object sender, System.EventArgs e)
        {
            if (boolExit == true) 
            {
                WriteLog("BoolExit is " + boolExit.ToString() + ". No autostart of navigator");
                return;
            }
            
            //LK, 9-dec-2013: Handle async invocation
            try
            {
                if (InvokeRequired)
                {
                    WriteLog("Invoking");
                    BeginInvoke(new pNavigator_ExitedDelegate(pNavigator_Exited), sender, e);
                    return;
                }
            }
            catch (Exception ex)
            {
                WriteLog("Restart of PC_Navigator failed: '" + ex.Message); //LK, 28-nov-2013: Text adjusted
            }

            try
            {                
                //User really wants to exit Navigator?                
                if (boolExit == false)
                {
                    WriteLog("Restart Navigator?");
                    //Only prompt for restart if not resuming from suspend
                    DialogResult result;
                    if (boolSuspend == false)
                    {
                        result = CF_systemDisplayDialog(CF_Dialogs.YesNo, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/RESTARTNAVIGATOR"));
                    }
                    else
                    {
                        result = DialogResult.OK;
                    }

                    //Restart Navigator?
                    if (result == DialogResult.OK)
                    {
                        WriteLog("Navigator no longer running. Exit code: " + pNavigator.ExitCode.ToString());

                        //Get current timer status
                        bool nightTimer_Status = nightTimer.Enabled;
                        bool muteCFTimer_Status = muteCFTimer.Enabled;

                        //Stop all timers. Call back does not work and causes grief...
                        nightTimer.Enabled = false;
                        muteCFTimer.Enabled = false;
                        CallStatusTimer.Enabled = false;
                        NavDestinationTimer.Enabled = false;

                        //Disconnect the TCP connection so it can be re-established                
                        WriteLog("Disconnecting TCP connection for reuse");
                        try
                        {
                            //Disconnect the TCP connection so it can be re-established                    
                            try
                            {
                                //This is known to fail on WinXP, so provide an alternative
                                if (CF_getConfigSetting(CF_ConfigSettings.OSVersion).ToString().ToUpper() != "XP".ToUpper()) server.Disconnect(true); else server.Close();
                            }
                            catch (Exception errMsg) { WriteLog("Failed to disconnect: " + errMsg.Message); }
                        }
                        catch (Exception errMsg)
                        {
                            WriteLog("Failed to disconnect :" + errMsg.Message);
                        }
                        finally
                        {
                            boolConnecting = false;
                        }

                        // Don't call 'ConfigureNavigatorXML()' here. Called at Init and pluginclose()

                        //Start  Navigator
                        StartNavigator();

                        //If user exited Navigator manually, then plugin is visible
                        if (this.Visible == true)
                        {
                            thepanel.Visible = true; // Make sure its visible, and not behind stats screen
                            thepanel.Focus(); //Give it focus
                            SendCommand("$maximize\r\n", false, TCPCommand.Maximize);
                        }

                        //Set timers back the way they were
                        nightTimer.Enabled = nightTimer_Status;
                        muteCFTimer.Enabled = muteCFTimer_Status;
                    }
                    else
                    {
                        WriteLog("User does not want Navigator to restart");
                        boolExit = true;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("Restart of PC_Navigator failed: '" + ex.Message); //LK, 28-nov-2013: Text adjusted
            }
        }

        
        /// <summary>
        /// Called on control clicks, down events, etc, if the control has a defined CML action parameter in the skin xml.
        /// </summary>
        /// <param name="id">The command to execute.</param>
        /// <param name="state">Button State.</param>
        /// <returns>Returns whatever is appropriate.</returns>
        public override bool CF_pluginCMLCommand(string id, string[] strparams, CF_ButtonState state, int zone)
        {
            if (state != CF_ButtonState.Click || state == CF_ButtonState.Down)
                return false;

            WriteLog("CF_pluginCMLCommand: " + id + ", state: " + state.ToString());

            switch (id.ToUpper())
            {
                case "GOTOSTATUS": //Flip Status / Navigation
                    btnSectionStatus_Click(null, null);
                    return true;
                case "TOGGLEMINMAX": //Flip full screen / Normal screen
                    btnMinMax_Click(null, null);
                    return true;
            }
            return false;
        }

#endregion
		
#region System Functions

        public void LoadSettings()
        {
            // The display name is shown in the application to represent
            // the plugin.  This sets the display name from the configuration file.
            this.CF_params.displayName = this.pluginLang.ReadField("/APPLANG/NAVIGATOR/DISPLAYNAME");
            CFTools.writeLog("Navigator", "New display name = " + this.CF_params.displayName);

            //LK, 25-nov-2013: Actualize input stream
            this.pluginConfig.Reload();

            //Get Navigator Configuration
            try
            {
                WriteLog("App Load Config File");

                // Fakce CF Mute/Unmute?
                try
                {
                    bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/MUTEUNMUTESTATUS"));
                }
                catch
                {
                    this.pluginConfig.WriteField("/APPCONFIG/MUTEUNMUTESTATUS", "False", true);
                }

                // OSMOK (Supresses OSM License prompt)
                try
                {
                    boolOSMOK = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/OSMOK"));
                }
                catch
                {
                    boolOSMOK = false;
                    this.pluginConfig.WriteField("/APPCONFIG/OSMOK", boolOSMOK.ToString(), true);
                }
                finally
                {
                    WriteLog("boolOSMOK: " + boolOSMOK.ToString());
                }
                
                // Edition
                try
                {
                    boolFREE = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/FREEEDITION"));
                }
                catch
                {
                    //Ask what edition to use
                    if (CF_systemDisplayDialog(CF_Dialogs.YesNo, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/DISPLAYNAME") + " " + this.pluginLang.ReadField("/APPLANG/SETUP/FREEEDITION")) == DialogResult.OK)
                    {
                        boolFREE = true;
                    }
                    else
                    {
                        boolFREE = false;
                    }
                                        
                    //Write edition to disk
                    this.pluginConfig.WriteField("/APPCONFIG/FREEEDITION", boolFREE.ToString(), true);
                }
                finally
                {
                    WriteLog("boolFREE: " + boolFREE.ToString());
                }

                // EXE
                try
                {
                    strEXEPath = this.pluginConfig.ReadField("/APPCONFIG/EXEPATH");
                    string strEXE = "\\Navigator12\\PC_Navigator";
                    FileInfo fi0 = new FileInfo(strEXEPath + "\\" + EXEName);

                    //Set some sane default value
                    if (strEXEPath == "" || !fi0.Exists)
                    {                        
                        string strTest = "C:\\Program Files (x86)" + strEXE;
                        FileInfo fi1 = new FileInfo(strTest + "\\" + EXEName);
                        if (fi1.Exists)
                        {
                            this.pluginConfig.WriteField("/APPCONFIG/EXEPATH", strTest, true);
                            strEXEPath = strTest;
                        }
                        else
                        {
                            strTest = "C:\\Program Files" + strEXE;
                            FileInfo fi2 = new FileInfo(strTest + "\\" + EXEName);
                            if (fi2.Exists)
                            {
                                this.pluginConfig.WriteField("/APPCONFIG/EXEPATH", "C:\\Program Files" + strEXE, true);
                                strEXEPath = strTest;
                            }
                            else
                            {                                
                                //Still not found PC_Navigator.exe. Ask user where it is?
                                try
                                {
                                    CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/EXELOCATION") + EXEName);

                                    string location = this.pluginConfig.ReadField("/APPCONFIG/EXEPATH");
                                    if (string.IsNullOrEmpty(location)) location = PluginPath;
                                    
                                    CFDialogParams dialogParams = new CFDialogParams(this.pluginLang.ReadField("/APPLANG/SETUP/EXEPATH"), location);
                                    dialogParams.browseable = true;
                                    dialogParams.enablesubactions = true;
                                    dialogParams.showfiles = true;

                                    CFDialogResults results = new CFDialogResults();
                                    if (CF_displayDialog(CF_Dialogs.FileBrowser, dialogParams, results) == DialogResult.OK)
                                    {
                                        WriteLog("Found :" + results.resulttext);
                                        FileInfo fi3 = new FileInfo(results.resultvalue);
                                        if (fi3.Exists)
                                        {
                                            string strPath = Path.GetDirectoryName(results.resultvalue);
                                            this.pluginConfig.WriteField("/APPCONFIG/EXEPATH", strPath, true);
                                            strEXEPath = strPath;
                                        }
                                        else
                                        {
                                            CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/UNABLE") + EXEName + this.pluginLang.ReadField("/APPLANG/NAVIGATOR/USESETUP"));
                                        }
                                    }
                                }
                                catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }            
                            }
                        }
                    }
                }
                catch
                {
                    strEXEPath = "C:\\Program Files\\Navigator12\\PC_Navigator";
                }
                finally
                {
                    WriteLog("strEXEPath: " + strEXEPath);
                }

                // Parameters
                try
                {
                    strEXEParameters = this.pluginConfig.ReadField("/APPCONFIG/EXEPARAMETERS");
                }
                catch
                {
                    strEXEParameters = "";
                }
                finally
                {
                    WriteLog("strEXEParameters: " + strEXEParameters);
                }

                // Get EXE version. Needed to read corect REG value
                try {
                    FileVersionInfo exeVer = FileVersionInfo.GetVersionInfo(strEXEPath + "\\" + EXEName);
                    REGNavigator = REGNavigatorBase + exeVer.FileMajorPart.ToString();
                }
                catch 
                {
                    WriteLog("Unable to determine registry key to use. Defaulting to 12");
                    REGNavigator = REGNavigatorBase + "_12";
                }
                finally
                {
                    WriteLog("REGNavigator: " + REGNavigator);
                }

                //Get correct atlas setting:
                try
                {
                    //Read from registry                     
                    RegistryKey rk = Registry.LocalMachine;
                    RegistryKey sk1 = rk.OpenSubKey(REGNavigator);

                    string strTmp = sk1.GetValue("Atlas").ToString().ToUpper();
                    string strFolder = Path.GetDirectoryName(strTmp);

                    if (boolFREE)
                    {
                        if (GetProductKey(strFolder + "\\" + atlas_free) == true)
                        {
                            WriteLog("Using FREE edition");
                            strEXEParameters = strEXEParameters + " --atlas=" + strFolder + "\\" + atlas_free;
                        }
                        else
                        {
                            WriteLog("FREE edition selected, but no product key in file");                            
                            CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/NOIDCFILE"));                            
                        }
                    }
                    else
                    {
                        if (GetProductKey(strFolder + "\\" + atlas_paid) == true)
                        {
                            WriteLog("PAID edition selected");
                            strEXEParameters = strEXEParameters + " --atlas=" + strFolder + "\\" + atlas_paid;
                        }
                        else
                        {
                            WriteLog("PAID edition selected, but no product key in file. Trying FREE edition");
                            if (GetProductKey(strFolder + "\\" + atlas_free) == true)
                            {
                                WriteLog("Using FREE edition");
                                strEXEParameters = strEXEParameters + " --atlas=" + strFolder + "\\" + atlas_free;
                                boolFREE = true;
                            }
                            else
                            {
                                WriteLog("Still no product key. Can't progress");
                                CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/NOIDCFILE"));
                            }
                        }                        
                    }
                }
                catch
                {
                    //Default value if all goes wrong...
                    strEXEParameters = strEXEParameters + " --atlas=C:\\ProgramData\\Navigator\\12.4\\" + atlas_free;
                }
                finally
                {
                    WriteLog("strEXEParameters: " + strEXEParameters);
                }

                //Get/Set initial window size. This value should closely match your screen size for optimum experience
                Rectangle resolution = Screen.PrimaryScreen.Bounds;
                string strWindowSize = "0,0," + resolution.Width.ToString() + "," + resolution.Height.ToString();
                try
                {
                    string tmpStr = pluginConfig.ReadField("/APPCONFIG/WINDOWSIZE");
                    if (tmpStr.Length < 8)
                    {
                        pluginConfig.WriteField("/APPCONFIG/WINDOWSIZE", strWindowSize, true);
                    }
                    else
                    {
                        strWindowSize = tmpStr;
                    }
                }
                catch
                {
                    pluginConfig.WriteField("/APPCONFIG/WINDOWSIZE", strWindowSize, true);
                    
                }
                finally
                {                    
                    WriteLog("Window Size: " + strWindowSize);
                }
                
                // TCP Port
                try
                {
                    intTCPPort = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/TCPPORT"));
                }
                catch
                {
                    intTCPPort = 4242;
                    this.pluginConfig.WriteField("/APPCONFIG/TCPPORT", intTCPPort.ToString(), true);                    
                }
                finally
                {
                    WriteLog("intTCPPort: " + intTCPPort.ToString());
                }

                // Alerts if not active plugin?
                try
                {
                    boolAlerts = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/ALERTSENABLED"));
                }
                catch
                {
                    boolAlerts = true;
                    this.pluginConfig.WriteField("/APPCONFIG/ALERTSENABLED", boolAlerts.ToString(), true);
                }
                finally
                {
                    WriteLog("boolAlerts: " + boolAlerts.ToString());
                }

                //Use Louk's named pipe?
                try
                {
                    boolNamedPipes = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/NAMEDPIPE"));
                }
                catch
                {
                    boolNamedPipes = false;
                    this.pluginConfig.WriteField("/APPCONFIG/NAMEDPIPE", boolNamedPipes.ToString(), true);
                }
                finally
                {
                    WriteLog("boolNamedPipes: " + boolNamedPipes.ToString());
                }

                // NoHiRes?
                try
                {
                    bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/NOHIRES"));
                }
                catch
                {
                    this.pluginConfig.WriteField("/APPCONFIG/NOHIRES", "False", true);
                }
                
                // Delay after Unmute
                int intDelay = 1800;
                try
                {
                    intDelay = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/AUDIODELAYAFTERMUTE"));
                }
                catch
                {
                    intDelay = 1800;    //LK, 30-nov-2013: Default value for sumulated UnMute (when no Unmute messages are received from the named pipe
                    this.pluginConfig.WriteField("/APPCONFIG/AUDIODELAYAFTERMUTE", intDelay.ToString(), true);
                }
                finally
                {
                    WriteLog("intDelay: " + intDelay.ToString());
                    muteCFTimerInterval = intDelay; //LK, 30-nov-2013: Cache this value to avoid many reads from the config file
                }

                //True string
                try
                {
                    strTRUE = this.pluginLang.ReadField("/APPLANG/NAVIGATOR/TRUE"); 
                }
                catch
                {
                    strTRUE = "True";
                }

                //False string
                try
                {
                    strFALSE = this.pluginLang.ReadField("/APPLANG/NAVIGATOR/FALSE");
                }
                catch
                {
                    strFALSE = "False";
                }

                // Swap mapFactor Navigator's Config.xml files around.
                bool boolSETTINGSXMLSWAP = true;
                try
                {
                    boolSETTINGSXMLSWAP = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/SETTINGSXMLSWAP"));
                }
                catch
                {
                    boolSETTINGSXMLSWAP = true;
                    this.pluginConfig.WriteField("/APPCONFIG/SETTINGSXMLSWAP", boolSETTINGSXMLSWAP.ToString(), true);
                }
                finally
                {
                    WriteLog("Swap mapFactor Navigator's settings.xml files around: " + boolSETTINGSXMLSWAP.ToString());
                }

                // Localize GPS Status screen?
                boolLocalize = false;
                try
                {
                    boolLocalize = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/LOCALIZE"));
                }
                catch
                {
                    boolLocalize = false;
                    this.pluginConfig.WriteField("/APPCONFIG/LOCALIZE", boolLocalize.ToString(), true);
                }
                finally
                {
                    WriteLog("Localize GPS Status screen: : " + boolLocalize.ToString());
                }

                // Number of retries before force shutdown of Navigator
                try
                {
                    intExitCounter = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/EXITCOUNTER"));
                }
                catch
                {
                    this.pluginConfig.WriteField("/APPCONFIG/EXITCOUNTER", intExitCounter.ToString(), true);
                }
                finally
                {
                    WriteLog("intExitCounter: " + intExitCounter.ToString());
                }

                // Trim number of digits?
                try
                {
                    boolTRIMDIGITS = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/TRIMDIGITS"));
                }
                catch
                {
                    boolTRIMDIGITS = false;
                    this.pluginConfig.WriteField("/APPCONFIG/TRIMDIGITS", boolTRIMDIGITS.ToString(), true);
                }
                finally
                {
                    WriteLog("Trim digits: " + boolTRIMDIGITS.ToString());
                }

                // Enable inserting the LiveTraffic.mca file into navigator?
                bool boolEnableCFCam = false;
                try
                {
                    boolEnableCFCam = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/CFCAMSUPPORT"));
                }
                catch
                {
                    boolEnableCFCam = false;
                    this.pluginConfig.WriteField("/APPCONFIG/CFCAMSUPPORT", boolEnableCFCam.ToString(), true);
                }
                finally
                {
                    WriteLog("CFCam Support : " + boolEnableCFCam.ToString());
                }

                // MCA path. Make sure this run's after setting boolFREE
                string strMCAFileCheck = "";    //Checkpoint file to look for to validate Navigators data folder
                try
                {
                    //Get the path to IDC file from registry
                    string strFolder = "";
                    try
                    {
                        //Read from registry                     
                        RegistryKey rk = Registry.LocalMachine;
                        RegistryKey sk1 = rk.OpenSubKey(REGNavigator);

                        string strTmp = sk1.GetValue("Atlas").ToString().ToUpper();
                        strFolder = Path.GetDirectoryName(strTmp);
                    }
                    catch (Exception errmsg)
                    {
                        WriteLog("Failed to get Navigator data from from registry, " + errmsg.Message);
                    }

                    //Get the dataPath to mca files
                    strMCAFolder = "";
                    strMCAFileCheck = "earth_osm.mca";   //File to look for depends on edition of Navigator (Free or Paid)
                    try
                    {
                        if (boolFREE)
                        {
                            strMCAFolder = GetDataPath(strFolder + "\\" + atlas_free);
                            strMCAFileCheck = "earth_osm.mca";
                        }
                        else
                        {
                            strMCAFolder = GetDataPath(strFolder + "\\" + atlas_paid);
                            strMCAFileCheck = "earth_ta.mca";
                        }
                    }
                    catch (Exception errmsg)
                    {
                        WriteLog("Failed to get MCA folder from IDC file, " + errmsg.Message);
                    }

                    //If we dont have the mca data path, ask user
                    if (strMCAFolder == "")
                    {
                        //Ask user for MCA location                        
                        try
                        {
                            CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/EXELOCATION") + " " + this.pluginLang.ReadField("/APPLANG/SETUP/MCAPATH"));

                            string location = this.pluginConfig.ReadField("/APPCONFIG/MCAPATH");
                            if (string.IsNullOrEmpty(location)) location = PluginPath;

                            CFDialogParams dialogParams = new CFDialogParams(this.pluginLang.ReadField("/APPLANG/SETUP/MCAPATH"), location);
                            dialogParams.browseable = true;
                            dialogParams.enablesubactions = true;
                            dialogParams.showfiles = true;

                            CFDialogResults results = new CFDialogResults();
                            if (CF_displayDialog(CF_Dialogs.FileBrowser, dialogParams, results) == DialogResult.OK)
                            {
                                WriteLog("Found :" + results.resulttext);
                                FileInfo fi3 = new FileInfo(results.resultvalue);
                                if (fi3.Exists)
                                {
                                    string strPath = Path.GetDirectoryName(results.resultvalue);
                                    this.pluginConfig.WriteField("/APPCONFIG/MCAPATH", strPath, true);
                                    strMCAFolder = strPath;
                                    WriteLog("mca folder : '" + strMCAFolder + "'");
                                }
                                else
                                {
                                    CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/UNABLE") + " " + this.pluginLang.ReadField("/APPLANG/NAVIGATOR/USESETUP"));
                                    strMCAFolder = "";
                                }
                            }
                            else
                            {
                                CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/UNABLE") + " " + this.pluginLang.ReadField("/APPLANG/NAVIGATOR/USESETUP"));
                                strMCAFolder = "";
                            }
                        }
                        catch (Exception errmsg) { WriteLog("Unable to get mca folder, " + errmsg.ToString()); }
                    }
                }
                catch (Exception errmsg) 
                {
                    WriteLog("Failed to get MCA folder, " + errmsg.ToString());
                }

                //Copy the mca file to the data folder, or remove it
                if (strMCAFolder != "")
                {
                    if (boolEnableCFCam)
                    {
                        //Check if checkpoint file exists
                        FileInfo fi3 = new FileInfo(strMCAFolder + "\\" + strMCAFileCheck);
                        if (fi3.Exists)
                        {
                            WriteLog("Found mca data path, '" + strMCAFolder + "', write the mca file to Navigators data folder");

                            string strSource = @PluginPath + "LiveTraffic\\" + strCFCam;
                            string strDestination = strMCAFolder + strCFCam;
                            WriteLog("Source: '" + strSource + "', Destination '" + strDestination + "'");
                            try
                            {
                                System.IO.File.Copy(strSource, strDestination, true);
                            }
                            catch (Exception errmsg)
                            {
                                WriteLog("Failed to copy the mca file to Navigators data folder, " + errmsg.ToString());
                            }
                        }
                        else
                        {
                            WriteLog("Checkpoint file does not exist. Failed to copy mca file");
                            CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/UNABLE") + " " + this.pluginLang.ReadField("/APPLANG/SETUP/MCAPATH"));
                        }
                    }
                    else
                    {
                        //Remove the mca file
                        //Check if checkpoint file exists
                        string strSource = strMCAFolder + "\\" + strCFCam;
                        FileInfo fi3 = new FileInfo(strSource);
                        if (fi3.Exists)
                        {
                            WriteLog("Found mca data file, '" + strSource + "', write the mca file to Navigators data folder");
                            WriteLog("Source: '" + strSource + "'");
                            try
                            {
                                System.IO.File.Delete(strSource);
                            }
                            catch (Exception errmsg)
                            {
                                WriteLog("Failed to remove the mca file from Navigators data folder, " + errmsg.ToString());
                            }
                        }
                        else
                        {
                            WriteLog("LiveTraffic MCA file does not exist. Failed to remove mca file");
                        }
                    }
                }
                else
                {
                    WriteLog("Unable to locate data folder for mca files.");
                    CF_systemDisplayDialog(CF_Dialogs.OkBox, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/UNABLE") + " " + this.pluginLang.ReadField("/APPLANG/SETUP/MCAPATH"));
                }

                // OSRM Enabled?
                try
                {
                    boolOSRMEnabled = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/OSRM_ENABLE"));
                }
                catch
                {
                    boolOSRMEnabled = false; //Don't enable OSRM by default
                    this.pluginConfig.WriteField("/APPCONFIG/OSRM_ENABLE", boolOSRMEnabled.ToString(), true);
                }
                finally
                {
                    WriteLog("boolOSRMEnabled: " + boolOSRMEnabled.ToString());
                }
                
                // OSRM TCP Port
                try
                {
                    intOSRMPort = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/OSRM_ENABLE"));
                }
                catch
                {
                    intOSRMPort = 5000; //Default OSRM port number
                    this.pluginConfig.WriteField("/APPCONFIG/OSRM_TCP_PORT", intOSRMPort.ToString(), true);
                }
                finally
                {
                    WriteLog("intOSRMPort: " + intOSRMPort.ToString());
                }

                //Pocket GPS support
                try
                {
                    strPocket_GPS_Folder = this.pluginConfig.ReadField("/APPCONFIG/POCKET_GPS_FOLDER");
                }
                catch
                {
                    strPocket_GPS_Folder = "";
                }
                finally
                {
                    WriteLog("Pocket GPS folder: " + strPocket_GPS_Folder);

                    //Thread for processing traffic speed cameras. Suspend if no folder, start if folder given
                    if (strPocket_GPS_Folder == "")
                    {
                        if (ThreadCheckSpeedCameraData.IsAlive == true) ThreadCheckSpeedCameraData.Suspend();
                    }
                    else
                    {
                        if (ThreadCheckSpeedCameraData.IsAlive == false) ThreadCheckSpeedCameraData.Start();
                    }
                }

                //Dynamic Audio Level
                try
                {
                    boolDynamicAudioLevel = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/DYNAMICAUDIOLEVEL"));
                }
                catch
                {
                    boolDynamicAudioLevel = false;
                }
                finally
                {
                    WriteLog("Dynamic Audio Level: " + boolDynamicAudioLevel);

                    //Thread for processing audio levels
                    if (boolDynamicAudioLevel)
                    {
                        if (ThreadDynamicAudioLevel.IsAlive == false) ThreadDynamicAudioLevel.Start();
                    }
                    else
                    {
                        if (ThreadDynamicAudioLevel.IsAlive == true) ThreadDynamicAudioLevel.Suspend();
                    }
                }



                //How is ATT handled?
                //If CF version 4.4.6 or higher then use internal CF mixer, like mediaplayer
                Version CF_ver = Assembly.GetEntryAssembly().GetName().Version;               
                Version CF_audio_mixer = new Version("4.4.6");      // This is the minimum required CF version to use internal CF mixer
                if (CF_ver.CompareTo(CF_audio_mixer) == -1) boolUseCFMixerforATT = false; else boolUseCFMixerforATT = true;
                WriteLog("boolUseCFMixerforATT: " + boolUseCFMixerforATT.ToString());

                // CF Settings
                try
                {
                    WriteLog("CF_ConfigFlags.AttMute:             '" + CF_getConfigFlag(CF_ConfigFlags.AttMute).ToString() + "'");
                    WriteLog("CF_ConfigFlags.Fullscreen:          '" + CF_getConfigFlag(CF_ConfigFlags.Fullscreen).ToString() + "'");
                    WriteLog("CF_ConfigFlags.GPSAttMute:          '" + CF_getConfigFlag(CF_ConfigFlags.GPSAttMute).ToString() + "'");
                    WriteLog("CF_ConfigFlags.GPSEnableVoice:      '" + CF_getConfigFlag(CF_ConfigFlags.GPSEnableVoice).ToString() + "'");
                    WriteLog("CF_ConfigFlags.GPSFullscreen:       '" + CF_getConfigFlag(CF_ConfigFlags.GPSFullscreen).ToString() + "'");
                    WriteLog("CF_ConfigFlags.GPSSetNavSoundLevel: '" + CF_getConfigFlag(CF_ConfigFlags.GPSSetNavSoundLevel).ToString() + "'");
                    WriteLog("CF_ConfigFlags.NightSkinFlag:       '" + CF_getConfigFlag(CF_ConfigFlags.NightSkinFlag).ToString() + "'");
                    WriteLog("CF_ConfigFlags.RadioMute:           '" + CF_getConfigFlag(CF_ConfigFlags.RadioMute).ToString() + "'");
                    WriteLog("CF_ConfigSettings.GPSNavSoundLevel: '" + CF_getConfigSetting(CF_ConfigSettings.GPSNavSoundLevel).ToString() + "'");
                    WriteLog("CF_ConfigSettings.AttMuteLevel:     '" + CF_getConfigSetting(CF_ConfigSettings.AttMuteLevel).ToString() + "'");
                    WriteLog("CF_ConfigSettings.GPSVoicePrompts:  '" + CF_getConfigSetting(CF_ConfigSettings.GPSVoicePrompts).ToString() + "'");
                    WriteLog("CF_ConfigSettings.OSVersion:        '" + CF_getConfigSetting(CF_ConfigSettings.OSVersion).ToString() + "'");
                }
                catch (Exception errMsg) { WriteLog("Unable to get CF configuration flags or settings: " + errMsg.Message); }
            }
            catch (Exception errMsg) { WriteLog("Unable to get configuration settings: " + errMsg.Message); }
        }

        //Read Navigator's product key from IDC file
        private bool GetProductKey(string tmpIDCFile)
        {
            string productKey = "";
            FileInfo fiIDC = new FileInfo(tmpIDCFile);
            
            //bool boolTmp = ReadCFValue("/config/app/product_key", "", tmpIDCFile);
            //WriteLog("booltmp: " + boolTmp);

            //XML File exists, get product key
            if (fiIDC.Exists)
            {
                WriteLog("Reading product key from '" + tmpIDCFile + "'");
                try
                {
                    //Get Mapfactor license
                    XmlDocument configxml = new XmlDocument();
                    configxml.XmlResolver = null; //Ignore settings.dtd file not in same folder
                    configxml.Load(tmpIDCFile);

                    XmlNodeList xnList = configxml.SelectNodes("/config/app");
                    foreach (XmlNode xn in xnList)
                    {
                        productKey = xn["product_key"].InnerText;
                    }
                }
                catch (Exception errMsg) { WriteLog("Failed to read product key: " + errMsg.Message); }

                WriteLog("ProductKey: " + productKey);
                if (productKey == "") return false; else return true;
            }
            else
            {
                WriteLog("IDC file '" + tmpIDCFile + "' does not exist");
                return false;
            }
        }

        //Get Navigator's data path from IDC file
        private string GetDataPath(string tmpIDCFile)
        {
            string dataPath = "";
            FileInfo fiIDC = new FileInfo(tmpIDCFile);
           
            //XML File exists, get the data
            if (fiIDC.Exists)
            {
                WriteLog("Reading data path from '" + tmpIDCFile + "'");
                try
                {
                    //Get Mapfactor license
                    XmlDocument configxml = new XmlDocument();
                    configxml.XmlResolver = null; //Ignore settings.dtd file not in same folder
                    configxml.Load(tmpIDCFile);

                    XmlNodeList xnList = configxml.SelectNodes("/config/atlas/sheet");
                    foreach (XmlNode xn in xnList)
                    {
                        try 
                        {
                            dataPath = xn["data"].InnerText;
                        }
                        catch (Exception errmsg)
                        { 
                            WriteLog("Failed to get data folder from IDC file, " + errmsg.ToString());
                        }
                    }
                }
                catch (Exception errMsg) { WriteLog("Failed to read 'mainland' entry: " + errMsg.Message); }

                //WriteLog("dataPath: '" + dataPath + "'");
                return dataPath;
            }
            else
            {
                WriteLog("IDC file '" + tmpIDCFile + "' does not exist");
                return "";
            }
        }

        //Get routing information from Navigator's XML file
        private List<Waypoint> GetNavigatorRoutingXML()
        {
            //WriteLog("GetNavigatorRoutingXML() - Start");

            //Only get routing information from Navigator, if InRoute is TRUE
            if (String.Compare(CF_navGetInfo(CFNavInfo.InRoute), strTRUE, true) == 0)
            {
                //Start with empty route
                string tmpRoute = "";
                waypoints.Clear();

                //Get Routing information
                FileInfo fiXML = new FileInfo(strAppDataPath + "\\routing_points.xml");
                if (fiXML.Exists)
                {
                    try
                    {
                        XmlDocument routingxml = new XmlDocument();
                        routingxml.XmlResolver = null; //Ignore routing_points.dtd file not in same folder
                        routingxml.Load(strAppDataPath + "\\routing_points.xml");

                        try
                        {
                            //Select all the nodes
                            XmlNodeList xnList = routingxml.SelectNodes("/routing_points/default_set");
                            //WriteLog("Nodes : " + xnList.Count.ToString());
                            
                            foreach (XmlNode xn in xnList)
                            {
                                //WriteLog("Node Name: " + xn.Name.ToString() + ", xn ChildNodes: " + xn.ChildNodes.Count.ToString());
                                foreach (XmlNode xcn in xn.ChildNodes)
                                {
                                    //WriteLog("ChildNode Name: " + xcn.Name.ToString() + ", xcn ChildNodes: " + xcn.ChildNodes.Count.ToString());

                                    //No need to get departure values
                                    if (xcn.Name != "departure")
                                    {
                                        bool boolProcess = true;

                                        //Skip any waypoints already passed
                                        if (xcn.Name == "waypoint")
                                        {
                                            if (xcn.Attributes["passed"].Value.ToString() == "yes") boolProcess = false;
                                        }

                                        //Ok to proceed?
                                        if (boolProcess)
                                        {
                                            Waypoint waypoint = new Waypoint();

                                            //Process Lat/Lon values
                                            foreach (XmlNode xcnRoute in xcn.ChildNodes)
                                            {
                                                switch (xcnRoute.Name.ToString().ToUpper())
                                                {
                                                    case "LAT":
                                                        waypoint.Latitude = Convert.ToDouble(xcnRoute.InnerText.ToString()) / 3600000;
                                                        break;
                                                    case "LON":
                                                        waypoint.Longitude = Convert.ToDouble(xcnRoute.InnerText.ToString()) / 3600000;
                                                        break;
                                                }
                                            }

                                            //Add to list. Last one added is destination
                                            waypoints.Add(waypoint);

                                            //WriteLog("Lon: " + waypoint.Longitude.ToString() + ", Lat: " + waypoint.Latitude.ToString());
                                        }
                                    }
                                }
                            }
                            //WriteLog("Done Parsing the Routing XML File");

                            //Update Dest GPS coordinates
                            if (waypoints.Count > 0)
                            {
                                _currentPosition.DestLatitude = waypoints[waypoints.Count - 1].Latitude;
                                _currentPosition.DestLongitude = waypoints[waypoints.Count - 1].Longitude;
                            }
                            else
                            {
                                _currentPosition.DestLatitude = 0;
                                _currentPosition.DestLongitude = 0;
                            }
                            //WriteLog("Updated Destination Fields: " + _currentPosition.DestLatitude.ToString() + ", " + _currentPosition.DestLongitude.ToString());

                            //Update Route, including destination coords
                            for (int i = 0; i < waypoints.Count; i++)
                            {
                                String strWP = Math.Round(waypoints[i].Latitude, 5).ToString().Replace(',', '.') + "," + Math.Round(waypoints[i].Longitude, 5).ToString().Replace(',', '.');

                                if (tmpRoute == "")
                                    tmpRoute = strWP;
                                else
                                    tmpRoute = tmpRoute + ";" + strWP;

                                //WriteLog("Updated Route Field: " + tmpRoute);
                            }
                            _currentPosition.Route = tmpRoute;
                            WriteLog("Updated Route Field: " + _currentPosition.Route);

                            //All done
                            return waypoints;
                        }
                        catch (Exception errMsg) { WriteLog("Failed to parse routing XML file: " + errMsg.Message); }
                    }
                    catch (Exception errMsg) { WriteLog("Failed to read routing XML file: " + errMsg.Message); }

                    //Update to reflect we have no destination?
                    _currentPosition.DestLatitude = 0;
                    _currentPosition.DestLongitude = 0;

                    //Didn't work out...
                    return null;
                }
                else
                {
                    WriteLog("Error : No Routing File found. No route to return");

                    //Update to reflect we have no destination?
                    _currentPosition.DestLatitude = 0;
                    _currentPosition.DestLongitude = 0;

                    //Didn't work out...
                    return null;
                }
            }
            else
            {
                WriteLog("Not Navigating. No route to return");

                //Update to reflect we have no destination?
                _currentPosition.DestLatitude = 0;
                _currentPosition.DestLongitude = 0;

                //Not route to report back on
                return null;
            }
        }

        //Manipulate Navigator's XML file
        public void ConfigureNavigatorXML()
        {
            //Find users profile path with settings.xml file
            RegistryKey rk = Registry.LocalMachine;
            RegistryKey sk1 = rk.OpenSubKey(REGNavigator);

            strAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Navigator\\" + sk1.GetValue("major_ver").ToString() + "." + sk1.GetValue("minor_ver").ToString();
            WriteLog("Path :" + strAppDataPath);

            //Handle XML files
            FileInfo fiXML= new FileInfo(strAppDataPath + "\\settings.xml");
            FileInfo fiNAV = new FileInfo(strAppDataPath + "\\settings.xml.NAV");
            FileInfo fiCF = new FileInfo(strAppDataPath + "\\settings.xml.CF");
            
            //XML File exists, and user wants to swap config files around
            if (fiXML.Exists && bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/SETTINGSXMLSWAP")))
            {
                WriteLog("XML exists");
                //If NAV files exist, remove NAV
                if (fiNAV.Exists)
                {
                    WriteLog("NAV Exists");
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml.NAV", strAppDataPath + "\\settings.xml." + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")); }
                    catch { WriteLog("Unable to rename xml.NAV to XML.datetime"); }
                    WriteLog("NAV Removed");
                }

                //IF CF exists
                if (fiCF.Exists)
                {
                    WriteLog("CF Exists");
                    //Rename XML to NAV
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml", strAppDataPath + "\\settings.xml.NAV"); }
                    catch { WriteLog("Unable to rename xml to xml.NAV"); }
                    WriteLog("Renamed XML to NAV");

                    //Rename CF to XML
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml.CF", strAppDataPath + "\\settings.xml"); }
                    catch { WriteLog("Unable to rename xml.CF to xml"); }
                    WriteLog("Renamed CF to XML");
                }
                else
                {
                    WriteLog("CF does not exist");
                    try
                    {
                        WriteLog("Creating XML.orig");
                        WriteLog("Creating XML");
                        WriteLog("Creating NAV");
                        string newFileName = strAppDataPath + "\\settings.xml." + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".orig";
                        System.IO.File.Move(strAppDataPath + "\\settings.xml", newFileName);
                        System.IO.File.Copy(newFileName, strAppDataPath + "\\settings.xml", true);
                        System.IO.File.Copy(newFileName, strAppDataPath + "\\settings.xml.NAV", true);
                    }
                    catch (Exception errMsg) { WriteLog("Failed to create new settings.xml file: " + errMsg.Message); }
                }
            }
            else
            {
                WriteLog("Initial XML file does not exist");
                //Try to use CF first
                if (fiCF.Exists)
                {
                    WriteLog("Using CF");
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml.CF", strAppDataPath + "\\settings.xml"); }
                    catch (Exception errMsg) { WriteLog("Unable to rename xml.CF to xml: "+ errMsg.Message); }

                    if (fiNAV.Exists == false)
                    {
                        try { System.IO.File.Copy(strAppDataPath + "\\settings.xml", strAppDataPath + "\\settings.xml.NAV"); }
                        catch (Exception errMsg) { WriteLog("Unable to copy xml to xml.NAV: " + errMsg.Message); }
                    }
                }
                else if (fiNAV.Exists) //try NAV
                {
                    WriteLog("Using NAV");
                    try { System.IO.File.Copy(strAppDataPath + "\\settings.xml.NAV", strAppDataPath + "\\settings.xml"); }
                    catch (Exception errMsg) { WriteLog("Unable to rename xml.NAV to xml: " + errMsg.Message); }
                }
                else
                {
                    WriteLog("No XML to use");
                }
            }


            FileInfo fi= new FileInfo(strAppDataPath + "\\settings.xml");
            if (fi.Exists)
            {
                //Configure navigator for usage with CF
                try
                {                                        
                    //Get Mapfactor settings
                    XmlDocument configxml = new XmlDocument();
                    configxml.XmlResolver = null; //Ignore settings.dtd file not in same folder
                    configxml.Load(strAppDataPath + "\\settings.xml");

                    //Set communication type
                    try
                    {
                        XmlNodeList xnList = configxml.SelectNodes("/settings/EXTERFACE");
                        foreach (XmlNode xn in xnList)
                        {
                            xn["type"].InnerText = "tcpip";
                            WriteLog("Communication type set to: " + xn["type"].InnerText);
                        }
                        configxml.Save(strAppDataPath + "\\settings.xml");
                    }
                    catch (Exception errMsg) { WriteLog("Failed to set communication type: " + errMsg.Message); }

                    //Set communication IP and port
                    try
                    {
                        XmlNodeList xnList = configxml.SelectNodes("/settings/EXTERFACE/tcpip");
                        foreach (XmlNode xn in xnList)
                        {                            
                            xn["ip_address"].InnerText = strIP;
                            WriteLog("IP Address configured: " + xn["ip_address"].InnerText);
                            xn["port"].InnerText = intTCPPort.ToString();
                            WriteLog("Port configured: " + xn["port"].InnerText);
                        }
                        configxml.Save(strAppDataPath + "\\settings.xml");
                    }
                    catch (Exception errMsg) { WriteLog("Failed to set IP / Port details: " + errMsg.Message); }

                    //Remove Exit and Minimize from Navigator
                    try
                    {
                        XmlNodeList xnList = configxml.SelectNodes("/settings/APP/mainMenu/action");
                        foreach (XmlElement xe in xnList)                                                
                        {
                            if (xe.InnerText == "Exit")
                            {
                                xe.SetAttribute("visible", "no");
                                WriteLog("'Exit' removed");
                            }
                            if (xe.InnerText == "Minimize")
                            {
                                xe.SetAttribute("visible", "no");
                                WriteLog("'Minimize' removed");
                            }
                        }
                        configxml.Save(strAppDataPath + "\\settings.xml");
                    }
                    catch (Exception errMsg) { WriteLog("Failed to disable menu options: " + errMsg.Message); }

                    //Allow suspend
                    try
                    {
                        XmlNodeList xnList = configxml.SelectNodes("/settings/APP");
                        foreach (XmlNode xn in xnList)
                        {
                            xn["allow_suspend"].InnerText = "yes";
                            WriteLog("Suspend configured: " + xn["allow_suspend"].InnerText);
                        }
                        configxml.Save(strAppDataPath + "\\settings.xml");
                    }
                    catch (Exception errMsg) { WriteLog("Failed to set suspend setting: " + errMsg.Message); }
                }
                catch (Exception errMsg) { WriteLog("Failed to configure Navigator's settings.xml file: " + errMsg.Message); }
            }
        }

        private void SetDayNightToggle()
        {
            //Configure night mode toggle option
            try
            {
                //Get CF setting
                bool boolTmp = ReadCFValue("/APPCONFIG/AUTOSWITCHSKIN", "True", configPath);
                if (boolTmp) nightTimer.Enabled = true; else nightTimer.Enabled = false;
                WriteLog("AUTOSWITCHSKIN: " + nightTimer.Enabled.ToString());
            }
            catch (Exception errMsg) { WriteLog("Failed to configure auto day/night mode: " + errMsg.Message); }
        }

        // Event to keep checking if CF is in night mode
        private void nightTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                bool nightMode = CF_getConfigFlag(CF_ConfigFlags.NightSkinFlag);
                if (boolCurrentNightMode != nightMode)
                {
                    WriteLog("Switching mode");
                    if (nightMode) SendCommand("$set_mode=night\r\n", false, TCPCommand.DayNight); else SendCommand("$set_mode=day\r\n", false, TCPCommand.DayNight);

                    //Update current mode
                    boolCurrentNightMode = nightMode;
                }
            }
            catch (Exception errMsg) { WriteLog("Failed to change day/night mode: " + errMsg.Message); }
        }

        //Background polling of Callstatus information
        //This should be an event instead of polling a flag... CF to add/create
        private void CallStatusTimer_Tick(object sender, EventArgs e)
        {
            //ATT Mute enabled. Lets do some work
            if (CF_getConfigFlag(CF_ConfigFlags.AttMute) == true)
            {
                try
                {
                    //Get current Call status
                    bool callMode = CF_getCallStatus();

                    //Did call status change?
                    if (boolCurrentCallMode != callMode)
                    {
                        WriteLog("Switching CallMode mode");

                        //If Call is active
                        if (callMode == true)
                        {
                            //Call is active
                            WriteLog("Call is active, reduce Navigator volume");
                            //Set to CF ATT level
                            SendCommand("$sound_volume=" + CF_getConfigSetting(CF_ConfigSettings.AttMuteLevel).ToString() + "\r\n", false, TCPCommand.SoundVolume);
                        }
                        else
                        {
                            //Configure Navigator Audio back to what it was
                            WriteLog("Call is not active");
                            
                            //Enable or disable Navigator voice prompts?
                            if (CF_getConfigFlag(CF_ConfigFlags.GPSEnableVoice) == true)
                            {
                                SendCommand("$sound_volume=on\r\n", false, TCPCommand.SoundVolume);

                                //Configure Navigator Audio volume
                                if (CF_getConfigFlag(CF_ConfigFlags.GPSSetNavSoundLevel) == true)
                                {
                                    SendCommand("$sound_volume=" + CF_getConfigSetting(CF_ConfigSettings.GPSNavSoundLevel).ToString() + "\r\n", false, TCPCommand.SoundVolume);
                                }
                            }
                            else
                            {
                                SendCommand("$sound_volume=0\r\n", false, TCPCommand.SoundVolume);
                                SendCommand("$sound_volume=off\r\n", false, TCPCommand.SoundVolume);
                            }
                        }

                        //Update current mode
                        boolCurrentCallMode = callMode;
                    }
                }
                catch (Exception errMsg) { WriteLog("Failed to change sound warning mode: " + errMsg.Message); }
            }
         }

        //Switch to status window
        private void btnSectionStatus_Click(object sender, MouseEventArgs e)
        {
            if (boolMainScreen)
            {
                WriteLog("Switch to status screen");
                thepanel.Visible = false;
                SendCommand("$minimize\r\n", false, TCPCommand.Minimize);
                boolMainScreen = false;
                CF_localskinsetup();

                //Timer to update GPS Status screen
                NavDestinationTimer.Interval = 1000; // Increase freqency of updates from Navigator
                NavStatustimer_Tick(null, null); //Make first update now
                NavStatustimer.Enabled = true;               
            }
            else
            {
                WriteLog("Switch to Navigator");
                NavStatustimer.Enabled = false; //Stop the updates
                NavDestinationTimer.Interval = 5000; // Put back to 5000ms

                boolMainScreen = true;
                CF_localskinsetup();
                thepanel.Visible = true;
                
                SendCommand("$maximize\r\n", false, TCPCommand.Maximize);
            }
        }

        
        //Resize window
        private void btnMinMax_Click(object sender, MouseEventArgs e)
        {
            // The MinMax button has been clicked...
            WriteLog("MinMax Button clicked.");
            try
            {
                if (boolFullScreen) SetNonFullScreen(); else SetFullScreen();
            }
            catch (Exception errmsg) { WriteLog(errmsg.ToString()); }
        }

        private void SetFullScreen()
        {
            //Not currently fullscreen, change to fullscreen
            WriteLog("Configure for fullscreen");

            if (boolMainScreen)
            {
                //Resize section
                this.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetSectionAttribute("Navigator", ("fullbounds").ToLower(), base.pluginSkinReader)));

                //Resize panel
                this.thepanel.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "PanelNavigator", ("fullbounds").ToLower(), base.pluginSkinReader)));

                //Repos buttons and labels
                for (int i = 0; i < buttonArray.Length; i++) RePosButton(buttonArray[i].Name, "fullbounds");
                for (int i = 0; i < labelArray.Length; i++) RePosLabel(labelArray[i].Name, "fullbounds");

                //Configure screen size. Use the panel size            
                SendCommand("$window=0,0," + thepanel.Bounds.Width.ToString() + "," + thepanel.Bounds.Height.ToString() + ",noborder\r\n", false, TCPCommand.Window);
            }
            else
            {
                //Resize section
                //Do NOT enable this, causes Navigator to frequently crash! Workaround by modifying Skin.xml
                //this.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetSectionAttribute("GPSStatus", ("fullbounds").ToLower(), base.pluginSkinReader)));
            }

            //Refresh screen
            this.Invalidate();

            boolFullScreen = true;
        }


        private void SetNonFullScreen()
        {
            WriteLog("Configure for non-fullscreen");

            if (boolMainScreen)
            {
                //Resize section
                this.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetSectionAttribute("Navigator", ("bounds").ToLower(), base.pluginSkinReader)));

                //Resize panel
                thepanel.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "PanelNavigator", ("bounds").ToLower(), base.pluginSkinReader)));

                //Repos buttons and labels
                for (int i = 0; i < buttonArray.Length; i++) RePosButton(buttonArray[i].Name, "bounds");
                for (int i = 0; i < labelArray.Length; i++) RePosLabel(labelArray[i].Name, "bounds");

                //Configure screen size. Use the panel size              
                SendCommand("$window=0,0," + thepanel.Bounds.Width.ToString() + "," + thepanel.Bounds.Height.ToString() + ",noborder\r\n", false, TCPCommand.Window);
            }
            else
            {
                //Resize section
                //Do NOT enable this, causes Navigator to frequently crash! Workaround by modifying Skin.xml
                //this.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetSectionAttribute("GPSStatus", ("bounds").ToLower(), base.pluginSkinReader)));
            }

            //Refresh screen
            this.Invalidate();

            boolFullScreen = false;
        }


        //Reposition buttons when changing skin size
        private void RePosButton(string strID, string strSize)
        {
            try
            {
                CFControls.CFButton a = new CFControls.CFButton();
                a = buttonArray[CF_getButtonID(strID)];
                a.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", strID, (strSize).ToLower(), base.pluginSkinReader)));
            }
            catch { WriteLog("Failed to set button's new position, or button does not existing in skin: " + strID); } //JJ: Added button ID
        }

        private void RePosLabel(string strID, string strSize)
        {
            try
            {
                CFControls.CFLabel a = new CFControls.CFLabel();
                a = labelArray[CF_getLabelID(strID)];
                a.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", strID, (strSize).ToLower(), base.pluginSkinReader)));
            }
            catch { WriteLog("Failed to set label's new position, or label does not existing in skin: " + strID); } //JJ: Added label ID
        }

        
        //Write to plugin log file
        public void WriteLog(string msg)
        {
            try
            {
                if (Boolean.Parse(this.pluginConfig.ReadField("/APPCONFIG/LOGEVENTS")))
                    CFTools.writeModuleLog(msg, LogFilePath);
            }
            catch (Exception errMsg) { CFTools.writeError("Unable to log to plugin log file: " + errMsg.Message); }
        }
#endregion

        //Set terminate to true if kill process
        public bool TerminateOrphanedProcess(bool terminate)
        {
            bool boolTerminateOrphanedProcess = false; //Assume no killing...

            try
            {
                WriteLog("Listing all processes to check if " + EXEName + " is running");
                Process[] processlist = Process.GetProcesses();
                foreach (Process theprocess in processlist)
                {
                    //WriteLog("Process: '" + theprocess.ProcessName + "' ID: '" + theprocess.Id + "'");
                    if (theprocess.ProcessName.Contains("PC_Navigator"))
                    {
                        WriteLog("PC_Navigator is running");
                        boolTerminateOrphanedProcess = true;
                        if (terminate)
                        {
                            WriteLog("Terminating process");
                            theprocess.Kill();
                            System.Threading.Thread.Sleep(1000); // Allow the process time to terminate
                        }
                        return boolTerminateOrphanedProcess;    //LK, 29-nov-2013: Return here, no need to continue search
                    }
                }
            }
            catch (Exception errMsg)
            {
                WriteLog("Error getting Process information: " + errMsg.Message);
            }

            return boolTerminateOrphanedProcess;
        }

        //Thread to monitor CF audio level and adjust Navigator
        private void DynamicAudioLevel_Worker()
        {
            int intSleep = 500;
            int oldVolume = -1;

            //Ugly, but avoid error messages until TCP connection is established by waiting here
            while (server.Connected == false && boolConnecting == false)
            {
                System.Threading.Thread.Sleep(intSleep);
            }

            //Keep checking
            while (true)
            {
                try
                {
                    //Get CF Volume level
                    int CFVolume = CF_getAudioLevel(CF_AudioLevels.VOLUME) * 100 / 65535;

                    //Get GPS offset level
                    int GPSVolume = int.Parse(CF_getConfigSetting(CF_ConfigSettings.GPSNavSoundLevel));

                    //Combined new level
                    int CombinedVolume = CFVolume + GPSVolume;

                    //Sanity check
                    if (CombinedVolume > 100) CombinedVolume = 100;
                    if (CombinedVolume < 0) CombinedVolume = 0;

                    //Only set new level if different
                    if (oldVolume != CombinedVolume)
                    {
                        //Write to log
                        WriteLog("CF Volume : " + CFVolume.ToString() + "%, GPSVolume: " + GPSVolume.ToString() + "%, Combined: " + CombinedVolume.ToString());
                        //Adjust Navigator to new level
                        SendCommand("$sound_volume=" + CombinedVolume.ToString() + "\r\n", true, TCPCommand.SoundVolume);
                        //Save new volume level
                        oldVolume = CombinedVolume;
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Failed to modify volume. Error: " + ex.ToString());
                };

                WriteLog("Sleep for '" + intSleep.ToString() + "' milliseconds");
                System.Threading.Thread.Sleep(intSleep);
            }
        }


        public void CloseNavigator()
        {
            //User really wants to exit Navigator...
            boolExit = true;
            
            //LK, 25-nov-2013: Only close when started
            if (pNavigator != null)
            {
                IntPtr mainWindowHandle = pNavigator.MainWindowHandle;  //LK, 29-nov-2013: Cache before close

                //Close Navigator before disconnecting, but expect TCP link to die, as Paid version does not support normal close command
                if (boolFREE == false)
                {
                    //CloseMainWindow() does not work on paid version
                    SendCommand("$exit\r\n", false, TCPCommand.Exit);
                    Thread.Sleep(1000);
                }

                //Disconnect the TCP connection so it can be re-established                
                WriteLog("Disconnecting TCP connection for reuse");
                try
                {
                    //Disconnect the TCP connection so it can be re-established                    
                    try
                    {
                        //This is known to fail on WinXP, so provide an alternative
                        if (CF_getConfigSetting(CF_ConfigSettings.OSVersion).ToString().ToUpper() != "XP".ToUpper()) server.Disconnect(true); else server.Close();
                    }
                    catch (Exception errMsg) { WriteLog("Failed to disconnect: " + errMsg.Message); }
                }
                catch (Exception errMsg)
                {
                    WriteLog("Failed to disconnect :" + errMsg.Message);
                }
                finally
                {
                    boolConnecting = false;
                }

                //Stop all timers first to avoid callbacks and additional TCP commands
                nightTimer.Enabled = false;
                muteCFTimer.Enabled = false;
                CallStatusTimer.Enabled = false;
                NavDestinationTimer.Enabled = false;

                if (boolFREE)
                {
                    //CloseMainWindow() does not work on paid version
                    pNavigator.CloseMainWindow(); //Ask nicely, just like ALT-F4
                }

                //Louk's Pipe
                if (this.pipeServer != null && this.pipeServer.Running)//LK, 29-nov-2013: Added check for null object
                {
                    WriteLog("Closing Louk's pipe");
                    this.pipeServer.Stop();
                    this.pipeServer.MessageReceived -= new PipeServer.Server.MessageReceivedHandler(pipeServer_MessageReceived);
                    //Don't discard pipeServer here, only in pluginClose()
                }
                else WriteLog("Can't stop a non-running pipe-server");
                
                //Wait for Navigator to close before swapping XML files around
                for (int loop = intExitCounter; loop > 0; loop--)   //LK, 29-nov-2013: count down...    //was 100
                {
                    if (TerminateOrphanedProcess(false) == false)
                    {
                        //No longer running, exit out of loop
                        break;
                    }
                    WriteLog("Waiting for Navigator to close: " + loop.ToString());
                    //LK, 29-nov-2014: Use the applications main window handle instead of the panel handle mHandlePtr
                    try
                    {
                        //OSM version have the Ad screen at the end when closing
                        if (boolFREE)
                        {
                            ClickOnPoint(mainWindowHandle, new Point(100, 200));
                        }
                        pNavigator.WaitForExit(500);
                    }
                    catch (Exception errMsg) { WriteLog("Failed to stop application: " + errMsg.Message); } //LK,28-nov-2013: Catch unhandled pointer exceptions
                }
            }

            //Release resources as all mouse clicks etc are done
            pNavigator.Close(); 

            //Assume didn't exit. Force close if still running...
            try
            {
                TerminateOrphanedProcess(true);
            }
            catch (Exception errMsg) { WriteLog("Failed to terminate process: " + errMsg.Message); }  //LK,28-nov-2013: Add reason to message
        }
			
#region CF events
        // Fired when the power mode of the operating system changes
        private void OnPowerModeChanged(object sender, CFPowerModeChangedEventArgs e)
        {
            WriteLog("OnPowerModeChanged - start()");
            WriteLog("OnPowerModeChanged '" + e.Mode.ToString() + "'");

            CFTools.writeLog(PluginName, "OnPowerModeChanged", e.Mode.ToString());

            //If suspending
            if (e.Mode == CFPowerModes.Suspend)
            {
                CloseNavigator();

                //Make sure user is not prompted to restart Navigator and make sure we're in Suspend mode
                boolExit = true;
                boolSuspend = true;
            }

            //If resuming from sleep
            if (e.Mode == CFPowerModes.Resume)
            {
                StartNavigator();

                //Reset timers
                CallStatusTimer.Enabled = true;
                NavDestinationTimer.Enabled = true;

                //User can be prompted to restart Navigator again
                boolExit = false;

                //No longer in suspend mode
                boolSuspend = false;

                //If suspend was initiated when plugin active, we need to ensure CF_pluginShow() is called
                if (this.Visible == true) CF_pluginShow();
            }

            WriteLog("OnPowerModeChanged - end()");
            return;
        }

#endregion

	}
}
