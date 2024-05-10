using dotEnhancer;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

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

            _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += BackgroundWorker_DoWork;
            _backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            _backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
        }

        private BackgroundWorker _backgroundWorker;

        public MainWindowViewModel ViewModel
            => (MainWindowViewModel)DataContext;

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var succeedFileCount = 0;
            var failedFileCount = 0;

            try
            {
                _backgroundWorker.ReportProgress(0, "공동 인증서 파일을 검색 중입니다...");
                var userProfileDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!Directory.Exists(userProfileDirectory))
                    return;

                var fileList = Directory.GetFiles(userProfileDirectory)
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
                var repeatCount = ViewModel.OverwriteMultipleTimes ? 3 : 1;

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
                _backgroundWorker.ReportProgress(100, $"모든 작업을 완료했습니다. 총 {succeedFileCount}개 파일을 삭제했고, {failedFileCount}개 파일을 삭제하지 못했습니다.");
            }
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ViewModel.ProgressRate = e.ProgressPercentage;
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ViewModel.WorkInProgress = false;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.WorkInProgress = true;
                _backgroundWorker.RunWorkerAsync();
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ViewModel.WorkInProgress)
                e.Cancel = true;
        }
    }
}
