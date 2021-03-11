using DemoInfo;
using System;
using System.Collections.Generic;

namespace MatchTraceGenerator
{
    public class PressureMap
    {
        public List<Point> Points = new List<Point>();
        public List<Line> Lines = new List<Line>();
        public class Point
        {
            public Vector Location { get; set; }
            public double Radius { get; set; }
            double Duration { get; set; }
            double Heat { get; set; }
            private double _TimePassed { get; set; }
            double TimePassed
            {
                get
                {
                    return _TimePassed;
                }
                set
                {
                    // Applies cooling to heat value immediately after adding time
                    double SigmoidInput = 12 * value / Duration - 6;
                    CurrentHeat = Heat * (1 - Sigmoid(SigmoidInput));
                    _TimePassed = value;

                }
            }
            public double CurrentHeat { get; set; }
            // Sigmoid function, for all your sigmoid needs
            public static float Sigmoid(double value)
            {
                float k = (float)Math.Exp(value);
                return k / (1.0f + k);
            }
            public Point(Vector Location, double Radius, double Duration, double Heat)
            {
                this.Location = Location;
                this.Radius = Radius;
                this.Duration = Duration;
                this.Heat = Heat;
                this.TimePassed = 0;
            }
            /// <summary>
            /// Applies time to all points, decreasing the point's core heat
            /// </summary>
            /// <param name="Delta"></param>
            /// <returns></returns>
            public bool ApplyTime(double Delta)
            {
                TimePassed += Delta;
                return TimePassed > Duration;
            }
            public virtual double PerceivedHeat(Vector Location)
            {
                Vector DistanceVector = Location - this.Location;
                double Distance = DistanceVector.Absolute;
                if (Distance > this.Radius)
                {
                    return 0;
                }
                else
                {
                    return Radius * Math.Exp(-1 / (1 - Math.Pow(Distance, 2)));
                }
            }
        }
        public class Line : Point
        {
            Vector Location2;
            public Line(Vector Location, double Radius, double Heat, double Direction, double Duration) : base(Location, Radius, Duration, Heat)
            {
                this.Location2 = Location.Copy();
                this.Location2.X += (float)Math.Sin(Direction);
                this.Location2.Y += (float)Math.Cos(Direction);
            }
            public override double PerceivedHeat(Vector InputLocation)
            {
                double X0, X1, X2, Y0, Y1, Y2, X21, Y21;
                (X0, X1, X2) = (InputLocation.X, Location.X, Location2.X);
                (Y0, Y1, Y2) = (InputLocation.Y, Location.Y, Location2.Y);
                X21 = X2 - X1;
                Y21 = Y2 - Y1;

                double Distance = Math.Abs(X21*(Y1-Y0)- Y21*(X1-X0))/Math.Sqrt(X21*X21+Y21*Y21);
                if (Distance > this.Radius)
                {
                    return 0;
                }
                else
                {
                    return Radius * Math.Exp(-1 / (1 - Math.Pow(Distance, 2)));
                }
            }
        }
        public void ResetMap()
        {
            Points = new List<Point>();
            Lines = new List<Line>();
        }
        public void Cooldown(double DeltaTime)
        {
            if (Points.Count > 0)
            {
                for (int i = Points.Count - 1; i >= 0; i--)
                {
                    if (Points[i].ApplyTime(DeltaTime))
                    {
                        Points.RemoveAt(i);
                    }
                }
            }
            if (Lines.Count > 0)
            {
                for (int i = Lines.Count - 1; i >= 0; i--)
                {
                    if (Lines[i].ApplyTime(DeltaTime))
                    {
                        Lines.RemoveAt(i);
                    }
                }
            }
        }
        
        public double PerceivedPressure(Vector Location)
        {
            double CurrentPressure = 0;
            if (Points.Count > 0)
            {
                foreach (Point Point in Points)
                {
                    CurrentPressure += Point.PerceivedHeat(Location);
                }
            }
            if (Lines.Count > 0)
            {
                foreach (Line Line in Lines)
                {
                    CurrentPressure += Line.PerceivedHeat(Location);
                }
            }
            return CurrentPressure;
        }

    }
}
