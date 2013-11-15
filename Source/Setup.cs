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

using System;
using System.Windows.Forms;
using System.Xml;
using System.Web;
using centrafuse.Plugins;

using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace Navigator
{
    internal class NavSetup : ICFInterfaceSetup
    {
        private readonly ConfigReader configReader;
        private readonly LanguageReader langReader;
        private readonly Navigator mainForm;

        // Total configuration pages for each mode
        public int nAdvancedSetupPages { get { return 2; } }
        public int nBasicSetupPages { get { return 2; } }

        #region Variables
        private const string PluginPath = @"plugins\Navigator\";
        private const string PluginPathLanguages = PluginPath + @"Languages\";
        private const string ConfigurationFile = "config.xml";
        private const string ConfigSection = "/APPCONFIG/";
        private const string LanguageSection = "/APPLANG/SETUP/";
        private const string LanguageControlSection = "/APPLANG/NAVIGATOR/";
        #endregion
                
        public NavSetup(Navigator mForm, ConfigReader config, LanguageReader lang)
        {
            mainForm = mForm;

            configReader = config;
            langReader = lang;
        }

        public void CF_setupExitSettings(bool save)
        {
            if (save)
            {
                configReader.Save();
                mainForm.LoadSettings();
            }
            else
                configReader.Reload();
        }

        public void CF_setupReadSettings(int page, bool advanced, CFSetupHandler[] ButtonHandler, string[] ButtonText, string[] ButtonValue)
        {
            try
            {
                int i = CFSetupButton.One;
                
                if (page == 1)
                {
                    // TEXT BUTTONS (1-4)
                    ButtonHandler[i] = new CFSetupHandler(SetExePath);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/EXEPATH");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/EXEPATH");

                    ButtonHandler[i] = new CFSetupHandler(SetExeParameters);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/EXEPARAMETERS");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/EXEPARAMETERS");

                    ButtonHandler[i] = new CFSetupHandler(SetTCPPort);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/TCPPORT");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/TCPPORT");

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";

                    /*ButtonHandler[i] = SetNavigatorVolume;
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/VOLUME");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/VOLUME");*/
                                                           
                    // BOOL BUTTONS (5-8)
                    ButtonHandler[i] = new CFSetupHandler(SetLogEvents);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/LOGEVENTS");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/LOGEVENTS");

                    ButtonHandler[i] = new CFSetupHandler(SetEdition);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/FREEEDITION");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/FREEEDITION");

                    ButtonHandler[i] = new CFSetupHandler(AcceptedOSM);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/OSMOK");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/OSMOK");

                    ButtonHandler[i] = new CFSetupHandler(SetAlertStatus);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/GETALERTSTATUS");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/ALERTSENABLED");                    
                }
                else if (page == 2)
                {
                    // TEXT BUTTONS (1-4)
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";

                    // BOOL BUTTONS (5-8)
                    ButtonHandler[i] = new CFSetupHandler(SetNamedPipeStatus);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/NAMEDPIPE");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/NAMEDPIPE");

                    ButtonHandler[i] = new CFSetupHandler(SetPausePlayStatus);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/PAUSEPLAYSTATUS");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/PAUSEPLAYSTATUS");

                    ButtonHandler[i] = new CFSetupHandler(SetNotificationStatus);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/NOTIFICATIONSTATUS");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/NOTIFICATIONSTATUS");

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                }

            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        public void CF_setupReloadSharedSettings()
        {
            this.mainForm.LoadSettings();
        }

        public int numAdvancedSetupPages
        {
            get { return nAdvancedSetupPages; }
        }

        public int numBasicSetupPages
        {
            get { return nBasicSetupPages; }
        }

#region User Input Events

        //Folder with exe
        private void SetExePath(ref object value)
        {
            try
            {
                string location = this.configReader.ReadField("/APPCONFIG/EXEPATH");
                if (string.IsNullOrEmpty(location)) location = PluginPath;

                CFDialogParams dialogParams = new CFDialogParams("Select the folder with PC_Navigator.exe", location);
                dialogParams.browseable = true;
                dialogParams.enablesubactions = true;
                dialogParams.showfiles = false;

                CFDialogResults results = new CFDialogResults();
                if (mainForm.CF_displayDialog(CF_Dialogs.FileBrowser, dialogParams, results) == DialogResult.OK)
                {
                    string newPath = results.resultvalue;
                    this.configReader.WriteField("/APPCONFIG/EXEPATH", newPath);
                    value = newPath;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        //Extra parameters
        private void SetExeParameters(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Display OSK for user to type display name
                if (mainForm.CF_systemDisplayDialog(CF_Dialogs.OSK, this.langReader.ReadField("/APPLANG/SETUP/EXEPARAMTERS"), (string)value, null, out resultvalue, out resulttext, out tempobject, null, true, true, true, true, false, false, 1) == DialogResult.OK)
                {
                    this.configReader.WriteField("/APPCONFIG/EXEPARAMETERS", resultvalue);

                    // Display new value on Settings Screen button
                    value = resultvalue;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }
        
        //Port to use for communications with Navigator
        private void SetTCPPort(ref object value)
        {
            try
            {
                string resultvalue, resulttext;

                if (mainForm.CF_systemDisplayDialog(CF_Dialogs.NumberPad, this.langReader.ReadField("/APPLANG/SETUP/TCPPORT"), out resultvalue, out resulttext) == DialogResult.OK)
                {
                    //Parse the value
                    int intTemp = int.Parse(resultvalue);

                    //Sanity check it and set to its extremes.
                    if (intTemp > 65536) intTemp = 0;
                    if (intTemp < 0) intTemp = 0;

                    //Value is scrubbed, write it
                    this.configReader.WriteField("/APPCONFIG/TCPPORT", intTemp.ToString());

                    // Display new value on Settings Screen button
                    value = intTemp.ToString();
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        //Log to file during run-time
        private void SetLogEvents(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/LOGEVENTS", value.ToString());
        }
        
        //Off = Licensed edition. On = Free edition. Dictates which IDC file is used at launch
        private void SetEdition(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/FREEEDITION", value.ToString());
        }

        //If on, supresses OSM OK box
        private void AcceptedOSM(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/OSMOK", value.ToString());
        }

        //Enable alert status when Navigator is NOT active plugin?
        private void SetAlertStatus(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/ALERTSENABLED", value.ToString());
        }

        //Enable Sending Pause/Play on Sound alert?
        private void SetPausePlayStatus(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/PAUSEPLAYSTATUS", value.ToString());
        }

        //Enable Sending Notification Message on Sound alert?
        private void SetNotificationStatus(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/NOTIFICATIONSTATUS", value.ToString());
        }

        //Enable Louk's message handler
        private void SetNamedPipeStatus(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/NAMEDPIPE", value.ToString());
        }
        
#endregion

    }
}
