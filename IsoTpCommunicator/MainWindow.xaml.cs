using IsoTp.CANBus.Net;
using IsoTpCommunicator.CANPackage;
using Peak.Can.Basic.BackwardCompatibility;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;

namespace IsoTpCommunicator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Thread-safe queue for incoming CAN messages
        public static readonly BlockingCollection<ICANMessage> CAN_msg_queu_rx = new BlockingCollection<ICANMessage>(
            new ConcurrentQueue<ICANMessage>(),
            boundedCapacity: 10000
        );

        private Thread? m_ReadThread;
        private Thread? m_ProcessingThread;
        private ReadDelegateHandler? m_ReadDelegate;
        internal static CancellationTokenSource? m_ReadCancellationTokenSource;

        private readonly AutoResetEvent m_ReceiveEvent = new AutoResetEvent(false);
        private delegate void ReadDelegateHandler();

        private CAN_CONNECT_TYPE _connectionState = CAN_CONNECT_TYPE.NONE;
        private static MainWindow? _mainWindow;

        // ISO-TP and CAN Driver instances
        private readonly ICanDriver _canDriver;
        private IsoTpProcessor _isoTpProcessor;

        public MainWindow()
        {
            _mainWindow = this;
            InitializeComponent();

            // Initialize CAN Driver and ISO-TP Processor with default UI values
            _canDriver = new ICanDriver();
            _canDriver.SendCanMessage = (id, data, dlc) =>
            {
                PeakCAN.WriteCCFrame(id, data, dlc);
            };

            InitializeIsoTpProcessor();
        }

        /// <summary>
        /// Re-initializes or updates the IsoTpProcessor based on UI inputs safely.
        /// </summary>
        private void InitializeIsoTpProcessor()
        {
            try
            {
                uint physId = ParseHexInput(txt_PhysicalId?.Text, 0x701);
                uint funcId = ParseHexInput(txt_FuncId?.Text, 0x7DF);
                uint resId = ParseHexInput(txt_ResId?.Text, 0x7C1);

                IsoTpParams udsParams = new IsoTpParams(physId, resId); // Using standard Tx/Rx mapping
                _isoTpProcessor = new IsoTpProcessor(udsParams, _canDriver);
                _isoTpProcessor.OnMessageReceived += IsoTpProcessor_OnMessageReceived;
            }
            catch (Exception ex)
            {
                AddText2Box($"[Error] Failed to initialize ISO-TP parameters: {ex.Message}");
            }
        }

        private uint ParseHexInput(string? text, uint defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text)) return defaultValue;
            string cleanText = text.Trim();
            if (uint.TryParse(cleanText.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? cleanText[2..] : cleanText, NumberStyles.HexNumber, null, out uint result))
            {
                return result;
            }
            return defaultValue;
        }

        private void IsoTpProcessor_OnMessageReceived(byte[] obj)
        {
            if (obj == null || obj.Length == 0) return;

            var hexValues = Array.ConvertAll(obj, b => b.ToString("X2"));
            AddText2Box($"[Rx] {string.Join(" ", hexValues)}");
        }

        private void btn_connect_Click(object sender, RoutedEventArgs e)
        {
            if (_connectionState != CAN_CONNECT_TYPE.NONE)
            {
                MessageBoxResult ret = MessageBox.Show("CAN port is open. Close it now?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ret == MessageBoxResult.No) return;
            }
            ToggleCanConnection(_connectionState == CAN_CONNECT_TYPE.NONE);
        }

        private void ToggleCanConnection(bool connect)
        {
            if (connect)
            {
                var connectPopup = new ConnectCAN { Owner = this };
                connectPopup.ShowDialog();
            }
            else
            {
                switch (_connectionState)
                {
                    case CAN_CONNECT_TYPE.PCAN_CC:
                    case CAN_CONNECT_TYPE.PCAN_FD:
                        TPCANStatus status = ClosePCanPort();
                        if (status == TPCANStatus.PCAN_ERROR_OK)
                        {
                            SetCanState(CAN_CONNECT_TYPE.NONE);
                        }
                        else
                        {
                            MessageBox.Show($"Failed to disconnect. Error: {status}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        break;
                    default:
                        SetCanState(CAN_CONNECT_TYPE.NONE);
                        break;
                }
            }
        }

        private void SetCanState(CAN_CONNECT_TYPE state)
        {
            _connectionState = state;
            Dispatcher.Invoke(() =>
            {
                switch (_connectionState)
                {
                    case CAN_CONNECT_TYPE.PCAN_CC:
                    case CAN_CONNECT_TYPE.PCAN_FD:
                        btn_connect.Content = "Disconnect";
                        row_0.IsEnabled = row_1.IsEnabled = row_2.IsEnabled = true;
                        break;

                    case CAN_CONNECT_TYPE.NONE:
                    default:
                        btn_connect.Content = "Connect";
                        row_0.IsEnabled = row_1.IsEnabled = row_2.IsEnabled = false;
                        break;
                }
            });
        }

        public static TPCANStatus OpenPCanPort(string portName)
        {
            if (_mainWindow == null) return TPCANStatus.PCAN_ERROR_UNKNOWN;

            PeakCAN.p_PortHandle = PeakCAN.getHandle(portName);
            PeakCAN.isCANFD = PeakCAN.IsCanFdChannel(PeakCAN.p_PortHandle);

            TPCANStatus stsResult = PeakCAN.isCANFD
                ? PCANBasic.InitializeFD(PeakCAN.p_PortHandle, "f_clock_mhz=80, nom_brp=2, nom_tseg1=63, nom_tseg2=16, nom_sjw=16, data_brp=2, data_tseg1=15, data_tseg2=4, data_sjw=4, data_ssp=32")
                : PCANBasic.Initialize(PeakCAN.p_PortHandle, TPCANBaudrate.PCAN_BAUD_500K);

            if (stsResult == TPCANStatus.PCAN_ERROR_CAUTION)
            {
                stsResult = TPCANStatus.PCAN_ERROR_OK;
            }

            if (stsResult == TPCANStatus.PCAN_ERROR_OK)
            {
                PeakCAN.ConfigureTraceFile(PeakCAN.p_PortHandle);
                _mainWindow.SetCanState(PeakCAN.isCANFD ? CAN_CONNECT_TYPE.PCAN_FD : CAN_CONNECT_TYPE.PCAN_CC);

                if (_mainWindow.m_ReadThread == null || !_mainWindow.m_ReadThread.IsAlive)
                {
                    m_ReadCancellationTokenSource = new CancellationTokenSource();

                    _mainWindow.m_ReadThread = new Thread(_mainWindow.CANReadThreadFunc) { IsBackground = true };
                    _mainWindow.m_ReadThread.Start(m_ReadCancellationTokenSource.Token);

                    _mainWindow.m_ProcessingThread = new Thread(_mainWindow.CANProcessingThreadFuncRx) { IsBackground = true };
                    _mainWindow.m_ProcessingThread.Start(m_ReadCancellationTokenSource.Token);
                }
            }

            return stsResult;
        }

        private TPCANStatus ClosePCanPort()
        {
            TPCANStatus stsResult = PCANBasic.Uninitialize(PeakCAN.p_PortHandle);

            m_ReadCancellationTokenSource?.Cancel();

            if (m_ReadThread != null && m_ReadThread.IsAlive)
            {
                m_ReadThread.Join(500);
            }
            if (m_ProcessingThread != null && m_ProcessingThread.IsAlive)
            {
                m_ProcessingThread.Join(500);
            }

            m_ReadThread = null;
            m_ProcessingThread = null;
            m_ReadCancellationTokenSource?.Dispose();
            m_ReadCancellationTokenSource = null;

            return stsResult;
        }

        private void CANReadThreadFunc(object? cancellationTokenObj)
        {
            if (cancellationTokenObj is not CancellationToken token) return;

            uint iBuffer = Convert.ToUInt32(m_ReceiveEvent.SafeWaitHandle.DangerousGetHandle().ToInt32());
            TPCANStatus stsResult = PCANBasic.SetValue(PeakCAN.p_PortHandle, TPCANParameter.PCAN_RECEIVE_EVENT, ref iBuffer, sizeof(uint));

            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
            {
                Dispatcher.Invoke(() => MessageBox.Show(PeakCAN.GetFormatedError(stsResult), "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                return;
            }

            CreateHandle();

            while (!token.IsCancellationRequested)
            {
                if (m_ReceiveEvent.WaitOne(50))
                {
                    try
                    {
                        Dispatcher.BeginInvoke(m_ReadDelegate);
                    }
                    catch (Exception)
                    {
                        // Suppress dispatcher invocation errors during shutdown
                    }
                }
            }
        }

        private void CANProcessingThreadFuncRx(object? cancellationTokenObj)
        {
            if (cancellationTokenObj is not CancellationToken token) return;

            try
            {
                foreach (ICANMessage msg in CAN_msg_queu_rx.GetConsumingEnumerable(token))
                {
                    _canDriver.InvokeReceive(msg.ID, msg.DATA);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful exit on cancellation
            }
        }

        private void CreateHandle()
        {
            m_ReadDelegate = new ReadDelegateHandler(() =>
            {
                if (m_ReadThread != null)
                {
                    PeakCAN.ReadMessages(PeakCAN.p_PortHandle);
                }
            });
        }

        private void btn_Send_Click(object sender, RoutedEventArgs e)
        {
            string hexStr = txt_MessageInput.Text;
            if (TryParseHexText(hexStr, out byte[]? byteArr) && byteArr != null)
            {
                AddText2Box($"[Tx] {hexStr.Trim()}");
                try
                {
                    _isoTpProcessor.SendRequest((ushort)byteArr.Length, byteArr, false);
                }
                catch (Exception ex)
                {
                    AddText2Box($"[Error] Send failed: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Invalid HEX format. Please check your input data.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btn_Cfg_Click(object sender, RoutedEventArgs e)
        {
            InitializeIsoTpProcessor();
            AddText2Box("[System] ISO-TP configuration updated successfully.");
        }

        public static bool TryParseHexText(string hexString, out byte[]? resultBytes)
        {
            resultBytes = null;

            if (string.IsNullOrWhiteSpace(hexString))
            {
                return false;
            }

            string cleaned = Regex.Replace(hexString, @"[\s,\-_xX]", "");

            if (cleaned.Length % 2 != 0 || !Regex.IsMatch(cleaned, @"^[0-9a-fA-F]+$"))
            {
                return false;
            }

            try
            {
                var bytes = new List<byte>();
                for (int i = 0; i < cleaned.Length; i += 2)
                {
                    string byteValueStr = cleaned.Substring(i, 2);
                    bytes.Add(byte.Parse(byteValueStr, NumberStyles.HexNumber));
                }

                resultBytes = bytes.ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void AddText2Box(string text)
        {
            if (_mainWindow == null) return;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                const int maxChars = 100000;
                string dateTime = DateTime.Now.ToString("HH:mm:ss.fff");
                Paragraph p = new Paragraph(new Run($"{dateTime} >> {text}"));
                _mainWindow.rtb_log.Document.Blocks.Add(p);

                // Limit document length to prevent memory bloat
                string currentText = new TextRange(_mainWindow.rtb_log.Document.ContentStart, _mainWindow.rtb_log.Document.ContentEnd).Text;
                if (currentText.Length > maxChars)
                {
                    TextPointer start = _mainWindow.rtb_log.Document.ContentStart;
                    TextPointer end = start.GetPositionAtOffset(10000);
                    if (end != null)
                    {
                        TextRange rangeToRemove = new TextRange(start, end);
                        rangeToRemove.Text = string.Empty;
                    }
                }

                _mainWindow.rtb_log.ScrollToEnd();
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                m_ReadCancellationTokenSource?.Cancel();
                if (_connectionState != CAN_CONNECT_TYPE.NONE)
                {
                    ClosePCanPort();
                }
            }
            catch
            {
                // Force safe exit
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
}