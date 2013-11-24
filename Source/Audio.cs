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
 * All functions related to audio and Navigator
*/

using System;
using System.Windows.Forms;
using centrafuse.Plugins;
using System.Runtime.InteropServices;

namespace Navigator
{
    public partial class Navigator
    {
        //Audio
        [DllImport("winmm.dll")]
        private static extern int waveOutGetVolume(IntPtr hwo, out uint dwVolume);

        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);


        //Action the message from the named pipe
        void namedPipeMessageReceived(PipeServer.Server.Client client, string message)
        {
            //Note: 'message' is not \0 terminated!
            WriteLog("boolInMutePeriod: " + boolInMutePeriod.ToString());

            if (message.Contains("Un"))
            {
                WriteLog("Named pipe received the 'unmute' message");

                //Only unmute if we've mute'ed
                if (boolInMutePeriod == true)
                {
                    //Lets try and unmute in x ms
                    muteCFTimer.Interval = 1000;
                    muteCFTimer.Enabled = true;
                }
            }
            else
            {
                WriteLog("Named pipe received the 'mute' message");

                //If timer is already enabled, mute has been sent. Make it stop!
                muteCFTimer.Stop();
                muteCFTimer.Enabled = false; //Stop the timer, Navigator spoke...
                if (boolInMutePeriod == false)
                {
                    NavigatorStopCFAudio();
                }
            }
        }

        // Event to get CF to play audio again
        private void muteCFTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("Timer over. Play Audio");

            //Mute/unmute
            if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute)) CF_systemCommand(CF_Actions.UNMUTE);

            //Mute/Unmute
            if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/MUTEUNMUTESTATUS")) == true)
            {
                //Restore CF volume level
                WriteLog("Current CF Volume Level: " + GetVolume().ToString());
                SetVolume(intCFVolumeLevel);
                WriteLog("New CF Volume Level: " + GetVolume().ToString());
            }

            boolInMutePeriod = false; // No longer in mute phase
            muteCFTimer.Enabled = false; //Turn off timer until next time
        }


        //Called when Navigator Speaks (Either SOUND via TCP or messages via Louk's named pipe
        private void NavigatorStopCFAudio()
        {
            //We're in a MUTE period
            this.BeginInvoke(new MethodInvoker(delegate { boolInMutePeriod = true; }));

            //ATT Mute/unmute
            if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
            {
                WriteLog("Mute (GPS ATT) CF Audio");
                CF_systemCommand(CF_Actions.ATT);

                //Can't enable timer in a non-UI thread. Only start timer if not using named pipe
                if (!boolNamedPipes) this.BeginInvoke(new MethodInvoker(delegate { muteCFTimer.Interval = 2500; muteCFTimer.Enabled = true; }));
            }
            else WriteLog("CF GPS ATT not enabled");

            //Mute/Unmute
            if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/MUTEUNMUTESTATUS")) == true)
            {
                WriteLog("MuteUnmute Enabled.");

                //Fake "ATT" mute CF
                //Get current CF volume level
                intCFVolumeLevel = GetVolume();
                WriteLog("Current CF Volume Level: " + intCFVolumeLevel.ToString());
                //Set to ATTMuteLevel
                SetVolume(Convert.ToInt32(Math.Round(double.Parse(CF_getConfigSetting(CF_ConfigSettings.AttMuteLevel)) / 10, MidpointRounding.AwayFromZero)));
                WriteLog("New CF Volume Level: " + GetVolume().ToString());

                //Can't enable timer in a non-UI thread. Only start timer if not using named pipe
                if (!boolNamedPipes) this.BeginInvoke(new MethodInvoker(delegate { muteCFTimer.Interval = 2500; muteCFTimer.Enabled = true; }));
            }
            else WriteLog("MuteUnmute not enabled");
        }


        //Get current volume
        public static int GetVolume()
        {
            uint CurrVol = 0;
            waveOutGetVolume(IntPtr.Zero, out CurrVol);
            ushort CalcVol = (ushort)(CurrVol & 0x0000ffff);
            int volume = CalcVol / (ushort.MaxValue / 10);
            return volume;
        }

        //Set volume
        public static void SetVolume(int volume)
        {
            int NewVolume = ((ushort.MaxValue / 10) * volume);
            uint NewVolumeAllChannels = (((uint)NewVolume & 0x0000ffff) | ((uint)NewVolume << 16));
            waveOutSetVolume(IntPtr.Zero, NewVolumeAllChannels);
        }
    }
}
