using System;

namespace OMtoSMConverter
{
    public class OsuHitObject
    {
        public int Xpos { get; private set; }
        private int YPos { get; set; }
        public int Time { get; private set; }
        public int Type { get; private set; }
        private int HitSound { get; set; }
        public OsuAddition Addition { get; private set; }

        private OsuHitObject()
        {
            Addition = new OsuAddition();
        }

        public static OsuHitObject Parse(string line)
        {
            var ho = new OsuHitObject();
            try
            {
                var parser = line.Split(",".ToCharArray());
                if (parser.Length != 6)
                    return null;

                ho.Xpos = int.Parse(parser[0]);
                ho.YPos = int.Parse(parser[1]);
                ho.Time = int.Parse(parser[2]);
                ho.Type = int.Parse(parser[3]);
                ho.HitSound = int.Parse(parser[4]);
                ho.Addition = OsuAddition.Parse(parser[5], ho.Type);
                return ho;
            }
            catch (Exception)
            {
                Console.WriteLine(@"Error here");

                throw;
            }
        }

        public void KsCopy(OsuHitObject ks)
        {
            //there is an actual wav? copy that
            if (ks.Addition.KeySound != "")
            {
                Addition.KeySound = ks.Addition.KeySound;
            }
            else
            {
                HitSound = ks.HitSound;
            }

            Addition.Volume = ks.Addition.Volume;
        }

        public string Write()
        {
            return string.Join(",", Xpos.ToString(), YPos.ToString(), Time.ToString(), Type.ToString(), HitSound.ToString(), Addition.Write());
        }
    }
}