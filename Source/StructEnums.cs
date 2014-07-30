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
 * Strucs and enums
*/

namespace Navigator
{
    using System;
    using System.Drawing;
    using System.Collections.Generic;

    [Serializable]
    // Structure for holding lat/lon values
    public struct LatLonDecimal
    {
        public double Latitude;
        public double Longitude;
    }

    // Structure for holding degree-minute-second values
    public struct LatLonDms
    {
        public double LatitudeDegrees;
        public double LatitudeMinutes;
        public double LatitudeSeconds;
        public double LongitudeDegrees;
        public double LongitudeMinutes;
        public double LongitudeSeconds;
    }

    // Structure for holding navigation statistics
    public struct NavStats
    {
        public double DistanceNextWaypoint;
        public double TimeSecondsNextWaypoint;
        public double DistanceDestination;
        public double TimeSecondsDestination;
        public string GPSDate;
        public string GPSTime;
        public string Street;
    }

    public enum TCPCommand : int
    {
        Protocol = 1,
        SoftwareVersion = 2,
        Minimize = 3,
        Maximize = 4,
        GPSSending = 5,
        GPSReceiving = 6,
        NavInfoSoundWarning = 7,
        SoundVolume = 8,
        NavInfoWaypointInfo = 9,
        NavInfoRecalculationWarning = 10,
        DayNight = 11,
        Window = 12,
        Destination = 13,
        Statistics = 14,
        Exit = 15,       //LK, 29-nov-2013: Added exit command
        NearestStreets = 16 //JJ: Added undocumented TCP command
    }

    public enum showWindowAttribute : int 
    { 
        SW_HIDE = 0,
        SW_SHOWNORMAL = 1,
        SW_MAXIMIZE = 3,
        SW_SHOW = 5,
        SW_MINIMIZE = 6,
        SW_RESTORE = 9,
        SW_FORCEMINIMIZE = 11 
    }

    //LK, 29-nov-2013: Added Mouse Click Events
    public enum WindowManagerEvents : int
    {
        WM_KEYDOWN = 0x001,
        WM_KEYUP = 0x002,
        WM_CHAR = 0x102,
        WM_COMMAND = 0x112,

        WM_LBUTTONDOWN = 0x201,
        WM_LBUTTONUP = 0x202,
        WM_LBUTTONDBLCLK = 0x203,
        WM_RBUTTONDOWN = 0x204,
        WM_RBUTTONUP = 0x205,
        WM_RBUTTONDBLCLK = 0x206,
        WM_MBUTTONDOWN = 0x207,
        WM_MBUTTONUP = 0x208,
        WM_MBUTTONDBLCLK = 0x209
    }

    public enum SC : int
    {
        SC_CLOSE = 0xF060,
        SC_CONTEXTHELP = 0xF180,
        SC_DEFAULT = 0xF160,
        SC_HOTKEY = 0xF150,
        SC_HSCROLL = 0xF080,
        SC_KEYMENU = 0xF100,
        SC_MAXIMIZE = 0xF030,
        SC_MINIMIZE = 0xF020,
        SC_MONITORPOWER = 0xF170,
        SC_MOUSEMENU = 0xF090,
        SC_MOVE = 0xF010,
        SC_NEXTWINDOW = 0xF040,
        SC_PREVWINDOW = 0xF050,
        SC_RESTORE = 0xF120,
        SC_SCREENSAVE = 0xF140,
        SC_SIZE = 0xF000,
        SC_TASKLIST = 0xF130,
        SC_VSCROLL = 0xF070,
    }

    public enum VK : int
    {
        VK_RETURN = 0x00d
    }

    public enum Unit : int
    {
        UNKNOWN = -1,
        METRIC = 0,
        IMPERIAL = 1
    }

    //JSON Response from OSRM
    public struct OSRMResponse
    {
        public string status_message;
        public string status;
        public string[] route_geometry;
    }

}
