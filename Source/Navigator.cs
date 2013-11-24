/*
 * Copyright 2013, 2014, John Jore
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
 * Bug: TCP setup on Navigator? (Workaround implemented, modify settings.xml)
 * 
 * Move SendCommand and receive to its own thread?
 * Parse TCP responses from Navigator... counter++ for each SendCommand. Create FIFO buffer? Create thread?
 * 
 * Setup screen values not refreshing
 * How to detect if media is playing or not - Not required?
 * Mute: Mutes everything, including navigator prompts
 * 
 * Remove non-used functions
 */

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
//using System.Net;
using System.Net.Sockets;
using System.Text;
using Timer = System.Windows.Forms.Timer;

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
        public static string PluginXmlElement = "Navigator";
        private const string PluginPath = @"plugins\" + PluginName + @"\";
		private const string PluginPathSkins = PluginPath + @"Skins\";
		private const string PluginPathLanguages = PluginPath + @"Languages\";
		private const string PluginPathIcons = PluginPath + @"Icons\";
        private const string ConfigurationFile = "config.xml";
		private const string LogFile= "Navigator.log";        
        public static string LogFilePath = CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + LogFile;
        public static string settingsPath = CFTools.AppDataPath + "\\system\\settings.xml";
        public static string configPath = CFTools.AppDataPath + "\\system\\config.xml";	//LK, 20-nov-2013: Needed to check if this is the current navigation app
                
        /**/ //This should be moved to a AppConfigure class?
        private string strEXEPath = "";                     // Folder and EXE name
        private string strEXEParameters = "";               // Paramters to use
        private bool boolFullScreen = false;                // Full screen?
        private bool boolExit = false;                      // Set True if hibernating
        private bool boolFREE = true;                       // Free edition?
        private bool boolOSMOK = false;                     // If true, supresses OSM License prompt
        private bool boolAlerts = false;                    // Show alerts if NOT active plugin?
        private bool boolNamedPipes = false;                // Use Louk's named pipes for mute/unmute?
        private bool boolMainScreen = true;                 // Start in main navigation screen
        private bool boolInMutePeriod = false;              // True if already in MUTE period
        private int intCFVolumeLevel = 0;                   // CF's volume level before "ATT"
        private IntPtr mHandlePtr;                          // var for window handle number to catch
        CFControls.CFPanel thepanel = null;                 // The panel to 'project' Navigator into        
        Process pNavigator = null;                          // Navigator's process
        private bool boolCurrentNightMode = false;          // Are we currently in night mode? (We don't actually know this)
        private bool boolCurrentCallMode = false;           // Are we currently on the phone?
        private string strAppDataPath = "";                 // Path to Navigator's XML file
        private CFNavLocation navCurrentLocation = new CFNavLocation();       // Navigator's current location
        private NavStats _navStats = new NavStats();         // Navigation statistics

        Timer nightTimer = new System.Windows.Forms.Timer(); // timer for switching day/night skin      
        Timer muteCFTimer = new System.Windows.Forms.Timer();    // timer for mute'ing CF
        Timer CallStatusTimer = new System.Windows.Forms.Timer();    // timer for checking if a call is in progress
        Timer NavDestinationTimer = new System.Windows.Forms.Timer();    // timer for checking for destination proximity if not active plugin
        Timer NavStatustimer = new System.Windows.Forms.Timer();        //timer for updating GPS status screen

        //From Mark
        public override event CFNavVoiceEventHandler CF_navVoiceEvent;
        private delegate void VoidDelegate();
                
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, string lParam);

        //Placeholders
        //[DllImport("user32.dll")]
        //static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        //[DllImport("user32.dll")]
        //static extern bool MoveWindow(IntPtr Handle, int x, int y, int w, int h, bool repaint);

        //[DllImport("user32.dll")]
        //private static extern bool SetForegroundWindow(IntPtr hWnd);

        //[DllImport("user32.dll")]
        //private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        /**/ //Remove later if not used
        ////LK, 20-nov-2013: Experimental
        //[DllImport("user32.dll")]
        //private static extern IntPtr GetForegroundWindow();

        //[DllImport("user32.dll")]
        //private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);        
        
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
                WriteLog(PluginName + " starting");
                WriteLog("CF_pluginInit");

                // CF3_initPlugin() Will configure pluginConfig and pluginLang automatically
                // All plugins must call this method once
                CF3_initPlugin(PluginName, true);
                ICFSetup = new NavSetup(this, pluginConfig, pluginLang);

                //Clear old values from log file
                CFTools.writeModuleLog("startup", LogFilePath);

                //Log current version of DLL for debug purposes
                WriteLog("Assembly Version: '" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + "'");

                // All controls should be created or Setup in CF_localskinsetup.
                // This method is also called when the resolution or skin has changed.
                CF_localskinsetup();

                //Get configuration settings
                LoadSettings();
                
                //Setup the Panel used by PC_Navigator.exe
                WriteLog("Init the panel to use for MapFactor");
                thepanel = new CFControls.CFPanel();

                //Associate 'thepanel' with the panel defined in the skin.xml
                thepanel = panelArray[CF_getPanelID("PanelNavigator")];

                //LK, 18-nov-2013: Added some panel settings that might help parenting
                thepanel.ParentForm = this;
                thepanel.ParentFocus = true;
                thepanel.PreRenderPreviousImage = true;
                thepanel.BackColor = Color.Black;
                thepanel.Name = "ThePanel";

                //Get the handle so we can associate it with the process later
                mHandlePtr = thepanel.Handle;

                //Timer for day/night skin swap                
                nightTimer.Interval = 2500; // Check every 2.5 seconds for a change
                nightTimer.Enabled = false;
                nightTimer.Tick += new EventHandler(nightTimer_Tick);

                //Timer for mute'ing CF while Navigator speaks
                muteCFTimer.Interval = 2500; // Unpause audio after this duration. Named pipe will change value as it receives unmute notice
                muteCFTimer.Enabled = false;
                muteCFTimer.Tick += new EventHandler(muteCFTimer_Tick);

                //Timer for getting Navigation Stats
                CallStatusTimer.Interval = 2000; // Check every
                CallStatusTimer.Enabled = false;
                CallStatusTimer.Tick += new EventHandler(CallStatusTimer_Tick);
               
                //Timer to use to check if arrived at destination
                NavDestinationTimer.Interval = 5000; // Wait this long...
                NavDestinationTimer.Enabled = false;
                NavDestinationTimer.Tick += new EventHandler(NavDestinationTimer_Tick);

                // Creates new events to catch when CF is being closed, loaded or the power mode changed
				this.KeyDown += new KeyEventHandler(Navigator_KeyDown);
                this.CF_events.CFPowerModeChanged += new CFPowerModeChangedEventHandler(OnPowerModeChanged); //Hibernation support
                this.CF_events.applicationClosing += OnApplicationClosing;
                this.CF_events.applicationLoaded += OnApplicationLoaded;
                this.CF_events.CFPowerModeChanged += OnPowerModeChanged;

                //Check if already running
                if (TerminateOrphanedProcess(true))
                {
                    if (TerminateOrphanedProcess(true)) this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/EMBEDDINGFAILED"), "AUTOHIDE");
                }

                //Modify Navigator's Settings XML file...
                ConfigureNavigatorXML();

                //Using named pipe? 
                if (boolNamedPipes) 
                    patchNavigator(); 
                else 
                    unpatchNavigator();
                
                /**/
                //Force logging...
                this.pluginConfig.WriteField("/APPCONFIG/LOGEVENTS", "True", true);

                // Active navigation engine?
                if (ReadCFValue("/APPCONFIG/NAVENGINE", "NAVIGATOR", configPath))
                {
                    //Launch navigator
                    LaunchNavigator();

                    //LK, 23-nov-2013: Start timers
                    CallStatusTimer.Enabled = true;
                    NavDestinationTimer.Enabled = true;

                    //Louk's Named pipe server
                    if (!this.pipeServer.Running)
                    {
                        this.pipeServer.PipeName = @"\\.\Pipe\" + "NavigatorCF4Plugin";
                        this.pipeServer.Start();
                        WriteLog("Named pipe '" + pipeServer.PipeName + "' is '" + (this.pipeServer.Running).ToString() + "'");

                        //Create event handler
                        this.pipeServer.MessageReceived += new PipeServer.Server.MessageReceivedHandler(pipeServer_MessageReceived);
                    }
                    else
                        WriteLog("Named pipe server is already running");

                    //Get Fullscreen information
                    boolFullScreen = CF_getConfigFlag(CF_ConfigFlags.GPSFullscreen);

                    //Set correct size
                    if (boolFullScreen) SetFullScreen(); else SetNonFullScreen();

                    ConfigureNavigator();
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
            WriteLog("CF_localskinsetup");

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
                WriteLog("skin setup: '" + ex.Message);
            }

            // Read the skin file, controls from the skin will be automatically created
            // CF_localskinsetup() should always call CF3_initSection() first, with the exception of setting any
            // CF_displayHooks flags, which affect the behaviour of the CF3_initSection() call.
            CF3_initSection(PluginName);
                        
            // Set display hook so that future CF3_initSection() calls will not clear the panels array
            CF_displayHooks.clearControl.panels = false;
            
            // Set up custom button handlers for buttons without a CML action in skin.xml
            this.CF_createButtonClick("MinMax", new MouseEventHandler(btnMinMax_Click));

            /**/
            //LK, 22-nov-2013: In the case of a skin change, adjust panel size.
            if (thepanel != null)   //Anytime, but the first (when called from CF_pluginInit())
            {
                //LK, 22-nov-2013: Experimental
                CFControls.CFPanel tmpPanel = new CFControls.CFPanel();
                tmpPanel = panelArray[CF_getPanelID("PanelNavigator")];
                tmpPanel.Visible = false;
                tmpPanel.Enabled = false;
                tmpPanel.BackColor = Color.Blue;

                if (boolFullScreen)
                    SetFullScreen();
                else
                    SetNonFullScreen();
            }

            CFTools.writeLog(PluginName, "CF_localskinsetup", "Exiting");
		}

        
		/// <summary>
		/// This is called by the system when it exits or the plugin has been deleted.
		/// </summary>
		public override void CF_pluginClose()
		{
            WriteLog("CF_pluginClose() - Start");

            //User really wants to exit Navigator...
            boolExit = true;

            //Stop all timers
            nightTimer.Enabled = false;
            muteCFTimer.Enabled = false;
            CallStatusTimer.Enabled = false;
            NavDestinationTimer.Enabled = false;

            //Louk's Pipe
            if (this.pipeServer.Running)
            {
                WriteLog("Closing Louk's pipe");
                this.pipeServer.Stop();
                this.pipeServer.MessageReceived -= new PipeServer.Server.MessageReceivedHandler(pipeServer_MessageReceived);
                this.pipeServer = null;
            }
            else WriteLog("Can't stop a non-running pipe-server");

            //Switch to Nav if not free edition
            if (boolFREE)
            {
                WriteLog("Switch to NAV and close it");
                CF3_executeCMLAction("Centrafuse.CFActions.Nav");
                System.Threading.Thread.Sleep(500);
                if (!boolMainScreen)
                {
                    thepanel.Visible = true; // Make sure its visible, and not behind stats screen

                    //LK, 19-nov-2013: Set focus to the panel
                    thepanel.Focus();

                    SendCommand("$maximize\r\n", false, TCPCommand.Maximize);
                }
            }

            //Close the TCP connection
            try
            {
                server.Shutdown(SocketShutdown.Both);
                server.Disconnect(false);
                server.Close();
                WriteLog("Closed Connection");
            }
            catch { WriteLog("Failed to close connection."); }

            //Close it
            pNavigator.CloseMainWindow();
            pNavigator.Close();                     

            //Wait for Navigator to close before swapping XML files around
            for (int loop=0; loop<100; loop++)
            {                
                if (TerminateOrphanedProcess(false) == false)
                {
                    WriteLog("Navigator no longer running. Gracefull shutdown");
                    break;
                }
                WriteLog("Waiting for Navigator to close");
                if (boolFREE) ClickOnPoint(mHandlePtr, new Point(100, 200));

                System.Threading.Thread.Sleep(20);
            }

            //Assume didn't exit. Force close if still running...
            try
            {
                TerminateOrphanedProcess(true);
            }
            catch { WriteLog("Failed to terminate process"); }
            
            //Put the configuration files back again
            try { 
                System.IO.File.Move(strAppDataPath + "\\settings.xml", strAppDataPath + "\\settings.xml.CF"); 
            }
            catch {
                WriteLog("Failed to restore settings.xml to .CF"); 
            }
            try {
                System.IO.File.Move(strAppDataPath + "\\settings.xml.NAV", strAppDataPath + "\\settings.xml");
            }
            catch { 
                WriteLog("Failed to restore .NAV to settings.xml"); 
            }

            base.CF_pluginClose(); // calls form Dispose() method
            WriteLog("CF_pluginClose() - End");
		}
		

		/// <summary>
		/// This is called by the system when a button with this plugin action has been clicked.
		/// </summary>
		public override void CF_pluginShow()
		{           
            WriteLog("Start: CF_pluginShow");

            //LK, 18-nov-2013: Just make the panel visible (don't load again)
            thepanel.Visible = true;

            //Configure night mode toggle option
            SetDayNightToggle();
                       
            //Resume window
            SendCommand("$maximize\r\n", false, TCPCommand.Maximize);
                                               
            base.CF_pluginShow(); // sets form Visible property
		}

        /// <summary>
        /// This is called by the system when this plugin is minimized/exited (when screen is left).
        /// </summary>
        public override void CF_pluginHide()
        {
            try
            {
                //Make sure its not ontop of CF
                SendCommand("$minimize\r\n", false, TCPCommand.Minimize);

                //Don't check for skin change. Plugin not visible => no update required
                nightTimer.Enabled = false;
            }
            catch { WriteLog("Failed to send minimize command"); }

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
            WriteLog("CF_pluginCommand: " + command + " " + param1 + ", " + param2);
		}

        //Launch Navigator
        private bool LaunchNavigator()
        {
            //Launch Navigator                    
            try
            {
                if (ReadCFValue("/APPCONFIG/NAVENGINE", "NAVIGATOR", configPath))
                {
                    pNavigator = new Process();
                    pNavigator.StartInfo.FileName = strEXEPath + "\\PC_Navigator.exe";
                    pNavigator.StartInfo.Arguments = "--window_border=no " + strEXEParameters + " --window_position=" + this.pluginConfig.ReadField("/APPCONFIG/WINDOWSIZE");
                    try
                    {
                        if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/NOHIRES")) == true)
                        {
                            pNavigator.StartInfo.Arguments = pNavigator.StartInfo.Arguments + " --nohires";
                        }
                    }
                    catch { WriteLog("Failed to interpret NOHIRES setting"); }
                    //This does not work: "--tcpserver=127.0.0.1:" + intTCPPort.ToString(); Settings.XML modified
                    WriteLog("Launching Navigator using: '" + pNavigator.StartInfo.FileName + "'");
                    WriteLog("Parameters: '" + pNavigator.StartInfo.Arguments + "'");
                    pNavigator.EnableRaisingEvents = true;
                    pNavigator.Exited += new EventHandler(pNavigator_Exited);
                    
                    //LK, 18-nov-2013: Avoid flickering windows at startup
                    pNavigator.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                    pNavigator.Start();
                    
                    //Wait for Navigator to start
                    TimeSpan totalProcessorTime = new TimeSpan();
                    totalProcessorTime = pNavigator.TotalProcessorTime;
                    pNavigator.PriorityClass = ProcessPriorityClass.High;   //LK, 24-nov-2013: Top priority while starting 
                    System.Threading.Thread.Sleep(500); // Allow the process to open it's window
                    pNavigator.WaitForInputIdle();     //Dont use this, the window location is messed up. Can't press OK        

                    //ShowWindowAsync(pNavigator.MainWindowHandle, (int)showWindowAttribute.SW_MINIMIZE);
                    SendMessage(pNavigator.MainWindowHandle, (int)showWindowAttribute.SW_MINIMIZE, 0, string.Empty);

                    //LK, 18-nov-2013: Attach to hidden panel right away
                    int iRetry = 0;

                    WriteLog("Navigator started, waiting for process to idle");
                    while (pNavigator.TotalProcessorTime > totalProcessorTime || pNavigator.TotalProcessorTime == TimeSpan.Zero)
                    {
                        totalProcessorTime = pNavigator.TotalProcessorTime;
                        WriteLog("Waiting for Navigator to get idle... (totalProcessorTime used = " + totalProcessorTime);
                        System.Threading.Thread.Sleep(500);
                        if (iRetry++ > 20)
                            break;
                    };

                    if (SetParent(pNavigator.MainWindowHandle, mHandlePtr) == IntPtr.Zero)
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        WriteLog("Docking failed, last error = " + lastError);
                        System.Threading.Thread.Sleep(500);
                        throw new Exception("Failed to dock Navigator");
                    };

                    pNavigator.PriorityClass = ProcessPriorityClass.AboveNormal;    //LK, 24-nov-2013: Lower the priority to "normal"
                    //Form fNavigator = (Form)FromHandle(pNavigator.MainWindowHandle);
                    //if (fNavigator != null)
                    //{
                    //    Form mainForm = (Form)thepanel.Parent;
                    //    fNavigator.Owner = mainForm;
                    //}
                    
                    WriteLog("Connected to panel");
                    thepanel.Visible = false;
                    WriteLog("Panel hidden");                    
                    WriteLog("Launched");

                    //Say YES to OSM data usage, if user changed to ON
                    try
                    {
                        if (boolOSMOK && boolFREE)
                        {
                            System.Threading.Thread.Sleep(500); // Allow the process to open it's window
                            WriteLog("Sending ENTER");
                            SendKeys.SendWait("{ENTER}");
                        }
                    }
                    catch { WriteLog("Failed to send OK to OSM usage"); }

                    //Navigator should be launched and running
                    return true;
                }
            }
            catch (Exception ex)
            {
                WriteLog("Failed to launch Navigator: " + ex.Message);
                CFTools.writeError(ex.Message, ex.StackTrace);
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

            //Set initial day/night mode
            boolCurrentNightMode = CF_getConfigFlag(CF_ConfigFlags.NightSkinFlag);
            if (boolCurrentNightMode) SendCommand("$set_mode=night\r\n", false, TCPCommand.DayNight); else SendCommand("$set_mode=day\r\n", false, TCPCommand.DayNight);

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



        // Handle Navigator Exited event
        private void pNavigator_Exited(object sender, System.EventArgs e)
        {
            //User really wants to exit Navigator...
            if (!boolExit)
            {
                WriteLog("Navigator no longer running. Exit code: " + pNavigator.ExitCode.ToString());

                //If navigator is not running, its probably half hidden behind CF. Switch to Nav screen
                CF3_executeCMLAction("Centrafuse.CFActions.Nav");

                //Get current timer status
                bool nightTimer_Status = nightTimer.Enabled;
                bool muteCFTimer_Status = muteCFTimer.Enabled;                
                bool CallStatusTimer_Status = CallStatusTimer.Enabled;
                bool NavDestinationTimer_Status = NavDestinationTimer.Enabled;

                //Stop all timers. Call back does not work and causes grief...
                nightTimer.Enabled = false;
                muteCFTimer.Enabled = false;
                CallStatusTimer.Enabled = false;
                NavDestinationTimer.Enabled = false;

                //Launch Navigator
                LaunchNavigator();

                //Disconnect the TCP connection so it can be re-established
                server.Disconnect(true);

                //Connect to panel                
                SetParent(pNavigator.MainWindowHandle, mHandlePtr);
                WriteLog("Connected to Panel");

                //Set correct size
                if (boolFullScreen) SetFullScreen(); else SetNonFullScreen();

                //Re-configure Navigator
                ConfigureNavigator();

                //Set timers back the way they were
                nightTimer.Enabled = nightTimer_Status;
                muteCFTimer.Enabled = muteCFTimer_Status;
                CallStatusTimer.Enabled = CallStatusTimer_Status;
                NavDestinationTimer.Enabled = NavDestinationTimer_Status;
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
            if (state != CF_ButtonState.Click)
                return false;

            WriteLog("CF_pluginCMLCommand: " + id);


            switch (id.ToUpper())
            {
                case "GOTOSTATUS": //Flip Status / Navigation
                    btnSectionStatus_Click(null, null);
                    return true;
            }

            return false;
        }

        private void OnApplicationClosing(object sender, EventArgs e)
        {
            CFTools.writeLog(PluginName, "OnApplicationClosing", "");
        }
        
        private void OnApplicationLoaded(object sender, EventArgs e)
        {
            CFTools.writeLog(PluginName, "OnApplicationLoaded", "");
        }

#endregion
		
#region System Functions

        public void LoadSettings()
        {
            // The display name is shown in the application to represent
            // the plugin.  This sets the display name from the configuration file.
            this.CF_params.displayName = this.pluginLang.ReadField("/APPLANG/NAVIGATOR/DISPLAYNAME");
            CFTools.writeLog("Navigator", "New display name = " + this.CF_params.displayName);
            
            //Get Navigator Configuration
            try
            {
                WriteLog("App Load Config File");

                // Mute/Unmute prompt?
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
                    boolFREE = true;
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

                    //Set some sane default value
                    if (strEXEPath == "")
                    {
                        string strEXE = "\\Navigator12\\PC_Navigator";
                        string strTest = "C:\\Program Files (x86)" + strEXE;
                        FileInfo fi1 = new FileInfo(strTest + "\\PC_Navigator.exe");
                        if (fi1.Exists) {
                            this.pluginConfig.WriteField("/APPCONFIG/EXEPATH", strTest, true);
                            strEXEPath = strTest;
                        };
                        strTest = "C:\\Program Files" + strEXE;
                        FileInfo fi2 = new FileInfo(strTest + "\\PC_Navigator.exe");
                        if (fi2.Exists) { 
                            this.pluginConfig.WriteField("/APPCONFIG/EXEPATH", "C:\\Program Files" + strEXE, true);
                            strEXEPath = strTest;
                        };
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

                //Get correct atlas setting:
                try
                {
                    //Read from registry 
                    /**/ //Should not read hardcoded registry values. Either move to xml file, or enumerate registry
                    RegistryKey rk = Registry.LocalMachine;
                    RegistryKey sk1 = rk.OpenSubKey("SOFTWARE\\MapFactor\\set\\pcnavigator_12");

                    if (boolFREE)
                    {
                        //Add the '_free' text to the filename        
                        string strTmp = sk1.GetValue("Atlas").ToString();
                        strTmp = strTmp.ToUpper();
                        if (!strTmp.Contains("_FREE.IDC")) 
                            strEXEParameters = strEXEParameters + " --atlas=" + strTmp.Substring(0, strTmp.Length - 4) + "_free.idc";
                        else 
                            strEXEParameters = strEXEParameters + " --atlas=" + sk1.GetValue("Atlas").ToString();
                    }
                    else
                    {
                        strEXEParameters = strEXEParameters + " --atlas=" + sk1.GetValue("Atlas").ToString();
                    }
                }
                catch
                {
                    strEXEParameters = strEXEParameters + " --atlas=C:\\ProgramData\\Navigator\\12.3\\atlas_pcn_free.idc";
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
                int intDelay = 0;
                try
                {
                    intDelay = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/AUDIODELAYAFTERMUTE"));
                }
                catch
                {
                    intDelay = 1000;
                    this.pluginConfig.WriteField("/APPCONFIG/AUDIODELAYAFTERMUTE", intDelay.ToString(), true);
                }
                finally
                {
                    WriteLog("intDelay: " + intDelay.ToString());
                }

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
                    WriteLog("CF_ConfigSettings.GPSNavSoundLevel: '" + CF_getConfigSetting(CF_ConfigSettings.GPSNavSoundLevel).ToString() + "'");
                    WriteLog("CF_ConfigSettings.GPSVoicePrompts:  '" + CF_getConfigSetting(CF_ConfigSettings.GPSVoicePrompts).ToString() + "'");
                    WriteLog("CF_ConfigSettings.OSVersion:        '" + CF_getConfigSetting(CF_ConfigSettings.OSVersion).ToString() + "'");
                }
                catch { }
            }
            catch { }
        }

        //Manipulate Navigator's XML file
        private void ConfigureNavigatorXML()
        {
            //Find users profile path with settings.xml file
            RegistryKey rk = Registry.LocalMachine;
            RegistryKey sk1 = rk.OpenSubKey("SOFTWARE\\MapFactor\\set\\pcnavigator_12");

            strAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Navigator\\" + sk1.GetValue("major_ver").ToString() + "." + sk1.GetValue("minor_ver").ToString();
            WriteLog("Path :" + strAppDataPath);

            //Handle XML files
            FileInfo fiXML= new FileInfo(strAppDataPath + "\\settings.xml");
            FileInfo fiNAV = new FileInfo(strAppDataPath + "\\settings.xml.NAV");
            FileInfo fiCF = new FileInfo(strAppDataPath + "\\settings.xml.CF");
                
            //XML File exists
            if (fiXML.Exists)
            {
                WriteLog("XML exists");
                //If NAV files exist, remove NAV
                if (fiNAV.Exists)
                {
                    WriteLog("NAV Exists");
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml.NAV", strAppDataPath + "\\settings.xml." + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")); }
                    catch { }
                    WriteLog("NAV Removed");
                }

                //IF CF exists
                if (fiCF.Exists)
                {
                    WriteLog("CF Exists");
                    //Rename XML to NAV
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml", strAppDataPath + "\\settings.xml.NAV");} catch { }
                    WriteLog("Renamed XML to NAV");

                    //Rename CF to XML
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml.CF", strAppDataPath + "\\settings.xml"); }
                    catch { }
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
                    catch { WriteLog("Failed to create new settings.xml file"); }
                }
            }
            else
            {
                WriteLog("Initial XML file does not exist");
                //Try to use CF first
                if (fiCF.Exists)
                {
                    WriteLog("Using CF");
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml.CF", strAppDataPath + "\\settings.xml"); } catch { };

                    if (fiNAV.Exists == false)
                    {
                        try { System.IO.File.Copy(strAppDataPath + "\\settings.xml", strAppDataPath + "\\settings.xml.NAV"); }
                        catch { };
                    }
                }
                else if (fiNAV.Exists) //try NAV
                {
                    WriteLog("Using NAV");
                    try { System.IO.File.Copy(strAppDataPath + "\\settings.xml.NAV", strAppDataPath + "\\settings.xml"); } catch { };
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
                    catch { WriteLog("Failed to set communication type"); }

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
                    catch { WriteLog("Failed to set IP / Port details"); }

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
                    catch { WriteLog("Failed to disable menu options"); }
                }
                catch { WriteLog("Failed to configure Navigator's settings.xml file"); }
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
            catch { WriteLog("Failed to configure auto day/night mode"); }
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
            catch { WriteLog("Failed to change day/night mode"); }
        }

        //Background polling of Callstatus information
        private void CallStatusTimer_Tick(object sender, EventArgs e)
        {
            //ATT Mute enabled. Lets do some wrk
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
                catch { WriteLog("Failed to change sound warning mode"); }
            }
         }

        //Switch to status window
        private void btnSectionStatus_Click(object sender, MouseEventArgs e)
        {
            if (boolMainScreen)
            {
                WriteLog("Switch to status screen");
                SendCommand("$minimize\r\n", false, TCPCommand.Minimize);
                thepanel.Visible = false;
                SetLabelStatus(true);

                //Timer to update GPS Status screen
                NavStatustimer_Tick(null, null); //Make first update now
                NavStatustimer.Interval = 500; // Wait this long between the next updates
                NavStatustimer.Enabled = true;
                NavStatustimer.Tick += new EventHandler(NavStatustimer_Tick);

                boolMainScreen = false;
            }
            else
            {
                WriteLog("Switch to Navigator");
                NavStatustimer.Enabled = false; //Stop the updates
                SetLabelStatus(false);
                thepanel.Visible = true;
                SendCommand("$maximize\r\n", false, TCPCommand.Maximize);
                boolMainScreen = true;
            }
        }

        
        //Resize window
        private void btnMinMax_Click(object sender, MouseEventArgs e)
        {
            // The MinMax button has been clicked...
            WriteLog("MinMax Button clicked.");
            try
            {
                if (boolFullScreen == true)
                {
                    SetNonFullScreen();
                }
                else
                {
                    SetFullScreen();
                }
            }
            catch (Exception errmsg) { WriteLog(errmsg.ToString()); }
        }

        private void SetFullScreen()
        {
            //Not currently fullscreen, change to fullscreen
            WriteLog("Configure for fullscreen");

            //Resize panel
            this.thepanel.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "PanelNavigator", ("fullbounds").ToLower(), base.pluginSkinReader)));
            
            //Repos buttons
            RePosbutton("GPSStatus", "fullbounds");
            RePosbutton("VolDown", "fullbounds");
            RePosbutton("VolUp", "fullbounds");
            RePosbutton("Exit", "fullbounds");

            //Repos label
            CFControls.CFLabel a = new CFControls.CFLabel();
            a = labelArray[CF_getLabelID("DateTime")];
            a.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "DateTime", ("fullbounds").ToLower(), base.pluginSkinReader)));

            //Configure screen size. Use the panel size
            SendCommand("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n", false, TCPCommand.Window);

            //Resize section
            this.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetSectionAttribute("Navigator", ("fullbounds").ToLower(), base.pluginSkinReader)));

            //Refresh screen
            this.Invalidate();

            boolFullScreen = true;
        }


        private void SetNonFullScreen()
        {
            WriteLog("Configure for non-fullscreen");

            //Resize panel
            thepanel.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "PanelNavigator", ("bounds").ToLower(), base.pluginSkinReader)));

            //Repos buttons
            RePosbutton("GPSStatus", "bounds");
            RePosbutton("VolDown", "bounds");
            RePosbutton("VolUp", "bounds");
            RePosbutton("Exit", "bounds");

            //Reposition label
            CFControls.CFLabel a = new CFControls.CFLabel();
            a = labelArray[CF_getLabelID("DateTime")];
            a.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "DateTime", ("bounds").ToLower(), base.pluginSkinReader)));

            //Configure screen size. Use the panel size
            SendCommand("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n", false, TCPCommand.Window);

            //Resize section
            this.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetSectionAttribute("Navigator", ("bounds").ToLower(), base.pluginSkinReader)));

            //Refresh screen
            this.Invalidate();

            boolFullScreen = false;
        }


        //Reposition buttons when changing size
        private void RePosbutton(string strID, string strSize)
        {
            try
            {
                CFControls.CFButton a = new CFControls.CFButton();
                a = buttonArray[CF_getButtonID(strID)];
                a.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", strID, (strSize).ToLower(), base.pluginSkinReader)));
            }
            catch { WriteLog("Failed to send set button's new position"); }
        }
        
        //Write to plugin log file
        private void WriteLog(string msg)
        {
            try
            {
                if (Boolean.Parse(this.pluginConfig.ReadField("/APPCONFIG/LOGEVENTS")))
                    CFTools.writeModuleLog(msg, LogFilePath);
            }
            catch { }
        }
