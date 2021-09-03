using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Helpers.PowerSaving;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using static WalletWasabi.Helpers.PowerSaving.LinuxInhibitorTask;

namespace WalletWasabi.Services
{
	public class SystemAwakeChecker : PeriodicRunner
	{
		private const string Reason = "CoinJoin is in progress.";
		private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

		private readonly object _canShutDownLock = new();
		private bool _canShutdown = true;

		private volatile IPowerSavingInhibitorTask? _powerSavingTask;

		private SystemAwakeChecker(WalletManager walletManager, Func<Task<IPowerSavingInhibitorTask>>? taskFactory) : base(TimeSpan.FromSeconds(2))
		{
			WalletManager = walletManager;
			TaskFactory = taskFactory;
		}

		public bool CanShutdown
		{
			get
			{
				lock (_canShutDownLock)
				{
					return _canShutdown;
				}
			}
			private set
			{
				lock (_canShutDownLock)
				{
					_canShutdown = value;
				}
			}
		}

		private WalletManager WalletManager { get; }
		private Func<Task<IPowerSavingInhibitorTask>>? TaskFactory { get; }

		/// <summary>Checks whether we support awake state prolonging for the current platform.</summary>
		public static async Task<SystemAwakeChecker?> CreateAsync(WalletManager walletManager)
		{
			Func<Task<IPowerSavingInhibitorTask>>? taskFactory = null;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// Either we have systemd or not.
				bool isSystemd = await LinuxInhibitorTask.IsSystemdInhibitSupportedAsync().ConfigureAwait(false);
				if (!isSystemd)
				{
					return null;
				}

				GraphicalEnvironment gui = GraphicalEnvironment.Other;

				// Specialization for GNOME.
				if (await LinuxInhibitorTask.IsMateSessionInhibitSupportedAsync().ConfigureAwait(false))
				{
					gui = GraphicalEnvironment.Mate;
				}
				else if (await LinuxInhibitorTask.IsGnomeSessionInhibitSupportedAsync().ConfigureAwait(false))
				{
					gui = GraphicalEnvironment.Gnome;
				}

				taskFactory = () => Task.FromResult<IPowerSavingInhibitorTask>(LinuxInhibitorTask.Create(InhibitWhat.Idle, Timeout, Reason, gui));
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				taskFactory = () => Task.FromResult<IPowerSavingInhibitorTask>(WindowsPowerAvailabilityTask.Create(Reason));
			}

			return new SystemAwakeChecker(walletManager, taskFactory);
		}

		protected async override Task ActionAsync(CancellationToken cancel)
		{
			if (WalletManager.AnyCoinJoinInProgress())
			{
				if (_powerSavingTask is null)
				{
					_powerSavingTask = await TaskFactory!().ConfigureAwait(false);
				}

				if (WalletManager.AnyCoinJoinInProgress()) // WalletManager.AnyCoinJoinInCriticalPhase() will be here
				{
					PreventShutdown();
				}
				else
				{
					await PreventSleepAsync().ConfigureAwait(false);
				}
			}
			else
			{
				await ReleaseAllPreventionAsync().ConfigureAwait(false);
			}
		}

		private async Task PreventSleepAsync()
		{
			IPowerSavingInhibitorTask? task = _powerSavingTask;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (task is not null)
				{
					if (!task.Prolong(Timeout.Add(TimeSpan.FromMinutes(1))))
					{
						Logger.LogTrace("Failed to prolong the power saving task.");
						task = null;
					}
				}

				if (task is null)
				{
					Logger.LogTrace("Create new power saving prevention task.");
					_powerSavingTask = await TaskFactory!().ConfigureAwait(false);
				}
			}
			else
			{
				await EnvironmentHelpers.ProlongSystemAwakeAsync().ConfigureAwait(false);
			}
		}

		// The rest of the work is done in ApplicationViewModel.cs and App.axaml.cs
		private void PreventShutdown()
		{
			CanShutdown = false;
		}

		private async Task ReleaseAllPreventionAsync()
		{
			CanShutdown = true;
			if (_powerSavingTask is not null)
			{
				await _powerSavingTask.StopAsync().ConfigureAwait(false);
				_powerSavingTask = null;
			}
		}
	}
}
