using System.Collections.Generic;

namespace OMtoSMConverter
{
    public class OsuEvent
    {
        public List<string> Parameters { get; }

        public OsuEvent(string rwline)
        {
            Parameters = new List<string>();
            if (!rwline.Contains(","))
            {
                Parameters.Add(rwline);
            }
            else
            {
                Parameters.AddRange(rwline.Split(','));
                switch (Parameters[0])
                {
                    case "0":
                        break;
                    case "Sample":
                        break;
                    case "Sprite":
                        break;
                }

                if (Parameters[2].Contains("\""))
                {
                    Parameters[2] = Parameters[2].Replace("\"", "");
                }
            }
        }

        public OsuEvent(OsuEventType type, int time, int volume, string ksfile)
        {
            //Sample,24,0,"pispl_010.wav",60
            Parameters = new List<string>();
            switch (type)
            {
                case OsuEventType._0:
                    break;
                case OsuEventType.Sprite:
                    break;
                case OsuEventType.Sample:
                    Parameters = new List<string>
                    {
                        "Sample",
                        time.ToString(),
                        "0",
                        $"\"{ksfile}\"", volume.ToString()
                    };
                    break;
                case OsuEventType.Comment:
                    break;
            }
        }

        public override string ToString()
        {
            return string.Join(",", Parameters);
        }
    }
}