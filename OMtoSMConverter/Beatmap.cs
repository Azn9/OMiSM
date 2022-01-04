using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OMtoSMConverter
{
    public class Beatmap
    {
        //General Parts
        private double KeyCountRaw { get; set; }
        public int KeyCount { get; private set; }
        public int BeatsPerMeasure { get; }
        public string SourcePath { get; private set; }
        public string AudioPath { get; private set; }

        //Osu Parts
        public Dictionary<string, string> OGeneral { get; }
        private Dictionary<string, string> OEditor { get; }
        public Dictionary<string, string> OMetadata { get; }
        private Dictionary<string, string> ODifficulty { get; }
        public List<OsuEvent> OEvents { get; }
        public List<OsuTimingPoint> OTimingPoints { get; }
        public List<OsuHitObject> OHitObjects { get; private set; }

        //Stepmania Parts
        private List<SmMeasure> SmMeasures { get; }
        private Dictionary<double, double> SmBpMs { get; set; }
        public double SmStartMs { get; set; }
        public int SmDiffInd { get; set; }

        public string SmKeyType()
        {
            return SmKeyNames[KeyCount];
        }

        public string SmDiffHeader()
        {
            var diff = (SmDiff) SmDiffInd;
            var thingy = string.Join(Environment.NewLine,
                             "//---------------" + SmKeyType() + " - ----------------",
                             "#NOTES:") +
                         string.Join(Environment.NewLine + "     ",
                             "",
                             SmKeyType() + ":",
                             ":",
                             diff + ":",
                             "1:",
                             "",
                             "0.000,0.000,0.000,0.000,0.000:"); //No clue what this last one is

            return thingy;
        }

        //Constants, a few checks are done based on whether or not strings in this array are empty ("")
        private static readonly string[] SmKeyNames = {
            "", "", "", "",
            "dance-single",
            "pump-single",
            "dance-solo",
            "kb7-single",
            "dance-double",
            "", //Find pms header name
            "pump-double"
        };

        //Init
        private Beatmap()
        {
            OGeneral = new Dictionary<string, string>();
            OEditor = new Dictionary<string, string>();
            OMetadata = new Dictionary<string, string>();
            ODifficulty = new Dictionary<string, string>();
            OEvents = new List<OsuEvent>();
            OTimingPoints = new List<OsuTimingPoint>();
            OHitObjects = new List<OsuHitObject>();
            SmMeasures = new List<SmMeasure>();
            BeatsPerMeasure = 4;

            //Replace
            SmDiffInd = 4;
        }

        //Read in
        public static Beatmap GetRawOsuFile(string filename)
        {
            //Variables we need
            var osuFile = new StreamReader(filename);
            var placeHold = new Beatmap();
            string line;
            var osuField = "";
            placeHold.SourcePath = Path.GetDirectoryName(filename);

            //Loop Through file by line
            while ((line = osuFile.ReadLine()) != null)
            {
                //First get the raw line
                line = line.Trim();

                //Get settings and all osu data
                var setting = ParseOsuSetting(line);
                if (line.Contains("[") && !line.Contains(":"))
                {
                    osuField = line;
                }
                else
                {
                    switch (osuField)
                    {
                        case "[General]":
                            placeHold.OGeneral.Add(setting[0], setting[1]);
                            break;
                        case "[Editor]":
                            placeHold.OEditor.Add(setting[0], setting[1]);
                            break;
                        case "[Metadata]":
                            placeHold.OMetadata.Add(setting[0], setting[1]);
                            break;
                        case "[Difficulty]":
                            placeHold.ODifficulty.Add(setting[0], setting[1]);
                            break;
                        case "[Events]":
                            placeHold.OEvents.Add(new OsuEvent(line));
                            break;
                        case "[TimingPoints]":
                            var tp = OsuTimingPoint.Parse(line);
                            if (tp != null)
                                placeHold.OTimingPoints.Add(tp);
                            break;
                        case "[HitObjects]":
                            var ho = OsuHitObject.Parse(line);
                            if (ho != null)
                                placeHold.OHitObjects.Add(ho);
                            break;
                    }
                }
            }

            //File Read Done
            osuFile.Close();
            placeHold.KeyCountRaw = double.Parse(placeHold.ODifficulty["CircleSize"]);
            placeHold.KeyCount = (int) placeHold.KeyCountRaw;
            placeHold.AudioPath = placeHold.SourcePath + "\\" + placeHold.OGeneral["AudioFilename"];
            placeHold.OTimingPoints.Sort((x1, x2) => x1.Time.CompareTo(x2.Time));
            placeHold.OHitObjects.Sort((x1, x2) => x1.Time.CompareTo(x2.Time));

            //In case the mapper is an idiot and makes the first timing point later in the beatmap
            //we need to make our first measure begins at/before the first note.
            //We will make it so it doesn't start too far behind the first note or else we can get weird stuff happening
            try
            {
                var realStartMs = placeHold.OTimingPoints.First().Time;
                while (realStartMs <= placeHold.OHitObjects.First().Time)
                {
                    realStartMs += placeHold.BeatsPerMeasure * placeHold.OTimingPoints.First().MsPerBeat;
                }

                while (realStartMs >= placeHold.OHitObjects.First().Time)
                {
                    realStartMs -= placeHold.BeatsPerMeasure * placeHold.OTimingPoints.First().MsPerBeat;
                }

                placeHold.SmStartMs = realStartMs;
            }
            catch (InvalidOperationException)
            {
                //No hitobjects found... can't really do much about it
            }

            //We will need this value later
            //Lets try making the BPMs along with the notes
            //placeHold.smBPMs = getSMBPMsfromTP(placeHold.oTimingPoints, starttime: realStartMS);
            placeHold.SmBpMs = new Dictionary<double, double>();
            return placeHold;
        }

        //Conversions
        public void FillSMfromOsu()
        {
            //Divide all hitobjects up into measures, keeping their LN ends stored for separate entry.
            var queueHo = new Queue<OsuHitObject>(OHitObjects);

            //We only want the uninherited timing points for now
            var queueTp = new Queue<OsuTimingPoint>(OTimingPoints.FindAll(p => p.IsTiming()));

            //Initialize stuff
            var currMeasureNum = -1;
            var lnEnds = new List<Tuple<int, int>>();
            var currMeasureEnd = SmStartMs;

            //Make the first BPM
            var currTp = queueTp.Dequeue();
            SmBpMs.Add(0, MspBtoBpm(currTp.MsPerBeat));


            //We need to split osu up into measures here...
            //This outer while loop should loop once per measure.
            while (queueHo.Any() || lnEnds.Any())
            {
                //We need to get a list of all HO in the measure, the list of all LNEnds in the measure
                //measureNumber, start/end/BPM, key count to make a Stepmania measure
                //Reinitialize everything
                var currMeasureHo = new List<OsuHitObject>();
                currMeasureNum++;
                var currMeasureStart = currMeasureEnd;
                currMeasureEnd = currMeasureStart + (BeatsPerMeasure * currTp.MsPerBeat);

                //This fudge factor ensures that the first note of the next measure isn't cut off here
                var currFudgeMeasureEnd = currMeasureEnd - (currTp.MsPerBeat / 192) * 4;

                //Check if there is a new TP in the way and adjust for it, and add to smBPMs
                //We don't use the fudge factor here since we're moving through a continuous time space
                if (queueTp.Any())
                {
                    if (queueTp.Peek().Time <= currMeasureEnd + 1)
                    {
                        currTp = queueTp.Dequeue();

                        //Special BPM Considerations, code copied from original smBPMs retrival method
                        var pdequeue = currTp;
                        var gap = (pdequeue.Time - currMeasureStart);
                        if (gap == 0) gap = pdequeue.MsPerBeat * BeatsPerMeasure;

                        //Change the "current" measure
                        var gapBpm = MspBtoBpm(gap / BeatsPerMeasure);
                        if (SmBpMs.ContainsKey(currMeasureNum)) SmBpMs[currMeasureNum] = gapBpm;
                        else SmBpMs.Add(currMeasureNum, gapBpm);

                        //Create the next BPM point
                        SmBpMs.Add(currMeasureNum + 1, MspBtoBpm(pdequeue.MsPerBeat));

                        currMeasureEnd = currTp.Time;
                        currFudgeMeasureEnd = currMeasureEnd - (currTp.MsPerBeat / 192) * 4;
                    }
                    //No TPs left? The currTP will reign until the end!
                }


                //We now have measure num, start/end/Key count. We will do without BPM for now.
                //We need the HO list and LNEnds
                if (queueHo.Any())
                {
                    while (queueHo.Peek().Time < currFudgeMeasureEnd)
                    {
                        var currHo = queueHo.Dequeue();
                        currMeasureHo.Add(currHo);

                        //Tuple in format <xpos, LNEndTime>
                        if (currHo.Addition.LnEnd != 0) lnEnds.Add(Tuple.Create(currHo.Xpos, currHo.Addition.LnEnd));
                        
                        if (!queueHo.Any()) break;
                    }
                }

                //We have HO list now and full queue of LNEnds. We get all LNEnds that end before measure end.
                var currMeasureLnEnds = lnEnds.Where(lnEnd => lnEnd.Item2 < currFudgeMeasureEnd).ToList();

                //Remove all LNEnds that we took from the list
                foreach (var lnEnd in currMeasureLnEnds)
                {
                    lnEnds.Remove(lnEnd);
                }

                //We now have everything we need to make a measure.
                var toBeAdded = new SmMeasure
                {
                    KeyCount = KeyCount,
                    StartTime = currMeasureStart,
                    EndTime = currMeasureEnd,
                    MeasureNum = currMeasureNum
                };
                toBeAdded.FillMeasurefromOsu(currMeasureHo, currMeasureLnEnds);
                SmMeasures.Add(toBeAdded);
            }
        }

        //Copy Hitsounds/Keysounds
        public void CopyHs(Beatmap source)
        {
            //Make sure time is ascending
            source.OHitObjects.Sort((a, b) => a.Time.CompareTo(b.Time));
            OHitObjects.Sort((a, b) => a.Time.CompareTo(b.Time));
            var sHo = new Queue<OsuHitObject>(source.OHitObjects);
            var dHo = new Queue<OsuHitObject>(OHitObjects);

            var toSb = new List<OsuHitObject>();
            OHitObjects = CopyHSrec(sHo, dHo, toSb);
            OHitObjects.Reverse();

            //Put these into SB
            foreach (var sbSound in toSb)
            {
                OEvents.Add(new OsuEvent(OsuEventType.Sample,
                    sbSound.Time,
                    sbSound.Addition.Volume,
                    sbSound.Addition.KeySound));
            }
        }

        private List<OsuHitObject> CopyHSrec(Queue<OsuHitObject> sHo, Queue<OsuHitObject> dHo, List<OsuHitObject> toSb)
        {
            //OK so recursion is really slow? Eh, it's aight
            //Returns the modified list of hit objects that the recursion has looped through, by pure virtue of time comparisons
            //First, the base cases
            if (sHo.Count == 0 || dHo.Count == 0)
            {
                // anything remaining in source means it goes into SB
                while (sHo.Count > 0)
                {
                    toSb.Add(sHo.Dequeue());
                }

                //remainder in dHO goes into list unmodified, return the remainder, must be reversed
                var hold = new List<OsuHitObject>(dHo);
                hold.Reverse();
                return hold;
            }
            // next keysound hasn't been reached yet, need to remove note from dHO

            if (sHo.Peek().Time > dHo.Peek().Time)
            {
                //The add method throws list into reverse
                var rem = dHo.Dequeue();
                var hold = CopyHSrec(sHo, dHo, toSb);
                hold.Add(rem);
                return hold;
            }
            // keysound has been passed, need to put into SB

            if (sHo.Peek().Time < dHo.Peek().Time)
            {
                toSb.Add(sHo.Dequeue());
                return CopyHSrec(sHo, dHo, toSb);
            }
            // times match, we want to assign ks by column (xpos) and destination by randomly for the time

            {
                var time = sHo.Peek().Time;
                // Find all stuff in source at this time
                var sourceKs = new List<OsuHitObject>();
                while (sHo.Count > 0 && sHo.Peek().Time == time)
                {
                    sourceKs.Add(sHo.Dequeue());
                }

                //Order keysounds by xpos, convert to queue
                sourceKs.Sort((a, b) => a.Xpos.CompareTo(b.Xpos));
                var tKs = new Queue<OsuHitObject>(sourceKs);

                // Find all stuff in dest at this time
                var tDest = new List<OsuHitObject>();
                while (dHo.Count > 0 && dHo.Peek().Time == time)
                {
                    tDest.Add(dHo.Dequeue());
                }

                //random number generator
                var rng = new Random();

                //container for done dest hs
                var copiedHo = new List<OsuHitObject>();

                //go until source keysounds exhausted
                while (tKs.Count > 0)
                {
                    //find the source KS and the random note to copy it to, remove from both collections
                    var ks = tKs.Dequeue();

                    //if there is still a destination to copy to
                    if (tDest.Count > 0)
                    {
                        var indHo = rng.Next(0, tDest.Count);
                        var to = tDest[indHo];
                        tDest.RemoveAt(indHo);

                        //do the copy
                        to.KsCopy(ks);

                        //finalize and collect the completed copied HO
                        copiedHo.Add(to);
                    }
                    else
                    {
                        // no more destinations? current ks and rest of tKS goes into toSB
                        toSb.Add(ks);
                        toSb.AddRange(tKs);
                        tKs.Clear();
                    }
                }

                // Complete by emptying the dest HOs
                copiedHo.AddRange(tDest);

                // Finish recursive loop
                var hold = CopyHSrec(sHo, dHo, toSb);
                hold.AddRange(copiedHo);
                return hold;
            }
        }

        //Helpers
        private static string[] ParseOsuSetting(string line)
        {
            var sets = new string[2];
            var x = line.IndexOf(":", StringComparison.Ordinal);
            x = (x >= 0) ? x : 0;
            sets[0] = line.Substring(0, x);
            try
            {
                sets[1] = line.Substring(x + 1).Trim();
            }
            catch
            {
                sets[1] = "";
            }

            return sets;
        }

        private static double MspBtoBpm(double mspb)
        {
            return (60000 / mspb);
        }

        public void Backup(string dest)
        {
            var old = OMetadata["Version"];
            OMetadata["Version"] = old + " backup";
            WriteOut(dest);
            OMetadata["Version"] = old;
        }

        //This is the place to do BPM smoothing if we do any. Assumes BeatsPerMeasure = 4
        public static Dictionary<double, double> GetSmbpMsfromTp(List<OsuTimingPoint> allTPs, double starttime = 0)
        {
            //Initialize with the first and required BPM point
            //However, due to our algorithm, being the Beat #, the key always should be an integer.
            //The value in the dict is the BPM
            //Because BPMs and everything in SM is done in measures, our BPM metrics will need to be converted to Measures
            //MSPB = Milliseconds per beat // MSPM = Milliseconds per measure
            var smBpMs = new Dictionary<double, double>();
            const int beatsPerMeasure = 4; //Should be 4?
            double measureNum = 0;
            var currMspm = allTPs.First().MsPerBeat * beatsPerMeasure;

            //We will sweep through the time space of the entire map, assigning BPMs according to not only the value of the timing point but also their location
            var currTime = starttime;

            //Add the first BPM point
            smBpMs.Add(measureNum, MspBtoBpm(currMspm / beatsPerMeasure));

            //Keep only true timing points and create a Queue
            var ps = new Queue<OsuTimingPoint>(allTPs.FindAll(p => p.IsTiming()));

            //Discard timing points before the beginning of our file, with a conservative 1ms buffer
            while (ps.Peek().Time < starttime - 1) ps.Dequeue();

            while (ps.Count > 0)
            {
                //If the beat ends before the next timing point, don't do anything. Just move up in the time space.
                //This comparison also uses a 1 ms buffer to account for stupid osu rounding
                if (currTime + currMspm + 1 <= ps.Peek().Time)
                {
                    measureNum++;
                    currTime += currMspm;
                }

                //Otherwise, we will need to change the bpm of this measure so the whole measure will fit before the next one.
                //This requires changing the BPM of the previously determined measure to fit the gap perfectly as well as creating the next.
                else
                {
                    //Save this TP to be worked with
                    var pdequeue = ps.Dequeue();

                    var gap = (pdequeue.Time - currTime);
                    if (gap == 0) gap = pdequeue.MsPerBeat * beatsPerMeasure;

                    //Change the previous measure
                    var gapBpm = MspBtoBpm(gap / beatsPerMeasure);
                    if (smBpMs.ContainsKey(measureNum)) smBpMs[measureNum] = gapBpm;
                    else smBpMs.Add(measureNum, gapBpm);

                    //Create the next BPM point
                    currTime = pdequeue.Time;
                    currMspm = pdequeue.MsPerBeat * beatsPerMeasure;
                    measureNum++;
                    smBpMs.Add(measureNum, MspBtoBpm(pdequeue.MsPerBeat));
                }
            }

            //We're done!
            return smBpMs;
        }

        //Write Out
        public string RawSmNotes()
        {
            var rawNotes = Environment.NewLine;
            var measureQueue = new Queue<SmMeasure>(SmMeasures);
            
            while (measureQueue.Count > 0)
            {
                var measure = measureQueue.Dequeue();
                //Comment beginning of measure 
                rawNotes = rawNotes + "  //  Measure " + measure.MeasureNum;
                rawNotes = measure.SmNotes.Keys.Aggregate(rawNotes, (current, quant) => current + Environment.NewLine + measure.SmNotes[quant].MakeRawNoteLine());

                //Mark end of measure, unless it's the last measure.
                rawNotes += Environment.NewLine;
                if (measureQueue.Count > 0)
                    rawNotes += ",";
                else
                    rawNotes += ";";
            }

            return rawNotes;
        }

        public string RawSmbpMs()
        {
            var rawBpMs = "";
            //this originally involved measurenum * BeatsperMeasure
            foreach (var beatnum in SmBpMs.Keys)
            {
                if (rawBpMs != "") rawBpMs += Environment.NewLine + ",";
                rawBpMs += string.Join("",
                    (beatnum * BeatsPerMeasure).ToString("0.######"),
                    "=",
                    SmBpMs[beatnum].ToString(CultureInfo.CurrentCulture));
            }

            return rawBpMs;
        }

        private string EntireOsuFile()
        {
            var file = "osu file format v14";
            var nl = Environment.NewLine;
            file = file +
                   nl + nl + "[General]" + DictReduce(OGeneral) +
                   nl + nl + "[Editor]" + DictReduce(OEditor) +
                   nl + nl + "[Metadata]" + DictReduce(OMetadata) +
                   nl + nl + "[Difficulty]" + DictReduce(ODifficulty) +
                   nl + nl + "[Events]" + nl + string.Join(nl, OEvents.ConvertAll(a => a.ToString())) +
                   nl + nl + "[TimingPoints]" + nl + string.Join(nl, OTimingPoints.ConvertAll(a => a.Write())) +
                   nl + nl + "[HitObjects]" + nl + string.Join(nl, OHitObjects.ConvertAll(a => a.Write()))
                ;

            return file;
        }

        private static string DictReduce(Dictionary<string, string> dict)
        {
            return DictReduce(dict, ": ", Environment.NewLine);
        }

        private static string DictReduce(Dictionary<string, string> dict, string pairDelim, string keyDelim)
        {
            return dict.Keys.Aggregate("", (current, key) => current + keyDelim + key + pairDelim + dict[key]);
        }

        public void WriteOut(string folder)
        {
            var filename = OMetadata["ArtistUnicode"] + " - " +
                           OMetadata["TitleUnicode"] + $" ({OMetadata["Creator"]}) [{OMetadata["Version"]}].osu";

            var fileWrite = new StreamWriter(folder + "\\" + filename);
            fileWrite.Write(EntireOsuFile());
            fileWrite.Close();
        }
    }
}