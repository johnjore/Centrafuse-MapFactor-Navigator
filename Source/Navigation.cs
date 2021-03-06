﻿/*
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
 * All functions related to Navigation and navigation data
*/

using System;
using centrafuse.Plugins;
using System.Globalization;
using System.Net;
using System.IO;
using Newtonsoft.Json;  // Used to parse OSRM responses

namespace Navigator
{
    public partial class Navigator
    {
        private readonly CfNavData _currentPosition = new CfNavData();
        private readonly double meter_To_ft = 3.2808399;
        private readonly double knot_To_kmh = 1.852;
        private readonly double knot_To_mph = 1.1507794480136;

        // Event to get CF to ask for stats
        private void NavStatustimer_Tick(object sender, EventArgs e)
        {
            //Get current decimal separator
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            //WriteLog("Localize: " + boolLocalize.ToString() + " " + decimalSeparator);            

            try
            {
                CF_updateText("DataRoute", _currentPosition.Route);
            }
            catch
            {
                CF_updateText("DataRoute", "");
            }


            try
            {
                if (boolLocalize)
                {
                    CF_updateText("DestLatitude", CF_navGetInfo(CFNavInfo.DestLatitude).Replace(".", decimalSeparator));
                }
                else
                    CF_updateText("DestLatitude", CF_navGetInfo(CFNavInfo.DestLatitude));
            }
            catch
            {
                CF_updateText("DestLatitude", "");
            }

            try
            {
                if (boolLocalize)
                {
                    CF_updateText("DestLongitude", CF_navGetInfo(CFNavInfo.DestLongitude).Replace(".", decimalSeparator));
                }
                else
                    CF_updateText("DestLongitude", CF_navGetInfo(CFNavInfo.DestLongitude));
            }
            catch
            {
                CF_updateText("DestLongitude", "");
            }

            try 
            { 
                if (boolLocalize)
                {
                    CF_updateText("DataLongitude", CF_navGetInfo(CFNavInfo.Longitude).Replace(".", decimalSeparator));
                }
                else
                    CF_updateText("DataLongitude", CF_navGetInfo(CFNavInfo.Longitude));
            }
            catch 
            {
                CF_updateText("DataLongitude", ""); 
            }

            try 
            {
                if (boolLocalize)
                    CF_updateText("DataLatitude", CF_navGetInfo(CFNavInfo.Latitude).Replace(".", decimalSeparator));
                else
                    CF_updateText("DataLatitude", CF_navGetInfo(CFNavInfo.Latitude));
            }
            catch 
            {
                CF_updateText("DataLatitude", "");
            }

            try 
            {
                CF_updateText("DataLockedSatellites", CF_navGetInfo(CFNavInfo.LockedSatellites));
            }
            catch 
            {
                CF_updateText("DataLockedSatellites", "");
            }            

            //Don't read from disk on each update or each attribute
            string tmpSpeed;
            if (boolLocalize)
                tmpSpeed = CF_navGetInfo(CFNavInfo.Speed).Replace(".", decimalSeparator);
            else
                tmpSpeed = CF_navGetInfo(CFNavInfo.Speed);

            switch (SpeedUnit)
            {
                case Unit.METRIC:
                    try { CF_updateText("DataSpeed", tmpSpeed + " km/h");}
                    catch { CF_updateText("DataSpeed", "0 km/h"); }

                    break;
                case Unit.IMPERIAL:
                    try { CF_updateText("DataSpeed", tmpSpeed + " mph");}
                    catch { CF_updateText("DataSpeed", "0 mph"); }

                    break;
                default:
                    try { CF_updateText("DataSpeed", tmpSpeed); }
                    catch { CF_updateText("DataSpeed", "0"); }
                    break;
            }

            //Don't read from disk on each update or each attribute
            string tmpRD = CF_navGetInfo(CFNavInfo.RemainingDistance);
            string tmpNT = CF_navGetInfo(CFNavInfo.NextTurn);

            switch (DistUnit)
            {
                case Unit.METRIC:
                    try { CF_updateText("DataAltitude", CF_navGetInfo(CFNavInfo.Altitude) + " m"); }
                    catch { CF_updateText("DataAltitude", ""); }

                    try {if (tmpRD != "") CF_updateText("DataRemainingDistance", tmpRD + " m"); else CF_updateText("DataRemainingDistance", ""); }
                    catch { CF_updateText("DataRemainingDistance", ""); }

                    try { if (tmpNT != "") CF_updateText("DataNextTurn", tmpNT + " m"); else CF_updateText("DataNextTurn", ""); }
                    catch { CF_updateText("DataNextTurn", ""); }

                    break;
                case Unit.IMPERIAL:
                    try { CF_updateText("DataAltitude", CF_navGetInfo(CFNavInfo.Altitude) + " ft"); }
                    catch { CF_updateText("DataAltitude", ""); }

                    try { if (tmpRD != "") CF_updateText("DataRemainingDistance", tmpRD + " ft"); else CF_updateText("DataRemainingDistance", ""); }
                    catch { CF_updateText("DataRemainingDistance", ""); }

                    try { if (tmpNT != "") CF_updateText("DataNextTurn", tmpNT + " ft"); else CF_updateText("DataNextTurn", ""); }
                    catch { CF_updateText("DataNextTurn", ""); }

                    break;
                default:
                    try { CF_updateText("DataAltitude", CF_navGetInfo(CFNavInfo.Altitude)); }
                    catch { CF_updateText("DataAltitude", ""); }

                    try { CF_updateText("DataRemainingDistance", CF_navGetInfo(CFNavInfo.RemainingDistance)); }
                    catch { CF_updateText("DataRemainingDistance", ""); }

                    try { CF_updateText("DataNextTurn", CF_navGetInfo(CFNavInfo.NextTurn)); }
                    catch { CF_updateText("DataNextTurn", ""); }

                    break;
            }

            
            try
            {
                string tmpETR = CF_navGetInfo(CFNavInfo.ETR);

                if (tmpETR != "") CF_updateText("DataETR", tmpETR + " seconds"); else CF_updateText("DataETR", "");

                
                /*double tmpETR = System.Math.Floor(double.Parse(CF_navGetInfo(CFNavInfo.ETR)) / 60);

                //Less than 1 minute?
                switch (tmpETR.ToString())
                {
                    case "0":
                        CF_updateText("DataETR", CF_navGetInfo(CFNavInfo.ETR) + " " + pluginLang.ReadField("/APPLANG/NAVIGATOR/SECONDS"));
                        break;
                    case "1":
                        CF_updateText("DataETR", tmpETR.ToString() + " " + pluginLang.ReadField("/APPLANG/NAVIGATOR/MINUTE") + " ("+ CF_navGetInfo(CFNavInfo.ETR) + " " + pluginLang.ReadField("/APPLANG/NAVIGATOR/SECONDS") + ")");
                        break;
                    default:
                        CF_updateText("DataETR", tmpETR.ToString() + " " + pluginLang.ReadField("/APPLANG/NAVIGATOR/MINUTES") + " (" + CF_navGetInfo(CFNavInfo.ETR) + " " + pluginLang.ReadField("/APPLANG/NAVIGATOR/SECONDS") + ")");
                        break;
                }
                */
            }
            catch 
            {
                CF_updateText("DataETR", "");
            }

            try
            {
                CF_updateText("DataETA", CF_navGetInfo(CFNavInfo.ETA));
            }
            catch
            {
                CF_updateText("DataETA", "");
            }

            try 
            {
                CF_updateText("DataDirection", CF_navGetInfo(CFNavInfo.Direction));
            }
            catch 
            {
                CF_updateText("DataDirection", "");
            }

            try
            {
                CF_updateText("DataAzimuth", CF_navGetInfo(CFNavInfo.Azimuth) + "°");
            }
            catch 
            {
                CF_updateText("DataAzimuth", "");
            }


            //Ask Navigator for nearest street, but only if Navigator version is 12.4 or higher as previous versions crash
            if (decimal.Compare(decNavigatorVersion, new decimal(12.4)) >= 0)
            {
                try
                {
                    CF_updateText("DataStreet", CF_navGetInfo(CFNavInfo.Street));
                }
                catch
                {
                    CF_updateText("Street", "");
                }
            }
            else
            {
                CF_updateText("Street", pluginLang.ReadField("/APPLANG/NAVIGATOR/NEARESTSTREET"));
            }

            try
            {
                CF_updateText("DataInRoute", CF_navGetInfo(CFNavInfo.InRoute));
            }
            catch 
            {
                CF_updateText("DataInRoute", "");
            }

            try 
            {
                //Updated to incorporate DST
                CF_updateText("DataGPSTime", parsTimeOfFix(_navStats.GPSDate.ToString(), _navStats.GPSTime.ToString()).ToString());
            }
            catch 
            {
                CF_updateText("DataGPSTime", ""); 
            }          

        }


