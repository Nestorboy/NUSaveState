namespace Nessie.Udon.SaveState.Data
{
    public class DataConstants
    {
        public const int BITS_PER_PAGE = 16 * 16; // Bits per bone * bone count.
        public const int BYTES_PER_PAGE = BITS_PER_PAGE / 8;
        public const int MAX_PAGE_COUNT = 256;
        public const int MAX_BIT_COUNT = BITS_PER_PAGE * MAX_PAGE_COUNT;
        public const string DEFAULT_PARAMETER_NAME = "parameter";
    }
}
