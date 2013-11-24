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


// Parts donated by Mark
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Xml;
using centrafuse.Plugins;
using System.Drawing;
using System.Windows.Forms;

namespace Navigator
{
    public partial class Navigator
    {
        public LatLonDms ConvertDecimalToDms(double latitude, double longitude)
        {
            var retvalue = new LatLonDms();

            try
            {
                //Math.round is used to eliminate the small error caused by rounding in the computer:
                //e.g. 0.2 is not the same as 0.20000000000284

                double signlat = 1;
                double signlon = 1;

                if (latitude < 0)
                    signlat = -1;

                double latAbs = Math.Abs(Math.Round(latitude * (1000000)));

                if (latAbs > (90 * 1000000))
                    latAbs = 0;

                if (longitude < 0)
                    signlon = -1;

                double lonAbs = Math.Abs(Math.Round(longitude * (1000000)));

                if (lonAbs > (180 * 1000000))
                    lonAbs = 0;

                double latdegrees = Math.Floor(latAbs / (1000000)) * signlat;
                double latminutes = Math.Floor(((latAbs / (1000000)) - Math.Floor(latAbs / (1000000))) * (60));
                double latseconds =
                    Math.Floor(((((latAbs / (1000000)) - Math.Floor(latAbs / (1000000))) * (60)) -
                                Math.Floor(((latAbs / (1000000)) - Math.Floor(latAbs / (1000000))) * (60))) * (100000)) * (60) /
                    (100000);

                double londegrees = Math.Floor(lonAbs / (1000000)) * signlon;
                double lonminutes = Math.Floor(((lonAbs / (1000000)) - Math.Floor(lonAbs / (1000000))) * (60));
                double lonseconds =
                    Math.Floor(((((lonAbs / (1000000)) - Math.Floor(lonAbs / (1000000))) * (60)) -
                                Math.Floor(((lonAbs / (1000000)) - Math.Floor(lonAbs / (1000000))) * (60))) * (100000)) * (60) /
                    (100000);

                retvalue.LatitudeDegrees = latdegrees;
                retvalue.LatitudeMinutes = latminutes;
                retvalue.LatitudeSeconds = latseconds;

                retvalue.LongitudeDegrees = londegrees;
                retvalue.LongitudeMinutes = lonminutes;
                retvalue.LongitudeSeconds = lonseconds;
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(errmsg.Message, errmsg.StackTrace);
            }

            return retvalue;
        }

        public LatLonDecimal ConvertDmsToDecimal(double latdegrees, double latminutes, double latseconds,
                                                 double londegrees, double lonminutes, double lonseconds)
        {
            var retvalue = new LatLonDecimal();

            try
            {
                //Math.round is used to eliminate the small error caused by rounding in the computer:
                //e.g. 0.2 is not the same as 0.20000000000284

                double latsign = 1;
                double lonsign = 1;

                if (latdegrees < 0)
                    latsign = -1;

                double absdlat = Math.Abs(Math.Round(latdegrees * (1000000)));
                //if(absdlat > (90 * 1000000))

                double absmlat = Math.Abs(Math.Round(latminutes * (1000000)));
                //if(absmlat >= (60 * 1000000))

                double absslat = Math.Abs(Math.Round(latseconds * (1000000)));
                //if(absslat > (59.99999999 * 1000000))

                if (londegrees < 0)
                    lonsign = -1;

                double absdlon = Math.Abs(Math.Round(londegrees * (1000000)));
                //if(absdlon > (180 * 1000000))

                double absmlon = Math.Abs(Math.Round(lonminutes * (1000000)));
                //if(absmlon >= (60 * 1000000))

                double absslon = Math.Abs(Math.Round(lonseconds * (1000000)));
                //if(absslon > (59.99999999 * 1000000))

                double latitude = Math.Round(absdlat + (absmlat / (60)) + (absslat / (3600))) * latsign / (1000000);
                double longitude = Math.Round(absdlon + (absmlon / (60)) + (absslon / (3600))) * lonsign / (1000000);

                retvalue.Latitude = latitude;
                retvalue.Longitude = longitude;
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(errmsg.Message, errmsg.StackTrace);
            }

            return retvalue;
        }

        public int GetGmtOffset(double latitude, double longitude)
        {
            int retvalue = 0;

            try
            {
                bool addneg = false;
                LatLonDms dms = ConvertDecimalToDms(latitude, longitude);

                if (dms.LongitudeDegrees < 0)
                    addneg = true;

                double dvalue = Math.Abs(dms.LongitudeDegrees) / (15);
                string svalue = dvalue.ToString("#");
                int gmtoffset = Int32.Parse(svalue);

                if (addneg)
                    gmtoffset = gmtoffset * -1;

                retvalue = gmtoffset;
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(errmsg.Message, errmsg.StackTrace);
            }

            return retvalue;
        }


        //Read CF settings
        private Boolean ReadCFValue(string thenode, string pname, string strFile)
        {
            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(strFile);
                string str = document.SelectSingleNode(thenode).InnerText;
                if (str.Trim().ToUpper() == pname.Trim().ToUpper())
                    return true;
                else
                    return false;
            }

            catch (Exception exception)
            {
                CFTools.writeError(exception.Message, exception.StackTrace);
            }

            return false;
        }