        /// <summary>
        ///     Centrafuse and other plugins will call this to get information from plugin about various bits of navigation data. 
        ///     Plugin should return a string with the appropriate value set. For Nav data, all this does is pass-through to the
        ///     overridden function CF_navGetInfo(...)
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="param">The parameter.</param>
        /// <returns>Returns whatever is appropriate.</returns>
        public override string CF_pluginData(string command, string param)
        {
            //WriteLog("CF_pluginData: " + command + " " + param);
            string retvalue = "";

            switch (command.ToUpper())
            {
                case "ALTITUDE":
                    retvalue = CF_navGetInfo(CFNavInfo.Altitude);
                    break;
                case "AZIMUTH":
                    retvalue = CF_navGetInfo(CFNavInfo.Azimuth);
                    break;
                case "DESTLATITUDE":
                    retvalue = CF_navGetInfo(CFNavInfo.DestLatitude);
                    break;
                case "DESTLONGITUDE":
                    retvalue = CF_navGetInfo(CFNavInfo.DestLongitude);
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
                case "SCREENSIZE":
                    if (boolFullScreen) retvalue = "FULL"; else retvalue = "NORMAL";
                    break;
                case "RESTARTNAV":
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

                        //If we got this far, success
                        retvalue = bool.TrueString;
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Failed to run 'RESTARTNAV', " + ex.ToString());
                        retvalue = bool.FalseString;
                    }
                    break;
                case "GETMCAFOLDER":
                    retvalue = strMCAFolder.ToString();
                    break;
                case "TCPCOMMAND":
                    //Send TCP Command to MapFactor Navigator
                    try
                    {
                        string[] strCommand = param.Split('|');                    
                        //WriteLog("TCP Command : '" + strCommand[0] + "', '" + strCommand[1] + "'");
                        SendCommand(strCommand[0], false, (TCPCommand)Enum.Parse(typeof(TCPCommand), strCommand[1], true));
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Failed to parse and run TCP Command: '" +  param.ToString() + "', " + ex.ToString());
                    }
                    break;
                case "ROUTE":
                    retvalue = _currentPosition.Route;
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
                    if (String.Compare(CF_navGetInfo(CFNavInfo.InRoute), strTRUE, true) == 0) retvalue = (DateTime.Now.AddSeconds(_navStats.TimeSecondsDestination)).ToString(); else retvalue = "";
                    break;
                case CFNavInfo.ETR:
                    if (String.Compare(CF_navGetInfo(CFNavInfo.InRoute), strTRUE, true) == 0) retvalue = _navStats.TimeSecondsDestination.ToString(); else retvalue = "";
                    break;
                case CFNavInfo.HouseNumber:
                    retvalue = "";
                    break;
                case CFNavInfo.Latitude:
                    retvalue = _currentPosition.Latitude.ToString("F5", CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.LockedSatellites:
                    retvalue = _currentPosition.LockedSatellites.ToString(CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.Longitude:
                    retvalue = _currentPosition.Longitude.ToString("F5", CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.RemainingDistance:
                    if (String.Compare(CF_navGetInfo(CFNavInfo.InRoute), strTRUE, true) == 0) retvalue = _navStats.DistanceDestination.ToString(); else retvalue = "";
                    break;
                case CFNavInfo.Speed:
                    retvalue = _currentPosition.Speed.ToString(CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.Street:
                    retvalue = _navStats.Street;
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
                    retvalue = _currentPosition.DestLatitude.ToString("F5", CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.DestLongitude:
                    retvalue = _currentPosition.DestLongitude.ToString("F5", CultureInfo.InvariantCulture);
                    break;
                case CFNavInfo.DestStreet:
                    retvalue = "";
                    break;
                case CFNavInfo.DestZip:
                    retvalue = "";
                    break;
                case CFNavInfo.NextTurn:
                    if (String.Compare(CF_navGetInfo(CFNavInfo.InRoute), strTRUE, true) == 0) retvalue = _navStats.DistanceNextWaypoint.ToString(); else retvalue = "";
                    break;
                case CFNavInfo.InRoute:
                    if (_navStats.DistanceDestination != 0) retvalue = strTRUE; else retvalue = strFALSE;
                    break;
            }

            return retvalue;
        }

        // This returns the underlying data your plugin has. It is called by Centrafuse
        public override CFNavInfoBundle CF_navGetInfoBundle()
        {
            var retvalue = new CFNavInfoBundle();

            if (InvokeRequired)
            {
                WriteLog("INVOKE REQUIRED!!");
            }

            try
            {
                retvalue.altitude = CF_navGetInfo(CFNavInfo.Altitude);
                retvalue.azimuth = CF_navGetInfo(CFNavInfo.Azimuth);
                retvalue.direction = CF_navGetInfo(CFNavInfo.Direction);
                retvalue.eta = CF_navGetInfo(CFNavInfo.ETA);
                retvalue.etr = CF_navGetInfo(CFNavInfo.ETR);
                retvalue.lockedsatellites = CF_navGetInfo(CFNavInfo.LockedSatellites);
                retvalue.remainingdistance = CF_navGetInfo(CFNavInfo.RemainingDistance);
                retvalue.speed = CF_navGetInfo(CFNavInfo.Speed);
                retvalue.nextturn = CF_navGetInfo(CFNavInfo.NextTurn);

                if (CF_navGetInfo(CFNavInfo.RemainingDistance) != "0") retvalue.inroute = true; else retvalue.inroute = false;

                retvalue.currentlocation.house = CF_navGetInfo(CFNavInfo.HouseNumber);
                retvalue.currentlocation.latitude = double.Parse(CF_navGetInfo(CFNavInfo.Latitude), CultureInfo.InvariantCulture);
                retvalue.currentlocation.longitude = double.Parse(CF_navGetInfo(CFNavInfo.Longitude), CultureInfo.InvariantCulture);

                retvalue.currentlocation.street = CF_navGetInfo(CFNavInfo.Street);
                retvalue.currentlocation.city = CF_navGetInfo(CFNavInfo.City);
                retvalue.currentlocation.zip = CF_navGetInfo(CFNavInfo.Zip);

                retvalue.destlocation.city = CF_navGetInfo(CFNavInfo.DestCity);
                retvalue.destlocation.house = CF_navGetInfo(CFNavInfo.DestHouseNumber);
                retvalue.destlocation.latitude = double.Parse(CF_navGetInfo(CFNavInfo.DestLatitude), CultureInfo.InvariantCulture);
                retvalue.destlocation.longitude = double.Parse(CF_navGetInfo(CFNavInfo.DestLongitude), CultureInfo.InvariantCulture);
                retvalue.destlocation.street = CF_navGetInfo(CFNavInfo.DestStreet);
                retvalue.destlocation.zip = CF_navGetInfo(CFNavInfo.DestZip);
                retvalue.destlocation.description = "";
                retvalue.destlocation.telephone = "";
            }
            catch (Exception errMsg) 
            {
                WriteLog("Unable to parse retvalue :" + errMsg.Message);

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
            }

            return retvalue;
        }

        //Convert NMEA string to Decimal value
        private double NMEAtoDecimal(String Pos)
        {
            //WriteLog("Raw NMEA value:" + Pos);
            double PosDb = double.Parse(Pos, CultureInfo.InvariantCulture);
            double Deg = Math.Floor(PosDb / 100);
            double DecPos = Math.Round(Deg + ((PosDb - (Deg * 100)) / 60), 5);
            return DecPos;
        }

        // This method is called to pass a destination to your navigation engine. Read the parameters you need from the navLocation variable and act accordingly
        public override void CF_navSetDestination(CFNavLocation navLocation)
        {
            WriteLog("CF_navSetDestination(1)");

            //Set the navigation location
            navSetDestination(navLocation);
        }

        // This method is called to pass a destination to your navigation engine. Read the parameters you need from the navLocation variable and act accordingly
        public override void CF_navSetDestination(CFNavLocation navLocation, bool openNav, bool openFullScreen)
        {
            WriteLog("CF_navSetDestination(3)");

            //Set the navigation location
            navSetDestination(navLocation);

            //Switch to Nav
            CF3_executeCMLAction("Centrafuse.CFActions.Nav");

            //Resize Nav screen
            if (openFullScreen) SetFullScreen(); else SetNonFullScreen();
        }

        //Called by CF_navSetDestination methods
        private void navSetDestination(CFNavLocation navLocation)
        {
            //Clear current destination
            SendCommand("$destination=clear\r\n", true, TCPCommand.Destination);

            //Did OSRM Encounter an error?
            bool boolOSRMError = false;
            
            //If OSRM is enabled, get route from it
            if (boolOSRMEnabled)
            {
                //Send command to OSRM
                try
                {
                    WriteLog("Request route from OSRM");
                    WebClient client = new WebClient();
                    Stream data = client.OpenRead("http://localhost:" + intOSRMPort.ToString() + "/viaroute?loc=" + CF_navGetInfo(CFNavInfo.Latitude).ToString() + "," + CF_navGetInfo(CFNavInfo.Longitude).ToString() + "&loc=" + navLocation.latitude.ToString() + "," + navLocation.longitude.ToString() + "&alt=true&instructions=true&compression=false");

                    //Get the response
                    WriteLog("Get route from OSRM");
                    StreamReader reader = new StreamReader(data);
                    string strResponse = reader.ReadToEnd();
                    data.Close();
                    reader.Close();
                    client.Dispose();

                    //Parse the response
                    OSRMResponse OSRMData = JsonConvert.DeserializeObject<OSRMResponse>(strResponse);
                    
                    //Check if OSRM found a route
                    if (OSRMData.status == "0")
                    {
                        WriteLog("Route found. Generating route command for Navigator");
                        string strRoutingTable = ""; // Start off empty

                        //Loop the feedback from OSRM
                        foreach (var routing_point in OSRMData.route_geometry)
                        {
                            strRoutingTable = strRoutingTable + routing_point + ";";
                        }

                        //Route
                        WriteLog("Request Navigator to route to destination");
                        SendCommand("$destination=" + strRoutingTable + "navigate;instant\r\n", true, TCPCommand.Destination);
                    }
                    else boolOSRMError = true;
                }
                catch
                {
                    //Let user know OSRM is not working
                    WriteLog("OSRM failed to provide routing information");
                    this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/OSRMERROR"), "AUTOHIDE");

                    //OSRM encountered an error
                    boolOSRMError = true;
                }
            }

            //Use Navigator Engine?
            if (boolOSRMEnabled == false || boolOSRMError == true)
            {
                //Set new destination
                WriteLog("Navigator generating route to destination");
                SendCommand("$destination=" + navLocation.latitude.ToString(CultureInfo.InvariantCulture) + "," + navLocation.longitude.ToString(CultureInfo.InvariantCulture) + ";navigate;instant\r\n", true, TCPCommand.Destination);
            }
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

        // Tells Centrafuse whether or not navigation is visible
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
            SendCommand("$destination=clear\r\n", true, TCPCommand.Destination);
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
                
        // Event to ask Navigator for navigation statistics
        private void NavDestinationTimer_Tick(object sender, EventArgs e)
        {
            //Ask Navigator for navigation statistics
            SendCommand("$navigation_statistics\r\n", false, TCPCommand.Statistics);

            //Ask Navigator for nearest street, but only if Navigator version is 12.4 or higher as previous versions crash
            if ((CF_navGetInfo(CFNavInfo.Latitude) != "0.00000") && (CF_navGetInfo(CFNavInfo.Longitude) != "0.00000") && (decimal.Compare(decNavigatorVersion, new decimal(12.4)) >= 0))
            {
                SendCommand("$nearest_streets=" + CF_navGetInfo(CFNavInfo.Latitude) + "," + CF_navGetInfo(CFNavInfo.Longitude) + ";1;50\r\n", false, TCPCommand.NearestStreets);
            }

            //Get SpeedUnit (We need to poll this as user can change setting, but we're not told about the change)
            if (ReadCFValue("/APPCONFIG/SPEEDUNIT", "I", configPath)) SpeedUnit = Unit.IMPERIAL;
            if (ReadCFValue("/APPCONFIG/SPEEDUNIT", "M", configPath)) SpeedUnit = Unit.METRIC;
            if (ReadCFValue("/APPCONFIG/UNIT", "I", configPath)) DistUnit = Unit.IMPERIAL;
            if (ReadCFValue("/APPCONFIG/UNIT", "M", configPath)) DistUnit = Unit.METRIC;

            //Ask navigator for routing and destination information
            GetNavigatorRoutingXML();
        }
        
        //Called by GPS Status screen to parse GPS Date/Time into local date/time
        private DateTime parsTimeOfFix(String dateOfFix, String timeOfFix)
        {
            string[] formats= { "dd/MM/yy HH:mm:ss" };
            DateTime convertedDate = 
                DateTime.SpecifyKind(
                    DateTime.ParseExact(
                        dateOfFix.Substring(0, 2) + "/" + dateOfFix.Substring(2, 2) + "/" + dateOfFix.Substring(4, 2) + " "
                        + timeOfFix.Substring(0, 2) + ":" + timeOfFix.Substring(2, 2) + ":" + timeOfFix.Substring(4, 2), 
                        formats, 
                        new CultureInfo(CultureInfo.CurrentUICulture.Name), 
                        DateTimeStyles.None)
                , DateTimeKind.Utc);

            DateTime dt = convertedDate.ToLocalTime();
            return dt;
        }
    }

}