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

namespace Navigator
{
    [System.ComponentModel.DesignerCategory("Code")]
    public class Setup : CFSetup
    {

#region Variables
        private const string PluginPath = @"plugins\Navigator\";
        private const string PluginPathLanguages = PluginPath + @"Languages\";
        private const string ConfigurationFile = "config.xml";
        private const string ConfigSection = "/APPCONFIG/";
        private const string LanguageSection = "/APPLANG/SETUP/";
        private const string LanguageControlSection = "/APPLANG/Navigator/";
#endregion

#region Construction

        // The setup constructor will be called each time this plugin's setup is opened from the CF Setting Page
        // This setup is opened as a dialog from the CF_pluginShowSetup() call into the main plugin application form.
        public Setup(ICFMain mForm, ConfigReader config, LanguageReader lang)
        {
            // Total configuration pages for each mode
            const sbyte NormalTotalPages = 1;
            const sbyte AdvancedTotalPages = 1;

            // MainForm must be set before calling any Centrafuse API functions
            this.MainForm = mForm;

            // pluginConfig and pluginLang should be set before calling CF_initSetup() so this CFSetup instance 
            // will internally save any changed settings.
            this.pluginConfig = config;
            this.pluginLang = lang;

            // When CF_initSetup() is called, the CFPlugin layer will call back into CF_setupReadSettings() to read the page
            // Note that this.pluginConfig and this.pluginLang must be set before making this call
            CF_initSetup(NormalTotalPages, AdvancedTotalPages);

            // Update the Settings page title
            this.CF_updateText("TITLE", this.pluginLang.ReadField("/APPLANG/SETUP/TITLE"));
        }

#endregion

#region CFSetup

        public override void CF_setupReadSettings(int currentpage, bool advanced)
        {
            /*
            * Number of configuration pages is defined in two constsants in Setup(...)
            * const sbyte NormalTotalPages = ;
            * const sbyte AdvancedTotalPages = ;
            */

            try
            {
                int i = CFSetupButton.One;

                if (currentpage == 1)
                {
                    // TEXT BUTTONS (1-4)
                    ButtonHandler[i] = new CFSetupHandler(SetExePath);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/EXEPATH");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/EXEPATH");

                    ButtonHandler[i] = new CFSetupHandler(SetExeParameters);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/EXEPARAMETERS");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/EXEPARAMETERS");

                    ButtonHandler[i] = new CFSetupHandler(SetTCPPort);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/TCPPORT");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/TCPPORT");

                    ButtonHandler[i] = new CFSetupHandler(SetNavigatorVolume);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/VOLUME");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/VOLUME");

                                                           
                    // BOOL BUTTONS (5-8)
                    ButtonHandler[i] = new CFSetupHandler(SetLogEvents);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/LOGEVENTS");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/LOGEVENTS");

                    ButtonHandler[i] = new CFSetupHandler(SetEdition);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/FREEEDITION");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/FREEEDITION");

                    ButtonHandler[i] = new CFSetupHandler(AcceptedOSM);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/OSMOK");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/OSMOK");

                    ButtonHandler[i] = new CFSetupHandler(SetAlertStatus);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/GETALERTSTATUS");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/ALERTSENABLED");                    
                }
                // Not required. Reading CF's ATT setting instead
                /*
                ButtonHandler[i] = new CFSetupHandler(MuteOnInstruction);
                ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/MUTE");
                ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/MUTE");
                */
                
                /*
                    ButtonHandler[i] = new CFSetupHandler(SetDisplayName);
                    ButtonText[i] = this.pluginLang.ReadField("APPLANG/SETUP/DISPLAYNAME");
                    ButtonValue[i++] = this.pluginLang.ReadField("APPLANG/NAVIGATOR/DISPLAYNAME");
                */
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

#endregion

