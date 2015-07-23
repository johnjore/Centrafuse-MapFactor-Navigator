/*
 * Copyright 2014, 2015 John Jore
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
using System.IO;
using centrafuse.Plugins;
using System.Diagnostics;           //run DiggerConsole

namespace Navigator
{
    public partial class Navigator
    {
        //Generate MCA file by running digger, copy to data folder and restart MapFactor
        private void CreateMCA(string strGenerateMCAFolder)
        {
            //Create folder for MCA file
            try
            {
                WriteLog("Creating MCA folder");
                System.IO.Directory.CreateDirectory(CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + strGenerateMCAFolder);
            }
            catch (Exception ex)
            {
                WriteLog("Failed to created temp folder, " + ex.ToString());
            }

            //Copy xml file to folder
            try
            {
                WriteLog("Copy configuration XML file");
                System.IO.File.Copy(CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + strGenerateMCAFolder + "_Source\\digger_config.xml", CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + strGenerateMCAFolder + "\\digger_config.xml", true);
            }
            catch (Exception errmsg)
            {
                WriteLog("Failed to copy the xml file to " + strGenerateMCAFolder + " temp folder, " + errmsg.ToString());
            }

            try
            {
                WriteLog("Compiling MCA file");
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.FileName = @PluginPath + "\\digger\\DiggerConsole.exe";
                startInfo.WorkingDirectory = CFTools.AppDataPath + "\\Plugins\\" + PluginName;
                startInfo.Arguments = strGenerateMCAFolder + @"\digger_config.xml";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                // Start the process with the info we specified. Call WaitForExit and then the using statement will close.
                using (Process compileMCA = Process.Start(startInfo))
                {
                    compileMCA.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                WriteLog("Failed to run 'DiggerConsole.exe', " + ex.ToString());
            }

            //Move (Copy/Delete) MCA file to Navigator data folder
            try
            {
                WriteLog("Move MCA file to Navigator MCA folder");
                System.IO.File.Copy(CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + strGenerateMCAFolder + ".mca", strMCAFolder + strGenerateMCAFolder + ".mca", true);

                //Cleanup
                if (File.Exists(CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + strGenerateMCAFolder + ".mca")) File.Delete(CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + strGenerateMCAFolder + ".mca");

                //Restart MapFactor if MCA file is updated, else MapFactor has random crashes
                WriteLog("Restart Navigator: " + CF_getPluginData("NAVIGATOR", "RESTARTNAV", ""));
            }
            catch (Exception ex)
            {
                WriteLog("Failed to move the mca file to Navigators data folder, " + ex.ToString());

                //Restart MapFactor if MCA file is updated, else MapFactor has random crashes
                WriteLog("Restart Navigator: " + CF_getPluginData("NAVIGATOR", "RESTARTNAV", ""));
            }
        }
    }
}
