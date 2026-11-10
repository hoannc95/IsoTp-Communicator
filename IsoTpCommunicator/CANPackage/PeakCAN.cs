using Peak.Can.Basic.BackwardCompatibility;
using System.Text;

namespace IsoTpCommunicator.CANPackage
{
    // Type alias for a PCAN-Basic channel handle
    using TPCANHandle = System.UInt16;

    public class PeakCAN
    {
        public static TPCANHandle p_PortHandle;
        public static bool isCANFD;

        /// <summary>
        /// Continuously reads messages from the PCAN receive queue until it is empty or an error occurs.
        /// </summary>
        public static void ReadMessages(TPCANHandle m_PcanHandle)
        {
            TPCANStatus stsResult;

            do
            {
                stsResult = ReadMessage(m_PcanHandle);
                
                if (stsResult == TPCANStatus.PCAN_ERROR_ILLOPERATION ||
                    stsResult == TPCANStatus.PCAN_ERROR_BUSHEAVY ||
                    stsResult == TPCANStatus.PCAN_ERROR_BUSOFF)
                {
                    Console.WriteLine($"[CAN Error] Read operation failed with status: {stsResult}");
                    break;
                }

            } while (!Convert.ToBoolean(stsResult & TPCANStatus.PCAN_ERROR_QRCVEMPTY));
        }

        private static TPCANStatus ReadMessage(TPCANHandle m_PcanHandle)
        {
            TPCANStatus stsResult;

            if (isCANFD)
            {
                stsResult = PCANBasic.ReadFD(m_PcanHandle, out TPCANMsgFD canMsg, out ulong canTimeStamp);
                if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                {
                    MainWindow.CAN_msg_queu_rx.TryAdd(new PeakFDMessageWrapper(canMsg));
                }
                return stsResult;
            }
            else
            {
                stsResult = PCANBasic.Read(m_PcanHandle, out TPCANMsg canMsg, out TPCANTimestamp canTimeStamp);
                if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                {
                    MainWindow.CAN_msg_queu_rx.TryAdd(new PeakMessageWrapper(canMsg));
                }
                return stsResult;
            }
        }

        /// <summary>
        /// Writes a Classical CAN (CC) or CAN FD frame to the bus.
        /// </summary>
        public static TPCANStatus WriteCCFrame(uint id, byte[] data, byte dlc)
        {
            if (data == null || data.Length == 0) return TPCANStatus.PCAN_ERROR_ILLPARAMVAL;

            if (isCANFD)
            {
                TPCANMsgFD canMsg = new()
                {
                    DATA = new byte[64],
                    MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
                    ID = id,
                    DLC = dlc
                };

                int copyLength = Math.Min(dlc, data.Length);
                Array.Copy(data, canMsg.DATA, copyLength);

                return PCANBasic.WriteFD(p_PortHandle, ref canMsg);
            }
            else
            {
                TPCANMsg canMsg = new()
                {
                    DATA = new byte[8],
                    MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
                    ID = id,
                    LEN = dlc
                };

                int copyLength = Math.Min(dlc, data.Length);
                Array.Copy(data, canMsg.DATA, copyLength);

                return PCANBasic.Write(p_PortHandle, ref canMsg);
            }
        }

        /// <summary>
        /// Formats the channel name for UI display based on its handle and FD capability.
        /// </summary>
        public static string FormatChannelName(TPCANHandle handle, bool isFD)
        {
            TPCANDevice devDevice;
            byte byChannel;

            if (handle < 0x100)
            {
                devDevice = (TPCANDevice)(handle >> 4);
                byChannel = (byte)(handle & 0xF);
            }
            else
            {
                devDevice = (TPCANDevice)(handle >> 8);
                byChannel = (byte)(handle & 0xFF);
            }

            return isFD 
                ? $"{devDevice}:FD {byChannel} ({handle:X2}h)" 
                : $"{devDevice} {byChannel} ({handle:X2}h)";
        }

        /// <summary>
        /// Configures trace file options for the PCAN channel.
        /// </summary>
        public static void ConfigureTraceFile(TPCANHandle m_PcanHandle)
        {
            // Configure the maximum size of a trace file to 5 megabytes
            UInt32 iBuffer = 5;
            TPCANStatus stsResult = PCANBasic.SetValue(m_PcanHandle, TPCANParameter.PCAN_TRACE_SIZE, ref iBuffer, sizeof(UInt32));
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
            {
                Console.WriteLine(GetFormatedError(stsResult));
            }

            // Configure trace file behavior: Single file, overwrite existing
            iBuffer = PCANBasic.TRACE_FILE_SINGLE | PCANBasic.TRACE_FILE_OVERWRITE;
            stsResult = PCANBasic.SetValue(m_PcanHandle, TPCANParameter.PCAN_TRACE_CONFIGURE, ref iBuffer, sizeof(UInt32));
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
            {
                Console.WriteLine(GetFormatedError(stsResult));
            }
        }

        /// <summary>
        /// Retrieves the translated text description for a given TPCANStatus error code.
        /// </summary>
        public static string GetFormatedError(TPCANStatus error)
        {
            StringBuilder strTemp = new(256);
            if (PCANBasic.GetErrorText(error, 0, strTemp) != TPCANStatus.PCAN_ERROR_OK)
            {
                return $"An error occurred. Error code text (0x{(uint)error:X}) couldn't be retrieved.";
            }
            return strTemp.ToString();
        }

        /// <summary>
        /// Extracts and parses the TPCANHandle from a formatted channel name string safely.
        /// </summary>
        public static TPCANHandle getHandle(string text)
        {
            try
            {
                int startIndex = text.IndexOf('(');
                if (startIndex != -1 && text.Length >= startIndex + 4)
                {
                    string hexPart = text.Substring(startIndex + 1, 3).Replace('h', ' ').Trim();
                    return Convert.ToUInt16(hexPart, 16);
                }
            }
            catch
            {
                // Fallback handle if parsing fails
            }
            return 0;
        }

        /// <summary>
        /// Checks if a given channel supports CAN FD features.
        /// </summary>
        public static bool IsCanFdChannel(TPCANHandle channel)
        {
            TPCANStatus stsResult = PCANBasic.GetValue(channel, TPCANParameter.PCAN_CHANNEL_FEATURES, out uint features, sizeof(uint));
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
            {
                return false;
            }
            return (features & PCANBasic.FEATURE_FD_CAPABLE) == PCANBasic.FEATURE_FD_CAPABLE;
        }
    }
}