using System;
using System.Threading;
using System.Threading.Tasks;

namespace MDBCashChanger
{
    /// <summary>
    /// High-level cash changer controller using MDB protocol
    /// </summary>
    public class CashChanger : IDisposable
    {
        private readonly MDBProtocol _mdb;
        private CancellationTokenSource? _pollingCancellation;
        private Task? _pollingTask;
        private bool _disposed = false;

        public event EventHandler<BillEventArgs>? BillAccepted;
        public event EventHandler<BillEventArgs>? BillRejected;
        public event EventHandler<StatusChangedEventArgs>? StatusChanged;
        public event EventHandler<string>? LogMessage;

        public MDBSetupInfo? SetupInfo { get; private set; }
        public CashChangerStatus Status { get; private set; } = CashChangerStatus.Disconnected;
        public decimal TotalAccepted { get; private set; }
        public bool IsPolling => _pollingTask != null && !_pollingTask.IsCompleted;

        public CashChanger()
        {
            _mdb = new MDBProtocol();
            _mdb.LogMessage += (s, e) => LogMessage?.Invoke(this, $"MDB: {e}");
        }

        public bool Initialize(string portName, int baudRate = 9600)
        {
            LogMessage?.Invoke(this, "Initializing cash changer...");
            
            if (!_mdb.Initialize(portName, baudRate))
            {
                SetStatus(CashChangerStatus.Error, "Failed to open MDB port");
                return false;
            }

            // Reset the device
            LogMessage?.Invoke(this, "Resetting cash changer...");
            if (!_mdb.Reset())
            {
                SetStatus(CashChangerStatus.Error, "Failed to reset cash changer");
                return false;
            }

            Thread.Sleep(1000); // Wait for reset to complete

            // Get setup information
            LogMessage?.Invoke(this, "Getting setup information...");
            SetupInfo = _mdb.GetSetupInfo();
            if (SetupInfo == null)
            {
                SetStatus(CashChangerStatus.Error, "Failed to get setup information");
                return false;
            }

            LogMessage?.Invoke(this, $"Setup Info - Feature Level: {SetupInfo.FeatureLevel}, " +
                                   $"Country: {SetupInfo.CountryCode}, " +
                                   $"Scaling: {SetupInfo.BillScalingFactor}, " +
                                   $"Escrow: {SetupInfo.EscrowStatus}");

            // Enable all bill types
            ushort allBillTypes = 0xFFFF;
            if (!_mdb.EnableBillTypes(allBillTypes))
            {
                LogMessage?.Invoke(this, "Warning: Failed to enable bill types");
            }

            SetStatus(CashChangerStatus.Ready, "Cash changer initialized successfully");
            return true;
        }

        public void StartPolling()
        {
            if (IsPolling)
            {
                LogMessage?.Invoke(this, "Polling already active");
                return;
            }

            _pollingCancellation = new CancellationTokenSource();
            _pollingTask = Task.Run(PollLoop, _pollingCancellation.Token);
            LogMessage?.Invoke(this, "Polling started");
        }

        public void StopPolling()
        {
            if (!IsPolling)
            {
                return;
            }

            _pollingCancellation?.Cancel();
            _pollingTask?.Wait(5000);
            _pollingCancellation?.Dispose();
            _pollingCancellation = null;
            _pollingTask = null;
            LogMessage?.Invoke(this, "Polling stopped");
        }

        private async Task PollLoop()
        {
            LogMessage?.Invoke(this, "Poll loop started");
            
            while (!_pollingCancellation!.Token.IsCancellationRequested)
            {
                try
                {
                    var pollResult = _mdb.Poll();
                    
                    switch (pollResult.Status)
                    {
                        case MDBStatus.Ready:
                            if (Status != CashChangerStatus.Ready)
                                SetStatus(CashChangerStatus.Ready, "Device ready");
                            break;
                            
                        case MDBStatus.Active:
                            if (Status != CashChangerStatus.Active)
                                SetStatus(CashChangerStatus.Active, "Device active");
                            break;
                            
                        case MDBStatus.NoResponse:
                            SetStatus(CashChangerStatus.Error, "No response from device");
                            break;
                    }

                    // Process events
                    foreach (var evt in pollResult.Events)
                    {
                        ProcessEvent(evt);
                    }

                    await Task.Delay(100, _pollingCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"Poll error: {ex.Message}");
                    await Task.Delay(1000, _pollingCancellation.Token);
                }
            }
            
            LogMessage?.Invoke(this, "Poll loop ended");
        }

        private void ProcessEvent(MDBEvent evt)
        {
            switch (evt.Type)
            {
                case MDBEventType.BillAccepted:
                    TotalAccepted += evt.Amount;
                    LogMessage?.Invoke(this, $"Bill accepted: ${evt.Amount:F2} (Type {evt.BillType})");
                    BillAccepted?.Invoke(this, new BillEventArgs
                    {
                        BillType = evt.BillType,
                        Amount = evt.Amount,
                        TotalAccepted = TotalAccepted
                    });
                    break;

                case MDBEventType.BillRejected:
                    LogMessage?.Invoke(this, $"Bill rejected (Type {evt.BillType})");
                    BillRejected?.Invoke(this, new BillEventArgs
                    {
                        BillType = evt.BillType,
                        Amount = 0,
                        TotalAccepted = TotalAccepted
                    });
                    break;

                case MDBEventType.Jam:
                    SetStatus(CashChangerStatus.Error, "Bill jam detected");
                    break;

                case MDBEventType.StackerFull:
                    SetStatus(CashChangerStatus.Error, "Stacker full");
                    break;

                case MDBEventType.Defective:
                    SetStatus(CashChangerStatus.Error, "Device defective");
                    break;

                case MDBEventType.Busy:
                    SetStatus(CashChangerStatus.Busy, "Device busy");
                    break;

                default:
                    LogMessage?.Invoke(this, $"Event: {evt.Type} (Data: {evt.Data})");
                    break;
            }
        }

        public bool StackBill()
        {
            LogMessage?.Invoke(this, "Stacking bill...");
            return _mdb.Stack();
        }

        public bool ReturnBill()
        {
            LogMessage?.Invoke(this, "Returning bill...");
            return _mdb.Return();
        }

        public bool Reset()
        {
            LogMessage?.Invoke(this, "Resetting cash changer...");
            bool success = _mdb.Reset();
            if (success)
            {
                TotalAccepted = 0;
                SetStatus(CashChangerStatus.Ready, "Device reset");
            }
            return success;
        }

        public void ClearTotal()
        {
            TotalAccepted = 0;
            LogMessage?.Invoke(this, "Total cleared");
        }

        private void SetStatus(CashChangerStatus newStatus, string message)
        {
            if (Status != newStatus)
            {
                var oldStatus = Status;
                Status = newStatus;
                LogMessage?.Invoke(this, $"Status changed: {oldStatus} -> {newStatus} ({message})");
                StatusChanged?.Invoke(this, new StatusChangedEventArgs
                {
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    Message = message
                });
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopPolling();
                _mdb?.Dispose();
                _disposed = true;
            }
        }
    }

    public class BillEventArgs : EventArgs
    {
        public byte BillType { get; set; }
        public decimal Amount { get; set; }
        public decimal TotalAccepted { get; set; }
    }

    public class StatusChangedEventArgs : EventArgs
    {
        public CashChangerStatus OldStatus { get; set; }
        public CashChangerStatus NewStatus { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public enum CashChangerStatus
    {
        Disconnected,
        Ready,
        Active,
        Busy,
        Error
    }
}