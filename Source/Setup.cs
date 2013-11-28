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
    internal class NavSetup : ICFInterfaceSetup
    {        
        #region Variables and consts
        //LK, 25-nov-2013: Changed to read only: Savety first
        private readonly ConfigReader configReader;
        private readonly LanguageReader langReader;
        private readonly Navigator mainForm;

        // Total configuration pages for each mode
        public int nAdvancedSetupPages { get { return 2; } }
        public int nBasicSetupPages { get { return 2; } }

        public int numAdvancedSetupPages { get { return nAdvancedSetupPages; } }
        public int numBasicSetupPages { get { return nBasicSetupPages; } }

        private const string PluginPath = @"plugins\Navigator\";
        #endregion

        #region CF functions
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

        public void CF_setupReloadSharedSettings()
        {
            this.mainForm.LoadSettings();
        }
        #endregion
        
        #region Setup items
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

                    ButtonHandler[i] = new CFSetupHandler(SetInitialWindowSize);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/WINDOWSIZE");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/WINDOWSIZE");
                                                           
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
                    ButtonHandler[i] = new CFSetupHandler(SetAudioDelayAfterMute);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/AUDIODELAYAFTERMUTE");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/AUDIODELAYAFTERMUTE");
                                        
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";

                    // BOOL BUTTONS (5-8)
                    ButtonHandler[i] = new CFSetupHandler(SetNamedPipeStatus);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/NAMEDPIPE");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/NAMEDPIPE");

                    ButtonHandler[i] = new CFSetupHandler(SetMuteUnmuteStatus);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/MUTEUNMUTESTATUS");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/MUTEUNMUTESTATUS");
                    
                    ButtonHandler[i] = new CFSetupHandler(SetNoHiRes);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/NOHIRES");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/NOHIRES");

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }
        #endregion

        #region User Input Events

        //Folder with exe
        private void SetExePath(ref object value)
        {
            try
            {
                //LK, 27-nov-2013: Implement InternalHandler to update the screen and button values
                //TODO: LK, 25-nov-2013: Needs to (re-)start the applicaton here or in LoadSettings()
                if (value.GetType().Equals(typeof(CFSetupHandlerParams)))
                {
                    string location = this.configReader.ReadField("/APPCONFIG/EXEPATH");
                    if (string.IsNullOrEmpty(location)) location = PluginPath;

                    CFDialogParams dialogParams = new CFDialogParams(this.langReader.ReadField("/APPLANG/SETUP/EXEPATH"), location);
                    dialogParams.browseable = true;
                    dialogParams.enablesubactions = true;
                    dialogParams.showfiles = false;

                    CFDialogResults results = new CFDialogResults();
                    if (mainForm.CF_displayDialog(CF_Dialogs.FileBrowser, dialogParams, results) == DialogResult.OK)
                    {
                        ((CFSetupHandlerParams)value).result.ok = true;
                        ((CFSetupHandlerParams)value).result.pobject = results.resultobject;
                        ((CFSetupHandlerParams)value).result.text = results.resulttext;
                        ((CFSetupHandlerParams)value).result.value = results.resultvalue;
                        this.configReader.WriteField("/APPCONFIG/EXEPATH", results.resultvalue);
                    }

                    ((CFSetupHandlerParams)value).requesttype = CFSetupHandlerRequest.None; //Get out of loop
                    return;
                }

                CFSetupHandlerParams internalhandler = new CFSetupHandlerParams();
                internalhandler.requesttype = CFSetupHandlerRequest.None;
                internalhandler.button = (int)value;
                internalhandler.dialogtype = CF_Dialogs.OkBox;
                internalhandler.listviewitems = null;
                internalhandler.writebutton = true;
                internalhandler.writebuttonwithvalue = true;
                internalhandler.title = this.langReader.ReadField("APPLANG/SETUP/EXEPATH");
                internalhandler.listheader = this.configReader.ReadField("/APPCONFIG/EXEPATH");
                value = internalhandler;

            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }            
        }

        /**/
        //Extra parameters
        //TODO: LK, 25-nov-2013: Needs to (re-)start the applicaton here or in LoadSettings()
        private void SetExeParameters(ref object value)
        {
            try
            {
                //LK, 27-nov-2013: Implement InternalHandler to update the screen and button values
                if (value.GetType().Equals(typeof(CFSetupHandlerParams)))
                {
                    if (((CFSetupHandlerParams)value).result.ok)
                        this.configReader.WriteField("/APPCONFIG/EXEPARAMETERS", ((CFSetupHandlerParams)value).result.value);

                    ((CFSetupHandlerParams)value).requesttype = CFSetupHandlerRequest.None; //Get out of loop
                    return;
                }

                CFSetupHandlerParams internalhandler = new CFSetupHandlerParams();
                internalhandler.requesttype = CFSetupHandlerRequest.ShowDialog;
                internalhandler.button = (int)value;
                internalhandler.dialogtype = CF_Dialogs.OSK;
                internalhandler.listviewitems = null;
                internalhandler.writebutton = true;
                internalhandler.writebuttonwithvalue = true;
                internalhandler.title = this.langReader.ReadField("APPLANG/SETUP/EXEPARAMETERS");
                internalhandler.listheader = this.configReader.ReadField("/APPCONFIG/EXEPARAMETERS");
                value = internalhandler;

            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }
        
        /**/
        //Port to use for communications with Navigator
        //TODO: LK, 25-nov-2013: Needs to (re-)open the connection here or in LoadSettings()
        private void SetTCPPort(ref object value)
        {
            try
            {
                //LK, 27-nov-2013: Implement InternalHandler to update the screen and button values
                int button;
                if (value.GetType().Equals(typeof(CFSetupHandlerParams)))
                {
                    button = ((CFSetupHandlerParams)value).button;
                    if (((CFSetupHandlerParams)value).result.ok)
                    {
                        int iTemp = 0;
                        try { iTemp = Int32.Parse(((CFSetupHandlerParams)value).result.value); }
                        catch { iTemp = -1; }

                        //Sanity check it and set to its extremes.
                        if (iTemp <= 65536 && iTemp >= 0)
                        {
                            ((CFSetupHandlerParams)value).requesttype = CFSetupHandlerRequest.None; //Get out of loop
                            this.configReader.WriteField("/APPCONFIG/TCPPORT", iTemp.ToString());
                            return;
                        }
                    }
                    else
                    {
                        //+++???((CFSetupHandlerParams)value).result.text = this.configReader.ReadField("/APPCONFIG/TCPPORT");
                        ((CFSetupHandlerParams)value).requesttype = CFSetupHandlerRequest.None; //Get out of loop
                        return;
                    }
                }
                else
                    button = (int)value;

                CFSetupHandlerParams internalhandler = new CFSetupHandlerParams();
                internalhandler.requesttype = CFSetupHandlerRequest.ShowDialog;
                internalhandler.button = button;
                internalhandler.dialogtype = CF_Dialogs.NumberPad;
                internalhandler.listviewitems = null;
                internalhandler.writebutton = true;
                internalhandler.writebuttonwithvalue = false;
                internalhandler.title = this.langReader.ReadField("APPLANG/SETUP/TCPPORT");
                internalhandler.listheader = "";
                value = internalhandler;

            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        //Initial Window Size
        private void SetInitialWindowSize(ref object value)
        {
            try
            {
                //LK, 27-nov-2013: Implement InternalHandler to update the screen and button values
                if (value.GetType().Equals(typeof(CFSetupHandlerParams)))
                {
                    if (((CFSetupHandlerParams)value).result.ok)
                        this.configReader.WriteField("/APPCONFIG/WINDOWSIZE", ((CFSetupHandlerParams)value).result.value);

                    ((CFSetupHandlerParams)value).requesttype = CFSetupHandlerRequest.None; //Get out of loop
                    return;
                }

                CFSetupHandlerParams internalhandler = new CFSetupHandlerParams();
                internalhandler.requesttype = CFSetupHandlerRequest.ShowDialog;
                internalhandler.button = (int)value;
                internalhandler.dialogtype = CF_Dialogs.OSK;
                internalhandler.listviewitems = null;
                internalhandler.writebutton = true;
                internalhandler.writebuttonwithvalue = true;
                internalhandler.title = this.langReader.ReadField("APPLANG/SETUP/WINDOWSIZE");
                internalhandler.listheader = this.configReader.ReadField("/APPCONFIG/WINDOWSIZE");
                value = internalhandler;

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


        //How long to wait before audio resumes after mute
        private void SetAudioDelayAfterMute(ref object value)
        {
            try
            {
                string resultvalue, resulttext;

                if (mainForm.CF_systemDisplayDialog(CF_Dialogs.NumberPad, this.langReader.ReadField("/APPLANG/SETUP/AUDIODELAYAFTERMUTE"), out resultvalue, out resulttext) == DialogResult.OK)
                {
                    //Parse the value
                    int intTemp = int.Parse(resultvalue);

                    //Sanity check it and set to its extremes.
                    if (intTemp > 10000) intTemp = 1000;
                    if (intTemp < 0) intTemp = 0;

                    //Value is scrubbed, write it
                    this.configReader.WriteField("/APPCONFIG/AUDIODELAYAFTERMUTE", intTemp.ToString());
                    value = intTemp.ToString();
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        //Enable Sending Mute/Unmute on Sound alert?
        private void SetMuteUnmuteStatus(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/MUTEUNMUTESTATUS", value.ToString());

            //string boolButton = value.ToString();
            if (bool.Parse(value.ToString()))
            {
                mainForm.CF_systemDisplayDialog(CF_Dialogs.OkBox, mainForm.pluginLang.ReadField("/APPLANG/SETUP/MUTEUNMUTEW7"));
            }
        }

        //Enable Louk's message handler
        private void SetNamedPipeStatus(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/NAMEDPIPE", value.ToString());

            //string boolButton = value.ToString();
            if (bool.Parse(value.ToString()))
            {
                mainForm.CF_systemDisplayDialog(CF_Dialogs.OkBox, mainForm.pluginLang.ReadField("/APPLANG/SETUP/PATCHNAVIGATOR"));
            }
            else
            {
                mainForm.CF_systemDisplayDialog(CF_Dialogs.OkBox, mainForm.pluginLang.ReadField("/APPLANG/SETUP/UNPATCHNAVIGATOR"));
            }           
        }

        //NoHiRes on or off
        private void SetNoHiRes(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/NOHIRES", value.ToString());
        }
        
#endregion

    }
}
