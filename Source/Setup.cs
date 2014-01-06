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
 * CF Bugs:
 *  If ExeParameters is blank, screen is not refreshed with empty string. Old value shown, but correct value (empty string) written to disk.
 *  Requires CF to fix the issue
 */

//Include OSM license text when changing to Yes on OSM license
using System;
using System.Windows.Forms;
using System.Xml;
using System.Web;
using centrafuse.Plugins;
using System.Reflection;

namespace Navigator
{
    internal class NavSetup : ICFInterfaceSetup
    {        
        #region Variables and consts
        //LK, 25-nov-2013: Changed to read only: Savety first
        private readonly ConfigReader configReader;
        private readonly LanguageReader langReader;
        private readonly Navigator mainForm;
        private bool boolAskNavigatorRestart = false;       //Ask if to restart Navigator?

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

            boolAskNavigatorRestart = false;       //Ask if to restart Navigator?
        }

        public void CF_setupExitSettings(bool save)
        {
            if (save)
            {
                //Save and get configuration settings
                configReader.Save();
                mainForm.LoadSettings();

                //Only ask if a parameter that requires Navigator restart is changed
                if (boolAskNavigatorRestart)
                {
                    //Ask if restart of Navigator is desired?
                    if (mainForm.CF_systemDisplayDialog(CF_Dialogs.YesNo, this.langReader.ReadField("/APPLANG/NAVIGATOR/RESTARTNAVIGATOR")) == DialogResult.OK)
                    {
                        mainForm.WriteLog("Restarting Navigator by user request (setup)");
                        //Close Navigator
                        mainForm.WriteLog("Setup - Closenavigator()");
                        mainForm.CloseNavigator();

                        //User does not really want to exit Navigator anymore
                        mainForm.boolExit = false;

                        //Modify Navigator's Settings XML file to match new configuration
                        mainForm.WriteLog("Setup - ConfigureNavigatorXML()");
                        mainForm.ConfigureNavigatorXML();

                        //Start Nnavigator
                        mainForm.WriteLog("Setup - StartNavigator()");
                        mainForm.StartNavigator();

                        //Need this to get back to the correct window
                        mainForm.WriteLog("Setup - Centrafuse.CFActions.MainMenu");
                        mainForm.CF3_executeCMLAction("Centrafuse.CFActions.MainMenu");

                        //Default back to off now
                        boolAskNavigatorRestart = false;       //Ask if to restart Navigator?
                    }
                }
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
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/EXEPATH") + " (" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + ")";
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

                    ButtonHandler[i] = new CFSetupHandler(SetSettingsXMLSwap);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/SETTINGSXMLSWAP");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/SETTINGSXMLSWAP");

                    ButtonHandler[i] = new CFSetupHandler(SetLocalizeGPSStatus);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/LOCALIZE");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/LOCALIZE");

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

