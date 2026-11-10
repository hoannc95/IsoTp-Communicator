using Peak.Can.Basic.BackwardCompatibility;
using System.Windows;
using IsoTpCommunicator.CANPackage;

namespace IsoTpCommunicator
{
    /// <summary>
    /// Interaction logic for ConnectCAN.xaml
    /// </summary>
    public partial class ConnectCAN : Window
    {
        private readonly List<string> _pcanConnections = new List<string>();

        public ConnectCAN()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            lv_peakcan.ItemsSource = _pcanConnections;
            RefreshConnection();
        }

        private void btn_refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshConnection();
        }

        /// <summary>
        /// Scans and refreshes available PEAK-CAN hardware channels.
        /// </summary>
        private void RefreshConnection()
        {
            _pcanConnections.Clear();

            try
            {
                // Checks for available Plug&Play channels
                TPCANStatus stsResult = PCANBasic.GetValue(PCANBasic.PCAN_NONEBUS, TPCANParameter.PCAN_ATTACHED_CHANNELS_COUNT, out uint channelsCount, sizeof(uint));

                if (stsResult == TPCANStatus.PCAN_ERROR_OK && channelsCount > 0)
                {
                    TPCANChannelInformation[] channelInfoArray = new TPCANChannelInformation[channelsCount];

                    stsResult = PCANBasic.GetValue(PCANBasic.PCAN_NONEBUS, TPCANParameter.PCAN_ATTACHED_CHANNELS, channelInfoArray);
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                    {
                        foreach (var channel in channelInfoArray)
                        {
                            // Include only connectable channels
                            if ((channel.channel_condition & PCANBasic.PCAN_CHANNEL_AVAILABLE) == PCANBasic.PCAN_CHANNEL_AVAILABLE)
                            {
                                bool isFD = (channel.device_features & PCANBasic.FEATURE_FD_CAPABLE) == PCANBasic.FEATURE_FD_CAPABLE;
                                _pcanConnections.Add(PeakCAN.FormatChannelName(channel.channel_handle, isFD));
                            }
                        }
                    }
                }

                lv_peakcan.Items.Refresh();
                gb_peakcan.IsEnabled = true;
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Could not find PeakCAN driver. The program will run without PeakCAN features.\nTo download the driver, please visit: https://www.peak-system.com/",
                    "Driver Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                gb_peakcan.IsEnabled = false;
            }
        }

        private void btn_connect_Click(object sender, RoutedEventArgs e)
        {
            if (lv_peakcan.SelectedItem == null)
            {
                MessageBox.Show("Please select a CAN channel first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CAN_CONNECT_TYPE connectionType = GetCurrentSelection();
            string selectedChannelName = lv_peakcan.SelectedItem.ToString() ?? string.Empty;

            switch (connectionType)
            {
                case CAN_CONNECT_TYPE.PCAN_CC:
                case CAN_CONNECT_TYPE.PCAN_FD:
                    TPCANStatus status = MainWindow.OpenPCanPort(selectedChannelName);
                    if (status == TPCANStatus.PCAN_ERROR_OK)
                    {
                        Close();
                    }
                    else
                    {
                        MessageBox.Show($"Connection failed. Error code: {status}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;

                case CAN_CONNECT_TYPE.NONE:
                default:
                    MessageBox.Show("Invalid channel selection.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }

        private CAN_CONNECT_TYPE GetCurrentSelection()
        {
            if (lv_peakcan.SelectedItem != null)
            {
                string selectedChannel = lv_peakcan.SelectedItem.ToString() ?? string.Empty;
                bool isCANFD = PeakCAN.IsCanFdChannel(PeakCAN.getHandle(selectedChannel));
                return isCANFD ? CAN_CONNECT_TYPE.PCAN_FD : CAN_CONNECT_TYPE.PCAN_CC;
            }
            return CAN_CONNECT_TYPE.NONE;
        }
    }
}