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


// This was donated by Mark
// Anything not used in here should be removed!
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Navigator
{
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
                throw new ArgumentOutOfRangeException("degree", "Degree value must be greater than or equal to 0 and less than or equal to 360.");
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
                throw new ArgumentOutOfRangeException("value", "Latitude value cannot be greater than 90.");
            }
            if (value < -90.0)
            {
                throw new ArgumentOutOfRangeException("value", "Latitude value cannot be less than -90.");
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
                throw new ArgumentOutOfRangeException("value", "Longitude value cannot be greater than 180.");
            }
            if (value < -180.0)
            {
                throw new ArgumentOutOfRangeException("value", "Longitude value cannot be less than -180.");
            }
            _longitude = value;
        }
    }
}