#endregion

        //Set terminate to true if kill process
        public bool TerminateOrphanedProcess(bool terminate)
        {
            bool boolTerminateOrphanedProcess = false; //Assume no killing...

            try
            {
                WriteLog("Listing all processes to check if PC_Navigator.exe is already running");
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
                    }
                }
            }
            catch
            {
                WriteLog("Error getting Process information");
            }

            return boolTerminateOrphanedProcess;
        }
			
#region CF events

#if !WindowsCE
		private void Navigator_CF_Event_powerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
		{

		}
#endif

        // Fired when the power mode of the operating system changes
        private void OnPowerModeChanged(object sender, CFPowerModeChangedEventArgs e)
        {
            WriteLog("OnPowerModeChanged - start()");
            WriteLog("OnPowerModeChanged '" + e.Mode.ToString() + "'");

            CFTools.writeLog(PluginName, "OnPowerModeChanged", e.Mode.ToString());

            //If suspending
            if (e.Mode == CFPowerModes.Suspend)
            {
                //User really wants to exit Navigator...
                boolExit = true;

                //Stop all timers. Call back does not work and causes grief...
                nightTimer.Enabled = false;
                muteCFTimer.Enabled = false;
                CallStatusTimer.Enabled = false;
                NavDestinationTimer.Enabled = false;
                
                //If navigator is not running, its probably half hidden behind CF. Switch to Nav screen
                //Switch to Nav if not free edition
                if (boolFREE)
                {
                    WriteLog("Switch to NAV and close it");
                    CF3_executeCMLAction("Centrafuse.CFActions.Nav");
                    System.Threading.Thread.Sleep(500);
                    if (!boolMainScreen)
                    {
                        thepanel.Visible = true; // Make sure its visible, and not behind stats screen
                        SendCommand("$maximize\r\n", false, TCPCommand.Maximize);
                    }
                }
                
                //Disconnect the TCP connection so it can be re-established
                server.Disconnect(true);

                //Close it
                pNavigator.CloseMainWindow();
                pNavigator.Close();

                //Wait for Navigator to close
                for (int loop = 0; loop < 100; loop++)
                {
                    if (TerminateOrphanedProcess(false) == false)
                    {
                        WriteLog("Navigator no longer running. Gracefull shutdown");
                        break;
                    }
                    WriteLog("Waiting for Navigator to close");
                    if (boolFREE) ClickOnPoint(mHandlePtr, new Point(100, 100));

                    System.Threading.Thread.Sleep(20);
                }

                //Assume didn't exit. Force close if still running...
                try
                {
                    TerminateOrphanedProcess(true);
                }
                catch { WriteLog("Failed to terminate process"); }
            }

            //If resuming from sleep
            if (e.Mode == CFPowerModes.Resume)
            {
                //If exit, restart Navigator
                boolExit = false;

                //Launch Navigator
                LaunchNavigator();

                //Connect to panel                
                SetParent(pNavigator.MainWindowHandle, mHandlePtr);
                WriteLog("Connected to Panel");

                //Set correct size
                if (boolFullScreen) SetFullScreen(); else SetNonFullScreen();

                //Re-configure Navigator
                ConfigureNavigator();

                //Reset timers
                CallStatusTimer.Enabled = true;
                NavDestinationTimer.Enabled = true;
            }

            WriteLog("OnPowerModeChanged - end()");
            return;
        }

        // If the plugin uses back/forward buttons, we need to catch the left/right keyboard commands too...
		private void Navigator_KeyDown(object sender, KeyEventArgs e)
		{
			e.Handled = true;

			if(e.KeyCode == Keys.Left)
			{
				//---------------------------------------------------------------------------
				// TODO: replace this if needed
				//--------------------------------------------------------------------------- 
				//this.back_Click(this, new MouseEventArgs(MouseButtons.Left,1,0,0,0));
			}
			else if(e.KeyCode == Keys.Right)
			{
				//---------------------------------------------------------------------------
				// TODO: replace this if needed
				//--------------------------------------------------------------------------- 
				//this.forward_Click(this, new MouseEventArgs(MouseButtons.Left,1,0,0,0));
			}
		}

#endregion

	}
}
