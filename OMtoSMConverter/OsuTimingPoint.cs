using System.Globalization;

namespace OMtoSMConverter
{
    public class OsuTimingPoint
    {
        public double Time { get; private set; }
        public double MsPerBeat { get; private set; }
        private int TimeSig { get; set; }
        private int SType { get; set; }
        private int SSet { get; set; }
        private int Volume { get; set; }
        private int Inherited { get; set; }
        private int Kiai { get; set; }

        public static OsuTimingPoint Parse(string line)
        {
            var tp = new OsuTimingPoint();
            var parser = line.Split(",".ToCharArray());
            //Console.WriteLine(line);
            if (parser.Length != 8)
                return null;

            tp.Time = double.Parse(parser[0]);
            tp.MsPerBeat = double.Parse(parser[1]);
            tp.TimeSig = int.Parse(parser[2]);
            tp.SType = int.Parse(parser[3]);
            tp.SSet = int.Parse(parser[4]);
            tp.Volume = int.Parse(parser[5]);
            tp.Inherited = int.Parse(parser[6]);
            tp.Kiai = int.Parse(parser[7]);
            return tp;
        }

        public bool IsTiming()
        {
            return (Inherited == 1);
        }

        public bool IsInherited()
        {
            return (Inherited == 0);
        }

        public string Write()
        {
            return string.Join(",", Time.ToString(CultureInfo.CurrentCulture), MsPerBeat.ToString(CultureInfo.CurrentCulture), TimeSig.ToString(), SType.ToString(), SSet.ToString(), Volume.ToString(), Inherited.ToString(), Kiai.ToString());
        }
    }
}