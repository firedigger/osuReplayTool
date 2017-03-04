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
