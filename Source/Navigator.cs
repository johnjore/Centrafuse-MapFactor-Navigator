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
 * 
 * Move SendCommand and receive to its own thread?
 * Parse TCP responses from Navigator... counter++ for each SendCommand. Create FIFO buffer? Create thread?
 * 
 * Remove non-used functions
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
        private const string REGNavigator = "SOFTWARE\\MapFactor\\set\\pcnavigator_12";
        //public static string PluginXmlElement = "Navigator";
        private const string PluginPath = @"plugins\" + PluginName + @"\";
		//private const string PluginPathSkins = PluginPath + @"Skins\";
		//private const string PluginPathLanguages = PluginPath + @"Languages\";
		//private const string PluginPathIcons = PluginPath + @"Icons\";
        private const string ConfigurationFile = "config.xml";
		private const string LogFile= "Navigator.log";        
        public static string LogFilePath = CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + LogFile;
        public static string settingsPath = CFTools.AppDataPath + "\\system\\settings.xml";
        public static string configPath = CFTools.AppDataPath + "\\system\\config.xml";	//LK, 20-nov-2013: Needed to check if this is the current navigation app
                
        /**/ //This should be moved to a AppConfiguration class?
        private string strEXEPath = "";                     // Folder and EXE name
        private string strEXEParameters = "";               // Paramters to use
        private bool boolFullScreen = false;                // Full screen?
        public bool boolExit = false;                       // Set True if hibernating
        private bool boolFREE = true;                       // Free edition?
        private bool boolOSMOK = false;                     // If true, supresses OSM License prompt
        private bool boolAlerts = false;                    // Show alerts if NOT active plugin?
        private bool boolNamedPipes = false;                // Use Louk's named pipes for mute/unmute?
        private bool boolMainScreen = true;                 // Start in main navigation screen
        private bool boolInMutePeriod = false;              // True if already in MUTE period
        private int muteCFTimerInterval = 1800;             //LK, 30-nov-2013: Cache MuteCfTimer Interval (JJ: Value in milliseconds)
        private int intCFVolumeLevel = 0;                   // CF's volume level before "ATT"
        private IntPtr mHandlePtr;                          // var for window handle number to catch
        CFControls.CFPanel thepanel = null;                 // The panel to 'project' Navigator into        
        Process pNavigator = null;                          // Navigator's process
        private bool boolCurrentNightMode = false;          // Are we currently in night mode? (We don't actually know this)
        private bool boolCurrentCallMode = false;           // Are we currently on the phone?
        private string strAppDataPath = "";                 // Path to Navigator's XML file
        private CFNavLocation navCurrentLocation = new CFNavLocation();       // Navigator's current location
        private NavStats _navStats = new NavStats();         // Navigation statistics

        //Timers
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
        public static extern int SendMessage(IntPtr hWnd, int uMsg, Int16 wParam, int lParam);

        [DllImport("User32.dll")]
        public static extern int PostMessage(IntPtr hWnd, int uMsg, Int16 wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        //Placeholders
        //[DllImport("user32.dll")]
        //static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        //[DllImport("user32.dll")]
        //static extern bool MoveWindow(IntPtr Handle, int x, int y, int w, int h, bool repaint);

        //[DllImport("user32.dll")]
        //private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        /**/ //Remove later if not used
        ////LK, 20-nov-2013: Experimental
        //[DllImport("user32.dll")]
        //private static extern IntPtr GetForegroundWindow();        
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
                NavStatustimer.Interval = 500; // Wait this long between the next updates
                NavStatustimer.Enabled = false;
                NavStatustimer.Tick += new EventHandler(NavStatustimer_Tick);

                // Creates new events to catch power mode change
                this.CF_events.CFPowerModeChanged += OnPowerModeChanged;

                //Check if already running
                if (TerminateOrphanedProcess(true))
                {
                    if (TerminateOrphanedProcess(true)) this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/EMBEDDINGFAILED"), "AUTOHIDE");
                }

                /**/
                //Force logging...
                this.pluginConfig.WriteField("/APPCONFIG/LOGEVENTS", "True", true);

                // Active navigation engine?
                if (ReadCFValue("/APPCONFIG/NAVENGINE", "NAVIGATOR", configPath))
                {
                    //Modify Navigator's Settings XML file...
                    ConfigureNavigatorXML();
                                   
                    //Launch navigator
                    //LK, 30-nov-2013: Moved common code to new method  
                    StartNavigator();
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
                    // Set display hook so that future CF3_initSection() calls will not clear the panels array
                    //+++ CF_displayHooks.clearControl.panels = false;

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

                    //Get the handle so we can associate it with the process later
                    mHandlePtr = thepanel.Handle;

                    //LK, 22-nov-2013: In the case of a skin change, adjust panel size.
                    if (thepanel != null)   //Anytime, but the first (when called from CF_pluginInit())
                    {
                        WriteLog("Panel configured. Configure screen-size");
                        //JJ: Moved here, else never called...
                        if (boolFullScreen)
                            SetFullScreen();
                        else
                            SetNonFullScreen();

                        ////LK, 22-nov-2013: Experimental
                        //CFControls.CFPanel tmpPanel = new CFControls.CFPanel();
                        //tmpPanel = panelArray[CF_getPanelID("PanelNavigator")];
                        //tmpPanel.Visible = false;
                        //tmpPanel.Enabled = false;
                        //tmpPanel.ForeColor = Color.Blue;

                        //LK, 30-nov-2013: Instead of keeping the old panels (leeds to trouble when changing sections), dock again
                        //JJ: This is never called and is the cause of the panel size error
                        //The string "Connected to new panel again" never appears in the log file
                        if (pNavigator != null)
                        {
                            SetParent(pNavigator.MainWindowHandle, mHandlePtr);
                            WriteLog("Connected to new panel again");

                            if (boolFullScreen)
                                SetFullScreen();
                            else
                                SetNonFullScreen();
                        }
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

            //We can discard of the TCP server connection here
            try
            {
                server.Close();
                WriteLog("TCP connection closed ");
            }
            catch (Exception errMsg) { WriteLog("Failed to dispose of TCP connection: " + errMsg.Message); }
            
            
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
                if (ReadCFValue("/APPCONFIG/NAVENGINE", "NAVIGATOR", configPath) && pNavigator == null)
                    StartNavigator();

                if (boolMainScreen) //LK, 30-nov-2013: Aonly do this when in the main screen (not the status screen)
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
            WriteLog("CF_pluginCommand: " + command + " " + param1 + ", " + param2);
		}


        //LK, 30-nov-2013: New method to start PC_navigator, including pipes
        //JJ: Replaced my new StartNavigator :)
        public bool StartNavigator()
        {
            try
            {
                if (LaunchNavigator())
                {
                    //LK, 23-nov-2013: Start timers
                    CallStatusTimer.Enabled = true;
                    NavDestinationTimer.Enabled = true;

                    //Configure named pipe
                    SetupNamedPipe();

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
                    //Ensure Navigator is restarted if it crashes, or user manages to close it. No Navigator => no Nav data in CF
                    pNavigator.Exited += new EventHandler(pNavigator_Exited);
                    
                    //LK, 18-nov-2013: Avoid flickering windows at startup
                    pNavigator.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                    //Start the EXE
                    pNavigator.Start();
                    
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

                        if (iRetry++ > 20)
                            break;

                        System.Threading.Thread.Sleep(500);
                        pNavigator.WaitForInputIdle();
                    };

                    WriteLog("MainWindowHandle before parenting = 0x" + pNavigator.MainWindowHandle.ToString("X"));

                    if (SetParent(pNavigator.MainWindowHandle, mHandlePtr) == IntPtr.Zero)
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        WriteLog("Docking failed, last error = 0x" + lastError.ToString("X"));  //LK, 29-nov-2013: Display Hex value
                        System.Threading.Thread.Sleep(500);
                        CF_systemDisplayDialog(CF_Dialogs.OkBox, pluginLang.ReadField("/APPLANG/NAVIGATOR/FAILEDTODOCK"));
                    }
                    else
                        WriteLog("Connected to panel");

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
                            WriteLog("Sending ENTER");

                            /**/ //TBD...
                            //LK, 29-nov-2013: Under construction...
                            //PostMessage(pNavigator.MainWindowHandle, (int)WindowManagerEvents.WM_KEYDOWN, (short)VK.VK_RETURN, 1); //LK,29-nov-2013: Send 1 Return (0x13)
                            //PostMessage(pNavigator.MainWindowHandle, (int)WindowManagerEvents.WM_KEYUP, (short)VK.VK_RETURN, 1); //LK,29-nov-2013: Send 1 Return (0x13)
                            ////PostMessage(pNavigator.MainWindowHandle, (int)WindowManagerEvents.WM_CHAR, 0x13, 1); //LK,29-nov-2013: Send 1 Return (0x13)
                            //LK, 29-nov-2013: This command will send the key to the top window, not always to PC_Navigator:
                            //---thepanel.Focus(); //Give it focus

                            //IntPtr toplevelWindow = FindWindow("MPFCWindow", "");
                            //JJ: Hm.. somehow i deleted FindWindow...
                            /*if (toplevelWindow != null)
                            {
                                WriteLog("Got a handle...");
                            }*/

                            //JJ: Just send 'Enter' until the message system works...
                            SendKeys.SendWait("{ENTER}");
                        }
                    }
                    catch { WriteLog("Failed to send OK to OSM usage"); }

                    //JJ: Re-run now, else panels not resized correctly
                    CF_localskinsetup();

                    WriteLog("Sending minimize command to window with handle 0x" + pNavigator.MainWindowHandle.ToString("X"));
                    //ShowWindowAsync(pNavigator.MainWindowHandle, (int)showWindowAttribute.SW_MINIMIZE);
                    //LK, 29-nov-2013: Last parameter of SendMessage and PostMessage is LWord (int), not string
                    //PostMessage(pNavigator.MainWindowHandle, (int)WindowManagerEvents.WM_COMMAND, unchecked((short)SC.SC_MINIMIZE), 0);
                    ShowWindow(pNavigator.MainWindowHandle, (int)showWindowAttribute.SW_MINIMIZE);

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

        // Handle Navigator Exited event
        private void pNavigator_Exited(object sender, System.EventArgs e)
        {
            //User really wants to exit Navigator...
            if (!boolExit)
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
                server.Disconnect(true);
                boolConnecting = false;

                //Modify Navigator's Settings XML file...
                ConfigureNavigatorXML();

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
                                    dialogParams.enablesubactions = false;
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

                //Get correct atlas setting:
                try
                {
                    //Read from registry                     
                    RegistryKey rk = Registry.LocalMachine;
                    RegistryKey sk1 = rk.OpenSubKey(REGNavigator);                  

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
                    //Default value if all goes wrong...
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
                    intDelay = 1800;    //LK, 30-nov-2013: Default value for sumulated UnMute (when no Unmute messages are received from the named pipe
                    this.pluginConfig.WriteField("/APPCONFIG/AUDIODELAYAFTERMUTE", intDelay.ToString(), true);
                }
                finally
                {
                    WriteLog("intDelay: " + intDelay.ToString());
                    muteCFTimerInterval = intDelay; //LK, 30-nov-2013: Cache this value to avoid many reads from the config file
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
                    WriteLog("CF_ConfigSettings.GPSVoicePrompts:  '" + CF_getConfigSetting(CF_ConfigSettings.GPSVoicePrompts).ToString() + "'");
                    WriteLog("CF_ConfigSettings.OSVersion:        '" + CF_getConfigSetting(CF_ConfigSettings.OSVersion).ToString() + "'");
                }
                catch (Exception errMsg) { WriteLog("Unable to get CF configuration flags or settings: " + errMsg.Message); }
            }
            catch (Exception errMsg) { WriteLog("Unable to get configuration settings: " + errMsg.Message); }
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
                
            //XML File exists
            if (fiXML.Exists)
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
        /**/ //This should be an event instead of polling a flag...
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
                NavStatustimer_Tick(null, null); //Make first update now
                NavStatustimer.Enabled = true;

                //Make button hidden
                this.CF_setButtonEnableFlag("MinMax", false);
            }
            else
            {
                WriteLog("Switch to Navigator");
                NavStatustimer.Enabled = false; //Stop the updates

                boolMainScreen = true;
                CF_localskinsetup();
                thepanel.Visible = true;
                
                SendCommand("$maximize\r\n", false, TCPCommand.Maximize);

                //Make it visible
                this.CF_setButtonEnableFlag("MinMax", true);
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

                //Repos buttons
                RePosbutton("GPSStatus", "fullbounds");
                RePosbutton("VolDown", "fullbounds");
                RePosbutton("VolUp", "fullbounds");
                RePosbutton("PlayPause", "fullbounds");
                RePosbutton("Rewind", "fullbounds");
                RePosbutton("FastForward", "fullbounds");
                RePosbutton("MinMax", "fullbounds");
                RePosbutton("NowPlaying", "fullbounds");
                RePosbutton("Exit", "fullbounds");

                //Repos label
                RePosLabel("DateTime", "fullbounds");	//LK,24-nov-2013: Simular to RePosbutton

                //Configure screen size. Use the panel size            
                SendCommand("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n", false, TCPCommand.Window);
                WriteLog("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n");
                //LK, 30-nov-2013: Window position is relative to the position of the panel:
                //JJ: Maybe, but nothing stops user from being creative with the startup values or has made modifications to skin. Left and top will normally resolve to 0, unless user tinkers... Better safe than sorry
                //SendCommand("$window=" + 0 + "," + 0 + "," + thepanel.Bounds.Width.ToString() + "," + thepanel.Bounds.Height.ToString() + ",noborder\r\n", false, TCPCommand.Window);
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

                //Repos buttons
                RePosbutton("GPSStatus", "bounds");
                RePosbutton("VolDown", "bounds");
                RePosbutton("VolUp", "bounds");
                RePosbutton("PlayPause", "bounds");
                RePosbutton("Rewind", "bounds");
                RePosbutton("FastForward", "bounds");
                RePosbutton("MinMax", "bounds");
                RePosbutton("NowPlaying", "bounds");
                RePosbutton("Exit", "bounds");

                //Reposition label
                RePosLabel("DateTime", "bounds");	//LK,24-nov-2013: Simular to RePosbutton

                //Configure screen size. Use the panel size
                SendCommand("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n", false, TCPCommand.Window);
                WriteLog("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n");
                //LK, 30-nov-2013: Window position is relative to the position of the panel:          
                //JJ: Maybe, but nothing stops user from being creative with the startup values or has made modifications to skin. Left and top will normally resolve to 0, unless user tinkers... Better safe than sorry
                //SendCommand("$window=" + 0 + "," + 0 + "," + thepanel.Bounds.Width.ToString() + "," + thepanel.Bounds.Height.ToString() + ",noborder\r\n", false, TCPCommand.Window);            
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
        private void RePosbutton(string strID, string strSize)
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
        private void WriteLog(string msg)
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

        public void CloseNavigator()
        {
            //User really wants to exit Navigator...
            boolExit = true;

            //LK, 25-nov-2013: Only close when started
            if (pNavigator != null)
            {
                IntPtr mainWindowHandle = pNavigator.MainWindowHandle;  //LK, 29-nov-2013: Cache before close

                //Disconnect the TCP connection so it can be re-established
                WriteLog("Disconnecting TCP connection for reuse");
                try
                {
                    server.Disconnect(true);
                    boolConnecting = false;
                }
                catch (Exception errMsg) { WriteLog("Failed to disconnect: " + errMsg.Message); }

                //Stop all timers first to avoid callbacks and additional TCP commands
                nightTimer.Enabled = false;
                muteCFTimer.Enabled = false;
                CallStatusTimer.Enabled = false;
                NavDestinationTimer.Enabled = false;

                //SendCommand("$exit\r\n", false, TCPCommand.Exit);
                pNavigator.CloseMainWindow(); //Ask nicely, just like ALT-F4
                
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
                for (int loop = 20; loop > 0; loop--)   //LK, 29-nov-2013: count down...    //was 100
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
                        if (boolFREE)
                            ClickOnPoint(mainWindowHandle, new Point(100, 200));
                        pNavigator.WaitForExit(500);    //LK, 29-nov-2014: Don;t wait longer then required//---   System.Threading.Thread.Sleep(200); //was 20
                        //JJ: Changed from 1000. Too long to wait... I'll meet you half way. Check every 500ms
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
                CloseNavigator();
            }

            //If resuming from sleep
            if (e.Mode == CFPowerModes.Resume)
            {
                StartNavigator();

                //Reset timers
                CallStatusTimer.Enabled = true;
                NavDestinationTimer.Enabled = true;

                //If exit, restart Navigator
                boolExit = false;
            }

            WriteLog("OnPowerModeChanged - end()");
            return;
        }

#endregion

	}
}
