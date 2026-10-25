using System;
using System.Threading;
using PhantomVault.Core.Interfaces;
using PhantomVault.Core.Models;

namespace PhantomVault.Security.SecurityPolicies;

/// <summary>
/// Coordinates inactivity timeouts, dead-man's switch policies, and emergency lock procedures.
/// </summary>
public sealed class SecurityPolicyService : ISecurityPolicyService, IDisposable
{
    private readonly Timer _timer;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private bool _inCountdown;
    private int _remainingCountdownSeconds;
    private readonly object _lock = new();

    public EmergencyPolicy CurrentPolicy { get; private set; } = new();

    public event Action<int>? DeadMansSwitchCountdownTick;
    public event Action? EmergencyLockTriggered;

    public SecurityPolicyService()
    {
        _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void UpdatePolicy(EmergencyPolicy policy)
    {
        lock (_lock)
        {
            CurrentPolicy = policy ?? new EmergencyPolicy();
            _inCountdown = false;
        }
    }

    public void RegisterUserActivity()
    {
        lock (_lock)
        {
            _lastActivityUtc = DateTime.UtcNow;
            if (_inCountdown)
            {
                _inCountdown = false;
            }
        }
    }

    public void TriggerEmergencyLock()
    {
        lock (_lock)
        {
            _inCountdown = false;
        }
        EmergencyLockTriggered?.Invoke();
    }

    private void OnTimerTick(object? state)
    {
        lock (_lock)
        {
            if (!CurrentPolicy.AutoLockOnInactivity && !CurrentPolicy.DeadMansSwitchEnabled)
                return;

            TimeSpan idle = DateTime.UtcNow - _lastActivityUtc;

            // 1. Standard Auto-Lock
            if (CurrentPolicy.AutoLockOnInactivity && idle.TotalMinutes >= CurrentPolicy.InactivityMinutes)
            {
                TriggerEmergencyLock();
                return;
            }

            // 2. Dead-Man's Switch
            if (CurrentPolicy.DeadMansSwitchEnabled)
            {
                double deadMansThresholdMinutes = CurrentPolicy.DeadMansSwitchTimeoutMinutes;
                double warningStartMinutes = Math.Max(0.1, deadMansThresholdMinutes - (CurrentPolicy.WarningCountdownSeconds / 60.0));

                if (idle.TotalMinutes >= warningStartMinutes)
                {
                    if (!_inCountdown)
                    {
                        _inCountdown = true;
                        _remainingCountdownSeconds = CurrentPolicy.WarningCountdownSeconds;
                    }

                    _remainingCountdownSeconds = Math.Max(0, (int)((deadMansThresholdMinutes * 60) - idle.TotalSeconds));
                    DeadMansSwitchCountdownTick?.Invoke(_remainingCountdownSeconds);

                    if (_remainingCountdownSeconds <= 0)
                    {
                        TriggerEmergencyLock();
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