                    ButtonHandler[i] = new CFSetupHandler(SetTrimDigits);
                    ButtonText[i] = this.langReader.ReadField("/APPLANG/SETUP/TRIMDIGITS");
                    ButtonValue[i++] = this.configReader.ReadField("/APPCONFIG/TRIMDIGITS");                   
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
                if (value.GetType().Equals(typeof(CFSetupHandlerParams)))
                {
                    string location = this.configReader.ReadField("/APPCONFIG/EXEPATH");
                    if (string.IsNullOrEmpty(location)) location = PluginPath;

                    CFDialogParams dialogParams = new CFDialogParams(this.langReader.ReadField("/APPLANG/SETUP/EXEPATH"), location);
                    dialogParams.browseable = true;
                    dialogParams.enablesubactions = true;
                    dialogParams.showfiles = false;
                    dialogParams.showextension = true;

                    CFDialogResults results = new CFDialogResults();
                    if (mainForm.CF_displayDialog(CF_Dialogs.FileBrowser, dialogParams, results) == DialogResult.OK)
                    {
                        ((CFSetupHandlerParams)value).result.ok = true;
                        ((CFSetupHandlerParams)value).result.pobject = results.resultobject;
                        ((CFSetupHandlerParams)value).result.text = results.resulttext;
                        ((CFSetupHandlerParams)value).result.value = results.resultvalue;
                        this.configReader.WriteField("/APPCONFIG/EXEPATH", results.resultvalue);

                        //Make sure user can opt to restart Navigator
                        boolAskNavigatorRestart = true;
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

        //Extra parameters
        private void SetExeParameters(ref object value)
        {
            try
            {
                //LK, 27-nov-2013: Implement InternalHandler to update the screen and button values
                if (value.GetType().Equals(typeof(CFSetupHandlerParams)))
                {
                    bool boolIgnoreCFError;
                    //If start value is empty, then don't show message at the end if that's also empty
                    string location = this.configReader.ReadField("/APPCONFIG/EXEPARAMETERS");
                    if (string.IsNullOrEmpty(location)) boolIgnoreCFError = true; else boolIgnoreCFError = false;

                    if (((CFSetupHandlerParams)value).result.ok)
                    {
                        this.configReader.WriteField("/APPCONFIG/EXEPARAMETERS", ((CFSetupHandlerParams)value).result.value);

                        //Make sure user can opt to restart Navigator
                        boolAskNavigatorRestart = true;

                        //Let use know about the screen refresh issue
                        if (((CFSetupHandlerParams)value).result.value == "" && boolIgnoreCFError == false) mainForm.CF_systemDisplayDialog(CF_Dialogs.OkBox, mainForm.pluginLang.ReadField("/APPLANG/SETUP/CFREFRESHBUG"));
                    }

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
        
        //Port to use for communications with Navigator
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

                            //Make sure user can opt to restart Navigator
                            boolAskNavigatorRestart = true;

                            return;
                        }
                    }
                    else
                    {
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

                    //Make sure user can opt to restart Navigator
                    boolAskNavigatorRestart = true;

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

            try
            {
                if (bool.Parse(value.ToString()) == false)
                {
                    mainForm.CF_systemDisplayDialog(CF_Dialogs.OkBox, mainForm.pluginLang.ReadField("/APPLANG/SETUP/LICENSED"));
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            //Make sure user can opt to restart Navigator
            boolAskNavigatorRestart = true;
        }

        //If on, supresses OSM OK box
        private void AcceptedOSM(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/OSMOK", value.ToString());

            try
            {
                if (bool.Parse(value.ToString()) == true)
                {
                    mainForm.CF_systemDisplayDialog(CF_Dialogs.OkBox, mainForm.pluginLang.ReadField("/APPLANG/SETUP/OSMMESSAGE"));
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }                        
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
                        if (iTemp <= 10000 && iTemp >= 0)
                        {
                            ((CFSetupHandlerParams)value).requesttype = CFSetupHandlerRequest.None; //Get out of loop
                            this.configReader.WriteField("/APPCONFIG/AUDIODELAYAFTERMUTE", iTemp.ToString());
                            return;
                        }
                    }
                    else
                    {
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
                internalhandler.title = this.langReader.ReadField("APPLANG/SETUP/AUDIODELAYAFTERMUTE");
                internalhandler.listheader = "";
                value = internalhandler;

            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }                
        }


        //Swap mapFactor Navigator Config XML files around
        private void SetSettingsXMLSwap(ref object value)
        {
            try
            {
                if (value.GetType().Equals(typeof(CFSetupHandlerParams)))
                {
                    // Create a listview with the number of items in the Array
                    CFControls.CFListViewItem[] textoptions = new CFControls.CFListViewItem[2];
                    textoptions[0] = new CFControls.CFListViewItem(this.langReader.ReadField("/APPLANG/NAVIGATOR/TRUE"), true.ToString(), false);
                    textoptions[1] = new CFControls.CFListViewItem(this.langReader.ReadField("/APPLANG/NAVIGATOR/FALSE"), false.ToString(), false);

                    CFDialogParams dialogParams = new CFDialogParams(this.langReader.ReadField("/APPLANG/SETUP/SETTINGSXMLSWAP"), this.langReader.ReadField("/APPLANG/SETUP/SETTINGSXMLSWAP"));
                    dialogParams.browseable = false;
                    dialogParams.enablesubactions = false;
                    dialogParams.showfiles = false;
                    dialogParams.showextension = false;
                    dialogParams.listitems = textoptions;
                    
                    CFDialogResults results = new CFDialogResults();
                    if (mainForm.CF_displayDialog(CF_Dialogs.FileBrowser, dialogParams, results) == DialogResult.OK)
                    {
                        ((CFSetupHandlerParams)value).result.ok = true;
                        ((CFSetupHandlerParams)value).result.pobject = results.resultobject;
                        ((CFSetupHandlerParams)value).result.text = results.resulttext;
                        ((CFSetupHandlerParams)value).result.value = results.resultvalue;

                        this.configReader.WriteField("/APPCONFIG/SETTINGSXMLSWAP", bool.Parse(results.resultvalue).ToString());

                        //Make sure user can opt to restart Navigator
                        boolAskNavigatorRestart = true;
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
                internalhandler.title = this.langReader.ReadField("APPLANG/SETUP/SETTINGSXMLSWAP");
                internalhandler.listheader = this.langReader.ReadField("APPLANG/SETUP/SETTINGSXMLSWAP");
                value = internalhandler;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }            
        }


        //Localize GPS Status page
        private void SetLocalizeGPSStatus(ref object value)
        {
            try
            {
                if (value.GetType().Equals(typeof(CFSetupHandlerParams)))
                {
                    // Create a listview with the number of items in the Array
                    CFControls.CFListViewItem[] textoptions = new CFControls.CFListViewItem[2];
                    textoptions[0] = new CFControls.CFListViewItem(this.langReader.ReadField("/APPLANG/NAVIGATOR/TRUE"), true.ToString(), false);
                    textoptions[1] = new CFControls.CFListViewItem(this.langReader.ReadField("/APPLANG/NAVIGATOR/FALSE"), false.ToString(), false);

                    CFDialogParams dialogParams = new CFDialogParams(this.langReader.ReadField("/APPLANG/SETUP/LOCALIZE"), this.langReader.ReadField("/APPLANG/SETUP/LOCALIZE"));
                    dialogParams.browseable = false;
                    dialogParams.enablesubactions = false;
                    dialogParams.showfiles = false;
                    dialogParams.showextension = false;
                    dialogParams.listitems = textoptions;

                    CFDialogResults results = new CFDialogResults();
                    if (mainForm.CF_displayDialog(CF_Dialogs.FileBrowser, dialogParams, results) == DialogResult.OK)
                    {
                        ((CFSetupHandlerParams)value).result.ok = true;
                        ((CFSetupHandlerParams)value).result.pobject = results.resultobject;
                        ((CFSetupHandlerParams)value).result.text = results.resulttext;
                        ((CFSetupHandlerParams)value).result.value = results.resultvalue;

                        this.configReader.WriteField("/APPCONFIG/LOCALIZE", bool.Parse(results.resultvalue).ToString());
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
                internalhandler.title = this.langReader.ReadField("APPLANG/SETUP/LOCALIZE");
                internalhandler.listheader = this.langReader.ReadField("APPLANG/SETUP/LOCALIZE");
                value = internalhandler;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }
       
        //Enable Sending Mute/Unmute on Sound alert?
        private void SetMuteUnmuteStatus(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/MUTEUNMUTESTATUS", value.ToString());

            if (bool.Parse(value.ToString()))
            {
                //Dont show warning if running W7 or W8
                if ((mainForm.CF_getConfigSetting(CF_ConfigSettings.OSVersion).ToString() != "Win7") && (mainForm.CF_getConfigSetting(CF_ConfigSettings.OSVersion).ToString() != "Win8"))
                {
                    mainForm.CF_systemDisplayDialog(CF_Dialogs.OkBox, mainForm.pluginLang.ReadField("/APPLANG/SETUP/MUTEUNMUTEW7"));
                }
            }
        }

        //Enable Louk's message handler?
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

            //Make sure user can opt to restart Navigator
            boolAskNavigatorRestart = true;
        }

        //NoHiRes on or off
        private void SetNoHiRes(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/NOHIRES", value.ToString());

            //Make sure user can opt to restart Navigator
            boolAskNavigatorRestart = true;
        }

        //Trim number of digits?
        private void SetTrimDigits(ref object value)
        {
            this.configReader.WriteField("/APPCONFIG/TRIMDIGITS", value.ToString());
        }

#endregion

    }
}
