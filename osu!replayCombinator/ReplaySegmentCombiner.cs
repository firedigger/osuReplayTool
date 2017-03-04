using ReplayAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace osu_replayCombinator
{
    class ReplaySegmentCombiner
    {
        public double stickError = 0;

        private List<ReplayFrame> segment1;
        private List<ReplayFrame> segment2;
        private List<ReplayFrame> resultingSegment;

        public ReplaySegmentCombiner(List<ReplayFrame> segment1, List<ReplayFrame> segment2)
        {
            //Make sure that segment1 goes before segment2, swap them if that's not true
            if (segment1[0].Time >= segment2[0].Time)
            {
                segment2 = Interlocked.Exchange(ref segment1, segment2);
            }

            //Check that there is a windows between the segments
            if (segment1.Last().Time < segment2.First().Time)
            {
                throw new Exception("The replays do not intersect");
            }

            this.segment1 = segment1;
            this.segment2 = segment2;

            glueReplays();
        }

        private void glueReplays()
        {
            const int replayFrameTimeDifferenceThreshold = 50;

            int segment1StartIndex;
            int segment1EndIndex = segment1.Count - 1;
            int segment2StartIndex = 0;
            int segment2EndIndex;

            for (segment2EndIndex = 0; segment2EndIndex < segment2.Count; ++segment2EndIndex)
            {
                if (segment2[segment2EndIndex].Time > segment1.Last().Time)
                    break;
            }

            for (segment1StartIndex = segment1.Count - 1; segment1StartIndex >= 0; --segment1StartIndex)
            {
                if (segment1[segment1StartIndex].Time < segment2.First().Time)
                    break;
            }

            int segment1GlueIndex = -1;
            int segment2GlueIndex = -1;
            double glueError = double.MaxValue;

            int segment1CurrentIndex = segment1StartIndex;
            int segment2CurrentIndex = segment2StartIndex;
            while(segment1CurrentIndex <= segment1EndIndex && segment2CurrentIndex <= segment2EndIndex)
            {
                for(int segment2CurrentInternalIndex = segment2CurrentIndex;segment2CurrentInternalIndex <= segment2EndIndex; ++segment2CurrentInternalIndex)
                {
                    if (segment2[segment2CurrentInternalIndex].Time - segment1[segment1CurrentIndex].Time > replayFrameTimeDifferenceThreshold)
                        break;

                    double error = Utils.dist(segment1[segment1CurrentIndex],segment2[segment2CurrentInternalIndex]);

                    if (error < glueError)
                    {
                        segment1GlueIndex = segment1CurrentIndex;
                        segment2GlueIndex = segment2CurrentInternalIndex;
                    }

                }
                ++segment1CurrentIndex;

                while(segment1[segment1CurrentIndex].Time - segment2[segment2CurrentIndex].Time > replayFrameTimeDifferenceThreshold)
                {
                    ++segment2CurrentIndex;
                }

            }

            resultingSegment = new List<ReplayFrame>(segment1.GetRange(0, segment1CurrentIndex));
            resultingSegment.AddRange(segment2.GetRange(segment2CurrentIndex, segment2.Count - segment2CurrentIndex + 1));
        }

        public List<ReplayFrame> getResult()
        {
            return resultingSegment;
        }
    }
}
