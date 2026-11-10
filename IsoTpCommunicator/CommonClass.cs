namespace IsoTpCommunicator
{
    public class IsoTpCanFrame()
    {
        public ushort Len;
        public byte[] Data = new byte[150];

        public IsoTpCanFrame Clone()
        {
            IsoTpCanFrame clone = new IsoTpCanFrame();
            clone.Len = Len;
            clone.Data = (byte[])Data.Clone();
            return clone;
        }
    }
    public enum CAN_CONNECT_TYPE
    {
        NONE,
        PCAN_CC,
        PCAN_FD,
    }
}
