using dotEnhancer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using TableCloth;
using TableCloth.Models.Answers;

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

            _backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = false,
            };

            _backgroundWorker.DoWork += BackgroundWorker_DoWork;
            _backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            _backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
        }

        private BackgroundWorker _backgroundWorker;

        public MainWindowViewModel ViewModel
            => (MainWindowViewModel)DataContext;

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
                _backgroundWorker.ReportProgress(0, "공동 인증서 파일을 검색 중입니다...");
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
                        _backgroundWorker.ReportProgress((int)((double)++processedFileCount / totalFileCount * 100d), $"파일 삭제 완료 ({totalFileCount}개 중 {processedFileCount}개)");
                    }
                }
            }
            finally
            {
                var fragments = new List<string>();
                if (succeedFileCount > 0)
                    fragments.Add($"{succeedFileCount}개 파일 삭제 완료");
                if (failedFileCount > 0)
                    fragments.Add($"{failedFileCount}개 파일 삭제 실패");

                _backgroundWorker.ReportProgress(100, $"모든 작업을 완료했습니다. ({string.Join(", ", fragments)})");
                e.Result = new RemovePrivacyFilesResult(succeedFileCount, failedFileCount);
            }
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ViewModel.ProgressRate = e.ProgressPercentage;
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ViewModel.WorkInProgress = false;

            if (e.Cancelled)
            {
                MessageBox.Show(this, "작업이 도중에 취소되었습니다.", Title, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
                return;
            }

            if (e.Error != null)
            {
                var actualError = e.Error is AggregateException ? e.Error.InnerException : e.Error;
                MessageBox.Show(this, $"예기치 않은 오류가 발생했습니다. {actualError.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                return;
            }

            var result = e.Result as RemovePrivacyFilesResult;
            if (result == null)
            {
                MessageBox.Show(this, "작업을 완료했지만 결과를 확인할 수 없습니다.", Title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                return;
            }

            MessageBox.Show(this, $"총 {result.SucceedFileCount}개의 파일을 삭제했고, {result.FailedFileCount}개의 파일을 삭제하지 못했습니다.", Title, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.WorkInProgress = true;
                _backgroundWorker.RunWorkerAsync(new RemovePrivacyFilesRequest(
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
    }
}
