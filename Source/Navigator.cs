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
 * Parse TCP responses from Navigator...
 * XP and embedding?
 * How long to wait for Navigator to start? (White screen of nothing)
 * 
 * Remove non-required functions
 * 
 * Not used in Setup:
 *      Port
 *      Baud Rate
 *      GPS Protocol
 *      Voice Prompts
 *      Route from other screen
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
                
        /**/ //This should be moved to a AppConfigure class?
        private string strEXEPath = "";                     // Folder and EXE name
        private string strEXEParameters = "";               // Paramters to use
        private bool boolFirstStart = true;                 // First time pluginshow is run?
        private bool boolFullScreen = false;                // Full screen?
        private bool boolFREE = true;                       // Free edition?
        private bool boolOSMOK = false;                     // If true, supresses OSM License prompt
        private bool boolAlerts = false;                    // Show alerts if NOT active plugin?
        private IntPtr mHandlePtr;                          // var for window handle number to catch
        private readonly CFControls.CFPanel _containerPanel;
        CFControls.CFPanel thepanel = null;                 // The panel to 'project' Navigator into        
        Process pNavigator = null;                          // Navigator's process
        private bool boolCurrentNightMode = false;          // Are we currently in night mode?
        private bool boolCurrentCallMode = false;           // Are we currently on the phone?
        private string strAppDataPath = "";                 // Path to Navigator's XML file
        private CFNavLocation navCurrentLocation = new CFNavLocation();       // Navigator's current location

        Timer nightTimer = new System.Windows.Forms.Timer(); // timer for switching day/night skin      
        Timer muteCFTimer = new System.Windows.Forms.Timer();    // timer for mute'ing CF
        Timer NavStatsTimer = new System.Windows.Forms.Timer();    // timer for retrieving Navigator's Navigation Stats
        Timer CallStatusTimer = new System.Windows.Forms.Timer();    // timer for checking if a call is in progress
        Timer EnableGPSTimer = new System.Windows.Forms.Timer();    // timer before enabling the GPS after hibernation
        Timer NavDestinationTimer = new System.Windows.Forms.Timer();    // timer for checking for destination proximity if not active plugin

        //From Mark
        private readonly CfNavData _currentPosition = new CfNavData();
        private readonly byte[] _mByBuff = new byte[0x100];
        private Rectangle _nMapSize;
        private Rectangle _nNavSize;
        public override event CFNavVoiceEventHandler CF_navVoiceEvent;
        private delegate void VoidDelegate();

        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        //Placeholders
        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, string lParam);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll")]
        static extern bool MoveWindow(IntPtr Handle, int x, int y, int w, int h, bool repaint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
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
                
                //Log current version of DLL for debug purposes
                WriteLog("Assembly Version: '" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + "'");

                // All controls should be created or Setup in CF_localskinsetup.
                // This method is also called when the resolution or skin has changed.
                CF_localskinsetup();

                //From http://wiki.centrafuse.com/wiki/Application-Description.ashx
                CF_params.settingsDisplayDesc = this.pluginLang.ReadField("/APPLANG/SETUP/DESCRIPTION");

                //Get configuration settings
                LoadSettings();

                //Setup the Panel used by PC_Navigator.exe
                WriteLog("Init the panel to use for MapFactor");
                thepanel = new CFControls.CFPanel();

                //Associate 'thepanel' with the panel defined in the skin.xml
                thepanel = panelArray[CF_getPanelID("PanelNavigator")];

                //Get the handle so we can associate it with the process later
                mHandlePtr = thepanel.Handle;

                //Timer for day/night skin swap                
                nightTimer.Interval = 2500; // Check every 2.5 seconds for a change
                nightTimer.Enabled = false;
                nightTimer.Tick += new EventHandler(nightTimer_Tick);

                //Timer for mute'ing CF while Navigator speaks
                muteCFTimer.Interval = 2500; // Unpause audio after this duration
                muteCFTimer.Enabled = false;
                muteCFTimer.Tick += new EventHandler(muteTimer_Tick);
                
                //Timer for getting Navigation Stats
                NavStatsTimer.Interval = 1500; // Check every 1500 ms
                NavStatsTimer.Enabled = false;
                NavStatsTimer.Tick += new EventHandler(NavStatsTimer_Tick);                            

                //Timer for getting Navigation Stats
                CallStatusTimer.Interval = 2000; // Check every
                CallStatusTimer.Enabled = true;
                CallStatusTimer.Tick += new EventHandler(CallStatusTimer_Tick);

                //Timer before enabling GPS in Navigator after hibernation
                EnableGPSTimer.Interval = 7500; // Wait this long...
                EnableGPSTimer.Enabled = false;
                EnableGPSTimer.Tick += new EventHandler(EnableGPSTimer_Tick);
                
                //Timer to use to check if arrived at destination
                NavDestinationTimer.Interval = 5000; // Wait this long...
                NavDestinationTimer.Enabled = false;
                NavDestinationTimer.Tick += new EventHandler(NavDestinationTimer_Tick);

                //Modify Navigator's Settings XML file...
                ConfigureNavigatorXML();

                // Creates new events to catch when CF is being closed, loaded or the power mode changed
				this.KeyDown += new KeyEventHandler(Navigator_KeyDown);
                this.CF_events.CFPowerModeChanged += new CFPowerModeChangedEventHandler(OnPowerModeChanged); //Hibernation support
                this.CF_events.applicationClosing += OnApplicationClosing;
                this.CF_events.applicationLoaded += OnApplicationLoaded;
                this.CF_events.CFPowerModeChanged += OnPowerModeChanged;

                //Set panel size to match size defined in the skin.xml and the fullscreen flag
                boolFullScreen = CF_getConfigFlag(CF_ConfigFlags.GPSFullscreen);
                if (boolFullScreen) SetFullScreen(); else SetNonFullScreen();
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

            if (boolFirstStart) this.CF3_initSection("Navigator");

            // Set up custom button handlers for buttons without a CML action in skin.xml
            this.CF_createButtonClick("MinMax", new MouseEventHandler(btnMinMax_Click));

            CFTools.writeLog(PluginName, "CF_localskinsetup", "Exiting");
		}


		/// <summary>
		/// This is called by the system when it exits or the plugin has been deleted.
		/// </summary>
		public override void CF_pluginClose()
		{
            //Send Exit to Mapfactor          
            SendCommand("$exit\r\n", false);
            
            //Close the connection.
            server.Close();            
            server = null;
            WriteLog("Closed");

            //Put the configuration files back again
            try { System.IO.File.Move(strAppDataPath + "\\settings.xml", strAppDataPath + "\\settings.xml.CF"); } catch { }
            try { System.IO.File.Move(strAppDataPath + "\\settings.xml.NAV", strAppDataPath + "\\settings.xml"); } catch { }

            base.CF_pluginClose(); // calls form Dispose() method
		}
		

		/// <summary>
		/// This is called by the system when a button with this plugin action has been clicked.
		/// </summary>
		public override void CF_pluginShow()
		{
            if (boolFirstStart)
            {
                //Do not re-init connection
                boolFirstStart = false;

                //Check if already running
                if (TerminateOrphanedProcess())
                {
                    if (TerminateOrphanedProcess()) this.CF_systemCommand(CF_Actions.SHOWINFO, "Unable to terminated existing PC_Navigator. Embedding will fail.", "AUTOHIDE");
                }

                //Launch Navigator
                try
                {
                    pNavigator = new Process();
                    pNavigator.StartInfo.FileName = strEXEPath + "\\PC_Navigator.exe";
                    pNavigator.StartInfo.Arguments = "--window_border=no " + strEXEParameters + " --window_position=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString();
                    //This does not work: "--tcpserver=127.0.0.1:" + intTCPPort.ToString(); Settings.XML modified
                    WriteLog("Launching Navigator using: '" + pNavigator.StartInfo.FileName + "'");
                    WriteLog("Parameters: '" + pNavigator.StartInfo.Arguments + "'");
                    pNavigator.Start();
                    System.Threading.Thread.Sleep(750); // Allow the process to open it's window
                    //pNavigator.WaitForInputIdle();    //Dont use this, the window location is messed up. Can't press OK
                    SetParent(pNavigator.MainWindowHandle, mHandlePtr);
                }
                catch { WriteLog("Failed to launch and connect to Navigators window"); }

                //Say YES to OSM data usage, if user changed to ON
                try
                {
                    if (boolOSMOK)
                    {
                        System.Threading.Thread.Sleep(500); // Allow the process to open it's window
                        WriteLog("Sending ENTER");
                        SendKeys.SendWait("{ENTER}");
                    }
                }
                catch { WriteLog("Failed to send OK to OSM usage"); }

                //Enable or disable Navigator voice prompts?
                if (CF_getConfigFlag(CF_ConfigFlags.GPSEnableVoice) == true)
                {
                    SendCommand("$sound_volume=on\r\n", false);

                    //Configure Navigator Audio volume
                    if (CF_getConfigFlag(CF_ConfigFlags.GPSSetNavSoundLevel) == true)
                    {
                        SendCommand("$sound_volume=" + CF_getConfigSetting(CF_ConfigSettings.GPSNavSoundLevel).ToString() + "\r\n", false);
                    }
                }
                else
                {
                    SendCommand("$sound_volume=off\r\n", false);
                }

                //Do we want to know?
                if (boolAlerts)
                {
                    SendCommand("$navigation_info=waypoint_info:on\r\n", false);
                    SendCommand("$navigation_info=recalculation_warning:on\r\n", false);
                }
                else
                {
                    SendCommand("$navigation_info=waypoint_info:off\r\n", false);
                    SendCommand("$navigation_info=recalculation_warning:off\r\n", false);
                }

                //Act as navigation plugin
                SendCommand("$gps_sending=start;nmea\r\n", false);
            }
            //End first startup

            //Configure night mode toggle option
            SetDayNightToggle();

            //Set panel size to match size defined in the skin.xml
            //if (boolFullScreen) SetFullScreen(); else SetNonFullScreen();            

            //Don't check for navigation updates if Navigator is active plugin
            NavDestinationTimer.Enabled = false;

            try
            {
                //Configure screen size. Use the panel size
                SendCommand("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n", false);                
                SendCommand("$maximize\r\n", false);              
            }
            catch { WriteLog("Failed to configure Navigators screensize"); }
                                               
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
                SendCommand("$minimize\r\n", false);

                //Don't check for skin change. Plugin not visible => no update required
                nightTimer.Enabled = false;

                //Check for navigation updates if user selected to enable this
                if (boolAlerts) NavDestinationTimer.Enabled = true;
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

        /// <summary>
        ///     This is a very important method for nav plugins! Centrafuse and other plugins will call
        ///     this to get information from your plugin about various bits of navigation data. Your plugin
        ///     should return a string with the appropriate value set. All this does is pass-through to the
        ///     overridden function CF_navGetInfo - that way, you only need to edit in one place! You probably
        ///     don't need to modify much, if anything, here - just edit CD_navGetInfo
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="param">The parameter.</param>
        /// <returns>Returns whatever is appropriate.</returns>
        public override string CF_pluginData(string command, string param)
        {
            WriteLog("CF_pluginData: " + command + " " + param);
            string retvalue = "";

            switch (command)
            {
                case "ALTITUDE":
                    retvalue = CF_navGetInfo(CFNavInfo.Altitude);
                    break;
                case "AZIMUTH":
                    retvalue = CF_navGetInfo(CFNavInfo.Azimuth);
                    break;
                case "DIRECTION":
                    retvalue = CF_navGetInfo(CFNavInfo.Direction);
                    break;
                case "ETA":
                    retvalue = CF_navGetInfo(CFNavInfo.ETA);
                    break;
                case "ETR":
                    retvalue = CF_navGetInfo(CFNavInfo.ETR);
                    break;
                case "HOUSENUMBER":
                    retvalue = CF_navGetInfo(CFNavInfo.HouseNumber);
                    break;
                case "LATITUDE":
                    retvalue = CF_navGetInfo(CFNavInfo.Latitude);
                    break;
                case "LOCKEDSATELLITES":
                    retvalue = CF_navGetInfo(CFNavInfo.LockedSatellites);
                    break;
                case "LONGITUDE":
                    retvalue = CF_navGetInfo(CFNavInfo.Longitude);
                    break;
                case "REMAININGDISTANCE":
                    retvalue = CF_navGetInfo(CFNavInfo.RemainingDistance);
                    break;
                case "SPEED":
                    retvalue = CF_navGetInfo(CFNavInfo.Speed);
                    break;
                case "STREET":
                    retvalue = CF_navGetInfo(CFNavInfo.Street);
                    break;
                case "CITY":
                    retvalue = CF_navGetInfo(CFNavInfo.City);
                    break;
                case "ZIP":
                    retvalue = CF_navGetInfo(CFNavInfo.Zip);
                    break;
                case "DESTCITY":
                    retvalue = CF_navGetInfo(CFNavInfo.DestCity);
                    break;
                case "DESTHOUSENUMBER":
                    retvalue = CF_navGetInfo(CFNavInfo.DestHouseNumber);
                    break;
                case "DESTLATITUDE":
                    retvalue = CF_navGetInfo(CFNavInfo.DestLatitude);
                    break;
                case "DESTLONGITUDE":
                    retvalue = CF_navGetInfo(CFNavInfo.DestLongitude);
                    break;
                case "DESTSTREET":
                    retvalue = CF_navGetInfo(CFNavInfo.DestStreet);
                    break;
                case "DESTZIP":
                    retvalue = CF_navGetInfo(CFNavInfo.DestZip);
                    break;
                case "NEXTTURN":
                    retvalue = CF_navGetInfo(CFNavInfo.NextTurn);
                    break;
                case "INROUTE":
                    retvalue = CF_navGetInfo(CFNavInfo.InRoute);
                    break;
            }

            return retvalue;
        }

        // This returns the underlying data your plugin has. It is called by CF_pluginData as well as Centrafuse
        public override string CF_navGetInfo(CFNavInfo infoType)
        {
            string retvalue = "";

            switch (infoType)
            {
                case CFNavInfo.Altitude:
                    retvalue = _currentPosition.Altitude.ToString(CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.Azimuth:
                    retvalue = _currentPosition.Heading.ToString(CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.Direction:
                    double temp;
                    retvalue = double.TryParse(_currentPosition.Heading.ToString(CultureInfo.InvariantCulture), out temp)
                                   ? _currentPosition.Heading.ToCardinalMark().ToString()
                                   : "";
                    break;
                case CFNavInfo.ETA:
                    retvalue = "";
                    break;
                case CFNavInfo.ETR:
                    retvalue = "";
                    break;
                case CFNavInfo.HouseNumber:
                    retvalue = "";
                    break;
                case CFNavInfo.Latitude:
                    retvalue = _currentPosition.Latitude.ToString(CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.LockedSatellites:
                    retvalue = _currentPosition.LockedSatellites.ToString(CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.Longitude:
                    retvalue = _currentPosition.Longitude.ToString(CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.RemainingDistance:
                    retvalue = "";
                    break;
                case CFNavInfo.Speed:
                    retvalue = _currentPosition.Speed.ToString(CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.Street:
                    retvalue = "";
                    break;
                case CFNavInfo.City:
                    retvalue = "";
                    break;
                case CFNavInfo.Zip:
                    retvalue = "";
                    break;
                case CFNavInfo.DestCity:
                    retvalue = "";
                    break;
                case CFNavInfo.DestHouseNumber:
                    retvalue = "";
                    break;
                case CFNavInfo.DestLatitude:
                    retvalue = "";
                    break;
                case CFNavInfo.DestLongitude:
                    retvalue = "";
                    break;
                case CFNavInfo.DestStreet:
                    retvalue = "";
                    break;
                case CFNavInfo.DestZip:
                    retvalue = "";
                    break;
                case CFNavInfo.NextTurn:
                    retvalue = "";
                    break;
                case CFNavInfo.InRoute:
                    retvalue = _currentPosition.InRoute.ToString();
                    break;
            }

            return retvalue;
        }

        // This returns the underlying data your plugin has. It is called by CF_pluginData as well as Centrafuse
        public override CFNavInfoBundle CF_navGetInfoBundle()
        {
            var retvalue = new CFNavInfoBundle();

            if (InvokeRequired)
            {
                CFTools.writeLog("NAV", "CF_navGetInfoBundle", "INVOKE REQUIRED!!");
            }
            CFTools.writeLog("NAV", "CF_navGetInfoBundle", "", CFTools.DebugLevel.FIVE);

            retvalue.altitude = "";
            retvalue.azimuth = "";
            retvalue.direction = "";
            retvalue.eta = "";
            retvalue.etr = "";
            retvalue.lockedsatellites = "";
            retvalue.remainingdistance = "";
            retvalue.speed = "";
            retvalue.nextturn = "";
            retvalue.inroute = false;

            retvalue.currentlocation.house = "";
            retvalue.currentlocation.latitude = -1;
            retvalue.currentlocation.longitude = -1;
            retvalue.currentlocation.street = "";
            retvalue.currentlocation.city = "";
            retvalue.currentlocation.zip = "";

            retvalue.destlocation.city = "";
            retvalue.destlocation.house = "";
            retvalue.destlocation.latitude = -1;
            retvalue.destlocation.longitude = -1;
            retvalue.destlocation.street = "";
            retvalue.destlocation.zip = "";
            retvalue.destlocation.description = "";
            retvalue.destlocation.telephone = "";

            return retvalue;
        }

        // This method is called to pass a destination to your navigation engine. Read the parameters you need from the navLocation variable and act accordingly
        public override void CF_navSetDestination(CFNavLocation navLocation)
        {
            WriteLog("CF_navSetDestination(1)");

            //Clear current destination
            SendCommand("$destination=clear\r\n", true);

            //Set new destination
            SendCommand("$destination=" + navLocation.latitude.ToString(CultureInfo.InvariantCulture) + "," + navLocation.longitude.ToString(CultureInfo.InvariantCulture) + ";navigate;instant\r\n", true);
        }

        // This method is called to pass a destination to your navigation engine. Read the parameters you need from the navLocation variable and act accordingly
        public override void CF_navSetDestination(CFNavLocation navLocation, bool openNav, bool openFullScreen)
        {
            WriteLog("CF_navSetDestination(3)");
            //Clear current destination
            SendCommand("$destination=clear\r\n", true);

            //Set new destination
            SendCommand("$destination=" + navLocation.latitude.ToString(CultureInfo.InvariantCulture) + "," + navLocation.longitude.ToString(CultureInfo.InvariantCulture) + ";navigate;instant\r\n", true);

            //Switch to Nav
            CF3_executeCMLAction("Centrafuse.CFActions.Nav");

            //Resize Nav screen
            if (openFullScreen) SetFullScreen(); else SetNonFullScreen();
        }

        // Called by Centrafuse to find out what the destination is. Set the navLocation variable with as much information as is relevant
        public override CFNavLocation CF_navGetDestination()
        {
            WriteLog("CF_navSetDestination()");           
            var navLocation = new CFNavLocation();
            return navLocation;
        }

        // Called by Centrafuse to find out what the location is. Set the navLocation variable with as much information as is relevant
        public override CFNavLocation CF_navGetLocation()
        {
            WriteLog("CF_navGetLocation()");
            var navLocation = new CFNavLocation();
            navLocation.latitude = _currentPosition.Latitude;
            navLocation.longitude = _currentPosition.Longitude;
            return navLocation;
        }

        // Tells Centrafuse whether or not navigation is visible - you probably don't need to edit this
        public override bool CF_navIsVisible()
        {
            WriteLog("CF_navIsvisible()");
            return Visible;
        }

        // Called when the user wishes to cancel the current route. Call your navigation engine's appropriate methods
        public override void CF_navCancelRoute()
        {
            WriteLog("CF_navCancelRoute()");

            //Clear current destination
            SendCommand("$destination=clear\r\n", true);
        }

        // Called when Centrafuse is requesting the main menu for your navigation plugin.
        public override void CF_navShowMenu()
        {
            WriteLog("CF_navShowMenu()");
        }

        // Called when Centrafuse is requesting the view menu for your navigation plugin.
        public override void CF_navShowViewMenu()
        {
            WriteLog("CF_navShowViewMenu()");
        }

        public override void CF_navZoomIn()
        {
            WriteLog("CF_navZoomIn()");
        }

        public override void CF_navZoomOut()
        {
            WriteLog("CF_navZoomOut()");
        }

        public override CFPOICategory[] CF_navGetPOICategories()
        {
            var retvalue = new CFPOICategory[0];
            return retvalue;
        }

        public override CFPOICategory[] CF_navGetPOISubCategories(int poinumber)
        {
            var retvalue = new CFPOICategory[0];
            return retvalue;
        }

        public override CFNavLocation[] CF_navGetPOILocations(int poinumber, int subpoinumber)
        {
            var retvalue = new CFNavLocation[0];
            return retvalue;
        }

        public override bool CF_navIsPOICategoryVisible(int poinumber)
        {
            return false;
        }

        public override void CF_navShowPOICategory(int poinumber, bool visible)
        {
        }

        public override CFNavLocation[] CF_navGetHistory(int maxlocations)
        {
            var locations = new CFNavLocation[maxlocations];
            return locations;
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

/*          switch (id.ToUpper())
            {
            }
*/
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

        // Called when Centrafuse is is going to switch between full screen mode
        public override void CF_sectionToggleFullScreen()
        {
            // Handle async invocation
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new VoidDelegate(CF_sectionToggleFullScreen), new object[] { });
                    return;
                }
            }
            catch { }

            CFTools.writeLog(PluginName, "CF_sectionToggleFullScreen", "");

            int nViewMode = 0; // View mode 0 = showing main controls, 1 = non-full-screen, 2 = full-screen

            try
            {
                nViewMode++;
                if (nViewMode > 2) nViewMode = 1;

                if (nViewMode == 1 &&  (!CFTools.IsSecondaryDisplay(this, CF_displayHooks.displayNumber) || CF_displayHooks.rearScreen))
                {
                    CF_displayHooks.boundsAttributeId = "bounds";
                    Rectangle rect = SkinReader.ParseBounds(SkinReader.GetSectionAttribute(CF_params.sectionID, CF_displayHooks.boundsAttributeId, pluginSkinReader));
                    Bounds = CF_createRect(rect, true);
                    rect = SkinReader.ParseBounds(SkinReader.GetControlAttribute(CF_params.sectionID, "NavContainer", CF_displayHooks.boundsAttributeId, pluginSkinReader));
                    _containerPanel.Bounds = CF_createRect(rect);
                }
                else if (nViewMode == 2 && (!CFTools.IsSecondaryDisplay(this, CF_displayHooks.displayNumber) || CF_displayHooks.rearScreen))
                {
                    CF_displayHooks.boundsAttributeId = "fullbounds";
                    Rectangle rect = SkinReader.ParseBounds(SkinReader.GetSectionAttribute(CF_params.sectionID, CF_displayHooks.boundsAttributeId, pluginSkinReader));
                    Bounds = CF_createRect(rect, true);
                    rect = SkinReader.ParseBounds(SkinReader.GetControlAttribute(CF_params.sectionID, "NavContainer", CF_displayHooks.boundsAttributeId, pluginSkinReader));
                    _containerPanel.Bounds = CF_createRect(rect);
                }
                else
                    return;

                if (Handle != IntPtr.Zero) Win32.SetWindowPos(Handle, IntPtr.Zero, 0, 0, _containerPanel.Bounds.Width, _containerPanel.Bounds.Height, 0);

                _nNavSize = Bounds;
                _nMapSize = _containerPanel.Bounds;
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(errmsg.Message, errmsg.StackTrace);
            }
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

                // PlayPause prompt?
                try
                {
                    bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/PAUSEPLAYSTATUS"));
                }
                catch
                {
                    this.pluginConfig.WriteField("/APPCONFIG/PAUSEPLAYSTATUS", "false", true);
                }

                // Send notification
                try
                {
                    bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/NOTIFICATIONSTATUS"));
                }
                catch
                {
                    this.pluginConfig.WriteField("/APPCONFIG/NOTIFICATIONSTATUS", "false", true);
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
                    RegistryKey rk = Registry.LocalMachine;
                    RegistryKey sk1 = rk.OpenSubKey("SOFTWARE\\MapFactor\\set\\pcnavigator_12");

                    if (boolFREE)
                    {
                        //Add the '_free' text to the filename        
                        string strTmp = sk1.GetValue("Atlas").ToString();
                        strEXEParameters = strEXEParameters + " --atlas=" + strTmp.Substring(0, strTmp.Length - 4) + "_free.idc";
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
                    boolAlerts = false;
                    this.pluginConfig.WriteField("/APPCONFIG/ALERTSENABLED", boolAlerts.ToString(), true);
                }
                finally
                {
                    WriteLog("boolAlerts: " + boolAlerts.ToString());
                }

                // Navigator Volume
                try
                {
                    
                    WriteLog("CF_ConfigFlags.GPSSetNavSoundLevel: '" + CF_getConfigFlag(CF_ConfigFlags.GPSSetNavSoundLevel).ToString() + "'");
                    WriteLog("CF_ConfigSettings.GPSNavSoundLevel: '" + CF_getConfigSetting(CF_ConfigSettings.GPSNavSoundLevel).ToString() + "'");                    
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

            FileInfo fi = new FileInfo(strAppDataPath + "\\settings.xml");
            //Make copy of original settings file
            if (fi.Exists) 
            {   
                //Get backup status
                bool boolBackupDone = false;
                try
                {
                    boolBackupDone = bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/BACKUPDONE"));
                }
                catch { boolBackupDone = false; }

                //Check how to handle XMLfiles
                if ( boolBackupDone == true)
                {
                    WriteLog("Backup already done. Flipping XML files around");

                    //If .NAV already exists, there a problem... Remove problem...
                    FileInfo fi1 = new FileInfo(strAppDataPath + "\\settings.xml.NAV");
                    if (fi1.Exists == true) fi1.Delete();

                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml", strAppDataPath + "\\settings.xml.NAV");} catch { }
                    try { System.IO.File.Move(strAppDataPath + "\\settings.xml.CF", strAppDataPath + "\\settings.xml"); } catch { }
                }
                else
                {
                    WriteLog("Backup not done. Creating backup copies of XML file");
                    //Not the most optimized, but safest to rename original, and then make a new copy...
                    string newFileName = strAppDataPath + "\\settings.xml." + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".orig";
                    try
                    {
                        System.IO.File.Move(strAppDataPath + "\\settings.xml", newFileName);
                        System.IO.File.Copy(newFileName, strAppDataPath + "\\settings.xml", true);
                        System.IO.File.Copy(newFileName, strAppDataPath + "\\settings.xml.NAV", true);
                        this.pluginConfig.WriteField("/APPCONFIG/BACKUPDONE", "True", true);
                    }
                    catch { WriteLog("Failed to create new settings.xml file"); }
                }

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

        // Event to get CF to play audio again
        private void NavStatsTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("NavStats...");
            SendCommand("$navigation_statistics\r\n", false);
        }
        
        // Event to get CF to play audio again
        private void muteTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("Play Audio");
            muteCFTimer.Enabled = false; //Turn off timer until next time

            //Mute/unmute
            if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute)) CF_systemCommand(CF_Actions.UNMUTE);
            
            //Play/Pause
            if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/PAUSEPLAYSTATUS")) == true) CF_systemCommand(CF_Actions.PLAY);

            /**/ //Not implemented
            //Send notification;            
            if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/NOTIFICATIONSTATUS")) == true)
            {
                //CF3_raisePluginEvent(Unmute)
            }
        }

        // Event to enable GPS in Navigator
        private void EnableGPSTimer_Tick(object sender, EventArgs e)
        {
            SendCommand("$gps_receiving=start\r\n", false);
            EnableGPSTimer.Enabled = false;                     //Disable the timer
        }

        // Event to ask Navigator for navigation statistics
        private void NavDestinationTimer_Tick(object sender, EventArgs e)
        {
            //Ask CF for navigation statistics
            SendCommand("$navigation_statistics\r\n", false);
        }

        private void SetDayNightToggle()
        {
            //Configure night mode toggle option
            try
            {
                //Get CF setting
                XmlDocument configxml = new XmlDocument();
                //configxml.Load("C:\\ProgramData\\Centrafuse\\Centrafuse Auto\\administrator\\System\\config.xml");
                configxml.Load(CFTools.AppDataPath + "\\System\\config.xml");
                XmlNodeList xnList = configxml.SelectNodes("/APPCONFIG");

                foreach (XmlNode xn in xnList)
                {
                    if (bool.Parse(xn["AUTOSWITCHSKIN"].InnerText) == true)
                    {
                        WriteLog("AUTOSWITCHSKIN is enabled");
                        nightTimer.Enabled = true;
                    }
                    else nightTimer.Enabled = false;
                }
            }
            catch { WriteLog("Failed to configure auto day/night mode"); }
        }

        // Event to keep checking if CF is in night mode
        private void nightTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                bool nightMode = CF_getConfigFlag(CF_ConfigFlags.NightSkinFlag);
                //WriteLog("Is Night Mode Active: " + nightMode.ToString());
                if (boolCurrentNightMode != nightMode)
                {
                    WriteLog("Switching mode");
                    string t1 = "";
                    if (nightMode)
                    {
                        t1 = "$set_mode=night\r\n";
                    }
                    else
                    {
                        t1 = "$set_mode=day\r\n";
                    }
                    SendCommand(t1, false);

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
                            SendCommand("$sound_volume=" + CF_getConfigSetting(CF_ConfigSettings.AttMuteLevel).ToString() + "\r\n", false);
                        }
                        else
                        {
                            //Configure Navigator Audio back to what it was
                            WriteLog("Call is not active");
                            
                            //Enable or disable Navigator voice prompts?
                            if (CF_getConfigFlag(CF_ConfigFlags.GPSEnableVoice) == true)
                            {
                                SendCommand("$sound_volume=on\r\n", false);

                                //Configure Navigator Audio volume
                                if (CF_getConfigFlag(CF_ConfigFlags.GPSSetNavSoundLevel) == true)
                                {
                                    SendCommand("$sound_volume=" + CF_getConfigSetting(CF_ConfigSettings.GPSNavSoundLevel).ToString() + "\r\n", false);
                                }
                            }
                            else
                            {
                                SendCommand("$sound_volume=off\r\n", false);
                            }
                        }

                        //Update current mode
                        boolCurrentCallMode = callMode;
                    }
                }
                catch { WriteLog("Failed to change sound warning mode"); }
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
                    boolFullScreen = false;                    
                }
                else
                {
                    SetFullScreen();
                    boolFullScreen = true;
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
            //RePosbutton("MinMax", "fullbounds");
            RePosbutton("VolDown", "fullbounds");
            RePosbutton("VolUp", "fullbounds");
            RePosbutton("Exit", "fullbounds");

            //Repos label
            CFControls.CFLabel a = new CFControls.CFLabel();
            a = labelArray[CF_getLabelID("DateTime")];
            a.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "DateTime", ("fullbounds").ToLower(), base.pluginSkinReader)));

            //Configure screen size. Use the panel size
            SendCommand("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n", false);

            //Resize section
            this.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetSectionAttribute("Navigator", ("fullbounds").ToLower(), base.pluginSkinReader)));

            //Refresh screen
            this.Invalidate();
        }


        private void SetNonFullScreen()
        {
            WriteLog("Configure for non-fullscreen");

            //Resize panel
            thepanel.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "PanelNavigator", ("bounds").ToLower(), base.pluginSkinReader)));

            //Repos buttons
            //RePosbutton("MinMax", "bounds");
            RePosbutton("VolDown", "bounds");
            RePosbutton("VolUp", "bounds");
            RePosbutton("Exit", "bounds");

            //Repos label
            CFControls.CFLabel a = new CFControls.CFLabel();
            a = labelArray[CF_getLabelID("DateTime")];
            a.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetControlAttribute("Navigator", "DateTime", ("bounds").ToLower(), base.pluginSkinReader)));

            //Configure screen size. Use the panel size
            SendCommand("$window=" + thepanel.Bounds.Left.ToString() + "," + thepanel.Bounds.Top.ToString() + "," + thepanel.Bounds.Right.ToString() + "," + thepanel.Bounds.Bottom.ToString() + ",noborder\r\n", false);

            //Resize section
            this.Bounds = base.CF_createRect(SkinReader.ParseBounds(SkinReader.GetSectionAttribute("Navigator", ("bounds").ToLower(), base.pluginSkinReader)));

            //Refresh screen
            this.Invalidate();
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

        #region Helper methods

        public LatLonDms ConvertDecimalToDms(double latitude, double longitude)
        {
            var retvalue = new LatLonDms();

            try
            {
                //Math.round is used to eliminate the small error caused by rounding in the computer:
                //e.g. 0.2 is not the same as 0.20000000000284

                double signlat = 1;
                double signlon = 1;

                if (latitude < 0)
                    signlat = -1;

                double latAbs = Math.Abs(Math.Round(latitude * (1000000)));

                if (latAbs > (90 * 1000000))
                    latAbs = 0;

                if (longitude < 0)
                    signlon = -1;

                double lonAbs = Math.Abs(Math.Round(longitude * (1000000)));

                if (lonAbs > (180 * 1000000))
                    lonAbs = 0;

                double latdegrees = Math.Floor(latAbs / (1000000)) * signlat;
                double latminutes = Math.Floor(((latAbs / (1000000)) - Math.Floor(latAbs / (1000000))) * (60));
                double latseconds =
                    Math.Floor(((((latAbs / (1000000)) - Math.Floor(latAbs / (1000000))) * (60)) -
                                Math.Floor(((latAbs / (1000000)) - Math.Floor(latAbs / (1000000))) * (60))) * (100000)) * (60) /
                    (100000);

                double londegrees = Math.Floor(lonAbs / (1000000)) * signlon;
                double lonminutes = Math.Floor(((lonAbs / (1000000)) - Math.Floor(lonAbs / (1000000))) * (60));
                double lonseconds =
                    Math.Floor(((((lonAbs / (1000000)) - Math.Floor(lonAbs / (1000000))) * (60)) -
                                Math.Floor(((lonAbs / (1000000)) - Math.Floor(lonAbs / (1000000))) * (60))) * (100000)) * (60) /
                    (100000);

                retvalue.LatitudeDegrees = latdegrees;
                retvalue.LatitudeMinutes = latminutes;
                retvalue.LatitudeSeconds = latseconds;

                retvalue.LongitudeDegrees = londegrees;
                retvalue.LongitudeMinutes = lonminutes;
                retvalue.LongitudeSeconds = lonseconds;
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(errmsg.Message, errmsg.StackTrace);
            }

            return retvalue;
        }

        public LatLonDecimal ConvertDmsToDecimal(double latdegrees, double latminutes, double latseconds,
                                                 double londegrees, double lonminutes, double lonseconds)
        {
            var retvalue = new LatLonDecimal();

            try
            {
                //Math.round is used to eliminate the small error caused by rounding in the computer:
                //e.g. 0.2 is not the same as 0.20000000000284

                double latsign = 1;
                double lonsign = 1;

                if (latdegrees < 0)
                    latsign = -1;

                double absdlat = Math.Abs(Math.Round(latdegrees * (1000000)));
                //if(absdlat > (90 * 1000000))

                double absmlat = Math.Abs(Math.Round(latminutes * (1000000)));
                //if(absmlat >= (60 * 1000000))

                double absslat = Math.Abs(Math.Round(latseconds * (1000000)));
                //if(absslat > (59.99999999 * 1000000))

                if (londegrees < 0)
                    lonsign = -1;

                double absdlon = Math.Abs(Math.Round(londegrees * (1000000)));
                //if(absdlon > (180 * 1000000))

                double absmlon = Math.Abs(Math.Round(lonminutes * (1000000)));
                //if(absmlon >= (60 * 1000000))

                double absslon = Math.Abs(Math.Round(lonseconds * (1000000)));
                //if(absslon > (59.99999999 * 1000000))

                double latitude = Math.Round(absdlat + (absmlat / (60)) + (absslat / (3600))) * latsign / (1000000);
                double longitude = Math.Round(absdlon + (absmlon / (60)) + (absslon / (3600))) * lonsign / (1000000);

                retvalue.Latitude = latitude;
                retvalue.Longitude = longitude;
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(errmsg.Message, errmsg.StackTrace);
            }

            return retvalue;
        }

        public int GetGmtOffset(double latitude, double longitude)
        {
            int retvalue = 0;

            try
            {
                bool addneg = false;
                LatLonDms dms = ConvertDecimalToDms(latitude, longitude);

                if (dms.LongitudeDegrees < 0)
                    addneg = true;

                double dvalue = Math.Abs(dms.LongitudeDegrees) / (15);
                string svalue = dvalue.ToString("#");
                int gmtoffset = Int32.Parse(svalue);

                if (addneg)
                    gmtoffset = gmtoffset * -1;

                retvalue = gmtoffset;
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(errmsg.Message, errmsg.StackTrace);
            }

            return retvalue;
        }

        #endregion

	
#region Click Events


#endregion
			
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
                SendCommand("$gps_receiving=stop\r\n", false);                
            }

            //If resuming from sleep
            if (e.Mode == CFPowerModes.Resume)
            {
                //Timer before enabling GPS in Navigator after hibernation
                //Use a timer to not pause execution
                EnableGPSTimer.Enabled = true;
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
