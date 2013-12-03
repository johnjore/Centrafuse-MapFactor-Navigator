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
                    //Use user selectable unmute delay
                    muteCFTimer.Interval = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/AUDIODELAYAFTERMUTE"));
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

            if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/MUTEUNMUTESTATUS")) == false)    //LK, 30-nov-2013: Distinguish between real and simulated mutes
            {
                //Mute/unmute
                //LK, 24-nov-2013: Added UnMuteAudio if ATT mode is not enabled
                //JJ: This prevents users from configuring "No action" if Navigator speaks while music plays...
                /**/
                if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                    CF_systemCommand(CF_Actions.DISABLEATT);
                else
                    CF_systemCommand(CF_Actions.UNMUTEAUDIO);
            }
            else
            {
                //Mute/Unmute
                if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/MUTEUNMUTESTATUS")) == true)
                {
                    //Restore CF volume level
                    WriteLog("Current CF Volume Level: " + GetVolume().ToString());
                    SetVolume(intCFVolumeLevel);
                    WriteLog("New CF Volume Level: " + GetVolume().ToString());
                }
            }

            boolInMutePeriod = false; // No longer in mute phase
            muteCFTimer.Enabled = false; //Turn off timer until next time
        }


        //Called when Navigator Speaks (Either SOUND via TCP or messages via Louk's named pipe
        private void NavigatorStopCFAudio()
        {
            //We're in a MUTE period
            this.BeginInvoke(new MethodInvoker(delegate { boolInMutePeriod = true; }));

            if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/MUTEUNMUTESTATUS")) == false)    //LK, 30-nov-2013: Distinguish between real and simulated mutes
            {
                //ATT Mute/unmute
                if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                {
                    WriteLog("Mute (GPS ATT) CF Audio");
                    CF_systemCommand(CF_Actions.ATT);

                }
                else
                {
                    WriteLog("CF GPS ATT not enabled");
                    //LK, 24-nov-2013: If not in AttMode, use MuteAudio to stop the audio source
                    //JJ: This prevents users from configuring "No action" if Navigator speaks while music plays...
                    /**/
                    CF_systemCommand(CF_Actions.MUTEAUDIO);
                }

                //When not in named pipe mode, use an estimated fixed time to mute the audio source
                //Can't enable timer in a non-UI thread. Only start timer if not using named pipe
                if (!boolNamedPipes) this.BeginInvoke(new MethodInvoker(delegate { muteCFTimer.Interval = muteCFTimerInterval; muteCFTimer.Enabled = true; })); //LK,30-nov-2013: Timer interval cached in LoadSettings()
            }
            else
            {
                // Use simulated (ATT) mutes to be used when an audio source does not support proper (ATT) mute functions
                WriteLog("CF Fake MuteUnmute Enabled.");
                
                //Get current CF volume level
                intCFVolumeLevel = GetVolume();
                WriteLog("Current CF Volume Level: " + intCFVolumeLevel.ToString());

                //Set to ATTMuteLevel   //LK, 30-nov-2013: select ATT mute / normal mute
                //JJ: This prevents users from configuring "No action" if Navigator speaks while music plays...
                /**/
                if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                {
                    SetVolume(Convert.ToInt32(Math.Round(double.Parse(CF_getConfigSetting(CF_ConfigSettings.AttMuteLevel)) / 10, MidpointRounding.AwayFromZero)));
                    WriteLog("New CF Volume Level: " + GetVolume().ToString());
                }
                else
                    CF_systemCommand(CF_Actions.MUTEAUDIO);

                //When not in named pipe mode, use an estimated fixed time to mute the audio source
                //Can't enable timer in a non-UI thread. Only start timer if not using named pipe
                if (!boolNamedPipes) this.BeginInvoke(new MethodInvoker(delegate { muteCFTimer.Interval = muteCFTimerInterval; muteCFTimer.Enabled = true; })); //LK, 30-nov-2013: Timer interval cached in LoadSettings()
            }
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
