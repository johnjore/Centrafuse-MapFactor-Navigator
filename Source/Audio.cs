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
                    muteCFTimer.Interval = muteCFTimerInterval;
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

            //If MediaPlayer is active, it uses BASS and can process native CF commands correctly
            if (ReadCFValue("SETTINGS/CURRENT/MUSICMODE", "1", settingsPath) || (boolUseCFMixerforATT))
            {
                //ATT Mute/UnMute
                if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                {
                    WriteLog("UnMute (GPS ATT) CF Audio, using DISABLEATT");
                    CF_systemCommand(CF_Actions.DISABLEATT);
                }
                //Don't use ATT mute/unmute, but use MUTE/UnMute
                else
                {
                    WriteLog("CF GPS ATT not enabled, using UNMUTEAUDIO");
                    CF_systemCommand(CF_Actions.UNMUTEAUDIO);
                }
            }
            //3rd party plugin is active. Can't use CF_Actions.ATT
            else if (ReadCFValue("SETTINGS/CURRENT/MUSICMODE", "3", settingsPath))
            {
                //Raise an event and hope for the best
                //ATT Mute/unmute
                if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                {
                    WriteLog("Raising Event: " + PluginName.ToUpper() + " AUDIO DISABLEATT");
                    CF3_raisePluginEvent(new CFPluginEventArgs(PluginName.ToUpper(), "AUDIO", "DISABLEATT"));
                }
                //Don't use ATT mute/unmute, but use Mute/UnMute
                else
                {
                    WriteLog("Raising Event: " + PluginName.ToUpper() + " AUDIO UNMUTE");
                    CF3_raisePluginEvent(new CFPluginEventArgs(PluginName.ToUpper(), "AUDIO", "UNMUTE"));
                }

                //Has user enabled CF fake Mute/Unmute?
                if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/MUTEUNMUTESTATUS")) == true)
                {
                    // Use simulated (ATT) mutes to be used when an audio source does not support proper (ATT) mute functions
                    WriteLog("CF Fake MuteUnmute Enabled.");

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

            
            //If MediaPlayer is active, it uses BASS and can process native CF commands correctly
            if (ReadCFValue("SETTINGS/CURRENT/MUSICMODE", "1", settingsPath) || (boolUseCFMixerforATT))
            {
                //ATT Mute/unmute
                if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                {
                    WriteLog("Mute (GPS ATT) CF Audio");
                    CF_systemCommand(CF_Actions.ATT);
                }
                //Don't use ATT mute/unmute, but use MUTE
                else
                {
                    WriteLog("CF GPS ATT not enabled");
                    CF_systemCommand(CF_Actions.MUTEAUDIO);
                }
            }
            //3rd party plugin is active. Can't use CF_Actions.ATT
            //this else is redundant as musicmode can only be 1, 2 or 3. Keep 2 here as that's radios and they behave like plugins
            else if (ReadCFValue("SETTINGS/CURRENT/MUSICMODE", "3", settingsPath) || ReadCFValue("SETTINGS/CURRENT/MUSICMODE", "2", settingsPath))
            {
                //Raise an event and hope for the best
                //ATT Mute/unmute
                if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                {
                    CF3_raisePluginEvent(new CFPluginEventArgs(PluginName.ToUpper(), "AUDIO", "ATT"));
                }
                //Don't use ATT mute/unmute, but use MUTE
                else
                {
                    CF3_raisePluginEvent(new CFPluginEventArgs(PluginName.ToUpper(), "AUDIO", "MUTE"));
                }

                //Has user enabled CF fake Mute/Unmute?
                if (bool.Parse(this.pluginConfig.ReadField("/APPCONFIG/MUTEUNMUTESTATUS")) == true)
                {
                    // Use simulated (ATT) mutes to be used when an audio source does not support proper (ATT) mute functions
                    WriteLog("CF Fake MuteUnmute Enabled.");
                
                    //Get current CF volume level
                    intCFVolumeLevel = GetVolume();
                    WriteLog("Current CF Volume Level: " + intCFVolumeLevel.ToString());

                    //ATT Mute/unmute
                    if (CF_getConfigFlag(CF_ConfigFlags.GPSAttMute))
                    {
                        SetVolume(Convert.ToInt32(Math.Round(double.Parse(CF_getConfigSetting(CF_ConfigSettings.AttMuteLevel)) / 10, MidpointRounding.AwayFromZero)));
                        WriteLog("New CF Volume Level: " + GetVolume().ToString());
                    }
                    //Don't use ATT mute/unmute, but use MUTE
                    else
                    {
                        CF_systemCommand(CF_Actions.MUTEAUDIO);
                    }
                }
            }

            //When not in named pipe mode, use an estimated fixed time to mute the audio source
            //Can't enable timer in a non-UI thread. Only start timer if not using named pipe
            if (!boolNamedPipes) this.BeginInvoke(new MethodInvoker(delegate { muteCFTimer.Interval = muteCFTimerInterval; muteCFTimer.Enabled = true; })); 
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
