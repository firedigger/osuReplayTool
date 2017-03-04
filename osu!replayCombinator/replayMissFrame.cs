using BMAPI.v1.HitObjects;
using ReplayAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace osu_replayCombinator
{
    public class ReplayMissFrame
    {
        private ReplayFrame replayFrame;
        public CircleObject note;
        public int segmentIndex;

        public ReplayMissFrame(ReplayFrame replayFrame, int segmentIndex, CircleObject hitObject)
        {
            this.replayFrame = replayFrame;
            this.segmentIndex = segmentIndex;
            this.note = hitObject;
        }
    }
}
