﻿using BMAPI.v1;
using BMAPI.v1.Events;
using BMAPI.v1.HitObjects;
using ReplayAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace osu_replayCombinator
{

    /* This class is a list of pair of a clickable object and a replay frame hit
     * Initializing the class is a task of associating every keypress with an object hit
     * After that all the procedural checks on suspicious moment become possible
     */
    public class ReplayAnalyzer
    {
        //The beatmap
        public Beatmap beatmap;

        //The replay
        public Replay replay;

        //Circle radius
        private double circleRadius;

        //hit time window
        private double hitTimeWindow;

        //hit time window
        private double approachTimeWindow;


        //The list of pair of a <hit, object hit>
        public List<HitFrame> hits
        {
            get; private set;
        }
        public List<HitFrame> attemptedHits
        {
            get; private set;
        }
        public List<ReplayMissFrame> misses
        {
            get; private set;
        }
        public List<CircleObject> effortlessMisses
        {
            get; private set;
        }
        public List<BreakEvent> breaks
        {
            get; private set;
        }
        public List<SpinnerObject> spinners
        {
            get; private set;
        }
        public CursorMovement cursorData
        {
            get; private set;
        }
        public List<ClickFrame> extraHits
        {
            get; private set;
        }

        private void applyHardrock()
        {
            replay.flip();
            beatmap.applyHardRock();
        }

        private void selectBreaks()
        {
            foreach(var event1 in this.beatmap.Events)
            {
                if(event1.GetType() == typeof(BreakEvent))
                {
                    this.breaks.Add((BreakEvent)event1);
                }
            }
        }

        private void selectSpinners()
        {
            foreach (var obj in this.beatmap.HitObjects)
            {
                if (obj.Type.HasFlag(HitObjectType.Spinner))
                {
                    this.spinners.Add((SpinnerObject)obj);
                }
            }
        }

        private void associateHits()
        {
            int keyIndex = 0;
            Keys lastKey;
            KeyCounter keyCounter = new KeyCounter();

            if((replay.Mods & Mods.HardRock) > 0)
            {
                applyHardrock();
            }

            int breakIndex = 0;
            int combo = 0;

            for(int i = 0; i < beatmap.HitObjects.Count; ++i)
            {
                CircleObject note = beatmap.HitObjects[i];
                bool noteHitFlag = false;
                bool noteAttemptedHitFlag = false;

                if ((note.Type.HasFlag(HitObjectType.Spinner)))
                    continue;

                for(int j = keyIndex; j < replay.ReplayFrames.Count; ++j)
                {
                    ReplayFrame frame = replay.ReplayFrames[j];
                    lastKey = j > 0 ? replay.ReplayFrames[j - 1].Keys : Keys.None;

                    Keys pressedKey = Utils.getKey(lastKey, frame.Keys);

                    if(breakIndex < breaks.Count && frame.Time > breaks[breakIndex].EndTime)
                    {
                        ++breakIndex;
                    }

                    if(frame.Time >= beatmap.HitObjects[0].StartTime - hitTimeWindow && (breakIndex >= breaks.Count || frame.Time < this.breaks[breakIndex].StartTime - hitTimeWindow))
                    {
                        keyCounter.Update(lastKey, frame.Keys);
                    }

                    frame.keyCounter = new KeyCounter(keyCounter);

                    if (frame.Time - note.StartTime > hitTimeWindow)
                        break;

                    if (pressedKey > 0 && Math.Abs(frame.Time - note.StartTime) <= hitTimeWindow)
                    {
                        if (note.ContainsPoint(new BMAPI.Point2(frame.X, frame.Y)))
                        {
                            noteAttemptedHitFlag = true;
                            ++combo;
                            frame.combo = combo;
                            noteHitFlag = true;
                            HitFrame hitFrame = new HitFrame(note, frame, pressedKey);
                            hits.Add(hitFrame);
                            lastKey = frame.Keys;
                            keyIndex = j + 1;
                            break;
                        }
                        else
                        {
                            if (Utils.dist(note.Location.X, note.Location.Y, frame.X, frame.Y) > 150)
                            {
                                extraHits.Add(new ClickFrame(frame, Utils.getKey(lastKey, frame.Keys)));
                            }
                            else
                            {
                                noteAttemptedHitFlag = true;
                                attemptedHits.Add(new HitFrame(note, frame, pressedKey));
                            }
                        }
                    }
                    if (pressedKey > 0 && Math.Abs(frame.Time - note.StartTime) <= 3 * hitTimeWindow && note.ContainsPoint(new BMAPI.Point2(frame.X, frame.Y)))
                    {
                        noteAttemptedHitFlag = true;
                        attemptedHits.Add(new HitFrame(note, frame, pressedKey));
                    }

                    lastKey = frame.Keys;

                    frame.combo = combo;

                }

                if(!noteHitFlag)
                {
                    misses.Add(new ReplayMissFrame(replay.ReplayFrames[keyIndex], keyIndex, note));
                }
                if (!noteAttemptedHitFlag)
                {
                    effortlessMisses.Add(note);
                }
            }
        }

        private List<double> calcPressIntervals()
        {
            List<double> result = new List<double>();

            bool k1 = false, k2 = false;
            double k1_timer = 0, k2_timer = 0;
            foreach(var frame in this.replay.ReplayFrames)
            {
                var hit = this.hits.Find(x => x.frame.Equals(frame));

                if(!ReferenceEquals(hit,null) && hit.note.Type == HitObjectType.Circle)
                {
                    if(!k1 && frame.Keys.HasFlag(Keys.K1))
                        k1 = true;

                    if(!k2 && frame.Keys.HasFlag(Keys.K2))
                        k2 = true;
                }

                //k1
                if(k1 && frame.Keys.HasFlag(Keys.K1))
                {
                    k1_timer += frame.TimeDiff;
                }

                if(k1 && !frame.Keys.HasFlag(Keys.K1))
                {
                    k1 = false;
                    result.Add(k1_timer);
                    k1_timer = 0;
                }

                //k2
                if(k2 && frame.Keys.HasFlag(Keys.K2))
                {
                    k2_timer += frame.TimeDiff;
                }

                if(k2 && !frame.Keys.HasFlag(Keys.K2))
                {
                    k2 = false;
                    result.Add(k2_timer);
                    k2_timer = 0;
                }
            }

            if (result.Count == 0)
                result.Add(-1);

            return result;
        }

        public List<KeyValuePair<HitFrame, HitFrame>> checkTappingConsistency()
        {
            var times = new List<KeyValuePair<HitFrame, HitFrame>>();

            double limit = (90 * (replay.Mods.HasFlag(Mods.DoubleTime) ? 1.5 : 1));

            for (int i = 0; i < hits.Count - 1; ++i)
            {
                HitFrame hit1 = hits[i], hit2 = hits[i + 1];

                if (((hit2.frame.Time - hit1.frame.Time <= limit) || (hit2.note.StartTime - hit1.note.StartTime <= limit)) && ((hit1.key & hit2.key) > 0))
                    times.Add(new KeyValuePair<HitFrame, HitFrame>(hit1, hit2));
            }

            return times;
        }

        public List<ReplayFrame> findCursorTeleports()
        {
            List<ReplayFrame> times = new List<ReplayFrame>();

            int spinnerIndex = 0;
            for (int i = 2; i < replay.times.Count - 1; ++i)
            {
                ReplayFrame frame = replay.times[i + 1], prev = replay.times[i];

                if (spinnerIndex < spinners.Count && frame.Time > spinners[spinnerIndex].EndTime)
                {
                    ++spinnerIndex;
                }

                if (isTeleport(prev, frame) && (spinnerIndex >= spinners.Count || frame.Time < spinners[spinnerIndex].StartTime))
                {
                    times.Add(frame);
                }
            }

            return times;
        }

        private bool isTeleport(ReplayFrame prev, ReplayFrame frame)
        {
            if (frame.travelledDistanceDiff >= 40 && double.IsInfinity(frame.speed))
                return true;

            if (frame.travelledDistanceDiff >= 150 && frame.speed >= 6)
                return true;

            return false;
        }

        public string outputDistances() 
        {
            string res = "";
            foreach (var value in findCursorTeleports())
            {
                res += value.travelledDistanceDiff + ",";
            }
            return res.Remove(res.Length - 1);
        }

        public double calculateAverageFrameTimeDiff()
        {
            return replay.times.Where(x => x.Keys == 0).ToList().ConvertAll(x => x.TimeDiff).Where(x => x > 0 && x < 30).Average();
        }

        public List<double> speedList()
        {
            return replay.times.ConvertAll(x => x.speed);
        }

        public List<double> accelerationList()
        {
            return replay.times.ConvertAll(x => x.acceleration);
        }

        public string outputSpeed()
        {
            string res = "";
            foreach(var value in speedList())
            {
                res += value + ",";
            }
            return res.Remove(res.Length - 1);
        }

        public string outputAcceleration()
        {
            string res = "";
            foreach(var value in replay.times.ConvertAll(x => x.acceleration))
            {
                res += value + ",";
            }
            return res.Remove(res.Length - 1);
        }

        public string outputTime()
        {
            string res = "";
            foreach(var value in replay.times.ConvertAll(x => x.Time))
            {
                res += value + ",";
            }
            return res.Remove(res.Length - 1);
        }


        public List<HitFrame> findOverAimHits()
        {
            var result = new List<HitFrame>();
            int keyIndex = 0;
            for(int i = 0; i < hits.Count; ++i)
            {
                CircleObject note = hits[i].note;
                //bool hover = false;

                //searches for init circle object hover
                for(int j = keyIndex; j < replay.ReplayFrames.Count; ++j)
                {
                    ReplayFrame frame = replay.ReplayFrames[j];
                    if(note.ContainsPoint(new BMAPI.Point2(frame.X, frame.Y)) && Math.Abs(frame.Time - note.StartTime) <= hitTimeWindow)
                    {
                        while(note.ContainsPoint(new BMAPI.Point2(frame.X, frame.Y)) && frame.Time < hits[i].frame.Time)
                        {
                            ++j;
                            frame = replay.ReplayFrames[j];
                        }
                        if (!note.ContainsPoint(new BMAPI.Point2(frame.X, frame.Y)))
                        {
                            result.Add(hits[i]);
                        }
                    }

                }
            }
            return result;
        }


        //Recalculate the highest CS value for which the player would still have the same amount of misses
        public double bestCSValue()
        {
            double pixelPerfect = findBestPixelHit();

            double y = pixelPerfect * circleRadius;

            double x = (54.42 - y) / 4.48;

            return x;
        }

        public double calcAccelerationVariance()
        {
            return Utils.variance(accelerationList());
        }

        public string outputMisses()
        {
            string res = "";
            this.misses.ForEach((miss) => res += "Didn't find the hit for " + miss.note.StartTime);
            return res;
        }

        private double calcTimeWindow(double OD)
        {
            return -12 * OD + 259.5;
        }

        public ReplayAnalyzer(Beatmap beatmap, Replay replay)
        {
            this.beatmap = beatmap;
            this.replay = replay;

            if (!replay.fullLoaded)
                throw new Exception(replay.Filename + " IS NOT FULL");

            multiplier = replay.Mods.HasFlag(Mods.DoubleTime) ? 1.5 : 1;

            this.circleRadius = beatmap.HitObjects[0].Radius;
            this.hitTimeWindow = calcTimeWindow(beatmap.OverallDifficulty);

            this.approachTimeWindow = 1800 - 120 * beatmap.ApproachRate;

            hits = new List<HitFrame>();
            attemptedHits = new List<HitFrame>();
            misses = new List<ReplayMissFrame>();
            effortlessMisses = new List<CircleObject>();
            extraHits = new List<ClickFrame>();
            breaks = new List<BreakEvent>();
            spinners = new List<SpinnerObject>();

            selectBreaks();
            selectSpinners();
            associateHits();
        }



        public double findBestPixelHit()
        {
            return this.hits.Max((pair) => Utils.pixelPerfectHitFactor(pair.frame, pair.note));
        }

        public List<double> findPixelPerfectHits(double threshold)
        {
            List<double> result = new List<double>();

            foreach(var pair in this.hits)
            {
                double factor = Utils.pixelPerfectHitFactor(pair.frame, pair.note);

                if(factor >= threshold)
                {
                    result.Add(pair.note.StartTime);
                }
            }


            return result;
        }

        private List<HitFrame> findAllPixelHits()
        {
            var pixelPerfectHits = new List<HitFrame>();

            foreach (var pair in hits)
            {
                pixelPerfectHits.Add(pair);
            }

            return pixelPerfectHits;
        }


        public List<HitFrame> findSortedPixelPerfectHits(int maxSize, double threshold)
        {
            var pixelPerfectHits = new List<HitFrame>();

            foreach(var pair in this.hits)
            {
                double factor = pair.Perfectness;
                if(factor >= threshold)
                    pixelPerfectHits.Add(pair);
            }

            pixelPerfectHits.Sort((a, b) => b.Perfectness.CompareTo(a.Perfectness));

            return pixelPerfectHits.GetRange(0, Math.Min(maxSize, pixelPerfectHits.Count));
        }


        private double ur = -1;
        public double unstableRate()
        {
            if(ur >= 0)
                return ur;
            List<double> values = this.hits.ConvertAll((pair) => (double)pair.frame.Time - pair.note.StartTime);
            ur = 10 * Utils.variance(values);
            return ur;
        }

        private double multiplier;
        

        public StringBuilder MainInfo()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("GENERIC INFO");
            if (misses.Count > replay.CountMiss)
            {
                sb.AppendLine("WARNING! The detected number of misses is not consistent with the replay: " + misses.Count + " VS. " + replay.CountMiss + " (notepad user or missed on spinners or BUG in the code <- MOST LIKELY )");
            }
            else
            {
                sb.AppendLine("Misses: " + misses.Count);
            }
            sb.AppendLine("Unstable rate = " + unstableRate());

            if(unstableRate() < 47.5 * multiplier)
            {
                sb.AppendLine("WARNING! Unstable rate is too low (auto)");
            }

            sb.AppendLine("The best CS value = " + bestCSValue());

            double averageFrameTimeDiff = calculateAverageFrameTimeDiff();
            sb.AppendLine("Average frame time difference = " + averageFrameTimeDiff + "ms");

            if((replay.Mods.HasFlag(Mods.DoubleTime) && averageFrameTimeDiff < 17.35) || (!replay.Mods.HasFlag(Mods.HalfTime) && averageFrameTimeDiff < 12))
            {
                sb.AppendLine("WARNING! Average frame time difference is not consistent with the speed-modifying gameplay mods (timewarp)!");
            }

            var keyPressIntervals = calcPressIntervals();

            double averageKeyPressTime = (Utils.median(keyPressIntervals) / multiplier);
            sb.AppendLine("Median Key press time interval = " + averageKeyPressTime.ToString("0.00") + "ms");
            sb.AppendLine("Min Key press time interval = " + (keyPressIntervals.Min() / multiplier).ToString("0.00") + "ms");
            if (averageKeyPressTime < 30)
            {
                sb.AppendLine("WARNING! Average Key press time interval is inhumanly low (timewarp/relax)!");
            }
            sb.AppendLine("Extra hits = " + extraHits.Count);

            if (replay.Mods.HasFlag(Mods.NoFail))
            {
                sb.AppendLine("Pass = " + replay.IsPass());
            }

            return sb;
        }

        public StringBuilder CursorInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Cursor movement Info");

            var cursorAcceleration = accelerationList();
            sb.AppendLine("Cursor acceleration mean = " + cursorAcceleration.Average());
            sb.AppendLine("Cursor acceleration variance = " + Utils.variance(cursorAcceleration));

            return sb;
        }


        public StringBuilder PixelPerfectRawData()
        {
            StringBuilder sb = new StringBuilder();
            var pixelPerfectHits = findAllPixelHits();
            foreach (var hit in pixelPerfectHits)
            {
                sb.Append(hit).Append(',');
            }
            return sb;
        }

        public StringBuilder TimeFramesRawData()
        {
            StringBuilder sb = new StringBuilder();
            var timeFrames = this.replay.ReplayFrames.ConvertAll((x) => x.TimeDiff).Where((x) => x > 0);
            foreach (var frame in timeFrames)
            {
                sb.Append(frame).Append(',');
            }
            return sb;
        }

        public StringBuilder TravelledDistanceDiffRawData()
        {
            StringBuilder sb = new StringBuilder();
            var timeFrames = this.replay.ReplayFrames.ConvertAll((x) => x.travelledDistanceDiff);
            foreach (var frame in timeFrames)
            {
                sb.Append(frame).Append(',');
            }
            return sb;
        }

        public StringBuilder SpeedRawData()
        {
            StringBuilder sb = new StringBuilder();
            var timeFrames = this.replay.ReplayFrames.ConvertAll((x) => x.speed);
            foreach (var frame in timeFrames)
            {
                sb.Append(frame).Append(',');
            }
            return sb;
        }

        public StringBuilder AccelerationRawData()
        {
            StringBuilder sb = new StringBuilder();
            var timeFrames = this.replay.ReplayFrames.ConvertAll((x) => x.acceleration);
            foreach (var frame in timeFrames)
            {
                sb.Append(frame).Append(',');
            }
            return sb;
        }

        public StringBuilder HitErrorRawData()
        {
            StringBuilder sb = new StringBuilder();
            var timeFrames = this.hits.ConvertAll((x) => x.note.StartTime - x.frame.Time);
            foreach (var frame in timeFrames)
            {
                sb.Append(frame).Append(',');
            }
            return sb;
        }

        public StringBuilder PressKeyIntevalsRawData()
        {
            StringBuilder sb = new StringBuilder();
            var timeFrames = calcPressIntervals();
            foreach (var frame in timeFrames)
            {
                sb.Append(frame).Append(',');
            }
            return sb;
        }

        public StringBuilder PixelPerfectInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("PIXEL PERFECT");

            var pixelHits = findAllPixelHits();
            var values = pixelHits.Select((x) => x.Perfectness).ToList();
            double bestPxPerfect = pixelHits.Max((a) => a.Perfectness);
            sb.AppendLine("The best pixel perfect hit = " + bestPxPerfect);
            double median = Utils.Median(values);
            double variance = Utils.variance(values);
            sb.AppendLine("Median pixel perfect hit = " + median);
            sb.AppendLine("Perfectness variance = " + variance);

            if (bestPxPerfect < 0.5 || variance < 0.01 || median < 0.2)
            {
                sb.AppendLine("WARNING! Player is aiming the notes too consistently (autohack)");
                sb.AppendLine();
            }

            var pixelperfectHits = pixelHits.Where((x) => x.Perfectness > 0.98);

            sb.AppendLine("Pixel perfect hits: " + pixelperfectHits.Count());

            foreach(var hit in pixelperfectHits)
            {
                sb.AppendLine("* " + hit);
            }

            var LOLpixelPerfectHits = pixelperfectHits.Where((x) => x.Perfectness > 0.99);

            if(LOLpixelPerfectHits.Count() > 15)
            {
                sb.AppendLine("WARNING! Player is constantly doing pixel perfect hits (relax)");
                sb.AppendLine();
            }

            return sb;
        }

        public StringBuilder OveraimsInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("OVER-AIM");

            var overAims = findOverAimHits();
            sb.AppendLine("Over-aim count: " + overAims.Count);

            foreach(var hit in overAims)
            {
                sb.AppendLine("* " + hit.ToString());
            }

            return sb;
        }

        public StringBuilder TeleportsInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Cursor teleports");

            var teleports = findCursorTeleports();
            sb.AppendLine("Teleports count: " + teleports.Count);

            foreach (var frame in teleports)
            {
                sb.AppendLine("* " + frame.Time + "ms " + frame.travelledDistanceDiff + "px");
            }

            return sb;
        }

        public StringBuilder ExtraHitsInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Extra hits");

            var teleports = findCursorTeleports();
            sb.AppendLine("Extra hits count: " + extraHits.Count);

            foreach (var frame in extraHits)
            {
                sb.AppendLine(frame.ToString());
            }

            return sb;
        }

        public StringBuilder EffortlessMissesInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Effortless misses");

            sb.AppendLine("Effortless misses count: " + effortlessMisses.Count);

            foreach (var note in effortlessMisses)
            {
                sb.AppendLine(note.ToString() + " missed without a corresponding hit");
            }

            return sb;
        }

        public StringBuilder SingletapsInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Singletaps");

            var singletaps = checkTappingConsistency();
            sb.AppendLine("Fast singletaps count: " + singletaps.Count);

            foreach (var frame in singletaps)
            {
                sb.AppendLine("* Object at " + frame.Key.note.StartTime + "ms "  + frame.Key.key + " singletapped with next at " + frame.Value.note.StartTime + " (" + (frame.Value.frame.Time - frame.Key.frame.Time) / multiplier + "ms real frame time diff) " + " " + (frame.Key.frame.Time - frame.Key.note.StartTime) + "ms and " + (frame.Value.frame.Time - frame.Value.note.StartTime) + "ms error. ");
            }
            return sb;
        }

    }
}
