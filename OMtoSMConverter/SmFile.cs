using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OMtoSMConverter
{
    public class SmFile
    {
        public List<Beatmap> AllDiffs { get; }
        private Dictionary<SmSetting, string> HeaderData { get; set; }
        private double StartMs { get; set; }
        public string Errorfile { get; set; }
        public bool HasSubstamce { get; set; }

        //Init
        public SmFile()
        {
            AllDiffs = new List<Beatmap>();
            HeaderData = new Dictionary<SmSetting, string>();
        }

        public SmFile(Beatmap beatmap)
        {
            AllDiffs = new List<Beatmap> {beatmap};
            HeaderData = CreateHeaderData(beatmap);
            StartMs = beatmap.SmStartMs;
        }

        public SmFile(List<Beatmap> beatmaps)
        {
            StartMs = beatmaps.First().SmStartMs;
            AllDiffs = new List<Beatmap>();
            foreach (var beatmap in beatmaps)
            {
                Add(beatmap);
            }
        }

        //Methods
        public void Add(Beatmap beatmap)
        {
            AllDiffs.Add(beatmap);
            if (AllDiffs.Count == 1) StartMs = beatmap.SmStartMs;
            if (beatmap.SmStartMs < StartMs) StartMs = beatmap.SmStartMs;
            StandardizeStartTimes();
        }

        private void StandardizeStartTimes()
        {
            //Force the starting point of all maps to be uniform
            foreach (var beatmap in AllDiffs)
            {
                beatmap.SmStartMs = StartMs;
            }
        }

        public void SetHeaderData(int index = 0)
        {
            HeaderData = CreateHeaderData(AllDiffs[index]);
        }

        //Helpers
        private static Dictionary<SmSetting, string> CreateHeaderData(Beatmap beatmap)
        {
            var headerData = new Dictionary<SmSetting, string>();
            const string numForm = "0.######";
            headerData.Add(SmSetting.Title, beatmap.OMetadata["Title"]);
            headerData.Add(SmSetting.Subtitle, beatmap.OMetadata["Creator"] + " - " + beatmap.OMetadata["Version"]);
            headerData.Add(SmSetting.Artist, beatmap.OMetadata["Artist"]);
            headerData.Add(SmSetting.Titletranslit, "");
            headerData.Add(SmSetting.Subtitletranslit, "");
            headerData.Add(SmSetting.Artisttranslit, "");
            headerData.Add(SmSetting.Genre, beatmap.OMetadata["Tags"]);

            headerData.Add(SmSetting.Credit, beatmap.OMetadata["Creator"]);
            headerData.Add(SmSetting.Banner,
                ""); //FIX THIS SOON? Graphics manipulation maybe? How does graphics even work?

            //FIX THIS IN A BIT, this is not proper at all as it is... I mean, it works?
            string bg;
            try
            {
                bg = beatmap.OEvents[1].Parameters[2];
            }
            catch
            {
                bg = "";
            }

            headerData.Add(SmSetting.Background, bg);
            headerData.Add(SmSetting.Lyricspath, "");
            headerData.Add(SmSetting.Cdtitle, ""); //GODDAMMIT, maybe try 
            headerData.Add(SmSetting.Music, beatmap.OGeneral["AudioFilename"]);
            //See Image
            var mspb = beatmap.OTimingPoints.First().MsPerBeat;
            double bpMeasure = beatmap.BeatsPerMeasure;

            //80 is what seems to be the difference of osu and stuff
            headerData.Add(SmSetting.Offset,
                ((((mspb * bpMeasure) - (beatmap.SmStartMs + 77 + mspb * bpMeasure)) / 1000).ToString(numForm)));
            headerData.Add(SmSetting.Samplestart, (
                (double.Parse(beatmap.OGeneral["PreviewTime"]) >= 0)
                    ? (double.Parse(beatmap.OGeneral["PreviewTime"]) / 1000).ToString(numForm)
                    : "0")); //In case the mapper didn't specify a preview time.
            headerData.Add(SmSetting.Samplelength,
                "20.000000"); //This is completely arbitrary, try getting length of mp3?
            headerData.Add(SmSetting.Selectable, "YES");
            headerData.Add(SmSetting.Bpms, beatmap.RawSmbpMs());
            headerData.Add(SmSetting.Stops, ""); //Because fuck stops
            headerData.Add(SmSetting.Bgchanges, "");
            headerData.Add(SmSetting.Keysounds, ""); // Eventually...
            headerData.Add(SmSetting.Attacks, "");
            return headerData;
        }

        public void AutoSortDiffs()
        {
            //Filter out all non-mania maps
            AllDiffs.RemoveAll(i => i.OGeneral["Mode"] != "3");

            //Create count of all key counts
            var keycountCount = new Dictionary<int, int>();

            //Create something to help us track the diffs as they are assigned 
            var diffsInd = new Dictionary<int, int>();

            //Initialize both dictionaries
            for (var i = 0; i < 11; i++)
            {
                //Because stuff is 0based aaaaa
                keycountCount[i] = -1;
                diffsInd[i] = -1;
            }

            foreach (var beatmap in AllDiffs)
            {
                keycountCount[beatmap.KeyCount] += 1;

                //4 is the challenge, we don't want any higher than that if it can be avoided...
                diffsInd[beatmap.KeyCount] = (keycountCount[beatmap.KeyCount] > 4) ? 5 : 4;
            }

            //Sort AllDiffs by number of notes, this should be an alright indicator of difficulty right?
            //We want the highest number of HO to come first in the list
            AllDiffs.Sort((b1, b2) => b2.OHitObjects.Count().CompareTo(b1.OHitObjects.Count()));

            //Assign each beatmap their indexes! Counting down!
            foreach (var beatmap in AllDiffs)
            {
                beatmap.SmDiffInd = diffsInd[beatmap.KeyCount];

                keycountCount[beatmap.KeyCount] -= 1;

                //Still more than 4 maps left of the same keycount?, still goes in edit
                diffsInd[beatmap.KeyCount] = (keycountCount[beatmap.KeyCount] > 4) ? 5 : diffsInd[beatmap.KeyCount] - 1;
            }

            //Now sort the beatmaps by keycount
            AllDiffs.Sort((b1, b2) => b1.KeyCount.CompareTo(b2.KeyCount));
        }

        //Write Out
        private string GetWholeFile()
        {
            var file = HeaderData.Keys.Aggregate("//This file was created using BilliumMoto's Osu to SM file converter",
                (current, headerItem) => string.Join("", current, Environment.NewLine, "#", headerItem.ToString(), ":",
                    HeaderData[headerItem], ";"));

            file += Environment.NewLine;

            foreach (var diff in AllDiffs.Where(diff => diff.SmKeyType() != ""))
            {
                file = file + Environment.NewLine + diff.SmDiffHeader();
                file += diff.RawSmNotes();
            }

            return file;
        }

        public void WriteEverythingToFolder(Form1 parent)
        {
            if (!HasSubstamce) return;
            //Use the name of the mp3 to separate same-folder different files
            //Oh and clean the unsafe strings
            var fileName = Form1.FilenameSanitize(HeaderData[SmSetting.Title]);
            var audioName = Form1.FilenameSanitize(HeaderData[SmSetting.Music]);
            var creditName = Form1.FilenameSanitize(HeaderData[SmSetting.Credit]);

            //Make new folder
            var newFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\" +
                            Form1.DirnameSanitize(
                                (fileName) +
                                " (" +
                                (audioName.Substring(0, audioName.Count() - 4)) +
                                ") [" +
                                (creditName) +
                                "]");

            var destPath = newFolder + "\\";

            Directory.CreateDirectory(newFolder);

            //Write SM file
            var fileWrite = new StreamWriter(newFolder + "\\" + fileName + ".sm");
            fileWrite.Write(GetWholeFile());
            fileWrite.Close();

            //Copy mp3 and background
            var sourcePath = AllDiffs[0].SourcePath + "\\";
            var bgName = HeaderData[SmSetting.Background];

            try
            {
                if (bgName != "")
                    if (!File.Exists(destPath + bgName))
                        File.Copy(sourcePath + bgName, destPath + bgName, true);

                if (audioName == "") return;

                if (!File.Exists(destPath + audioName))
                    File.Copy(sourcePath + audioName, destPath + audioName, true);
            }
            catch
            {
                //Tell user somehow that files couldn't be copied over?
                parent.BoxInform("Files could not be copied over!");
            }
        }
    }
}