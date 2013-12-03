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
        Queue<TCPCommand> TCPCommandQueue = new Queue<TCPCommand>();        //Keeps track of which command are sent. Not fully implemented yet
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
                WriteLog("Sending '" + strNavigatorCommand + "'");
                server.Send(Encoding.ASCII.GetBytes(strNavigatorCommand));
                /**/ //Not used yet
                //TCPCommandQueue.Enqueue(tcpCommand);
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
                            sMessage = sMessage.Replace(" ", "");
                            string[] strParse = sMessage.ToUpper().Split(new string[] { "\r\n" }, StringSplitOptions.None);

                            //This is messy as there's no "standard" way Navigator provides the messages
                            foreach (string strCommands in strParse)
                            {
                                if (strCommands.Contains("SOUND"))
                                {
                                    //Only do this if we're not using named pipes
                                    if (!boolNamedPipes)
                                    {
                                        //Configure CF sound handling
                                        NavigatorStopCFAudio();
                                    }
                                }
                                else if (strCommands.Contains("WAYPOINT"))
                                {
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
                                else if (strCommands.Contains("RECALCULATING"))
                                {
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
                                else if (strCommands.Contains("LOST"))
                                {
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
                                else if (strCommands.Contains("DESTINATIONREACHED"))
                                {
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
                                else if (strCommands.Contains("$GPRMC"))
                                {
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
                                            //Speed is in knots in NMEA strings

                                            //Convert to Metric?
                                            if (ReadCFValue("/APPCONFIG/SPEEDUNIT", "M", configPath))
                                            {
                                                _currentPosition.Speed = double.Parse(rmCdata[7], CultureInfo.InvariantCulture) * 1.94384449244;
                                            } //Convert to Imperial?
                                            else if (ReadCFValue("/APPCONFIG/SPEEDUNIT", "I", configPath))
                                            {
                                                _currentPosition.Speed = double.Parse(rmCdata[7], CultureInfo.InvariantCulture) * 1.1507794480136;
                                            } //Unknown...
                                            else _currentPosition.Speed = 0;
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
                                else if (strCommands.Contains("$GPGGA"))
                                {
                                    //WriteLog("GPGGA sentence");
                                    try
                                    {
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
                                        try 
                                        {
                                            _currentPosition.Altitude = double.Parse(ggaData[9], CultureInfo.InvariantCulture);
                                        }
                                        catch 
                                        {
                                            _currentPosition.Altitude = 0;
                                            //WriteLog("Failed to convert Altitude");
                                        };
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
                                else if (strCommands.Contains("OK"))
                                {
                                    //strCommands will always be 'OK'
                                    WriteLog("Ack message... for which command is not known....");
                                }
                                else if (strCommands.Contains("BUSY"))
                                {
                                    //strCommands will always be 'BUSY'
                                    WriteLog("Failed to ask Navigator to do something... for which command is not known....");
                                    this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/BUSY"), "AUTOHIDE");
                                }
                                else if (strCommands.Contains("ERROR"))
                                {
                                    //strCommands will always be 'ERROR'
                                    WriteLog("Asked Navigator to do something it can't do... which command is not known....");
                                    this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/ERROR"), "AUTOHIDE");
                                }
                                else if (strCommands.Contains("NOTNAVIGATING"))
                                {
                                    //If not navigating, clear these
                                    _navStats.DistanceMetersDestination = 0;
                                    _navStats.DistanceMetersNextWaypoint = 0;
                                    _navStats.TimeSecondsDestination = 0;
                                    _navStats.TimeSecondsNextWaypoint = 0;
                                }
                                else if (strCommands.Split(',').Length == 4)
                                {
                                    try { _navStats.DistanceMetersNextWaypoint = int.Parse(strCommands.Split(',')[0]); }
                                    catch (Exception errMsg) { WriteLog("Unable to parse DistanceMetersNextWaypoint: " + errMsg.Message); }
                                    try { _navStats.TimeSecondsNextWaypoint = int.Parse(strCommands.Split(',')[1]); }
                                    catch (Exception errMsg) { WriteLog("Unable to parse TimeSecondsNextWaypoint: " + errMsg.Message); }
                                    try { _navStats.DistanceMetersDestination = int.Parse(strCommands.Split(',')[2]); }
                                    catch (Exception errMsg) { WriteLog("Unable to parse DistanceMetersDestination: " + errMsg.Message); }
                                    try { _navStats.TimeSecondsDestination = int.Parse(strCommands.Split(',')[3]); }
                                    catch (Exception errMsg) { WriteLog("Unable to parse TimeSecondsDestination: " + errMsg.Message); }
                                    
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
                                else if (strCommands != "")
                                {
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
    }
}
