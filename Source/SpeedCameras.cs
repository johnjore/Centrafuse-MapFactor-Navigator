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

/* 
 * This file handles all speed camera functions;
 * 
 * Supported platforms:
 *      Pocket GPS, http://www.pocketgpsworld.com, only "Other - CSV" format
 */

using System;
using System.IO;
using System.Windows.Forms;
using centrafuse.Plugins;
using Shell32;      // Zip files can be treated as folders in Windows
using System.Collections.Generic;

namespace Navigator
{
    public partial class Navigator : CFNavPlugin
    {

#region Generic
        private readonly double mph_To_kmh = 1.60934;
        List<TrafficSpeedCamera> TrafficSpeedCameras = new List<TrafficSpeedCamera>();  //List of all traffic speed cameras

        //Generate CSV file for Digger
        private bool CreateCSVFile(string strFilePath)
        {
            WriteLog("Generating CSV file");

            try
            {
                // Create (overwrite) the CSV file to which data will be exported
                StreamWriter sw = new StreamWriter(strFilePath, false);

                //Write the header
                sw.WriteLine("Lat,Lon,Azimuth,Speed,Type,Description");

                //Write the data
                foreach (TrafficSpeedCamera trafficspeedcamera in TrafficSpeedCameras)
                {
                    //Make sure the CSV file is in the correct format with focus on , and .
                    sw.WriteLine(trafficspeedcamera.lat.ToString().Replace(',', '.') + "," + trafficspeedcamera.lon.ToString().Replace(',', '.') + "," + trafficspeedcamera.azimuth.ToString() + "," + trafficspeedcamera.speed.ToString().Replace(',', '.') + "," + trafficspeedcamera.type + "," + trafficspeedcamera.description);
                }
                sw.Close();
            }
            catch (Exception ex)
            {
                WriteLog("Failed to generate CSV file, " + ex.ToString());
                return false;
            }

            WriteLog("CSV file generated");
            return true;
        }

