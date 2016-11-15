// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGetConsole;
using Task = System.Threading.Tasks.Task;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Restore packages menu command handler.
    /// </summary>
    internal sealed class SolutionRestoreCommand
    {
        private static SolutionRestoreCommand _instance;

        private const int CommandId = 0x300; // cmdidRestorePackages
        private static readonly Guid CommandSet = new Guid("25fd982b-8cae-4cbd-a440-e03ffccde106"); // guidNuGetDialogCmdSet;

        private readonly INuGetUILogger _logger;
        private readonly Lazy<ISolutionRestoreWorker> _restoreWorker;
        private readonly Lazy<ISolutionManager> _solutionManager;
        private readonly Lazy<IConsoleStatus> _consoleStatus;

        private readonly IVsMonitorSelection _vsMonitorSelection;
        private uint _solutionNotBuildingAndNotDebuggingContextCookie;

        private ISolutionRestoreWorker SolutionRestoreWorker => _restoreWorker.Value;
        private ISolutionManager SolutionManager => _solutionManager.Value;
        private IConsoleStatus ConsoleStatus => _consoleStatus.Value;

        private SolutionRestoreCommand(
            IComponentModel componentModel,
            IMenuCommandService commandService,
            IVsMonitorSelection vsMonitorSelection,
            INuGetUILogger logger)
        {
            _logger = logger;

            _restoreWorker = new Lazy<ISolutionRestoreWorker>(
                () => componentModel.GetService<ISolutionRestoreWorker>());

            _solutionManager = new Lazy<ISolutionManager>(
                () => componentModel.GetService<ISolutionManager>());

            _consoleStatus = new Lazy<IConsoleStatus>(
                () => componentModel.GetService<IConsoleStatus>());

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(
                OnRestorePackages, null, BeforeQueryStatusForPackageRestore, menuCommandId);
            commandService?.AddCommand(menuItem);

            _vsMonitorSelection = vsMonitorSelection;

            // get the solution not building and not debugging cookie
            Guid guid = VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_guid;
            _vsMonitorSelection.GetCmdUIContextCookie(ref guid, out _solutionNotBuildingAndNotDebuggingContextCookie);
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var componentModel = await package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            var vsMonitorSelection = await package.GetServiceAsync(typeof(IVsMonitorSelection)) as IVsMonitorSelection;

            var serviceProvider = await package.GetServiceAsync(typeof(SVsServiceProvider)) as IServiceProvider;
            var logger = new OutputConsoleLogger(serviceProvider);

            _instance = new SolutionRestoreCommand(componentModel, commandService, vsMonitorSelection, logger);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void OnRestorePackages(object sender, EventArgs args)
        {
            if (!SolutionRestoreWorker.IsBusy)
            {
                SolutionRestoreWorker.Restore(SolutionRestoreRequest.ByMenu());
            }
            else
            {
                // QueryStatus should disable the context menu in most of the cases.
                // Except when NuGetPackage was not loaded before VS won't send QueryStatus.
                _logger.Log(MessageLevel.Info, Resources.SolutionRestoreFailed_RestoreWorkerIsBusy);
            }
        }

        private void BeforeQueryStatusForPackageRestore(object sender, EventArgs args)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OleMenuCommand command = (OleMenuCommand)sender;

                // Enable the 'Restore NuGet Packages' dialog menu
                // - if the console is NOT busy executing a command, AND
                // - if the restore worker is not executing restore operation, AND
                // - if the solution exists and not debugging and not building AND
                // - if the solution is DPL enabled or there are NuGetProjects. This means that there loaded, supported projects
                // Checking for DPL more is a temporary code until we've the capability to get nuget projects
                // even in DPL mode. See https://github.com/NuGet/Home/issues/3711
                command.Enabled = !ConsoleStatus.IsBusy &&
                    !SolutionRestoreWorker.IsBusy &&
                    IsSolutionExistsAndNotDebuggingAndNotBuilding() &&
                    (SolutionManager.IsSolutionDPLEnabled || SolutionManager.GetNuGetProjects().Any());
            });
        }

        private bool IsSolutionExistsAndNotDebuggingAndNotBuilding()
        {
            int pfActive;
            var result = _vsMonitorSelection.IsCmdUIContextActive(_solutionNotBuildingAndNotDebuggingContextCookie, out pfActive);
            return (result == VSConstants.S_OK && pfActive > 0);
        }
    }
}
