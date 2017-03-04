﻿using System;

namespace ReplayAPI
{
    public class ReplayFrame : ICloneable
    {
        public int TimeDiff;
        public int Time;
        [System.ComponentModel.DisplayName("Time In Seconds")]
        public float TimeInSeconds { get { return Time / 1000f; } }
        public float X { get; set; }
        public float Y { get; set; }
        public Keys Keys { get; set; }
        public KeyCounter keyCounter { get; set; }
        public int combo { get; set; }
        public double travelledDistance { get; set; }
        public double travelledDistanceDiff { get; set; }
        public double speed { get; set; }
        public double acceleration { get; set; }

        
        public override string ToString()
        {
            return string.Format("{0}({1}): ({2},{3}) {4} {5}", Time, TimeDiff, X, Y, Keys, travelledDistanceDiff);
        }

        public object Clone()
        {
            ReplayFrame frame = new ReplayFrame();
            frame.TimeDiff = TimeDiff;
            frame.Time = Time;
            frame.X = X;
            frame.Y = Y;
            frame.Keys = Keys;
            frame.keyCounter = keyCounter;
            frame.combo = combo;
            frame.travelledDistance = travelledDistance;
            frame.travelledDistanceDiff = travelledDistanceDiff;
            frame.speed = speed;
            frame.acceleration = acceleration;
            return frame;
        }
    }
}
