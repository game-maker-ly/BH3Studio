using System.Collections.Generic;

namespace AssetStudio
{
    public class WMVFile
    {
        public Dictionary<long, BundleFile> Bundles = new Dictionary<long, BundleFile>();

        public WMVFile(FileReader reader)
        {
            if (reader.BundlePos.Length != 0)
            {
                foreach (var pos in reader.BundlePos)
                {
                    reader.Position = pos;
                    var bundle = new BundleFile(reader);
                    Bundles.Add(pos, bundle);
                }
            }
            else
            {
                long pos = -1;
                while (reader.Position != reader.Length)
                {
                    pos = reader.Position;
                    var bundle = new BundleFile(reader);
                    Bundles.Add(pos, bundle);
                }
            }
        }
    }
}
