using Microsoft.Win32;
using ReplayAPI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.IO;
using WinForms = System.Windows.Forms;
using osuDatabase = OsuDbAPI;
using System.Threading.Tasks;
using BMAPI.v1;

namespace osu_replayCombinator
{
    public partial class MainWindow : Window
    {
        private List<Replay> listReplays;
        private Dictionary<string, string> dMapsDatabase;
        private MainControlFrame settings;

        private string sCachedLabelText = string.Empty;
        private string osuFilePath = string.Empty;
        private string mapHash;
        private Beatmap beatMap;

        public MainWindow()
        {
            listReplays = new List<Replay>();
            dMapsDatabase = new Dictionary<string, string>();
            settings = new MainControlFrame();

            try
            {
                InitializeComponent();

                if (File.Exists(settings.pathSettings))
                {
                    settings.LoadSettings();
                }

                if (!string.IsNullOrEmpty(settings.pathSongs) && !string.IsNullOrEmpty(settings.pathOsuDB))
                {
                    ParseDatabaseFile(settings.pathOsuDB, settings.pathSongs);
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("Error!\n" + exp.ToString());
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            settings.saveSettings();
        }

        private void exitButton_Click(object sender, RoutedEventArgs e)
        {

            Close();
        }

        private void loadReplaysButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog replayFileDialog = new OpenFileDialog();
            replayFileDialog.Multiselect = true;
            replayFileDialog.Filter = "osu! replay files|*.osr";
            if (!string.IsNullOrEmpty(settings.pathOsuDB))
                replayFileDialog.InitialDirectory = settings.pathOsuDB;
            if (!string.IsNullOrEmpty(settings.pathReplays))
                replayFileDialog.InitialDirectory = settings.pathReplays;

            if (replayFileDialog.ShowDialog() ?? true)
            {
                bool bErrors = false;
                listReplays = new List<Replay>();

                string log = string.Empty;

                foreach (var replayFile in replayFileDialog.FileNames)
                {
                    try
                    {
                        listReplays.Add(new Replay(replayFile, true, true));
                    }

                    catch (Exception ex)
                    {
                        if (!bErrors)
                        {
                            log = "Some of the replays could not be read:" + Environment.NewLine;
                        }

                        log += string.Format("Failed to process {0} ({1}){2}", replayFile, ex.Message, Environment.NewLine);

                        bErrors = true;
                    }
                }

                if (!bErrors)
                {
                    log = "All replays were successfully read!";
                    if (replayFileDialog.FileNames.Length > 0)
                    {
                        string directoryName = Path.GetDirectoryName(replayFileDialog.FileNames[0]);
                        if (string.IsNullOrEmpty(settings.pathReplays))
                            settings.pathReplays = directoryName;

                    }
                }

                //MessageBox.Show(log);
            }
        }

        private void ParseDatabaseFile(string databasePath, string songsFolder)
        {
            try
            {
                OsuDbAPI.OsuDbFile osuDatabase = new OsuDbAPI.OsuDbFile(databasePath);

                labelTask.Content = "Processing beatmaps from database...";

                Dictionary<string, string> dTempDatabase = new Dictionary<string, string>();

                int iQueue = 0;

                foreach (OsuDbAPI.Beatmap beatMap in osuDatabase.Beatmaps)
                {
                    progressBar.Value = (100.0 / osuDatabase.Beatmaps.Count) * iQueue++;

                    OsuDbAPI.Beatmap dbBeatmap = beatMap;

                    if (!ReferenceEquals(dbBeatmap, null) && !string.IsNullOrEmpty(dbBeatmap.Hash))
                    {
                        string beatmapPath = string.Format("{0}\\{1}\\{2}", songsFolder, dbBeatmap.FolderName, dbBeatmap.OsuFile);
                        if (!dTempDatabase.ContainsKey(dbBeatmap.Hash))
                            dTempDatabase.Add(dbBeatmap.Hash, beatmapPath);
                    }
                }

                progressBar.Value = 100.0;
                dMapsDatabase = dTempDatabase;

                labelTask.Content = "Finished processing beatmaps from osuDB.";
            }
            catch (Exception exp)
            {
                MessageBox.Show("Error reading osuDB \n" + exp.ToString());
            }
        }

        private Beatmap FindBeatmapInDatabase(Replay replay)
        {
            Beatmap beatMap = null;

            if (!string.IsNullOrEmpty(osuFilePath))
            {
                beatMap = new Beatmap(osuFilePath);
            }

            if (!ReferenceEquals(beatMap, null) && replay.MapHash.Equals(beatMap.BeatmapHash))
            {
                return beatMap;
            }

            else
            {
                if (dMapsDatabase.ContainsKey(replay.MapHash))
                {
                    return new Beatmap(dMapsDatabase[replay.MapHash]);
                }
                else
                {
                    return null;
                }
            }
        }

        private bool findmaps()
        {
            if (listReplays.Count > 0)
            {
                bool found = false;

                foreach (Replay replay in listReplays)
                {
                    beatMap = FindBeatmapInDatabase(replay);

                    if (!ReferenceEquals(beatMap, null))
                    {
                        mapHash = beatMap.BeatmapHash;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    MessageBox.Show("Didn't find map");
                    return false;
                }
            }
            else
            {
                MessageBox.Show("Error! No replays selected.");
                return false;
            }
            return true;
        }

        private void combineReplaysButton_Click(object sender, RoutedEventArgs e)
        {
            if (!findmaps())
                return;
            ReplayProcessor replayProcessor = new ReplayProcessor(this.listReplays, beatMap);

            //replayProcessor.connectReplays();
            replayProcessor.copyReplay();

            var result = replayProcessor.exportReplay();

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            if (saveFileDialog.ShowDialog() ?? true)
            {
                result.Save(saveFileDialog.FileName);
            }
        }

        private void openOsuDBbutton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog osuFileDialog = new OpenFileDialog();
            osuFileDialog.Filter = "osu! database file|osu!.db";
            osuFileDialog.Multiselect = false;

            if (osuFileDialog.ShowDialog() ?? true)
            {
                settings.pathOsuDB = osuFileDialog.FileName;
                MessageBox.Show("Now select Songs folder.");

                WinForms.FolderBrowserDialog songsFolderDialogue = new WinForms.FolderBrowserDialog();
                songsFolderDialogue.SelectedPath = Path.GetDirectoryName(osuFileDialog.FileName);

                if (songsFolderDialogue.ShowDialog() == WinForms.DialogResult.OK)
                {
                    settings.pathSongs = songsFolderDialogue.SelectedPath;
                    ParseDatabaseFile(osuFileDialog.FileName, songsFolderDialogue.SelectedPath);
                }
            }
        }
    }

}
