using dotEnhancer;
using Sponge.Components.Implementations;
using Sponge.Models;
using Sponge.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using TableCloth;
using TableCloth.Models.Answers;
using TableCloth.Resources;

namespace Sponge
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindowViewModel ViewModel
            => (MainWindowViewModel)DataContext;

        public BackgroundWorker BackgroundWorker
            => (BackgroundWorker)Resources["BackgroundWorker"];

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ViewModel.ProgressRate = e.ProgressPercentage;
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ViewModel.WorkInProgress = false;

            if (e.Cancelled)
            {
                MessageBox.Show(this, ErrorStrings.Error_Sponge_TaskCancelled, Title, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
                return;
            }

            if (e.Error != null)
            {
                var actualError = e.Error is AggregateException ? e.Error.InnerException : e.Error;
                MessageBox.Show(this, string.Format(ErrorStrings.Error_Sponge_UnexpectedErrorOccurred, actualError.Message), Title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                return;
            }

            var result = e.Result as RemovePrivacyFilesResult;
            if (result == null)
            {
                MessageBox.Show(this, ErrorStrings.Error_Sponge_NoCompatibleResultFound, Title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                return;
            }

            MessageBox.Show(this, result.Message, Title, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.WorkInProgress = true;
                BackgroundWorker.RunWorkerAsync(new RemovePrivacyFilesRequest(
                    ViewModel.OverwriteMultipleTimes ? 3 : 0));
            }
            catch (Exception thrownException)
            {
                MessageBox.Show(this, thrownException.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
            finally
            {
                ViewModel.WorkInProgress = false;
            }
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var themeManager = new VisualThemeManager();
            themeManager.ApplyAutoThemeChange(this);

            var answerFilePath = Path.GetFullPath("SpongeAnswers.json");

            if (File.Exists(answerFilePath))
            {
                using (var answerFileContent = File.OpenRead(answerFilePath))
                {
                    var answer = DeserializeSpongeAnswersJson(answerFileContent);

                    if (answer != null)
                        ViewModel.OverwriteMultipleTimes = answer.RecommendSafeDelete;
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ViewModel.WorkInProgress)
                e.Cancel = true;
        }

        private SpongeAnswers DeserializeSpongeAnswersJson(Stream targetStream)
        {
            if (!targetStream.CanRead)
                return default;

            return JsonSerializer.Deserialize<SpongeAnswers>(targetStream);
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var args = e.Argument as RemovePrivacyFilesRequest;

            if (args == default)
                return;

            var repeatCount = args.OverwriteCount;
            var succeedFileCount = 0;
            var failedFileCount = 0;

            try
            {
                BackgroundWorker.ReportProgress(0, UIStringResources.Sponge_WorkInProgress);
                var localLowNpkiDirectoryPath = NativeMethods.GetKnownFolderPath(NativeMethods.LocalLowFolderGuid);

                if (!Directory.Exists(localLowNpkiDirectoryPath))
                    return;

                var fileList = Directory.GetFiles(localLowNpkiDirectoryPath, "*.*", SearchOption.AllDirectories)
                    .Where(x =>
                    {
                        return
                            string.Equals(".der", System.IO.Path.GetExtension(x), StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(".key", System.IO.Path.GetExtension(x), StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(".pfx", System.IO.Path.GetExtension(x), StringComparison.OrdinalIgnoreCase);
                    })
                    .Distinct()
                    .ToList();

                var totalFileCount = fileList.Count;
                var processedFileCount = 0;

                foreach (var eachFile in fileList)
                {
                    var fileInfo = new FileInfo(eachFile);

                    try
                    {
                        fileInfo.SecureDelete(repeatCount, SecureDeleteObfuscationMode.All);
                        succeedFileCount++;
                    }
                    catch
                    {
                        failedFileCount++;
                    }
                    finally
                    {
                        BackgroundWorker.ReportProgress((int)((double)++processedFileCount / totalFileCount * 100d), string.Format(UIStringResources.Sponge_DeleteProgressMessage, totalFileCount, processedFileCount));
                    }
                }
            }
            finally
            {
                var fragments = new List<string>();
                if (succeedFileCount > 0)
                    fragments.Add(string.Format(UIStringResources.Sponge_DeletedFileCount, succeedFileCount));
                if (failedFileCount > 0)
                    fragments.Add(string.Format(UIStringResources.Sponge_DeleteFailedFileCount, failedFileCount));

                var message = fragments.Count() > 0 ?
                    $"{UIStringResources.Sponge_OperationCompleted} ({string.Join(", ", fragments)})" :
                    UIStringResources.Sponge_OperationCompleted_WithNoResult;

                BackgroundWorker.ReportProgress(100, message);
                e.Result = new RemovePrivacyFilesResult(succeedFileCount, failedFileCount, message);
            }
        }
    }
}
