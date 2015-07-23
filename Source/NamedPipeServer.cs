/*
 * Copyright 2013, 2014, 2015 Louk and John Jore
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
        //JJ: byte arrays for patched or unpatched
        byte[] unpatched = { 0x57, 0x49, 0x4e, 0x4d, 0x4d };
        byte[] patched = { 0x43, 0x46, 0x5f, 0x4e, 0x50 };

        //Configure and enable named pipe, if enabled by user
        public bool SetupNamedPipe()
        {
            //Using named pipe? 
            if (boolNamedPipes)
            {
                //Did we succeed in patching Navigator for named pipe support?
                if (!patchNavigator())
                {
                    CF_systemDisplayDialog(CF_Dialogs.OkBox, pluginLang.ReadField("/APPLANG/NAVIGATOR/NAMEDPIPEPATCHFAILED"));
                    return false;
                }
                else
                    WriteLog("Patch successfully applied or already patched");

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
                catch (Exception errMsg) 
                {
                    WriteLog("Failed to check pipeServer: " + errMsg.Message);
                    return false;
                }
            }
            else
            {
                //Did we succeed in patching Navigator for named pipe support?
                if (!unpatchNavigator())
                {                    
                    CF_systemDisplayDialog(CF_Dialogs.OkBox, pluginLang.ReadField("/APPLANG/NAVIGATOR/NAMEDPIPEPATCHFAILED"));
                    return false;
                }
                else
                    WriteLog("Patch successfully removed or already removed");
            }

            //We made it this far, no issues
            return true;
        }

        //From Louk. Used by patched Navigator for mute/unmute instructions
        private PipeServer.Server pipeServer = new PipeServer.Server();

        //Louk's Named pipe received a message
        private void pipeServer_MessageReceived(PipeServer.Server.Client client, string message)
        {
            this.Invoke(new PipeServer.Server.MessageReceivedHandler(namedPipeMessageReceived), new object[] { client, message });
        }

        //JJ: Search for a byte[] pattern within a byte[]
        private int ByteMatch(ref byte[] Source, ref byte[] Match)
        {
            for (int i=0; i < Source.Length; i++)
            {
                bool Matched = false;
                if (Source[i] == Match[0])
                {
                    Matched = true; //Match found on first byte[] in Source. Are the rest a match too?
                    for (int j=1; j < Match.Length; j++)
                    {
                        //WriteLog("Potential");
                        if (Match[j] != Source[i + j])
                        {
                            //No match, set flag as such and break out
                            Matched = false;
                            break;
                        }                       
                    }

                    //If we made it this far, we've compared all byte[] in Match, and still good. Tell the world!
                    if (Matched)
                    {
                        WriteLog("Looped all, and still true. First match @ 0x" + i.ToString("X"));
                        return i;
                    }
                }
            }
         
            return -1;
        }

        //Patch navigator to support named pipe
        private bool patchNavigator()
        {
            WriteLog("Checking whether " + EXEName + " is patched for Named Pipe Support or not. Desired state: Patched");

            //Patch or UnPatch PC_Navigator.exe for named pipe support
            string strExeFile = strEXEPath + "\\" + EXEName;
            string strBackupFile = strEXEPath + "\\" + EXEName + ".orig";
            string strDLLFile = strEXEPath + "\\CF_NP.dll";
            FileInfo fiExe = new FileInfo(strExeFile);
            FileInfo fiUnPatched = new FileInfo(strBackupFile);
            FileInfo fiDLL = new FileInfo(strDLLFile);
            
            //Check if file to patch exists (PC_Navigator.exe)
            if (fiExe.Exists)
            {
                //Read the file
                byte[] bPatchedFile = File.ReadAllBytes(strExeFile);

                //-1 if not found
                if (ByteMatch(ref bPatchedFile, ref patched) != -1)
                {
                    //Patch found
                    WriteLog("Patch applied. No further action required");
                    return true;
                }
                else
                {
                    //Patch not found, need to modify the file

                    //Does a backup file exist?
                    if (fiUnPatched.Exists == false)
                    {
                        //Create backup file first
                        try
                        {
                            System.IO.File.Copy(strExeFile, strBackupFile);
                        }
                        catch (Exception errMsg)
                        {
                            WriteLog("Failed to create backup file : " + errMsg.Message);
                            return false;
                        };
                    }
                        
                    //Copy the extra dll
                    try
                    {
                        if (!fiDLL.Exists) System.IO.File.Copy(@PluginPath + "\\NamedPipePatch\\CF_NP.dll", strEXEPath + "\\CF_NP.dll");
                    }
                    catch (Exception errMsg)
                    {
                        WriteLog("Failed to copy helper DLL : " + errMsg.Message);
                        return false;
                    };

                    //Apply patch
                    try
                    {
                        BinaryWriter bw = new BinaryWriter(File.Open(strExeFile, FileMode.Open, FileAccess.ReadWrite));
                        bw.BaseStream.Seek(ByteMatch(ref bPatchedFile, ref unpatched), SeekOrigin.Begin);
                        bw.Write(patched);
                        bw.Close();                        
                    }
                    catch (Exception errMsg)
                    {
                        WriteLog("Failed to apply patch : " + errMsg.Message);
                        return false;
                    };

                    //We made it this far. Patch applied successfully
                    return true;
                }
            }
            else
            {
                WriteLog("File to patch does not exist. Can't proceed");
                return false;
            }
        }

        //UnPatch navigator and remove name pipe support
        private bool unpatchNavigator()
        {
            WriteLog("Checking whether " + EXEName + " is patched for Named Pipe Support or not. Desired state: UnPatched");

            //Remove named pipe support from PC_Navigator.exe
            string strExeFile = strEXEPath + "\\" + EXEName;
            string strBackupFile = strEXEPath + "\\" + EXEName + ".orig";
            string strDLLFile = strEXEPath + "\\CF_NP.dll";
            FileInfo fiExe = new FileInfo(strExeFile);
            FileInfo fiBackup = new FileInfo(strBackupFile);
            FileInfo fiDLL = new FileInfo(strDLLFile);

            //Check if file to unpatch exists (PC_Navigator.exe)
            if (fiExe.Exists)
            {
                //Read the file
                byte[] bExeFile = File.ReadAllBytes(strExeFile);

                //-1 if not found
                if (ByteMatch(ref bExeFile, ref unpatched) != -1)
                {
                    //UnPatch found
                    WriteLog("Patch not applied. No further action required");
                    return true;
                }
                else
                {
                    //Patch found, need to modify the file to remove patch

                    //Does a backup file exist?
                    if (fiBackup.Exists == false)
                    {
                        //Create backup file first
                        try
                        {
                            System.IO.File.Copy(strExeFile, strBackupFile);
                        }
                        catch (Exception errMsg)
                        {
                            WriteLog("Failed to create backup file : " + errMsg.Message);
                            return false;
                        };
                    }

                    //Find location to apply patch
                    try
                    {
                        //Remove patch
                        BinaryWriter bw = new BinaryWriter(File.Open(strExeFile, FileMode.Open, FileAccess.ReadWrite));
                        bw.BaseStream.Seek(ByteMatch(ref bExeFile, ref patched), SeekOrigin.Begin);
                        bw.Write(unpatched);
                        bw.Close();
                    }
                    catch (Exception errMsg)
                    {
                        WriteLog("Failed to remove patch : " + errMsg.Message);
                        return false;
                    };

                    //Delete the extra dll if it exists
                    if (fiDLL.Exists)
                    {
                        try
                        {
                            System.IO.File.Delete(strEXEPath + "\\CF_NP.dll");
                        }
                        catch (Exception errMsg)
                        {
                            WriteLog("Failed to remove helper DLL: " + errMsg.Message);
                            return false;
                        }
                    }
                    
                    //We made it this far. Patch removed successfully
                    return true;
                }
            }
            else
            {
                WriteLog("File to remove patch from does not exist. Can't proceed");
                return false;
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
