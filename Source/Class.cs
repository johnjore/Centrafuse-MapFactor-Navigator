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
 * Classes used for variables
*/

namespace Navigator
{
    using System;
    using System.Drawing;
    using System.Collections.Generic;

    [Serializable]
    // Structure for holding lat/lon values (used by helper methods, can be removed if you don't use them)
    public struct LatLonDecimal
    {
        public double Latitude;
        public double Longitude;
    }

    // Structure for holding degree-minute-second values (used by helper methods, can be removed if you don't use them)
    public struct LatLonDms
    {
        public double LatitudeDegrees;
        public double LatitudeMinutes;
        public double LatitudeSeconds;
        public double LongitudeDegrees;
        public double LongitudeMinutes;
        public double LongitudeSeconds;
    }

    public enum TCPCommand
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
        Statistics = 14
    }
}
