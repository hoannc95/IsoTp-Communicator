using Peak.Can.Basic.BackwardCompatibility;

namespace IsoTpCommunicator.CANPackage
{
    public interface ICANMessage
    {
        uint ID { get; set; }
        byte DLC { get; set; }
        byte[] DATA {  get; set; }
    }
    public class PeakMessageWrapper : ICANMessage
    {
        private TPCANMsg _msg;
        public PeakMessageWrapper(TPCANMsg msg) => _msg = msg;
        public uint ID { get => _msg.ID; set => _msg.ID = value; }
        public byte DLC { get => _msg.LEN; set => _msg.LEN = value; }
        public byte[] DATA { get => _msg.DATA; set => _msg.DATA = value; }
    }
    public class PeakFDMessageWrapper : ICANMessage
    {
        private TPCANMsgFD _msg;
        public PeakFDMessageWrapper(TPCANMsgFD msg) => _msg = msg;
        public uint ID { get => _msg.ID; set => _msg.ID = value; }
        public byte DLC { get => _msg.DLC; set => _msg.DLC = value; }
        public byte[] DATA { get => _msg.DATA; set => _msg.DATA = value; }
    }
}