        //Send mouse click. Used to exit Navigator
        private void ClickOnPoint(IntPtr wndHandle, Point clientPoint)
        {
            var oldPos = Cursor.Position;

            /// get screen coordinates
            ClientToScreen(wndHandle, ref clientPoint);

            /// set cursor on coords, and press mouse
            Cursor.Position = new Point(clientPoint.X, clientPoint.Y);

            //Send the events
            mouse_event(0x00000002, 0, 0, 0, UIntPtr.Zero); /// left mouse button down
            mouse_event(0x00000004, 0, 0, 0, UIntPtr.Zero); /// left mouse button up
            mouse_event(0x00000008, 0, 0, 0, UIntPtr.Zero); /// right mouse button down
            mouse_event(0x00000010, 0, 0, 0, UIntPtr.Zero); /// right mouse button up

            /// return mouse 
            Cursor.Position = oldPos;
        }
    }


    public static class Helper
    {        
        public enum CardinalPoints
        {
            N,
            E,
            W,
            S,
            NE,
            NW,
            SE,
            SW
        }
        
        public enum UnitsOfLength
        {
            Mile,
            NauticalMiles,
            Kilometer
        }

        public static double Bearing(Coordinate coordinate1, Coordinate coordinate2)
        {
            double latitude1 = coordinate1.Latitude.ToRadian();
            double latitude2 = coordinate2.Latitude.ToRadian();
            double longitudeDifference = (coordinate2.Longitude - coordinate1.Longitude).ToRadian();
            double y = Math.Sin(longitudeDifference)*Math.Cos(latitude2);
            double x = (Math.Cos(latitude1)*Math.Sin(latitude2)) - ((Math.Sin(latitude1)*Math.Cos(latitude2))*Math.Cos(longitudeDifference));
            return ((Math.Atan2(y, x).ToDegree() + 360.0)%360.0);
        }
       
        public static double Distance(Coordinate coordinate1, Coordinate coordinate2, UnitsOfLength unitsOfLength)
        {
            double theta = coordinate1.Longitude - coordinate2.Longitude;
            double distance = (Math.Sin(coordinate1.Latitude.ToRadian())*Math.Sin(coordinate2.Latitude.ToRadian())) +
                              ((Math.Cos(coordinate1.Latitude.ToRadian())*Math.Cos(coordinate2.Latitude.ToRadian()))*
                               Math.Cos(theta.ToRadian()));
            distance = (Math.Acos(distance).ToDegree()*60.0)*1.1515;
            if (unitsOfLength == UnitsOfLength.Kilometer)
            {
                return (distance*1.609344);
            }
            if (unitsOfLength == UnitsOfLength.NauticalMiles)
            {
                distance *= 0.8684;
            }
            return distance;
        }

        public static CardinalPoints ToCardinalMark(this double degree)
        {
            var crange0 = new List<CardinalRanges>();
            var crange1 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.N,
                    LowRange = 0.0,
                    HighRange = 22.5
                };
            crange0.Add(crange1);
            var crange2 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.NE,
                    LowRange = 22.5,
                    HighRange = 67.5
                };
            crange0.Add(crange2);
            var crange3 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.E,
                    LowRange = 67.5,
                    HighRange = 112.5
                };
            crange0.Add(crange3);
            var crange4 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.SE,
                    LowRange = 112.5,
                    HighRange = 157.5
                };
            crange0.Add(crange4);
            var crange5 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.S,
                    LowRange = 157.5,
                    HighRange = 202.5
                };
            crange0.Add(crange5);
            var crange6 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.SW,
                    LowRange = 202.5,
                    HighRange = 247.5
                };
            crange0.Add(crange6);
            var crange7 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.W,
                    LowRange = 247.5,
                    HighRange = 292.5
                };
            crange0.Add(crange7);
            var crange8 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.NW,
                    LowRange = 292.5,
                    HighRange = 337.5
                };
            crange0.Add(crange8);
            var crange9 = new CardinalRanges
                {
                    CardinalPoint = CardinalPoints.N,
                    LowRange = 337.5,
                    HighRange = 360.1
                };
            crange0.Add(crange9);

            List<CardinalRanges> cardinalRanges = crange0;
            if ((degree < 0.0) || (degree > 360.0))
            {
                //new ArgumentOutOfRangeException("degree", "Degree value must be greater than or equal to 0 and less than or equal to 360. (" + degree.ToString() + ")");
                degree = 0;
            }
            return cardinalRanges.Find(p => (degree >= p.LowRange) && (degree < p.HighRange)).CardinalPoint;
        }
        
        public static double ToDegree(this double radian)
        {
            return ((radian/3.1415926535897931)*180.0);
        }

        public static double ToRadian(this double degree)
        {
            return ((degree*3.1415926535897931)/180.0);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CardinalRanges
        {
            public CardinalPoints CardinalPoint;
            public double LowRange;
            public double HighRange;
        }
    }
}

public class CfNavData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double Heading { get; set; }
    public double Speed { get; set; }
    public bool InRoute { get; set; }
    public string Direction { get; set; }
    public int LockedSatellites { get; set; }
}

public class Coordinate
{
    private double _latitude;
    private double _longitude;

    public double Latitude
    {
        get { return _latitude; }
        set
        {
            if (value > 90.0)
            {
                //throw new ArgumentOutOfRangeException("value", "Latitude value cannot be greater than 90.");
                value = 0;
            }
            if (value < -90.0)
            {
                //throw new ArgumentOutOfRangeException("value", "Latitude value cannot be less than -90.");
                value = 0;
            }
            _latitude = value;
        }
    }

    public double Longitude
    {
        get { return _longitude; }
        set
        {
            if (value > 180.0)
            {
                //throw new ArgumentOutOfRangeException("value", "Longitude value cannot be greater than 180.");
                value = 0;
            }
            if (value < -180.0)
            {
                //throw new ArgumentOutOfRangeException("value", "Longitude value cannot be less than -180.");
                value = 0;
            }
            _longitude = value;
        }
    }
}
