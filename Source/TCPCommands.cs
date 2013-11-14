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
 * All functions realted to communicating with Navigator
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
        private int intTCPPort = 0;                         // TCP Port for communications with Mapfactor
        private string strIP = "127.0.0.1";                 // Default IP port
        IPEndPoint ipep = null;
        Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Queue<TCPCommand> TCPCommandQueue = new Queue<TCPCommand>();        //Keeps track of which command are sent
        # endregion

        /**/ //WaitForReply not implemented yet...
        private void SendCommand(string strNavigatorCommand, bool WaitForReply, TCPCommand tcpCommand)
        {
            if (server.Connected == false)
            {
                //If we dont have a connection with Navigator, try and create one
                try
                {
                    //Setup the telnet connection                

                    //ipep = new IPEndPoint(IPAddress.Parse(strIP), intTCPPort);
                    //WriteLog("IPEndPoint created");
                    server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    WriteLog("Socket created");
                }
                catch { WriteLog("Failed to create socket"); }
                
                try
                {
                    //Configure the connection
                    server.Blocking = false;
                    AsyncCallback onconnect = new AsyncCallback(OnConnect);
                    server.BeginConnect(IPAddress.Parse(strIP), intTCPPort, onconnect, server);                    
                    WriteLog("Trying to establish connection. BeginConnect() Started");

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
                        WriteLog("Failed to connect");
                    }
                }
                catch { WriteLog("Failed to connect");  }
            }

            if (server.Connected)
            {
                WriteLog("Sending '" + strNavigatorCommand + "'");
                server.Send(Encoding.ASCII.GetBytes(strNavigatorCommand));
                TCPCommandQueue.Enqueue(tcpCommand);
                //WriteLog(TCPCommandQueue.Count.ToString());
            }
            else
            {
                WriteLog("Not connected. Unable to communicate with Navigator");
            }
        }

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

        public void OnConnect(IAsyncResult ar)
        {
            // Socket was the passed in object
            Socket sock = (Socket)ar.AsyncState;

            // Check if we were sucessfull
            try
            {
                //    sock.EndConnect( ar );
                if (sock.Connected)
                    SetupRecieveCallback(sock);
                else
                    WriteLog("Unable to connect to remote machine, Connect Failed!");

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
            // Socket was the passed in object
            Socket sock = (Socket)ar.AsyncState;

            // Check if we got any data
            try
            {
                int nBytesRec = sock.EndReceive(ar);
                if (nBytesRec > 0)
                {
                    // Wrote the data to the List
                    string sMessage = Encoding.ASCII.GetString(m_byBuff, 0, nBytesRec);                  

                    // Any unhandled errors in this function causes all future messages from Navigator to be lost
                    try
                    {
                        // Thread safe operation here
                        //WriteLog("Recieved from Navigator: " + sMessage);

                        //Split on the CRLF and remove the empty spaces
                        sMessage = sMessage.Replace(" ", "");
                        string[] strParse = sMessage.ToUpper().Split(new string[] { "\r\n" }, StringSplitOptions.None);

                        //This is messy as there's no "standard" way Navigator provides the messages
                        foreach (string strCommands in strParse)
                        {
                            //xxx
                            if (TCPCommandQueue.Count > 0)
                            {
                                WriteLog("Next command response should be for : " + TCPCommandQueue.Peek().ToString() + " " + TCPCommandQueue.Count.ToString());
                                TCPCommandQueue.Dequeue();
                            }
                             
                            if (strCommands.Contains("SOUND"))
                            {
                                //Mute/unmute
                                if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                                {                                    
                                    WriteLog("Mute (GPS ATT) CF Audio. Start Timer");
                                    CF_systemCommand(CF_Actions.ATT);

                                    //Can't enable timer in a non-UI thread!
                                    this.BeginInvoke(new MethodInvoker(delegate { muteCFTimer.Enabled = true; }));
                                }
                                else WriteLog("CF GPS ATT not enabled");
                                
                                /*MUSICMODE: 3 = plugin source, 2 = radio, 1 = media player */
                                //If not mediaplayer, not GPSAttMute is not enabled, then use playpaus
                                if (ReadCFValue("/SETTINGS/CURRENT/MUSICMODE", "1", settingsPath) == false || CF_getConfigFlag(CF_ConfigFlags.GPSAttMute) == false)
                                {
                                    WriteLog("Mediaplayer is not active");

                                    //Play/Pause
                                    if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/PAUSEPLAYSTATUS")) == true)
                                    {
                                        WriteLog("PlayPause Enabled.");
                                        CF_systemCommand(CF_Actions.PAUSE);

                                        //Can't enable timer in a non-UI thread!
                                        this.BeginInvoke(new MethodInvoker(delegate { muteCFTimer.Enabled = true; }));
                                    }
                                    else WriteLog("PlayPause not enabled");

                                    //Send notification
                                    if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/NOTIFICATIONSTATUS")) == true)
                                    {
                                        WriteLog("Notification Enabled");
                                        //CF3_raisePluginEvent(Mmute)

                                        //Can't enable timer in a non-UI thread!
                                        this.BeginInvoke(new MethodInvoker(delegate { muteCFTimer.Enabled = true; }));
                                    }
                                    else WriteLog("Notification not enabled");
                                }
                                else WriteLog("Mediaplayer is active or ATT is not enabled. Not sending Pause or Event");
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
                            else if (strCommands.Contains("$GPRMC"))
                            {
                                //WriteLog("GPRMC sentence");
                                try
                                {
                                    string[] rmCdata = strCommands.Split(',');
                                    try { _currentPosition.Latitude = rmCdata[4] == "N" ? double.Parse(rmCdata[3], CultureInfo.InvariantCulture) : double.Parse("-" + rmCdata[3], CultureInfo.InvariantCulture); }
                                    catch { WriteLog("Failed to convert Latitude"); }
                                    try { _currentPosition.Longitude = rmCdata[6] == "E" ? double.Parse(rmCdata[5], CultureInfo.InvariantCulture) : double.Parse("-" + rmCdata[5], CultureInfo.InvariantCulture); }
                                    catch { WriteLog("Failed to convert Longitude"); }
                                    try { _currentPosition.Heading = double.Parse(rmCdata[8], CultureInfo.InvariantCulture); }
                                    catch { WriteLog("Failed to convert Heading"); }
                                    try { _currentPosition.Speed = double.Parse(rmCdata[7], CultureInfo.InvariantCulture); }
                                    catch { WriteLog("Failed to convert Speed"); }
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
                                    try { _currentPosition.LockedSatellites = int.Parse(ggaData[7], CultureInfo.InvariantCulture); }
                                    catch { } //WriteLog("Failed to convert LockedSatellites"); }
                                    try { _currentPosition.Altitude = double.Parse(ggaData[9], CultureInfo.InvariantCulture); }
                                    catch { } //WriteLog("Failed to convert Altitude"); }
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
                                WriteLog("Ack message... for which command is not known....");
                            }
                            else if (strCommands.Split(',').Length == 4)
                            {
                                if (this.Visible == true)
                                {
                                    WriteLog("Navigation stats. Do nothing as plugin is visible: '" + strCommands + "'");
                                }
                                else
                                {
                                    try
                                    {
                                        //0=distance in meters to next waypoint
                                        //1=time in seconds to next waypoint
                                        //2=distance in meters to destination
                                        //3=time in seconds to destination
                                        int intTimeToDestination = int.Parse(strCommands.Split(',')[3]);
                                        if ((intTimeToDestination < 30))
                                        {
                                            this.CF_systemCommand(CF_Actions.SHOWINFO, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/ARRIVING") + intTimeToDestination.ToString() + this.pluginLang.ReadField("/APPLANG/NAVIGATOR/SECONDS"), "AUTOHIDE");
                                        }
                                    }
                                    catch { WriteLog("Unable to parse navigation statistics"); }
                                }
                            }
                            else if (strCommands != "")
                            {
                                WriteLog("Not handled: '" + strCommands + "'");
                            }
                        }
                    }
                    catch { WriteLog("Error in OnAddMessage"); }

                    // If the connection is still usable restablish the callback
                    SetupRecieveCallback(sock);
                }
                else
                {
                    // If no data was recieved then the connection is probably dead
                    WriteLog("Client {0}, disconnected " + sock.RemoteEndPoint);
                    sock.Shutdown(SocketShutdown.Both);
                    sock.Close();
                }
            }
            catch { 
                WriteLog("Unusual error during recieve!"); 
            }
        }
    }
}
