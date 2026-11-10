# IsoTpCommunicator

`IsoTpCommunicator` is a lightweight, clean, and efficient Windows Presentation Foundation (WPF) desktop application designed for automotive embedded systems engineers. It allows developers and testers to communicate with Vehicle Electronic Control Units (ECUs) over the CAN bus utilizing the **ISO-TP (ISO 15765-2)** transport protocol via PEAK-System (PCAN) hardware interfaces.

---

## Key Features

* **ISO-TP Communication:** Supports sending and receiving segmented/single ISO-TP frames for ECU diagnostic and flashing sessions.
* **PEAK-System Integration:** Seamlessly connects with PCAN-Basic hardware channels (Classical CAN and CAN FD supported).
* **Real-time Diagnostic Logging:** Built-in Rx/Tx message logging viewer with precise timestamps and auto-scrolling.
* **Dynamic ID Configuration:** Easily configure and update **Physical ID**, **Functional ID**, and **Response ID** on the fly.
* **Modern & Clean UI:** Vertical layout (450x800) optimized for engineering workstations, featuring rounded control elements and intuitive workspace separation.

---

## Prerequisites & Dependencies

Before building and running the project, ensure you have the following installed:
* **Visual Studio 2022** (or later) with the **.NET Desktop Development** workload.
* **.NET 8.0** (or compatible .NET framework as configured in your project).
* **PCAN-Basic API Driver** installed on your system (Downloadable from [PEAK-System](https://www.peak-system.com/)).
* Required backend libraries/references included in your solution:
  * `Peak.Can.Basic.BackwardCompatibility`
  * `IsoTp.CANBus.Net`

---

## Project Architecture

* **`MainWindow.xaml / .cs`**: The main user interface and core application controller handling communication threads, queue processing, and UI log rendering.
* **`ConnectCAN.xaml / .cs`**: Handles hardware scanning, channel selection, and initialization of connected PEAK-System devices.
* **`PeakCAN.cs`**: Wrapper class managing PCAN initialization, message reading/writing loops (Classical CAN and CAN FD), trace configuration, and error formatting.

---

## How to Use

1. **Connect to CAN Hardware:**
   * Launch the application.
   * Click the **Connect** button to open the channel selection window (`ConnectCAN`).
   * Click **Refresh** to scan available physical PCAN channels.
   * Select your desired CAN channel and click **Connect**.

2. **Configure ISO-TP Identifiers:**
   * Enter your target **Physical ID**, **Func ID**, and **Res ID** in hexadecimal format (e.g., `0x7C0`, `0x7DF`, `0x7C8`).
   * Click the configuration update action to apply your changes to the diagnostic protocol stack.

3. **Send & Receive Messages:**
   * Type your payload as a hexadecimal string (e.g., `22 F1 90` for a DID read request) into the message input box.
   * Click **Send** to transmit the request over ISO-TP.
   * Monitor incoming and outgoing diagnostic responses in real-time inside the **Message Log** view (`Rx / Tx`).

---

## License

This project is developed for internal engineering, diagnostics, and firmware development purposes.