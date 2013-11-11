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

delegate void AddMessage(string sNewMessage);

namespace Navigator
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using centrafuse.Plugins;
    using System.Text;
    using System.Globalization;
    using System.Diagnostics;

    public partial class Navigator
    {
        #region Variables
        private event AddMessage m_AddMessage;              // Messages From Navigator
        private int intTCPPort = 0;                         // TCP Port for communications with Mapfactor
        private string strIP = "127.0.0.1";                 // Default IP port
        IPEndPoint ipep = null;
        Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        # endregion

        /**/ //WaitForReply not implemented yet...
        private void SendCommand(string strNavigatorCommand, bool WaitForReply)
        {
            if (server.Connected == false)
            {
                //If we dont have a connection with Navigator, try and create one
                try
                {
                    //Setup the telnet connection                

                    ipep = new IPEndPoint(IPAddress.Parse(strIP), intTCPPort);
                    WriteLog("IPEndPoint created");
                    server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    WriteLog("Socket created");
                }
                catch { WriteLog("Failed to create socket"); }
                
                try
                {
                    //Configure the connection
                    server.Blocking = false;
                    AsyncCallback onconnect = new AsyncCallback(OnConnect);
                    server.BeginConnect(ipep, onconnect, server);
                    WriteLog("Trying to establish connection");

                    //Get some basic information about Navigator
                    string strTmp = "$protocol_version\r\n";
                    WriteLog("Sending '" + strTmp + "'");
                    server.Send(Encoding.ASCII.GetBytes(strTmp));

                    strTmp = "$software_version\r\n";
                    WriteLog("Sending '" + strTmp + "'");
                    server.Send(Encoding.ASCII.GetBytes(strTmp));

                    //New message trigger
                    m_AddMessage = new AddMessage(OnAddMessage);
                    WriteLog("Message handler created");
                }
                catch { WriteLog("Failed to connect");  }
            }

            if (server.Connected)
            {
                WriteLog("Sending '" + strNavigatorCommand + "'");
                server.Send(Encoding.ASCII.GetBytes(strNavigatorCommand));
            }
            else
            {
                WriteLog("Not connected. Unable to communicate with Navigator");
            }
        }

        public bool TerminateOrphanedProcess()
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
                        WriteLog("PC_Navigator is already running. Terminating process");
                        boolTerminateOrphanedProcess = true;
                        theprocess.Kill();
                        System.Threading.Thread.Sleep(1000); // Allow the process time to terminate
                        //this.CF_systemCommand(CF_Actions.SHOWINFO, "Found PC_Navigator running. Terminating process", "AUTOHIDE");
                    }
                }
            }
            catch
            {
                WriteLog("Error getting Process information");
            }

            return boolTerminateOrphanedProcess;            
        }


        //Parse messages from Navigator
        public void OnAddMessage(string sMessage)
        {
            // Any unhandled errors in this function causes all future messages from Navigator to be lost
            try
            {
                // Thread safe operation here
                //WriteLog("Recieved from Navigator: " + sMessage);

                //Split on the CRLF
                string[] strParse = sMessage.ToUpper().Split(new string[] { "\r\n" }, StringSplitOptions.None);

                //This is messy as there's no "standard" way Navigator provides the messages
                foreach (string strCommands in strParse)
                {
                    if (strCommands.Contains("SOUND"))
                    {
                        if (CF_getConfigFlag(CF_ConfigFlags.AttMute))
                        {
                            WriteLog("Mute (ATT) CF Audio. Start Timer");
                            //Changed as per Louk's suggestion
                            //CF_systemCommand(CF_Actions.PLAYPAUSE);
                            CF_systemCommand(CF_Actions.MUTE);
                            muteCFTimer.Enabled = true;
                        }
                        else WriteLog("CF ATT not enabled");
                    }
                    else if (strCommands.Contains("WAYPOINT") || strCommands.Contains("RECALCULATING") || strCommands.Contains("LOST"))
                    {
                        if (this.Visible == true)
                        {
                            WriteLog("Waypoint reached, recalculating or lost. Do nothing as plugin is visible: '" + strCommands + "'");
                        }
                        else
                        {
                            WriteLog("Waypoint reached, recalculating or lost: '" + strCommands + "'");
                            this.CF_systemCommand(CF_Actions.SHOWINFO, strCommands, "AUTOHIDE");
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
                            catch { WriteLog("Failed to convert LockedSatellites"); }
                            try { _currentPosition.Altitude = double.Parse(ggaData[9], CultureInfo.InvariantCulture); }
                            catch { WriteLog("Failed to convert Altitude"); }
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
                        //WriteLog("Ack message... for which command is not known....");
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
                                if (intTimeToDestination < 30) this.CF_systemCommand(CF_Actions.SHOWINFO, "Arriving at destination", "AUTOHIDE");
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
                    string sRecieved = Encoding.ASCII.GetString(m_byBuff, 0, nBytesRec);

                    // WARNING : The following line is NOT thread safe. Invoke is m_lbRecievedData.Items.Add( sRecieved );
                    Invoke(m_AddMessage, new string[] { sRecieved });

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
                WriteLog("Unusual error druing Recieve!"); 
            }
        }
    }
}
