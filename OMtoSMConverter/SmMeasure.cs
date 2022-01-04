using System;
using System.Collections.Generic;
using System.Linq;

namespace OMtoSMConverter
{
    public class SmMeasure
    {
        //Constants
        private readonly List<int> _smQuants;

        //Basics
        public int MeasureNum { get; set; }
        public int KeyCount { get; set; }

        private int Quant { get; set; }

        //the int is the index of the line of the measure
        public Dictionary<int, SmNote> SmNotes { get; private set; }

        //If two of the following three are assigned the third is automatically calculated
        private double _iStartTime;

        public double StartTime
        {
            get => _iStartTime;
            set
            {
                _iStartTime = value;
                if (Math.Abs(_iEndTime - (-1)) > 0.1f)
                {
                    _iBpm = (60000 * 4 / (_iEndTime - _iStartTime));
                }
                else if (Math.Abs(_iBpm - (-1)) > 0.1f)
                {
                    _iEndTime = (60000 * 4 / _iBpm) + _iStartTime;
                }
            }
        }

        private double _iEndTime;

        public double EndTime
        {
            get => _iEndTime;
            set
            {
                _iEndTime = value;
                if (Math.Abs(_iStartTime - (-1)) > 0.1f)
                {
                    _iBpm = (60000 * 4 / (_iEndTime - _iStartTime));
                }
                else if (Math.Abs(_iBpm - (-1)) > 0.1f)
                {
                    _iStartTime = _iEndTime - (60000 * 4 / _iBpm);
                }
            }
        }

        private double _iBpm;

        private double Bpm
        {
            set
            {
                _iBpm = value;
                if (Math.Abs(_iStartTime - (-1)) > 0.1f)
                {
                    _iEndTime = (60000 * 4 / _iBpm) + _iStartTime;
                }
                else if (Math.Abs(_iEndTime - (-1)) > 0.1f)
                {
                    _iStartTime = _iEndTime - (60000 * 4 / _iBpm);
                }
            }
        }

        public SmMeasure()
        {
            SmNotes = new Dictionary<int, SmNote>();
            StartTime = -1;
            EndTime = -1;
            Bpm = -1;
            _smQuants = new List<int> {4, 8, 12, 16, 24, 32, 48, 64, 96, 192};
        }

        public void FillMeasurefromOsu(List<OsuHitObject> oHOinMeasure, List<Tuple<int, int>> lnEnds)
        {
            //We need two of the three optional parameters to work
            SmNotes = new Dictionary<int, SmNote>();
            //if (iStartTime != -1 && iEndTime >= 0 && iBPM >= 0)
            //Not sure how to deal with the variability of iStartTime here...
            if (_iEndTime >= 0 && _iBpm >= 0)
            {
                //First, get best quant of all notes and LNEnds in measure, call it timesList
                //Then, assign sm notes
                //Not the most clever, but it's easy
                //LNEnds tuple in format <xpos, LNEndTime>
                var timesList = new List<int>();

                //LNEnds are treated as a separate entity and must be sorted
                lnEnds.Sort((p1, p2) => p1.Item2.CompareTo(p2.Item2));

                //Get all the times into the timesList
                timesList.AddRange(oHOinMeasure.Select(i => i.Time));
                timesList.AddRange(lnEnds.Select(i => i.Item2));
                timesList.Sort();

                //Fix all times to span 0 to 1 in timesListNorm
                var timesListZeroed = timesList.Select(time => (time - StartTime)).ToList();

                //Get the best quantization
                Quant = BestQuant(timesListZeroed, EndTime - StartTime);
                var quantTime = (EndTime - StartTime) / Quant;

                //Assign SM Notes to Quant
                var hoQuene = new Queue<OsuHitObject>(oHOinMeasure);
                var lnEndsQueue = new Queue<Tuple<int, int>>(lnEnds);

                //This does stuff?
                const double precisionTemp = 2;
                for (var i = 0; i < Quant; i++)
                {
                    var currTime = StartTime + i * quantTime;
                    var note = new SmNote(KeyCount);
                    if (hoQuene.Count > 0)
                    {
                        //This boolean is to see if we accidentally passed a note
                        var notePassed = hoQuene.Peek().Time < currTime;
                        while (AlmostEqual(hoQuene.Peek().Time, currTime, precisionTemp) || notePassed)
                        {
                            var ho = hoQuene.Dequeue();
                            note.OsuXtoNote(ho.Xpos, ho.Type == 128 ? "2" : "1");

                            if (!(hoQuene.Count > 0))
                            {
                                break;
                            }

                            notePassed = hoQuene.Peek().Time < currTime;
                        }
                    }

                    if (lnEndsQueue.Count > 0)
                    {
                        var notePassed = lnEndsQueue.Peek().Item2 < currTime;
                        while (AlmostEqual(lnEndsQueue.Peek().Item2, currTime, precisionTemp) || notePassed)
                        {
                            note.OsuXtoNote(lnEndsQueue.Dequeue().Item1, "3");
                            if (!(lnEndsQueue.Count > 0)) break;
                            notePassed = lnEndsQueue.Peek().Item2 < currTime;
                        }
                    }

                    SmNotes.Add(i, note);
                }

                if (hoQuene.Count > 0)
                    Console.WriteLine(@"Leftover HOs detected at end of measure. Time : " +
                                      hoQuene.Peek().Time);
                if (lnEndsQueue.Count > 0)
                    Console.WriteLine(@"Leftover LNEnds detected at end of measure. Time : " +
                                      lnEndsQueue.Peek().Item2);


                //Clever way : look for best quant as writing all timestamps for sm notes. 
                //Convert all timestamps to quant# after best quant is found
                //Actually neither is faster on immediate analysis. So we go with the easier.
            }
            else
            {
                Console.Write(@"Failed Measure Conversion: ");
                Console.WriteLine(MeasureNum);
            }
        }

        //Quantizing Tools
        private static bool AlmostEqual(double x1, double x2, double precision = 1)
        {
            return AlmostZero(x1 - x2, precision);
        }

        private static bool AlmostZero(double x, double precision = 1)
        {
            return (Math.Abs(x) < precision);
        }

        private int BestQuant(List<double> times, double measureLength)
        {
            foreach (var quant in _smQuants)
            {
                //Here we make a somewhat arbitrary decision, we'll see if it works later
                //The precision of osu beatmaps is 1ms, so we should keep this 1ms, right?
                //precision = 1.5 / quant;
                double precisionTemp = 2;
                var testLength = measureLength / quant;
                //if (Times.All(time => AlmostZero(time % testLength, precisionTemp)))
                //Clever, but too hard to debug.
                var foundBest = times.Select(time => time % testLength)
                    .Select(isitZero => (Math.Abs(isitZero) > Math.Abs(isitZero - testLength))
                        ? isitZero - testLength
                        : isitZero)
                    .Aggregate(true, (current, isitZero) => current & AlmostZero(isitZero, precisionTemp));

                if (foundBest)
                {
                    //Console.WriteLine(quant);
                    return quant;
                }
            }

            return _smQuants.Last();
        }
    }
}