/*
 * Copyright 2013, 2014, Louk
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
 * All functions related to the Named Pipe Server
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using System.IO;
using System.Diagnostics;
using centrafuse.Plugins;

namespace Navigator
{
    public partial class Navigator
    {
        //Configure and enable named pipe, if enabled by user
        public void SetupNamedPipe()
        {
            //Using named pipe? 
            if (boolNamedPipes)
            {
                patchNavigator();

                //Louk's Named pipe server
                try
                {
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
                }
                catch (Exception errMsg) { WriteLog("Failed to check pipeServer: " + errMsg.Message); }
            }
            else
                unpatchNavigator();
        }

        //From Louk. Used by patched Navigator for mute/unmute instructions
        private PipeServer.Server pipeServer = new PipeServer.Server();

        //Louk's Named pipe received a message
        private void pipeServer_MessageReceived(PipeServer.Server.Client client, string message)
        {
            this.Invoke(new PipeServer.Server.MessageReceivedHandler(namedPipeMessageReceived), new object[] { client, message });
        }

        //Patch navigator to support named pipe
        private void patchNavigator()
        {
            WriteLog("Checking whether " + EXEName + " is patched for Named Pipe Support or not. Desired state: Patched");

            //Patch or UnPatch PC_Navigator.exe for named pipe support
            string strPatchedFile = strEXEPath + "\\" + EXEName;
            string strBackupFile = strEXEPath + "\\" + EXEName + ".orig";
            FileInfo fiUnPatched = new FileInfo(strBackupFile);
            FileInfo fiPatched = new FileInfo(strPatchedFile);

            if (fiUnPatched.Exists)
            {
                WriteLog("Patch already applied. Nothing to do.");
            }
            else
            {
                try
                {
                    //Create backup file first
                    System.IO.File.Copy(strPatchedFile, strBackupFile);

                    //Copy the extra dll
                    System.IO.File.Copy(@PluginPath + "\\NamedPipePatch\\CF_NP.dll", strEXEPath + "\\CF_NP.dll");

                    //Patch the Executable
                    Process pPatch = new Process();
                    pPatch.StartInfo.FileName = PluginPath + "\\NamedPipePatch\\binmay.exe";
                    pPatch.StartInfo.Arguments = "-i \"" + strBackupFile + "\" -o \"" + strPatchedFile + "\" -s \"57494e4d4d\" -r \"43465f4e50\"";
                    WriteLog(pPatch.StartInfo.FileName);
                    WriteLog(pPatch.StartInfo.Arguments);
                    pPatch.Start();
                    pPatch.WaitForExit();
                    if (pPatch.ExitCode != 0) WriteLog("Failed to patch " + EXEName + ", return code: " + pPatch.ExitCode.ToString());
                }
                catch (Exception errMsg)
                {
                    WriteLog("Failed to apply named pipe patch: " + errMsg.Message);    //LK, 24-nov-2013: Added reason for failure
                };
            }
        }

        //UnPatch navigator and remove name pipe support
        private void unpatchNavigator()
        {
            WriteLog("Checking whether " + EXEName + " is patched for Named Pipe Support or not. Desired state: UnPatched");

            //Patch or UnPatch PC_Navigator.exe for named pipe support
            string strPatchedFile = strEXEPath + "\\" + EXEName;
            string strBackupFile = strEXEPath + "\\" + EXEName + ".orig";
            FileInfo fiUnPatched = new FileInfo(strBackupFile);

            if (fiUnPatched.Exists)
            {
                //Put the exe files back...
                try { System.IO.File.Copy(strBackupFile, strPatchedFile, true); }
                catch (Exception errMsg) { WriteLog("Failed to undo named pipe patch (on restore original exe): " + errMsg.Message); }

                //Delete the extra dll
                try { System.IO.File.Delete(strEXEPath + "\\CF_NP.dll"); }
                catch (Exception errMsg) { WriteLog("Failed to undo named pipe patch (on delete helper DLL): " + errMsg.Message); }

                //Delete the extra EXE
                try { System.IO.File.Delete(strBackupFile); }
                catch (Exception errMsg) { WriteLog("Failed to undo named pipe patch (on delete backup file): " + errMsg.Message); }
            }
            else
            {
                WriteLog(EXEName + " is not patched, so nothing to do");
            }
        }
    }
}

namespace PipeServer
{
    class Server
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateNamedPipe(
           String pipeName,
           uint dwOpenMode,
           uint dwPipeMode,
           uint nMaxInstances,
           uint nOutBufferSize,
           uint nInBufferSize,
           uint nDefaultTimeOut,
           IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int ConnectNamedPipe(
           SafeFileHandle hNamedPipe,
           IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DisconnectNamedPipe(
            SafeFileHandle hNamedPipe);

        public const uint DUPLEX = (0x00000003);
        public const uint FILE_FLAG_OVERLAPPED = (0x40000000);

        public class Client
        {
            public SafeFileHandle handle;
            public FileStream stream;
        }

        public delegate void MessageReceivedHandler(Client client, string message);

        public event MessageReceivedHandler MessageReceived;
        public const int BUFFER_SIZE = 4096;

        string pipeName;
        Thread listenThread;
        bool running;
        List<Client> clients;

        public string PipeName
        {
            get { return this.pipeName; }
            set { this.pipeName = value; }
        }

        public bool Running
        {
            get { return this.running; }
        }

        public Server()
        {
            this.clients = new List<Client>();
        }

        /// <summary>
        /// Starts the pipe server
        /// </summary>
        public void Start()
        {
            //start the listening thread
            this.listenThread = new Thread(new ThreadStart(ListenForClients));
            this.listenThread.Start();

            this.running = true;
        }

        /// <summary>
        /// Stops the pipe server
        /// </summary>
        public void Stop()
        {
            foreach (Client client in clients)
            {
                if (client.stream != null)
                    client.stream.Close();
                if (client.handle != null)
                    if (!client.handle.IsClosed)
                        DisconnectNamedPipe(client.handle);
            }
            //stop the listening thread
            if (this.listenThread != null)
            {
                this.listenThread.Abort();
                this.listenThread = null;
            }

            this.running = false;
        }

        /// <summary>
        /// Listens for client connections
        /// </summary>
        private void ListenForClients()
        {
            try
            {
                while (true)
                {
                    SafeFileHandle clientHandle =
                    CreateNamedPipe(
                         this.pipeName,
                         DUPLEX | FILE_FLAG_OVERLAPPED,
                         0,
                         255,
                         BUFFER_SIZE,
                         BUFFER_SIZE,
                         0,
                         IntPtr.Zero);

                    //could not create named pipe
                    if (clientHandle.IsInvalid)
                        return;

                    Client client = new Client();
                    client.handle = clientHandle;

                    lock (clients)
                        this.clients.Add(client);

                    int success = ConnectNamedPipe(clientHandle, IntPtr.Zero);

                    //could not connect client
                    if (success == 0)
                        return;

                    Thread readThread = new Thread(new ParameterizedThreadStart(Read));
                    readThread.Start(client);
                }
            }
            catch (Exception errMsg) { CFTools.writeLog("Unable to listen for clients: " + errMsg.Message); }
        }

        /// <summary>
        /// Reads incoming data from connected clients
        /// </summary>
        /// <param name="clientObj"></param>
        private void Read(object clientObj)
        {
            try
            {
                Client client = (Client)clientObj;
                client.stream = new FileStream(client.handle, FileAccess.ReadWrite, BUFFER_SIZE, true);
                byte[] buffer = new byte[BUFFER_SIZE];
                ASCIIEncoding encoder = new ASCIIEncoding();

                while (true)
                {
                    int bytesRead = 0;

                    try
                    {
                        bytesRead = client.stream.Read(buffer, 0, BUFFER_SIZE);
                    }
                    catch
                    {
                        //read error has occurred
                        break;
                    }

                    //client has disconnected
                    if (bytesRead == 0)
                        break;

                    //fire message received event
                    if (this.MessageReceived != null)
                        this.MessageReceived(client, encoder.GetString(buffer, 0, bytesRead));
                }

                //clean up resources
                client.stream.Close();
                client.handle.Close();
                lock (this.clients)
                    this.clients.Remove(client);
            }
            catch (Exception errMsg) { CFTools.writeLog("Unable to read message: " + errMsg.Message); }
        }

        /// <summary>
        /// Sends a message to all connected clients
        /// </summary>
        /// <param name="message">the message to send</param>
        public void SendMessage(string message)
        {
            try
            {
                lock (this.clients)
                {
                    ASCIIEncoding encoder = new ASCIIEncoding();
                    byte[] messageBuffer = encoder.GetBytes(message);
                    foreach (Client client in this.clients)
                    {
                        if (client.stream != null)
                        {
                            client.stream.Write(messageBuffer, 0, messageBuffer.Length);
                            client.stream.Flush();
                        }
                    }
                }
            }
            catch (Exception errMsg) { CFTools.writeLog("Unable to SendMessage: " + errMsg.Message); }
        }
    }
}
