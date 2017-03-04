using BMAPI.v1;
using ReplayAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace osu_replayCombinator
{
    class ReplayProcessor
    {
        private List<ReplayAnalyzer> processedReplays;
        private Replay resultingReplay;

        public ReplayProcessor(List<Replay> replays, Beatmap beatmap)
        {
            checkInputConsistency(replays, beatmap);

            resultingReplay = new Replay();
            processedReplays = replays.ConvertAll((x) => new ReplayAnalyzer(beatmap, x));
            initializeEmptyReplay();
        }

        private void checkInputConsistency(List<Replay> replays, Beatmap beatmap)
        {
            if (replays.Count == 0)
                throw new Exception("Empty replay list supplied!");

            var mapHash = beatmap.BeatmapHash;

            if (!replays.TrueForAll((x) => x.MapHash == mapHash))
                throw new Exception("Map has is not consistent!");
        }

        private void initializeEmptyReplay()
        {
            resultingReplay.MapHash = processedReplays[0].replay.MapHash;
            resultingReplay.PlayerName = processedReplays[0].replay.PlayerName;
        }

        //Connects replays into resultingReplay trying to eliminate all the misses
        public void connectReplays()
        {
            if (!processedReplays.TrueForAll((x) => x.misses.Count > 0))
                throw new Exception("There is already an FC in the replays");

            var nextMissesList = new List<int>(new int[processedReplays.Count]);

            for(;;)
            {
                int nextReplay = 0;
                for(int i = 0; i < nextMissesList.Count; ++i)
                {
                    if (processedReplays[i].misses[nextMissesList[i]].note.StartTime > processedReplays[nextReplay].misses[nextMissesList[nextReplay]].note.StartTime)
                    {
                        nextReplay = i;
                    }
                }

                int prevMiss;
                if (nextMissesList[nextReplay] == 0)
                    prevMiss = 0;
                else
                    prevMiss = processedReplays[nextReplay].misses[nextMissesList[nextReplay] - 1].segmentIndex;

                int nextMiss = processedReplays[nextReplay].misses[nextMissesList[nextReplay]].segmentIndex;

                var newSegment = processedReplays[nextReplay].replay.ReplayFrames.GetRange(prevMiss, nextMiss);

                ReplaySegmentCombiner combiner = new ReplaySegmentCombiner(resultingReplay.ReplayFrames, newSegment);
                resultingReplay.ReplayFrames = combiner.getResult();

                ++nextMissesList[nextReplay];
                float newTime = processedReplays[nextReplay].misses[nextMissesList[nextReplay]].note.StartTime;
                for (int i = 0; i < nextMissesList.Count; ++i)
                {
                    while (processedReplays[i].misses[nextMissesList[i]].note.StartTime < newTime && nextMissesList[i] < processedReplays[i].misses.Count)
                        ++nextMissesList[i];

                    if (nextMissesList[nextReplay] == processedReplays[nextReplay].misses.Count)
                    {
                        int prev = processedReplays[i].misses[nextMissesList[i] - 1].segmentIndex;
                        ReplaySegmentCombiner combiner1 = new ReplaySegmentCombiner(resultingReplay.ReplayFrames, processedReplays[i].replay.ReplayFrames.GetRange(prev, processedReplays[i].replay.ReplayFrames.Count - prev + 1));
                        resultingReplay.ReplayFrames = combiner1.getResult();
                        return;
                    }

                }

                /*if (nextMissesList[nextReplay] >= processedReplays[nextReplay].misses.Count)
                {
                    ReplaySegmentCombiner combiner1 = new ReplaySegmentCombiner(resultingReplay.ReplayFrames, );
                    resultingReplay.ReplayFrames = combiner1.getResult();
                    break;
                }*/
            }
        }

        private ReplayFrame insertReplayFrame(int j, int time)
        {
            ReplayFrame r = (ReplayFrame)this.processedReplays[0].replay.ReplayFrames[j].Clone();

            this.processedReplays[0].replay.ReplayFrames.Insert(j, r);

            //r.Time = (this.processedReplays[0].replay.ReplayFrames[j - 1].Time + r.Time) / 2;
            r.Time = time;

            r.TimeDiff = r.Time - this.processedReplays[0].replay.ReplayFrames[j - 1].Time;

            this.processedReplays[0].replay.ReplayFrames[j + 1].TimeDiff = this.processedReplays[0].replay.ReplayFrames[j + 1].Time - r.Time;

            return r;
        }

        public void fixMisses()
        {
            int j = 0;
            foreach (var miss in this.processedReplays[0].misses)
            {
                var time = miss.note.StartTime;

                for(;this.processedReplays[0].replay.ReplayFrames[j].Time < time;++j)
                {
                }

                Keys current = this.processedReplays[0].replay.ReplayFrames[j].Keys;
                Keys last = this.processedReplays[0].replay.ReplayFrames[j-1].Keys;

                double dist = Utils.dist(this.processedReplays[0].replay.ReplayFrames[j].X, this.processedReplays[0].replay.ReplayFrames[j].Y, miss.note.Location.X, miss.note.Location.Y);

                if (dist - miss.note.Radius > -2)
                {
                    var newFrame = insertReplayFrame(j, (int)time);

                    newFrame.X = miss.note.Location.X;
                    newFrame.Y = miss.note.Location.Y;
                }

                Keys k = Utils.getKey(last, current);

                if (k.HasFlag(Keys.K1))
                {
                    this.processedReplays[0].replay.ReplayFrames[j].Keys = Keys.K2;
                }
                else
                {
                    this.processedReplays[0].replay.ReplayFrames[j].Keys = Keys.K1;
                }
            }
        }

        public void copyReplay()
        {
            fixMisses();
            processedReplays[0].replay.Mods |= Mods.Hidden;
            this.resultingReplay = new Replay(processedReplays[0].replay);
        }

        private void finalizeReplay()
        {
            ReplayPostProcessor replayPostProcessor = new ReplayPostProcessor(resultingReplay);
            resultingReplay = replayPostProcessor.getReplay();
        }

        public Replay exportReplay()
        {
            finalizeReplay();
            return resultingReplay;
        }
    }
}
