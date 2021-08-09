using Serilog;
using Serilog.Formatting.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TableCloth.Contracts;
using TableCloth.Implementations.WindowsSandbox;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Implementations.WinForms
{
    public sealed class WinFormUserInterface : IAppUserInterface
    {
        public WinFormUserInterface(
            IAppStartup appStartup,
            ICatalogDeserializer catalogDeserializer,
            IX509CertPairScanner certPairScanner,
            ISandboxBuilder sandboxBuilder,
            IAppMessageBox appMessageBox,
            ISandboxLauncher sandboxLauncher)
        {
            _appStartup = appStartup;
            _catalogDeserializer = catalogDeserializer;
            _certPairScanner = certPairScanner;
            _sandboxBuilder = sandboxBuilder;
            _appMessageBox = appMessageBox;
            _sandboxLauncher = sandboxLauncher;
        }

        private readonly IAppStartup _appStartup;
        private readonly ICatalogDeserializer _catalogDeserializer;
        private readonly IX509CertPairScanner _certPairScanner;
        private readonly ISandboxBuilder _sandboxBuilder;
        private readonly IAppMessageBox _appMessageBox;
        private readonly ISandboxLauncher _sandboxLauncher;

        private IWin32Window _mainWindow;

        public object MainWindowHandle
            => _mainWindow;

        public void StartApplication(IEnumerable<string> args)
        {
            var appThread = new Thread(new ParameterizedThreadStart(_ =>
            {
                Application.OleRequired();
                Application.EnableVisualStyles();
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.SetCompatibleTextRenderingDefault(false);

                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.File(new JsonFormatter(), Path.Combine(_appStartup.AppDataDirectoryPath, "ApplicationLog.jsonl"))
                    .CreateLogger();

                using var form = CreateMainForm();
                _mainWindow = form;
                Application.Run(new ApplicationContext(form));
            }));

            appThread.SetApartmentState(ApartmentState.STA);
            appThread.Start(args);
            appThread.Join();
        }

        internal sealed class MainFormContext
        {
            public List<string> TemporaryDirectories { get; } = new List<string>();
        }

        private Form CreateMainForm()
        {
            var context = new MainFormContext();

            var form = new Form()
            {
                Text = LocalStringResources.MainForm_Title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                Icon = new Icon(
                    new MemoryStream(Convert.FromBase64String(GraphicResources.AppIcon)),
                    64, 64),
                Size = new Size(640, 480),
                AutoScaleDimensions = new SizeF(96f, 96f),
                AutoScaleMode = AutoScaleMode.Dpi,
                Tag = context,
            };

            var tableLayout = new TableLayoutPanel()
            {
                Parent = form,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };

            /*
             * +--------------------+
             * |                    |
             * |         A          |
             * |                    |
             * +---------+----------+
             * |    B0   |    B1    |
             * +---------+----------+
             * 
             * */

            // Row A, 90%
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 0.9f));

            // Row B, 10%
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 0.1f));

            // Column 0, 50%
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0.5f));

            // Column 1, 50%
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0.5f));

            // Row A, Column 0 + 1
            var dialogLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                AutoScroll = true,
            };
            tableLayout.SetCellPosition(dialogLayout, new TableLayoutPanelCellPosition(column: 0, row: 0));
            tableLayout.SetColumnSpan(dialogLayout, 2);

            // Row B, Column 0
            var actionLeftLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            tableLayout.SetCellPosition(actionLeftLayout, new TableLayoutPanelCellPosition(column: 0, row: 1));

            // Row B, Column 1
            var actionRightLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            tableLayout.SetCellPosition(actionRightLayout, new TableLayoutPanelCellPosition(column: 1, row: 1));

            dialogLayout.CreateLabel(LocalStringResources.MainForm_SelectOptionsLabelText);
            dialogLayout.CreateLabel();

            var certListPanel = new FlowLayoutPanel()
            {
                Parent = dialogLayout,
                FlowDirection = FlowDirection.LeftToRight,
                Width = form.ClientSize.Width - 100,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            var mapNPKICert = certListPanel.CreateCheckBox(LocalStringResources.MainForm_MapNpkiCertButtonText, true);
            var importButton = certListPanel.CreateButton(LocalStringResources.MainForm_BrowseButtonText);
            certListPanel.Height = (int)(mapNPKICert.Height * 1.6f);

            var npkiFileListBox = new ListBox()
            {
                Parent = dialogLayout,
                Height = 60,
                IntegralHeight = true,
                Width = form.ClientSize.Width - 100,
            };

            importButton.AddClickEvent(x =>
            {
                using var selectForm = CreateCertSelectForm();
                if (selectForm.ShowDialog(form) != DialogResult.OK)
                    return;

                if (selectForm.Tag is not X509CertPair certPair)
                    return;

                npkiFileListBox.DataSource = new string[] { certPair.DerFilePath, certPair.KeyFilePath, };
            });

            importButton.DataBindings.Add(nameof(Control.Enabled), mapNPKICert, nameof(CheckBox.Checked));
            npkiFileListBox.DataBindings.Add(nameof(Control.Enabled), mapNPKICert, nameof(CheckBox.Checked));

            var enableMicrophone = dialogLayout.CreateCheckBox(LocalStringResources.MainForm_UseMicrophoneCheckboxText, false);
            var enableWebCam = dialogLayout.CreateCheckBox(LocalStringResources.MainForm_UseWebCameraCheckboxText, false);
            var enablePrinters = dialogLayout.CreateCheckBox(LocalStringResources.MainForm_UsePrinterCheckboxText, false);

            enableMicrophone.Font = new Font(enableMicrophone.Font, FontStyle.Bold);
            enableWebCam.Font = new Font(enableWebCam.Font, FontStyle.Bold);

            dialogLayout.CreateLabel();
            var siteInstructionLabel = dialogLayout.CreateLabel(LocalStringResources.MainForm_SelectSiteLabelText);
            dialogLayout.CreateLabel();

            var siteCatalogTabControl = new TabControl
            {
                Parent = dialogLayout,
                Width = form.ClientSize.Width - 100,
                Height = 100,
                Visible = true,
            };

            var categoryValues = Enum.GetValues<CatalogInternetServiceCategory>().ToList();
            categoryValues.Remove(CatalogInternetServiceCategory.Other);
            categoryValues.Add(CatalogInternetServiceCategory.Other);

            foreach (var eachCategoryType in categoryValues)
            {
                var eachTabPage = new TabPage
                {
                    Parent = siteCatalogTabControl,
                    Text = StringResources.InternetServiceCategory_DisplayText(eachCategoryType),
                    Tag = eachCategoryType,
                };

                var eachSiteCatalog = new ListBox()
                {
                    Name = "SiteList",
                    Parent = eachTabPage,
                    Dock = DockStyle.Fill,
                    SelectionMode = SelectionMode.MultiExtended,
                    IntegralHeight = false,
                };
            }

            var aboutButton = actionLeftLayout.CreateButton(LocalStringResources.MainForm_AboutButtonText, handler: x =>
            {
                _appMessageBox.DisplayInfo(this, StringResources.AboutDialog_BodyText);
            });

            var cancelButton = actionRightLayout.CreateButton(LocalStringResources.MainForm_CloseButtonText, handler: x =>
            {
                form.Close();
            });

            var launchButton = actionRightLayout.CreateButton(LocalStringResources.MainForm_LaunchSandboxButtonText);

            form.CancelButton = cancelButton;
            form.AcceptButton = launchButton;

            form.Load += (_sender, _e) =>
            {
                if (_sender is not Form realSender)
                    return;

                try
                {
                    File.WriteAllText(
                        Path.Combine(_appStartup.AppDataDirectoryPath, "Readme.txt"),
                        StringResources.SandboxWorkingDir_ReadmeText,
                        new UTF8Encoding(false));
                }
                catch { }

                try
                {
                    var catalog = _catalogDeserializer.DeserializeCatalog(new Uri(StringResources.CatalogUrl));

                    var siteListControls = siteCatalogTabControl.TabPages.Cast<TabPage>().ToDictionary(
                        x => (CatalogInternetServiceCategory)x.Tag,
                        x => x.Controls["SiteList"] as ListBox);

                    if (catalog != null && catalog.Services.Any())
                    {
                        foreach (var eachType in catalog.Services.GroupBy(x => x.Category))
                            siteListControls[eachType.Key].DataSource = eachType.ToList();
                    }
                    else
                    {
                        siteCatalogTabControl.Visible = false;
                        siteInstructionLabel.Text = LocalStringResources.MainForm_SelectSiteLabelText_Alt;
                    }
                }
                catch (Exception e)
                {
                    siteCatalogTabControl.Visible = false;
                    siteInstructionLabel.Text = LocalStringResources.MainForm_SelectSiteLabelText_Alt;
                    _appMessageBox.DisplayError(this, StringResources.Error_Cannot_Download_Catalog(e), false);
                }

                try
                {
                    var pairs = _certPairScanner.ScanX509Pairs(_certPairScanner.GetCandidateDirectories());
                    if (pairs != null && pairs.Count() == 1)
                    {
                        var pair = pairs.First();
                        npkiFileListBox.DataSource = new string[] { pair.DerFilePath, pair.KeyFilePath, };
                    }
                }
                catch { }
            };

            form.FormClosed += (_sender, _e) =>
            {
                if (context.TemporaryDirectories.Any())
                {
                    var explorerFilePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "explorer.exe");
                    if (!File.Exists(explorerFilePath))
                    {
                        _appMessageBox.DisplayError(this, StringResources.Error_Windows_Explorer_Missing, false);
                        return;
                    }

                    using var explorerProcess = new Process()
                    {
                        StartInfo = new ProcessStartInfo(explorerFilePath, _appStartup.AppDataDirectoryPath),
                    };

                    if (!explorerProcess.Start())
                    {
                        _appMessageBox.DisplayError(this, StringResources.Error_Windows_Explorer_CanNotStart, false);
                        return;
                    }
                }
            };

            launchButton.AddClickEvent(x =>
            {
                var isSandboxRunning = Process.GetProcesses()
                    .Where(x => x.ProcessName.StartsWith("WindowsSandbox", StringComparison.OrdinalIgnoreCase))
                    .Any();

                if (isSandboxRunning)
                {
                    _appMessageBox.DisplayError(this, StringResources.Error_Windows_Sandbox_Already_Running, false);
                    return;
                }

                var pair = default(X509CertPair);
                var fileList = (npkiFileListBox.DataSource as IEnumerable<string>)?.ToArray();

                if (mapNPKICert.Checked && fileList != null)
                {
                    var derFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".der", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    var keyFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".key", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                    if (File.Exists(derFilePath) && File.Exists(keyFilePath))
                        pair = _certPairScanner.CreateX509CertPair(derFilePath, keyFilePath);
                }

                var activeTabPage = siteCatalogTabControl.SelectedTab;
                var activeListBox = activeTabPage.Controls["SiteList"] as ListBox;
                var activeItems = activeListBox.SelectedItems.Cast<CatalogInternetService>().ToArray();

                var config = new TableClothConfiguration()
                {
                    CertPair = pair,
                    EnableMicrophone = enableMicrophone.Checked,
                    EnableWebCam = enableWebCam.Checked,
                    EnablePrinters = enablePrinters.Checked,
                    Packages = activeItems,
                };

                var tempPath = Path.Combine(_appStartup.AppDataDirectoryPath, $"bwsb_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}");
                var excludedFolderList = new List<SandboxMappedFolder>();
                var wsbFilePath = _sandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

                if (excludedFolderList.Any())
                    _appMessageBox.DisplayError(this, StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

                context.TemporaryDirectories.Add(tempPath);
                _sandboxLauncher.RunSandbox(this, tempPath, wsbFilePath);
            });

            return form;
        }

        private Form CreateCertSelectForm()
        {
            var form = new Form()
            {
                Text = LocalStringResources.CertSelectForm_Title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(480, 400),
                AutoScaleDimensions = new SizeF(96f, 96f),
                AutoScaleMode = AutoScaleMode.Dpi,
            };

            var tableLayout = new TableLayoutPanel()
            {
                Parent = form,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };

            /*
             * +--------------------+
             * |                    |
             * |         A          |
             * |                    |
             * +--------------------+
             * |         B          |
             * +--------------------+
             * 
             * */

            // Row A, Auto
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row B, 20%
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 0.2f));

            // Column 0, 100%
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1f));

            // Row A
            var dialogLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                AutoScroll = true,
            };
            tableLayout.SetCellPosition(dialogLayout, new TableLayoutPanelCellPosition(column: 0, row: 0));

            // Row B
            var actionLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 0, 10, 0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            tableLayout.SetCellPosition(actionLayout, new TableLayoutPanelCellPosition(column: 0, row: 1));

            dialogLayout.CreateLabel(LocalStringResources.CertSelectForm_InstructionLabel);
            dialogLayout.CreateLabel();

            var largeListViewImageList = new ImageList()
            {
                ImageSize = new Size(48, 48),
                ColorDepth = ColorDepth.Depth32Bit,
                TransparentColor = Color.Transparent,
            };
            largeListViewImageList.Images.Add(Image.FromStream(new MemoryStream(Convert.FromBase64String(GraphicResources.CertIcon))));

            var smallListViewImageList = new ImageList()
            {
                ImageSize = new Size(16, 16),
                ColorDepth = ColorDepth.Depth32Bit,
                TransparentColor = Color.Transparent,
            };
            smallListViewImageList.Images.Add(Image.FromStream(new MemoryStream(Convert.FromBase64String(GraphicResources.CertIcon))));

            var certListView = new ListView()
            {
                Parent = dialogLayout,
                View = View.Tile,
                Width = form.ClientSize.Width - 100,
                LargeImageList = largeListViewImageList,
                SmallImageList = smallListViewImageList,
            };

            var refreshButton = dialogLayout.CreateButton(LocalStringResources.CertSelectForm_RefreshButtonText);

            dialogLayout.CreateLabel();
            dialogLayout.CreateLabel(LocalStringResources.CertSelectForm_ManualInstructionLabelText);
            dialogLayout.CreateLabel();
            var browseCertPairButton = dialogLayout.CreateButton(LocalStringResources.CertSelectForm_OpenNpkiCertButton);

            var cancelButton = actionLayout.CreateButton(LocalStringResources.CertSelectForm_CancelButtonText, dialogResult: DialogResult.Cancel);
            var okayButton = actionLayout.CreateButton(LocalStringResources.CertSelectForm_OkayButtonText, dialogResult: DialogResult.OK);

            form.CancelButton = cancelButton;
            form.AcceptButton = okayButton;
            actionLayout.Height = (int)(okayButton.Height * 1.6f);

            certListView.ItemSelectionChanged += (_sender, _e) =>
            {
                form.Tag = _e.Item.Tag as X509CertPair;
            };

            certListView.MouseDoubleClick += (_sender, _e) =>
            {
                var info = certListView.HitTest(_e.X, _e.Y);
                var item = info.Item;

                if (item != null)
                    okayButton.PerformClick();
                else
                    certListView.SelectedItems.Clear();
            };

            refreshButton.AddClickEvent(x =>
            {
                var scannedPairs = _certPairScanner.ScanX509Pairs(_certPairScanner.GetCandidateDirectories());

                if (!scannedPairs.Any())
                    return;

                certListView.Items.Clear();

                foreach (var eachPair in scannedPairs)
                {
                    certListView.Items.Add(new ListViewItem(eachPair.ToString())
                    {
                        Tag = eachPair,
                        ImageIndex = 0,
                    });
                }

                if (certListView.Items.Count > 0)
                    certListView.Items[0].Selected = true;
            });

            browseCertPairButton.AddClickEvent(x =>
            {
                var npkiDirectoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "LocalLow", "NPKI");
                if (!Directory.Exists(npkiDirectoryPath))
                    npkiDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);

                var certFileBrowserDialog = new OpenFileDialog()
                {
                    Title = LocalStringResources.CertSelectForm_FileOpenDialog_Text,
                    Filter = LocalStringResources.CertSelectForm_FileOpenDialog_FilterText,
                    ReadOnlyChecked = true,
                    SupportMultiDottedExtensions = true,
                    DereferenceLinks = true,
                    Multiselect = true,
                    InitialDirectory = npkiDirectoryPath,
                };

                if (certFileBrowserDialog.ShowDialog(form) != DialogResult.OK)
                    return;

                var selectedFiles = certFileBrowserDialog.FileNames;
                var derFilePath = selectedFiles.Where(x => string.Equals(Path.GetExtension(x), ".der", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                var keyFilePath = selectedFiles.Where(x => string.Equals(Path.GetExtension(x), ".key", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (!File.Exists(derFilePath) || !File.Exists(keyFilePath))
                {
                    _appMessageBox.DisplayError(this, StringResources.Error_OpenDerAndKey_Simultaneously, true);
                    return;
                }

                form.Tag = _certPairScanner.CreateX509CertPair(derFilePath, keyFilePath);
                form.DialogResult = DialogResult.OK;
                form.Close();
            });

            form.Load += (_sender, _e) =>
            {
                refreshButton.PerformClick();
            };

            return form;
        }
    }

    // 메인 화면에 표시될 문자열들
    partial class LocalStringResources
    {
        internal static readonly string MainForm_Title
            = $"{StringResources.AppName} - 컴퓨터를 깨끗하게 사용하세요!";

        internal static readonly string MainForm_SelectOptionsLabelText
            = @"원하는 옵션을 선택해주세요.";

        internal static readonly string MainForm_MapNpkiCertButtonText
            = @"기존 공인인증서 파일 가져오기(&C)";

        internal static readonly string MainForm_BrowseButtonText
            = @"찾아보기(&B)...";

        internal static readonly string MainForm_UseMicrophoneCheckboxText
            = @"오디오 입력 사용하기(&A) - 개인 정보 노출에 주의하세요!";

        internal static readonly string MainForm_UseWebCameraCheckboxText
            = @"비디오 입력 사용하기(&V) - 개인 정보 노출에 주의하세요!";

        internal static readonly string MainForm_UsePrinterCheckboxText
            = @"프린터 같이 사용하기(&P)";

        internal static readonly string MainForm_SelectSiteLabelText
            = $"{StringResources.AppName} 위에서 접속할 사이트들을 선택해주세요. 사이트에서 필요한 프로그램들을 자동으로 설치해드려요.";

        internal static readonly string MainForm_SelectSiteLabelText_Alt
            = @"카탈로그 파일을 가져오지 못했어요! 그래도 샌드박스는 대신 실행해드려요.";

        internal static readonly string MainForm_JustRunItemText
            = @"그냥 실행해주세요.";

        internal static readonly string MainForm_AboutButtonText
            = @"정보";

        internal static readonly string MainForm_CloseButtonText
            = @"닫기";

        internal static readonly string MainForm_LaunchSandboxButtonText
            = @"샌드박스 실행";
    }

    // 인증서 검색 창에 표시될 문자열들
    partial class LocalStringResources
    {
        internal static readonly string CertSelectForm_Title
            = @"검색된 공인 인증서 선택";

        internal static readonly string CertSelectForm_InstructionLabel
            = @"검색된 공인 인증서가 다음과 같습니다. 다음 중 하나를 선택해주세요.";

        internal static readonly string CertSelectForm_RefreshButtonText
            = @"새로 고침(&R)";

        internal static readonly string CertSelectForm_ManualInstructionLabelText
            = @"원하는 인증서가 없다면, 직접 인증서 찾기 버튼을 눌러서 직접 DER 파일과 KEY 파일을 찾아주세요.";

        internal static readonly string CertSelectForm_OpenNpkiCertButton
            = @"직접 인증서 찾기(&B)...";

        internal static readonly string CertSelectForm_OkayButtonText
            = "확인";

        internal static readonly string CertSelectForm_CancelButtonText
            = "취소";

        internal static readonly string CertSelectForm_FileOpenDialog_Text
            = @"인증서 파일 (signCert.der, signPri.key) 열기";

        internal static readonly string CertSelectForm_FileOpenDialog_FilterText
            = @"인증서 파일 (*.der;*.key)|*.der;*.key|모든 파일|*.*";
    }
}