#region User Input Events

        //Set Navigator volume level
        private void SetNavigatorVolume(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Display OSK for user to type display name
                if (this.CF_systemDisplayDialog(CF_Dialogs.OSK, this.pluginLang.ReadField("/APPLANG/SETUP/VOLUME"), ButtonValue[(int)value], null, out resultvalue, out resulttext, out tempobject, null, true, true, true, true, false, false, 1) == DialogResult.OK)
                {
                    this.pluginConfig.WriteField("/APPCONFIG/VOLUME", resultvalue);

                    // Display new value on Settings Screen button
                    ButtonValue[(int)value] = resultvalue;
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

                if (this.CF_systemDisplayDialog(CF_Dialogs.NumberPad, this.pluginLang.ReadField("/APPLANG/SETUP/TCPPORT"), out resultvalue, out resulttext) == DialogResult.OK)
                {
                    //Parse the value
                    int intTemp = int.Parse(resultvalue);

                    //Sanity check it and set to its extremes.
                    if (intTemp > 65536) intTemp = 0;
                    if (intTemp < 0) intTemp = 0;

                    //Value is scrubbed, write it
                    this.pluginConfig.WriteField("/APPCONFIG/TCPPORT", intTemp.ToString());

                    // Display new value on Settings Screen button
                    ButtonValue[(int)value] = intTemp.ToString();
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }


        //Folder with exe
        private void SetExePath(ref object value)
        {
            try
            {
                string location = GetLocation();

                CFDialogParams dialogParams = new CFDialogParams("Select the folder with MapFactor", location);
                dialogParams.browseable = true;
                dialogParams.enablesubactions = false;
                dialogParams.showfiles = false;

                CFDialogResults results = new CFDialogResults();
                if (this.CF_displayDialog(CF_Dialogs.FileBrowser, dialogParams, results) == DialogResult.OK)
                {
                    string newPath = results.resultvalue;
                    this.pluginConfig.WriteField("/APPCONFIG/EXEPATH", newPath + "\\PC_Navigator.exe");
                    ButtonValue[(int)value] = newPath;
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
                if (this.CF_systemDisplayDialog(CF_Dialogs.OSK, this.pluginLang.ReadField("/APPLANG/SETUP/EXEPARAMTERS"), ButtonValue[(int)value], null, out resultvalue, out resulttext, out tempobject, null, true, true, true, true, false, false, 1) == DialogResult.OK)
                {
                    this.pluginConfig.WriteField("/APPCONFIG/EXEPARAMETERS", resultvalue);

                    // Display new value on Settings Screen button
                    ButtonValue[(int)value] = resultvalue;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }
        
        private void SetDisplayName(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Display OSK for user to type display name
                if (this.CF_systemDisplayDialog(CF_Dialogs.OSK, this.pluginLang.ReadField("/APPLANG/SETUP/DISPLAYNAME"), ButtonValue[(int)value], null, out resultvalue, out resulttext, out tempobject, null, true, true, true, true, false, false, 1) == DialogResult.OK)
                {
                    // save user value, note this does not save to file yet, as this should only be done when user confirms settings
                    // being overwritten when they click the "Save" button.  Saving is done internally by the CFSetup instance if
                    // pluginConfig and pluginLang were properly set before callin CF_initSetup().
                    this.pluginLang.WriteField("/APPLANG/NAVIGATOR/DISPLAYNAME", resultvalue);

                    // Display new value on Settings Screen button
                    ButtonValue[(int)value] = resultvalue;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        //Log to file during run-time
        private void SetLogEvents(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/LOGEVENTS", value.ToString());
        }

        //Enable alert status when Navigator is NOT active plugin?
        private void SetAlertStatus(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/ALERTSENABLED", value.ToString());
        }

        //Off = Licensed edition. On = Free edition. Dictates which IDC file is used at launch
        private void SetEdition(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/FREEEDITION", value.ToString());
        }

        //If on, supresses OSM OK box
        private void AcceptedOSM(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/OSMOK", value.ToString());
        }
        
        //If on, mutes audio on instructions
        private void MuteOnInstruction(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/MUTE", value.ToString());
        }

        //Helper function for selecting folder with EXE
        private string GetLocation()
        {
            string location = this.pluginConfig.ReadField("/APPCONFIG/EXEPATH");
            if (string.IsNullOrEmpty(location))
                location = PluginPath;
            return location;
        }

#endregion

    }
}
