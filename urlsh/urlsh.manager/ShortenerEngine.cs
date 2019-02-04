using HashidsNet;

namespace urlsh.manager
{
    public class ShortenerEngine
    {
        Hashids h;
        public ShortenerEngine(string salt)
        {
            h = new Hashids(salt, 6);
        }
        public string Encode(long i)
        {
            return h.EncodeLong(i);
        }
        public long Decode(string s)
        {
            return h.DecodeLong(s)[0];
        }
    }
}
