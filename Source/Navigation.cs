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
 * All functions related to Navigation and navigation data
*/

using System;
using centrafuse.Plugins;
using System.Globalization;

namespace Navigator
{
    using System;

    public partial class Navigator
    {
        private readonly CfNavData _currentPosition = new CfNavData();

        // Event to get CF to ask for stats
        private void NavStatustimer_Tick(object sender, EventArgs e)
        {
            try { CF_updateText("DataLongitude", CF_navGetInfo(CFNavInfo.Longitude)); } catch { };
            try { CF_updateText("DataLatitude", CF_navGetInfo(CFNavInfo.Latitude)); } catch { };
            try { CF_updateText("DataAltitude", CF_navGetInfo(CFNavInfo.Altitude)); } catch { };
            try { CF_updateText("DataLockedSatellites", CF_navGetInfo(CFNavInfo.LockedSatellites)); } catch { };
            try {
                if (ReadCFValue("/APPCONFIG/SPEEDUNIT", "I", CFTools.AppDataPath + "\\System\\config.xml")) CF_updateText("DataSpeed", CF_navGetInfo(CFNavInfo.Speed).Substring(0, 5) + " mph");
                if (ReadCFValue("/APPCONFIG/SPEEDUNIT", "M", CFTools.AppDataPath + "\\System\\config.xml")) CF_updateText("DataSpeed", CF_navGetInfo(CFNavInfo.Speed).Substring(0, 5) + " km/h"); 
            } 
            catch { };
            try { CF_updateText("DataDirection", CF_navGetInfo(CFNavInfo.Direction)); } catch { };
            try { CF_updateText("DataAzimuth", CF_navGetInfo(CFNavInfo.Azimuth)); } catch { };
            try { CF_updateText("DataETR", CF_navGetInfo(CFNavInfo.ETR)); } catch { };
            try { CF_updateText("DataRemainingDistance", CF_navGetInfo(CFNavInfo.RemainingDistance)); } catch { };
            try { CF_updateText("DataNextTurn", CF_navGetInfo(CFNavInfo.NextTurn)); } catch { };
            try { CF_updateText("DataInRoute", CF_navGetInfo(CFNavInfo.InRoute)); } catch { };
            try { CF_updateText("DataGPSTime", _navStats.GPSTime.Substring(0, 2) + ":" + _navStats.GPSTime.Substring(2, 2) + ":" + _navStats.GPSTime.Substring(4, 2) + " Offset:" + GetGmtOffset(_currentPosition.Latitude, _currentPosition.Longitude).ToString() + "+DST"); }catch { };
        }

        //Hide / Unhide the fields
        /**/ //This should move to a 2nd section later
        private void SetLabelStatus(bool status)
        {
            CF_setLabelEnableFlag("DataLongitude", status);
            CF_setLabelEnableFlag("DataLatitude", status);
            CF_setLabelEnableFlag("DataAltitude", status);
            CF_setLabelEnableFlag("DataLockedSatellites", status);
            CF_setLabelEnableFlag("DataSpeed", status);
            CF_setLabelEnableFlag("DataDirection", status);
            CF_setLabelEnableFlag("DataAzimuth", status);
            CF_setLabelEnableFlag("DataETR", status);
            CF_setLabelEnableFlag("DataRemainingDistance", status);
            CF_setLabelEnableFlag("DataNextTurn", status);
            CF_setLabelEnableFlag("DataInRoute", status);
            CF_setLabelEnableFlag("DataGPSTime", status);
            CF_setLabelEnableFlag("lblLongitude", status);
            CF_setLabelEnableFlag("lblLatitude", status);
            CF_setLabelEnableFlag("lblAltitude", status);
            CF_setLabelEnableFlag("lblLockedSatellites", status);
            CF_setLabelEnableFlag("lblSpeed", status);
            CF_setLabelEnableFlag("lblDirection", status);
            CF_setLabelEnableFlag("lblAzimuth", status);
            CF_setLabelEnableFlag("lblETR", status);
            CF_setLabelEnableFlag("lblRemainingDistance", status);
            CF_setLabelEnableFlag("lblNextTurn", status);
            CF_setLabelEnableFlag("lblInRoute", status);
            CF_setLabelEnableFlag("lblGPSTime", status);
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
                    retvalue = _navStats.TimeSecondsNextWaypoint.ToString();
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
                    retvalue = _navStats.DistanceMetersDestination.ToString();
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
                    retvalue = _navStats.TimeSecondsNextWaypoint.ToString();
                    break;
                case CFNavInfo.InRoute:
                    retvalue = _navStats.DistanceMetersNextWaypoint.ToString();
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

            //Clear current destination
            SendCommand("$destination=clear\r\n", true, TCPCommand.Destination);

            //Set new destination
            SendCommand("$destination=" + navLocation.latitude.ToString(CultureInfo.InvariantCulture) + "," + navLocation.longitude.ToString(CultureInfo.InvariantCulture) + ";navigate;instant\r\n", true, TCPCommand.Destination);
        }

        // This method is called to pass a destination to your navigation engine. Read the parameters you need from the navLocation variable and act accordingly
        public override void CF_navSetDestination(CFNavLocation navLocation, bool openNav, bool openFullScreen)
        {
            WriteLog("CF_navSetDestination(3)");
            //Clear current destination
            SendCommand("$destination=clear\r\n", true, TCPCommand.Destination);

            //Set new destination
            SendCommand("$destination=" + navLocation.latitude.ToString(CultureInfo.InvariantCulture) + "," + navLocation.longitude.ToString(CultureInfo.InvariantCulture) + ";navigate;instant\r\n", true, TCPCommand.Destination);

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
                
        // Event to get CF to ask for stats
        private void NavStatsTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("NavStats...");
            SendCommand("$navigation_statistics\r\n", false, TCPCommand.Statistics);
        }

        // Event to ask Navigator for navigation statistics
        private void NavDestinationTimer_Tick(object sender, EventArgs e)
        {
            //Ask CF for navigation statistics
            SendCommand("$navigation_statistics\r\n", false, TCPCommand.Statistics);
        }

        private DateTime parsTimeOfFix(String dateOfFix, String timeOfFix)
        {
            DateTime convertedDate = DateTime.SpecifyKind(DateTime.Parse(dateOfFix + " " + timeOfFix), DateTimeKind.Utc);
            var kind = convertedDate.Kind; // will equal DateTimeKind.Utc

            DateTime dt = convertedDate.ToLocalTime();
            return dt;
        }

    }

}