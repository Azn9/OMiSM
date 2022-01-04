namespace OMtoSMConverter
{
    public class OsuAddition
    {
        private int TypeRaw { get; set; }
        public int LnEnd { get; private set; }
        public int Volume { get; set; }
        public string KeySound { get; set; }

        private static int TypeDecider(int typeRaw)
        {
            //Questionable, will need changing if parsing non-mania files
            return (typeRaw < 16) ? 1 : typeRaw;
        }

        public static OsuAddition Parse(string additionRawString, int type)
        {
            var oa = new OsuAddition
            {
                TypeRaw = type
            };
            type = TypeDecider(type);
            var parser = additionRawString.Split(":".ToCharArray());
            switch (type)
            {
                case 1:
                    oa.Volume = int.Parse(parser[3]);
                    oa.KeySound = parser[4];
                    break;
                case 128:
                    oa.LnEnd = int.Parse(parser[0]);
                    oa.Volume = int.Parse(parser[4]);
                    oa.KeySound = parser[5];
                    break;
            }

            return oa;
        }

        public string Write()
        {
            var typew = TypeDecider(TypeRaw);
            switch (typew)
            {
                case 1:
                    return string.Join(":", "0", "0", "0", Volume.ToString(), KeySound);
                case 128:
                    return string.Join(":", LnEnd.ToString(), "0", "0", "0", Volume.ToString(), KeySound);

                default:
                    return "";
            }
        }
    }
}