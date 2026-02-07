# POS Counter App

A modern, touch-friendly Point of Sale (POS) application built with **WPF** and **.NET 6**. Designed for high-performance retail environments with support for offline operations, API synchronization, and peripheral integration.

## Key Features

- **Efficient Sales Interface**: Quick product lookup via barcode or search, multiplier support (with lock feature), and touch-optimized UI.
- **Refund Management**: Dedicated refund dialog with item selection, partial refund support, and age validation warnings (>2 days old).
- **Payment Flexibility**: Support for Cash, Card, and Online payments with automatic denomination calculation.
- **Daily Cash Report (DCR)**: Detailed end-of-day reporting with denomination breakdown for cash and bank transactions.
- **Hardware Integration**:
  - **ESC/POS Printing**: Fast thermal receipt printing (legacy and modern support).
  - **Pole Display**: Customer-facing display integration.
  - **Barcode Scanners**: Optimized for usb barcode scanners.
- **Data Synchronization**: Background service to sync products and changes from a central API.
- **Security**:
  - Role-based access (User/Supervisor/Admin).
  - Password protection for Void and Refund operations.
  - Kiosk Mode to lock down the application window.
- **Audit Trails**: Logs for refunds and voided items.

## Technologies

- **Frontend**: WPF (Windows Presentation Foundation)
- **Framework**: .NET 6.0
- **Database**: SQLite (Local storage)
- **Architecture**: MVVM (Model-View-ViewModel) with CommunityToolkit.Mvvm
- **Printing**: System.Printing and RawPrinterHelper for direct ESC/POS commands

## Installation

1.  Clone the repository.
2.  Open `PosApp.sln` in Visual Studio 2022.
3.  Restore NuGet packages.
4.  Build and Run (Release/Debug).

## Usage

- **Login**: Use valid credentials (default `admin`/`123`).
- **Sales**: Scan items or search manually. Press `F1`-`F12` for quick actions.
- **Refunds**: Press `F7` (Return) -> Enter Invoice Number -> Select Items -> Confirm.
- **DCR**: Access via Dashboard to view daily totals.

## License

Private / Proprietary.