        private void SpeedCamera_Worker()
        {
            string[] ZipFiles = null;

            //Allow Cloud storage to sync to PC before we check if updates are available
            int intSleep = 1000 * 60 * 15;   //15 min
            WriteLog("Initial sleep for '" + intSleep.ToString() + "' milliseconds");
            System.Threading.Thread.Sleep(intSleep);
            
            //Loop for eternity
            while (true)
            {
                EnumerateZip(strPocket_GPS_Folder, ref ZipFiles);  //Get list of zip files to process
                
                if (ZipFiles.Length > 0)
                {
                    //Files are ready for processing. Process them?
                    if (CF_systemDisplayDialog(CF_Dialogs.YesNo, this.pluginLang.ReadField("/APPLANG/NAVIGATOR/NEWSPEEDCAMERA") + " (" + ZipFiles.Length.ToString() + ")") == DialogResult.OK)
                    {
                        TrafficSpeedCameras.Clear();                //Clear list
                        foreach (String fileName in ZipFiles)       //Process each zip file
                        {
                            WriteLog("Zip file to process, " + fileName);
                            string tempPath = System.IO.Path.GetTempPath() + Path.GetRandomFileName();  //Unpack zip file to a temp folder
                            UnpackZip(fileName, tempPath);          //Unpack zip file
                            ParseInputFiles(tempPath);              //Grab data from zip file and add to list
                            CleanupZip(tempPath, fileName);         //Remove temp data and mark zip file as processed
                            WriteLog("Zip file processed");
                        }

                        CreateCSVFile(CFTools.AppDataPath + @"\" + PluginPath + @"\CFSpeedCameras_Source\CFSpeedCameras.csv");  //Generate CSV file for Digger
                        CreateMCA("CFSpeedCameras");           //Generate the MCA file, copy to Data folder & restart Navigator
                    }
                }

                intSleep = 1000 * 60 * 30;   //30 min
                WriteLog("Sleep for '" + intSleep.ToString() + "' milliseconds");
                System.Threading.Thread.Sleep(intSleep);
            }
        }

#endregion

#region Pocket GPS
        //Get list of zip files to procëss
        private bool EnumerateZip(String inputFolder, ref string[] ZipFiles)
        {
            //Enumerate the ZIP files
            try
            {
                ZipFiles = System.IO.Directory.GetFiles(inputFolder, "*.zip");
            }
            catch (Exception ex)
            {
                WriteLog("ERROR: Failed to enumerate zip files, " + ex.ToString());
                ZipFiles = null;
                return false;
            }

            return true;
        }
        
        //Unpack zip file to temp folder
        private bool UnpackZip(string zipFile, string extractPath)
        {
            try
            {

                // Create the extraction path directory, if it does not exist
                if (!Directory.Exists(extractPath)) Directory.CreateDirectory(extractPath);

                Shell shell = new Shell();
                Folder archiveFolder = shell.NameSpace(Path.GetFullPath(zipFile));
                Folder extractFolder = shell.NameSpace(Path.GetFullPath(extractPath));

                // Extract each item, one by one
                WriteLog("Extracting files...");
                foreach (FolderItem fi in archiveFolder.Items())
                {
                    extractFolder.CopyHere(fi, 20);
                }

                //Wait until unpack is completed by comparing the number of files in both locations
                if (!WaitUnpackCompleted(archiveFolder, extractFolder)) return false;

                //If we get this far, then all is ok
                return true;
            }
            catch (Exception ex)
            {
                WriteLog("ERROR: Failed to unpack zip file '" + zipFile + "' to temp folder '" + extractPath + "', " + ex.ToString());
                return false;
            }
        }

        //Loop to check if unpack is completed. Max wait is 10 seconds
        private bool WaitUnpackCompleted(Folder folderSource, Folder folderDestination)
        {
            try
            {
                int sourceFolderItemCount = folderSource.Items().Count;
                int intMaxLoops = 100; //Wait max 10 seconds
                int intCurLoops = 0;
                while (folderDestination.Items().Count < sourceFolderItemCount)
                {
                    if (intMaxLoops <= intCurLoops++)
                    {
                        WriteLog("ERROR: Timeout occurred while processing archive");
                        return false;
                    }
                    System.Threading.Thread.Sleep(100);
                }

                return true;
            }
            catch (Exception ex)
            {
                WriteLog("ERROR: Could not create archive. Exception: " + ex.Message);

                return false;
            }
        }

        //Cleanup and remove temp files
        private bool CleanupZip(string extractPath, string zipFile)
        {
            bool boolSuccess = true;
            try
            {
                Directory.Delete(extractPath, true);
            }
            catch (Exception ex)
            {
                WriteLog("Failed to remove temp files, " + ex.ToString());
                boolSuccess = false;
            }

            try
            {
                System.IO.File.Move(zipFile, zipFile + ".done");
            }
            catch (Exception ex)
            {
                WriteLog("Failed to rename zip file, " + ex.ToString());
                boolSuccess = false;
            }

            //Return now
            return boolSuccess;
        }

        //Loop all CSV files and generate the CSV file
        private bool ParseInputFiles(string tmpFolder)
        {
            //Enumerate the CSV files and grab the contents to output format for use by Digger
            string[] files = System.IO.Directory.GetFiles(tmpFolder, "*.csv");
            foreach (string fileName in files)
            {
                WriteLog("Processing CSV File: " + fileName);

                string[] allLines = File.ReadAllLines(fileName);
                foreach (string inputLine in allLines)
                {
                    //Lines starting with ; are comments
                    if (inputLine[0] != ';')
                    {
                        bool boolValidEntry = true;

                        string[] inputData = inputLine.Split(',');
                        inputData[2] = inputData[2].Replace("\"", "");  //Remove " from string
                        TrafficSpeedCamera trafficspeedcamera = new TrafficSpeedCamera();

                        //GPS, Longitude
                        try
                        {
                            trafficspeedcamera.lon = double.Parse(inputData[0].Replace(".", decimalSeparator));
                        }
                        catch (Exception ex)
                        {
                            WriteLog("Failed to convert Lon, " + ex.ToString());
                            boolValidEntry = false;
                        }

                        //GPS, Latitude
                        try
                        {
                            trafficspeedcamera.lat = double.Parse(inputData[1].Replace(".", decimalSeparator));
                        }
                        catch (Exception ex)
                        {
                            WriteLog("Failed to convert Lat, " + ex.ToString());
                            boolValidEntry = false;
                        }

                        //Direction
                        try
                        {
                            switch (inputData[2].Split('-')[0].ToUpper())
                            {
                                case "N": trafficspeedcamera.azimuth = 0; break;
                                case "NE": trafficspeedcamera.azimuth = 45; break;
                                case "E": trafficspeedcamera.azimuth = 90; break;
                                case "SE": trafficspeedcamera.azimuth = 135; break;
                                case "S": trafficspeedcamera.azimuth = 180; break;
                                case "SW": trafficspeedcamera.azimuth = 225; break;
                                case "W": trafficspeedcamera.azimuth = 270; break;
                                case "NW": trafficspeedcamera.azimuth = 315; break;
                                default:
                                    trafficspeedcamera.azimuth = -1;
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteLog("Failed to convert azimuth, " + ex.ToString());
                            boolValidEntry = false;
                        }

                        //Description
                        try
                        {
                            trafficspeedcamera.description = inputData[2].Split(':')[1].Split('@')[0];
                        }
                        catch (Exception ex)
                        {
                            WriteLog("Failed to convert description, " + ex.ToString());
                            boolValidEntry = false;
                        }

                        //Type
                        try
                        {
                            string[] tmpType = inputData[2].Split(':')[0].Split('-');
                            trafficspeedcamera.type = tmpType[tmpType.Length - 1];
                        }
                        catch (Exception ex)
                        {
                            WriteLog("Failed to convert type, " + ex.ToString());
                        }

                        //Speed. km/h or mph?
                        try
                        {
                            trafficspeedcamera.speed = double.Parse(inputData[2].Split('@')[1]);

                            //Crude check to see if input file is mph or km/h format.
                            if (fileName.ToUpper().Contains("UK".ToUpper()))
                            {
                                trafficspeedcamera.speed = trafficspeedcamera.speed * mph_To_kmh;
                            }
                        }
                        catch
                        {
                            //WriteLog("Failed to convert speed, " + ex.ToString() + ". Using default value 0");
                            trafficspeedcamera.speed = 0;
                        }

                        //Add speed camera to list if data parsed successfully
                        if (boolValidEntry)
                        {
                            //Add to database
                            TrafficSpeedCameras.Add(trafficspeedcamera);

                            //Check if its doubles as a Reverse (-R) facing camera
                            if (inputData[2].ToUpper().Contains("R-".ToUpper()))
                            {
                                try
                                {
                                    //Duplicate existing record
                                    TrafficSpeedCamera trafficspeedcamera_r = new TrafficSpeedCamera();
                                    trafficspeedcamera_r.lat = trafficspeedcamera.lat;
                                    trafficspeedcamera_r.lon = trafficspeedcamera.lon;
                                    trafficspeedcamera_r.speed = trafficspeedcamera.speed;
                                    trafficspeedcamera_r.type = trafficspeedcamera.type;

                                    //Modify description
                                    trafficspeedcamera_r.description = trafficspeedcamera.description + "-R";

                                    //Reverse Direction, if valid direction data exists
                                    if (trafficspeedcamera.azimuth != -1)
                                    {
                                        trafficspeedcamera_r.azimuth = trafficspeedcamera.azimuth + 180;
                                        if (trafficspeedcamera_r.azimuth >= 360) trafficspeedcamera_r.azimuth = trafficspeedcamera_r.azimuth - 360;
                                        if (trafficspeedcamera_r.azimuth < 0) trafficspeedcamera_r.azimuth = trafficspeedcamera_r.azimuth + 360;
                                    }
                                    else
                                    {
                                        trafficspeedcamera_r.azimuth = -1;
                                    }

                                    //Add to database
                                    TrafficSpeedCameras.Add(trafficspeedcamera_r);
                                }
                                catch (Exception ex)
                                {
                                    WriteLog("Failed to convert azimuth for Reverse direction, " + ex.ToString());
                                    boolValidEntry = false;
                                }
                            }
                        }
                        else
                        {
                            WriteLog("Not added to list: " + inputLine);
                        }
                    }
                }
            }

            return true;
        }
#endregion

    }
}