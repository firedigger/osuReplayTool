using ReplayAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace osu_replayCombinator
{
    class ReplayPostProcessor
    {
        private Replay inputReplay;

        public ReplayPostProcessor(Replay inputReplay)
        {
            this.inputReplay = inputReplay;
        }

        private string calculateHash()
        {
            //TODO: fix
            //string checkString = "xxdstem luchshiy v mire";
            string checkString = inputReplay.MaxCombo + @"osu" + inputReplay.PlayerName + inputReplay.MapHash + inputReplay.TotalScore + inputReplay.Rank();

            string md5 = CryptoHelper.GetMd5String(checkString);
            if (md5.Length != 32)
                throw new Exception(@"checksum failure");
            return md5;
        }

        public enum Rankings
        {
            XH,
            SH,
            X,
            S,
            A,
            B,
            C,
            D,
            F,
            N
        }

        public static float GetAccuracy(ReplayFile rf)
        {
            float totalhits = rf.Count50 + rf.Count100 + rf.Count300 + rf.CountMiss;
            if (totalhits <= 0) return 0.0f;
             return (rf.Count50 * 50 + rf.Count100 * 100 + rf.Count300 * 300) / (totalhits * 300);
                    
}

        public static Rankings GetRanking(ReplayFile rf)   //ranking
        {
            if (!rf.Passed)
                return Rankings.F;
            float totalhits = rf.Count50 + rf.Count100 + rf.Count300 + rf.CountMiss;
            bool hiddenStd = ContainsMods((Mods)rf.UsedMods, Mods.Hidden | Mods.Flashlight);
            bool hiddenMania = ContainsMods((Mods)rf.UsedMods, Mods.Hidden | Mods.Flashlight | Mods.FadeIn);
            float acc = GetAccuracy(rf);
            float percent50, percent300;

            switch (rf.Mode)
            {
                default:
                case 0:     //Standard
                    percent50 = rf.Count50 / totalhits;
                    if (acc == 1f && rf.FullCombo) return !hiddenStd ? Rankings.X : Rankings.XH;
                    if (acc > 0.9 && percent50 <= 0.01 && rf.FullCombo) return !hiddenStd ? Rankings.S : Rankings.SH;
                    if (acc > 0.8 && rf.FullCombo || acc > 0.9) return Rankings.A;
                    if (acc > 0.7 && rf.FullCombo || acc > 0.8) return Rankings.B;
                    return GetAccuracy(rf) > 0.6 ? Rankings.C : Rankings.D;
                case 1:     //Taiko
                    percent300 = rf.Count300 / totalhits;
                    percent50 = rf.Count50 / totalhits;
                    if (percent300 == 1.0) return !hiddenStd ? Rankings.X : Rankings.XH;
                    if (percent300 > 0.9 && percent50 <= 0.01 && rf.CountMiss == 0) return !hiddenStd ? Rankings.S : Rankings.SH;
                    if (percent300 > 0.8 && rf.CountMiss == 0 || percent300 > 0.9) return Rankings.A;
                    if (percent300 > 0.7 && rf.CountMiss == 0 || percent300 > 0.8) return Rankings.B;
                    return percent300 > 0.6 ? Rankings.C : Rankings.D;
                case 2:     //CtB
                    if (acc == 1.0) return !hiddenStd ? Rankings.X : Rankings.XH;
                    if (acc > 0.98) return !hiddenStd ? Rankings.S : Rankings.SH;
                    if (acc > 0.94) return Rankings.A;
                    if (acc > 0.9) return Rankings.B;
                    return acc > 0.85 ? Rankings.C : Rankings.D;
                case 3:     //Mania
                    if (acc == 1.0) return !hiddenMania ? Rankings.X : Rankings.XH;
                    if (acc > 0.95) return !hiddenMania ? Rankings.S : Rankings.SH;
                    if (acc > 0.9) return Rankings.A;
                    if (acc > 0.8) return Rankings.B;
                    return acc > 0.7 ? Rankings.C : Rankings.D;
            }
        }


        private void postProcessReplay()
        {
            inputReplay.ReplayHash = calculateHash();
        }

        public Replay getReplay()
        {
            postProcessReplay();
            return inputReplay;
        }
    }
}
