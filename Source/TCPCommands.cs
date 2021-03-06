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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

/*
 * Navigator Bug:
 *  Unable to use command line parameters for IP and port for TCP communications. Workaround modifies XML file directly
 *  Requires Navigator to fix bug
*/

/*
 * All functions related to communicating with Navigator
*/

namespace Navigator
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using centrafuse.Plugins;
    using System.Text;
    using System.Globalization;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Windows.Forms;

    public partial class Navigator
    {
        #region Variables
        private int intTCPPort = 4242;                      // TCP Port for communications with Mapfactor
        private string strIP = "127.0.0.1";                 // Default IP port
        Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        bool boolConnecting = false;                        // Are we trying to connect?
        Queue<TCPCommand> TCPCommandQueue = new Queue<TCPCommand>();        //Keeps track of which command are sent to match up with the response
        # endregion

        /**/ //WaitForReply not implemented yet...
        private void SendCommand(string strNavigatorCommand, bool WaitForReply, TCPCommand tcpCommand)
        {
            //There's probably a better way of doing this...
            if (server.Connected == false && boolConnecting == false)
            {
                if (boolConnecting == false)
                {
                    server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                }

                //If we dont have a connection with Navigator, try and create one
                try
                {
                    boolConnecting = true; // Do not re-enter here until we either have a connection or a failed connection

                    //Configure the connection  
                    server.Blocking = false;
                    AsyncCallback onconnect = new AsyncCallback(OnConnect);
                    IAsyncResult serverResult = server.BeginConnect(IPAddress.Parse(strIP), intTCPPort, onconnect, server);
                    WriteLog("Trying to establish connection. BeginConnect() Started");

                    bool success = serverResult.AsyncWaitHandle.WaitOne(500, true);
                    WriteLog("Success: " + success.ToString());

                    if (server.Connected)
                    {
                        WriteLog("Connected");

                        //Get some basic information about Navigator
                        string strTmp = "$protocol_version\r\n";
                        WriteLog("Sending '" + strTmp + "'");
                        server.Send(Encoding.ASCII.GetBytes(strTmp));                                            
                        TCPCommandQueue.Enqueue(TCPCommand.Protocol);

                        strTmp = "$software_version\r\n";
                        WriteLog("Sending '" + strTmp + "'");
                        server.Send(Encoding.ASCII.GetBytes(strTmp));
                        TCPCommandQueue.Enqueue(TCPCommand.SoftwareVersion);
                    }
                    else
                    {
                        WriteLog("Failed to connect.");
                        this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/TCPFAILED"), "AUTOHIDE");

                        try
                        {
                            boolConnecting = false;
                            server.Close();
                        }
                        catch { WriteLog("Failed to close socket"); }
                    }
                }
                catch (SocketException se)
                {
                    WriteLog("Failed to connect; " + se.Message);
                }
            }

            if (server.Connected)
            {
                try
                {
                    WriteLog("Sending '" + strNavigatorCommand + "'");
                    server.Send(Encoding.ASCII.GetBytes(strNavigatorCommand));

                    //Statistics don't return a value if GPS is not working or receiving data
                    if (tcpCommand != TCPCommand.Statistics) TCPCommandQueue.Enqueue(tcpCommand);
                }
                catch (Exception ex)
                {
                    WriteLog("Failed to Send(), " + ex.ToString());
                }
            }
            else
            {
                WriteLog("Not connected. Unable to communicate with Navigator");
            }
        }

        public void OnConnect(IAsyncResult ar)
        {
            // Socket was the passed in object
            Socket sock = (Socket)ar.AsyncState;

            // Check if we were sucessfull
            try
            {
                //sock.EndConnect(ar);
                if (sock.Connected)
                {
                    WriteLog("Connection made");
                    SetupRecieveCallback(sock);
                }
                else
                {                    
                    WriteLog("Unable to connect to remote machine, Connect Failed!");
                    sock.Close();
                    WriteLog("Socket closed");
                }

            }
            catch { WriteLog("Unknown error during connect"); }
        }
   
        private byte[] m_byBuff = new byte[256];    // Recieved data buffer
        public void SetupRecieveCallback(Socket sock)
        {
            try
            {
                AsyncCallback recieveData = new AsyncCallback(OnRecievedData);
                sock.BeginReceive(m_byBuff, 0, m_byBuff.Length, SocketFlags.None, recieveData, sock);
            }
            catch { WriteLog("Setup Recieve Callback failed!"); }
        }

        //Triggered when new data arrives
        public void OnRecievedData(IAsyncResult ar)
        {
            // Check if we got any data
            try
            {
                // Socket was the passed in object
                Socket sock = (Socket)ar.AsyncState;

                if (sock.Connected)
                {
                    int nBytesRec = sock.EndReceive(ar);

                    if (nBytesRec > 0)
                    {
                        // sMessage contains the message from navigator
                        string sMessage = Encoding.ASCII.GetString(m_byBuff, 0, nBytesRec);

                        // Any unhandled errors in this function causes all future messages from Navigator to be lost
                        try
                        {
                            //Split on the CRLF and remove the empty spaces
                            //sMessage = sMessage.Replace(" ", "");
                            string[] strParse = sMessage.Replace(" ", "").ToUpper().Split(new string[] { "\r\n" }, StringSplitOptions.None);

                            //This is messy as there's no "standard" way Navigator provides the messages
                            foreach (string strCommands in strParse)
                            {
                                if (strCommands.ToUpper().Contains("SOUND"))
                                {
                                    WriteLog("TCPCommand '" + TCPCommand.SoundVolume.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();
                                    
                                    //Only do this if we're not using named pipes
                                    if (!boolNamedPipes)
                                    {
                                        //Configure CF sound handling
                                        NavigatorStopCFAudio();
                                    }
                                }
                                else if (strCommands.ToUpper().Contains("WAYPOINT"))
                                {
                                    WriteLog("TCPCommand '"  + TCPCommand.NavInfoWaypointInfo.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();

                                    if (this.Visible == true)
                                    {
                                        WriteLog("Waypoint reached. Do nothing as plugin is visible: '" + strCommands + "'");
                                    }
                                    else
                                    {
                                        WriteLog("Waypoint reached: '" + strCommands + "'");
                                        this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/WAYPOINT"), "AUTOHIDE");
                                    }
                                }
                                else if (strCommands.ToUpper().Contains("RECALCULATING"))
                                {
                                    WriteLog("TCPCommand '" + TCPCommand.NavInfoRecalculationWarning.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();

                                    if (this.Visible == true)
                                    {
                                        WriteLog("Recalculating. Do nothing as plugin is visible: '" + strCommands + "'");
                                    }
                                    else
                                    {
                                        WriteLog("Recalculating or lost: '" + strCommands + "'");
                                        this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/RECALCULATING"), "AUTOHIDE");
                                    }
                                }
                                else if (strCommands.ToUpper().Contains("LOST"))
                                {
                                    WriteLog("TCPCommand '" + TCPCommand.NavInfoWaypointInfo.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();

                                    if (this.Visible == true)
                                    {
                                        WriteLog("Lost. Do nothing as plugin is visible: '" + strCommands + "'");
                                    }
                                    else
                                    {
                                        WriteLog("Lost: '" + strCommands + "'");
                                        this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/LOST"), "AUTOHIDE");
                                    }
                                }
                                else if (strCommands.ToUpper().Contains("DESTINATIONREACHED"))
                                {
                                    WriteLog("TCPCommand '" + TCPCommand.NavInfoWaypointInfo.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();

                                    if (this.Visible == true)
                                    {
                                        WriteLog("Destination reached. Do nothing as plugin is visible: '" + strCommands + "'");
                                    }
                                    else
                                    {
                                        WriteLog("Destination reached: '" + strCommands + "'");
                                        this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/DESTINATION"), "AUTOHIDE");
                                    }
                                }
                                else if (strCommands.ToUpper().Contains("$GPRMC"))
                                {
                                    //Do not Dequeue for this response

                                    //WriteLog("GPRMC sentence");
                                    try
                                    {
                                        string[] rmCdata = strCommands.Split(',');

                                        //Latitude
                                        try 
                                        { 
                                            _currentPosition.Latitude = rmCdata[4] == "N" ? NMEAtoDecimal(rmCdata[3]) : NMEAtoDecimal(rmCdata[3]) * -1; 
                                        }
                                        catch
                                        { 
                                            _currentPosition.Latitude = 0; 
                                            WriteLog("Failed to convert Latitude"); 
                                        }

                                        //Longitude
                                        try 
                                        { 
                                            _currentPosition.Longitude = rmCdata[6] == "E" ? NMEAtoDecimal(rmCdata[5]) : NMEAtoDecimal(rmCdata[5]) * -1; 
                                        }
                                        catch 
                                        { 
                                            _currentPosition.Longitude = 0; 
                                            WriteLog("Failed to convert Longitude"); 
                                        }

                                        //Heading
                                        try 
                                        { 
                                            _currentPosition.Heading = double.Parse(rmCdata[8], CultureInfo.InvariantCulture); 
                                        }
                                        catch 
                                        { 
                                            _currentPosition.Heading = 0; 
                                            WriteLog("Failed to convert Heading"); 
                                        }

                                        //Speed
                                        try 
                                        {
                                            //WriteLog("Raw NMEA Speed: " + rmCdata[7].ToString());

                                            //Speed is in knots in NMEA strings
                                            switch (SpeedUnit)
                                            {
                                                case Unit.METRIC:
                                                    try { _currentPosition.Speed = double.Parse(rmCdata[7], CultureInfo.InvariantCulture) * knot_To_kmh; }
                                                    catch { _currentPosition.Speed = 0; }
                                                    break;
                                                case Unit.IMPERIAL:
                                                    try { _currentPosition.Speed = double.Parse(rmCdata[7], CultureInfo.InvariantCulture) * knot_To_mph; }
                                                    catch { _currentPosition.Speed = 0; }
                                                    break;
                                                default:
                                                    try { _currentPosition.Speed = double.Parse(rmCdata[7], CultureInfo.InvariantCulture); }
                                                    catch { _currentPosition.Speed = 0; }
                                                    break;
                                            }

                                            //How many digits after .
                                            int length = rmCdata[7].Substring(rmCdata[7].IndexOf(".") + 1).Length;

                                            //Round to the same number of decimals as the input string has?
                                            if (boolTRIMDIGITS)
                                            {
                                                _currentPosition.Speed = System.Math.Round(_currentPosition.Speed, 0, MidpointRounding.AwayFromZero);
                                            }
                                            else
                                            {
                                                _currentPosition.Speed = System.Math.Round(_currentPosition.Speed, length, MidpointRounding.AwayFromZero);
                                            }
                                        }
                                        catch
                                        {
                                            _currentPosition.Speed = 0;
                                            WriteLog("Failed to convert Speed"); 
                                        }

                                        //GPS Time
                                        try 
                                        { 
                                            _navStats.GPSTime = rmCdata[1]; 
                                        }
                                        catch 
                                        { 
                                            _navStats.GPSTime = "";
                                            WriteLog("Failed to convert GPS Time"); 
                                        }

                                        //GPS Date
                                        try 
                                        {
                                            _navStats.GPSDate = rmCdata[9];
                                        }
                                        catch
                                        {
                                            _navStats.GPSDate = "";
                                            WriteLog("Failed to convert GPS Date"); 
                                        }
                                    }
                                    catch
                                    {
                                        WriteLog("Failed to parse GPRMC data");
                                    }
                                    finally
                                    {
                                        //WriteLog("Current Lat/Long: '" + _currentPosition.Latitude + " - " + _currentPosition.Longitude + "'");
                                        //WriteLog("Current alt/head: '" + _currentPosition.Altitude + " - " + _currentPosition.Heading + "'");
                                        //WriteLog("Current Direction: '" + CF_navGetInfo(CFNavInfo.Direction) + "'");
                                    }
                                }
                                else if (strCommands.ToUpper().Contains("$GPGGA"))
                                {
                                    //Do not Dequeue for this response

                                    //WriteLog("GPGGA sentence");
                                    try
                                    {
                                        //Decode the data
                                        string[] ggaData = strCommands.Split(',');

                                        //LockedSatellites
                                        try 
                                        {
                                            _currentPosition.LockedSatellites = int.Parse(ggaData[7], CultureInfo.InvariantCulture); 
                                        }
                                        catch
                                        {
                                            _currentPosition.LockedSatellites = 0;
                                            //WriteLog("Failed to convert LockedSatellites"); }
                                        };

                                        //Altitude
                                        switch (ggaData[10].ToString())
                                        {
                                            case "M":
                                                switch (DistUnit)
                                                {
                                                    case Unit.METRIC:
                                                        try { _currentPosition.Altitude = double.Parse(ggaData[9], CultureInfo.InvariantCulture);}
                                                        catch { _currentPosition.Altitude = 0; }
                                                        break;
                                                    case Unit.IMPERIAL:
                                                        //Convert M to feet and round it off to 0 decimals
                                                        try { _currentPosition.Altitude = System.Math.Round(double.Parse(ggaData[9], CultureInfo.InvariantCulture) * meter_To_ft, 0, MidpointRounding.AwayFromZero); }
                                                        catch { _currentPosition.Altitude = 0; }
                                                        break;
                                                    default:
                                                        _currentPosition.Altitude = 0;
                                                        break;
                                                }
                                                break;
                                            default:
                                                break;
                                        }
                                    }
                                    catch
                                    {
                                        WriteLog("Failed to parse GPGGA data");
                                    }
                                    finally
                                    {
                                        //WriteLog("Current Lat/Long: '" + _currentPosition.Latitude + " - " + _currentPosition.Longitude + "'");
                                        //WriteLog("Current alt/head: '" + _currentPosition.Altitude + " - " + _currentPosition.Heading + "'");
                                        //WriteLog("Current Direction: '" + CF_navGetInfo(CFNavInfo.Direction) + "'");
                                    }
                                }
                                else if (strCommands.ToUpper().Contains("OK"))
                                {
                                    //strCommands will always be 'OK'
                                    WriteLog("Command for '" + TCPCommandQueue.Peek().ToString() + "' was successfull");
                                    DeQueueTCPCommandQueue();
                                }
                                else if (strCommands.ToUpper().Contains("BUSY"))
                                {
                                    //strCommands will always be 'BUSY'
                                    WriteLog("Failed to ask Navigator to '" + TCPCommandQueue.Peek().ToString() + "'. System busy");
                                    DeQueueTCPCommandQueue();

                                    this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/BUSY"), "AUTOHIDE");
                                }
                                else if (strCommands.ToUpper().Contains("ERROR"))
                                {
                                    //strCommands will always be 'ERROR'
                                    WriteLog("Error when asking Navigator to '" + TCPCommandQueue.Peek().ToString() + "'");
                                    DeQueueTCPCommandQueue();
                                    
                                    this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/ERROR"), "AUTOHIDE");
                                }
                                else if (strCommands.ToUpper().Contains("LICENSEERROR"))
                                {
                                    //strCommands will always be 'LICENSEERROR'
                                    WriteLog("Failed to ask Navigator to do something due to the lack of a license '" + TCPCommandQueue.Peek().ToString() + "'");
                                    DeQueueTCPCommandQueue();
                                    
                                    this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/LICENSEERROR"), "AUTOHIDE");
                                }
                                else if (strCommands.ToUpper().Contains("NOTNAVIGATING"))
                                {
                                    //If not navigating, clear these
                                    _navStats.DistanceDestination = 0;
                                    _navStats.DistanceNextWaypoint = 0;
                                    _navStats.TimeSecondsDestination = 0;
                                    _navStats.TimeSecondsNextWaypoint = 0;
                                }
                                else if (strCommands.Split(',').Length == 4)
                                {
                                    WriteLog("TCPCommand '" + TCPCommand.NavInfoWaypointInfo.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();

                                    //Default from Navigator is Meters
                                    switch (DistUnit)
                                    {
                                        case Unit.METRIC:
                                            WriteLog("Logging Stats in Metric");

                                            try { _navStats.DistanceDestination = double.Parse(strCommands.Split(',')[2]); }
                                            catch (Exception errMsg) { _navStats.DistanceDestination = 0; WriteLog("Unable to parse DistanceDestination: " + errMsg.Message); }

                                            //Sanity check. If Destination = Waypoint, set Waypoint values to 0
                                            try
                                            {
                                                if (double.Parse(strCommands.Split(',')[0]) != double.Parse(strCommands.Split(',')[2]))
                                                    _navStats.DistanceNextWaypoint = double.Parse(strCommands.Split(',')[0]);
                                                else
                                                    _navStats.DistanceNextWaypoint = 0;
                                            }
                                            catch (Exception errMsg) { _navStats.DistanceNextWaypoint = 0; WriteLog("Unable to parse DistanceNextWaypoint: " + errMsg.Message); }

                                            try { _navStats.TimeSecondsDestination = double.Parse(strCommands.Split(',')[3]); }
                                            catch (Exception errMsg) { _navStats.TimeSecondsDestination = 0; WriteLog("Unable to parse TimeSecondsDestination: " + errMsg.Message); }

                                            //Sanity check. If Destination = Waypoint, set Waypoint values to 0
                                            try 
                                            {
                                                if (double.Parse(strCommands.Split(',')[1]) != double.Parse(strCommands.Split(',')[3]))
                                                    _navStats.TimeSecondsNextWaypoint = double.Parse(strCommands.Split(',')[1]);
                                                else
                                                    _navStats.TimeSecondsNextWaypoint = 0;
                                            }
                                            catch (Exception errMsg) { _navStats.TimeSecondsNextWaypoint = 0; WriteLog("Unable to parse TimeSecondsNextWaypoint: " + errMsg.Message); }
                                            
                                            break;
                                        case Unit.IMPERIAL:
                                            //Convert M to feet
                                            WriteLog("Logging Stats in Imperial");
                                            try { _navStats.DistanceDestination = System.Math.Round(double.Parse(strCommands.Split(',')[2]) * meter_To_ft, 0, MidpointRounding.AwayFromZero); }
                                            catch (Exception errMsg) { _navStats.DistanceDestination = 0; WriteLog("Unable to parse DistanceDestination: " + errMsg.Message); }

                                            //Sanity check. If Destination = Waypoint, set Waypoint values to 0
                                            try
                                            {
                                                if (double.Parse(strCommands.Split(',')[0]) != double.Parse(strCommands.Split(',')[2]))
                                                    _navStats.DistanceNextWaypoint = System.Math.Round(double.Parse(strCommands.Split(',')[0]) * meter_To_ft, 0, MidpointRounding.AwayFromZero);
                                                else
                                                    _navStats.DistanceNextWaypoint = 0;
                                            }
                                            catch (Exception errMsg) { _navStats.DistanceNextWaypoint = 0; WriteLog("Unable to parse DistanceNextWaypoint: " + errMsg.Message); }

                                            try { _navStats.TimeSecondsDestination = System.Math.Round(double.Parse(strCommands.Split(',')[3]) * meter_To_ft, 0, MidpointRounding.AwayFromZero); }
                                            catch (Exception errMsg) { _navStats.TimeSecondsDestination = 0; WriteLog("Unable to parse TimeSecondsDestination: " + errMsg.Message); }

                                            //Sanity check. If Destination = Waypoint, set Waypoint values to 0
                                            try
                                            {
                                                if (double.Parse(strCommands.Split(',')[1]) != double.Parse(strCommands.Split(',')[3]))
                                                    _navStats.TimeSecondsNextWaypoint = System.Math.Round(double.Parse(strCommands.Split(',')[1]) * meter_To_ft, 0, MidpointRounding.AwayFromZero);
                                                else
                                                    _navStats.TimeSecondsNextWaypoint = 0;
                                            }
                                            catch (Exception errMsg) { _navStats.TimeSecondsNextWaypoint = 0; WriteLog("Unable to parse TimeSecondsNextWaypoint: " + errMsg.Message); }

                                            break;
                                        default:
                                            try { _navStats.DistanceNextWaypoint = double.Parse(strCommands.Split(',')[0]); }
                                            catch (Exception errMsg) { _navStats.DistanceNextWaypoint = 0; WriteLog("Unable to parse DistanceNextWaypoint: " + errMsg.Message); }
                                            
                                            try { _navStats.TimeSecondsNextWaypoint = double.Parse(strCommands.Split(',')[1]); }
                                            catch (Exception errMsg) { _navStats.TimeSecondsNextWaypoint = 0; WriteLog("Unable to parse TimeSecondsNextWaypoint: " + errMsg.Message); }

                                            try { _navStats.DistanceDestination = double.Parse(strCommands.Split(',')[2]); }
                                            catch (Exception errMsg) { _navStats.DistanceDestination = 0; WriteLog("Unable to parse DistanceDestination: " + errMsg.Message); }

                                            try { _navStats.TimeSecondsDestination = double.Parse(strCommands.Split(',')[3]); }
                                            catch (Exception errMsg) { _navStats.TimeSecondsDestination = 0; WriteLog("Unable to parse TimeSecondsDestination: " + errMsg.Message); }

                                            break;
                                    }

                                    if (this.Visible == true)
                                    {
                                        WriteLog("Navigation stats. Do nothing as plugin is visible: '" + strCommands + "'");
                                    }
                                    else
                                    {
                                        //Tell the user?
                                        if ((_navStats.TimeSecondsNextWaypoint < 30) && (_navStats.TimeSecondsNextWaypoint > 20))
                                        {
                                            this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/ARRIVING") + " " + _navStats.TimeSecondsNextWaypoint.ToString() + " " + this.pluginLang.ReadField("/APPLANG/NAVIGATOR/SECONDS"), "AUTOHIDE");
                                        }
                                    }
                                }
                                else if (strCommands.Split(';').Length == 2)
                                {
                                    WriteLog("TCPCommand '" + TCPCommand.NearestStreets.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();

                                    try
                                    {
                                        _navStats.Street = sMessage.Split(';')[0].Replace("\"", "");
                                        //WriteLog("Street name: '" + _navStats.Street + "'");
                                    }
                                    catch (Exception errMsg) { _navStats.Street = ""; WriteLog("Unable to parse Street: " + errMsg.Message); }
                                }
                                else if (strCommands.ToUpper().Contains("V."))
                                {
                                    WriteLog("TCPCommand '" + TCPCommand.SoftwareVersion.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();

                                    //V.12.4.3 => 12.4 / 3
                                    try
                                    {
                                        decNavigatorVersion = decimal.Parse(strCommands.Split('.')[1] + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + strCommands.Split('.')[2]);
                                    }
                                    catch (Exception errMsg) { WriteLog("Error Parsing Navigator Version: '" + strCommands + "', " + errMsg.Message); }
                                    try
                                    {
                                        intNavigatorRevision = int.Parse(strCommands.Split('.')[3]);
                                    }
                                    catch (Exception errMsg) { WriteLog("Error Parsing Navigator Revision: '" +strCommands + "', " + errMsg.Message); }
                                    
                                    WriteLog("Navigator Version: '" + decNavigatorVersion.ToString() + "', revision '" + intNavigatorRevision.ToString() + "'");
                                }
                                else if (strCommands.Split('.').Length == 3)
                                {
                                    WriteLog("TCPCommand '" + TCPCommand.Protocol.ToString() + "' arrived");
                                    DeQueueTCPCommandQueue();

                                    //2.2.0
                                    strProtocolVersion = strCommands;
                                    WriteLog("Navigator TCP Protocol Version: '" + strProtocolVersion + "'");
                                }
                                else if (strCommands != "")
                                {
                                    WriteLog("TCPCommand 'Unknown/Not handled' arrived");
                                    DeQueueTCPCommandQueue();

                                    WriteLog("Not handled: '" + strCommands + "'");
                                }
                            }
                        }                       
                        //LK, 30-nov-2013: Added reason for exception
                        catch (Exception errMsg) { WriteLog("Error in OnAddMessage: " + errMsg.Message); }

                        // If the connection is still usable restablish the callback
                        SetupRecieveCallback(sock);
                    }
                    else
                    {
                        // If no data was recieved then the connection is probably dead
                        WriteLog("Client disconnected: " + sock.RemoteEndPoint.ToString());
                        sock.Shutdown(SocketShutdown.Both);
                        sock.Close();
                    }
                }
            }
            //LK, 30-nov-2013: Added reason for exception
            catch (Exception errMsg) { WriteLog("Unusual error during recieve: " + errMsg.Message); }
        }
        
        private void DeQueueTCPCommandQueue()
        {
            try
            {
                if (TCPCommandQueue.Count > 0)
                {
                    TCPCommandQueue.Dequeue();
                }
                else
                {
                    WriteLog("Failed to dequeue TCPCommandQueue. Navigator can send TCP responses without being queried like Waypoint and sound information");
                }
            }
            catch
            {
                WriteLog("Failed to dequeue TCPCommandQueue. Navigator can send TCP responses without being queried like Waypoint and sound information");
            }
        }
    }
}
