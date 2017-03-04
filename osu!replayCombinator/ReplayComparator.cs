﻿using BMAPI.v1;
using ReplayAPI;
using System;
using System.Collections.Generic;

namespace osu_replayCombinator
{
    public class ReplayComparator
    {
        public Replay newReplay;
        public Replay oldReplay;
        public Beatmap beatmap;

        public ReplayComparator(Replay newReplay, Replay oldReplay, Beatmap beatmap) : this(newReplay, oldReplay)
        {
            this.beatmap = beatmap;
        }

        public ReplayComparator(Replay newReplay, Replay oldReplay)
        {
            this.newReplay = newReplay;
            this.oldReplay = oldReplay;

            if (newReplay.Mods.HasFlag(Mods.HardRock) ^ oldReplay.Mods.HasFlag(Mods.HardRock))
            {
                newReplay.flip();
            }
        }

        public double compareReplays()
        {
            int count = Math.Min(newReplay.ReplayFrames.Count, oldReplay.ReplayFrames.Count);
            var distances = new List<double>();
            for(int i = 0; i < count; ++i)
            {
                ReplayFrame newFrame = newReplay.ReplayFrames[i];
                ReplayFrame oldFrame = oldReplay.ReplayFrames[i];

                distances.Add(Utils.dist(newFrame.X, newFrame.Y, oldFrame.X, oldFrame.Y));
            }

            distances.Sort();

            return Utils.median(distances);
        }
    }
}
